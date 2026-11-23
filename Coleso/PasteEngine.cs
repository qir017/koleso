using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Clipboard = System.Windows.Clipboard;
using DataObject = System.Windows.DataObject;
using DataFormats = System.Windows.DataFormats;

namespace Coleso;

public static class PasteEngine
{
    /// <summary>Глубокая копия содержимого буфера (OLE-объект протухает после замены буфера).</summary>
    private sealed class SavedClip
    {
        public string? Text;
        public StringCollection? Files;
        public byte[]? PngImage;
    }

    private static SavedClip? Capture()
    {
        try
        {
            var d = Clipboard.GetDataObject();
            if (d == null) return null;

            var s = new SavedClip();
            if (d.GetDataPresent(DataFormats.UnicodeText))
                s.Text = d.GetData(DataFormats.UnicodeText) as string;
            else if (d.GetDataPresent(DataFormats.Text))
                s.Text = d.GetData(DataFormats.Text) as string;

            if (d.GetDataPresent(DataFormats.FileDrop) &&
                d.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                s.Files = new StringCollection();
                s.Files.AddRange(files);
            }

            if (d.GetDataPresent(DataFormats.Bitmap) &&
                d.GetData(DataFormats.Bitmap) is BitmapSource src)
            {
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(src));
                using var ms = new MemoryStream();
                enc.Save(ms);
                s.PngImage = ms.ToArray();
            }

            if (s.Text == null && s.Files == null && s.PngImage == null) return null;
            return s;
        }
        catch { return null; }
    }

    private static void Restore(SavedClip? s)
    {
        if (s == null) return;
        try
        {
            var d = new DataObject();
            if (s.Files != null) d.SetFileDropList(s.Files);
            if (s.PngImage != null)
            {
                using var ms = new MemoryStream(s.PngImage);
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = ms;
                img.EndInit();
                img.Freeze();
                d.SetImage(img);
            }
            if (s.Text != null) d.SetText(s.Text);
            Clipboard.SetDataObject(d, true);
        }
        catch { }
    }

    /// <summary>
    /// Кладёт текст в буфер и шлёт Ctrl+V в активное окно.
    /// Вызывать на UI (STA) потоке.
    /// </summary>
    public static async void Paste(string text, IntPtr targetHwnd, bool restoreClipboard)
    {
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            SavedClip? saved = restoreClipboard ? Capture() : null;

            if (targetHwnd != IntPtr.Zero && targetHwnd != Native.GetForegroundWindow())
            {
                Native.SetForegroundWindow(targetHwnd);
                await Task.Delay(60);
            }

            // буфер могут держать другие приложения — с ретраями
            for (int i = 0; ; i++)
            {
                try { Clipboard.SetText(text); break; }
                catch (COMException) when (i < 4) { await Task.Delay(120); }
            }

            await Task.Delay(70);      // дать буферу устояться
            SendCtrlV();

            if (saved != null)
            {
                await Task.Delay(700); // приложение успело забрать вставку
                Restore(saved);
            }
        }
        catch { }
    }

    /// <summary>
    /// Кладёт картинку в буфер двумя форматами сразу — как файл-вложение (FileDrop)
    /// и как изображение (Bitmap), чтобы её приняли и мессенджеры (прикрепление файла),
    /// и приложения, вставляющие картинку через Ctrl+V. Затем шлёт Ctrl+V в активное окно.
    /// Вызывать на UI (STA) потоке.
    /// </summary>
    public static async void PasteImage(string path, IntPtr targetHwnd, bool restoreClipboard)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try
        {
            SavedClip? saved = restoreClipboard ? Capture() : null;

            if (targetHwnd != IntPtr.Zero && targetHwnd != Native.GetForegroundWindow())
            {
                Native.SetForegroundWindow(targetHwnd);
                await Task.Delay(60);
            }

            // декодируем для формата «картинка»; если формат не поддерживается —
            // останется только файл-вложение (файл всё равно уйдёт)
            BitmapImage? img = null;
            try
            {
                var b = new BitmapImage();
                b.BeginInit();
                b.CacheOption = BitmapCacheOption.OnLoad;
                b.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                b.EndInit();
                b.Freeze();
                img = b;
            }
            catch { }

            var files = new StringCollection();
            files.Add(path);

            // буфер могут держать другие приложения — с ретраями
            for (int i = 0; ; i++)
            {
                try
                {
                    var d = new DataObject();
                    d.SetFileDropList(files);
                    if (img != null) d.SetImage(img);
                    Clipboard.SetDataObject(d, true);
                    break;
                }
                catch (COMException) when (i < 4) { await Task.Delay(120); }
            }

            await Task.Delay(70);      // дать буферу устояться
            SendCtrlV();

            if (saved != null)
            {
                await Task.Delay(700); // приложение успело забрать вставку
                Restore(saved);
            }
        }
        catch { }
    }

    public static void SendCtrlV()
    {
        var input = new Native.INPUT[4];
        input[0] = Key(Native.VK_CONTROL, down: true);
        input[1] = Key(Native.VK_V, down: true);
        input[2] = Key(Native.VK_V, down: false);
        input[3] = Key(Native.VK_CONTROL, down: false);
        Native.SendInput((uint)input.Length, input, Marshal.SizeOf<Native.INPUT>());
    }

    private static Native.INPUT Key(ushort vk, bool down) => new()
    {
        type = Native.INPUT_KEYBOARD,
        u = new Native.INPUTUNION
        {
            ki = new Native.KEYBDINPUT
            {
                wVk = vk,
                wScan = 0,
                dwFlags = down ? 0u : Native.KEYEVENTF_KEYUP,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        }
    };
}
