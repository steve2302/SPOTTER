// SPOTTER/Controls/PillBadge.cs
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SPOTTER.Controls
{
    public class PillBadge : Control
    {
        private DistanceBin.Bin _bin = DistanceBin.Bin.Unknown;
        private Color _fillColour = Theme.BackgroundTertiary;
        private int _hPad = 12;
        private int _vPad = 5;

        public PillBadge()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.HeroPill();
            ForeColor = Theme.TextSecondary;
            AutoSize = true;
        }

        /// <summary>
        /// Sets fill + foreground from the centralised distance-bin palette.
        /// </summary>
        internal DistanceBin.Bin Bin
        {
            get => _bin;
            set
            {
                _bin = value;
                var (fill, text) = DistanceBin.Colours(value);
                _fillColour = fill;
                ForeColor = text;
                Invalidate();
                if (AutoSize) AdjustSize();
            }
        }

        public Color FillColour
        {
            get => _fillColour;
            set { _fillColour = value; Invalidate(); }
        }

        public int HorizontalPadding
        {
            get => _hPad;
            set { _hPad = value; if (AutoSize) AdjustSize(); Invalidate(); }
        }

        public int VerticalPadding
        {
            get => _vPad;
            set { _vPad = value; if (AutoSize) AdjustSize(); Invalidate(); }
        }

        protected override void OnTextChanged(System.EventArgs e)
        {
            base.OnTextChanged(e);
            if (AutoSize) AdjustSize();
            Invalidate();
        }

        protected override void OnFontChanged(System.EventArgs e)
        {
            base.OnFontChanged(e);
            if (AutoSize) AdjustSize();
            Invalidate();
        }

        private void AdjustSize()
        {
            using (var g = CreateGraphics())
            {
                var sz = TextRenderer.MeasureText(g, Text ?? string.Empty, Font);
                Size = new Size(sz.Width + _hPad * 2, sz.Height + _vPad * 2);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Pill = rectangle with corner radius = height / 2
            int radius = Height / 2;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRect(rect, radius))
            using (var brush = new SolidBrush(_fillColour))
            {
                g.FillPath(brush, path);
            }

            TextRenderer.DrawText(g, Text ?? string.Empty, Font,
                new Rectangle(0, 0, Width, Height),
                ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.NoPadding);
        }
    }
}