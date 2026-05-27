using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SPOTTER.Controls
{
    /// <summary>
    /// A flat, rounded-rectangle action button used for the primary call-to-action
    /// in the SPOTTER UI (e.g. the Start/Stop button on the AIRCRAFT card). Mirrors the
    /// look-and-feel of the other custom-painted SPOTTER controls: rounded corners,
    /// solid fill, no system 3D border, hover/pressed states tinted from the fill.
    ///
    /// Set <see cref="FillColour"/> to control the resting colour — defaults to
    /// <see cref="Theme.AccentPrimary"/>.
    /// </summary>
    public class AccentButton : Control
    {
        private Color _fillColour = Theme.AccentPrimary;
        private bool _hover;
        private bool _pressed;

        public AccentButton()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.Selectable, true);

            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Font = Theme.BodyBold();
            Height = 36;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        /// <summary>The resting fill colour of the button.</summary>
        public Color FillColour
        {
            get => _fillColour;
            set { _fillColour = value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            _pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _pressed = false;
            Invalidate();
        }

        protected override bool ProcessMnemonic(char charCode)
        {
            // Allow the parent form to treat the text as a mnemonic if it contains "&"
            if (CanSelect && IsMnemonic(charCode, Text))
            {
                OnClick(EventArgs.Empty);
                return true;
            }
            return base.ProcessMnemonic(charCode);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                _pressed = true;
                Invalidate();
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                _pressed = false;
                Invalidate();
                OnClick(EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Color fill = _fillColour;
            if (_pressed)      fill = Darken(_fillColour, 0.18);
            else if (_hover)   fill = Darken(_fillColour, 0.08);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRect(rect, Theme.CornerRadius))
            using (var brush = new SolidBrush(fill))
            {
                g.FillPath(brush, path);
            }

            // Focus ring
            if (Focused && TabStop)
            {
                var inner = Rectangle.Inflate(rect, -3, -3);
                using (var path2 = Theme.RoundedRect(inner, Math.Max(1, Theme.CornerRadius - 2)))
                using (var pen = new Pen(Color.FromArgb(80, Color.White), 1))
                {
                    pen.DashStyle = DashStyle.Dot;
                    g.DrawPath(pen, path2);
                }
            }

            TextRenderer.DrawText(g, Text ?? string.Empty, Font,
                new Rectangle(0, 0, Width, Height),
                ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.NoPadding);
        }

        private static Color Darken(Color c, double amount)
        {
            int r = (int)(c.R * (1 - amount));
            int g = (int)(c.G * (1 - amount));
            int b = (int)(c.B * (1 - amount));
            return Color.FromArgb(c.A, Clamp(r), Clamp(g), Clamp(b));
        }

        private static int Clamp(int v) => v < 0 ? 0 : (v > 255 ? 255 : v);
    }
}
