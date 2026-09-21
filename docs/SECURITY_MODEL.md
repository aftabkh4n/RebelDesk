# Security model

## Goals

RebelDesk grants one authenticated user real-time control of a Windows machine they own. Every design decision below serves one of these three goals:

1. **The wrong person cannot start or continue a session.**
2. **The right session cannot outlive its explicit end.**
3. **The server cannot secretly observe or replay a session.**

If you spot a case where any of these does not hold, treat it as a security bug and follow `SECURITY.md`.

## Actors and trust

| Actor | Trust |
|---|---|
| Owner (viewer + host, same person for the intended use case) | Root of trust. |
| Server operator | Trusted for identity, availability, and configuration. **Not** trusted with session content — video and input never pass through the server. |
| Coturn operator | Trusted for relay availability. Sees encrypted DTLS-SRTP media only. |
| Anyone else | Untrusted. |

## Trust boundaries

- **Signaling and API (server ↔ everyone)** — carry authentication, device management, SDP offers/answers, and ICE candidates. Encrypted only if you deploy TLS in front of the server (required for internet-facing deployment).
- **Media and control (viewer ↔ agent)** — flow only over the WebRTC peer connection. Video uses DTLS-SRTP. Data channel uses DTLS + SCTP. Even on a compromised server, media stays end-to-end encrypted between browser and agent.

## Authentication

- Users: email + BCrypt-hashed password (work factor 12), JWT (HS256) on login. Tokens have `kind=user` and a subject claim of the user id.
- Agents: at pairing time the agent generates an RSA key pair (v0.1; slated for Ed25519 in v0.2), posts the public key with a single-use pairing code, and receives a JWT (`kind=agent`) plus an opaque refresh token. The server stores only the SHA-256 hash of the refresh token.
- Sessions: on session creation the server issues a short-lived JWT (`kind=session-viewer`, TTL 10 minutes) scoped to that session.

## Pairing

- Codes are 8 characters from an unambiguous alphabet, single-use, and expire in 15 minutes.
- Consuming a code binds the agent's public key to a `Device` row owned by the user who created the code. There is no other admission path.
- Rotation: every successful refresh replaces the refresh token. A stolen refresh token becomes useless after the legitimate agent next reconnects.

## Sessions

- The server holds a `Session` row with state `Requested → Approved | Rejected → Active → Ended` (or `Expired`). Every hub method authorises against the row's owner and device before acting.
- Approval must happen on the host, in a topmost, timeout-limited local dialog. There is no bypass. The host process holds the approval state in memory; if it exits, all sessions end.
- Session end is single-writer: whichever side sends `EndSession` first flips the state; further messages are refused.

## Input authorisation

- `AgentPeer.AllowInput(false)` is called before any of: session end, agent-side reject, viewer-side disconnect, connection state moving out of `connected`, or agent process exit.
- On any of the above, `SendInputInjector.ReleaseAllHeld()` fires. It releases every held scancode and every pressed mouse button that this session pressed. This prevents stuck-modifier states after network drops.

## TURN credentials

- HMAC-SHA1 over `expiry-unix-ts:subject-id` with a shared secret (coturn `use-auth-secret`). TTL 10 minutes.
- The subject-id includes both the token kind (`user`/`agent`) and the session id, so a leaked TURN credential is scoped to one leg of one session.

## Not implemented, deliberately rejected at the transport

- **Clipboard**: no code path. The data channel dispatcher (`AgentPeer.DispatchControl`) has an allowlist of event kinds; anything else is dropped and logged.
- **File transfer**: same.
- **Audio**: same. The peer connection only offers a send-only video track.
- **Multi-monitor**: only the primary monitor is captured. Extra data channels are refused (`ondatachannel` closes anything not labelled `control`).

## Windows-specific caveats

- **Secure desktop** (UAC prompts, lock screen, Ctrl-Alt-Del) is not capturable from a user session, by design in Windows. The remote screen will show black in these transitions; input will fail. To work through those flows, the agent must run as a service and use `WTSVirtualChannel`/kernel input, which v0.1 does not.
- **Elevated applications**: input injected from a non-elevated agent is rejected against elevated windows (UIPI). Documented; not a bypass path.
- **Session change events**: the agent listens for `WM_WTSSESSION_CHANGE` (lock/unlock/console-connect/disconnect) — planned for v0.2 — and will end sessions on lock. In v0.1, a lock screen simply causes video to freeze; end the session manually.

## Reporting

`SECURITY.md` in the repo root.
