using System.Runtime.InteropServices;

namespace GestureClip.Infrastructure.Win32;

public static class KeyboardInputNativeMethods
{
    public const ushort VkBack = 0x08;
    public const ushort VkTab = 0x09;
    public const ushort VkReturn = 0x0D;
    public const ushort VkShift = 0x10;
    public const ushort VkControl = 0x11;
    public const ushort VkMenu = 0x12;
    public const ushort VkPause = 0x13;
    public const ushort VkEscape = 0x1B;
    public const ushort VkSpace = 0x20;
    public const ushort VkPageUp = 0x21;
    public const ushort VkPageDown = 0x22;
    public const ushort VkEnd = 0x23;
    public const ushort VkHome = 0x24;
    public const ushort VkLeft = 0x25;
    public const ushort VkUp = 0x26;
    public const ushort VkRight = 0x27;
    public const ushort VkDown = 0x28;
    public const ushort VkDelete = 0x2E;
    public const ushort Vk0 = 0x30;
    public const ushort VkD = 0x44;
    public const ushort VkF = 0x46;
    public const ushort VkI = 0x49;
    public const ushort VkN = 0x4E;
    public const ushort VkP = 0x50;
    public const ushort VkS = 0x53;
    public const ushort VkT = 0x54;
    public const ushort VkW = 0x57;
    public const ushort VkA = 0x41;
    public const ushort VkC = 0x43;
    public const ushort VkV = 0x56;
    public const ushort VkX = 0x58;
    public const ushort VkY = 0x59;
    public const ushort VkZ = 0x5A;
    public const ushort VkLWin = 0x5B;
    public const ushort VkF5 = 0x74;
    public const ushort VkF11 = 0x7A;
    public const ushort VkVolumeMute = 0xAD;
    public const ushort VkVolumeDown = 0xAE;
    public const ushort VkVolumeUp = 0xAF;
    public const ushort VkMediaNextTrack = 0xB0;
    public const ushort VkMediaPrevTrack = 0xB1;
    public const ushort VkMediaPlayPause = 0xB3;
    public const ushort VkOemPlus = 0xBB;
    public const ushort VkOemMinus = 0xBD;
    public const uint InputKeyboard = 1;
    public const uint KeyEventKeyUp = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(
        uint cInputs,
        [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs,
        int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;

        [FieldOffset(0)]
        public MOUSEINPUT mi;

        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }
}
