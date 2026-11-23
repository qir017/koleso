using System.IO;
using System.Windows.Media.Imaging;

namespace Coleso;

/// <summary>
/// Кэш декодированных картинок секторов. Ключ — путь + время изменения файла,
/// поэтому замена файла подхватывается без перезапуска. Ошибочные файлы
/// кэшируются как null, чтобы не декодировать их каждый раз.
/// </summary>
internal static class ImageCache
{
    private const int MaxEntries = 64;
    private static readonly object _lock = new();
    private static readonly Dictionary<string, BitmapImage?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public static BitmapImage? Get(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        string key;
        try
        {
            var fi = new FileInfo(path);
            if (!fi.Exists) return null;
            key = path + "|" + fi.LastWriteTimeUtc.Ticks + "|" + fi.Length;
        }
        catch { return null; }

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var cached)) return cached;

            BitmapImage? bmp = null;
            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad; // файл не держим открытым
                img.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                img.EndInit();
                img.Freeze();
                bmp = img;
            }
            catch { bmp = null; } // не картинка / не поддерживаемый формат

            if (_cache.Count >= MaxEntries) _cache.Clear();
            _cache[key] = bmp;
            return bmp;
        }
    }
}
