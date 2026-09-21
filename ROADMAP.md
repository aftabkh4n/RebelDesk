# RebelDesk — Roadmap

Milestones are gated by real, verifiable evidence, not by code merged.

## v0.1 — Attended remote desktop, one host, one viewer

Goal: prove the core (per `PROJECT_SPEC.md`, "FIRST MILESTONE").

- Self-hosted server + coturn deploy.
- Pairing flow (short-lived code, device key on file).
- Attended session with explicit local approval.
- Single-monitor Windows capture, H.264 over WebRTC.
- Mouse and keyboard control (scancodes, non-US layouts).
- Visible active-session indicator and local disconnect.
- Session-end key/button release.
- Interfaces localized: en, de, fr, nl, ar (with RTL).
- Documented performance measurement, at least one measured LAN run.

**Exit criteria:** a fresh machine can install the agent, pair it, and receive a session from a browser on the same or a different network within 15 minutes following `docs/SELF_HOSTING.md`, and the flow works end-to-end with measured latency and framerate written into `docs/PERFORMANCE.md`.

## v0.2 — Reliability and adaptation

- ICE restart on network drop.
- Encoder bitrate adaptation from `RTCP REMB` / `TWCC`.
- Reconnection UX in browser client.
- Structured logs, `/healthz` and metrics endpoints, log rotation.
- Basic Playwright end-to-end tests for signaling and pairing.
- Windows agent packaged as a Service with auto-start option.
- Docker image published from CI on tagged releases.

## v0.3 — Multi-monitor and better capture

- Monitor selection.
- DXGI Desktop Duplication path with cursor overlay.
- HDR path (documented limitations).
- Optional MFT hardware encoder path replacing ffmpeg where available.

## v0.4 — Audio and clipboard (opt-in)

- Windows audio loopback capture (WASAPI), Opus encoding, second WebRTC track.
- Clipboard sync (text first, size-capped, disabled by default, per-session opt-in).
- Documentation of trust boundary changes.

## v0.5 — File transfer (opt-in)

- WebRTC data channel file transfer, size- and rate-capped.
- Explicit approval per transfer.

## v0.6 — Additional platforms

Research spikes only until v0.5 is stable:
- macOS agent (ScreenCaptureKit, ScreenCaptureAccess check).
- Linux agent (PipeWire capture, XI2 input).

## v1.0 — Stable release

- Security review and third-party pen test proof.
- End-to-end tests in CI cover: pairing, session approve, session revoke, ICE fallback to TURN, disconnect key release.
- Documented upgrade path from v0.x.
- Native-speaker review of all shipped locales.
- Signed installers.

## Deferred (post-1.0)

- Unattended access with explicit hardware-key trust model.
- Mobile clients.
- Multi-user tenancy.
- Recording (with strong local-consent flow).

## Explicitly not planned

- Silent install, hidden agent, "helper" mode without local approval.
- Any telemetry that transmits session pixel content or clipboard data.
