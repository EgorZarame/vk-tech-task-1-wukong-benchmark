using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WukongBenchRunner.Running;

public interface IInputSimulator
{
    bool IsForeground(nint window);

    /// <summary>Выводит окно на передний план.</summary>
    bool TryActivate(nint window);

    void PressKey(ushort virtualKey);

    /// <summary>Клик по точке в долях клиентской области окна (0..1), не зависит от разрешения.</summary>
    void ClickRelative(nint window, double x, double y);
}

/// <summary>Эмуляция ввода через SendInput. Клавиши отправляются со скан-кодами — игры часто читают именно их.</summary>
[SupportedOSPlatform("windows")]
public sealed partial class Win32Input : IInputSimulator
{
    private const ushort VkMenu = 0x12; // Alt

    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventScanCode = 0x0008;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const int SwRestore = 9;

    public bool IsForeground(nint window) => GetForegroundWindow() == window;

    public bool TryActivate(nint window)
    {
        if (IsIconic(window)) ShowWindow(window, SwRestore);

        // Windows не даёт фоновому процессу забрать фокус. Нажатие Alt
        // снимает это ограничение для SetForegroundWindow.
        SendKey(VkMenu, keyUp: false);
        SendKey(VkMenu, keyUp: true);
        SetForegroundWindow(window);
        return IsForeground(window);
    }

    public void PressKey(ushort virtualKey)
    {
        SendKey(virtualKey, keyUp: false);
        Thread.Sleep(50);
        SendKey(virtualKey, keyUp: true);
    }

    public void ClickRelative(nint window, double x, double y)
    {
        if (!GetClientRect(window, out var rect)) return;

        var point = new Point
        {
            X = (int)(rect.Right * x),
            Y = (int)(rect.Bottom * y),
        };
        if (!ClientToScreen(window, ref point)) return;

        SetCursorPos(point.X, point.Y);
        Thread.Sleep(50);
        Send(MouseInput(MouseEventLeftDown));
        Thread.Sleep(50);
        Send(MouseInput(MouseEventLeftUp));
    }

    private static void SendKey(ushort virtualKey, bool keyUp)
    {
        var input = new Input
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = virtualKey,
                    ScanCode = (ushort)MapVirtualKey(virtualKey, 0),
                    Flags = KeyEventScanCode | (keyUp ? KeyEventKeyUp : 0),
                },
            },
        };
        Send(input);
    }

    private static Input MouseInput(uint flags) => new()
    {
        Type = InputMouse,
        Union = new InputUnion { Mouse = new MouseInputData { Flags = flags } },
    };

    private static void Send(Input input) => SendInput(1, [input], Marshal.SizeOf<Input>());

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInputData Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInputData
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [LibraryImport("user32.dll")]
    private static partial uint SendInput(uint count, [In] Input[] inputs, int size);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(nint window, int command);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetClientRect(nint window, out Rect rect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ClientToScreen(nint window, ref Point point);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetCursorPos(int x, int y);

    [LibraryImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
    private static partial uint MapVirtualKey(uint code, uint mapType);
}
