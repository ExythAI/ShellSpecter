# ShellSpecter 👻

**Real-time Linux system observability dashboard** built with .NET 10, SignalR, and Blazor WebAssembly.

A decoupled daemon-and-dashboard architecture: deploy the **Specter** agent to your Linux machines and monitor them from the **Seer** dashboard in your browser — with live-streaming telemetry at 500ms intervals.


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
<summary>What deploy.sh does (click to expand)</summary>

1. Installs .NET 10 SDK if missing
2. Clones and builds both Specter and Seer in Release mode
3. Copies binaries to `/opt/shellspecter/`
4. Fixes .NET 10 fingerprinted framework files for static serving
5. Generates a secure random JWT secret
6. Creates a `specter` service user with PAM access
7. Installs and enables the systemd service
8. Opens firewall port 5050 (UFW or firewalld)
9. Starts the service and prints the dashboard URL

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
├── deploy.sh                       # Automated Linux deploy script
├── shellspecter.service            # systemd unit file
├── docker-compose.yml              # Docker deployment
├── run.bat                         # Windows dev launcher
└── ShellSpecter.slnx               # .NET solution
```

## 🔧 Configuration

### Specter Daemon (`appsettings.json`)

| Setting | Default | Description |
|---------|---------|-------------|
| `Jwt:Secret` | (auto-generated) | HMAC-SHA256 signing key — auto-set by `deploy.sh` |
| `AllowedOrigins` | `http://localhost:*` | CORS allowed origins for the dashboard |
| `ASPNETCORE_URLS` | `http://0.0.0.0:5050` | Listen address |

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `SHELLSPECTER_ALLOW_ANY_LOGIN` | `true` | Set to `false` to require PAM authentication |
| `DOTNET_ENVIRONMENT` | `Production` | .NET environment |

### Authentication

- **Linux (PAM)**: Set `SHELLSPECTER_ALLOW_ANY_LOGIN=false` in the systemd service, then login with real system credentials
- **Linux (default)**: Accepts any non-empty credentials (for easy initial setup)
- **Windows/macOS**: Mock mode — any credentials accepted for development

To enable PAM auth:
```bash
sudo systemctl edit shellspecter
# Add: Environment=SHELLSPECTER_ALLOW_ANY_LOGIN=false
sudo systemctl restart shellspecter
```

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

## 🔍 Troubleshooting

| Problem | Solution |
|---------|----------|
| Service won't start | `sudo journalctl -u shellspecter -f` for logs |
| Login returns 401 | Check `SHELLSPECTER_ALLOW_ANY_LOGIN` is `true`, or verify PAM setup |
| Blank page after login | Hard refresh (Ctrl+Shift+R) to clear cached WASM files |
| Fonts not loading | Server can't reach Google Fonts — cosmetic only, system fonts used as fallback |
| Port 5050 not accessible | Check firewall: `sudo ufw allow 5050/tcp` |

## 📜 License

MIT

---

*Built with 👻 by ExythAI*
