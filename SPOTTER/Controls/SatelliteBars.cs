using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SPOTTER.Controls
{
    /// <summary>
    /// Vertical bar chart showing GPS satellite signal strength.
    ///
    /// When per-satellite SNR data is wired up later (NMEAParser would need to
    /// expose $GPGSV SNR values), call <see cref="SetSatellites"/> with the SNR
    /// array and the count used in fix.
    ///
    /// Until then, call <see cref="SetCounts"/> with (used, inView) and the
    /// control will render a synthetic, illustrative pattern: bars representing
    /// "used" satellites get the good colour; bars representing "in view but
    /// not used" get the muted colour. This is a placeholder visualisation, not
    /// real signal data — the panel caption "(awaiting SNR feed)" makes that
    /// explicit to the user.
    /// </summary>
    public class SatelliteBars : Control
    {
        private int[] _snr = new int[0];   // 0..50 dB-Hz range typical
        private int   _used  = 0;
        private int   _inView = 0;
        private bool  _hasRealSnr = false;

        public SatelliteBars()
        {
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 32;
        }

        /// <summary>Real SNR values per satellite (dB-Hz, typically 0–50).</summary>
        public void SetSatellites(int[] snrPerSat, int satellitesUsedInFix)
        {
            _snr = snrPerSat ?? new int[0];
            _used = satellitesUsedInFix;
            _inView = _snr.Length;
            _hasRealSnr = true;
            Invalidate();
        }

        /// <summary>Fallback when only used/inView counts are available.</summary>
        public void SetCounts(int used, int inView)
        {
            _used = used;
            _inView = inView;
            _hasRealSnr = false;
            Invalidate();
        }

        public bool HasRealSnr => _hasRealSnr;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int bars = _hasRealSnr ? _snr.Length : _inView;
            if (bars <= 0)
            {
                TextRenderer.DrawText(g, "No satellites in view", Theme.Caption(),
                    new Rectangle(0, 0, Width, Height), Theme.TextTertiary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            // Layout
            int gap = 2;
            int totalGap = gap * (bars - 1);
            int barW = (Width - totalGap) / bars;
            if (barW < 2) barW = 2;
            int chartH = Height;

            for (int i = 0; i < bars; i++)
            {
                int x = i * (barW + gap);
                int snr;
                bool used;

                if (_hasRealSnr)
                {
                    snr = _snr[i];
                    used = i < _used; // assume the first _used entries are the in-fix ones
                }
                else
                {
                    // Synthetic pattern — first _used bars are "used" with deterministic
                    // pseudo-heights so the panel feels alive without lying about SNR.
                    used = i < _used;
                    snr = used ? 35 + ((i * 7) % 15) : 15 + ((i * 5) % 10);
                }

                int h = (int)((snr / 50.0) * chartH);
                if (h < 3) h = 3;
                if (h > chartH) h = chartH;

                var barRect = new Rectangle(x, chartH - h, barW, h);
                using (var brush = new SolidBrush(used ? Theme.GoodDot : Theme.SatelliteUnused))
                {
                    g.FillRectangle(brush, barRect);
                }
            }
        }
    }
}
