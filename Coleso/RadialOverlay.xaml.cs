using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace Coleso;

/// <summary>
/// Визуал колеса. Окно никогда не активируется (WS_EX_NOACTIVATE), вся логика
/// выбора живёт в WheelController — оверлей только рисует.
/// </summary>
public partial class RadialOverlay : Window
{
    private IntPtr _hwnd;
    private double _radius = 210;
    private int _count;
    private int _highlight = -1;
    private WheelTheme _theme = WheelThemes.Dark;
    private readonly List<SectorView> _sectors = new();

    /// <summary>Один нарисованный сектор: база (цвет/картинка), слой подсветки и подпись.</summary>
    private sealed class SectorView
    {
        public Path Base = null!;
        public Path Hi = null!;
        public TextBlock Label = null!;
        public bool HasImage;
        public string Text = "";
        public string ImageName = "";
    }

    private TextBlock _nameText = null!;
    private TextBlock _badgeText = null!;
    private TextBlock _previewText = null!;

    public RadialOverlay()
    {
        InitializeComponent();
        SourceInitialized += (s, e) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            int ex = Native.GetWindowLong(_hwnd, Native.GWL_EXSTYLE);
            Native.SetWindowLong(_hwnd, Native.GWL_EXSTYLE,
                ex | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW);
        };
    }

    private static Brush Hex(string s)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(s));
        b.Freeze();
        return b;
    }

    // ---------- показ / скрытие ----------

    public void ShowWheel(Wheel wheel, int wheelIndex, int wheelTotal, Native.POINT cursorPx)
    {
        ApplySize();
        Rebuild(wheel, wheelIndex, wheelTotal, App.Config.Settings.ShowHints);
        ApplyPosition(cursorPx);
        Show();
        Native.SetWindowPos(_hwnd, new IntPtr(-1) /* HWND_TOPMOST */, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        AnimateOpen();
    }

    public void HideWheel(bool fade)
    {
        if (!fade)
        {
            Hide();
            ResetVisual();
            return;
        }
        var op = new DoubleAnimation(0, TimeSpan.FromMilliseconds(80));
        op.Completed += (s, e) => { Hide(); ResetVisual(); };
        Root.BeginAnimation(OpacityProperty, op);
    }

    public void SwitchWheel(Wheel wheel, int wheelIndex, int wheelTotal, int dir)
    {
        Rebuild(wheel, wheelIndex, wheelTotal, App.Config.Settings.ShowHints);
        var rot = new DoubleAnimation(dir * 14, 0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        RootRotate.BeginAnimation(RotateTransform.AngleProperty, rot);
    }

    // ---------- отрисовка ----------

    private void ApplySize()
    {
        _radius = App.Config.Settings.WheelSize;
        double side = _radius * 2 + 56;
        if (Width != side) Width = side;
        if (Height != side) Height = side;
        if (WheelCanvas.Width != side) WheelCanvas.Width = side;
        if (WheelCanvas.Height != side) WheelCanvas.Height = side;
    }

    private void ApplyPosition(Native.POINT px)
    {
        double scale = ScreenHelper.ScaleAt(px);
        var (ml, mt) = Native.MonitorOrigin(px);
        // Left/Top в WPF — в DIP текущего монитора
        Left = (px.X - ml) / scale - Width / 2;
        Top = (px.Y - mt) / scale - Height / 2;
    }

    private void Rebuild(Wheel w, int wheelIndex, int wheelTotal, bool showHints)
    {
        _sectors.Clear();
        WheelCanvas.Children.Clear();
        _highlight = -1;
        _count = Math.Max(1, w.Items.Count);
        _theme = WheelThemes.Get(w.Theme);
        var t = _theme;

        double side = WheelCanvas.Width;
        double c = side / 2;
        double R = _radius;
        double rIn = R * 0.34;
        double step = 360.0 / _count;

        // тёмный диск-основа
        var bg = new Ellipse
        {
            Width = R * 2, Height = R * 2,
            Fill = Hex(t.Disc),
            Stroke = Hex(t.DiscStroke), StrokeThickness = 1,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(bg, c - R);
        Canvas.SetTop(bg, c - R);
        WheelCanvas.Children.Add(bg);

        // сектора
        for (int i = 0; i < _count; i++)
        {
            var geo = SectorGeometry(c, R, rIn, i, step);
            var item = i < w.Items.Count ? w.Items[i] : null;
            string imgPath = item?.Image ?? "";
            var img = ImageCache.Get(imgPath);
            bool hasImage = img != null;

            var basePath = new Path
            {
                Data = geo,
                Fill = hasImage ? ImageFill(img!) : Hex(t.SectorFill),
                Stroke = Hex(t.SectorStroke),
                StrokeThickness = 1.2,
                IsHitTestVisible = false
            };
            WheelCanvas.Children.Add(basePath);

            // прозрачный слой подсветки поверх (картинка не пропадает при выборе)
            var hiPath = new Path
            {
                Data = geo,
                Fill = Brushes.Transparent,
                IsHitTestVisible = false
            };
            WheelCanvas.Children.Add(hiPath);

            double th = i * step * Math.PI / 180.0;
            double lx = c + Math.Sin(th) * R * 0.655;
            double ly = c - Math.Cos(th) * R * 0.655;
            var label = new TextBlock
            {
                Text = PreviewOf(item?.Text ?? ""),
                FontSize = 13,
                Foreground = Hex(hasImage ? "#FFFFFFFF" : t.LabelText),
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Width = R * 0.62,
                IsHitTestVisible = false
            };
            if (hasImage)
            {
                // читаемость текста поверх картинки
                label.Effect = new DropShadowEffect
                {
                    Color = Colors.Black, BlurRadius = 6,
                    ShadowDepth = 0, Opacity = 0.95
                };
            }
            Canvas.SetLeft(label, lx - label.Width / 2);
            Canvas.SetTop(label, ly - 9);
            WheelCanvas.Children.Add(label);

            _sectors.Add(new SectorView
            {
                Base = basePath,
                Hi = hiPath,
                Label = label,
                HasImage = hasImage,
                Text = item?.Text ?? "",
                ImageName = hasImage ? System.IO.Path.GetFileName(imgPath) : ""
            });
        }

        // центральный диск с информацией
        var disc = new Ellipse
        {
            Width = rIn * 2 + 16, Height = rIn * 2 + 16,
            Fill = Hex(t.CenterFill),
            Stroke = Hex(t.CenterStroke), StrokeThickness = 1.4,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(disc, c - (rIn + 8));
        Canvas.SetTop(disc, c - (rIn + 8));
        WheelCanvas.Children.Add(disc);

        var cp = new StackPanel { Width = rIn * 1.85, IsHitTestVisible = false };
        _nameText = new TextBlock
        {
            Text = w.Name,
            FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = Hex(t.NameText),
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _badgeText = new TextBlock
        {
            Text = $"{wheelIndex + 1} / {wheelTotal}",
            FontSize = 11, Foreground = Hex(t.BadgeText),
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };
        _previewText = new TextBlock
        {
            FontSize = 11, Foreground = Hex(t.PreviewText),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = rIn, TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 7, 0, 0)
        };
        cp.Children.Add(_nameText);
        cp.Children.Add(_badgeText);
        cp.Children.Add(_previewText);
        Canvas.SetLeft(cp, c - cp.Width / 2);
        Canvas.SetTop(cp, c - rIn * 0.62);
        WheelCanvas.Children.Add(cp);

        // подсказка снизу
        if (showHints)
        {
            var hint = new TextBlock
            {
                Text = "Скролл — выбор   ·   ПКМ — другое колесо   ·   СКМ — закрыть",
                FontSize = 11, Foreground = Hex(t.HintText),
                TextAlignment = TextAlignment.Center,
                Width = side - 60,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(hint, 30);
            Canvas.SetTop(hint, side - 26);
            WheelCanvas.Children.Add(hint);
        }
    }

    /// <summary>
    /// Картинка-фон сектора: растягивается «на покрытие» (UniformToFill) по рамке
    /// сектора, лишнее обрезается — видно только то, что уместилось.
    /// </summary>
    private static Brush ImageFill(BitmapImage img)
    {
        var b = new ImageBrush(img)
        {
            Stretch = Stretch.UniformToFill,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center
        };
        b.Freeze();
        return b;
    }

    public void SetHighlight(int idx)
    {
        if (idx >= _count) return;
        if (idx == _highlight) return;
        var t = _theme;

        if (_highlight >= 0 && _highlight < _sectors.Count)
        {
            var prev = _sectors[_highlight];
            prev.Hi.Fill = Brushes.Transparent;
            prev.Label.Foreground = Hex(prev.HasImage ? "#FFFFFFFF" : t.LabelText);
            prev.Label.FontWeight = FontWeights.Normal;
        }

        _highlight = idx;

        if (idx >= 0 && idx < _sectors.Count)
        {
            var cur = _sectors[idx];
            // поверх картинки — полупрозрачная вуаль, чтобы её было видно
            cur.Hi.Fill = Hex(cur.HasImage ? t.HiVeil : t.HiFill);
            cur.Label.Foreground = Hex(cur.HasImage ? "#FFFFFFFF" : t.HiText);
            cur.Label.FontWeight = FontWeights.SemiBold;

            string text = cur.Text;
            string preview = string.IsNullOrWhiteSpace(text) ? "(пусто)" : text;
            if (cur.HasImage) preview += "\n🖼 " + cur.ImageName;
            _previewText.Text = preview;
        }
        else
        {
            _previewText.Text = "";
        }
    }

    private static Geometry SectorGeometry(double c, double rOut, double rIn, int i, double stepDeg)
    {
        double half = stepDeg / 2.0;
        double a1 = i * stepDeg - half;
        double a2 = i * stepDeg + half;
        bool large = stepDeg > 180;

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(Pt(c, a1, rOut), true, true);
            ctx.ArcTo(Pt(c, a2, rOut), new Size(rOut, rOut), 0, large, SweepDirection.Clockwise, true, false);
            ctx.LineTo(Pt(c, a2, rIn), true, false);
            ctx.ArcTo(Pt(c, a1, rIn), new Size(rIn, rIn), 0, large, SweepDirection.Counterclockwise, true, false);
        }
        geo.Freeze();
        return geo;
    }

    /// <summary>Точка на окружности: угол отсчитывается от верхней точки по часовой.</summary>
    private static Point Pt(double c, double degFromTop, double r)
    {
        double t = degFromTop * Math.PI / 180.0;
        return new Point(c + r * Math.Sin(t), c - r * Math.Cos(t));
    }

    private static string PreviewOf(string t)
    {
        if (string.IsNullOrWhiteSpace(t)) return "(пусто)";
        var line = t.Replace("\r", "").Split('\n')[0].Trim();
        return line.Length > 34 ? line[..34] + "…" : line;
    }

    // ---------- анимации ----------

    private void AnimateOpen()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var sx = new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease };
        var sy = new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease };
        var op = new DoubleAnimation(1, TimeSpan.FromMilliseconds(110));
        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
        Root.BeginAnimation(OpacityProperty, op);
    }

    private void ResetVisual()
    {
        Root.BeginAnimation(OpacityProperty, null);
        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        RootRotate.BeginAnimation(RotateTransform.AngleProperty, null);
        Root.Opacity = 1;
        RootScale.ScaleX = RootScale.ScaleY = 1;
        RootRotate.Angle = 0;
    }
}
