import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router-dom'
import { WebRtcClient } from '../webrtc/WebRtcClient'
import type { ConnectionStatus } from '../webrtc/WebRtcClient'
import { keyEventToScancode } from '../webrtc/ScanCodes'

export function Session() {
  const { deviceId } = useParams<{ deviceId: string }>()
  const { t } = useTranslation()
  const navigate = useNavigate()
  const videoRef = useRef<HTMLVideoElement>(null)
  const clientRef = useRef<WebRtcClient | null>(null)
  const [status, setStatus] = useState<ConnectionStatus>('idle')
  const [endReason, setEndReason] = useState<string | null>(null)
  const [inputEnabled, setInputEnabled] = useState(false)

  useEffect(() => {
    if (!deviceId) return
    const c = new WebRtcClient()
    clientRef.current = c
    c.onStatus = setStatus
    c.onEnded = setEndReason
    c.onStream = (stream) => {
      if (videoRef.current) {
        videoRef.current.srcObject = stream
        videoRef.current.play().catch(() => { /* autoplay blocked; user gesture below unblocks */ })
      }
    }
    c.start(deviceId).catch((err) => { console.error(err); setStatus('error') })
    return () => { void c.stop('viewer-unmount') }
  }, [deviceId])

  function sendMouse(e: React.PointerEvent<HTMLVideoElement>, kind: 'mousemove' | 'mousedown' | 'mouseup') {
    if (!inputEnabled || !videoRef.current) return
    const rect = videoRef.current.getBoundingClientRect()
    const x = (e.clientX - rect.left) / rect.width
    const y = (e.clientY - rect.top) / rect.height
    const payload = { kind, x, y } as { kind: string; x: number; y: number; button?: number }
    if (kind !== 'mousemove') payload.button = e.button
    clientRef.current?.sendControl(payload as any)
  }

  function sendWheel(e: React.WheelEvent<HTMLVideoElement>) {
    if (!inputEnabled) return
    e.preventDefault()
    clientRef.current?.sendControl({ kind: 'wheel', deltaX: -Math.sign(e.deltaX) * 120, deltaY: -Math.sign(e.deltaY) * 120 })
  }

  useEffect(() => {
    function onKey(e: KeyboardEvent, down: boolean) {
      if (!inputEnabled) return
      const sc = keyEventToScancode(e.code)
      if (!sc) return
      e.preventDefault()
      clientRef.current?.sendControl({ kind: down ? 'keydown' : 'keyup', scanCode: sc.code, extended: sc.extended })
    }
    const kd = (e: KeyboardEvent) => onKey(e, true)
    const ku = (e: KeyboardEvent) => onKey(e, false)
    window.addEventListener('keydown', kd)
    window.addEventListener('keyup', ku)
    return () => { window.removeEventListener('keydown', kd); window.removeEventListener('keyup', ku) }
  }, [inputEnabled])

  async function endSession() {
    await clientRef.current?.stop('viewer-end')
    navigate('/devices')
  }

  return (
    <div className="session-page">
      <header className="session-header">
        <button onClick={endSession}>{t('session.disconnect')}</button>
        <span className="status">{status}</span>
        {endReason && <span className="ended-reason">— {endReason}</span>}
      </header>

      <div className="session-stage" onClick={() => setInputEnabled(true)}>
        {!inputEnabled && <div className="input-hint">{t('session.inputBlocked')}</div>}
        <video
          ref={videoRef}
          playsInline
          autoPlay
          muted
          onPointerMove={(e) => sendMouse(e, 'mousemove')}
          onPointerDown={(e) => sendMouse(e, 'mousedown')}
          onPointerUp={(e) => sendMouse(e, 'mouseup')}
          onWheel={sendWheel}
          onContextMenu={(e) => e.preventDefault()}
        />
      </div>
    </div>
  )
}
