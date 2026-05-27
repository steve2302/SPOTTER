using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using GMap.NET;
using GMap.NET.WindowsForms;

namespace SPOTTER
{
    /// <summary>
    /// Custom aircraft marker that rotates with heading
    /// </summary>
    public class GMarkerAircraft : GMapMarker
    {
        private float _heading;
        private Color _color;
        //private static Bitmap _aircraftIcon;

        public GMarkerAircraft(PointLatLng p, float heading = 0, Color? color = null)
            : base(p)
        {
            _heading = heading;
            _color = color ?? Color.Red;
            Size = new Size(32, 32);
            Offset = new Point(-16, -16);
        }

        public void UpdateHeading(float heading)
        {
            _heading = heading;
        }

        public override void OnRender(Graphics g)
        {
            // Save the current graphics state
            GraphicsState state = g.Save();

            // Move to the marker position
            g.TranslateTransform(LocalPosition.X, LocalPosition.Y);

            // Rotate based on heading
            g.RotateTransform(_heading);

            // Draw the aircraft shape
            DrawAircraft(g);

            // Restore the graphics state
            g.Restore(state);
        }

        private void DrawAircraft(Graphics g)
        {
            // Enable anti-aliasing for smooth drawing
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Aircraft body (fuselage)
            using (SolidBrush brush = new SolidBrush(_color))
            using (Pen pen = new Pen(Color.Black, 1.5f))
            {
                // Body - elongated triangle pointing up
                PointF[] body = new PointF[]
                {
                    new PointF(0, -12),      // Nose
                    new PointF(-3, 8),       // Left tail
                    new PointF(0, 6),        // Center tail
                    new PointF(3, 8),        // Right tail
                };
                g.FillPolygon(brush, body);
                g.DrawPolygon(pen, body);

                // Wings - horizontal
                PointF[] leftWing = new PointF[]
                {
                    new PointF(-3, 0),
                    new PointF(-12, 2),
                    new PointF(-12, 4),
                    new PointF(-3, 2)
                };
                g.FillPolygon(brush, leftWing);
                g.DrawPolygon(pen, leftWing);

                PointF[] rightWing = new PointF[]
                {
                    new PointF(3, 0),
                    new PointF(12, 2),
                    new PointF(12, 4),
                    new PointF(3, 2)
                };
                g.FillPolygon(brush, rightWing);
                g.DrawPolygon(pen, rightWing);

                // Tail wings
                PointF[] leftTail = new PointF[]
                {
                    new PointF(-2, 6),
                    new PointF(-6, 7),
                    new PointF(-5, 8)
                };
                g.FillPolygon(brush, leftTail);
                g.DrawPolygon(pen, leftTail);

                PointF[] rightTail = new PointF[]
                {
                    new PointF(2, 6),
                    new PointF(6, 7),
                    new PointF(5, 8)
                };
                g.FillPolygon(brush, rightTail);
                g.DrawPolygon(pen, rightTail);

                // Cockpit highlight
                using (SolidBrush whiteBrush = new SolidBrush(Color.FromArgb(150, Color.White)))
                {
                    g.FillEllipse(whiteBrush, -2, -8, 4, 5);
                }
            }

            // Draw a subtle shadow
            using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(50, Color.Black)))
            {
                g.FillEllipse(shadowBrush, -14, -14, 28, 28);
            }
        }
    }

    /// <summary>
    /// Alternative simple arrow marker
    /// </summary>
    public class GMarkerArrow : GMapMarker
    {
        private float _heading;
        private Color _color;

        public GMarkerArrow(PointLatLng p, float heading = 0, Color? color = null)
            : base(p)
        {
            _heading = heading;
            _color = color ?? Color.Blue;
            Size = new Size(24, 24);
            Offset = new Point(-12, -12);
        }

        public void UpdateHeading(float heading)
        {
            _heading = heading;
        }

        public override void OnRender(Graphics g)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(LocalPosition.X, LocalPosition.Y);
            g.RotateTransform(_heading);

            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw arrow
            using (SolidBrush brush = new SolidBrush(_color))
            using (Pen pen = new Pen(Color.White, 2))
            {
                PointF[] arrow = new PointF[]
                {
                    new PointF(0, -10),      // Point
                    new PointF(-6, 8),       // Left
                    new PointF(0, 4),        // Notch
                    new PointF(6, 8),        // Right
                };
                g.FillPolygon(brush, arrow);
                g.DrawPolygon(pen, arrow);
            }

            g.Restore(state);
        }
    }

    /// <summary>
    /// Simple dot with heading indicator
    /// </summary>
    public class GMarkerDotWithHeading : GMapMarker
    {
        private float _heading;
        private Color _color;

        public GMarkerDotWithHeading(PointLatLng p, float heading = 0, Color? color = null)
            : base(p)
        {
            _heading = heading;
            _color = color ?? Color.Red;
            Size = new Size(20, 20);
            Offset = new Point(-10, -10);
        }

        public void UpdateHeading(float heading)
        {
            _heading = heading;
        }

        public override void OnRender(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw outer circle (border)
            using (SolidBrush borderBrush = new SolidBrush(Color.White))
            {
                g.FillEllipse(borderBrush, LocalPosition.X - 10, LocalPosition.Y - 10, 20, 20);
            }

            // Draw inner circle
            using (SolidBrush brush = new SolidBrush(_color))
            {
                g.FillEllipse(brush, LocalPosition.X - 8, LocalPosition.Y - 8, 16, 16);
            }

            // Draw heading indicator line
            GraphicsState state = g.Save();
            g.TranslateTransform(LocalPosition.X, LocalPosition.Y);
            g.RotateTransform(_heading);

            using (Pen pen = new Pen(Color.White, 2))
            {
                g.DrawLine(pen, 0, 0, 0, -12);
            }

            // Draw arrowhead
            using (SolidBrush whiteBrush = new SolidBrush(Color.White))
            {
                PointF[] arrowHead = new PointF[]
                {
                    new PointF(0, -12),
                    new PointF(-3, -8),
                    new PointF(3, -8)
                };
                g.FillPolygon(whiteBrush, arrowHead);
            }

            g.Restore(state);
        }
    }
}
