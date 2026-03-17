# ShellSpecter 👻

**Real-time Linux system observability dashboard** built with .NET 10, SignalR, and Blazor WebAssembly.

A decoupled daemon-and-dashboard architecture: deploy the **Specter** agent to your Linux machines and monitor them from the **Seer** dashboard in your browser — with live-streaming telemetry at 500ms intervals.

![Dashboard Screenshot](docs/dashboard.png)

---

## ✨ Features

- **CPU** — Per-core utilization sparklines, frequency, temperature, load averages, user/system/iowait breakdown
- **Memory** — Used/buffers/cached/available stacked bar, swap usage, PSI pressure stall metrics
- **GPU** — NVIDIA GPU monitoring via NVML (core load, VRAM, power draw, temperature)
- **Disk I/O** — Per-device read/write throughput with auto-scaling bars
- **Network** — Per-interface Rx/Tx bandwidth
- **Processes** — "Spirit List" sortable process table with inline CPU bars
- **System Details** — Hostname, IP, OS, kernel, CPU model, RAM, uptime, disk volumes with usage bars
- **Multi-Node** — Connect to multiple Specter daemons from a single dashboard
- **Dark Theme** — "Spectral" cyan/purple aesthetic with glassmorphism and micro-animations

## 🏗️ Architecture

```
┌──────────────────────┐          ┌──────────────────────┐
│   Specter Daemon     │          │    Seer Dashboard     │
│  (ASP.NET Core API)  │◄────────►│   (Blazor WASM)       │
│                      │ SignalR  │                       │
│  • /proc parsers     │ WebSocket│  • Canvas charts      │
│  • NVML GPU interop  │  500ms   │  • CSS Grid layout    │
│  • PAM auth + JWT    │          │  • Multi-node manager │
│  • Mock data mode    │          │  • Dark Spectral theme│
└──────────────────────┘          └───────────────────────┘
```

## 📋 Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Linux host for production (reads `/proc`, `/sys`, NVML)
- Windows/macOS for development (runs in mock data mode)

## 🚀 Quick Start

### Windows (Development / Demo Mode)

```bash
# Clone the repo
git clone https://github.com/ExythAI/ShellSpecter.git
cd ShellSpecter

# Option 1: Use the launcher script
run.bat

# Option 2: Manual start (two terminals)
dotnet run --project src/ShellSpecter.Specter          # Terminal 1 — daemon on :5050
dotnet run --project src/ShellSpecter.Seer             # Terminal 2 — dashboard
```

Then open the URL shown in Terminal 2 and login with **any username/password** (mock mode accepts all credentials).

### Linux (Development — Live Data)

```bash
# 1. Install .NET 10 SDK (if not already installed)
#    See https://dotnet.microsoft.com/download/dotnet/10.0
#    For Ubuntu/Debian:
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0
export PATH="$HOME/.dotnet:$PATH"

# 2. Clone the repo
git clone https://github.com/ExythAI/ShellSpecter.git
cd ShellSpecter

# 3. Start the Specter daemon (reads real /proc data on Linux)
dotnet run --project src/ShellSpecter.Specter &

# 4. Start the Seer dashboard
dotnet run --project src/ShellSpecter.Seer --urls "http://0.0.0.0:5051" &

# 5. Open in browser
#    Navigate to http://<your-linux-ip>:5051
#    Login with your Linux system username and password (PAM auth)
```

> **Note:** On Linux the daemon uses real PAM authentication — login with actual system user credentials.

### Linux (Production — Systemd Service)

**One-liner deploy** (installs .NET SDK, builds, configures systemd, firewall, JWT secret):

```bash
curl -sSL https://raw.githubusercontent.com/ExythAI/ShellSpecter/master/deploy.sh | sudo bash
```

Or clone first and run locally:

```bash
git clone https://github.com/ExythAI/ShellSpecter.git
cd ShellSpecter
sudo ./deploy.sh          # Full production install
# or
./deploy.sh --dev         # Dev mode (foreground, no systemd)
```

<details>
<summary>Manual step-by-step (click to expand)</summary>
# 1. Clone and build a self-contained release binary
git clone https://github.com/ExythAI/ShellSpecter.git
cd ShellSpecter
dotnet publish src/ShellSpecter.Specter -c Release -r linux-x64 --self-contained true -o /opt/shellspecter

# 2. Build the Seer dashboard and copy to the daemon's wwwroot
dotnet publish src/ShellSpecter.Seer -c Release -o /tmp/seer-publish
cp -r /tmp/seer-publish/wwwroot /opt/shellspecter/wwwroot

# 3. Create a service user
sudo useradd -r -s /sbin/nologin specter

# 4. Set ownership and permissions
sudo chown -R specter:specter /opt/shellspecter
sudo chmod +x /opt/shellspecter/ShellSpecter.Specter

# 5. Update the JWT secret in production
sudo nano /opt/shellspecter/appsettings.json
# Change "Jwt:Secret" to a strong random string
# Update "AllowedOrigins" to your dashboard URL if hosting separately

# 6. Install the systemd service
sudo cp shellspecter.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now shellspecter

# 7. Check status
sudo systemctl status shellspecter
sudo journalctl -u shellspecter -f

# 8. Access the dashboard
#    Open http://<server-ip>:5050 in your browser
#    Login with your Linux system credentials
```

</details>

### Linux (Native AOT — Minimal Binary)

```bash
# Produces a single ~15MB binary with no .NET runtime dependency
dotnet publish src/ShellSpecter.Specter \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishAot=true \
  -p:StripSymbols=true \
  -o /opt/shellspecter
```

### Docker

```bash
docker compose up -d
# Dashboard available at http://localhost:5050
```

## 📁 Project Structure

```
ShellSpecter/
├── src/
│   ├── ShellSpecter.Shared/       # Shared DTOs (SystemSnapshot, AuthModels, SystemInfo)
│   ├── ShellSpecter.Specter/      # Daemon — telemetry collection agent
│   │   ├── Parsers/               # /proc and /sys parsers (allocation-free)
│   │   ├── Gpu/                   # NVML P/Invoke interop
│   │   ├── Security/              # PAM auth + JWT generation
│   │   ├── Hubs/                  # SignalR TelemetryHub
│   │   └── Services/              # Background collector + broadcaster
│   └── ShellSpecter.Seer/         # Dashboard — Blazor WebAssembly frontend
│       ├── Components/            # CpuPanel, MemoryPanel, GpuPanel, etc.
│       ├── Pages/                 # Login, Dashboard
│       ├── Layout/                # MainLayout with sidebar
│       ├── Services/              # AuthService, NodeManager
│       └── wwwroot/               # CSS theme, JS charts, index.html
├── shellspecter.service            # systemd unit file
├── docker-compose.yml              # Docker deployment
├── run.bat                         # Windows dev launcher
└── ShellSpecter.slnx               # .NET solution
```

## 🔧 Configuration

### Specter Daemon (`appsettings.json`)

| Setting | Default | Description |
|---------|---------|-------------|
| `Jwt:Secret` | (built-in) | HMAC-SHA256 signing key — **change in production** |
| `AllowedOrigins` | `http://localhost:*` | CORS allowed origins for the dashboard |
| `ASPNETCORE_URLS` | `http://[::]:5050` | Listen address |

### Authentication

- **Linux**: Uses PAM — login with real system credentials
- **Windows/macOS**: Mock mode — any credentials accepted for development

## 🎨 Theme

The "Spectral" dark theme features:
- Deep navy/void background with cyan (#00E5FF) and purple (#B388FF) accents
- Glassmorphism card effects with subtle glow on hover
- JetBrains Mono for data, Inter for UI text
- Canvas-rendered sparklines and arc gauges
- Responsive CSS Grid layout

## 📡 API Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/api/auth/login` | No | Authenticate, receive JWT |
| `GET` | `/api/health` | No | Health check |
| | `/hub/telemetry` | JWT | SignalR hub — call `StreamTelemetry` |

## ⚡ Performance

- **Allocation-free parsers** using `ReadOnlySpan<char>` for `/proc` file parsing
- **Delta calculations** for CPU, disk, and network metrics (rate-based, not cumulative)
- **500ms collection interval** via `PeriodicTimer`
- **Native AOT** compatible for minimal binary size on Linux
- **Channel-based** SignalR streaming for backpressure support

## 📜 License

MIT

---

*Built with 👻 by ExythAI*
