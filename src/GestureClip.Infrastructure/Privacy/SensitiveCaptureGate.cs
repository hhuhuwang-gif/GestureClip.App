using System.Runtime.InteropServices;
using System.Text;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Privacy;
using GestureClip.Core.Settings;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Privacy;

public sealed class SensitiveCaptureGate : ISensitiveCaptureGate
{
    private const int EsPassword = 0x0020;
    private const int GwlStyle = -16;

    private readonly ISettingsService _settingsService;
    private readonly ILogger<SensitiveCaptureGate> _logger;

    private static readonly HashSet<string> PasswordManagerProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "1Password.exe",
        "Bitwarden.exe",
        "KeePass.exe",
        "KeePassXC.exe",
        "LastPass.exe",
        "Enpass.exe",
        "Dashlane.exe",
        "Authenticator.exe",
        "Authy Desktop.exe",
        "RoboForm.exe"
    };

    public SensitiveCaptureGate(ISettingsService settingsService, ILogger<SensitiveCaptureGate> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public bool ShouldSkipCapture(string? sourceProcess, string? sourceAppOrTitle)
    {
        if (!_settingsService.Get(SettingKeys.PrivacySuppressPasswordFields, true))
        {
            return false;
        }

        var process = NormalizeProcess(sourceProcess);
        if (!string.IsNullOrEmpty(process) &&
            (PasswordManagerProcesses.Contains(process) ||
             DefaultPrivacyBlacklist.ProcessNames.Any(name =>
                 name.Equals(process, StringComparison.OrdinalIgnoreCase))))
        {
            _logger.LogInformation("Capture skip: password manager process {Process}", process);
            return true;
        }

        if (LooksLikeSensitiveTitle(sourceAppOrTitle))
        {
            _logger.LogInformation("Capture skip: sensitive window title.");
            return true;
        }

        if (IsPasswordFieldFocused())
        {
            _logger.LogInformation("Capture skip: password-style edit control focused.");
            return true;
        }

        return false;
    }

    private static string NormalizeProcess(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return "";
        }

        var normalized = processName.Trim();
        return normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"{normalized}.exe";
    }

    private static bool LooksLikeSensitiveTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var t = title.Trim();
        return t.Contains("密码", StringComparison.Ordinal) ||
               t.Contains("口令", StringComparison.Ordinal) ||
               t.Contains("验证码登录", StringComparison.Ordinal) ||
               t.Contains("password", StringComparison.OrdinalIgnoreCase) ||
               t.Contains("passcode", StringComparison.OrdinalIgnoreCase) ||
               t.Contains("sign in", StringComparison.OrdinalIgnoreCase) ||
               t.Contains("log in", StringComparison.OrdinalIgnoreCase) ||
               t.Contains("login", StringComparison.OrdinalIgnoreCase) &&
               (t.Contains("bank", StringComparison.OrdinalIgnoreCase) ||
                t.Contains("wallet", StringComparison.OrdinalIgnoreCase) ||
                t.Contains("secure", StringComparison.OrdinalIgnoreCase));
    }

    private bool IsPasswordFieldFocused()
    {
        try
        {
            var info = new GuiThreadInfo { CbSize = Marshal.SizeOf<GuiThreadInfo>() };
            if (!GetGUIThreadInfo(0, ref info))
            {
                return false;
            }

            var focus = info.HwndFocus;
            if (focus == IntPtr.Zero)
            {
                return false;
            }

            var style = GetWindowLongPtr(focus, GwlStyle).ToInt64();
            if ((style & EsPassword) != 0)
            {
                return true;
            }

            var className = GetClassName(focus);
            if (className.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                className.Equals("PasswordBox", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Password field probe failed.");
            return false;
        }
    }

    private static string GetClassName(IntPtr hwnd)
    {
        var buffer = new StringBuilder(256);
        _ = GetClassName(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GuiThreadInfo info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int CbSize;
        public int Flags;
        public IntPtr HwndActive;
        public IntPtr HwndFocus;
        public IntPtr HwndCapture;
        public IntPtr HwndMenuOwner;
        public IntPtr HwndMoveSize;
        public IntPtr HwndCaret;
        public int CaretLeft;
        public int CaretTop;
        public int CaretRight;
        public int CaretBottom;
    }
}
