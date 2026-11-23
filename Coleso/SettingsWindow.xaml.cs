using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;

namespace Coleso;

public partial class SettingsWindow : Window
{
    private Config _work = null!;
    private bool _loading;

    private Wheel? CurrentWheel
    {
        get
        {
            if (_work == null || lstWheels == null) return null;
            return lstWheels.SelectedItem as Wheel ?? _work.Wheels.FirstOrDefault();
        }
    }

    public SettingsWindow()
    {
        InitializeComponent();
        _work = DeepCopy(App.Config);
        cmbTheme.ItemsSource = WheelThemes.All;
        lstWheels.ItemsSource = _work.Wheels;
        lstWheels.SelectedIndex = Math.Clamp(_work.Settings.LastWheel, 0, _work.Wheels.Count - 1);
        LoadGlobal();
    }

    private static Config DeepCopy(Config c) =>
        JsonSerializer.Deserialize<Config>(JsonSerializer.Serialize(c))!;

    private void LoadGlobal()
    {
        _loading = true;
        chkAutoStart.IsChecked = _work.Settings.AutoStart;
        chkRestore.IsChecked = _work.Settings.RestoreClipboard;
        chkHints.IsChecked = _work.Settings.ShowHints;
        sldSize.Value = _work.Settings.WheelSize;
        txtExcluded.Text = _work.Settings.ExcludedProcesses;
        _loading = false;
    }

    // ---------- колёса ----------

    private void LstWheels_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || CurrentWheel is not { } w) return;
        _loading = true;
        txtName.Text = w.Name;
        cmbTheme.SelectedValue = w.Theme;
        sldSectors.Value = w.SectorCount;
        lblSectors.Text = $"Секторов: {w.SectorCount}";
        _loading = false;
        RefreshItems();
    }

    private void CmbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || lstWheels == null || cmbTheme == null) return;
        if (cmbTheme.SelectedValue is string key && CurrentWheel is { } w && w.Theme != key)
            w.Theme = key;
    }

    private void BtnAddWheel_Click(object sender, RoutedEventArgs e)
    {
        _work.Wheels.Add(new Wheel { Name = $"Колесо {_work.Wheels.Count + 1}" });
        lstWheels.Items.Refresh();
        lstWheels.SelectedIndex = _work.Wheels.Count - 1;
    }

    private void BtnDelWheel_Click(object sender, RoutedEventArgs e)
    {
        if (_work.Wheels.Count <= 1)
        {
            MessageBox.Show("Должно оставаться хотя бы одно колесо.", "Coleso");
            return;
        }
        var w = CurrentWheel;
        if (w == null) return;
        var res = MessageBox.Show($"Удалить колесо «{w.Name}»?", "Coleso",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res != MessageBoxResult.Yes) return;

        int i = Math.Max(0, lstWheels.SelectedIndex);
        _work.Wheels.Remove(w);
        lstWheels.Items.Refresh();
        lstWheels.SelectedIndex = Math.Clamp(i, 0, _work.Wheels.Count - 1);
    }

    private void TxtName_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || lstWheels == null) return;
        if (CurrentWheel is { } w && w.Name != txtName.Text)
        {
            w.Name = txtName.Text;
            lstWheels.Items.Refresh();
        }
    }

    private void SldSectors_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (lblSectors != null)
            lblSectors.Text = $"Секторов: {(int)e.NewValue}";
        if (_loading || CurrentWheel is not { } w || lstWheels == null) return;

        int target = (int)e.NewValue;
        while (w.Items.Count < target) w.Items.Add(new WheelItem());
        while (w.Items.Count > target) w.Items.RemoveAt(w.Items.Count - 1);
        RefreshItems();
    }

    // ---------- секторы ----------

    public sealed record ItemRow(int Num, string Preview, string Image);

    private static string Preview(string t)
    {
        if (string.IsNullOrWhiteSpace(t)) return "(пусто)";
        var line = t.Replace("\r", "").Split('\n')[0].Trim();
        return line.Length > 70 ? line[..70] + "…" : line;
    }

    private static string ImageName(string? p) =>
        string.IsNullOrWhiteSpace(p) ? "" : Path.GetFileName(p);

    private void RefreshItems()
    {
        var w = CurrentWheel;
        if (w == null) { lstItems.ItemsSource = null; return; }
        int sel = lstItems.SelectedIndex;
        lstItems.ItemsSource = w.Items
            .Select((it, i) => new ItemRow(i + 1, Preview(it.Text), ImageName(it.Image)))
            .ToList();
        if (sel >= 0 && sel < w.Items.Count)
            lstItems.SelectedIndex = sel;
        UpdateEditor();
    }

    private void LstItems_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateEditor();

    private void UpdateEditor()
    {
        _loading = true;
        var w = CurrentWheel;
        int idx = lstItems.SelectedIndex;
        bool ok = w != null && idx >= 0 && idx < w.Items.Count;
        txtText.Text = ok ? w!.Items[idx].Text : "";
        txtImage.Text = ok ? w!.Items[idx].Image : "";
        txtText.IsEnabled = ok;
        txtImage.IsEnabled = ok;
        SetPreview(ok ? w!.Items[idx].Image : null);
        _loading = false;
    }

    private void SetPreview(string? path) =>
        imgPreview.Source = string.IsNullOrWhiteSpace(path) ? null : ImageCache.Get(path);

    private void TxtText_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || lstItems == null) return;
        var w = CurrentWheel;
        int idx = lstItems.SelectedIndex;
        if (w != null && idx >= 0 && idx < w.Items.Count && w.Items[idx].Text != txtText.Text)
            w.Items[idx].Text = txtText.Text;
    }

    private void TxtImage_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || lstItems == null) return;
        var w = CurrentWheel;
        int idx = lstItems.SelectedIndex;
        if (w != null && idx >= 0 && idx < w.Items.Count && w.Items[idx].Image != txtImage.Text)
            w.Items[idx].Image = txtImage.Text.Trim();
        if (w != null && idx >= 0 && idx < w.Items.Count)
            SetPreview(w.Items[idx].Image);
    }

    /// <summary>Вставить картинку из буфера обмена: скриншот, «Копировать картинку», файл.</summary>
    private void BtnImageFromClip_Click(object sender, RoutedEventArgs e)
    {
        var w = CurrentWheel;
        int idx = lstItems.SelectedIndex;
        if (w == null || idx < 0 || idx >= w.Items.Count) return;

        try
        {
            string? path = null;

            // скопированный файл (Проводник, «Сохранить как…») — берём путь как есть
            if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                if (files.Count > 0 && !string.IsNullOrWhiteSpace(files[0]) && File.Exists(files[0]))
                    path = files[0];
            }

            // картинка из буфера: скриншот (Win+Shift+S), «Копировать картинку» и т.п.
            if (path == null && Clipboard.ContainsImage())
            {
                var img = Clipboard.GetImage();
                if (img != null)
                {
                    string dir = Path.Combine(ConfigStore.Folder, "images");
                    Directory.CreateDirectory(dir);
                    path = Path.Combine(dir, $"img_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
                    var enc = new PngBitmapEncoder();
                    enc.Frames.Add(BitmapFrame.Create(img));
                    using var fs = File.Create(path);
                    enc.Save(fs);
                }
            }

            if (path == null)
            {
                MessageBox.Show("В буфере обмена нет картинки и нет файла.\n" +
                    "Скопируйте картинку (скриншот или «Копировать картинку») и нажмите ещё раз.",
                    "Coleso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            w.Items[idx].Image = path;
            RefreshItems();
            lstItems.SelectedIndex = idx;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось вставить из буфера: " + ex.Message, "Coleso");
        }
    }

    private void BtnImageBrowse_Click(object sender, RoutedEventArgs e)
    {
        var w = CurrentWheel;
        int idx = lstItems.SelectedIndex;
        if (w == null || idx < 0 || idx >= w.Items.Count) return;

        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Картинка для сектора",
            Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|Все файлы|*.*"
        };
        if (dlg.ShowDialog(this) != true) return;

        w.Items[idx].Image = dlg.FileName;
        RefreshItems();
        lstItems.SelectedIndex = idx;
    }

    private void BtnImageClear_Click(object sender, RoutedEventArgs e)
    {
        var w = CurrentWheel;
        int idx = lstItems.SelectedIndex;
        if (w == null || idx < 0 || idx >= w.Items.Count) return;
        if (string.IsNullOrEmpty(w.Items[idx].Image)) return;

        w.Items[idx].Image = "";
        RefreshItems();
        lstItems.SelectedIndex = idx;
    }

    private void MoveItem(int dir)
    {
        var w = CurrentWheel;
        int i = lstItems.SelectedIndex;
        if (w == null || i < 0) return;
        int j = i + dir;
        if (j < 0 || j >= w.Items.Count) return;
        (w.Items[i], w.Items[j]) = (w.Items[j], w.Items[i]);
        RefreshItems();
        lstItems.SelectedIndex = j;
    }

    private void BtnUp_Click(object sender, RoutedEventArgs e) => MoveItem(-1);
    private void BtnDown_Click(object sender, RoutedEventArgs e) => MoveItem(+1);

    private void BtnDelItem_Click(object sender, RoutedEventArgs e)
    {
        var w = CurrentWheel;
        int i = lstItems.SelectedIndex;
        if (w == null || i < 0) return;
        if (w.Items.Count <= 4)
        {
            MessageBox.Show("Минимум 4 сектора.", "Coleso");
            return;
        }
        w.Items.RemoveAt(i);
        _loading = true;
        sldSectors.Value = w.Items.Count;
        _loading = false;
        RefreshItems();
    }

    // ---------- сохранение / тест ----------

    private void ApplyToConfig()
    {
        _work.Settings.AutoStart = chkAutoStart.IsChecked == true;
        _work.Settings.RestoreClipboard = chkRestore.IsChecked == true;
        _work.Settings.ShowHints = chkHints.IsChecked == true;
        _work.Settings.WheelSize = sldSize.Value;
        _work.Settings.ExcludedProcesses = txtExcluded.Text;
        _work.Settings.LastWheel = App.Config.Settings.LastWheel; // не трогаем
        App.Config = DeepCopy(_work);
    }

    private void BtnTest_Click(object sender, RoutedEventArgs e)
    {
        ApplyToConfig();          // тест показывает текущие правки
        App.Instance.Controller.OpenDemo();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        ApplyToConfig();
        ConfigStore.Save(App.Config);
        try
        {
            AutoStart.SetEnabled(App.Config.Settings.AutoStart);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Настройки сохранены, но автозапуск обновить не удалось: " + ex.Message,
                "Coleso", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        Close();
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        var res = MessageBox.Show(
            "Вернуть настройки и колёса к исходным? Текущие тексты будут потеряны.",
            "Coleso", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (res != MessageBoxResult.Yes) return;

        bool autoStart = chkAutoStart.IsChecked == true;
        _work = ConfigStore.DefaultConfig();
        _work.Settings.AutoStart = autoStart;
        lstWheels.ItemsSource = _work.Wheels;
        lstWheels.SelectedIndex = 0;
        LoadGlobal();
        RefreshItems();
    }
}
