using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SPOTTER.Controls
{
    /// <summary>
    /// Large rounded tile used as the headline element in the "Last observation"
    /// hero panel. Shows a count number prominently with a small "COUNT" caption.
    /// Set <see cref="Value"/> to update the display; pass null or empty to clear.
    /// </summary>
    public class CountTile : Control
    {
        private string _value = "—";
        private string _caption = "COUNT";

        public CountTile()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(Theme.CountTileSize, Theme.CountTileSize);
        }

        public string Value
        {
            get => _value;
            set
            {
                _value = string.IsNullOrEmpty(value) ? "—" : value;
                Invalidate();
            }
        }

        public string Caption
        {
            get => _caption;
            set { _caption = value ?? string.Empty; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRect(rect, Theme.CornerRadiusLarge))
            using (var fill = new SolidBrush(Theme.AccentBackground))
            using (var border = new Pen(Theme.BorderTertiary, 1))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            // Big number — vertically nudged up so caption fits underneath
            using (var bigFont = Theme.HeroCount())
            {
                var numRect = new Rectangle(0, 6, Width, Height - 22);
                TextRenderer.DrawText(g, _value, bigFont, numRect, Theme.AccentPrimary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                        | TextFormatFlags.NoPadding);
            }

            // Caption underneath
            using (var capFont = Theme.Caption())
            {
                var capRect = new Rectangle(0, Height - 18, Width, 14);
                TextRenderer.DrawText(g, _caption, capFont, capRect, Theme.AccentPrimary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                        | TextFormatFlags.NoPadding);
            }
        }
    }
}
