# Security Policy

## Supported versions

RebelDesk is pre-alpha. Only the current `main` branch receives security fixes. There is no supported release yet.

## Reporting a vulnerability

Please report suspected vulnerabilities privately.

1. Open a GitHub Security Advisory on the repository (Security tab → Advisories → Report a vulnerability). This creates a private thread with the maintainers.
2. If you cannot use GitHub, contact the maintainer listed in the repository's public profile.

Do **not** open a public issue for security problems. Do not attach screenshots or logs that contain remote-session pixel content, clipboards, or credentials from a real session.

## What to include

- Version / commit SHA where you observed the problem.
- Environment (OS, browser, agent version, deployment topology).
- Minimum reproduction steps.
- Impact assessment from your perspective.

## Response

RebelDesk is maintained by volunteers. Expect an acknowledgement within 7 days and a mitigation plan within 30 days for high-severity issues. We will credit reporters in the advisory unless they request otherwise.

## Trust boundaries (short summary)

The full model is in `docs/SECURITY_MODEL.md`. Key points:

- The server never sees session video or input; those flow only over the WebRTC peer connection (DTLS-SRTP / DTLS-SCTP).
- Pairing tokens expire in 15 minutes and are single-use.
- TURN credentials are short-lived HMAC secrets, valid for the session duration only.
- The agent does not capture the secure desktop (UAC prompts, lock screen). This is a Windows limitation, not a bug.
- Held modifier keys and mouse buttons are released whenever a session ends or the connection is lost.
- Clipboard and file transfer are not implemented and are rejected at the transport layer.

## Out of scope

- Denial-of-service against your own self-hosted server.
- Vulnerabilities that require physical access to an already-unlocked host machine and full local user privileges.
- Findings in unmodified upstream dependencies without a specific RebelDesk-usage impact. Report those upstream and CC us.
