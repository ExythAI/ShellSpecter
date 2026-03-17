#!/usr/bin/env bash
set -euo pipefail

# ============================================================
#  ShellSpecter — Automated Linux Deployment Script
#  Usage: sudo ./deploy.sh [--dev]
#    --dev    Run in development mode (no systemd, foreground)
# ============================================================

INSTALL_DIR="/opt/shellspecter"
SERVICE_USER="specter"
SERVICE_NAME="shellspecter"
LISTEN_PORT="5050"
REPO_URL="https://github.com/ExythAI/ShellSpecter.git"
DOTNET_CHANNEL="10.0"

RED='\033[0;31m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
NC='\033[0m'

log()  { echo -e "${CYAN}[ShellSpecter]${NC} $1"; }
ok()   { echo -e "${GREEN}  ✔${NC} $1"; }
warn() { echo -e "${YELLOW}  ⚠${NC} $1"; }
fail() { echo -e "${RED}  ✘ $1${NC}"; exit 1; }

DEV_MODE=false
[[ "${1:-}" == "--dev" ]] && DEV_MODE=true

# ── Step 1: Check prerequisites ──────────────────────────────
log "Checking prerequisites..."

if ! command -v git &>/dev/null; then
    log "Installing git..."
    if command -v apt-get &>/dev/null; then
        apt-get update -qq && apt-get install -y -qq git
    elif command -v dnf &>/dev/null; then
        dnf install -y -q git
    elif command -v yum &>/dev/null; then
        yum install -y -q git
    else
        fail "Cannot install git — please install it manually"
    fi
fi
ok "git"

# ── Step 2: Install .NET SDK if missing ───────────────────────
if ! command -v dotnet &>/dev/null || ! dotnet --list-sdks 2>/dev/null | grep -q "^${DOTNET_CHANNEL}"; then
    log "Installing .NET ${DOTNET_CHANNEL} SDK..."
    
    TMPDIR=$(mktemp -d)
    curl -sSL https://dot.net/v1/dotnet-install.sh -o "$TMPDIR/dotnet-install.sh"
    chmod +x "$TMPDIR/dotnet-install.sh"
    
    if [[ $EUID -eq 0 ]]; then
        "$TMPDIR/dotnet-install.sh" --channel "$DOTNET_CHANNEL" --install-dir /usr/share/dotnet
        ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
    else
        "$TMPDIR/dotnet-install.sh" --channel "$DOTNET_CHANNEL"
        export PATH="$HOME/.dotnet:$PATH"
    fi
    
    rm -rf "$TMPDIR"
    ok ".NET SDK installed"
else
    ok ".NET $(dotnet --version)"
fi

# ── Step 3: Clone or update the repo ─────────────────────────
WORK_DIR=$(mktemp -d)
log "Cloning ShellSpecter to ${WORK_DIR}..."
git clone --depth 1 "$REPO_URL" "$WORK_DIR/ShellSpecter"
cd "$WORK_DIR/ShellSpecter"
ok "Source cloned"

# ── Step 4: Build ─────────────────────────────────────────────
log "Building Specter daemon (Release)..."
dotnet publish src/ShellSpecter.Specter \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -o "$WORK_DIR/publish" \
    --verbosity quiet
ok "Specter daemon built"

log "Building Seer dashboard..."
dotnet publish src/ShellSpecter.Seer \
    -c Release \
    -o "$WORK_DIR/seer-publish" \
    --verbosity quiet
ok "Seer dashboard built"

# ── Dev mode: just run it ─────────────────────────────────────
if [[ "$DEV_MODE" == true ]]; then
    log "Starting in development mode (Ctrl+C to stop)..."
    cp -r "$WORK_DIR/seer-publish/wwwroot" "$WORK_DIR/publish/wwwroot" 2>/dev/null || true
    echo ""
    echo -e "${GREEN}═══════════════════════════════════════════════════${NC}"
    echo -e "  ShellSpecter running at ${CYAN}http://0.0.0.0:${LISTEN_PORT}${NC}"
    echo -e "  Login with your Linux system credentials"
    echo -e "${GREEN}═══════════════════════════════════════════════════${NC}"
    echo ""
    ASPNETCORE_URLS="http://0.0.0.0:${LISTEN_PORT}" "$WORK_DIR/publish/ShellSpecter.Specter"
    exit 0
fi

# ── Step 5: Production install ────────────────────────────────
if [[ $EUID -ne 0 ]]; then
    fail "Production install requires root. Run with sudo or use --dev for dev mode."
fi

log "Installing to ${INSTALL_DIR}..."

# Stop existing service if running
systemctl stop "$SERVICE_NAME" 2>/dev/null || true

# Copy binaries
mkdir -p "$INSTALL_DIR"
cp -r "$WORK_DIR/publish/"* "$INSTALL_DIR/"

# Clean old wwwroot and copy fresh Seer WASM files
rm -rf "$INSTALL_DIR/wwwroot" 2>/dev/null || true
cp -r "$WORK_DIR/seer-publish/wwwroot" "$INSTALL_DIR/wwwroot" 2>/dev/null || true

# .NET 10 fingerprints _framework files (e.g. blazor.webassembly.{hash}.js)
# Copy fingerprinted files to their original names that index.html expects
# Also remove pre-compressed .br/.gz variants to prevent content negotiation issues
if [ -d "$INSTALL_DIR/wwwroot/_framework" ]; then
    log "Fixing framework file names..."
    cd "$INSTALL_DIR/wwwroot/_framework"

    # Remove pre-compressed files — UseStaticFiles can misserve them
    rm -f *.br *.gz 2>/dev/null || true

    # Copy fingerprinted files to their original names
    # Pattern: name.fingerprint.ext -> name.ext
    for f in *.*.*; do
        [ -f "$f" ] || continue
        ext="${f##*.}"
        base="${f%.*}"          # Remove last extension: name.hash
        name="${base%.*}"       # Remove hash: name
        orig="${name}.${ext}"   # Original name: name.ext

        if [ "$orig" != "$f" ]; then
            cp -f "$f" "$orig"
        fi
    done
    cd - >/dev/null
    ok "Framework files fixed"
fi
ok "Binaries installed to ${INSTALL_DIR}"

# ── Step 6: Generate a secure JWT secret ──────────────────────
log "Generating secure JWT secret..."
JWT_SECRET=$(head -c 48 /dev/urandom | base64 | tr -d '\n')

# Update appsettings.json with the new secret
if command -v python3 &>/dev/null; then
    python3 -c "
import json, sys
with open('$INSTALL_DIR/appsettings.json', 'r') as f:
    cfg = json.load(f)
cfg.setdefault('Jwt', {})['Secret'] = '$JWT_SECRET'
with open('$INSTALL_DIR/appsettings.json', 'w') as f:
    json.dump(cfg, f, indent=2)
"
    ok "JWT secret configured"
elif command -v sed &>/dev/null; then
    sed -i "s|ShellSpecter-Default-Secret-Change-Me-In-Production-2024!|${JWT_SECRET}|g" \
        "$INSTALL_DIR/appsettings.json" 2>/dev/null || true
    ok "JWT secret configured (sed fallback)"
else
    warn "Could not auto-configure JWT secret — edit ${INSTALL_DIR}/appsettings.json manually"
fi

# ── Step 7: Create service user ───────────────────────────────
if ! id "$SERVICE_USER" &>/dev/null; then
    log "Creating service user '${SERVICE_USER}'..."
    useradd -r -s /sbin/nologin "$SERVICE_USER"
    ok "User created"
else
    ok "User '${SERVICE_USER}' already exists"
fi

chown -R "$SERVICE_USER:$SERVICE_USER" "$INSTALL_DIR"
chmod +x "$INSTALL_DIR/ShellSpecter.Specter"
ok "Permissions set"

# ── Step 8: Install systemd service ──────────────────────────
log "Installing systemd service..."
cp "$WORK_DIR/ShellSpecter/shellspecter.service" /etc/systemd/system/
systemctl daemon-reload
systemctl enable "$SERVICE_NAME"
ok "Service installed and enabled"

# ── Step 9: Configure firewall (optional) ─────────────────────
if command -v ufw &>/dev/null; then
    log "Opening port ${LISTEN_PORT} in UFW..."
    ufw allow "$LISTEN_PORT/tcp" >/dev/null 2>&1 || true
    ok "Firewall rule added"
elif command -v firewall-cmd &>/dev/null; then
    log "Opening port ${LISTEN_PORT} in firewalld..."
    firewall-cmd --permanent --add-port="${LISTEN_PORT}/tcp" >/dev/null 2>&1 || true
    firewall-cmd --reload >/dev/null 2>&1 || true
    ok "Firewall rule added"
fi

# ── Step 10: Start the service ─────────────────────────────────
log "Starting ShellSpecter..."
systemctl start "$SERVICE_NAME"

sleep 2
if systemctl is-active --quiet "$SERVICE_NAME"; then
    ok "Service is running"
else
    warn "Service may not have started — check: journalctl -u ${SERVICE_NAME} -f"
fi

# ── Cleanup ───────────────────────────────────────────────────
rm -rf "$WORK_DIR"

# ── Done ──────────────────────────────────────────────────────
LOCAL_IP=$(hostname -I 2>/dev/null | awk '{print $1}' || echo "localhost")
echo ""
echo -e "${GREEN}═══════════════════════════════════════════════════════════${NC}"
echo -e "  ${GREEN}✔ ShellSpecter deployed successfully!${NC}"
echo -e ""
echo -e "  Dashboard:  ${CYAN}http://${LOCAL_IP}:${LISTEN_PORT}${NC}"
echo -e "  Login:      Your Linux system credentials (PAM)"
echo -e ""
echo -e "  Manage:     sudo systemctl {start|stop|restart} ${SERVICE_NAME}"
echo -e "  Logs:       sudo journalctl -u ${SERVICE_NAME} -f"
echo -e "  Config:     ${INSTALL_DIR}/appsettings.json"
echo -e "${GREEN}═══════════════════════════════════════════════════════════${NC}"
echo ""
