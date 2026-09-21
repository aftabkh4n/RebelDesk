# Architecture

## Components

- **`server/`** — ASP.NET Core 10 web API + SignalR hub. Owns identity, device pairing, session lifecycle, and TURN credential issuance. Persists to SQLite (single-file, EF Core).
- **`agent/`** — .NET 10 Windows console app + WinForms approval prompt. Captures the primary display, encodes VP8 (pure managed `SIPSorcery.VP8`), sends it over WebRTC. Receives input on a data channel and injects with `SendInput`.
- **`client/`** — Vite + React 18 + TypeScript. Talks to the server over HTTPS + SignalR, talks to the agent over WebRTC once ICE completes.
- **`coturn`** — off-the-shelf 4.18.0 image, configured for `use-auth-secret` HMAC credentials.

## Data-flow, session-scoped

```
Viewer                                                    Agent (Windows)
──────                                                    ─────────────
POST /api/sessions             ┌──────────────┐
     ────────────────────────► │  Server      │ SignalR: "sessionRequested"
                               │  SignalR hub │ ─────────────────────────►
GET /api/sessions/:id/turn     │              │
     ────────────────────────► │              │ ◄─── SignalR: "ApproveSession"
     ◄─── credentials          │              │
                               │              │ SignalR: "sessionApproved"
SignalR: "JoinSession"         │              │ ─────────────────────────►
     ────────────────────────► │              │
     ◄────── "sessionApproved" │              │
                               └──────────────┘
Viewer creates RTCPeerConnection with TURN creds ──────┐
Viewer creates "control" DataChannel                   │  SDP offer/answer relayed
Viewer sends SDP offer via signaling  ─────────────────┼───► through hub, never
Agent creates its RTCPeerConnection, adds video track  │    inspected by server.
Agent sends SDP answer via signaling ◄─────────────────┘
                          ICE candidates flow same way.
                          Once ICE completes:
Viewer <───── DTLS-SRTP video (VP8 RTP) ───── Agent
Viewer ────── DTLS-SCTP control events ─────► Agent
```

## Why signaling and not raw WebSockets

SignalR gives us server-side group management (device presence, per-session viewer set), automatic reconnect with negotiated transports, and JWT auth via query string — all of which we would otherwise have to build. The choice does not commit us to WebSockets: SignalR negotiates SSE and long polling as fallbacks.

## Why VP8 for v0.1

- Pure C# encoder (`SIPSorcery.VP8`) — no native dependency, no version-drift risk with FFmpeg.
- Universally supported in Chrome and Edge without pref changes.
- Acceptable quality for a proof of concept; H.264 with hardware encode is on the v0.3 roadmap.

## Why GDI for v0.1 capture

- Works everywhere Windows works, including inside an RDP session (which the initial development host is).
- Simple, no D3D device management.
- Slow — bounded to ~10–15 fps at 1080p. DXGI Desktop Duplication (v0.3) is the fix.

## Files that carry security semantics

If you touch any of the following, `docs/SECURITY_MODEL.md` must be re-read and probably updated:

- `server/Auth/*.cs`
- `server/Hubs/SignalingHub.cs`
- `server/Controllers/PairingController.cs`, `SessionsController.cs`
- `agent/Ui/ApprovalPrompt.cs`
- `agent/Input/SendInputInjector.cs`
- `agent/WebRtc/AgentPeer.cs`
- `docker/coturn/turnserver.conf`

## Build outputs

- Server: `.NET` published DLLs + `appsettings.json`, deployed via `docker/Dockerfile.server`.
- Agent: single-target `win-x64` publish, ships as `RebelDesk.Agent.exe` + dependencies.
- Client: static HTML/CSS/JS in `client/dist/`, served by any static host.

## What is deliberately not in the diagram

- No message queue.
- No microservices.
- No search index.
- No distributed cache.
- No SPA-service-worker offline mode.

Add these when a measured limit forces them, not before.
