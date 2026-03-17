# ShellSpecter: Functional Specifications & Requirements

## 1. Executive Summary
**ShellSpecter** is a high-performance, real-time Linux system observability dashboard designed for developers and power users. Built entirely on the .NET 10 ecosystem, it bypasses heavy CLI tools like `top` by reading directly from the Linux `/proc` virtual filesystem and hardware APIs. It provides a lightweight, real-time web interface for monitoring CPU, distributed GPUs, memory pressure, and process-level metrics.

## 2. Project Scope & Architecture
ShellSpecter uses a decoupled, daemon-and-dashboard architecture to support both single-node and multi-node environments.

* **The Specter (Daemon):** A headless ASP.NET Core Minimal API service deployed to target Linux machines. It acts as the telemetry agent, parsing `/proc` and hardware APIs, and streaming the data via WebSockets (SignalR).
* **The Seer (Dashboard):** A Blazor WebAssembly frontend that connects to one or multiple Specter daemons. It aggregates the streams into a unified, high-refresh-rate graphical interface.

## 3. Technology Stack
* **Runtime:** .NET 10 (compiled via Native AOT for zero-dependency Linux deployment).
* **Backend Agent:** ASP.NET Core 10 Minimal APIs, SignalR.
* **Frontend UI:** Blazor WebAssembly 10.
* **Hardware Interop:** P/Invoke wrappers for `libnvidia-ml.so` (NVML).
* **Styling:** CSS Grid/Flexbox with a dark-mode "Spectral" theme (high-contrast cyan/purple data visualizations).

## 4. Core Features & Telemetry (Phase 1)

### 4.1. Compute & Kernel Metrics
* **CPU Utilization:** Real-time sparkline rendering of `%user`, `%system`, `%iowait`, and `%idle` per logical core.
* **System Load:** 1, 5, and 15-minute load averages.
* **Thermal Throttling:** Detection of CPU frequency scaling via `/sys/devices/system/cpu/`.

### 4.2. Memory Subsystem
* **Physical RAM:** Breakdown of Total, Used, Shared, Buffers, Cached, and logically Available memory.
* **Swap Metrics:** Identification of thrashing via swap utilization rates.
* **Pressure Stall Information (PSI):** Hardware wait times parsed from `/proc/pressure/memory`.

### 4.3. Distributed GPU Monitoring
* **Multi-Node Support:** Ability to aggregate telemetry from multiple distinct physical machines (e.g., monitoring single RTX 3090s operating on separate hosts).
* **NVML Telemetry:** Real-time extraction of GPU Core load, VRAM allocation, current wattage, and thermal states.

### 4.4. I/O & Network
* **Disk Throughput:** Real-time KB/s read/write operations parsed via `/proc/diskstats`.
* **Network Interfaces:** Rx/Tx bandwidth streaming per active interface (e.g., `eth0`, `wlan0`).

### 4.5. "The Spirit List" (Interactive Task Manager)
* **Differential Process Scrape:** Efficient delta calculations of `/proc/[PID]/stat` to determine accurate per-process CPU percentage without shelling out to `ps`.
* **Process Hierarchies:** Parent-child thread grouping (tree-view) to identify resource-hogging application pools.

## 5. Active Management Capabilities (Phase 2)
While Phase 1 focuses on read-only observability, the architecture must support state-changing commands in Phase 2:
* **The Reaper:** A UI trigger to send `SIGTERM` (15) or `SIGKILL` (9) to misbehaving processes.
* **Priority Tuning:** Real-time adjustments to a process's `nice` value.
* **Daemon Management:** Ability to restart associated `systemd` services directly from the dashboard.

## 6. Performance & Engineering Constraints
* **Allocation-Free Parsing:** The Specter daemon must utilize `ReadOnlySpan<char>` and `Memory<T>` when parsing `/proc` text buffers to ensure garbage collection does not cause latency spikes in the telemetry stream.
* **Binary Size:** The natively compiled AOT daemon must remain under 15MB.
* **Update Frequency:** The system must support a reliable 500ms refresh rate over local area networks.
* **Security:** The dashboard will not implement native authentication, relying instead on Reverse Proxy Headers (e.g., `X-Remote-User`) to integrate with established network perimeters (Authelia, Nginx Proxy Manager, Cloudflare Tunnels).

## 7. Deployment Strategy
* **Standalone AOT:** Distributed as a single compiled executable (`linux-x64`).
* **Systemd Integration:** Shipped with a standardized `shellspecter.service` file for host-level daemonization.
* **Containerization:** Provided via a minimal `docker-compose.yml` configured with necessary volume mounts (`/proc:ro`) and device pass-throughs (`/dev/nvidia0`).




Authentication
Going with Linux PAM (Pluggable Authentication Modules) gives ShellSpecter a truly native feel. It allows users to log in with their existing OS credentials and respects Linux user management out of the box. 

Since you want to validate against the OS but don't want to run a PAM check on every single HTTP/SignalR request (which would be incredibly slow), the logical architecture is: **PAM for initial validation -> JWT for session management.**

Here is the implementation roadmap for integrating PAM into your .NET 10 ASP.NET Core backend.

### 1. The Architecture Flow
1. **The Login Request:** The Blazor frontend sends a `POST /api/auth/login` containing the Linux username and password.
2. **The PAM Interop:** The C# backend uses P/Invoke (`DllImport`) to pass these credentials to `libpam.so`.
3. **The Token Handoff:** If PAM returns `PAM_SUCCESS`, ASP.NET Core generates a standard JWT (JSON Web Token) and returns it to the client.
4. **The SignalR Connection:** The Blazor client passes the JWT as a Bearer token to connect to the SignalR telemetry hub.

---

### 2. The Native C# PAM Wrapper (P/Invoke)

Working with PAM in C# requires careful marshaling because `pam_authenticate` uses a callback function (`pam_conv`) to ask the application for the password. If the Garbage Collector moves this delegate during the unmanaged call, the daemon will segfault.

Here is the robust, safe way to define the interop:

```csharp
using System;
using System.Runtime.InteropServices;

namespace ShellSpecter.Security
{
    public static class PamAuth
    {
        private const string PamLibrary = "libpam.so.0";

        // PAM Return Codes
        private const int PAM_SUCCESS = 0;

        // PAM Conversation Message Styles
        private const int PAM_PROMPT_ECHO_OFF = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct pam_message
        {
            public int msg_style;
            public IntPtr msg;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct pam_response
        {
            public IntPtr resp;
            public int resp_retcode;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct pam_conv
        {
            public PamConversationDelegate conv;
            public IntPtr appdata_ptr;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int PamConversationDelegate(
            int num_msg, 
            IntPtr msg, 
            out IntPtr resp, 
            IntPtr appdata_ptr);

        [DllImport(PamLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int pam_start(string service_name, string user, ref pam_conv pam_conversation, out IntPtr pamh);

        [DllImport(PamLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int pam_authenticate(IntPtr pamh, int flags);

        [DllImport(PamLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int pam_end(IntPtr pamh, int pam_status);

        public static bool Authenticate(string username, string password)
        {
            IntPtr pamh = IntPtr.Zero;

            // The conversation callback that libpam will invoke to get the password
            PamConversationDelegate pamConvDelegate = (int num_msg, IntPtr msg, out IntPtr resp, IntPtr appdata) =>
            {
                // Allocate memory for the response array
                resp = Marshal.AllocHGlobal(IntPtr.Size * num_msg);
                
                for (int i = 0; i < num_msg; i++)
                {
                    // In a real implementation, check the msg_style to ensure it's asking for a password (PAM_PROMPT_ECHO_OFF)
                    IntPtr pResponse = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(pam_response)));
                    var responseStruct = new pam_response
                    {
                        resp = Marshal.StringToHGlobalAnsi(password),
                        resp_retcode = 0
                    };
                    Marshal.StructureToPtr(responseStruct, pResponse, false);
                    Marshal.WriteIntPtr(resp, i * IntPtr.Size, pResponse);
                }
                return PAM_SUCCESS;
            };

            var conv = new pam_conv { conv = pamConvDelegate, appdata_ptr = IntPtr.Zero };

            try
            {
                // Initialize PAM session for the standard "login" service
                int startResult = pam_start("login", username, ref conv, out pamh);
                if (startResult != PAM_SUCCESS) return false;

                // Attempt authentication
                int authResult = pam_authenticate(pamh, 0);
                
                return authResult == PAM_SUCCESS;
            }
            finally
            {
                if (pamh != IntPtr.Zero)
                {
                    pam_end(pamh, 0); // Cleanup the PAM handle
                }
            }
        }
    }
}
```

---

### 3. Execution Requirements & Security Context

Because you are checking local system passwords, the Linux OS will not allow a standard user process to arbitrarily verify credentials. You have two options for deploying the ShellSpecter daemon:

**Option A: Run as Root (Easiest, but highest risk)**
If ShellSpecter runs under the `root` user via systemd, PAM authentication will succeed without modification. 

**Option B: Shadow Group / Capabilities (Best Practice)**
To run the daemon as a standard user (e.g., a dedicated `specter` user) while still allowing PAM to read password hashes:
1. Add the daemon's user to the `shadow` group so it can read `/etc/shadow`.
   ```bash
   usermod -aG shadow specter
   ```
2. Alternatively, grant the binary the specific Linux capability to read files bypassing standard DAC (Discretionary Access Control):
   ```bash
   setcap cap_dac_read_search+ep ./ShellSpecter
   ```

### 4. Group-Based Authorization (Optional but Recommended)
Since you are using PAM, you can easily restrict dashboard access to specific Linux groups. For example, you can add a check in your C# login endpoint to ensure the authenticated user belongs to the `wheel` (admin) or a custom `specter-admins` group by reading `/etc/group` before issuing the JWT.

**Would you like the ASP.NET Core endpoint code that wires this PAM validator up to generate and return the JWT for the Blazor client?**
