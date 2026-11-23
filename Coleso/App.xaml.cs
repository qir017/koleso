using System.Threading;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Threading;
using WF = System.Windows.Forms;

namespace Coleso;

public partial class App : Application
{
    private static Mutex? _mutex;
    private MouseHook _hook = new();
    private WheelController _controller = null!;
    private RadialOverlay _overlay = null!;
    private WF.NotifyIcon _tray = null!;
    private SettingsWindow? _settings;

    public static Config Config { get; set; } = null!;
    public static App Instance => (App)Current;
    public WheelController Controller => _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, "Coleso_SingleInstance", out bool created);
        if (!created)
        {
            MessageBox.Show("Coleso уже запущен — значок в трее возле часов.",
                "Coleso", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        Config = ConfigStore.Load();

        // самопочинка автозапуска: включён в настройках, но ключа нет
        if (Config.Settings.AutoStart && !AutoStart.IsEnabled())
        {
            try { AutoStart.SetEnabled(true); } catch { }
        }

        _overlay = new RadialOverlay();
        _controller = new WheelController(Dispatcher, _overlay,
            () => Config, () => ConfigStore.Save(Config));

        CreateTray();

        _hook.Handler = _controller.Handle;
        if (!_hook.Install())
            MessageBox.Show("Не удалось установить глобальный хук мыши.",
                "Coleso", MessageBoxButton.OK, MessageBoxImage.Warning);

        DispatcherUnhandledException += (s, a) =>
        {
            MessageBox.Show("Ошибка: " + a.Exception.Message,
                "Coleso", MessageBoxButton.OK, MessageBoxImage.Error);
            a.Handled = true;
        };

        var args = e.Args.Select(a => a.ToLowerInvariant()).ToArray();
        if (args.Contains("--settings"))
            ShowSettings();
        if (args.Contains("--demo"))
            Dispatcher.BeginInvoke(() => _controller.OpenDemo(), DispatcherPriority.ApplicationIdle);
    }

    private void CreateTray()
    {
        var icon = IconHelper.CreateAppIcon(32);

        _tray = new WF.NotifyIcon
        {
            Icon = icon,
            Text = "Coleso — менеджер вставок",
            Visible = true
        };

        var menu = new WF.ContextMenuStrip();
        menu.Items.Add("Настройки", null, (s, e) => ShowSettings());
        menu.Items.Add("Показать колесо", null, (s, e) => _controller.OpenDemo());

        var pause = new WF.ToolStripMenuItem("Пауза") { CheckOnClick = true };
        pause.Click += (s, e) => _controller.Paused = pause.Checked;
        menu.Items.Add(pause);

        var auto = new WF.ToolStripMenuItem("Запускать с Windows")
        {
            Checked = Config.Settings.AutoStart,
            CheckOnClick = true
        };
        auto.Click += (s, e) =>
        {
            try
            {
                AutoStart.SetEnabled(auto.Checked);
                Config.Settings.AutoStart = auto.Checked;
                ConfigStore.Save(Config);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось изменить автозапуск: " + ex.Message, "Coleso");
                auto.Checked = AutoStart.IsEnabled();
            }
        };
        menu.Items.Add(auto);

        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add("Выход", null, (s, e) => Shutdown());

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (s, e) => ShowSettings();
    }

    public void ShowSettings()
    {
        if (_settings == null || !_settings.IsLoaded)
        {
            _settings = new SettingsWindow();
            _settings.Show();
        }
        else
        {
            if (_settings.WindowState == WindowState.Minimized)
                _settings.WindowState = WindowState.Normal;
            _settings.Activate();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ConfigStore.Save(Config);
        _hook.Dispose();
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        try { _mutex?.ReleaseMutex(); } catch { }
        base.OnExit(e);
    }
}
