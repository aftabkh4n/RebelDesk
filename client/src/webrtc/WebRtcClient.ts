import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr'
import { api, getToken } from '../api/client'

export type ConnectionStatus =
  | 'idle' | 'requesting' | 'waiting-approval' | 'connecting'
  | 'connected' | 'reconnecting' | 'ended' | 'rejected' | 'error'

export interface SessionEnd { reason: string }

export interface SignalingMessage {
  type: string
  sdp?: string
  candidate?: string
  sdpMid?: string
  sdpMLineIndex?: number
}

export interface ControlEvent {
  kind: 'mousemove' | 'mousedown' | 'mouseup' | 'wheel' | 'keydown' | 'keyup'
  x?: number; y?: number
  button?: number
  deltaX?: number; deltaY?: number
  scanCode?: number; extended?: boolean
}

export class WebRtcClient {
  private hub?: HubConnection
  private pc?: RTCPeerConnection
  private controlChannel?: RTCDataChannel
  private sessionId?: string

  public onStatus: (s: ConnectionStatus) => void = () => {}
  public onEnded: (r: string) => void = () => {}
  public onStream: (s: MediaStream) => void = () => {}

  async start(deviceId: string): Promise<void> {
    this.onStatus('requesting')
    const created = await api.createSession(deviceId)
    this.sessionId = created.sessionId
    const turn = await api.getTurn(created.sessionId)

    const iceServers: RTCIceServer[] = []
    for (const uri of turn.stunUris) iceServers.push({ urls: uri })
    if (turn.turnUris.length) {
      iceServers.push({
        urls: turn.turnUris,
        username: turn.username,
        credential: turn.password,
      })
    }

    this.pc = new RTCPeerConnection({ iceServers, iceTransportPolicy: 'all' })

    const stream = new MediaStream()
    this.pc.ontrack = (ev) => {
      ev.streams[0]?.getTracks().forEach(t => stream.addTrack(t))
      if (ev.track.kind === 'video') this.onStream(stream)
    }

    this.pc.onconnectionstatechange = () => {
      const s = this.pc?.connectionState
      if (s === 'connected') this.onStatus('connected')
      else if (s === 'disconnected') this.onStatus('reconnecting')
      else if (s === 'failed' || s === 'closed') this.onStatus('ended')
    }

    // The viewer creates the control data channel; the agent listens for it.
    this.controlChannel = this.pc.createDataChannel('control', { ordered: true, negotiated: false })
    this.controlChannel.onopen = () => this.onStatus('connected')

    this.pc.onicecandidate = async (ev) => {
      if (!ev.candidate || !this.sessionId) return
      await this.hub?.invoke('Signal', this.sessionId, {
        type: 'ice',
        candidate: ev.candidate.candidate,
        sdpMid: ev.candidate.sdpMid,
        sdpMLineIndex: ev.candidate.sdpMLineIndex,
      } as SignalingMessage)
    }

    // Connect signaling with the session token (bearer + query fallback).
    this.hub = new HubConnectionBuilder()
      .withUrl(`/hub/signaling?access_token=${encodeURIComponent(getToken() ?? '')}`)
      .withAutomaticReconnect()
      .build()

    this.hub.on('sessionApproved', async (payload: { sessionId: string }) => {
      if (payload.sessionId !== this.sessionId) return
      this.onStatus('connecting')
      const offer = await this.pc!.createOffer({ offerToReceiveVideo: true })
      await this.pc!.setLocalDescription(offer)
      await this.hub!.invoke('Signal', this.sessionId, { type: 'offer', sdp: offer.sdp } as SignalingMessage)
    })
    this.hub.on('sessionRejected', () => {
      this.onStatus('rejected')
      void this.stop('rejected')
    })
    this.hub.on('sessionEnded', (payload: { reason?: string }) => {
      this.onStatus('ended')
      this.onEnded(payload.reason ?? 'ended')
      void this.stop('remote-end')
    })
    this.hub.on('signal', async (payload: SignalingMessage) => {
      if (!this.pc) return
      switch (payload.type) {
        case 'answer':
          await this.pc.setRemoteDescription({ type: 'answer', sdp: payload.sdp })
          break
        case 'offer':
          // Not expected — the agent answers our offer. Ignore.
          break
        case 'ice':
          if (payload.candidate) {
            await this.pc.addIceCandidate({
              candidate: payload.candidate,
              sdpMid: payload.sdpMid,
              sdpMLineIndex: payload.sdpMLineIndex,
            })
          }
          break
      }
    })

    await this.hub.start()
    await this.hub.invoke('JoinSession', this.sessionId)
    this.onStatus('waiting-approval')
  }

  sendControl(evt: ControlEvent): void {
    if (this.controlChannel?.readyState === 'open') {
      this.controlChannel.send(JSON.stringify(evt))
    }
  }

  async stop(reason: string = 'viewer-end'): Promise<void> {
    if (this.sessionId && this.hub?.state === HubConnectionState.Connected) {
      try { await this.hub.invoke('EndSession', this.sessionId, reason) } catch { /* ignore */ }
    }
    try { this.controlChannel?.close() } catch { /* ignore */ }
    try { this.pc?.close() } catch { /* ignore */ }
    if (this.hub) { try { await this.hub.stop() } catch { /* ignore */ } }
    this.pc = undefined
    this.hub = undefined
    this.controlChannel = undefined
    this.sessionId = undefined
  }
}
