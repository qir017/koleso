namespace Coleso;

public static class ScreenHelper
{
    /// <summary>Масштаб DPI монитора, содержащего точку (1.0 = 100%).</summary>
    public static double ScaleAt(Native.POINT pt)
    {
        try
        {
            IntPtr mon = Native.MonitorFromPoint(pt, Native.MONITOR_DEFAULTTONEAREST);
            if (mon != IntPtr.Zero &&
                Native.GetDpiForMonitor(mon, Native.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 &&
                dpiX > 0)
                return dpiX / 96.0;
        }
        catch { }
        return 1.0;
    }
}
