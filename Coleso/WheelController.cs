using System.Diagnostics;
using System.IO;
using System.Windows.Threading;

namespace Coleso;

/// <summary>
/// Конечный автомат колеса: получает события мыши из хука и решает,
/// открыть/закрыть меню, сменить колесо, выбрать сектор или вставить текст.
/// </summary>
public class WheelController
{
    private readonly Dispatcher _dispatcher;
    private readonly RadialOverlay _overlay;
    private readonly Func<Config> _getConfig;
    private readonly Action _saveConfig;

    private bool _open;
    private bool _paused;
    private int _wheelIdx;
    private int _selIdx = -1;
    private Native.POINT _centerPx;
    private double _outerPx;
    private double _deadPx;
    private IntPtr _targetHwnd;
    private DispatcherTimer? _demoTimer;

    // двойное нажатие СКМ для открытия колеса
    private bool _middleArmed;
    private uint _lastMiddleTime;
    private Native.POINT _lastMiddlePt;

    public bool Paused
    {
        get => _paused;
        set { _paused = value; if (value) Close(true); }
    }

    public bool IsOpen => _open;

    public WheelController(Dispatcher dispatcher, RadialOverlay overlay,
        Func<Config> getConfig, Action saveConfig)
    {
        _dispatcher = dispatcher;
        _overlay = overlay;
        _getConfig = getConfig;
        _saveConfig = saveConfig;
    }

    public bool Handle(int msg, Native.MSLLHOOKSTRUCT m)
    {
        if (_paused) return false;
        return _open ? HandleOpen(msg, m) : HandleClosed(msg, m);
    }

    // ---------- меню закрыто ----------

    private bool HandleClosed(int msg, Native.MSLLHOOKSTRUCT m)
    {
        if (msg != Native.WM_MBUTTONDOWN) return false;

        _targetHwnd = Native.GetForegroundWindow();
        if (IsExcluded(ProcNameOf(_targetHwnd))) { _middleArmed = false; return false; }

        // Колесо открывается только двойным нажатием СКМ: второй клик должен
        // уложиться в системное время двойного клика и в допустимое смещение курсора.
        uint now = m.time != 0 ? m.time : (uint)Environment.TickCount;
        bool dbl = _middleArmed
            && unchecked(now - _lastMiddleTime) <= Native.GetDoubleClickTime()
            && Math.Abs(m.pt.X - _lastMiddlePt.X) <= Math.Max(Native.GetSystemMetrics(Native.SM_CXDOUBLECLK), 24)
            && Math.Abs(m.pt.Y - _lastMiddlePt.Y) <= Math.Max(Native.GetSystemMetrics(Native.SM_CYDOUBLECLK), 24);

        _lastMiddleTime = now;
        _lastMiddlePt = m.pt;
        _middleArmed = true;
        if (!dbl) return false;   // первый клик: пропускаем в приложение
        _middleArmed = false;     // двойной клик: открываем, отсчёт начинается заново

        var cfg = _getConfig();
        _wheelIdx = Math.Clamp(cfg.Settings.LastWheel, 0, cfg.Wheels.Count - 1);
        _selIdx = -1;
        _centerPx = m.pt;

        double scale = ScreenHelper.ScaleAt(m.pt);
        _outerPx = cfg.Settings.WheelSize * scale;
        _deadPx = _outerPx * 0.22;

        _open = true;
        var wheel = cfg.Wheels[_wheelIdx];
        int total = cfg.Wheels.Count;
        _dispatcher.BeginInvoke(() => _overlay.ShowWheel(wheel, _wheelIdx, total, m.pt));
        return true; // съедаем СКМ
    }

    // ---------- меню открыто ----------

    private bool HandleOpen(int msg, Native.MSLLHOOKSTRUCT m)
    {
        switch (msg)
        {
            case Native.WM_MBUTTONDOWN:
                Close(true);
                return true;
            case Native.WM_MBUTTONUP:
                return true;

            case Native.WM_RBUTTONDOWN:
                NextWheel(+1);
                return true;
            case Native.WM_RBUTTONUP:
                return true;

            case Native.WM_MOUSEWHEEL:
            {
                short delta = unchecked((short)((m.mouseData >> 16) & 0xFFFF));
                int dir = delta > 0 ? -1 : 1; // вниз = по часовой
                int n = _getConfig().Wheels[_wheelIdx].Items.Count;
                if (n == 0) return true;
                _selIdx = _selIdx < 0
                    ? (dir > 0 ? 0 : n - 1)
                    : (_selIdx + dir + n) % n;
                int sel = _selIdx;
                _dispatcher.BeginInvoke(() => _overlay.SetHighlight(sel));
                return true;
            }

            case Native.WM_LBUTTONDOWN:
            {
                double dist = Dist(m.pt, _centerPx);
                if (dist > _outerPx)
                {
                    Close(false);   // клик мимо колеса: закрыть и пропустить клик в приложение
                    return false;
                }
                if (_selIdx >= 0)
                {
                    var items = _getConfig().Wheels[_wheelIdx].Items;
                    var item = _selIdx < items.Count ? items[_selIdx] : null;
                    string text = item?.Text ?? "";
                    string image = item?.Image ?? "";
                    bool hasImage = !string.IsNullOrWhiteSpace(image) && File.Exists(image);
                    bool restore = _getConfig().Settings.RestoreClipboard;
                    Close(true);
                    // картинка задана — вставляем только её, иначе только текст
                    if (hasImage)
                        _dispatcher.BeginInvoke(() => PasteEngine.PasteImage(image, _targetHwnd, restore));
                    else if (!string.IsNullOrWhiteSpace(text))
                        _dispatcher.BeginInvoke(() => PasteEngine.Paste(text, _targetHwnd, restore));
                }
                else
                {
                    Close(true);    // клик по центру без выбора = отмена
                }
                return true;
            }

            case Native.WM_LBUTTONUP:
                return true;

            case Native.WM_MOUSEMOVE:
            {
                double dx = m.pt.X - _centerPx.X;
                double dy = m.pt.Y - _centerPx.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist >= _deadPx && dist <= _outerPx)
                {
                    int n = _getConfig().Wheels[_wheelIdx].Items.Count;
                    if (n > 0)
                    {
                        int idx = AngleIndex(dx, dy, 360.0 / n, n);
                        if (idx != _selIdx)
                        {
                            _selIdx = idx;
                            _dispatcher.BeginInvoke(() => _overlay.SetHighlight(idx));
                        }
                    }
                }
                return false; // движение не блокируем
            }
        }
        return false;
    }

    // ---------- служебное ----------

    private void NextWheel(int dir)
    {
        var cfg = _getConfig();
        int n = cfg.Wheels.Count;
        if (n == 0) return;
        _wheelIdx = ((_wheelIdx + dir) % n + n) % n;
        cfg.Settings.LastWheel = _wheelIdx;
        _selIdx = -1;
        var wheel = cfg.Wheels[_wheelIdx];
        int idx = _wheelIdx;
        int total = n;
        _dispatcher.BeginInvoke(() => _overlay.SwitchWheel(wheel, idx, total, dir));
    }

    private void Close(bool fade)
    {
        if (!_open) return;
        _open = false;
        var cfg = _getConfig();
        if (cfg.Settings.LastWheel != _wheelIdx)
        {
            cfg.Settings.LastWheel = _wheelIdx;
            _saveConfig();
        }
        _dispatcher.BeginInvoke(() => _overlay.HideWheel(fade));
    }

    /// <summary>Показать колесо в позиции курсора без цели вставки (для теста/демо).</summary>
    public void OpenDemo()
    {
        if (_open) return;
        var cfg = _getConfig();
        Native.GetCursorPos(out var p);
        _targetHwnd = IntPtr.Zero;
        _wheelIdx = Math.Clamp(cfg.Settings.LastWheel, 0, cfg.Wheels.Count - 1);
        _selIdx = -1;
        _centerPx = p;
        double scale = ScreenHelper.ScaleAt(p);
        _outerPx = cfg.Settings.WheelSize * scale;
        _deadPx = _outerPx * 0.22;
        _open = true;
        var wheel = cfg.Wheels[_wheelIdx];
        int idx = _wheelIdx;
        int total = cfg.Wheels.Count;
        _dispatcher.BeginInvoke(() => _overlay.ShowWheel(wheel, idx, total, p));

        _demoTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _demoTimer.Stop();
        _demoTimer.Tick += OnDemoTick;
        _demoTimer.Start();
    }

    private void OnDemoTick(object? sender, EventArgs e)
    {
        _demoTimer?.Stop();
        if (_open) Close(true);
    }

    private static double Dist(Native.POINT a, Native.POINT b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Индекс сектора по углу. Сектор 0 сверху, далее по часовой.</summary>
    private static int AngleIndex(double dx, double dy, double stepDeg, int n)
    {
        double ang = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
        if (ang < 0) ang += 360;
        return (int)Math.Floor((ang + stepDeg / 2.0) / stepDeg) % n;
    }

    private static string ProcNameOf(IntPtr hwnd)
    {
        try
        {
            Native.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return "";
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName.ToLowerInvariant();
        }
        catch { return ""; }
    }

    private bool IsExcluded(string proc)
    {
        var list = _getConfig().Settings.ExcludedProcesses;
        if (string.IsNullOrWhiteSpace(list) || string.IsNullOrEmpty(proc)) return false;
        foreach (var raw in list.Split(','))
        {
            var s = raw.Trim().ToLowerInvariant();
            if (s.Length == 0) continue;
            if (!s.EndsWith(".exe")) s += ".exe";
            if (s == proc) return true;
        }
        return false;
    }
}
