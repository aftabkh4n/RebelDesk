# Self-hosting RebelDesk

This walks a fresh operator through running the server, coturn, one Windows agent, and the browser client. It is intentionally opinionated — the goal is a working session on your own hardware within an hour, not a general architecture guide.

Read `docs/SECURITY_MODEL.md` before you point this at the public internet.

## Prerequisites

- One machine to run the server + coturn. Linux or Docker Desktop on Windows.
- One Windows 10/11 machine to run the agent.
- One machine with Chrome or Edge to be the viewer.
- Ports: 8080/tcp (server), 3478/udp+tcp (STUN/TURN), 49160-49200/udp (TURN relay).

Development also needs .NET 10 SDK, Node 20+, and Docker.

## 1. Server + coturn (with Docker Compose)

```bash
cd RebelDesk
export REBELDESK_JWT_KEY="$(openssl rand -base64 48)"
export REBELDESK_TURN_SECRET="$(openssl rand -base64 32)"
export TURN_HOSTNAME=turn.example.com  # or your public IP

docker compose -f docker/docker-compose.yml up -d --build
curl -fsS http://localhost:8080/healthz
```

Notes:
- The compose file runs coturn with `network_mode: host` so UDP hole-punching works. On a machine that cannot use host networking (Docker Desktop on Windows), delete that line and publish the ports listed in `docker/coturn/turnserver.conf` instead, then set `external-ip` inside that config.
- `REBELDESK_TURN_SECRET` must match `static-auth-secret` in `docker/coturn/turnserver.conf`. Edit both.
- The compose stack writes the SQLite database to a named volume (`server-data`). Back it up.

## 2. Register the first user

Open `http://localhost:5173` (dev) or wherever you deploy the client. Register the first account. There is no admin distinction yet in v0.1 — the first user is a user.

## 3. Pair a Windows host

1. On the Windows machine, install .NET 10 desktop runtime and build the agent:
   ```powershell
   git clone https://github.com/aftabkh4n/RebelDesk.git
   cd RebelDesk
   dotnet publish agent/RebelDesk.Agent.csproj -c Release -r win-x64 -o C:\RebelDesk
   ```
2. In the browser, go to **Devices → Pair a new device**, generate a pairing code.
3. On the Windows machine, run:
   ```powershell
   $env:ServerUrl = "http://your-server:8080"
   C:\RebelDesk\RebelDesk.Agent.exe
   ```
4. Paste the pairing code when prompted. The agent stores its device credentials in `device.json` next to the executable.

## 4. Start a session

1. Browser: pick the paired device from the list, click **Start session**.
2. Agent host: a topmost dialog appears with the requesting IP. Click **Approve**.
3. Video should start within a few seconds. Click the video area once to enable input.

## 5. Verifying performance

See `docs/PERFORMANCE.md` for the measurement procedure.

## Production checklist

Before letting anyone from outside your LAN connect:

- [ ] Terminate TLS in front of the server (nginx, Caddy, Traefik).
- [ ] Move `REBELDESK_JWT_KEY` and `REBELDESK_TURN_SECRET` into a secret store, not `.env`.
- [ ] Give coturn a real cert and enable `tls-listening-port`.
- [ ] Rate-limit `/api/auth/*` and `/api/pairing/consume` at the reverse proxy.
- [ ] Set `Cors:Origins` in `appsettings.Production.json` to only your client origin.
- [ ] Rotate the JWT signing key on a schedule.
- [ ] Read `docs/SECURITY_MODEL.md` end to end.
