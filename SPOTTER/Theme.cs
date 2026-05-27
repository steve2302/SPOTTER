using System.Drawing;
using System.Drawing.Drawing2D;

namespace SPOTTER
{
    /// <summary>
    /// Central palette, typography, and sizing for the SPOTTER UI.
    /// Keep this the single source of truth — every custom control
    /// reads from here so a colour tweak only needs one edit.
    /// </summary>
    internal static class Theme
    {
        // ---- Colours (NSW-friendly, accessible contrast) ----
        public static readonly Color BackgroundPrimary   = Color.FromArgb(255, 255, 255);
        public static readonly Color BackgroundSecondary = Color.FromArgb(238, 236, 231);
        public static readonly Color BackgroundTertiary  = Color.FromArgb(210, 208, 202); // rail — clearly distinct from white cards

        public static readonly Color BorderTertiary  = Color.FromArgb(185, 183, 175); // visible card outline
        public static readonly Color BorderSecondary = Color.FromArgb(148, 145, 136); // stronger border

        public static readonly Color TextPrimary    = Color.FromArgb(28, 27, 26);
        public static readonly Color TextSecondary  = Color.FromArgb(80, 78, 73);    // darker for legibility
        public static readonly Color TextTertiary   = Color.FromArgb(120, 118, 110);

        // Segment (toggle) buttons — white text on these backgrounds passes WCAG AA
        public static readonly Color SegmentBackground = Color.FromArgb(108, 106, 100);
        public static readonly Color SegmentHover      = Color.FromArgb(82, 80, 76);

        // Accent (NSW Navy)
        public static readonly Color AccentPrimary    = Color.FromArgb(24, 95, 165);
        public static readonly Color AccentDeep       = Color.FromArgb(12, 68, 124);
        public static readonly Color AccentBackground = Color.FromArgb(230, 241, 251);

        // Recording / good
        public static readonly Color GoodPrimary    = Color.FromArgb(15, 110, 86);
        public static readonly Color GoodDot        = Color.FromArgb(29, 158, 117);
        public static readonly Color GoodBackground = Color.FromArgb(225, 245, 238);

        // Warning / amber
        public static readonly Color WarnPrimary    = Color.FromArgb(99, 56, 6);
        public static readonly Color WarnBackground = Color.FromArgb(250, 238, 218);

        // Alert / red
        public static readonly Color AlertPrimary    = Color.FromArgb(114, 36, 62);
        public static readonly Color AlertBackground = Color.FromArgb(251, 234, 240);

        public static readonly Color SatelliteUnused = Color.FromArgb(180, 178, 169);

        // ---- Type ----
        // Public Sans is the NSW Government typeface; falls back gracefully.
        public const string SansFamily  = "Public Sans, Segoe UI, Tahoma";
        public const string MonoFamily  = "Cascadia Mono, Consolas, Courier New";

        public static Font Title()      => new Font("Segoe UI", 12F, FontStyle.Regular);
        public static Font Subtitle()   => new Font("Segoe UI", 8F,  FontStyle.Regular);
        public static Font Heading()    => new Font("Segoe UI", 9F,  FontStyle.Regular);
        public static Font Body()       => new Font("Segoe UI", 9F,  FontStyle.Regular);
        public static Font BodyBold()   => new Font("Segoe UI", 9F,  FontStyle.Bold);
        public static Font Caption()    => new Font("Segoe UI", 8F,  FontStyle.Regular);
        public static Font Mono()       => new Font("Consolas", 9F,  FontStyle.Regular);
        public static Font MonoLarge()  => new Font("Consolas", 11F, FontStyle.Regular);
        public static Font HeroSpecies()=> new Font("Segoe UI", 18F, FontStyle.Regular);
        public static Font HeroCount()  => new Font("Segoe UI", 26F, FontStyle.Regular);
        public static Font HeroPill()   => new Font("Segoe UI", 11F, FontStyle.Bold);

        // ---- Sizing ----
        public const int CornerRadius      = 6;
        public const int CornerRadiusLarge = 10;
        public const int PanelPadding      = 12;
        public const int RailWidth         = 300;
        public const int CountTileSize     = 88;

        // ---- Helpers ----
        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>
    /// Distance bin colour scheme used by both the hero pill and the session log.
    /// Centralised so the two stay in sync.
    /// </summary>
    internal static class DistanceBin
    {
        public enum Bin { Unknown, Near, Medium, Far, VeryFar }

        public static Bin Classify(string distanceText)
        {
            if (string.IsNullOrEmpty(distanceText)) return Bin.Unknown;
            string t = distanceText.ToLowerInvariant();

            // Long word form (legacy audio-driven tokens)
            if (t.Contains("zero to fifty")           || t.Contains("0-50"))   return Bin.Near;
            if (t.Contains("fifty to one hundred")    || t.Contains("50-100"))  return Bin.Medium;
            if (t.Contains("one hundred to two hundred") || t.Contains("100-200")) return Bin.Far;
            if (t.Contains("two hundred to three hundred") || t.Contains("200-300")) return Bin.VeryFar;

            // Colour keyword (compact CSV format: "(yellow) 0-50", "(blue) 100-200", etc.)
            if (t.Contains("yellow")) return Bin.Near;
            if (t.Contains("green"))  return Bin.Medium;
            if (t.Contains("blue"))   return Bin.Far;
            if (t.Contains("black"))  return Bin.VeryFar;

            return Bin.Unknown;
        }

        public static (Color fill, Color text) Colours(Bin bin)
        {
            switch (bin)
            {
                case Bin.Near:    return (Theme.GoodBackground,    Theme.GoodPrimary);
                case Bin.Medium:  return (Theme.AccentBackground,  Theme.AccentDeep);
                case Bin.Far:     return (Theme.WarnBackground,    Theme.WarnPrimary);
                case Bin.VeryFar: return (Theme.AlertBackground,   Theme.AlertPrimary);
                default:          return (Theme.BackgroundTertiary, Theme.TextSecondary);
            }
        }
    }
}
