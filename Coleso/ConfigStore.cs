using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Coleso;

public static class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Coleso");

    public static string FilePath { get; } = Path.Combine(Folder, "config.json");

    public static Config Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var cfg = JsonSerializer.Deserialize<Config>(File.ReadAllText(FilePath));
                if (cfg != null)
                {
                    Normalize(cfg);
                    return cfg;
                }
            }
        }
        catch { }
        var def = DefaultConfig();
        Save(def);
        return def;
    }

    public static void Save(Config cfg)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(cfg, JsonOpts));
            File.Move(tmp, FilePath, true);
        }
        catch { }
    }

    public static void Normalize(Config cfg)
    {
        cfg.Settings ??= new AppSettings();
        cfg.Wheels ??= new List<Wheel>();
        if (cfg.Wheels.Count == 0)
            cfg.Wheels.Add(new Wheel { Name = "Колесо 1" });
        cfg.Settings.LastWheel = Math.Clamp(cfg.Settings.LastWheel, 0, cfg.Wheels.Count - 1);
        if (cfg.Settings.WheelSize is < 140 or > 320)
            cfg.Settings.WheelSize = 210;
        foreach (var w in cfg.Wheels)
        {
            w.Items ??= new List<WheelItem>();
            if (!WheelThemes.IsValid(w.Theme)) w.Theme = WheelThemes.DefaultKey;
            foreach (var it in w.Items)
                it.Image ??= "";
            if (w.Items.Count < 4)
                while (w.Items.Count < 4) w.Items.Add(new WheelItem());
        }
    }

    public static Config DefaultConfig() => new()
    {
        Settings = new AppSettings { AutoStart = true },
        Wheels = new List<Wheel>
        {
            new()
            {
                Name = "Основное",
                Items = Items("Привет! 👋", "Спасибо большое!", "Хорошо, сделаю 👍", "Сейчас занят, отвечу позже",
                    "Пожалуйста 🙂", "Согласовано ✅", "Проверьте, пожалуйста", "Удачного дня!")
            },
            new()
            {
                Name = "Работа",
                Items = Items("Задача выполнена.", "Файл отправил, проверьте почту.", "Уточню и вернусь с ответом.",
                    "Давайте обсудим завтра на созвоне.", "Взял в работу.", "Нужна помощь с этим вопросом.")
            },
            new()
            {
                Name = "Ссылки и данные",
                Items = Items("https://mail.google.com", "https://github.com", "+7 900 000-00-00",
                    "ivan@example.com", "г. Москва", "Иван")
            },
        }
    };

    private static List<WheelItem> Items(params string[] texts) =>
        texts.Select(t => new WheelItem { Text = t }).ToList();
}
