# RebelDesk — Status

_Last update: 2026-09-20_

The purpose of this file is to prevent overclaiming. Every feature must sit in exactly one of the categories below. If it is not in **Implemented and executed end-to-end**, it is not ready.

## Implemented and executed end-to-end
_(built, compiled, and manually run at least once in the described flow)_

Verified by direct HTTP + hub invocation against a locally running server + a locally running agent, on this development host, on 2026-09-20:

- **Server** builds and starts on `http://0.0.0.0:8080`. `/healthz` returns `{ "status": "ok" }`.
- **User registration** (`POST /api/auth/register`) creates a User row, hashes the password with BCrypt (work factor 12), and returns a signed JWT with `kind=user`.
- **Login** (`POST /api/auth/login`) verifies password, returns a fresh user JWT.
- **Pairing code issue** (`POST /api/devices/pairing`, authorised by user JWT) writes a `PairingCode` row with 15-minute expiry and returns the 8-character code (`XXXX-XXXX`).
- **Pairing consumption** (`POST /api/pairing/consume`, unauthenticated) validates the code, creates a `Device` row bound to the user, and returns an agent JWT plus a refresh token whose SHA-256 hash is stored server-side.
- **Device listing** (`GET /api/devices`) returns the caller's devices with an `online` boolean derived from live SignalR connections.
- **Session creation** (`POST /api/sessions`) refuses (503) if the target device is offline, otherwise writes a `Session` row (`Requested` state) and returns a short-lived session JWT.
- **Signaling hub** (`/hub/signaling`) accepts both user and agent JWTs, tracks device presence, and correctly routes a `sessionRequested` event from a viewer's `JoinSession` invocation to the agent's device group. The agent's log shows `Session requested: <id> from 127.0.0.1` when a headless probe (`scripts/headless-viewer.mjs`) invokes `JoinSession`.
- **Session end** propagates: probe calls `EndSession` → server updates row → hub emits `sessionEnded` back to the probe (and would emit to the agent). Agent's approval prompt honours cancellation from either side.
- **Agent auto-resume** — `device.json` loaded from disk skips the pairing prompt and connects straight to signaling.

Client build: `npm run build` produces a 395 KB gzipped-122 KB bundle with all five locales embedded and RTL wiring on `<html>` for Arabic.

Agent build: `dotnet build agent/` succeeds against .NET 10 on `net10.0-windows`, zero warnings, zero errors.

Server build: `dotnet build server/` succeeds against .NET 10 with two low-severity transitive advisories (`Microsoft.OpenApi 2.0.0`, `SQLitePCLRaw.lib.e_sqlite3 2.1.10`) documented below.

## Implemented and automatically tested
_(unit/integration tests exist and pass in CI)_

- None yet. The `tests/` scaffold exists but has no tests. CI (`.github/workflows/ci.yml`) builds server + agent + client, runs `dotnet list package --vulnerable`, and configures CodeQL for C# and JavaScript.

## Implemented, compiled, not yet manually verified end-to-end

- **Approval prompt (WinForms modal)** — the code path is exercised (`ApprovalPrompt.AskAsync` runs on an STA thread when a session is requested), but no human clicked Approve/Reject during this session, so the dialog's visual behaviour was not observed.
- **WebRTC negotiation on the agent side** — the code creates an `RTCPeerConnection`, adds a VP8 video track from `SIPSorcery.VP8`, and handles SDP offer / answer / ICE messages routed through the hub. This path is not reached without a real WebRTC peer on the viewer side.
- **GDI capture → VP8 encode → RTP** — the pipeline compiles, but has not run against real network traffic to a browser.
- **Data-channel control events** — `SendInputInjector` compiles and its P/Invoke signatures match the Windows SDK, but no real click / keypress has round-tripped through the data channel yet.
- **Browser client UI** — the pages compile and route, but the end-to-end sign-in → devices → session flow has not been walked through a browser during this session.
- **Docker Compose stack** — the compose file, Dockerfile, and coturn config are complete but have not been `docker compose up`'d during this session.

## Documented, not yet implemented

- Ed25519 device identity (v0.1 uses RSA as a placeholder; the key is not yet used for anything beyond registration).
- ICE restart on network drop.
- Bitrate adaptation from RTCP feedback.
- DXGI Desktop Duplication capture path.
- Hardware H.264 encoding (via ffmpeg `h264_nvenc`/`h264_qsv`/`h264_amf`).
- Agent packaging as a signed MSI / MSIX, service install.
- `WM_WTSSESSION_CHANGE` handling to auto-end sessions on lock.
- Native-speaker review of the de / fr / nl / ar locales — all four currently carry `"needs-review": true`.

## Known blockers (this session)

- **No second machine and no interactive display.** The controlled Windows host and the controlling browser are both this development host. The remaining "compiled, not verified" rows above require either a real user clicking the WinForms dialog and driving a browser, or a headless Chromium harness. Neither was in scope for this session.
- **SIPSorcery 8.x has two open high-severity advisories** (`GHSA-28gm-jrmw-xx93`, `GHSA-jwjp-4649-v8jp`). RebelDesk uses 10.0.16 which is not listed in either advisory, but the CVE feed for 10.x should be watched.
- **`Microsoft.OpenApi 2.0.0` and `SQLitePCLRaw.lib.e_sqlite3 2.1.10`** carry low-severity NU1903 warnings when transitively pulled. `Swashbuckle.AspNetCore` was removed for this reason; the SQLite package still comes in via EF Core and will be pinned once EF Core ships an updated dep in a stable release.

## Performance targets (NOT achieved yet)

- Setup time from "approve" to first painted frame: target < 2 s on LAN.
- Frame rate: target 30 fps at 1080p on modern hardware.
- Input-to-glass latency: target < 100 ms on LAN.
- CPU: target < 15 % on an 8-core CPU with hardware H.264.
- Bandwidth: target 2–6 Mbps at 1080p30.

No performance measurement has been executed. Do not quote these numbers as results. The measurement procedure lives in `docs/PERFORMANCE.md`.

## Next task

**Run one real end-to-end session on the local Windows desktop.** Concretely:

1. Start `docker compose up -d` from `docker/` (or `dotnet run --project server`).
2. Open `http://localhost:5173`, register a user, generate a pairing code.
3. `dotnet run --project agent`, enter the pairing code.
4. Click **Start session** in the browser.
5. Approve on the agent's WinForms prompt.
6. Confirm actual desktop pixels arrive; move mouse and type; confirm both work.
7. End the session from both sides in turn, confirm keys release.
8. Record numbers into `docs/PERFORMANCE.md` per the procedure there.
9. Move the corresponding rows in this file from "compiled, not verified" to "executed end-to-end".

Every failure encountered will be recorded here, not hidden.
