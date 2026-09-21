using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RebelDesk.Agent.Capture;
using RebelDesk.Agent.Config;
using RebelDesk.Agent.Input;
using RebelDesk.Agent.Pairing;
using RebelDesk.Agent.Signaling;
using RebelDesk.Agent.Ui;
using RebelDesk.Agent.WebRtc;
using Serilog;
using SIPSorcery.Net;
using Vpx.Net;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables("REBELDESK_")
    .AddCommandLine(args)
    .Build();

var settings = config.Get<AgentSettings>() ?? new AgentSettings();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(Enum.TryParse<Serilog.Events.LogEventLevel>(settings.LogLevel, true, out var lvl)
        ? lvl : Serilog.Events.LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("agent.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .CreateLogger();

using var loggerFactory = LoggerFactory.Create(b => b.AddSerilog());
var appLog = loggerFactory.CreateLogger("RebelDesk.Agent");

appLog.LogInformation("RebelDesk agent starting. Server: {Url}", settings.ServerUrl);

using var http = new HttpClient { BaseAddress = new Uri(settings.ServerUrl) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("RebelDesk-Agent/0.1");

var pairing = new PairingClient(http, loggerFactory.CreateLogger<PairingClient>());

var stateFile = Path.IsPathRooted(settings.DeviceStateFile)
    ? settings.DeviceStateFile
    : Path.Combine(AppContext.BaseDirectory, settings.DeviceStateFile);
var device = DeviceState.Load(stateFile);

if (device is null)
{
    Console.WriteLine();
    Console.WriteLine("This host is not paired. Sign in to the RebelDesk server, create a pairing code, then enter it below.");
    Console.Write("Pairing code (e.g. ABCD-1234): ");
    var code = Console.ReadLine()?.Trim() ?? "";
    Console.Write("Device name (default: this machine): ");
    var nm = Console.ReadLine()?.Trim();
    var deviceName = string.IsNullOrWhiteSpace(nm) ? Environment.MachineName : nm;

    try
    {
        device = await pairing.PairAsync(code, deviceName, CancellationToken.None);
        device.Save(stateFile);
        appLog.LogInformation("Paired as device {DeviceId} ({DeviceName}). State saved to {File}",
            device.DeviceId, device.DeviceName, stateFile);
    }
    catch (Exception ex)
    {
        appLog.LogError(ex, "Pairing failed. Exiting.");
        return 1;
    }
}
else
{
    appLog.LogInformation("Resuming as device {DeviceId} ({DeviceName})", device.DeviceId, device.DeviceName);
}

var signaling = new SignalingClient(settings.ServerUrl, device.AgentToken,
    loggerFactory.CreateLogger<SignalingClient>());

var approval = new ApprovalPrompt { TimeoutSeconds = 30 };
var input = new SendInputInjector();
AgentPeer? activePeer = null;
Guid activeSessionId = Guid.Empty;
var peerLock = new object();

async Task TeardownPeer(string reason)
{
    AgentPeer? toDispose = null;
    Guid id;
    lock (peerLock)
    {
        toDispose = activePeer;
        id = activeSessionId;
        activePeer = null;
        activeSessionId = Guid.Empty;
    }
    if (toDispose is not null)
    {
        appLog.LogInformation("Tearing down session {Id} ({Reason})", id, reason);
        await toDispose.DisposeAsync();
    }
    input.ReleaseAllHeld();
}

signaling.SessionRequested += async evt =>
{
    appLog.LogInformation("Session requested: {SessionId} from {Ip}", evt.SessionId, evt.InitiatorIp);
    var approved = await approval.AskAsync(
        new ApprovalRequest(evt.SessionId, evt.InitiatorUserId ?? "", evt.InitiatorIp, evt.CreatedAtUtc),
        CancellationToken.None);
    if (!approved)
    {
        await signaling.RejectAsync(evt.SessionId, "user-rejected");
        return;
    }

    var capture = new GdiScreenCapture(settings.FrameRate, settings.MaxWidth, settings.MaxHeight);
    var encoder = new VP8Codec { BaseQIndex = 40, KeyframeIntervalFrames = settings.FrameRate * 2 };
    var rtcConfig = new RTCConfiguration
    {
        iceServers = new List<RTCIceServer> { new() { urls = "stun:stun.l.google.com:19302" } },
    };
    var peer = new AgentPeer(
        loggerFactory.CreateLogger("AgentPeer"),
        capture, encoder, input, rtcConfig,
        signalMsg => signaling.SignalAsync(evt.SessionId, signalMsg));
    lock (peerLock) { activePeer = peer; activeSessionId = evt.SessionId; }
    await peer.StartAsync();
    peer.AllowInput(true);

    await signaling.ApproveAsync(evt.SessionId);
};

signaling.Signal += async (sessionId, msg) =>
{
    AgentPeer? peer;
    lock (peerLock) { peer = sessionId == activeSessionId ? activePeer : null; }
    if (peer is null) { appLog.LogWarning("Signal for unknown session {Id}", sessionId); return; }
    await peer.HandleSignalAsync(msg);
};

signaling.SessionEnded += async (id, reason) =>
{
    await TeardownPeer(reason ?? "ended");
};

try
{
    await signaling.StartAsync(CancellationToken.None);
    appLog.LogInformation("Connected to signaling hub. Waiting for session requests. Press Ctrl-C to exit.");
}
catch (Exception ex)
{
    appLog.LogError(ex, "Failed to connect to signaling hub.");
    return 1;
}

using var exitSignal = new ManualResetEventSlim(false);
Console.CancelKeyPress += (_, e) => { e.Cancel = true; exitSignal.Set(); };
AppDomain.CurrentDomain.ProcessExit += (_, _) => exitSignal.Set();

exitSignal.Wait();

appLog.LogInformation("Shutting down.");
await TeardownPeer("agent-exit");
await signaling.DisposeAsync();
await Log.CloseAndFlushAsync();
return 0;
