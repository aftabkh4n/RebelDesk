// Headless viewer probe. Not a real WebRTC client — just a signaling participant.
// Verifies that:
//   1. A user JWT lets us open /hub/signaling.
//   2. Invoking JoinSession forwards a "sessionRequested" event to the agent's device group.
//   3. Rejecting the request end-to-end propagates back as "sessionRejected".
// Usage:
//   node headless-viewer.mjs <server> <email> <password> <deviceId>

import { createRequire } from 'node:module'
const require = createRequire(import.meta.url)
const signalR = require('@microsoft/signalr')

const [server, email, password, deviceId] = process.argv.slice(2)
if (!server || !email || !password || !deviceId) {
  console.error('Usage: node headless-viewer.mjs <server> <email> <password> <deviceId>')
  process.exit(2)
}

async function api(path, body, token) {
  const res = await fetch(`${server}${path}`, {
    method: body ? 'POST' : 'GET',
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  })
  const text = await res.text()
  if (!res.ok) throw new Error(`${res.status} ${text}`)
  return text ? JSON.parse(text) : {}
}

const auth = await api('/api/auth/login', { email, password })
console.log('[probe] logged in as', auth.email)

const session = await api('/api/sessions', { deviceId }, auth.token)
console.log('[probe] session created', session.sessionId)

const conn = new signalR.HubConnectionBuilder()
  .withUrl(`${server}/hub/signaling?access_token=${encodeURIComponent(auth.token)}`)
  .build()

conn.on('sessionApproved', (p) => console.log('[probe] event sessionApproved', p))
conn.on('sessionRejected', (p) => { console.log('[probe] event sessionRejected', p); shutdown() })
conn.on('sessionEnded', (p) => { console.log('[probe] event sessionEnded', p); shutdown() })
conn.on('signal', (p) => console.log('[probe] event signal', p.type))

await conn.start()
console.log('[probe] hub connected. Invoking JoinSession...')
await conn.invoke('JoinSession', session.sessionId)
console.log('[probe] JoinSession invoked. Watching for events for 20 s.')

setTimeout(shutdown, 20_000)

async function shutdown() {
  try { await conn.invoke('EndSession', session.sessionId, 'probe-shutdown') } catch { /* ignore */ }
  try { await conn.stop() } catch { /* ignore */ }
  process.exit(0)
}
