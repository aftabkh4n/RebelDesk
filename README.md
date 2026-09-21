# RebelDesk

Self-hosted, open-source remote desktop with browser access. Windows host today; other operating systems and clients later.

**Maturity: pre-alpha, v0.1 scaffold.** The vertical slice (capture → WebRTC → browser → input) is under active development. Read `STATUS.md` for exactly what has been executed end-to-end vs what has only been compiled or scaffolded. Do not deploy this on the public internet yet.

## What RebelDesk is

- One authenticated user opens a browser tab (Chrome or Edge) and views the desktop of a Windows machine they own.
- The Windows machine runs a small agent that captures the screen, encodes H.264, and streams over WebRTC.
- All coordination goes through a self-hosted ASP.NET Core server plus a coturn TURN/STUN relay.
- Every incoming session must be approved on the host by a local prompt. Session end from either side revokes control immediately.

## What RebelDesk is not (yet)

- No unattended access.
- No macOS, Linux, iOS, Android agents.
- No file transfer, no clipboard sync, no audio, no multi-monitor.
- No mobile clients.
- No hosted service — you run every component yourself.

## Architecture

```
  Browser (Chrome/Edge)                    Windows Host
  ┌─────────────────┐                     ┌─────────────────────┐
  │ React + TS      │                     │ RebelDesk.Agent     │
  │ RTCPeerConn     │◄───── WebRTC ─────► │ DXGI Duplication    │
  │ input encoder   │       (H.264 + DC)  │ ffmpeg H.264 encode │
  └────────┬────────┘                     │ SendInput injector  │
           │                              │ WPF approval prompt │
           │                              └──────────┬──────────┘
           │  HTTPS + SignalR (JSON signaling)       │
           │                                         │
           ▼                                         ▼
        ┌────────────────────────────────────────────────┐
        │ RebelDesk.Server (ASP.NET Core)                │
        │  - accounts, device pairing, session auth      │
        │  - SignalR SignalingHub (SDP + ICE relay)      │
        │  - short-lived TURN credentials (HMAC-SHA1)    │
        │  - SQLite (default), Postgres (later)          │
        └────────────────────────────────────────────────┘
                              │
                              ▼
                     ┌───────────────────┐
                     │ coturn 4.18       │
                     │ STUN + TURN over  │
                     │ UDP/TCP/TLS       │
                     └───────────────────┘
```

Signaling and API traffic never carry video frames. Video and input flow only over the WebRTC peer connection (DTLS-SRTP for media, DTLS + SCTP for the input data channel).

## Repository layout

```
RebelDesk/
  server/                    ASP.NET Core 10 (accounts, signaling, TURN creds)
  agent/                     .NET 10 Windows worker + WPF approval UI
  client/                    Vite + React 18 + TS browser client
  docker/                    docker-compose (server + coturn)
  docs/                      architecture, security, performance notes
  scripts/                   dev launchers (PowerShell)
  .github/                   CI, issue and PR templates
  PROJECT_SPEC.md            source-of-truth requirements
  STATUS.md                  what actually runs, what does not
  ROADMAP.md                 milestone plan
  AGENTS.md                  deferred agent workflow definitions
```

## Quick start (local development)

Prerequisites: .NET 10 SDK, Node 20+, Docker Desktop, ffmpeg 6+ on `PATH` (Windows agent uses ffmpeg shared libraries).

```powershell
# 1. Start coturn + server (in docker/)
docker compose -f docker/docker-compose.yml up -d

# 2. Or run the server directly for development
dotnet run --project server/RebelDesk.Server

# 3. Browser client
cd client
npm install
npm run dev

# 4. Windows agent (only on the machine you want to control)
dotnet run --project agent/RebelDesk.Agent
```

Detailed self-hosting is in [`docs/SELF_HOSTING.md`](docs/SELF_HOSTING.md). Security posture and trust boundaries in [`docs/SECURITY_MODEL.md`](docs/SECURITY_MODEL.md). Performance measurement method in [`docs/PERFORMANCE.md`](docs/PERFORMANCE.md).

## Localization

Interface strings ship in `client/src/i18n/locales/{en,de,fr,nl,ar}.json`. Arabic layout mirrors via `dir="rtl"` on `<html>`. Browser language is auto-detected; user preference is persisted in `localStorage`. Machine-generated strings are marked with a `needs-review` comment in each locale file and must be replaced by a native speaker before a stable release.

Interface language is decoupled from keyboard layout. Scancode-based key events are sent from the browser and injected on the host so non-US layouts work regardless of interface language.

## Security posture (short version)

- Every device operation is authorized by a bearer token bound to a session or a paired device key.
- Pairing tokens expire in 15 minutes and are single-use.
- TURN credentials are HMAC-SHA1 short-lived (`use-auth-secret`).
- Input injection is only enabled while a session is `active` and `approved`; held keys and mouse buttons are released on any disconnect event, including network drop.
- Clipboard and file transfer are unimplemented and reject any attempt at the transport layer.
- The agent cannot capture the secure desktop (UAC dialogs, lock screen). Documented in `docs/SECURITY_MODEL.md`.

Report vulnerabilities per `SECURITY.md`.

## Licence

AGPL-3.0. See `LICENSE` and `NOTICE`. If you self-host RebelDesk and let other users connect to it over a network, AGPL requires you to offer them the source of your running version. This matches the self-hosted, single-owner deployment model RebelDesk targets. Reach out via an issue if this does not match your intended use.

## Trademark / naming

"RebelDesk" is a working name chosen for this project. No trademark search or domain acquisition has been performed. If you plan to redistribute or run a public service under this name, do your own clearance first.

## Contributing

See `CONTRIBUTING.md`. Small, reviewable pull requests. Please open an issue before large changes.
