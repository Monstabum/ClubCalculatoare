using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using AppClubCalculatoare.UI;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Tabloul de bord cu indicatori-cheie (KPI) — design modern.
    /// Layout responsiv: KPI cards în grid, grafice în TabControl,
    /// top operatori, alerte clicabile.
    /// </summary>
    internal class PanouDashboard : UserControl
    {
        private readonly string connectionString;

        // ── KPI cards ──────────────────────────────────────────────
        private KpiCard kpiEchipamente, kpiSesiuni, kpiRezervari;
        private KpiCard kpiIncasareLuna, kpiIncasareTotal, kpiPortofoliu;

        // ── Top 3 operatori ───────────────────────────────────────────
        private readonly Label[] lblOperatorNume = new Label[3];
        private readonly Label[] lblOperatorIncasare = new Label[3];

        // ── Layout refs (pentru responsive) ────────────────────────
        private Panel _leftCol, _rightCol, _alertCard;
        private Panel _periodPanel;
        private ModernButton _btnArataTot;
        private int _yGrafice, _yAlerte;
        private const int _pad = 16;
        private const int _gap = 16;

        // ── Selector perioadă ──────────────────────────────────────
        private ComboBox cmbAn, cmbLuna;
        private const string ToateAnii = "Toate anii";
        private const string ToateLunile = "Toate lunile";
        private bool _incarcSelectii;
        private bool _primaIncarcare = true;

        // ── Alerte ─────────────────────────────────────────────────
        private Label lblRezumatAlerte;
        private readonly Label[] lblAlertaBadge = new Label[3];
        private readonly Label[] lblAlertaText = new Label[3];
        private readonly Label[] lblAlertaDetaliu = new Label[3];
        private readonly int[] numarAlerta = new int[3];

        // Caiet de sarcini 3.1.7: pragurile alertelor sunt configurabile din App.config
        // (AlertPropertyDays / AlertTransactionDays / AlertViewingDays).
        // Valorile implicite (90 / 30 / 7) se folosesc daca cheia lipseste sau e invalida.
        private static readonly int ZileEchipamentStagnanta  = CitesteZile("AlertPropertyDays", 90);
        private static readonly int ZileSesiuneInAsteptare = CitesteZile("AlertTransactionDays", 30);
        private static readonly int ZileRezervariViitoare     = CitesteZile("AlertViewingDays", 7);

        private static int CitesteZile(string cheie, int implicit_)
        {
            string val = System.Configuration.ConfigurationManager.AppSettings[cheie];
            int zile;
            return int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out zile)
                   && zile > 0 && zile <= 3650
                ? zile : implicit_;
        }
        // FIX_21: lunile se afișează în română indiferent de limba Windows (pe un OS rus apăreau nume de luni chirilice în interfața română).
        private static readonly CultureInfo CulturaRo = new CultureInfo("ro-RO");

        public event Action<int, bool> AlertaDoubleClick;

        // ── Grafice ────────────────────────────────────────────────
        private TabControl tabGrafice;
        private Chart chartIncasareLunar;
        private Chart chartTipuriEchipamente;
        private Chart chartOperatoriSesiuni;

        // ── Fonturi refolosite ─────────────────────────────────────
        private readonly Font fontAxa = new Font(Theme.FontFamily, 7.5f);
        private readonly Font fontAxaBold = new Font(Theme.FontFamily, 8f, FontStyle.Bold);
        private readonly Font fontLegend = new Font(Theme.FontFamily, 8f);
        private readonly Font fontTitluGrafic = new Font(Theme.FontFamily, 8.5f, FontStyle.Bold);
        private readonly Font fontMesaj = new Font(Theme.FontFamily, 8f, FontStyle.Italic);
        private readonly Font fontAlertaActiva = new Font(Theme.FontFamily, 8.5f, FontStyle.Bold);
        private readonly Font fontAlertaInactiva = new Font(Theme.FontFamily, 8.5f);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                fontAxa.Dispose(); fontAxaBold.Dispose(); fontLegend.Dispose();
                fontTitluGrafic.Dispose(); fontMesaj.Dispose();
                fontAlertaActiva.Dispose(); fontAlertaInactiva.Dispose();
            }
            base.Dispose(disposing);
        }

        public PanouDashboard(string connString)
        {
            connectionString = connString;
            // FIX_12: AutoScroll activ — pe 1366x768 tot conținutul (KPI + grafice + alerte) rămâne accesibil prin scroll.
            this.AutoScroll = true;
            this.BackColor = Theme.Background;
            this.Dock = DockStyle.Fill;
            ConstruiesteInterfata();
            this.Resize += (s, e) => AplicaLayoutResponsiv();
            IncarcaAsincron();
        }

        private void AplicaLayoutResponsiv()
        {
            if (_leftCol == null || _rightCol == null || _alertCard == null) return;
            if (this.Width <= 0) return;
            this.SuspendLayout();

            int availW = this.ClientSize.Width - _pad * 2;
            if (availW < 300) availW = 300;
            int cardW = Math.Max(180, (availW - _gap * 2) / 3);
            int cardH = 110;

            // KPI — 2 rânduri a 3
            if (kpiEchipamente != null)
            {
                kpiEchipamente.Location = new Point(_pad, 76);
                kpiEchipamente.Size = new Size(cardW, cardH);
                kpiSesiuni.Location = new Point(_pad + cardW + _gap, 76);
                kpiSesiuni.Size = new Size(cardW, cardH);
                kpiRezervari.Location = new Point(_pad + (cardW + _gap) * 2, 76);
                kpiRezervari.Size = new Size(cardW, cardH);

                int row2 = 76 + cardH + _gap;
                kpiIncasareLuna.Location = new Point(_pad, row2);
                kpiIncasareLuna.Size = new Size(cardW, cardH);
                kpiIncasareTotal.Location = new Point(_pad + cardW + _gap, row2);
                kpiIncasareTotal.Size = new Size(cardW, cardH);
                kpiPortofoliu.Location = new Point(_pad + (cardW + _gap) * 2, row2);
                kpiPortofoliu.Size = new Size(cardW, cardH);
            }

            // Grafice + Top 3 — proporțional, fără tăiere
            int yGraf = _yGrafice;
            int leftW = (int)Math.Round(availW * 0.62);
            int rightW = availW - leftW - _gap;
            if (rightW < 260) { leftW = availW - 260 - _gap; rightW = 260; }
            if (leftW < 380)  { leftW = 380; rightW = availW - leftW - _gap; }

            _leftCol.Location = new Point(_pad, yGraf);
            _leftCol.Size = new Size(leftW, 300);
            _rightCol.Location = new Point(_pad + leftW + _gap, yGraf);
            _rightCol.Size = new Size(rightW, 300);
            Theme.Rounded(_rightCol, Theme.Radius);

            // Alerte — pe toată lățimea
            _alertCard.Location = new Point(_pad, _yAlerte);
            _alertCard.Size = new Size(availW, 200);
            Theme.Rounded(_alertCard, Theme.Radius);

            // — Re-poziționează elementele din interiorul alertei —
            if (_btnArataTot != null && lblRezumatAlerte != null)
            {
                _btnArataTot.Location = new Point(_alertCard.Width - 96, 14);
                lblRezumatAlerte.Location = new Point(_alertCard.Width - 96 - lblRezumatAlerte.Width - 12, 16);
            }
            for (int i = 0; i < 3; i++)
            {
                if (lblAlertaDetaliu[i] != null)
                {
                    lblAlertaDetaliu[i].Location = new Point(_alertCard.Width - 250, 65 + i * 42);
                    lblAlertaDetaliu[i].Size = new Size(230, 18);
                }
                if (lblAlertaText[i] != null)
                {
                    int txtW = _alertCard.Width - 64 - 250 - 16;
                    if (txtW < 120) txtW = 120;
                    lblAlertaText[i].Size = new Size(txtW, 22);
                }
            }

            // Period panel — să nu iasă din ecran
            if (_periodPanel != null)
            {
                int maxX = this.ClientSize.Width - _periodPanel.Width - _pad;
                if (maxX < _pad) maxX = _pad;
                _periodPanel.Location = new Point(maxX, _periodPanel.Location.Y);
            }

            // Ajustează înălțimea totală ca să nu apară scroll dublu
            var lblStatusCtrl = this.Controls.Find("lblStatus", true);
            if (lblStatusCtrl.Length > 0)
            {
                lblStatusCtrl[0].Location = new Point(_pad, _yAlerte + 215);
            }
            this.AutoScrollMinSize = new Size(0, _yAlerte + 240);

            this.ResumeLayout(false);
            this.Invalidate();
        }

        // ════════════════════════════════════════════════════════════
        //  CONSTRUIRE INTERFAȚĂ
        // ════════════════════════════════════════════════════════════
        private void ConstruiesteInterfata()
        {
            int y = 0;
            int pad = _pad;

            // ── Titlu + selector perioadă ──────────────────────────
            var lblTitlu = new Label
            {
                Text      = "Bine ai venit 👋",
                Font      = Theme.H1,
                ForeColor = Theme.Text,
                AutoSize  = true,
                Location  = new Point(pad, y)
            };
            this.Controls.Add(lblTitlu);

            var lblSub = new Label
            {
                Text      = "Situația clubului · actualizat automat",
                Font      = Theme.Body,
                ForeColor = Theme.TextMuted,
                AutoSize  = true,
                Location  = new Point(pad, y + 34)
            };
            this.Controls.Add(lblSub);

            // Selector perioadă (dreapta sus)
            var periodPanel = new Panel
            {
                Location  = new Point(520, y + 4),
                Size      = new Size(420, 40),
                Anchor    = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.Transparent
            };
            _periodPanel = periodPanel;
            this.Controls.Add(periodPanel);

            var lblAn = new Label
            {
                Text      = "An:",
                Font      = Theme.SmallBold,
                ForeColor = Theme.TextMuted,
                AutoSize  = true,
                Location  = new Point(0, 10)
            };
            periodPanel.Controls.Add(lblAn);

            cmbAn = new ComboBox
            {
                Location      = new Point(28, 6),
                Size          = new Size(72, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = Theme.Small
            };
            cmbAn.SelectedIndexChanged += (s, e) => SchimbaPerioada();
            periodPanel.Controls.Add(cmbAn);

            var lblLuna = new Label
            {
                Text      = "Luna:",
                Font      = Theme.SmallBold,
                ForeColor = Theme.TextMuted,
                AutoSize  = true,
                Location  = new Point(112, 10)
            };
            periodPanel.Controls.Add(lblLuna);

            cmbLuna = new ComboBox
            {
                Location      = new Point(152, 6),
                Size          = new Size(110, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = Theme.Small
            };
            cmbLuna.Items.Add(ToateLunile);
            for (int i = 1; i <= 12; i++)
                cmbLuna.Items.Add($"{i:00} {new DateTime(2000, i, 1).ToString("MMM", CulturaRo)}");
            cmbLuna.SelectedIndex = 0;
            cmbLuna.SelectedIndexChanged += (s, e) => SchimbaPerioada();
            periodPanel.Controls.Add(cmbLuna);

            var btnRefresh = new ModernButton
            {
                Text     = "Reîncarcă",
                Variant  = ButtonVariant.Primary,
                Location = new Point(276, 4),
                Size     = new Size(140, 32)
            };
            btnRefresh.Click += (s, e) => Reincarca();
            periodPanel.Controls.Add(btnRefresh);

            y += 76;

            // ── KPI cards (grid 3×2) ───────────────────────────────
            int cardW = 300, cardH = 110, gap = _gap;
            int row1 = y;
            int row2 = y + cardH + gap;

            kpiEchipamente = new KpiCard
            {
                Icon = "\uEC0B", Title = "Echipamente libere",
                Accent = Theme.Accent, Location = new Point(pad, row1), Size = new Size(cardW, cardH)
            };
            kpiSesiuni = new KpiCard
            {
                Icon = "\uE8F5", Title = "Sesiuni în așteptare",
                Accent = Theme.Warning, Location = new Point(pad + cardW + gap, row1), Size = new Size(cardW, cardH)
            };
            kpiRezervari = new KpiCard
            {
                Icon = "\uE787", Title = "Rezervări programate",
                Accent = Theme.Success, Location = new Point(pad + (cardW + gap) * 2, row1), Size = new Size(cardW, cardH)
            };
            kpiIncasareLuna = new KpiCard
            {
                Icon = "\uE8AF", Title = "Încasare pe perioadă",
                Accent = Theme.Accent, Location = new Point(pad, row2), Size = new Size(cardW, cardH)
            };
            kpiIncasareTotal = new KpiCard
            {
                Icon = "\uEC58", Title = "Încasare totală",
                Accent = Color.FromArgb(0xA7, 0xB6, 0xFF), Location = new Point(pad + cardW + gap, row2), Size = new Size(cardW, cardH)
            };
            kpiPortofoliu = new KpiCard
            {
                Icon = "\uE9D8", Title = "Valoare portofoliu",
                Accent = Color.FromArgb(0x6D, 0x8B, 0xFF), Location = new Point(pad + (cardW + gap) * 2, row2), Size = new Size(cardW, cardH)
            };

            this.Controls.Add(kpiEchipamente);
            this.Controls.Add(kpiSesiuni);
            this.Controls.Add(kpiRezervari);
            this.Controls.Add(kpiIncasareLuna);
            this.Controls.Add(kpiIncasareTotal);
            this.Controls.Add(kpiPortofoliu);

            y = row2 + cardH + 20;
            _yGrafice = y;

            // ── Rând 2: grafice + top operatori ───────────────────────
            var leftCol = new Panel
            {
                Location  = new Point(pad, y),
                Size      = new Size(640, 300),
                BackColor = Theme.Surface,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left
            };
            _leftCol = leftCol;
            this.Controls.Add(leftCol);

            CreeazaGrafice(leftCol);

            // Top 3 operatori (dreapta)
            var rightCol = new Panel
            {
                Location  = new Point(660, y),
                Size      = new Size(340, 300),
                BackColor = Theme.Surface,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            _rightCol = rightCol;
            Theme.Rounded(rightCol, Theme.Radius);
            rightCol.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border, 1f))
                {
                    var rect = new Rectangle(0, 0, rightCol.Width - 1, rightCol.Height - 1);
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                    {
                        path.AddArc(rect.X, rect.Y, 20, 20, 180, 90);
                        path.AddArc(rect.Right - 20, rect.Y, 20, 20, 270, 90);
                        path.AddArc(rect.Right - 20, rect.Bottom - 20, 20, 20, 0, 90);
                        path.AddArc(rect.X, rect.Bottom - 20, 20, 20, 90, 90);
                        path.CloseFigure();
                        e.Graphics.DrawPath(pen, path);
                    }
                }
            };
            this.Controls.Add(rightCol);

            var lblTop = new Label
            {
                Text      = "🏆 TOP 3 OPERATORI",
                Font      = Theme.H3,
                ForeColor = Theme.Text,
                AutoSize  = true,
                Location  = new Point(18, 16)
            };
            rightCol.Controls.Add(lblTop);

            var lblTopSub = new Label
            {
                Text      = "după încasarea totală",
                Font      = Theme.Caption,
                ForeColor = Theme.TextMuted,
                AutoSize  = true,
                Location  = new Point(18, 42)
            };
            rightCol.Controls.Add(lblTopSub);

            for (int i = 0; i < 3; i++)
            {
                int ry = 78 + i * 60;

                var rank = new Label
                {
                    Text      = (i + 1).ToString(),
                    Font      = Theme.SmallBold,
                    ForeColor = i == 0 ? Color.White
                             : (i == 1 ? MaterialTheme.PrimaryLight : MaterialTheme.TextPrimary),
                    BackColor = i == 0 ? Theme.Accent :
                               (i == 1 ? Theme.AccentSoft :
                               MaterialTheme.SurfaceAlt),
                    Location  = new Point(18, ry),
                    Size      = new Size(28, 28),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                Theme.Rounded(rank, 8);
                rightCol.Controls.Add(rank);

                lblOperatorNume[i] = new Label
                {
                    Text      = "-",
                    Font      = Theme.BodyBold,
                    ForeColor = Theme.Text,
                    AutoSize  = false,
                    Location  = new Point(58, ry + 2),
                    Size      = new Size(180, 20)
                };
                rightCol.Controls.Add(lblOperatorNume[i]);

                lblOperatorIncasare[i] = new Label
                {
                    Text      = "-",
                    Font      = Theme.SmallBold,
                    ForeColor = Theme.Success,
                    AutoSize  = false,
                    Location  = new Point(58, ry + 22),
                    Size      = new Size(260, 18)
                };
                rightCol.Controls.Add(lblOperatorIncasare[i]);
            }

            y += 320;
            _yAlerte = y;

            // ── Alerte ─────────────────────────────────────────────
            var alertCard = new Panel
            {
                Location  = new Point(pad, y),
                Size      = new Size(1000, 200),
                BackColor = Theme.Surface,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _alertCard = alertCard;
            Theme.Rounded(alertCard, Theme.Radius);
            alertCard.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border, 1f))
                {
                    var rect = new Rectangle(0, 0, alertCard.Width - 1, alertCard.Height - 1);
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                    {
                        path.AddArc(rect.X, rect.Y, 20, 20, 180, 90);
                        path.AddArc(rect.Right - 20, rect.Y, 20, 20, 270, 90);
                        path.AddArc(rect.Right - 20, rect.Bottom - 20, 20, 20, 0, 90);
                        path.AddArc(rect.X, rect.Bottom - 20, 20, 20, 90, 90);
                        path.CloseFigure();
                        e.Graphics.DrawPath(pen, path);
                    }
                }
            };
            this.Controls.Add(alertCard);

            var lblAlerte = new Label
            {
                Text      = "⚠️ ALERTE OPERAȚIONALE",
                Font      = Theme.H3,
                ForeColor = Theme.Text,
                AutoSize  = true,
                Location  = new Point(18, 16)
            };
            alertCard.Controls.Add(lblAlerte);

            lblRezumatAlerte = new Label
            {
                Text      = "",
                Font      = Theme.SmallBold,
                ForeColor = Theme.Success,
                AutoSize  = true,
                Location  = new Point(alertCard.Width - 220, 18),
                TextAlign = ContentAlignment.MiddleRight,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            lblRezumatAlerte.AutoSize = true;
            alertCard.Controls.Add(lblRezumatAlerte);

            var btnArataTot = new ModernButton
            {
                Text     = "Arată tot",
                Variant  = ButtonVariant.Success,
                Location = new Point(alertCard.Width - 96, 14),
                Size     = new Size(80, 28),
                Anchor   = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnArataTot = btnArataTot;
            alertCard.Controls.Add(btnArataTot);

            var meniuArataTot = new ContextMenuStrip();
            meniuArataTot.Items.Add("Echipamente — lista completă", null,
                (s, e) => AlertaDoubleClick?.Invoke(0, true));
            meniuArataTot.Items.Add("Rezervări — lista completă", null,
                (s, e) => AlertaDoubleClick?.Invoke(1, true));
            meniuArataTot.Items.Add("Sesiuni — lista completă", null,
                (s, e) => AlertaDoubleClick?.Invoke(2, true));
            btnArataTot.Click += (s, e) =>
                meniuArataTot.Show(btnArataTot, new Point(0, btnArataTot.Height));

            for (int i = 0; i < 3; i++)
            {
                int ay = 60 + i * 42;

                lblAlertaBadge[i] = new Label
                {
                    Text      = "0",
                    Font      = Theme.SmallBold,
                    ForeColor = Color.White,
                    BackColor = Theme.Success,
                    Location  = new Point(18, ay),
                    Size      = new Size(28, 28),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Cursor    = Cursors.Hand
                };
                Theme.Rounded(lblAlertaBadge[i], 8);
                lblAlertaBadge[i].DoubleClick += (s, e) => RidicaAlerta(i);
                alertCard.Controls.Add(lblAlertaBadge[i]);

                lblAlertaText[i] = new Label
                {
                    Text      = "",
                    Font      = fontAlertaInactiva,
                    ForeColor = Theme.TextMuted,
                    AutoSize  = false,
                    Location  = new Point(58, ay + 3),
                    Size      = new Size(600, 22),
                    Cursor    = Cursors.Hand
                };
                lblAlertaText[i].DoubleClick += (s, e) => RidicaAlerta(i);
                alertCard.Controls.Add(lblAlertaText[i]);

                lblAlertaDetaliu[i] = new Label
                {
                    Text      = "",
                    Font      = Theme.CaptionItalic,
                    ForeColor = Theme.TextDim,
                    AutoSize  = false,
                    Location  = new Point(alertCard.Width - 260, ay + 5),
                    Size      = new Size(240, 18),
                    TextAlign = ContentAlignment.MiddleRight,
                    Anchor    = AnchorStyles.Top | AnchorStyles.Right,
                    Cursor    = Cursors.Hand
                };
                lblAlertaDetaliu[i].DoubleClick += (s, e) => RidicaAlerta(i);
                alertCard.Controls.Add(lblAlertaDetaliu[i]);
            }

            y += 220;

            // ── Linia de status ────────────────────────────────────
            var lblStatus = new Label
            {
                Text      = "",
                Font      = Theme.CaptionItalic,
                ForeColor = Theme.TextDim,
                AutoSize  = false,
                Location  = new Point(pad, y),
                Size      = new Size(600, 18),
                Name      = "lblStatus"
            };
            this.Controls.Add(lblStatus);

            // Aplică layout-ul responsiv imediat + centrare pentru chart legend
            this.Load += (s, e) => AplicaLayoutResponsiv();
        }

        private void CreeazaGrafice(Control parent)
        {
            tabGrafice = new TabControl
            {
                Dock      = DockStyle.Fill,
                Font      = Theme.SmallBold,
                Padding   = new Point(14, 6)
            };

            var tabIncasare = new TabPage("Incasare 6 luni")
            { BackColor = Theme.Surface, Padding = new Padding(4) };
            chartIncasareLunar = CreeazaChartIncasareLunar();
            tabIncasare.Controls.Add(chartIncasareLunar);
            tabGrafice.TabPages.Add(tabIncasare);

            var tabTipuri = new TabPage("Tipuri echipamente")
            { BackColor = Theme.Surface, Padding = new Padding(4) };
            chartTipuriEchipamente = CreeazaChartTipuri();
            tabTipuri.Controls.Add(chartTipuriEchipamente);
            tabGrafice.TabPages.Add(tabTipuri);

            var tabOperatori = new TabPage("Top operatori")
            { BackColor = Theme.Surface, Padding = new Padding(4) };
            chartOperatoriSesiuni = CreeazaChartOperatori();
            tabOperatori.Controls.Add(chartOperatoriSesiuni);
            tabGrafice.TabPages.Add(tabOperatori);

            parent.Controls.Add(tabGrafice);
        }

        private Chart CreeazaChartIncasareLunar()
        {
            var chart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Surface,
                AntiAliasing = AntiAliasingStyles.All,
                TextAntiAliasingQuality = TextAntiAliasingQuality.High
            };

            var area = new ChartArea("main") { BackColor = Theme.Surface };
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisX.LabelStyle.Font = fontAxa;
            area.AxisX.LabelStyle.ForeColor = Theme.TextMuted;
            area.AxisX.LineColor = Theme.Border;
            area.AxisX.MajorTickMark.LineColor = Theme.Border;

            area.AxisY.MajorGrid.LineColor = Theme.Border;
            area.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            area.AxisY.LabelStyle.Font = fontAxa;
            area.AxisY.LabelStyle.ForeColor = Theme.Accent;
            area.AxisY.LabelStyle.Format = "#,##0";
            area.AxisY.LineColor = Theme.Border;
            area.AxisY.Title = "EUR";
            area.AxisY.TitleFont = fontAxaBold;
            area.AxisY.TitleForeColor = Theme.Accent;

            area.AxisY2.MajorGrid.Enabled = false;
            area.AxisY2.LabelStyle.Font = fontAxa;
            area.AxisY2.LabelStyle.ForeColor = Theme.Warning;
            area.AxisY2.LineColor = Theme.Border;
            area.AxisY2.Title = "Nr. sesiuni";
            area.AxisY2.TitleFont = fontAxaBold;
            area.AxisY2.TitleForeColor = Theme.Warning;

            chart.ChartAreas.Add(area);

            var sIncasare = new Series("Incasare")
            {
                ChartType = SeriesChartType.Column,
                ChartArea = "main",
                Color = Theme.Accent,
                BorderColor = Theme.AccentHover,
                BorderWidth = 1,
                ToolTip = "#VALX\nIncasare: #VALY{N0} EUR"
            };
            sIncasare["PointWidth"] = "0.55";
            chart.Series.Add(sIncasare);

            var sSesiuni = new Series("Numar sesiuni")
            {
                ChartType = SeriesChartType.Line,
                ChartArea = "main",
                Color = Theme.Warning,
                BorderWidth = 3,
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 9,
                MarkerColor = Theme.Warning,
                MarkerBorderColor = Color.White,
                MarkerBorderWidth = 2,
                YAxisType = AxisType.Secondary,
                ToolTip = "#VALX\nSesiuni: #VALY"
            };
            chart.Series.Add(sSesiuni);

            var legenda = new Legend("leg")
            {
                Docking = Docking.Top,
                Alignment = StringAlignment.Far,
                Font = fontLegend,
                BackColor = Color.Transparent,
                IsTextAutoFit = true,
                LegendStyle = LegendStyle.Row
            };
            chart.Legends.Add(legenda);

            return chart;
        }

        private Chart CreeazaChartTipuri()
        {
            var chart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Surface,
                AntiAliasing = AntiAliasingStyles.All,
                TextAntiAliasingQuality = TextAntiAliasingQuality.High
            };

            var area = new ChartArea("pie") { BackColor = Theme.Surface };
            area.Position.Auto = false;
            area.Position.X = 2;
            area.Position.Y = 4;
            area.Position.Width = 58;
            area.Position.Height = 92;
            chart.ChartAreas.Add(area);

            var s = new Series("Tipuri")
            {
                ChartType = SeriesChartType.Doughnut,
                ChartArea = "pie",
                ToolTip = "#VALX\n#VALY echipamente (#PERCENT{P0})",
                Font = fontAxa,
                Label = "#PERCENT{P0}",
                LabelForeColor = Theme.TextMuted
            };
            s["DoughnutRadius"] = "52";
            s["PieLabelStyle"] = "Outside";
            s["PieLineColor"] = "Black";
            chart.Series.Add(s);

            var legenda = new Legend("leg")
            {
                Docking = Docking.Right,
                Alignment = StringAlignment.Center,
                Font = fontLegend,
                BackColor = Color.Transparent,
                IsTextAutoFit = true
            };
            chart.Legends.Add(legenda);

            return chart;
        }

        private Chart CreeazaChartOperatori()
        {
            var chart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Surface,
                AntiAliasing = AntiAliasingStyles.All,
                TextAntiAliasingQuality = TextAntiAliasingQuality.High
            };

            var area = new ChartArea("bar") { BackColor = Theme.Surface };
            area.AxisX.MajorGrid.LineColor = Theme.Border;
            area.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            area.AxisX.LabelStyle.Font = fontAxa;
            area.AxisX.LabelStyle.ForeColor = Theme.TextMuted;
            area.AxisX.LabelStyle.Format = "#,##0";
            area.AxisX.Title = "Incasare total (EUR)";
            area.AxisX.TitleFont = fontAxaBold;
            area.AxisX.TitleForeColor = Theme.Accent;

            area.AxisY.MajorGrid.Enabled = false;
            area.AxisY.LabelStyle.Font = fontAxa;
            area.AxisY.LabelStyle.ForeColor = Theme.TextMuted;
            area.AxisY.IsReversed = true;
            area.AxisY.Interval = 1;

            chart.ChartAreas.Add(area);

            var s = new Series("Incasare")
            {
                ChartType = SeriesChartType.Bar,
                ChartArea = "bar",
                Color = Theme.Accent,
                BorderColor = Theme.AccentHover,
                BorderWidth = 1,
                ToolTip = "#VALX\nIncasare: #VALY{N0} EUR",
                IsValueShownAsLabel = true,
                LabelFormat = "{0:N0}",
                Font = fontAxa,
                LabelForeColor = Theme.TextMuted
            };
            s["PointWidth"] = "0.6";
            chart.Series.Add(s);

            return chart;
        }

        // ════════════════════════════════════════════════════════════
        //  REÎNCĂRCARE DATE
        // ════════════════════════════════════════════════════════════
        public void Reincarca()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    IncarcaListaAni(conn);
                    RezolvaSelectiaAn(conn);

                    if (_primaIncarcare)
                    {
                        SelecteazaUltimaPerioadaCuActivitate(conn);
                        _primaIncarcare = false;
                    }

                    decimal echipamente = Scalar(conn,
                        "SELECT COUNT(*) FROM Echipamente WHERE status = 'Liber'");
                    decimal sesiuni = Scalar(conn,
                        "SELECT COUNT(*) FROM Sesiuni WHERE status = 'În așteptare'");
                    decimal rezervari = Scalar(conn,
                        "SELECT COUNT(*) FROM Rezervari WHERE status = 'Programata'");

                    decimal incasareTotal = Scalar(conn,
                        "SELECT ISNULL(SUM(incasare), 0) FROM Sesiuni");
                    decimal portofoliu = Scalar(conn,
                        // Valoare portofoliu = Σ tarif/oră al echipamentelor libere
                        // (venitul orar potențial al stațiilor disponibile).
                        "SELECT ISNULL(SUM(tarif_ora), 0) FROM Echipamente WHERE status = 'Liber'");

                    decimal totalEchipamente = Scalar(conn, "SELECT COUNT(*) FROM Echipamente");
                    decimal totalSesiuni = Scalar(conn, "SELECT COUNT(*) FROM Sesiuni");

                    kpiEchipamente.Value = echipamente.ToString("N0");
                    kpiEchipamente.Subtitle = $"din {totalEchipamente:N0} înregistrate";

                    kpiSesiuni.Value = sesiuni.ToString("N0");
                    kpiSesiuni.Subtitle = $"din {totalSesiuni:N0} sesiuni";

                    kpiRezervari.Value = rezervari.ToString("N0");
                    kpiRezervari.Subtitle = "cu status 'Programata'";

                    IncarcaIncasarePerioada(conn);

                    kpiIncasareTotal.Value = incasareTotal.ToString("N0");
                    kpiIncasareTotal.Unit = "EUR";
                    kpiIncasareTotal.Subtitle = "toată perioada";

                    kpiPortofoliu.Value = portofoliu.ToString("N0");
                    kpiPortofoliu.Unit = "EUR";
                    kpiPortofoliu.Subtitle = "Σ tarif/oră · echipamente libere";

                    IncarcaTopOperatori(conn);
                    IncarcaIncasareLunar(conn);
                    IncarcaTipuriEchipamente(conn);
                    IncarcaTopOperatoriGrafic(conn);
                    IncarcaAlerte(conn);

                    var lblStatus = this.Controls.Find("lblStatus", true);
                    if (lblStatus.Length > 0)
                    {
                        var lbl = (Label)lblStatus[0];
                        lbl.ForeColor = Theme.TextDim;
                        lbl.Text = $"Actualizat la {DateTime.Now:dd.MM.yyyy HH:mm}";
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PanouDashboard.Reincarca");
                var lblStatus = this.Controls.Find("lblStatus", true);
                if (lblStatus.Length > 0)
                {
                    var lbl = (Label)lblStatus[0];
                    lbl.ForeColor = Theme.Danger;
                    lbl.Text = "Indicatori indisponibili: " + ex.Message;
                }
            }
        }

        // ════════════════════════════════════════════════════════════
        //  ÎNCĂRCARE ASINCRONĂ CU INDICATOR (§4.4)
        //  Afișează un indicator înainte de a încărca datele, astfel încât
        //  fereastra să nu pară „blocată”. Reincarca() atinge controale UI
        //  (carduri KPI, chart-uri, combo-uri, etichete), deci rulează pe
        //  thread-ul UI — amânarea de mai jos doar permite message loop-ului
        //  să picteze indicatorul ÎNAINTE de interogările SQL (pe SQL local
        //  acestea sunt sub 100 ms, iar pe rețea eroarea rămâne tratată).
        // ════════════════════════════════════════════════════════════
        private Panel _overlayIncarcare;

        private async void IncarcaAsincron()
        {
            ArataIncarcare(true);
            await Task.Delay(80);   // lasă message loop-ul să picteze indicatorul
            try
            {
                Reincarca();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PanouDashboard.IncarcaAsincron");
                MessageBox.Show(
                    "Nu s-au putut încărca datele tabloului de bord.\n\n" + ex.Message,
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ArataIncarcare(false);
            }
        }

        /// <summary>
        /// Afișează/ascunde un indicator central de încărcare (culori din
        /// MaterialTheme). Overlay-ul este eliberat complet la final.
        /// </summary>
        private void ArataIncarcare(bool activ)
        {
            if (activ)
            {
                if (_overlayIncarcare != null) return;

                var overlay = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = MaterialTheme.Background
                };

                var tlp = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 2,
                    BackColor = MaterialTheme.Background
                };
                tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
                tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

                var progress = new ProgressBar
                {
                    Style = ProgressBarStyle.Marquee,
                    MarqueeAnimationSpeed = 30,
                    Width = MaterialTheme.Scale(240),
                    Height = MaterialTheme.Scale(6),
                    Anchor = AnchorStyles.None
                };

                var lbl = new Label
                {
                    Text = "Se încarcă datele tabloului de bord…",
                    Font = MaterialTheme.FontBody,
                    ForeColor = MaterialTheme.TextSecondary,
                    AutoSize = true,
                    Anchor = AnchorStyles.None,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                tlp.Controls.Add(progress, 0, 0);
                tlp.Controls.Add(lbl, 0, 1);
                overlay.Controls.Add(tlp);

                this.Controls.Add(overlay);
                overlay.BringToFront();
                _overlayIncarcare = overlay;
            }
            else
            {
                if (_overlayIncarcare == null) return;
                try
                {
                    this.Controls.Remove(_overlayIncarcare);
                    _overlayIncarcare.Dispose();
                }
                catch (Exception ex)
                {
                    AppLogger.Warn("Eliminare indicator încărcare: " + ex.Message,
                        "PanouDashboard.ArataIncarcare");
                }
                _overlayIncarcare = null;
            }
        }

        private void IncarcaIncasareLunar(SqlConnection conn)
        {
            chartIncasareLunar.Series["Incasare"].Points.Clear();
            chartIncasareLunar.Series["Numar sesiuni"].Points.Clear();
            chartIncasareLunar.Titles.Clear();

            int? an = AnSelectat();
            int? luna = LunaSelectata();
            DateTime? referinta = null;

            if (luna.HasValue && an.HasValue)
                referinta = new DateTime(an.Value, luna.Value, 1);
            else
            {
                using (var cmd = new SqlCommand(@"
                        SELECT MAX(data_sesiune) FROM Sesiuni
                        WHERE (@an IS NULL OR YEAR(data_sesiune) = @an)", conn))
                {
                    cmd.Parameters.Add("@an", SqlDbType.Int).Value =
                        an.HasValue ? (object)an.Value : DBNull.Value;
                    object rezultat = cmd.ExecuteScalar();
                    if (rezultat != null && rezultat != DBNull.Value)
                    {
                        DateTime ultima = Convert.ToDateTime(rezultat);
                        referinta = new DateTime(ultima.Year, ultima.Month, 1);
                    }
                }
            }

            if (referinta == null)
            {
                chartIncasareLunar.Titles.Add(
                    new Title("Nu există sesiuni în perioada afișată.",
                              Docking.Top, fontTitluGrafic, Theme.TextMuted));
                return;
            }

            DateTime start = referinta.Value.AddMonths(-5);
            decimal total = 0, peak = 0;

            for (int i = 0; i < 6; i++)
            {
                DateTime lunaGrafic = start.AddMonths(i);
                decimal incasare = IncasareLunaDecimal(conn, lunaGrafic);
                int nrSesiuni = NrSesiuniLuna(conn, lunaGrafic);

                string eticheta = lunaGrafic.ToString("MMM yy", CulturaRo);
                int idxC = chartIncasareLunar.Series["Incasare"].Points.AddXY(eticheta, incasare);
                chartIncasareLunar.Series["Numar sesiuni"].Points.AddXY(eticheta, nrSesiuni);

                total += incasare;
                if (incasare > peak) peak = incasare;

                if (i == 5)
                {
                    var pC = chartIncasareLunar.Series["Incasare"].Points[idxC];
                    pC.Color = Theme.Success;
                    pC.BorderColor = Color.FromArgb(0, 100, 50);
                }
            }

            decimal medie = total / 6m;
            chartIncasareLunar.Titles.Add(new Title(
                // FIX_21: lunile în română (ro-RO), nu în limba OS-ului.
                $"Încasare lunară ({start.ToString("MMM yyyy", CulturaRo)} → {referinta.Value.ToString("MMM yyyy", CulturaRo)})   |   " +
                $"Total: {total:N0} EUR   |   Medie: {medie:N0} EUR   |   Peak: {peak:N0} EUR",
                Docking.Top, fontTitluGrafic, Theme.Accent));
        }

        private static decimal IncasareLunaDecimal(SqlConnection conn, DateTime luna)
        {
            using (var cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(incasare), 0) FROM Sesiuni
                    WHERE YEAR(data_sesiune) = @an AND MONTH(data_sesiune) = @luna", conn))
            {
                cmd.Parameters.Add("@an", SqlDbType.Int).Value = luna.Year;
                cmd.Parameters.Add("@luna", SqlDbType.Int).Value = luna.Month;
                return Convert.ToDecimal(cmd.ExecuteScalar());
            }
        }

        private static int NrSesiuniLuna(SqlConnection conn, DateTime luna)
        {
            using (var cmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM Sesiuni
                    WHERE YEAR(data_sesiune) = @an AND MONTH(data_sesiune) = @luna", conn))
            {
                cmd.Parameters.Add("@an", SqlDbType.Int).Value = luna.Year;
                cmd.Parameters.Add("@luna", SqlDbType.Int).Value = luna.Month;
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private void IncarcaTipuriEchipamente(SqlConnection conn)
        {
            var serie = chartTipuriEchipamente.Series["Tipuri"];
            serie.Points.Clear();
            chartTipuriEchipamente.Titles.Clear();

            var culori = new[]
            {
                Theme.Accent, Color.FromArgb(0x6D, 0x8B, 0xFF),
                Color.FromArgb(0xA7, 0xB6, 0xFF), Color.FromArgb(0xE0, 0xE6, 0xFF),
                Theme.Success, Theme.Warning
            };

            int totalBucati = 0, i = 0;
            using (var cmd = new SqlCommand(@"
                    SELECT tip_echipament, COUNT(*) AS nr FROM Echipamente
                    GROUP BY tip_echipament ORDER BY nr DESC", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string tip = reader.GetString(0);
                    int nr = reader.GetInt32(1);
                    int idx = serie.Points.AddXY(tip, nr);
                    serie.Points[idx].Color = culori[i % culori.Length];
                    serie.Points[idx].BorderColor = Color.White;
                    serie.Points[idx].BorderWidth = 2;
                    serie.Points[idx].LegendText = $"{tip} ({nr})";
                    totalBucati += nr;
                    i++;
                }
            }

            if (totalBucati == 0)
            {
                chartTipuriEchipamente.Titles.Add(new Title(
                    "Nu există echipamente înregistrate.",
                    Docking.Top, fontTitluGrafic, Theme.TextMuted));
                return;
            }

            chartTipuriEchipamente.Titles.Add(new Title(
                $"Distribuția portofoliului   |   Total: {totalBucati} echipamente",
                Docking.Top, fontTitluGrafic, Theme.Accent));
        }

        private void IncarcaTopOperatoriGrafic(SqlConnection conn)
        {
            var serie = chartOperatoriSesiuni.Series["Incasare"];
            serie.Points.Clear();
            chartOperatoriSesiuni.Titles.Clear();

            using (var cmd = new SqlCommand(@"
                    SELECT TOP 5 a.nume + ' ' + a.prenume AS nume_operator,
                           ISNULL(SUM(t.incasare), 0) AS total,
                           COUNT(t.id_sesiune) AS nr
                    FROM Operatori a LEFT JOIN Sesiuni t ON a.id_operator = t.id_operator
                    GROUP BY a.id_operator, a.nume, a.prenume
                    ORDER BY total DESC, nume_operator", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string numeOperator = reader.GetString(0);
                    decimal total = Convert.ToDecimal(reader.GetValue(1));
                    int nr = Convert.ToInt32(reader.GetValue(2));
                    serie.Points.AddXY($"{numeOperator}  ({nr} ses.)", total);
                }
            }

            if (serie.Points.Count == 0)
            {
                chartOperatoriSesiuni.Titles.Add(new Title(
                    "Nu există operatori înregistrați.",
                    Docking.Top, fontTitluGrafic, Theme.TextMuted));
                return;
            }

            chartOperatoriSesiuni.Titles.Add(new Title(
                "Top 5 operatori după încasarea totală",
                Docking.Top, fontTitluGrafic, Theme.Accent));
        }

        private void IncarcaTopOperatori(SqlConnection conn)
        {
            const string query = @"
                SELECT TOP 3 a.nume + ' ' + a.prenume AS nume_operator,
                       ISNULL(SUM(t.incasare), 0) AS total_incasare,
                       COUNT(t.id_sesiune) AS numar_sesiuni
                FROM Operatori a LEFT JOIN Sesiuni t ON a.id_operator = t.id_operator
                GROUP BY a.id_operator, a.nume, a.prenume
                ORDER BY total_incasare DESC, nume_operator";

            using (var cmd = new SqlCommand(query, conn))
            using (var reader = cmd.ExecuteReader())
            {
                int index = 0;
                while (reader.Read() && index < 3)
                {
                    lblOperatorNume[index].Text = reader["nume_operator"].ToString();
                    lblOperatorIncasare[index].Text =
                        $"{Convert.ToDecimal(reader["total_incasare"]):N0} EUR  " +
                        $"({Convert.ToInt32(reader["numar_sesiuni"])} ses.)";
                    index++;
                }
                for (; index < 3; index++)
                {
                    lblOperatorNume[index].Text = "-";
                    lblOperatorIncasare[index].Text = "-";
                }
            }
        }

        private void IncarcaListaAni(SqlConnection conn)
        {
            _incarcSelectii = true;
            try
            {
                string selectieVeche = cmbAn.SelectedItem as string;
                var ani = new List<int>();

                using (var cmd = new SqlCommand(
                    "SELECT DISTINCT YEAR(data_sesiune) FROM Sesiuni ORDER BY 1 DESC", conn))
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read()) ani.Add(reader.GetInt32(0));

                cmbAn.Items.Clear();
                cmbAn.Items.Add(ToateAnii);
                foreach (int an in ani) cmbAn.Items.Add(an.ToString());

                int index = selectieVeche == null ? -1 : cmbAn.Items.IndexOf(selectieVeche);
                cmbAn.SelectedIndex = index >= 0 ? index : 0;
            }
            finally { _incarcSelectii = false; }
        }

        private void RezolvaSelectiaAn(SqlConnection conn)
        {
            int? luna = LunaSelectata();
            if (!luna.HasValue || AnSelectat().HasValue) return;

            using (var cmd = new SqlCommand(@"
                    SELECT MAX(YEAR(data_sesiune)) FROM Sesiuni
                    WHERE MONTH(data_sesiune) = @luna", conn))
            {
                cmd.Parameters.Add("@luna", SqlDbType.Int).Value = luna.Value;
                object rezultat = cmd.ExecuteScalar();
                if (rezultat == null || rezultat == DBNull.Value) return;

                int index = cmbAn.Items.IndexOf(rezultat.ToString());
                if (index < 0) return;

                _incarcSelectii = true;
                cmbAn.SelectedIndex = index;
                _incarcSelectii = false;
            }
        }

        private void SelecteazaUltimaPerioadaCuActivitate(SqlConnection conn)
        {
            object rezultat;
            using (var cmd = new SqlCommand("SELECT MAX(data_sesiune) FROM Sesiuni", conn))
                rezultat = cmd.ExecuteScalar();

            if (rezultat == null || rezultat == DBNull.Value) return;

            DateTime ultima = Convert.ToDateTime(rezultat);
            _incarcSelectii = true;

            int indexAn = cmbAn.Items.IndexOf(ultima.Year.ToString());
            if (indexAn >= 0) cmbAn.SelectedIndex = indexAn;
            cmbLuna.SelectedIndex = ultima.Month;
            _incarcSelectii = false;
        }

        private void SchimbaPerioada()
        {
            if (_incarcSelectii) return;
            Reincarca();
        }

        private int? AnSelectat()
        {
            string text = cmbAn.SelectedItem as string;
            int an;
            return (text != null && int.TryParse(text, out an)) ? (int?)an : null;
        }

        private int? LunaSelectata()
            => cmbLuna.SelectedIndex > 0 ? (int?)cmbLuna.SelectedIndex : null;

        private string DescriePerioada()
        {
            int? an = AnSelectat();
            int? luna = LunaSelectata();
            if (luna.HasValue && an.HasValue)
                return new DateTime(an.Value, luna.Value, 1).ToString("MMMM yyyy", CulturaRo);
            if (luna.HasValue)
                return new DateTime(2000, luna.Value, 1).ToString("MMMM", CulturaRo) + " (toti anii)";
            if (an.HasValue) return $"anul {an.Value}";
            return "toată perioada";
        }

        private void IncarcaIncasarePerioada(SqlConnection conn)
        {
            int? an = AnSelectat();
            int? luna = LunaSelectata();

            using (var cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(incasare), 0) AS total, COUNT(*) AS numar
                    FROM Sesiuni
                    WHERE (@an IS NULL OR YEAR(data_sesiune) = @an)
                      AND (@luna IS NULL OR MONTH(data_sesiune) = @luna)", conn))
            {
                cmd.Parameters.Add("@an", SqlDbType.Int).Value =
                    an.HasValue ? (object)an.Value : DBNull.Value;
                cmd.Parameters.Add("@luna", SqlDbType.Int).Value =
                    luna.HasValue ? (object)luna.Value : DBNull.Value;

                using (var reader = cmd.ExecuteReader())
                {
                    reader.Read();
                    decimal total = reader["total"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["total"]);
                    int numar = Convert.ToInt32(reader["numar"]);

                    kpiIncasareLuna.Value = total.ToString("N0");
                    kpiIncasareLuna.Unit = "EUR";
                    kpiIncasareLuna.Subtitle = $"{DescriePerioada()} ({numar:N0} sesiuni)";
                }
            }
        }

        private void IncarcaAlerte(SqlConnection conn)
        {
            int nrEchipamente = 0, zileEchipament = 0;
            using (var cmd = new SqlCommand(@"
                    SELECT COUNT(*), ISNULL(MAX(DATEDIFF(DAY, data_adaugare, CAST(GETDATE() AS DATE))), 0)
                    FROM Echipamente WHERE status = 'Liber'
                      AND DATEDIFF(DAY, data_adaugare, CAST(GETDATE() AS DATE)) > @zile", conn))
            {
                cmd.Parameters.Add("@zile", SqlDbType.Int).Value = ZileEchipamentStagnanta;
                using (var reader = cmd.ExecuteReader())
                    if (reader.Read())
                    {
                        nrEchipamente = Convert.ToInt32(reader[0]);
                        zileEchipament = Convert.ToInt32(reader[1]);
                    }
            }

            SeteazaAlerta(0, nrEchipamente,
                $"Echipamente libere de peste {ZileEchipamentStagnanta} de zile",
                nrEchipamente > 0 ? $"cea mai veche: {zileEchipament} zile" : "toate în termen");

            int nrRezervari = 0;
            DateTime? urmatoareaRezervare = null;
            using (var cmd = new SqlCommand(@"
                    SELECT COUNT(*), MIN(data_rezervare) FROM Rezervari
                    WHERE status = 'Programata' AND data_rezervare >= GETDATE()
                      AND data_rezervare <= DATEADD(DAY, @zile, GETDATE())", conn))
            {
                cmd.Parameters.Add("@zile", SqlDbType.Int).Value = ZileRezervariViitoare;
                using (var reader = cmd.ExecuteReader())
                    if (reader.Read())
                    {
                        nrRezervari = Convert.ToInt32(reader[0]);
                        if (reader[1] != DBNull.Value)
                            urmatoareaRezervare = Convert.ToDateTime(reader[1]);
                    }
            }

            SeteazaAlerta(1, nrRezervari,
                $"Rezervări programate în următoarele {ZileRezervariViitoare} zile",
                nrRezervari > 0 && urmatoareaRezervare.HasValue
                    ? $"următoarea: {urmatoareaRezervare.Value:dd.MM.yyyy HH:mm}"
                    : "nicio programare apropiată");

            int nrSesiuni = 0, zileSesiune = 0;
            using (var cmd = new SqlCommand(@"
                    SELECT COUNT(*), ISNULL(MAX(DATEDIFF(DAY, data_sesiune, CAST(GETDATE() AS DATE))), 0)
                    FROM Sesiuni WHERE status = 'În așteptare'
                      AND DATEDIFF(DAY, data_sesiune, CAST(GETDATE() AS DATE)) > @zile", conn))
            {
                cmd.Parameters.Add("@zile", SqlDbType.Int).Value = ZileSesiuneInAsteptare;
                using (var reader = cmd.ExecuteReader())
                    if (reader.Read())
                    {
                        nrSesiuni = Convert.ToInt32(reader[0]);
                        zileSesiune = Convert.ToInt32(reader[1]);
                    }
            }

            SeteazaAlerta(2, nrSesiuni,
                $"Sesiuni în așteptare de peste {ZileSesiuneInAsteptare} de zile",
                nrSesiuni > 0 ? $"cea mai veche: {zileSesiune} zile" : "toate în termen");

            int totalAlerte = 0;
            for (int i = 0; i < 3; i++) totalAlerte += Convert.ToInt32(lblAlertaBadge[i].Text);

            lblRezumatAlerte.Text = totalAlerte > 0 ? $"{totalAlerte} ALERTE ACTIVE" : "FĂRĂ ALERTE";
            lblRezumatAlerte.ForeColor = totalAlerte > 0 ? Theme.Warning : Theme.Success;
            // Repoziționează ca să nu se suprapună peste butonul "Arată tot"
            if (_alertCard != null && _btnArataTot != null)
            {
                // Asigură AutoSize calculat
                using (var g = this.CreateGraphics())
                {
                    var sz = TextRenderer.MeasureText(lblRezumatAlerte.Text, lblRezumatAlerte.Font);
                    lblRezumatAlerte.Size = new Size(sz.Width + 6, 20);
                }
                lblRezumatAlerte.Location = new Point(
                    _alertCard.Width - _btnArataTot.Width - lblRezumatAlerte.Width - 16, 16);
            }
        }

        private void SeteazaAlerta(int index, int numar, string descriere, string detaliu)
        {
            bool activa = numar > 0;
            numarAlerta[index] = numar;

            lblAlertaBadge[index].Text = numar.ToString();
            lblAlertaBadge[index].BackColor = activa ? Theme.Warning : Theme.Success;

            lblAlertaText[index].Text = descriere;
            lblAlertaText[index].Font = activa ? fontAlertaActiva : fontAlertaInactiva;
            lblAlertaText[index].ForeColor = activa ? Color.FromArgb(180, 80, 0) : Theme.TextMuted;

            lblAlertaDetaliu[index].Text = detaliu;
        }

        private void RidicaAlerta(int index)
        {
            bool arataTot = numarAlerta[index] <= 0;
            AlertaDoubleClick?.Invoke(index, arataTot);
        }

        private static decimal Scalar(SqlConnection conn, string query)
        {
            using (var cmd = new SqlCommand(query, conn))
            {
                object rezultat = cmd.ExecuteScalar();
                if (rezultat == null || rezultat == DBNull.Value) return 0;
                return Convert.ToDecimal(rezultat);
            }
        }
    }
}
