using SharpDX.XInput;
using SPOTTER.Controllers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace SPOTTER
{
    /// <summary>
    /// Displays an Xbox controller diagram. Press any physical gamepad button to
    /// highlight it on the diagram and show its GamepadAudioConfig.csv settings,
    /// so you can verify that CSV edits produce the expected behaviour.
    /// </summary>
    public class GamepadConfigViewer : Form
    {
        // ── fields ────────────────────────────────────────────────────────────────
        private Controller         _controller;
        private State              _currentState;
        private State              _previousState;
        private GamepadAudioConfig _config;
        private Timer              _pollTimer;
        private string             _activeKey;      // CSV key of the last detected press

        // ── UI controls ───────────────────────────────────────────────────────────
        private Panel _drawPanel;
        private Label _lblPrompt, _lblButton, _lblAudio, _lblData, _lblNewRec;

        // ── button descriptor ─────────────────────────────────────────────────────
        private class BDef
        {
            public string Key, Glyph;
            public float  Cx, Cy, Rw, Rh;
            public bool   IsRect;
            public Color  IdleColor;
        }

        private List<BDef> _btns;

        // ── colour palette ────────────────────────────────────────────────────────
        private static readonly Color ColA      = Color.FromArgb( 34, 177,  76);
        private static readonly Color ColB      = Color.FromArgb(220,  50,  50);
        private static readonly Color ColX      = Color.FromArgb( 50, 120, 210);
        private static readonly Color ColY      = Color.FromArgb(225, 180,  20);
        private static readonly Color ColGray   = Color.FromArgb(110, 108, 105);
        private static readonly Color ColDir    = Color.FromArgb( 70, 130, 180);
        private static readonly Color ColDark   = Color.FromArgb( 65,  63,  60);
        private static readonly Color ColActive = Color.FromArgb(255, 220,  40);
        private static readonly Color ColBody   = Color.FromArgb( 78,  76,  72);
        private static readonly Color ColPanel  = Color.FromArgb( 40,  40,  44);

        // ── constructor ───────────────────────────────────────────────────────────
        public GamepadConfigViewer()
        {
            BuildBtnList();
            BuildUI();
            LoadConfig();
            InitController();

            _pollTimer = new Timer { Interval = 50 };
            _pollTimer.Tick += OnPoll;
            _pollTimer.Start();
        }

        // ── button layout  (panel is 530 × 265) ──────────────────────────────────
        private void BuildBtnList()
        {
            _btns = new List<BDef>
            {
                // Triggers (top)
                Rct("LeftTrigger",   "LT",  106, 30, 40, 14, ColDark),
                Rct("RightTrigger",  "RT",  424, 30, 40, 14, ColDark),
                // Bumpers
                Rct("LeftShoulder",  "LB",  114, 63, 38, 11, ColGray),
                Rct("RightShoulder", "RB",  416, 63, 38, 11, ColGray),

                // Left stick: large circle = click, small circles = directions
                Cir("LeftThumbClick",  "L3",  150, 128, 27, ColGray),
                Cir("LeftThumbUp",     "▲",   150,  90,  9, ColDir),
                Cir("LeftThumbDown",   "▼",   150, 166,  9, ColDir),
                Cir("LeftThumbLeft",   "◄",   112, 128,  9, ColDir),
                Cir("LeftThumbRight",  "►",   188, 128,  9, ColDir),

                // D-pad
                Rct("DPadUp",    "▲", 100, 178, 11, 11, ColGray),
                Rct("DPadDown",  "▼", 100, 222, 11, 11, ColGray),
                Rct("DPadLeft",  "◄",  78, 200, 11, 11, ColGray),
                Rct("DPadRight", "►", 122, 200, 11, 11, ColGray),

                // Centre buttons
                Cir("Back",  "☰", 215, 128, 14, ColGray),
                Cir("Start", "≡", 315, 128, 14, ColGray),

                // Right stick: large circle = click, small circles = directions
                Cir("RightThumbClick", "R3", 368, 198, 27, ColGray),
                Cir("RightThumbUp",    "▲",  368, 160,  9, ColDir),
                Cir("RightThumbDown",  "▼",  368, 236,  9, ColDir),
                Cir("RightThumbLeft",  "◄",  330, 198,  9, ColDir),
                Cir("RightThumbRight", "►",  406, 198,  9, ColDir),

                // Face buttons
                Cir("Y", "Y", 418, 120, 16, ColY),
                Cir("X", "X", 388, 150, 16, ColX),
                Cir("B", "B", 448, 150, 16, ColB),
                Cir("A", "A", 418, 180, 16, ColA),
            };
        }

        private static BDef Cir(string k, string g, float cx, float cy, float r, Color c) =>
            new BDef { Key = k, Glyph = g, Cx = cx, Cy = cy, Rw = r, Rh = r, IsRect = false, IdleColor = c };

        private static BDef Rct(string k, string g, float cx, float cy, float rw, float rh, Color c) =>
            new BDef { Key = k, Glyph = g, Cx = cx, Cy = cy, Rw = rw, Rh = rh, IsRect = true, IdleColor = c };

        // ── UI construction ───────────────────────────────────────────────────────
        private void BuildUI()
        {
            Text            = "Gamepad Configuration Viewer";
            ClientSize      = new Size(550, 430);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;
            BackColor       = Theme.BackgroundPrimary;

            _drawPanel = new Panel
            {
                Location  = new Point(10, 10),
                Size      = new Size(530, 265),
                BackColor = ColPanel
            };
            _drawPanel.Paint += OnDraw;
            Controls.Add(_drawPanel);

            int y = 286;
            _lblPrompt = MkLabel("Press any gamepad button to see its configuration.", y, true); y += 26;
            _lblButton = MkLabel("Button:         —", y); y += 22;
            _lblAudio  = MkLabel("Audio Text:     —", y); y += 22;
            _lblData   = MkLabel("Recorded Data:  —", y); y += 22;
            _lblNewRec = MkLabel("New Record:     —", y);
        }

        private Label MkLabel(string text, int y, bool bold = false)
        {
            var lbl = new Label
            {
                Text      = text,
                Location  = new Point(10, y),
                Size      = new Size(530, 20),
                ForeColor = Theme.TextPrimary,
                BackColor = Color.Transparent,
                Font      = bold ? Theme.BodyBold() : Theme.Body(),
                AutoSize  = false,
            };
            Controls.Add(lbl);
            return lbl;
        }

        // ── config ────────────────────────────────────────────────────────────────
        private void LoadConfig()
        {
            _config = new GamepadAudioConfig();
            _config.LoadFromFile(System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "GamepadAudioConfig.csv"));
        }

        private void InitController()
        {
            try
            {
                _controller = new Controller(UserIndex.One);
                if (_controller.IsConnected)
                {
                    _currentState  = _controller.GetState();
                    _previousState = _currentState;
                }
            }
            catch { }
        }

        // ── poll timer ────────────────────────────────────────────────────────────
        private void OnPoll(object sender, EventArgs e)
        {
            if (_controller == null || !_controller.IsConnected) return;
            _previousState = _currentState;
            try { _currentState = _controller.GetState(); }
            catch { return; }

            string key = DetectJustPressed();
            if (key == null) return;

            _activeKey = key;
            ShowConfig(key);
            _drawPanel.Invalidate();
        }

        private string DetectJustPressed()
        {
            GamepadButtonFlags cur  = _currentState.Gamepad.Buttons;
            GamepadButtonFlags prev = _previousState.Gamepad.Buttons;

            if (JustPressed(cur, prev, GamepadButtonFlags.A))             return "A";
            if (JustPressed(cur, prev, GamepadButtonFlags.B))             return "B";
            if (JustPressed(cur, prev, GamepadButtonFlags.X))             return "X";
            if (JustPressed(cur, prev, GamepadButtonFlags.Y))             return "Y";
            if (JustPressed(cur, prev, GamepadButtonFlags.LeftShoulder))  return "LeftShoulder";
            if (JustPressed(cur, prev, GamepadButtonFlags.RightShoulder)) return "RightShoulder";
            if (JustPressed(cur, prev, GamepadButtonFlags.Start))         return "Start";
            if (JustPressed(cur, prev, GamepadButtonFlags.Back))          return "Back";
            if (JustPressed(cur, prev, GamepadButtonFlags.LeftThumb))     return "LeftThumbClick";
            if (JustPressed(cur, prev, GamepadButtonFlags.RightThumb))    return "RightThumbClick";
            if (JustPressed(cur, prev, GamepadButtonFlags.DPadUp))        return "DPadUp";
            if (JustPressed(cur, prev, GamepadButtonFlags.DPadDown))      return "DPadDown";
            if (JustPressed(cur, prev, GamepadButtonFlags.DPadLeft))      return "DPadLeft";
            if (JustPressed(cur, prev, GamepadButtonFlags.DPadRight))     return "DPadRight";

            // Analog triggers: fire when crossing the midpoint
            if (_currentState.Gamepad.LeftTrigger  >= 128 &&
                _previousState.Gamepad.LeftTrigger   < 128) return "LeftTrigger";
            if (_currentState.Gamepad.RightTrigger >= 128 &&
                _previousState.Gamepad.RightTrigger  < 128) return "RightTrigger";

            // Thumbstick directions (suppress while click held to avoid ghost inputs)
            bool lHeld = (cur & GamepadButtonFlags.LeftThumb)  != 0;
            bool rHeld = (cur & GamepadButtonFlags.RightThumb) != 0;

            if (!lHeld)
            {
                string d = StickDir(_currentState.Gamepad.LeftThumbX,   _currentState.Gamepad.LeftThumbY,
                                    _previousState.Gamepad.LeftThumbX,  _previousState.Gamepad.LeftThumbY,
                                    "LeftThumb");
                if (d != null) return d;
            }
            if (!rHeld)
            {
                string d = StickDir(_currentState.Gamepad.RightThumbX,  _currentState.Gamepad.RightThumbY,
                                    _previousState.Gamepad.RightThumbX, _previousState.Gamepad.RightThumbY,
                                    "RightThumb");
                if (d != null) return d;
            }

            return null;
        }

        private static bool JustPressed(GamepadButtonFlags cur, GamepadButtonFlags prev, GamepadButtonFlags flag) =>
            (cur & flag) != 0 && (prev & flag) == 0;

        private static string StickDir(short cx, short cy, short px, short py, string pfx)
        {
            const float T = 0.5f;
            float fcx = cx / 32767f, fcy = cy / 32767f;
            float fpx = px / 32767f, fpy = py / 32767f;
            float ax = Math.Abs(fcx), ay = Math.Abs(fcy);
            float pax = Math.Abs(fpx), pay = Math.Abs(fpy);

            // Fires only when crossing the threshold from below (avoids repeat triggering)
            if (ax > ay && ax > T && pax <= T) return fcx > 0 ? pfx + "Right" : pfx + "Left";
            if (ay >= ax && ay > T && pay <= T) return fcy > 0 ? pfx + "Up"    : pfx + "Down";
            return null;
        }

        // ── config display ────────────────────────────────────────────────────────
        private void ShowConfig(string key)
        {
            string displayKey   = key;
            string audioText    = _config.GetAudioText(key)    ?? "(not configured)";
            string recordedData = _config.GetRecordedData(key) ?? "(not configured)";
            bool   newRecord    = _config.GetNewRecord(key);

            // Start toggles between Start_On and Start_Off so show both
            if (key == "Start")
            {
                displayKey  = "Start  [toggles: Start_On / Start_Off]";
                string onA  = _config.GetAudioText("Start_On")     ?? "(not configured)";
                string offA = _config.GetAudioText("Start_Off")    ?? "(not configured)";
                string onD  = _config.GetRecordedData("Start_On")  ?? "(not configured)";
                string offD = _config.GetRecordedData("Start_Off") ?? "(not configured)";
                audioText    = $"On → {onA}  /  Off → {offA}";
                recordedData = $"On → {onD}  /  Off → {offD}";
                newRecord    = _config.GetNewRecord("Start_On");
            }

            _lblPrompt.Text = $"Last pressed:  {key}";
            _lblButton.Text = $"Button:         {displayKey}";
            _lblAudio.Text  = $"Audio Text:     {audioText}";
            _lblData.Text   = $"Recorded Data:  {recordedData}";
            _lblNewRec.Text = $"New Record:     {(newRecord ? "Yes  (starts a new line)" : "No  (appends to current line)")}";
        }

        // ── drawing ───────────────────────────────────────────────────────────────
        private void OnDraw(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            if (_controller == null || !_controller.IsConnected)
            {
                DrawNoController(g);
                return;
            }

            DrawBody(g);
            DrawAllBtns(g);
        }

        private void DrawNoController(Graphics g)
        {
            using (Font f = new Font("Segoe UI", 11f))
            using (SolidBrush br = new SolidBrush(Color.FromArgb(140, 138, 134)))
            {
                StringFormat sf = new StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString("No controller detected", f, br,
                    new RectangleF(0, 0, _drawPanel.Width, _drawPanel.Height), sf);
                sf.Dispose();
            }
        }

        private void DrawBody(Graphics g)
        {
            using (SolidBrush br = new SolidBrush(ColBody))
            {
                // Shoulder mount blocks (sit behind the trigger/bumper shapes)
                g.FillRectangle(br,  66, 15, 100, 63);
                g.FillRectangle(br, 364, 15, 100, 63);
                // Left and right wing lobes
                g.FillEllipse(br,  40,  78, 192, 182);
                g.FillEllipse(br, 298,  78, 192, 182);
                // Centre bar connecting both wings
                g.FillRectangle(br, 130, 88, 270, 140);
                // Lower grips
                g.FillEllipse(br,  32, 160, 152, 104);
                g.FillEllipse(br, 346, 160, 152, 104);
            }
        }

        private void DrawAllBtns(Graphics g)
        {
            StringFormat sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            foreach (BDef btn in _btns)
                DrawBtn(g, btn, btn.Key == _activeKey, sf);

            sf.Dispose();
        }

        private static void DrawBtn(Graphics g, BDef btn, bool active, StringFormat sf)
        {
            Color fill    = active ? ColActive : btn.IdleColor;
            Color outline = active ? Color.White : Color.FromArgb(50, 48, 45);
            Color textCol = active ? Color.Black : Color.FromArgb(230, 228, 224);

            RectangleF rect = new RectangleF(btn.Cx - btn.Rw, btn.Cy - btn.Rh,
                                             btn.Rw * 2,       btn.Rh * 2);

            using (SolidBrush br  = new SolidBrush(fill))
            using (Pen        pen = new Pen(outline, active ? 2f : 1f))
            {
                if (btn.IsRect)
                {
                    using (GraphicsPath path = RoundRect(rect, 4f))
                    {
                        g.FillPath(br, path);
                        g.DrawPath(pen, path);
                    }
                }
                else
                {
                    g.FillEllipse(br, rect);
                    g.DrawEllipse(pen, rect);
                }
            }

            float     fontSize = btn.Rw >= 14 ? 6.5f : 6f;
            FontStyle fstyle   = btn.Rw >= 14 ? FontStyle.Bold : FontStyle.Regular;
            using (Font       font = new Font("Segoe UI", fontSize, fstyle))
            using (SolidBrush tbr  = new SolidBrush(textCol))
            {
                g.DrawString(btn.Glyph, font, tbr, rect, sf);
            }

            // Yellow glow ring around the active button
            if (active)
            {
                const float pad = 4f;
                RectangleF gr = new RectangleF(btn.Cx - btn.Rw - pad, btn.Cy - btn.Rh - pad,
                                               (btn.Rw + pad) * 2,    (btn.Rh + pad) * 2);
                using (Pen gp = new Pen(Color.FromArgb(160, 255, 220, 30), 3f))
                {
                    if (btn.IsRect) g.DrawRectangle(gp, gr.X, gr.Y, gr.Width, gr.Height);
                    else            g.DrawEllipse(gp, gr);
                }
            }
        }

        private static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            float d = radius * 2f;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.Left,      r.Top,        d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top,        d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);
            p.AddArc(r.Left,      r.Bottom - d, d, d,  90, 90);
            p.CloseFigure();
            return p;
        }

        // ── cleanup ───────────────────────────────────────────────────────────────
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _pollTimer?.Stop();
            _pollTimer?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
