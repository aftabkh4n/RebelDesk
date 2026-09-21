import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { api } from '../api/client'

interface Device {
  id: string
  deviceName: string
  osPlatform: string
  online: boolean
  lastSeenUtc: string | null
}

export function Devices() {
  const { t, i18n } = useTranslation()
  const [devices, setDevices] = useState<Device[]>([])
  const [loading, setLoading] = useState(true)
  const [pairingCode, setPairingCode] = useState<{ code: string; expiresAtUtc: string } | null>(null)
  const [suggestedName, setSuggestedName] = useState('')

  async function refresh() {
    setLoading(true)
    try { setDevices(await api.devices()) } finally { setLoading(false) }
  }
  useEffect(() => {
    refresh()
    const timer = setInterval(refresh, 5000)
    return () => clearInterval(timer)
  }, [])

  async function generatePairing() {
    const res = await api.createPairingCode(suggestedName || 'Windows PC')
    setPairingCode(res)
  }

  async function revoke(id: string) {
    if (!confirm(t('devices.confirmRevoke'))) return
    await api.revokeDevice(id)
    await refresh()
  }

  return (
    <div className="devices-page">
      <h1>{t('devices.title')}</h1>

      {loading && <p>…</p>}
      {!loading && devices.length === 0 && <p className="muted">{t('devices.empty')}</p>}

      <ul className="device-list">
        {devices.map(d => (
          <li key={d.id} className={`device ${d.online ? 'online' : 'offline'}`}>
            <div className="device-name">
              <strong>{d.deviceName}</strong>
              <span className="badge">{d.osPlatform}</span>
              <span className={`status ${d.online ? 'ok' : 'off'}`}>
                {d.online ? t('devices.online') : t('devices.offline')}
              </span>
            </div>
            <div className="device-actions">
              {d.online && (
                <Link className="button" to={`/session/${d.id}`}>{t('devices.connect')}</Link>
              )}
              <button onClick={() => revoke(d.id)}>{t('devices.revoke')}</button>
            </div>
          </li>
        ))}
      </ul>

      <section className="pairing">
        <h2>{t('devices.addTitle')}</h2>
        <p className="muted">{t('devices.addHint')}</p>
        <div className="pairing-form">
          <input value={suggestedName} onChange={e => setSuggestedName(e.target.value)} placeholder="Device name" />
          <button onClick={generatePairing}>{t('devices.generateCode')}</button>
        </div>
        {pairingCode && (
          <div className="pairing-result">
            <code>{pairingCode.code}</code>
            <button onClick={() => navigator.clipboard?.writeText(pairingCode.code)}>{t('devices.copyCode')}</button>
            <p className="muted">
              {t('devices.codeExpires', {
                time: new Date(pairingCode.expiresAtUtc).toLocaleString(i18n.language),
              })}
            </p>
          </div>
        )}
      </section>
    </div>
  )
}
