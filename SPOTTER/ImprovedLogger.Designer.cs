using System;
using System.Drawing;
using System.Windows.Forms;
using SPOTTER.Controls;

namespace SPOTTER
{
    partial class ImprovedLogger
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        // ---- Original controls preserved (named for compatibility with ImprovedLogger.cs) ----
        private MenuStrip menuStrip;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem openObserverListToolStripMenuItem;
        private ToolStripMenuItem editToolStripMenuItem;
        private ToolStripMenuItem editControllerConfigToolStripMenuItem;
        private ToolStripMenuItem quitToolStripMenuItem;
        private ToolStripMenuItem viewToolStripMenuItem;
        private ToolStripMenuItem showMapToolStripMenuItem;
        private ToolStripMenuItem viewControllerMapToolStripMenuItem;
        private ToolStripMenuItem testGPSToolStripMenuItem;
        private ToolStripMenuItem helpToolStripMenuItem;
        private ToolStripMenuItem helpToolStripMenuItem1;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private ToolStripMenuItem practiceToolStripMenuItem2;

        private StatusStrip statusStrip;
        private ToolStripStatusLabel toolStripStatusTime;
        private ToolStripStatusLabel toolStripStatusLatitude;
        private ToolStripStatusLabel toolStripStatusLongitude;
        private ToolStripStatusLabel toolStripStatusAccuracy;
        private ToolStripStatusLabel toolStripStatusSatellites;
        private ToolStripStatusLabel toolStripStatusGPS;
        // Session controls (existing names preserved)
        private CheckBox rbAM;
        private CheckBox rbPM;
        private ComboBox cmbObserver;
        private RadioButton rbLeftFront;
        private RadioButton rbRightFront;
        private RadioButton rbLeftRear;
        private RadioButton rbRightRear;
        private Label lblCurrentTime;
        private Label lblTime;
        private Label lblObserver;
        private Label lblPosition;

        // Weather controls (existing names preserved)
        private ComboBox cmbCloud;
        private NumericUpDown nudTemperature;
        private NumericUpDown nudWind;
        private Label lblCloud;
        private Label lblTemperature;
        private Label lblWind;

        // Controller controls (existing names preserved)
        private AccentButton btnStart;

        // Data display (existing names preserved — txtDataStream becomes the session log)
        private TextBox txtDataStream;
        private Label lblLastObservation;
        private Label lblStatus;

        // ---- New SPOTTER controls ----
        private TableLayoutPanel mainGrid;            // 3-col layout: left rail | centre | right rail
        private Panel pnlLeftRail;
        private Panel pnlCentre;
        private Panel pnlRightRail;

        // Hero panel + components
        private Panel pnlHero;
        private Label lblHeroSection;
        private Label lblHeroFreshnessDot;
        private Label lblHeroFreshness;
        private CountTile heroCountTile;
        private CountTile heroLastInputTile;
        private Label lblHeroSpeciesLabel;
        private Label lblHeroSpecies;          // replaces the role of lblLastObservation
        private Label lblHeroDistanceLabel;
        private Label heroDistancePill;
        private Label lblHeroTimeLabel;
        private Label lblHeroTime;
        private Label lblHeroLocationLabel;
        private Label lblHeroLocation;
        private Panel pnlHeroSeparator;

        // Right-rail GPS panel
        private Panel pnlGps;
        private Label lblGpsSection;
        private Label lblGpsStatusDot;
        private Label lblGpsStatusText;
        private Label lblGpsPort;
        private Label lblGpsLatLabel; private Label lblGpsLatValue;
        private Label lblGpsLonLabel; private Label lblGpsLonValue;
        private Label lblGpsAccLabel; private Label lblGpsAccValue;
        private Label lblGpsSatLabel; private Label lblGpsSatValue;
        private SatelliteBars satBars;
        private Label lblSatBarsCaption;

        // Right-rail controller status
        private Panel pnlControllerStatus;
        private Label lblControllerSection;
        private Label lblControllerDot;
        private Label lblControllerStatusText;
        private Label lblLastInputLabel;
        private Panel pnlLastInputBadge;
        private Label lblLastInputButton;
        private Label lblLastInputText;

        // Right-rail status / file
        private Panel pnlFileStatus;
        private Label lblStatusSection;

        // Card panels for left rail
        private Panel pnlSessionCard;
        private Panel pnlWeatherCard;
        private Panel pnlAircraftCard;

        private Label lblBrandTitle;
        private Label lblBrandSubtitle;
        private Panel pnlBrandHeader;

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            // ============================================================
            // Form root
            // ============================================================
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1500, 900);
            this.MinimumSize = new System.Drawing.Size(900, 600);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "SPOTTER — NSW DPIRD Aerial Survey Logger";
            this.BackColor = Theme.BackgroundPrimary;
            this.Font = Theme.Body();
            this.Name = "ImprovedLogger";

            // ============================================================
            // Menu strip
            // ============================================================
            this.fileToolStripMenuItem = new ToolStripMenuItem("&File");
            this.quitToolStripMenuItem = new ToolStripMenuItem("&Quit");
            this.quitToolStripMenuItem.Click += new System.EventHandler(this.QuitToolStripMenuItem_Click);
            this.fileToolStripMenuItem.DropDownItems.Add(this.quitToolStripMenuItem);

            this.openObserverListToolStripMenuItem = new ToolStripMenuItem("Edit Observer List");
            this.openObserverListToolStripMenuItem.Click += new System.EventHandler(this.EditObserverListToolStripMenuItem_Click);
            this.editControllerConfigToolStripMenuItem = new ToolStripMenuItem("Edit Controller configuration");
            this.editControllerConfigToolStripMenuItem.Click += new System.EventHandler(this.EditControllerConfigToolStripMenuItem_Click);
            this.editToolStripMenuItem = new ToolStripMenuItem("&Edit");
            this.editToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
                this.openObserverListToolStripMenuItem,
                this.editControllerConfigToolStripMenuItem });

            this.showMapToolStripMenuItem = new ToolStripMenuItem("Show Map");
            this.showMapToolStripMenuItem.Click += new System.EventHandler(this.showMapToolStripMenuItem_Click_1);
            this.viewControllerMapToolStripMenuItem = new ToolStripMenuItem("Controller Map");
            this.viewControllerMapToolStripMenuItem.Click += new System.EventHandler(this.ViewControllerMapToolStripMenuItem_Click);
            this.viewToolStripMenuItem = new ToolStripMenuItem("View");
            this.viewToolStripMenuItem.DropDownItems.Add(this.showMapToolStripMenuItem);
            this.viewToolStripMenuItem.DropDownItems.Add(this.viewControllerMapToolStripMenuItem);

            this.testGPSToolStripMenuItem = new ToolStripMenuItem("Test GPS");
            this.testGPSToolStripMenuItem.Click += new System.EventHandler(this.testGPSToolStripMenuItem_Click);

            this.practiceToolStripMenuItem2 = new ToolStripMenuItem("Practice");
            this.practiceToolStripMenuItem2.Click += new System.EventHandler(this.practiceToolStripMenuItem2_Click);
            this.helpToolStripMenuItem1 = new ToolStripMenuItem("Help");
            this.helpToolStripMenuItem1.Click += new System.EventHandler(this.HelpToolStripMenuItem1_Click);
            this.aboutToolStripMenuItem = new ToolStripMenuItem("About");
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.AboutToolStripMenuItem_Click);
            this.helpToolStripMenuItem = new ToolStripMenuItem("&Help");
            this.helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
                this.practiceToolStripMenuItem2,
                this.helpToolStripMenuItem1,
                this.aboutToolStripMenuItem });

            this.menuStrip = new MenuStrip();
            this.menuStrip.Font = Theme.Body();
            this.menuStrip.BackColor = Theme.BackgroundPrimary;
            this.menuStrip.Padding = new Padding(8, 4, 0, 4);
            this.menuStrip.Items.AddRange(new ToolStripItem[] {
                this.fileToolStripMenuItem,
                this.editToolStripMenuItem,
                this.viewToolStripMenuItem,
                this.testGPSToolStripMenuItem,
                this.helpToolStripMenuItem });

            // ============================================================
            // Brand header — sits below the menu, above the main grid
            // ============================================================
            this.lblBrandTitle = new Label
            {
                Text = "SPOTTER",
                AutoSize = true,
                Font = new Font("Segoe UI", 14F, FontStyle.Regular),
                ForeColor = Theme.TextPrimary,
                Location = new Point(60, 8),
                BackColor = Color.Transparent
            };
            this.lblBrandSubtitle = new Label
            {
                Text = "NSW DPIRD · Aerial survey logger",
                AutoSize = true,
                Font = Theme.Subtitle(),
                ForeColor = Theme.TextSecondary,
                Location = new Point(62, 32),
                BackColor = Color.Transparent
            };

            var brandLogo = new Panel
            {
                Size = new Size(36, 36),
                Location = new Point(14, 8),
                BackColor = Color.Transparent
            };
            brandLogo.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(Theme.AccentPrimary))
                using (var path = Theme.RoundedRect(new Rectangle(1, 1, 34, 34), 8))
                    g.FillPath(brush, path);
                using (var font = new Font("Segoe UI", 15F, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.White))
                {
                    var sf = new StringFormat {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString("S", font, brush, new RectangleF(0, 0, 36, 36), sf);
                }
            };

            this.pnlBrandHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Theme.BackgroundPrimary,
                Padding = new Padding(0, 0, 0, 1)
            };
            this.pnlBrandHeader.Paint += (s, e) =>
            {
                // bottom border
                using (var pen = new Pen(Theme.BorderSecondary))
                    e.Graphics.DrawLine(pen, 0, this.pnlBrandHeader.Height - 1,
                        this.pnlBrandHeader.Width, this.pnlBrandHeader.Height - 1);
            };
            this.pnlBrandHeader.Controls.Add(brandLogo);
            this.pnlBrandHeader.Controls.Add(this.lblBrandTitle);
            this.pnlBrandHeader.Controls.Add(this.lblBrandSubtitle);

            // ============================================================
            // Status strip (footer)
            // ============================================================
            this.toolStripStatusTime = new ToolStripStatusLabel { Name = "toolStripStatusTime", Text = "—" };
            this.toolStripStatusLatitude = new ToolStripStatusLabel { Name = "toolStripStatusLatitude", Text = "Lat: —" };
            this.toolStripStatusLongitude = new ToolStripStatusLabel { Name = "toolStripStatusLongitude", Text = "Lon: —" };
            this.toolStripStatusAccuracy = new ToolStripStatusLabel { Name = "toolStripStatusAccuracy", Text = "± —" };
            this.toolStripStatusSatellites = new ToolStripStatusLabel { Name = "toolStripStatusSatellites", Text = "Sats: —" };
            this.toolStripStatusGPS = new ToolStripStatusLabel { Name = "toolStripStatusGPS", Text = "Waiting for GPS" };
            this.statusStrip = new StatusStrip
            {
                BackColor = Theme.BackgroundTertiary,
                Font = Theme.Caption(),
                SizingGrip = false,
                Padding = new Padding(8, 2, 8, 2)
            };
            this.toolStripStatusTime.Spring = true;
            this.toolStripStatusTime.TextAlign = ContentAlignment.MiddleRight;

            this.statusStrip.Items.AddRange(new ToolStripItem[] {
                this.toolStripStatusGPS,
                this.toolStripStatusLatitude,
                this.toolStripStatusLongitude,
                this.toolStripStatusAccuracy,
                this.toolStripStatusSatellites,
                this.toolStripStatusTime });

            // ============================================================
            // Main 3-column grid
            // ============================================================
            this.mainGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Theme.BackgroundPrimary,
                Padding = new Padding(0)
            };
            this.mainGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.RailWidth));
            this.mainGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.mainGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.RailWidth));
            this.mainGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // ----- Left rail container -----
            this.pnlLeftRail = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BackgroundTertiary,
                AutoScroll = true,
                Padding = new Padding(14, 14, 14, 14)
            };
            this.pnlLeftRail.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.BorderSecondary))
                    e.Graphics.DrawLine(pen, this.pnlLeftRail.Width - 1, 0,
                        this.pnlLeftRail.Width - 1, this.pnlLeftRail.Height);
            };

            BuildSessionCard();
            BuildWeatherCard();
            BuildAircraftCard();

            this.pnlAircraftCard.Dock = DockStyle.Top;
            this.pnlWeatherCard.Dock = DockStyle.Top;
            this.pnlSessionCard.Dock = DockStyle.Top;

            this.pnlLeftRail.Controls.Add(this.pnlAircraftCard);
            this.pnlLeftRail.Controls.Add(this.pnlWeatherCard);
            this.pnlLeftRail.Controls.Add(this.pnlSessionCard);

            // ----- Centre container -----
            this.pnlCentre = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BackgroundPrimary,
                Padding = new Padding(0)
            };
            BuildHeroPanel();
            BuildSessionLog();
            this.pnlCentre.Controls.Add(this.txtDataStream);
            this.pnlCentre.Controls.Add(this.pnlHero);

            // ----- Right rail container -----
            this.pnlRightRail = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BackgroundTertiary,
                AutoScroll = true,
                Padding = new Padding(14, 14, 14, 14)
            };
            this.pnlRightRail.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.BorderSecondary))
                    e.Graphics.DrawLine(pen, 0, 0, 0, this.pnlRightRail.Height);
            };
            BuildGpsCard();
            BuildControllerStatusCard();
            BuildFileStatusCard();
            this.pnlFileStatus.Dock = DockStyle.Top;
            this.pnlControllerStatus.Dock = DockStyle.Top;
            this.pnlGps.Dock = DockStyle.Top;
            this.pnlRightRail.Controls.Add(this.pnlFileStatus);
            this.pnlRightRail.Controls.Add(this.pnlControllerStatus);
            this.pnlRightRail.Controls.Add(this.pnlGps);

            this.mainGrid.Controls.Add(this.pnlLeftRail, 0, 0);
            this.mainGrid.Controls.Add(this.pnlCentre, 1, 0);
            this.mainGrid.Controls.Add(this.pnlRightRail, 2, 0);

            // ============================================================
            // Compose form (order matters: dock fill last)
            // ============================================================
            this.MainMenuStrip = this.menuStrip;
            this.mainGrid.Dock = DockStyle.Fill;

            this.Controls.Add(this.mainGrid);          // fills remaining space
            this.Controls.Add(this.statusStrip);       // docked bottom
            this.Controls.Add(this.pnlBrandHeader);    // docked top
            this.Controls.Add(this.menuStrip);         // docked top (above brand)

            this.FormClosing += new FormClosingEventHandler(this.ImprovedLogger_FormClosing);
            this.Load += new System.EventHandler(this.ImprovedLogger_Load);
        }

        // ============================================================
        // Card builders — each produces a self-contained card panel
        // and assigns it to the matching pnl* field
        // ============================================================

        private void BuildSessionCard()
        {
            this.pnlSessionCard = NewCardPanel("SESSION", out var body);

            this.lblTime = NewMutedLabel("Time", new Point(0, 0));
            this.lblCurrentTime = new Label
            {
                Font = Theme.MonoLarge(),
                ForeColor = Theme.TextPrimary,
                Text = "00:00:00",
                AutoSize = true,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                BackColor = Color.Transparent
            };
            // Right-align by setting location at paint time via parent resize
            body.Resize += (s, e) =>
            {
                this.lblCurrentTime.Location =
                    new Point(body.Width - this.lblCurrentTime.Width, 0);
            };
            this.lblCurrentTime.Location = new Point(140, 0);

            // AM / PM — CheckBoxes so either can be deselected by clicking again
            this.rbAM = NewToggle("AM", false);
            this.rbPM = NewToggle("PM", true);
            WireTogglePair(this.rbAM, this.rbPM);
            var segmentRow = LayoutSegment(this.rbAM, this.rbPM, body.Width);
            segmentRow.Location = new Point(0, 28);
            body.Resize += (s, e) => { segmentRow.Width = body.Width; };

            // Observer label + combobox
            this.lblObserver = NewMutedLabel("Observer", new Point(0, 70));
            this.cmbObserver = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                Font = Theme.Body(),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(0, 90),
                Width = body.Width
            };
            this.cmbObserver.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

            // Position label + 2x2 grid
            this.lblPosition = NewMutedLabel("Position", new Point(0, 124));
            var posGrid = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 2,
                Location = new Point(0, 144),
                Size = new Size(body.Width, 60),
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            posGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            posGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            posGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            posGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            this.rbLeftFront  = NewSegment("L. front", false);
            this.rbRightFront = NewSegment("R. front", false);
            this.rbLeftRear   = NewSegment("L. rear", false);
            this.rbRightRear  = NewSegment("R. rear", false);
            WireSegmentGroup(this.rbLeftFront, this.rbRightFront, this.rbLeftRear, this.rbRightRear);
            foreach (var rb in new[] { rbLeftFront, rbRightFront, rbLeftRear, rbRightRear })
                rb.Dock = DockStyle.Fill;
            posGrid.Controls.Add(this.rbLeftFront,  0, 0);
            posGrid.Controls.Add(this.rbRightFront, 1, 0);
            posGrid.Controls.Add(this.rbLeftRear,   0, 1);
            posGrid.Controls.Add(this.rbRightRear,  1, 1);

            body.Controls.Add(this.lblTime);
            body.Controls.Add(this.lblCurrentTime);
            body.Controls.Add(segmentRow);
            body.Controls.Add(this.lblObserver);
            body.Controls.Add(this.cmbObserver);
            body.Controls.Add(this.lblPosition);
            body.Controls.Add(posGrid);

            // Card height: header (32) + padding (24) + content (~210)
            this.pnlSessionCard.Height = 32 + 24 + 210;
        }

        private void BuildWeatherCard()
        {
            this.pnlWeatherCard = NewCardPanel("WEATHER", out var body);

            int y = 0;
            int yCloud = y;
            this.lblCloud = NewMutedLabel("Cloud (oktas)", new Point(0, yCloud));
            this.cmbCloud = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.Body(),
                Width = 80,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            for (int i = 0; i <= 8; i++) this.cmbCloud.Items.Add($"{i}/8");
            this.cmbCloud.SelectedIndex = 0;
            body.Resize += (s, e) =>
                this.cmbCloud.Location = new Point(body.Width - this.cmbCloud.Width, yCloud);
            this.cmbCloud.Location = new Point(120, yCloud);

            y += 36;
            this.lblTemperature = NewMutedLabel("Temperature (°C)", new Point(0, y));
            this.nudTemperature = new NumericUpDown
            {
                Minimum = -40, Maximum = 60, Value = 20,
                Font = Theme.Body(),
                Width = 80,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            int yT = y;
            body.Resize += (s, e) =>
                this.nudTemperature.Location = new Point(body.Width - this.nudTemperature.Width, yT);
            this.nudTemperature.Location = new Point(120, y);

            y += 36;
            this.lblWind = NewMutedLabel("Wind (knots)", new Point(0, y));
            this.nudWind = new NumericUpDown
            {
                Minimum = 0, Maximum = 100, Value = 0,
                Font = Theme.Body(),
                Width = 80,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            int yW = y;
            body.Resize += (s, e) =>
                this.nudWind.Location = new Point(body.Width - this.nudWind.Width, yW);
            this.nudWind.Location = new Point(120, y);

            body.Controls.Add(this.lblCloud);
            body.Controls.Add(this.cmbCloud);
            body.Controls.Add(this.lblTemperature);
            body.Controls.Add(this.nudTemperature);
            body.Controls.Add(this.lblWind);
            body.Controls.Add(this.nudWind);

            this.pnlWeatherCard.Height = 32 + 24 + (y + 30);
        }

        private void BuildAircraftCard()
        {
            this.pnlAircraftCard = NewCardPanel("AIRCRAFT", out var body);

            this.btnStart = new AccentButton
            {
                Text = "Start",
                FillColour = Theme.AccentPrimary,
                Location = new Point(0, 0),
                Width = body.Width,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);

            body.Controls.Add(this.btnStart);

            this.pnlAircraftCard.Height = 32 + 24 + 60;
        }

        private void BuildHeroPanel()
        {
            this.pnlHero = new Panel
            {
                Dock = DockStyle.Top,
                Height = 140,
                BackColor = Theme.BackgroundPrimary,
                Padding = new Padding(24, 16, 24, 18)
            };
            this.pnlHero.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.BorderSecondary))
                    e.Graphics.DrawLine(pen, 0, this.pnlHero.Height - 1,
                        this.pnlHero.Width, this.pnlHero.Height - 1);
            };

            this.lblHeroSection = new Label
            {
                Text = "LAST OBSERVATION",
                Font = Theme.Caption(),
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                Location = new Point(24, 16),
                BackColor = Color.Transparent
            };
            this.lblHeroFreshnessDot = new Label
            {
                Text = "●",
                Font = new Font("Segoe UI", 10F),
                ForeColor = Theme.GoodDot,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            this.lblHeroFreshness = new Label
            {
                Text = "Awaiting first input",
                Font = Theme.Caption(),
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            this.pnlHero.Resize += (s, e) =>
            {
                this.lblHeroFreshness.Location =
                    new Point(this.pnlHero.Width - this.lblHeroFreshness.Width - 24, 18);
                this.lblHeroFreshnessDot.Location =
                    new Point(this.lblHeroFreshness.Left - 14, 13);
            };

            // Row 1: count tile + last-input tile + species
            this.heroCountTile = new CountTile
            {
                Location = new Point(24, 44),
                Value = "—"
            };
            this.heroLastInputTile = new CountTile
            {
                Location = new Point(24 + Theme.CountTileSize + 8, 44),
                Value = "—",
                Caption = "LAST"
            };
            this.lblHeroSpeciesLabel = new Label
            {
                Text = "SPECIES",
                Font = Theme.Caption(),
                ForeColor = Theme.TextTertiary,
                AutoSize = true,
                Location = new Point(24 + Theme.CountTileSize * 2 + 26, 50),
                BackColor = Color.Transparent
            };
            this.lblHeroSpecies = new Label
            {
                Text = "—",
                Font = Theme.HeroSpecies(),
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Location = new Point(24 + Theme.CountTileSize * 2 + 26, 68),
                BackColor = Color.Transparent
            };
            // Keep the public name lblLastObservation pointed at the hero species label
            // so existing code in ImprovedLogger.cs that writes to lblLastObservation still works.
            this.lblLastObservation = this.lblHeroSpecies;

            // Distance label — right-aligned on same row as count/species, plain text
            this.heroDistancePill = new Label
            {
                Text = "—",
                Font = Theme.HeroSpecies(),
                ForeColor = Theme.TextPrimary,
                BackColor = Color.Transparent,
                AutoSize = true,
            };
            void positionPill() => this.heroDistancePill.Location =
                new Point(this.pnlHero.Width - this.heroDistancePill.Width - 24, 62);
            this.pnlHero.Resize          += (s, e) => positionPill();
            this.heroDistancePill.Resize  += (s, e) => positionPill();
            positionPill();

            // Keep unused hero labels alive (non-null) so any residual code paths compile
            this.pnlHeroSeparator  = new Panel();
            this.lblHeroDistanceLabel = new Label();
            this.lblHeroTimeLabel  = new Label();
            this.lblHeroTime       = new Label();
            this.lblHeroLocationLabel = new Label();
            this.lblHeroLocation   = new Label();

            this.pnlHero.Controls.Add(this.lblHeroSection);
            this.pnlHero.Controls.Add(this.lblHeroFreshnessDot);
            this.pnlHero.Controls.Add(this.lblHeroFreshness);
            this.pnlHero.Controls.Add(this.heroCountTile);
            this.pnlHero.Controls.Add(this.heroLastInputTile);
            this.pnlHero.Controls.Add(this.lblHeroSpeciesLabel);
            this.pnlHero.Controls.Add(this.lblHeroSpecies);
            this.pnlHero.Controls.Add(this.heroDistancePill);
        }

        private void BuildSessionLog()
        {
            this.txtDataStream = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = Theme.Mono(),
                ForeColor = Theme.TextPrimary,
                BackColor = Theme.BackgroundPrimary
            };
        }

        private void BuildGpsCard()
        {
            this.pnlGps = NewCardPanel("GPS", out var body);

            this.lblGpsStatusDot = new Label
            {
                Text = "●",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Theme.SatelliteUnused,
                AutoSize = true,
                Location = new Point(0, 0),
                BackColor = Color.Transparent
            };
            this.lblGpsStatusText = new Label
            {
                Text = "Waiting for fix",
                Font = Theme.BodyBold(),
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                Location = new Point(16, 0),
                BackColor = Color.Transparent
            };
            this.lblGpsPort = new Label
            {
                Text = "—",
                Font = Theme.Caption(),
                ForeColor = Theme.TextTertiary,
                AutoSize = true,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                BackColor = Color.Transparent
            };
            body.Resize += (s, e) =>
                this.lblGpsPort.Location = new Point(body.Width - this.lblGpsPort.Width, 2);

            int y = 26;
            (this.lblGpsLatLabel, this.lblGpsLatValue) = NewKeyValueRow(body, "Latitude", "—", y);  y += 22;
            (this.lblGpsLonLabel, this.lblGpsLonValue) = NewKeyValueRow(body, "Longitude", "—", y); y += 22;
            (this.lblGpsAccLabel, this.lblGpsAccValue) = NewKeyValueRow(body, "Accuracy", "—", y);  y += 22;
            (this.lblGpsSatLabel, this.lblGpsSatValue) = NewKeyValueRow(body, "Satellites", "—", y);
            y += 28;

            this.satBars = new SatelliteBars
            {
                Location = new Point(0, y),
                Width = body.Width,
                Height = 32,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            y += 36;
            this.lblSatBarsCaption = new Label
            {
                Text = "Signal strength (awaiting SNR feed)",
                Font = Theme.Caption(),
                ForeColor = Theme.TextTertiary,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, y),
                Width = body.Width,
                Height = 14,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                BackColor = Color.Transparent
            };

            body.Controls.Add(this.lblGpsStatusDot);
            body.Controls.Add(this.lblGpsStatusText);
            body.Controls.Add(this.lblGpsPort);
            body.Controls.Add(this.satBars);
            body.Controls.Add(this.lblSatBarsCaption);

            this.pnlGps.Height = 32 + 24 + y + 24;
        }

        private void BuildControllerStatusCard()
        {
            this.pnlControllerStatus = NewCardPanel("CONTROLLER", out var body);

            this.lblControllerDot = new Label
            {
                Text = "●",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Theme.SatelliteUnused,
                AutoSize = true,
                Location = new Point(0, 0),
                BackColor = Color.Transparent
            };
            this.lblControllerStatusText = new Label
            {
                Text = "Not connected",
                Font = Theme.BodyBold(),
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                Location = new Point(16, 0),
                BackColor = Color.Transparent
            };

            // Stub out legacy last-input fields so any existing references compile cleanly
            this.lblLastInputLabel = new Label();
            this.pnlLastInputBadge = new Panel();
            this.lblLastInputButton = new Label();
            this.lblLastInputText = new Label();

            body.Controls.Add(this.lblControllerDot);
            body.Controls.Add(this.lblControllerStatusText);

            this.pnlControllerStatus.Height = 32 + 24 + 30;
        }

        private void BuildFileStatusCard()
        {
            this.pnlFileStatus = NewCardPanel("STATUS", out var body);

            this.lblStatus = new Label
            {
                Text = "Ready.",
                Font = Theme.Caption(),
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                TextAlign = ContentAlignment.TopLeft,
                Location = new Point(0, 0),
                Size = new Size(body.Width, 60),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom,
                BackColor = Color.Transparent
            };
            body.Controls.Add(this.lblStatus);

            this.pnlFileStatus.Height = 32 + 24 + 70;
        }

        // ============================================================
        // Helpers
        // ============================================================

        /// <summary>
        /// Creates a "card" panel with a section heading and an inner body
        /// panel ready to receive content. Card has rounded white background
        /// and is sized for the rail width minus padding.
        /// </summary>
        private Panel NewCardPanel(string sectionTitle, out Panel body)
        {
            var card = new Panel
            {
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 14),
                Padding = new Padding(0, 0, 0, 0),
                Width = Theme.RailWidth - 28
            };

            var heading = new Label
            {
                Text = sectionTitle,
                Font = Theme.BodyBold(),
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Location = new Point(0, 0),
                BackColor = Color.Transparent
            };
            card.Controls.Add(heading);

            var inner = new Panel
            {
                BackColor = Theme.BackgroundPrimary,
                Location = new Point(0, 24),
                Width = card.Width,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom,
                Padding = new Padding(12, 12, 12, 12)
            };
            // Border drawn manually so we get a 1px rounded effect via Region
            inner.Region = new System.Drawing.Region(Theme.RoundedRect(
                new Rectangle(0, 0, inner.Width, 9999), Theme.CornerRadius));
            inner.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.BorderSecondary))
                {
                    var r = new Rectangle(0, 0, inner.Width - 1, inner.Height - 1);
                    using (var path = Theme.RoundedRect(r, Theme.CornerRadius))
                        e.Graphics.DrawPath(pen, path);
                }
            };

            body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            inner.Controls.Add(body);

            card.Controls.Add(inner);
            card.Resize += (s, e) =>
            {
                inner.Height = card.Height - 24;
            };

            return card;
        }

        private Label NewMutedLabel(string text, Point location)
        {
            return new Label
            {
                Text = text,
                Font = Theme.Caption(),
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                Location = location,
                BackColor = Color.Transparent
            };
        }

        private (Label, Label) NewKeyValueRow(Panel parent, string key, string value, int y)
        {
            var lblKey = new Label
            {
                Text = key,
                Font = Theme.Caption(),
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                Location = new Point(0, y),
                BackColor = Color.Transparent
            };
            var lblValue = new Label
            {
                Text = value,
                Font = Theme.Mono(),
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                BackColor = Color.Transparent
            };
            // Reposition on parent resize AND when the label auto-sizes (text changed)
            void realign() => lblValue.Location = new Point(parent.Width - lblValue.Width, y);
            parent.Resize += (s, e) => realign();
            lblValue.Resize += (s, e) => realign();
            realign();
            parent.Controls.Add(lblKey);
            parent.Controls.Add(lblValue);
            return (lblKey, lblValue);
        }

        private RadioButton NewSegment(string text, bool isChecked)
        {
            return new RadioButton
            {
                Text = text,
                Appearance = Appearance.Button,
                FlatStyle = FlatStyle.Flat,
                Checked = isChecked,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Theme.BodyBold(),
                BackColor = Theme.SegmentBackground,     // mid-dark grey — white text = ~7.5:1 contrast
                ForeColor = Color.White,
                Margin = new Padding(2),
                Padding = new Padding(0),
                Height = 28,
                FlatAppearance =
                {
                    BorderSize = 0,
                    CheckedBackColor = Theme.AccentPrimary,   // navy blue — white text = ~5.5:1
                    MouseOverBackColor = Theme.SegmentHover,
                }
            };
        }

        /// <summary>
        /// Wires CheckedChanged on every button in the group so BackColor reliably
        /// reflects checked state (WinForms' FlatAppearance.CheckedBackColor is not
        /// always honoured on Windows 11 when visual styles are enabled).
        /// </summary>
        private void WireSegmentGroup(params RadioButton[] buttons)
        {
            void Refresh() {
                foreach (var rb in buttons)
                    rb.BackColor = rb.Checked ? Theme.AccentPrimary : Theme.SegmentBackground;
            }
            foreach (var rb in buttons)
                rb.CheckedChanged += (s, e) => Refresh();
            Refresh(); // Apply initial state immediately
        }

        private FlowLayoutPanel LayoutSegment(ButtonBase a, ButtonBase b, int width)
        {
            a.Margin = Padding.Empty;
            b.Margin = Padding.Empty;
            // Set widths eagerly — Resize never fires on a fixed-width rail.
            a.Width = width / 2;
            b.Width = width - width / 2;
            var flow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Size = new Size(width, 30),
                AutoSize = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            flow.Resize += (s, e) =>
            {
                a.Width = flow.Width / 2;
                b.Width = flow.Width - flow.Width / 2;
            };
            flow.Controls.Add(a);
            flow.Controls.Add(b);
            return flow;
        }

        private CheckBox NewToggle(string text, bool isChecked)
        {
            return new CheckBox
            {
                Text = text,
                Appearance = Appearance.Button,
                FlatStyle = FlatStyle.Standard,
                Checked = isChecked,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Theme.BodyBold(),
                Margin = new Padding(2),
                Padding = new Padding(0),
                Height = 28,
            };
        }

        private void WireTogglePair(CheckBox a, CheckBox b)
        {
            a.CheckedChanged += (s, e) => { if (a.Checked) b.Checked = false; };
            b.CheckedChanged += (s, e) => { if (b.Checked) a.Checked = false; };
        }

        #endregion
    }
}
