# Performance — how to measure it, and what has actually been measured

This document distinguishes between what RebelDesk **targets** and what has been **measured** on real hardware. The two are not the same.

## Targets (v0.1)

- Setup time (approve → first painted frame): under 2 seconds on LAN.
- Frame rate: 30 fps at 1080p on modern hardware.
- Bandwidth: 2–6 Mbps at 1080p30 typical.
- Input-to-glass latency: under 100 ms on LAN.
- CPU on the host: under 20 % of one 8-core CPU.

## What has actually been measured

**Nothing yet.** No end-to-end benchmark has been recorded. See `STATUS.md`. Do not cite the numbers above as results.

## Measurement procedure (canonical, use exactly this)

### 1. Bandwidth

`chrome://webrtc-internals` on the viewer. Read `bytesReceived` on the inbound video RTP stream, sample every 5 s, divide.

### 2. Frame rate

Same page: read `framesPerSecond` on the inbound track. Cross-check on the agent side by counting frames delivered to the encoder (`FrameCaptured` events).

### 3. Round-trip time (network)

`chrome://webrtc-internals` → selected candidate pair → `currentRoundTripTime`. This is network only, not the full interaction latency.

### 4. Setup time

Wall-clock from clicking **Approve** on the host to the first non-black pixel in the viewer video element. Use OBS to record both machines' screens at 60 fps, count frames between the two events.

### 5. Input-to-glass latency (the number that matters)

Do **not** conflate this with network RTT. Measure the full pipeline:

1. Point a 240 fps camera so both the viewer's local keyboard and the host's monitor are in frame.
2. Press a key with a visible label (e.g. Caps Lock — LED gives a physical cue).
3. Count frames between: (a) the key visibly bottoming out on the local keyboard, and (b) the LED lighting on the remote keyboard (or the character appearing on the remote screen).
4. Divide by 240. Repeat 10 times, report median + p95.

Anything else (`chrome://tracing`, RTP timestamps) can help attribute the number to a stage, but is not the top-line latency.

### 6. Freeze / dropped-frame recovery

1. Start a session over your normal LAN path.
2. On the router, drop the outbound interface for exactly 5 seconds using `tc qdisc add dev eth0 root netem loss 100%` (Linux) or the equivalent.
3. Restore the interface.
4. Record: seconds until viewer shows fresh pixels again. Any stuck keys or mouse buttons on the host after recovery is a failing case.

## Reporting format

Every time you run the benchmark, add a section to this file with:

- Date, git commit, build config.
- Host CPU / GPU / OS.
- Viewer browser + OS.
- Network topology (LAN / same NAT / different NATs / forced TURN).
- All five numbers above with sample count.

Example (template — replace with real numbers when you run it):

```
### 2026-11-XX — Aftab dev box (LAN)
Commit: abc1234
Host: i7-13700K, RTX 4070, Windows 11 26100
Viewer: Chrome 141 on the same LAN, wired.
Topology: same subnet, no TURN.
Setup: median 1.4 s (n=5)
FPS: 30 stable at 1920x1080
Bandwidth: 3.1 Mbps avg
Network RTT: 2 ms
Input-to-glass: 68 ms median, 82 ms p95 (240 fps camera, n=10)
```
