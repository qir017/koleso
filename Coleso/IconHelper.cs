using System.Drawing;
using System.Drawing.Drawing2D;

namespace Coleso;

public static class IconHelper
{
    /// <summary>Рисует значок-колесо программно (иконка трея).</summary>
    public static Icon CreateAppIcon(int size = 32)
    {
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = size / 2f, cy = size / 2f;

            using var body = new SolidBrush(Color.FromArgb(244, 17, 20, 26));
            g.FillEllipse(body, size * 0.06f, size * 0.06f, size * 0.88f, size * 0.88f);

            using var ring = new Pen(Color.FromArgb(230, 232, 163, 61), size * 0.09f);
            g.DrawEllipse(ring, size * 0.13f, size * 0.13f, size * 0.74f, size * 0.74f);

            using var spoke = new Pen(Color.FromArgb(210, 235, 235, 235), size * 0.05f);
            spoke.StartCap = LineCap.Round;
            spoke.EndCap = LineCap.Round;
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4.0;
                float x1 = cx + (float)Math.Sin(a) * size * 0.17f;
                float y1 = cy - (float)Math.Cos(a) * size * 0.17f;
                float x2 = cx + (float)Math.Sin(a) * size * 0.31f;
                float y2 = cy - (float)Math.Cos(a) * size * 0.31f;
                g.DrawLine(spoke, x1, y1, x2, y2);
            }

            using var hub = new SolidBrush(Color.FromArgb(255, 232, 163, 61));
            float hr = size * 0.07f;
            g.FillEllipse(hub, cx - hr, cy - hr, hr * 2, hr * 2);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
