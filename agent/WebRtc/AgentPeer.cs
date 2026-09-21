using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using RebelDesk.Agent.Capture;
using RebelDesk.Agent.Input;

namespace RebelDesk.Agent.WebRtc;

/// <summary>
/// Holds the WebRTC peer connection for one attended session.
/// Owns: the video source (a raw-BGRA screen capturer), the SIPSorcery encoder end-point
/// that turns BGRA frames into VP8 RTP packets, and a data-channel handler that dispatches
/// browser-side input events to the SendInput injector. Tearing down the peer stops
/// capture and releases every held key/button.
/// </summary>
public sealed class AgentPeer : IAsyncDisposable
{
    private readonly ILogger _log;
    private readonly IScreenCapture _capture;
    private readonly IVideoEncoder _videoEncoder;
    private readonly SendInputInjector _input;
    private readonly RTCConfiguration _rtcConfig;
    private readonly Func<SignalingMessage, Task> _sendSignal;
    private RTCPeerConnection? _pc;
    private RTCDataChannel? _controlChannel;
    private bool _inputAllowed;
    private VideoFormat _negotiatedVideoFormat;

    public AgentPeer(
        ILogger log,
        IScreenCapture capture,
        IVideoEncoder videoEncoder,
        SendInputInjector input,
        RTCConfiguration rtcConfig,
        Func<SignalingMessage, Task> sendSignal)
    {
        _log = log;
        _capture = capture;
        _videoEncoder = videoEncoder;
        _input = input;
        _rtcConfig = rtcConfig;
        _sendSignal = sendSignal;
    }

    public async Task StartAsync()
    {
        _pc = new RTCPeerConnection(_rtcConfig);

        var vp8 = _videoEncoder.SupportedFormats.FirstOrDefault(f => f.Codec == VideoCodecsEnum.VP8);
        var format = vp8.Codec == VideoCodecsEnum.VP8 ? vp8 : _videoEncoder.SupportedFormats.First();
        var track = new MediaStreamTrack(format, MediaStreamStatusEnum.SendOnly);
        _pc.addTrack(track);

        _capture.FrameCaptured += OnFrame;

        _pc.OnVideoFormatsNegotiated += formats =>
        {
            _negotiatedVideoFormat = formats.FirstOrDefault(f => f.Codec == VideoCodecsEnum.VP8);
            if (_negotiatedVideoFormat.Codec == VideoCodecsEnum.Unknown) _negotiatedVideoFormat = formats.First();
            _log.LogInformation("Video format negotiated: {Codec}", _negotiatedVideoFormat.Codec);
        };
        _pc.oniceconnectionstatechange += state => _log.LogInformation("ICE state: {State}", state);
        _pc.onconnectionstatechange += state => _log.LogInformation("Peer state: {State}", state);
        _pc.onicecandidate += cand =>
        {
            if (cand?.candidate is null) return;
            _ = _sendSignal(new SignalingMessage("ice", null, cand.candidate, cand.sdpMid, cand.sdpMLineIndex));
        };
        _pc.ondatachannel += ch =>
        {
            if (ch.label == "control")
            {
                _controlChannel = ch;
                ch.onmessage += OnControlMessage;
                _log.LogInformation("Control data channel opened by viewer.");
            }
            else
            {
                _log.LogWarning("Ignoring unsolicited data channel: {Label}", ch.label);
                ch.close();
            }
        };

        _capture.Start();
        await Task.CompletedTask;
    }

    public void AllowInput(bool allowed)
    {
        _inputAllowed = allowed;
        if (!allowed) _input.ReleaseAllHeld();
    }

    public async Task HandleSignalAsync(SignalingMessage msg)
    {
        if (_pc is null) return;
        switch (msg.Type)
        {
            case "offer":
                if (string.IsNullOrEmpty(msg.Sdp)) return;
                _pc.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.offer, sdp = msg.Sdp });
                var answer = _pc.createAnswer(null);
                await _pc.setLocalDescription(answer);
                await _sendSignal(new SignalingMessage("answer", answer.sdp, null, null, null));
                break;

            case "answer":
                if (!string.IsNullOrEmpty(msg.Sdp))
                    _pc.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = msg.Sdp });
                break;

            case "ice":
                if (!string.IsNullOrEmpty(msg.Candidate))
                {
                    var init = new RTCIceCandidateInit
                    {
                        candidate = msg.Candidate,
                        sdpMid = msg.SdpMid ?? "0",
                        sdpMLineIndex = (ushort)(msg.SdpMLineIndex ?? 0),
                    };
                    _pc.addIceCandidate(init);
                }
                break;
        }
    }

    private void OnFrame(object? sender, CapturedFrame frame)
    {
        if (_pc is null || _pc.connectionState != RTCPeerConnectionState.connected) return;
        try
        {
            var encoded = _videoEncoder.EncodeVideo(frame.Width, frame.Height, frame.Bgra,
                VideoPixelFormatsEnum.Bgra, _negotiatedVideoFormat.Codec == VideoCodecsEnum.Unknown ? VideoCodecsEnum.VP8 : _negotiatedVideoFormat.Codec);
            if (encoded is null || encoded.Length == 0) return;
            uint duration = (uint)(90000 / Math.Max(_capture.Width > 0 ? 15 : 15, 1));
            _pc.SendVideo(duration, encoded);
        }
        catch (Exception ex) { _log.LogWarning(ex, "Encode/send failed"); }
    }

    private void OnControlMessage(RTCDataChannel channel, DataChannelPayloadProtocols proto, byte[] data)
    {
        if (!_inputAllowed) return;
        try
        {
            var msg = JsonSerializer.Deserialize<ControlEvent>(Encoding.UTF8.GetString(data));
            if (msg is null) return;
            DispatchControl(msg);
        }
        catch (Exception ex) { _log.LogWarning(ex, "Bad control message"); }
    }

    private void DispatchControl(ControlEvent msg)
    {
        switch (msg.Kind)
        {
            case "mousemove":
                if (msg.X.HasValue && msg.Y.HasValue) _input.MouseMoveAbsolute(msg.X.Value, msg.Y.Value);
                break;
            case "mousedown":
            case "mouseup":
                if (msg.Button.HasValue) _input.MouseButton(msg.Button.Value, msg.Kind == "mousedown");
                break;
            case "wheel":
                _input.MouseWheel(msg.DeltaY ?? 0, msg.DeltaX ?? 0);
                break;
            case "keydown":
            case "keyup":
                if (msg.ScanCode.HasValue)
                    _input.KeyEvent((ushort)msg.ScanCode.Value, msg.Kind == "keydown", msg.Extended ?? false);
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _inputAllowed = false;
        try { _input.ReleaseAllHeld(); } catch { }
        try { _capture.FrameCaptured -= OnFrame; } catch { }
        await _capture.StopAsync();
        if (_pc is not null)
        {
            try { _pc.close(); } catch { }
            _pc = null;
        }
    }

    // Local mirror of the server DTO to avoid a project reference.
    public record ControlEvent(
        string Kind,
        double? X, double? Y,
        int? Button,
        int? DeltaX, int? DeltaY,
        int? ScanCode, bool? Extended);
}

public record SignalingMessage(string Type, string? Sdp, string? Candidate, string? SdpMid, int? SdpMLineIndex);
