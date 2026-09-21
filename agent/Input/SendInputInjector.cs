using System.Runtime.InteropServices;

namespace RebelDesk.Agent.Input;

/// <summary>
/// Injects mouse and keyboard events onto the local desktop using <c>SendInput</c>.
/// Scancodes are used for keyboard events so non-US layouts on the host produce the
/// key the user pressed on the viewer, not the character mapped by a US layout. All
/// held modifiers and mouse buttons are released on <see cref="ReleaseAllHeld"/> to
/// prevent stuck-key states after disconnect.
/// </summary>
public sealed class SendInputInjector
{
    private readonly HashSet<ushort> _heldScanCodes = new();
    private byte _heldMouseButtons; // bit 0: left, 1: right, 2: middle
    private readonly object _lock = new();

    public void MouseMoveAbsolute(double normalizedX, double normalizedY)
    {
        // MOUSEEVENTF_ABSOLUTE uses 0..65535 across the virtual screen. We map 0..1 to primary monitor.
        var x = (int)Math.Round(Math.Clamp(normalizedX, 0.0, 1.0) * 65535);
        var y = (int)Math.Round(Math.Clamp(normalizedY, 0.0, 1.0) * 65535);
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            u = new InputUnion { mi = new MOUSEINPUT { dx = x, dy = y, dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE } }
        };
        Send(input);
    }

    public void MouseButton(int button, bool down)
    {
        uint flag = button switch
        {
            0 => down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP,
            1 => down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP,
            2 => down ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP,
            _ => 0,
        };
        if (flag == 0) return;
        lock (_lock)
        {
            if (down) _heldMouseButtons |= (byte)(1 << button);
            else _heldMouseButtons &= (byte)~(1 << button);
        }
        var input = new INPUT { type = INPUT_MOUSE, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = flag } } };
        Send(input);
    }

    public void MouseWheel(int deltaY, int deltaX)
    {
        if (deltaY != 0)
        {
            var input = new INPUT { type = INPUT_MOUSE, u = new InputUnion { mi = new MOUSEINPUT { mouseData = deltaY, dwFlags = MOUSEEVENTF_WHEEL } } };
            Send(input);
        }
        if (deltaX != 0)
        {
            var input = new INPUT { type = INPUT_MOUSE, u = new InputUnion { mi = new MOUSEINPUT { mouseData = deltaX, dwFlags = MOUSEEVENTF_HWHEEL } } };
            Send(input);
        }
    }

    public void KeyEvent(ushort scanCode, bool down, bool extended)
    {
        uint flags = KEYEVENTF_SCANCODE;
        if (!down) flags |= KEYEVENTF_KEYUP;
        if (extended) flags |= KEYEVENTF_EXTENDEDKEY;

        lock (_lock)
        {
            if (down) _heldScanCodes.Add(scanCode);
            else _heldScanCodes.Remove(scanCode);
        }

        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion { ki = new KEYBDINPUT { wScan = scanCode, dwFlags = flags } }
        };
        Send(input);
    }

    public void ReleaseAllHeld()
    {
        ushort[] scans;
        byte mouseBits;
        lock (_lock) { scans = _heldScanCodes.ToArray(); mouseBits = _heldMouseButtons; }

        foreach (var sc in scans)
            KeyEvent(sc, down: false, extended: false);

        for (int i = 0; i < 3; i++)
            if ((mouseBits & (1 << i)) != 0)
                MouseButton(i, down: false);
    }

    private static void Send(INPUT input)
    {
        var arr = new[] { input };
        _ = SendInput(1, arr, Marshal.SizeOf<INPUT>());
    }

    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint MOUSEEVENTF_HWHEEL = 0x01000;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public InputUnion u; }
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT { public int dx; public int dy; public int mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT { public uint uMsg; public ushort wParamL; public ushort wParamH; }
}
