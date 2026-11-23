using System.Text.Json.Serialization;

namespace Coleso;

public class WheelItem
{
    public string Text { get; set; } = "";
    /// <summary>Путь к картинке-фону сектора (пусто = нет картинки).</summary>
    public string Image { get; set; } = "";
}

public class Wheel
{
    public string Name { get; set; } = "Колесо";
    /// <summary>Ключ темы оформления из <see cref="WheelThemes"/> (см. config.json).</summary>
    public string Theme { get; set; } = WheelThemes.DefaultKey;
    public List<WheelItem> Items { get; set; } = new();

    [JsonIgnore]
    public int SectorCount => Math.Clamp(Items.Count, 4, 12);
}

public class AppSettings
{
    public bool AutoStart { get; set; } = true;
    public bool RestoreClipboard { get; set; } = true;
    public bool ShowHints { get; set; } = true;
    public double WheelSize { get; set; } = 210;      // радиус в DIP
    public string ExcludedProcesses { get; set; } = ""; // процессы через запятую
    public int LastWheel { get; set; }
}

public class Config
{
    public AppSettings Settings { get; set; } = new();
    public List<Wheel> Wheels { get; set; } = new();
}
