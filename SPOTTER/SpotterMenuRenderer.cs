using System.Drawing;
using System.Windows.Forms;

namespace SPOTTER
{
    /// <summary>
    /// A flat, modern renderer for the menu and status strip. Removes the
    /// raised borders, gradient backgrounds, and 3D edges that come stock
    /// with WinForms.
    ///
    /// To activate, set in ImprovedLogger.cs InitializeControllers (or Load):
    ///
    ///     ToolStripManager.Renderer = new SpotterMenuRenderer();
    ///
    /// </summary>
    public class SpotterMenuRenderer : ToolStripProfessionalRenderer
    {
        public SpotterMenuRenderer() : base(new SpotterColourTable()) { }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            // Suppress the default border — we want a single thin line drawn
            // by the surrounding panel, not the renderer's frame.
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            // Force consistent text colour for menu items
            if (e.Item is ToolStripMenuItem)
            {
                e.TextColor = e.Item.Selected ? Theme.AccentPrimary : Theme.TextPrimary;
            }
            base.OnRenderItemText(e);
        }
    }

    public class SpotterColourTable : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin => Theme.BackgroundPrimary;
        public override Color MenuStripGradientEnd   => Theme.BackgroundPrimary;
        public override Color ToolStripBorder        => Theme.BackgroundPrimary;

        public override Color MenuItemSelected         => Theme.BackgroundSecondary;
        public override Color MenuItemSelectedGradientBegin => Theme.BackgroundSecondary;
        public override Color MenuItemSelectedGradientEnd   => Theme.BackgroundSecondary;
        public override Color MenuItemBorder            => Theme.BorderTertiary;
        public override Color MenuItemPressedGradientBegin => Theme.BackgroundSecondary;
        public override Color MenuItemPressedGradientEnd   => Theme.BackgroundSecondary;

        public override Color ToolStripDropDownBackground => Theme.BackgroundPrimary;
        public override Color ImageMarginGradientBegin    => Theme.BackgroundPrimary;
        public override Color ImageMarginGradientMiddle   => Theme.BackgroundPrimary;
        public override Color ImageMarginGradientEnd      => Theme.BackgroundPrimary;

        public override Color SeparatorDark  => Theme.BorderTertiary;
        public override Color SeparatorLight => Theme.BorderTertiary;

        public override Color StatusStripGradientBegin => Theme.BackgroundTertiary;
        public override Color StatusStripGradientEnd   => Theme.BackgroundTertiary;
    }
}
