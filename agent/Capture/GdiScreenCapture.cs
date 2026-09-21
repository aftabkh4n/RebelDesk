using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace RebelDesk.Agent.Capture;

/// <summary>
/// Captures the primary monitor using GDI BitBlt (via <see cref="Graphics.CopyFromScreen"/>).
/// Emits BGRA24 frames at the requested target frame rate. This is the v0.1 baseline
/// implementation. It works everywhere Windows works, including inside RDP sessions,
/// but is slow (~10-15 fps at 1080p on typical hardware) and captures no hardware
/// overlays or protected content. DXGI Desktop Duplication is the target for v0.3.
/// </summary>
public sealed class GdiScreenCapture : IScreenCapture
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int Stride => Width * 4;

    private readonly int _targetFps;
    private readonly int _maxWidth;
    private readonly int _maxHeight;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private byte[]? _bgraBuf;
    public event EventHandler<CapturedFrame>? FrameCaptured;

    public GdiScreenCapture(int targetFps, int maxWidth, int maxHeight)
    {
        _targetFps = Math.Clamp(targetFps, 5, 60);
        _maxWidth = maxWidth;
        _maxHeight = maxHeight;
    }

    public void Start()
    {
        if (_loop != null) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunLoop(_cts.Token));
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_loop != null) { try { await _loop.ConfigureAwait(false); } catch { } }
        _loop = null;
        _cts?.Dispose();
        _cts = null;
    }

    private void RunLoop(CancellationToken ct)
    {
        var screenW = GetSystemMetrics(SM_CXSCREEN);
        var screenH = GetSystemMetrics(SM_CYSCREEN);
        Width = Math.Min(screenW, _maxWidth);
        Height = Math.Min(screenH, _maxHeight);
        _bgraBuf = new byte[Width * Height * 4];

        using var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);

        var interval = TimeSpan.FromSeconds(1.0 / _targetFps);
        var sw = Stopwatch.StartNew();
        var next = sw.Elapsed;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                g.CopyFromScreen(0, 0, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
                var data = bmp.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    unsafe
                    {
                        var src = (byte*)data.Scan0.ToPointer();
                        var stride = data.Stride;
                        fixed (byte* dst = _bgraBuf)
                        {
                            for (int y = 0; y < Height; y++)
                                Buffer.MemoryCopy(src + y * stride, dst + y * Width * 4, Width * 4, Width * 4);
                        }
                    }
                }
                finally { bmp.UnlockBits(data); }

                FrameCaptured?.Invoke(this, new CapturedFrame(_bgraBuf!, Width, Height, DateTime.UtcNow));
            }
            catch (Exception)
            {
                // A capture failure typically means desktop is transitioning (lock screen, session switch).
                // Skip this frame; loop will retry.
            }

            next += interval;
            var delay = next - sw.Elapsed;
            if (delay > TimeSpan.Zero) Thread.Sleep(delay);
            else next = sw.Elapsed; // fell behind; reset baseline
        }
    }

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
}

public interface IScreenCapture
{
    int Width { get; }
    int Height { get; }
    int Stride { get; }
    event EventHandler<CapturedFrame>? FrameCaptured;
    void Start();
    Task StopAsync();
}

public readonly record struct CapturedFrame(byte[] Bgra, int Width, int Height, DateTime CapturedAtUtc);
