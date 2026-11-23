using Microsoft.Win32;

namespace Coleso;

public static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Coleso";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string v && v.Contains("Coleso.exe");
        }
        catch { return false; }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (key == null) throw new InvalidOperationException("Не удалось открыть ключ реестра.");
        if (enabled)
        {
            var path = Environment.ProcessPath;
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("Неизвестен путь к exe.");
            key.SetValue(ValueName, $"\"{path}\"");
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }
}
