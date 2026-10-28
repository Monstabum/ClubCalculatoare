using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using AppClubCalculatoare.UI;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Tabloul de bord cu indicatori-cheie (KPI).
    ///
    /// Design: ierarhie pe suprafețe și tipografie, un singur accent cyan.
    /// KPI-urile stau într-un singur container cu separatoare de 1px,
    /// graficele folosesc doar accent + un neutru, alertele sunt o listă
    /// compactă cu punct de 6px (nu badge-uri colorate).
    /// </summary>
    internal class PanouDashboard : UserControl
    {
        // -- KPI cards ----------------------------------------------
        private KpiCard kpiEchipamente, kpiSesiuni, kpiRezervari;
        private KpiCard kpiIncasareLuna, kpiIncasareTotal, kpiPortofoliu;
        private TableLayoutPanel _kpiGrid;

        // -- Top 3 operatori -------------------------------------------
        private readonly Label[] lblOperatorNume = new Label[3];
        private readonly Label[] lblOperatorIncasare = new Label[3];
        private readonly Label[] lblOperatorRank = new Label[3];
        private RoundLabel _punctLoc1;

        // -- Layout refs (pentru responsive) ------------------------
        private Panel _leftCol, _rightCol, _alertCard;
        private Panel _periodPanel;
        private LinkLabel _lnkArataTot;
        private Label _lblTitlu, _lblSub, _lblTitluIncasare, _lblRezumatIncasare;
        private int _yKpi, _yGrafice, _yAlerte;
        private const int _pad = Design.Sp4;
        private const int _gap = Design.Sp4;
        private const int _kpiH = 240;
        private const int _grafH = 300;
        private const int _alerteH = 200;

        // -- Selector perioada --------------------------------------
        private ComboBox cmbAn, cmbLuna;
        private const string ToateAnii = "Toate anii";
        private const string ToateLunile = "Toate lunile";
        private bool _incarcSelectii;

        // -- Alerte -------------------------------------------------
        private Label lblRezumatAlerte;
        private readonly RoundLabel[] punctAlerta = new RoundLabel[3];
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
        // FIX_21: lunile se afi?eaza in romana indiferent de limba Windows (pe un OS rus apareau nume de luni chirilice in interfa?a romana).
        private static readonly CultureInfo CulturaRo = new CultureInfo("ro-RO");

        public event Action<int, bool> AlertaDoubleClick;

        // -- Grafice ------------------------------------------------
        private Chart chartIncasareLunar;
        private Chart chartTipuriEchipamente;
        private Chart chartOperatoriSesiuni;

        // -- Fonturi (scara unică din Design) -----------------------
        private readonly Font _fAxa = Design.FontSmall;
        private readonly Font _fAxaBold = Design.FontSmallBold;
        private readonly Font _fLegenda = Design.FontSmall;
        private readonly Font _fTitluGrafic = Design.FontSmallBold;
        private readonly Font _fMesaj = Design.FontCaptionItalic;
        private readonly Font _fAlerta = Design.FontSubtitle;
        private readonly Font _fAlertaDetaliu = Design.FontMono;
        private readonly Font _fRank = Design.SemiBold(11f);
        private readonly Font _fOperNume = Design.SemiBold(10f);
        private readonly Font _fOperSuma = Design.FontSubtitle;
        private readonly Font _fSectiune = Design.FontOverline;

        // Paleta graficelor: accent + un neutru (fără curcubeu).
        private static readonly Color[] CuloriSerii =
        {
            Design.Accent, Design.Warning, Design.TextMuted,
            Design.AccentHover, Design.AccentMuted, Design.TextDim
        };

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _ctsIncarcare?.Cancel();
                _ctsIncarcare?.Dispose();
                _ctsIncarcare = null;

                _fAxa.Dispose(); _fAxaBold.Dispose(); _fLegenda.Dispose();
                _fTitluGrafic.Dispose(); _fMesaj.Dispose();
                _fAlerta.Dispose(); _fAlertaDetaliu.Dispose();
                _fRank.Dispose(); _fOperNume.Dispose(); _fOperSuma.Dispose();
                _fSectiune.Dispose();
            }
            base.Dispose(disposing);
        }

        public PanouDashboard(string connString)
        {
            // Connection string-ul este global în SqlDb; parametrul rămâne
            // pentru compatibilitatea apelurilor existente.
            _ = connString;
            // Un singur scrollbar, la nivelul panoului de conținut: dashboard-ul
            // își ia înălțimea completă (vezi AplicaLayoutResponsiv), altfel
            // apăreau două bare verticale + una orizontală peste footer.
            this.AutoScroll = false;
            this.BackColor = Design.Bg;
            this.Dock = DockStyle.Top;
            ConstruiesteInterfata();
            this.Resize += (s, e) => AplicaLayoutResponsiv();
            _ = IncarcaAsincronAsync(autoPerioada: true);
        }

        /// <summary>Înălțimea necesară întregului conținut (KPI + grafice + alerte + status).</summary>
        private int InaltimeContinut => _yAlerte + _alerteH + Design.Sp3 + 18 + Design.Sp4;

        private void AplicaLayoutResponsiv()
        {
            if (_kpiGrid == null || _leftCol == null || _rightCol == null || _alertCard == null) return;
            if (this.Width <= 0) return;
            this.SuspendLayout();

            int availW = this.ClientSize.Width - _pad * 2;
            if (availW < 300) availW = 300;

            // ── KPI: un singur container 3×2, separatoare de 1px ──
            _kpiGrid.Location = new Point(_pad, _yKpi);
            _kpiGrid.Size = new Size(availW, _kpiH);

            // ── Grafice (stânga) + Top 3 (dreapta) ────────────────
            int yGraf = _yGrafice;
            int leftW = (int)Math.Round(availW * 0.62);
            int rightW = availW - leftW - _gap;
            if (rightW < 260) { leftW = availW - 260 - _gap; rightW = 260; }
            if (leftW < 380)  { leftW = 380; rightW = availW - leftW - _gap; }

            _leftCol.Location = new Point(_pad, yGraf);
            _leftCol.Size = new Size(leftW, _grafH);
            _rightCol.Location = new Point(_pad + leftW + _gap, yGraf);
            _rightCol.Size = new Size(rightW, _grafH);

            // ── Alerte — pe toata la?imea ─────────────────────────
            _alertCard.Location = new Point(_pad, _yAlerte);
            _alertCard.Size = new Size(availW, _alerteH);

            // ── Elementele din interiorul alertei ─────────────────
            if (_lnkArataTot != null && lblRezumatAlerte != null)
            {
                _lnkArataTot.Location = new Point(_alertCard.Width - _lnkArataTot.Width - Design.Sp4, 16);
                lblRezumatAlerte.Location = new Point(
                    _alertCard.Width - _lnkArataTot.Width - lblRezumatAlerte.Width
                        - Design.Sp4 - Design.Sp3, 17);
            }
            for (int i = 0; i < 3; i++)
            {
                if (lblAlertaDetaliu[i] != null)
                {
                    lblAlertaDetaliu[i].Location = new Point(_alertCard.Width - 240, 64 + i * 40);
                    lblAlertaDetaliu[i].Size = new Size(224, 18);
                }
                if (lblAlertaText[i] != null)
                {
                    int txtW = _alertCard.Width - 60 - 250 - 16;
                    if (txtW < 120) txtW = 120;
                    lblAlertaText[i].Size = new Size(txtW, 20);
                }
            }

            // ── Period panel — sa nu iasa din ecran ───────────────
            if (_periodPanel != null)
            {
                int maxX = this.ClientSize.Width - _periodPanel.Width - _pad;
                if (maxX < _pad) maxX = _pad;
                _periodPanel.Location = new Point(maxX, _periodPanel.Location.Y);
            }

            // Ajusteaza inaltimea totala: parintele (contentPanel) este singurul
            // care deruleaza, deci dashboard-ul trebuie sa fie complet inalt.
            var lblStatusCtrl = this.Controls.Find("lblStatus", true);
            if (lblStatusCtrl.Length > 0)
                lblStatusCtrl[0].Location = new Point(_pad, _yAlerte + _alerteH + Design.Sp3);

            int totalH = InaltimeContinut;
            if (this.Height != totalH) this.Height = totalH;

            this.ResumeLayout(false);
            this.Invalidate();
        }

        // ============================================================
        //  CONSTRUIRE INTERFA?A
        // ============================================================
        private void ConstruiesteInterfata()
        {
            int pad = _pad;

            // -- Titlu + subtitlu -----------------------------------
            _lblTitlu = new Label
            {
                Text      = "Bine ai venit!",
                Font      = Design.FontDisplay,
                ForeColor = Design.Text,
                BackColor = Color.Transparent,
                AutoSize  = true,
                Location  = new Point(pad, 0)
            };
            this.Controls.Add(_lblTitlu);

            _lblSub = new Label
            {
                Text      = "Situația clubului · actualizată automat",
                Font      = Design.FontSubtitle,
                ForeColor = Design.TextMuted,
                BackColor = Color.Transparent,
                // AutoSize-ul etichetei nu ține cont de scalarea DPI a fontului,
                // așa că rândul ieșea tăiat pe 125% — dimensiunea se măsoară aici.
                AutoSize  = false,
                Location  = new Point(pad, _lblTitlu.Bottom + Design.Sp1)
            };
            var marimeSub = TextRenderer.MeasureText(_lblSub.Text, _lblSub.Font);
            _lblSub.Size = new Size(marimeSub.Width + Design.Sp1,
                Math.Max(marimeSub.Height, _lblSub.Font.Height) + Design.Sp2);
            this.Controls.Add(_lblSub);

            ConstruiesteSelectorPerioada();

            _yKpi = Math.Max(_lblSub.Bottom + Design.Sp4, Design.Sp8 * 2);
            ConstruiesteGrilaKpi();

            _yGrafice = _yKpi + _kpiH + _gap;
            ConstruiesteGrafice();
            ConstruiesteTopOperatori();

            _yAlerte = _yGrafice + _grafH + _gap;
            ConstruiesteAlerte();

            // -- Linia de status ------------------------------------
            var lblStatus = new Label
            {
                Text      = "",
                Font      = Design.FontCaptionItalic,
                ForeColor = Design.TextDim,
                BackColor = Color.Transparent,
                AutoSize  = false,
                Location  = new Point(pad, _yAlerte + _alerteH + Design.Sp3),
                Size      = new Size(600, 18),
                Name      = "lblStatus"
            };
            this.Controls.Add(lblStatus);

            // Aplica layout-ul responsiv imediat + centrare pentru chart legend
            this.Load += (s, e) => AplicaLayoutResponsiv();
        }

        /// <summary>Selectorul „An / Lună" + butonul de reîncărcare (dreapta sus).</summary>
        private void ConstruiesteSelectorPerioada()
        {
            _periodPanel = new Panel
            {
                Location  = new Point(Design.Sp4, Design.Sp1),
                Size      = new Size(430, 40),
                Anchor    = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.Transparent
            };
            this.Controls.Add(_periodPanel);

            var lblAn = new Label
            {
                Text      = "An:",
                Font      = Design.FontSmallBold,
                ForeColor = Design.TextMuted,
                BackColor = Color.Transparent,
                AutoSize  = true,
                Location  = new Point(0, 12)
            };
            _periodPanel.Controls.Add(lblAn);

            cmbAn = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = Design.FontSubtitle
            };
            cmbAn.SelectedIndexChanged += (s, e) => SchimbaPerioada();
            MaterialUIHelper.StyleInput(cmbAn);

            // Pozițiile se calculează din lățimea reală a etichetelor (la 125%
            // DPI ele cresc, iar pozițiile fixe împingeau butonul peste combo).
            int xAn = TextRenderer.MeasureText(lblAn.Text, lblAn.Font).Width + Design.Sp1;
            var cadruAn = IncadreazaCombo(cmbAn, new Point(xAn, 6), new Size(78, 28));
            _periodPanel.Controls.Add(cadruAn);

            var lblLuna = new Label
            {
                Text      = "Luna:",
                Font      = Design.FontSmallBold,
                ForeColor = Design.TextMuted,
                BackColor = Color.Transparent,
                AutoSize  = true,
                Location  = new Point(cadruAn.Right + Design.Sp3, 12)
            };
            _periodPanel.Controls.Add(lblLuna);

            cmbLuna = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = Design.FontSubtitle
            };
            cmbLuna.Items.Add(ToateLunile);
            for (int i = 1; i <= 12; i++)
                cmbLuna.Items.Add($"{i:00} {new DateTime(2000, i, 1).ToString("MMM", CulturaRo)}");
            cmbLuna.SelectedIndex = 0;
            cmbLuna.SelectedIndexChanged += (s, e) => SchimbaPerioada();
            MaterialUIHelper.StyleInput(cmbLuna);

            var cadruLuna = IncadreazaCombo(cmbLuna,
                new Point(lblLuna.Right + Design.Sp1, 6), new Size(118, 28));
            _periodPanel.Controls.Add(cadruLuna);

            var btnRefresh = new ModernButton
            {
                // FIX BUG-12: era „Reincarca", fără diacritice.
                Text     = "Reîncarcă",
                Variant  = ButtonVariant.Primary,
                Location = new Point(cadruLuna.Right + Design.Sp3, (40 - Design.ButtonH) / 2),
                Size     = new Size(140, Design.ButtonH)
            };
            btnRefresh.Click += (s, e) => Reincarca();
            _periodPanel.Controls.Add(btnRefresh);

            _periodPanel.Width = btnRefresh.Right;
        }

        /// <summary>
        /// Încadrează un ComboBox într-un panou care desenează conturul
        /// <see cref="Design.Border"/> — ComboBox-ul nativ nu acceptă o
        /// culoare de chenar proprie în tema închisă.
        /// </summary>
        private static Panel IncadreazaCombo(ComboBox cmb, Point locatie, Size marime)
        {
            var cadru = new Panel
            {
                Location  = locatie,
                Size      = marime,
                BackColor = Design.SurfaceRaised,
                Padding   = new Padding(2)
            };
            cadru.Paint += (s, e) =>
            {
                using (var pen = new Pen(Design.Border, 1f))
                    e.Graphics.DrawRectangle(pen, 0, 0, cadru.Width - 1, cadru.Height - 1);
            };
            cmb.Dock = DockStyle.Fill;
            cmb.FlatStyle = FlatStyle.Flat;
            cmb.BackColor = Design.SurfaceRaised;
            cmb.ForeColor = Design.Text;
            cadru.Controls.Add(cmb);
            return cadru;
        }

        /// <summary>
        /// Grila de KPI: un singur TableLayoutPanel cu fundal
        /// <see cref="Design.Border"/>; cardurile sunt separate de „gap"-ul
        /// de 1px al containerului, nu de chenare proprii.
        /// </summary>
        private void ConstruiesteGrilaKpi()
        {
            _kpiGrid = new TableLayoutPanel
            {
                ColumnCount = 3,
                RowCount    = 2,
                BackColor   = Design.Border,
                Padding     = new Padding(1),
                Margin      = new Padding(0),
                GrowStyle   = TableLayoutPanelGrowStyle.FixedSize,
                Location    = new Point(_pad, _yKpi),
                Size        = new Size(600, _kpiH)
            };
            for (int c = 0; c < 3; c++)
                _kpiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3f));
            for (int r = 0; r < 2; r++)
                _kpiGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            kpiEchipamente = new KpiCard { Title = "Echipamente libere" };
            kpiSesiuni     = new KpiCard { Title = "Sesiuni în așteptare" };
            kpiRezervari   = new KpiCard { Title = "Rezervări programate" };
            kpiIncasareLuna  = new KpiCard { Title = "Încasare pe perioadă" };
            kpiIncasareTotal = new KpiCard { Title = "Încasare totală" };
            kpiPortofoliu    = new KpiCard { Title = "Valoare portofoliu" };

            var carduri = new[]
            {
                kpiEchipamente, kpiSesiuni, kpiRezervari,
                kpiIncasareLuna, kpiIncasareTotal, kpiPortofoliu
            };

            for (int i = 0; i < carduri.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                var card = carduri[i];
                card.Dock = DockStyle.Fill;
                // Margin 0 pe ultima coloană/ultimul rând, altfel „gap"-ul de
                // 1px ar fi dublat pe muchia exterioară a containerului.
                card.Margin = new Padding(
                    0, 0,
                    col == 2 ? 0 : 1,
                    row == 1 ? 0 : 1);
                _kpiGrid.Controls.Add(card, col, row);
            }

            this.Controls.Add(_kpiGrid);
        }

        /// <summary>Coloana din stânga: graficele (în tab-uri).</summary>
        private void ConstruiesteGrafice()
        {
            _leftCol = new Panel
            {
                Location  = new Point(_pad, _yGrafice),
                Size      = new Size(640, _grafH),
                BackColor = Design.Surface,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left
            };
            _leftCol.Paint += (s, e) =>
                Design.PaintCard(e.Graphics,
                    new Rectangle(0, 0, _leftCol.Width - 1, _leftCol.Height - 1), Design.Radius);
            this.Controls.Add(_leftCol);

            // Graficele nu trebuie sa blocheze tot dashboard-ul: daca
            // controlul Chart nu se poate crea (ex. assembly lipsa),
            // afisam un mesaj in locul graficelor si restul merge.
            try
            {
                CreeazaGrafice(_leftCol);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PanouDashboard.CreeazaGrafice");
                var lblFaraGrafice = new Label
                {
                    Text = "Grafice indisponibile: " + ex.Message,
                    Font = _fMesaj,
                    ForeColor = Design.Danger,
                    BackColor = Color.Transparent,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                _leftCol.Controls.Add(lblFaraGrafice);
            }
        }

        /// <summary>Coloana din dreapta: top 3 operatori după încasare.</summary>
        private void ConstruiesteTopOperatori()
        {
            _rightCol = new Panel
            {
                Location  = new Point(660, _yGrafice),
                Size      = new Size(340, _grafH),
                BackColor = Design.Surface,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            _rightCol.Paint += (s, e) =>
                Design.PaintCard(e.Graphics,
                    new Rectangle(0, 0, _rightCol.Width - 1, _rightCol.Height - 1), Design.Radius);
            this.Controls.Add(_rightCol);

            var lblTop = new Label
            {
                Text      = "TOP 3 OPERATORI",
                Font      = _fSectiune,
                ForeColor = Design.TextDim,
                BackColor = Color.Transparent,
                AutoSize  = true,
                Location  = new Point(Design.Sp5, Design.Sp4)
            };
            _rightCol.Controls.Add(lblTop);

            var lblTopSub = new Label
            {
                Text      = "după încasarea totală",
                Font      = Design.FontCaption,
                ForeColor = Design.TextDim,
                BackColor = Color.Transparent,
                AutoSize  = true,
                Location  = new Point(Design.Sp5, lblTop.Bottom + Design.Sp1)
            };
            _rightCol.Controls.Add(lblTopSub);

            for (int i = 0; i < 3; i++)
            {
                int ry = 72 + i * 64;

                lblOperatorRank[i] = new Label
                {
                    Text      = (i + 1).ToString(),
                    Font      = _fRank,
                    ForeColor = Design.TextDim,
                    BackColor = Color.Transparent,
                    AutoSize  = false,
                    Location  = new Point(Design.Sp5, ry + 2),
                    Size      = new Size(22, 20),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                _rightCol.Controls.Add(lblOperatorRank[i]);

                lblOperatorNume[i] = new Label
                {
                    Text      = "-",
                    Font      = _fOperNume,
                    ForeColor = Design.Text,
                    BackColor = Color.Transparent,
                    AutoSize  = false,
                    Location  = new Point(46, ry),
                    Size      = new Size(220, 20)
                };
                _rightCol.Controls.Add(lblOperatorNume[i]);

                lblOperatorIncasare[i] = new Label
                {
                    Text      = "-",
                    Font      = _fOperSuma,
                    ForeColor = Design.TextMuted,
                    BackColor = Color.Transparent,
                    AutoSize  = false,
                    Location  = new Point(46, ry + 21),
                    Size      = new Size(260, 18)
                };
                _rightCol.Controls.Add(lblOperatorIncasare[i]);
            }

            // Locul 1: doar un punct de 4px în accent, lipit de nume.
            _punctLoc1 = new RoundLabel
            {
                Text         = "",
                BackColor    = Design.Accent,
                CornerRadius = 2,
                Size         = new Size(4, 4),
                Visible      = false
            };
            // Adăugat ultimul: etichetele au fundal „transparent" și ar
            // picta peste punct dacă acesta ar rămâne în spatele lor.
            _rightCol.Controls.Add(_punctLoc1);
            _punctLoc1.BringToFront();
        }

        /// <summary>Lista compactă de alerte operaționale.</summary>
        private void ConstruiesteAlerte()
        {
            _alertCard = new Panel
            {
                Location  = new Point(_pad, _yAlerte),
                Size      = new Size(1000, _alerteH),
                BackColor = Design.Surface,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _alertCard.Paint += (s, e) =>
                Design.PaintCard(e.Graphics,
                    new Rectangle(0, 0, _alertCard.Width - 1, _alertCard.Height - 1), Design.Radius);
            this.Controls.Add(_alertCard);

            var lblAlerte = new Label
            {
                Text      = "ALERTE OPERAȚIONALE",
                Font      = _fSectiune,
                ForeColor = Design.TextDim,
                BackColor = Color.Transparent,
                AutoSize  = true,
                Location  = new Point(Design.Sp5, Design.Sp4)
            };
            _alertCard.Controls.Add(lblAlerte);

            lblRezumatAlerte = new Label
            {
                Text      = "",
                Font      = Design.FontSmallBold,
                ForeColor = Design.TextMuted,
                BackColor = Color.Transparent,
                AutoSize  = true,
                Location  = new Point(_alertCard.Width - 220, 17),
                TextAlign = ContentAlignment.MiddleRight,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            _alertCard.Controls.Add(lblRezumatAlerte);

            // „Arată tot →" ca link discret (nu buton plin).
            _lnkArataTot = new LinkLabel
            {
                Text            = "Arată tot →",
                Font            = Design.FontSmall,
                LinkColor       = Design.TextMuted,
                ActiveLinkColor = Design.AccentHover,
                VisitedLinkColor = Design.TextMuted,
                LinkBehavior    = LinkBehavior.NeverUnderline,
                BackColor       = Color.Transparent,
                AutoSize        = true,
                Location        = new Point(_alertCard.Width - 100, 16),
                Anchor          = AnchorStyles.Top | AnchorStyles.Right,
                Cursor          = Cursors.Hand
            };
            _alertCard.Controls.Add(_lnkArataTot);

            var meniuArataTot = new ContextMenuStrip();
            meniuArataTot.Items.Add("Echipamente → lista completă", null,
                (s, e) => AlertaDoubleClick?.Invoke(0, true));
            meniuArataTot.Items.Add("Rezervări → lista completă", null,
                (s, e) => AlertaDoubleClick?.Invoke(1, true));
            meniuArataTot.Items.Add("Sesiuni → lista completă", null,
                (s, e) => AlertaDoubleClick?.Invoke(2, true));
            _lnkArataTot.LinkClicked += (s, e) =>
                meniuArataTot.Show(_lnkArataTot, new Point(0, _lnkArataTot.Height));

            for (int i = 0; i < 3; i++)
            {
                int ay = 62 + i * 40;

                // Punct de 6px rotunjit — înlocuiește badge-ul colorat 28×28.
                punctAlerta[i] = new RoundLabel
                {
                    Text         = "",
                    BackColor    = Design.SuccessBright,
                    CornerRadius = 3,
                    Location     = new Point(Design.Sp5 + 1, ay + 7),
                    Size         = new Size(6, 6),
                    Cursor       = Cursors.Hand
                };
                punctAlerta[i].DoubleClick += (s, e) => RidicaAlerta(i);
                _alertCard.Controls.Add(punctAlerta[i]);

                lblAlertaText[i] = new Label
                {
                    Text      = "",
                    Font      = _fAlerta,
                    ForeColor = Design.TextDim,
                    BackColor = Color.Transparent,
                    AutoSize  = false,
                    Location  = new Point(Design.Sp5 + 18, ay),
                    Size      = new Size(600, 20),
                    Cursor    = Cursors.Hand
                };
                lblAlertaText[i].DoubleClick += (s, e) => RidicaAlerta(i);
                _alertCard.Controls.Add(lblAlertaText[i]);

                lblAlertaDetaliu[i] = new Label
                {
                    Text      = "",
                    Font      = _fAlertaDetaliu,
                    ForeColor = Design.TextDim,
                    BackColor = Color.Transparent,
                    AutoSize  = false,
                    Location  = new Point(_alertCard.Width - 240, ay + 2),
                    Size      = new Size(224, 18),
                    TextAlign = ContentAlignment.MiddleRight,
                    Anchor    = AnchorStyles.Top | AnchorStyles.Right,
                    Cursor    = Cursors.Hand
                };
                lblAlertaDetaliu[i].DoubleClick += (s, e) => RidicaAlerta(i);
                _alertCard.Controls.Add(lblAlertaDetaliu[i]);
            }
        }

        private void CreeazaGrafice(Control parent)
        {
            // Bară de tab-uri plată (TabControl-ul nativ își desenează banda
            // cu culorile temei Windows și rupe tema închisă).
            var tabs = new FlatTabs();

            chartIncasareLunar = CreeazaChartIncasareLunar();
            chartTipuriEchipamente = CreeazaChartTipuri();
            chartOperatoriSesiuni = CreeazaChartOperatori();

            var paginaIncasare = new Panel { Dock = DockStyle.Fill, BackColor = Design.Surface };
            paginaIncasare.Controls.Add(chartIncasareLunar);
            paginaIncasare.Controls.Add(CreeazaAntetIncasare());

            var paginaTipuri = new Panel { Dock = DockStyle.Fill, BackColor = Design.Surface };
            paginaTipuri.Controls.Add(chartTipuriEchipamente);

            var paginaOperatori = new Panel { Dock = DockStyle.Fill, BackColor = Design.Surface };
            paginaOperatori.Controls.Add(chartOperatoriSesiuni);

            var pagini = new Control[] { paginaIncasare, paginaTipuri, paginaOperatori };
            for (int i = 0; i < pagini.Length; i++)
                pagini[i].Visible = i == 0;

            var continut = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Design.Surface,
                Padding = new Padding(Design.Sp3, Design.Sp2, Design.Sp3, Design.Sp3)
            };
            // Ordinea de adăugare contează pentru Dock: ultimul adăugat este
            // așezat primul, deci bara de tab-uri își ia banda de sus.
            continut.Controls.Add(paginaOperatori);
            continut.Controls.Add(paginaTipuri);
            continut.Controls.Add(paginaIncasare);
            parent.Controls.Add(continut);

            tabs.Add("Încasare 6 luni");
            tabs.Add("Tipuri echipamente");
            tabs.Add("Top operatori");
            tabs.SelectedIndexChanged += (s, e) =>
            {
                for (int i = 0; i < pagini.Length; i++)
                    pagini[i].Visible = i == tabs.SelectedIndex;
            };
            parent.Controls.Add(tabs);
        }

        /// <summary>
        /// Antetul graficului de încasări: titlu + rezumat pe o linie proprie,
        /// legenda aliniată la dreapta. Înainte textul era <see cref="Chart.Title"/>;
        /// la 125% DPI se rupea pe două linii și intra peste zona graficului.
        /// </summary>
        private Panel CreeazaAntetIncasare()
        {
            var antet = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 42,
                BackColor = Design.Surface
            };

            _lblTitluIncasare = new Label
            {
                Text      = "",
                Font      = _fTitluGrafic,
                ForeColor = Design.TextMuted,
                BackColor = Color.Transparent,
                AutoSize  = true,
                Location  = new Point(0, 0)
            };
            antet.Controls.Add(_lblTitluIncasare);

            _lblRezumatIncasare = new Label
            {
                Text      = "",
                Font      = _fAxa,
                ForeColor = Design.TextDim,
                BackColor = Color.Transparent,
                AutoSize  = true,
                Location  = new Point(0, 20)
            };
            antet.Controls.Add(_lblRezumatIncasare);

            var legenda = CreeazaLegendaGrafic();
            legenda.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            antet.Controls.Add(legenda);
            EventHandler aseazaLegenda = (s, e) =>
                legenda.Location = new Point(antet.Width - legenda.Width, 1);
            antet.Resize += aseazaLegenda;
            antet.HandleCreated += aseazaLegenda;

            return antet;
        }

        /// <summary>Legendă proprie (Chart nu permite colorarea textului ei).</summary>
        private Control CreeazaLegendaGrafic()
        {
            var legenda = new FlowLayoutPanel
            {
                AutoSize      = true,
                AutoSizeMode  = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents  = false,
                BackColor     = Color.Transparent
            };
            legenda.Controls.Add(CreeazaElementLegenda("●  Nr. sesiuni", Design.Warning));
            legenda.Controls.Add(CreeazaElementLegenda("■  Încasare (EUR)", Design.Accent));
            return legenda;
        }

        private static Label CreeazaElementLegenda(string text, Color culoare)
        {
            return new Label
            {
                Text      = text,
                Font      = Design.FontSmall,
                ForeColor = culoare,
                BackColor = Color.Transparent,
                AutoSize  = true,
                Margin    = new Padding(Design.Sp4, 2, 0, 0)
            };
        }

        private Chart CreeazaChartIncasareLunar()
        {
            var chart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Design.Surface,
                AntiAliasing = AntiAliasingStyles.All,
                TextAntiAliasingQuality = TextAntiAliasingQuality.High
            };

            var area = new ChartArea("main") { BackColor = Design.Surface };
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisX.LabelStyle.Font = _fAxa;
            area.AxisX.LabelStyle.ForeColor = Design.TextDim;
            area.AxisX.LineColor = Design.Divider;
            area.AxisX.MajorTickMark.LineColor = Design.Divider;

            area.AxisY.MajorGrid.LineColor = Design.Divider;
            area.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            area.AxisY.LabelStyle.Font = _fAxa;
            area.AxisY.LabelStyle.ForeColor = Design.TextDim;
            area.AxisY.LabelStyle.Format = "#,##0";
            area.AxisY.LineColor = Design.Divider;
            area.AxisY.Title = "EUR";
            area.AxisY.TitleFont = _fAxaBold;
            area.AxisY.TitleForeColor = Design.TextDim;

            area.AxisY2.MajorGrid.Enabled = false;
            area.AxisY2.LabelStyle.Font = _fAxa;
            area.AxisY2.LabelStyle.ForeColor = Design.TextDim;
            area.AxisY2.LineColor = Design.Divider;
            area.AxisY2.Title = "Nr. sesiuni";
            area.AxisY2.TitleFont = _fAxaBold;
            area.AxisY2.TitleForeColor = Design.TextDim;

            chart.ChartAreas.Add(area);

            var sIncasare = new Series("Incasare")
            {
                ChartType = SeriesChartType.Column,
                ChartArea = "main",
                Color = Design.Accent,
                BorderColor = Design.Accent,
                BorderWidth = 0,
                ToolTip = "#VALX\nÎncasare: #VALY{N0} EUR"
            };
            sIncasare["PointWidth"] = "0.55";
            chart.Series.Add(sIncasare);

            var sSesiuni = new Series("Numar sesiuni")
            {
                ChartType = SeriesChartType.Line,
                ChartArea = "main",
                Color = Design.Warning,
                BorderWidth = 2,
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 7,
                MarkerColor = Design.Warning,
                MarkerBorderColor = Design.Surface,
                MarkerBorderWidth = 2,
                YAxisType = AxisType.Secondary,
                ToolTip = "#VALX\nSesiuni: #VALY"
            };
            chart.Series.Add(sSesiuni);

            return chart;
        }

        private Chart CreeazaChartTipuri()
        {
            var chart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Design.Surface,
                AntiAliasing = AntiAliasingStyles.All,
                TextAntiAliasingQuality = TextAntiAliasingQuality.High
            };

            var area = new ChartArea("pie") { BackColor = Design.Surface };
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
                Font = _fAxa,
                Label = "#PERCENT{P0}",
                LabelForeColor = Design.TextMuted
            };
            s["DoughnutRadius"] = "52";
            s["PieLabelStyle"] = "Outside";
            s["PieLineColor"] = "Black";
            chart.Series.Add(s);

            var legenda = new Legend("leg")
            {
                Docking = Docking.Right,
                Alignment = StringAlignment.Center,
                Font = _fLegenda,
                BackColor = Color.Transparent,
                ForeColor = Design.TextMuted,
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
                BackColor = Design.Surface,
                AntiAliasing = AntiAliasingStyles.All,
                TextAntiAliasingQuality = TextAntiAliasingQuality.High
            };

            var area = new ChartArea("bar") { BackColor = Design.Surface };

            // Bară orizontală: AxisX = categoriile (numele), AxisY = valorile.
            // Categoriile se adaugă crescător, ca locul 1 să iasă sus fără să
            // inversăm axa (inversarea muta etichetele valorilor deasupra).
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisX.LabelStyle.Font = _fAxa;
            area.AxisX.LabelStyle.ForeColor = Design.TextDim;

            area.AxisY.MajorGrid.LineColor = Design.Divider;
            area.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            area.AxisY.LabelStyle.Font = _fAxa;
            area.AxisY.LabelStyle.ForeColor = Design.TextDim;
            area.AxisY.LabelStyle.Format = "#,##0";
            area.AxisY.Title = "Încasare totală (EUR)";
            area.AxisY.TitleFont = _fAxaBold;
            area.AxisY.TitleForeColor = Design.TextDim;

            chart.ChartAreas.Add(area);

            var s = new Series("Incasare")
            {
                ChartType = SeriesChartType.Bar,
                ChartArea = "bar",
                Color = Design.Accent,
                BorderColor = Design.Accent,
                BorderWidth = 0,
                ToolTip = "#VALX\nÎncasare: #VALY{N0} EUR",
                IsValueShownAsLabel = true,
                LabelFormat = "{0:N0}",
                Font = _fAxa,
                LabelForeColor = Design.TextMuted
            };
            s["PointWidth"] = "0.6";
            chart.Series.Add(s);

            return chart;
        }

        // ============================================================
        //  REINCARCARE DATE
        // ============================================================
        private CancellationTokenSource _ctsIncarcare;
        private Panel _overlayIncarcare;

        /// <summary>
        /// Reîncarcă tabloul de bord asincron (selectoarele curente).
        /// Poate fi apelat și după închiderea unui dialog CRUD.
        /// </summary>
        public void Reincarca()
        {
            _ = IncarcaAsincronAsync(autoPerioada: false);
        }

        private async Task IncarcaAsincronAsync(bool autoPerioada)
        {
            if (IsDisposed) return;

            _ctsIncarcare?.Cancel();
            _ctsIncarcare?.Dispose();
            var cts = new CancellationTokenSource();
            _ctsIncarcare = cts;
            CancellationToken ct = cts.Token;

            ArataIncarcare(true);
            try
            {
                DashboardData date = await DashboardRepository.IncarcaAsync(
                    AnSelectat(), LunaSelectata(), autoPerioada,
                    ZileEchipamentStagnanta, ZileSesiuneInAsteptare, ZileRezervariViitoare,
                    ct);

                if (IsDisposed || ct.IsCancellationRequested) return;

                AplicaSelectori(date);
                AplicaKpi(date);
                IncarcaTopOperatori(date);
                IncarcaIncasareLunar(date);
                IncarcaTipuriEchipamente(date);
                IncarcaTopOperatoriGrafic(date);
                IncarcaAlerte(date);

                ActualizeazaStatus(
                    $"Actualizat la {DateTime.Now:dd.MM.yyyy HH:mm}", Design.TextDim);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Reîncărcare rapidă sau închidere — normal.
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PanouDashboard.IncarcaAsincronAsync");
                if (!IsDisposed)
                    ActualizeazaStatus("Indicatori indisponibili: " + ex.Message, Design.Danger);
            }
            finally
            {
                if (!IsDisposed && ReferenceEquals(_ctsIncarcare, cts))
                    ArataIncarcare(false);
            }
        }

        private void AplicaSelectori(DashboardData date)
        {
            _incarcSelectii = true;
            try
            {
                cmbAn.Items.Clear();
                cmbAn.Items.Add(ToateAnii);
                foreach (int an in date.Ani)
                    cmbAn.Items.Add(an.ToString());

                int indexAn = 0;
                if (date.AnSelectat.HasValue)
                {
                    int gasit = cmbAn.Items.IndexOf(date.AnSelectat.Value.ToString());
                    if (gasit >= 0) indexAn = gasit;
                }
                cmbAn.SelectedIndex = indexAn;

                int luna = date.LunaSelectat ?? 0;
                cmbLuna.SelectedIndex = Math.Max(0, Math.Min(12, luna));
            }
            finally
            {
                _incarcSelectii = false;
            }
        }

        private void AplicaKpi(DashboardData date)
        {
            kpiEchipamente.Value = date.EchipamenteLibere.ToString("N0");
            kpiEchipamente.Subtitle = $"din {date.TotalEchipamente:N0} înregistrate";

            kpiSesiuni.Value = date.SesiuniInAsteptare.ToString("N0");
            kpiSesiuni.Subtitle = $"din {date.TotalSesiuni:N0} sesiuni";

            kpiRezervari.Value = date.RezervariProgramate.ToString("N0");
            kpiRezervari.Subtitle = "cu status „Programată”";

            kpiIncasareLuna.Value = date.TotalPerioada.ToString("N0");
            kpiIncasareLuna.Unit = "EUR";
            kpiIncasareLuna.Subtitle =
                $"{DescriePerioada()} ({date.NumarPerioada:N0} sesiuni)";
            AplicaTrendIncasare(date);

            kpiIncasareTotal.Value = date.IncasareTotala.ToString("N0");
            kpiIncasareTotal.Unit = "EUR";
            kpiIncasareTotal.Subtitle = "toată perioada";

            kpiPortofoliu.Value = date.Portofoliu.ToString("N0");
            kpiPortofoliu.Unit = "EUR";
            kpiPortofoliu.Subtitle = "Σ tarif/oră · echipamente libere";
        }

        /// <summary>
        /// Diferența procentuală față de perioada anterioară. Totalul anterior
        /// vine din aceeași procedură (1 round trip), nu dintr-un query separat.
        /// </summary>
        private void AplicaTrendIncasare(DashboardData date)
        {
            int? an = AnSelectat();
            if (!an.HasValue)
            {
                kpiIncasareLuna.TrendText = "";
                return;
            }

            decimal anterior = date.TotalAnterior;
            decimal total = date.TotalPerioada;

            if (anterior <= 0)
            {
                if (total <= 0) { kpiIncasareLuna.TrendText = ""; return; }
                kpiIncasareLuna.TrendText = "\u25B2 nou";
                kpiIncasareLuna.TrendColor = Design.SuccessBright;
                return;
            }

            decimal delta = (total - anterior) / anterior * 100m;
            bool sus = delta >= 0;
            kpiIncasareLuna.TrendText = (sus ? "\u25B2 +" : "\u25BC ")
                + delta.ToString("0.#", CultureInfo.InvariantCulture) + "%";
            kpiIncasareLuna.TrendColor = sus
                ? Design.SuccessBright
                : Design.DangerBright;
        }

        private void IncarcaTopOperatori(DashboardData date)
        {
            for (int i = 0; i < 3; i++)
            {
                if (i < date.Operatori.Count)
                {
                    DashboardOperator op = date.Operatori[i];
                    lblOperatorNume[i].Text = op.Nume;
                    lblOperatorIncasare[i].Text =
                        $"{op.Total:N0} EUR  ({op.Numar} ses.)";
                }
                else
                {
                    lblOperatorNume[i].Text = "-";
                    lblOperatorIncasare[i].Text = "-";
                }
            }

            // Locul 1: punctul de accent se așază imediat după nume.
            if (_punctLoc1 != null)
            {
                string nume1 = lblOperatorNume[0].Text ?? "";
                _punctLoc1.Visible = nume1.Length > 0 && nume1 != "-";
                if (_punctLoc1.Visible)
                {
                    using (var g = CreateGraphics())
                    {
                        int w = TextRenderer.MeasureText(g, nume1, _fOperNume).Width;
                        _punctLoc1.Location = new Point(
                            lblOperatorNume[0].Left + w + Design.Sp2,
                            lblOperatorNume[0].Top + 8);
                    }
                }
            }
        }

        private void IncarcaIncasareLunar(DashboardData date)
        {
            chartIncasareLunar.Series["Incasare"].Points.Clear();
            chartIncasareLunar.Series["Numar sesiuni"].Points.Clear();

            if (date.Luni.Count == 0)
            {
                _lblTitluIncasare.Text = "Încasare lunară";
                _lblRezumatIncasare.Text = "Nu există sesiuni în perioada afișată.";
                return;
            }

            decimal total = 0, peak = 0;
            for (int i = 0; i < date.Luni.Count; i++)
            {
                DashboardLuna luna = date.Luni[i];
                string eticheta = luna.Luna.ToString("MMM yy", CulturaRo);
                int idxC = chartIncasareLunar.Series["Incasare"].Points.AddXY(
                    eticheta, luna.Total);
                chartIncasareLunar.Series["Numar sesiuni"].Points.AddXY(
                    eticheta, luna.Numar);

                total += luna.Total;
                if (luna.Total > peak) peak = luna.Total;

                if (i == date.Luni.Count - 1)
                {
                    // Ultima lună (cea aleasă) se evidențiază doar prin
                    // treapta luminoasă a aceluiași accent — fără a doua culoare.
                    var pC = chartIncasareLunar.Series["Incasare"].Points[idxC];
                    pC.Color = Design.AccentHover;
                }
            }

            DateTime prima = date.Luni[0].Luna;
            DateTime ultima = date.Luni[date.Luni.Count - 1].Luna;
            decimal medie = total / date.Luni.Count;

            _lblTitluIncasare.Text =
                // FIX_21: lunile in romana (ro-RO), nu in limba OS-ului.
                $"Încasare lunară · {prima.ToString("MMM yyyy", CulturaRo)} – " +
                $"{ultima.ToString("MMM yyyy", CulturaRo)}";
            _lblRezumatIncasare.Text =
                $"Total {total:N0} EUR   ·   Medie {medie:N0} EUR   ·   Vârf {peak:N0} EUR";
        }

        private void IncarcaTipuriEchipamente(DashboardData date)
        {
            var serie = chartTipuriEchipamente.Series["Tipuri"];
            serie.Points.Clear();
            chartTipuriEchipamente.Titles.Clear();

            int totalBucati = 0, i = 0;
            foreach (DashboardTip tip in date.Tipuri)
            {
                int idx = serie.Points.AddXY(tip.Tip, tip.Numar);
                serie.Points[idx].Color = CuloriSerii[i % CuloriSerii.Length];
                serie.Points[idx].BorderColor = Design.Surface;
                serie.Points[idx].BorderWidth = 2;
                serie.Points[idx].LegendText = $"{tip.Tip} ({tip.Numar})";
                totalBucati += tip.Numar;
                i++;
            }

            if (totalBucati == 0)
            {
                chartTipuriEchipamente.Titles.Add(new Title(
                    "Nu există echipamente înregistrate.",
                    Docking.Top, _fTitluGrafic, Design.TextMuted));
                return;
            }

            chartTipuriEchipamente.Titles.Add(new Title(
                $"Distribuția portofoliului   |   Total: {totalBucati} echipamente",
                Docking.Top, _fTitluGrafic, Design.TextMuted));
        }

        private void IncarcaTopOperatoriGrafic(DashboardData date)
        {
            var serie = chartOperatoriSesiuni.Series["Incasare"];
            serie.Points.Clear();
            chartOperatoriSesiuni.Titles.Clear();

            var operatori = date.Operatori;
            for (int i = operatori.Count - 1; i >= 0; i--)
            {
                serie.Points.AddXY(
                    $"{operatori[i].Nume}  ({operatori[i].Numar} ses.)",
                    operatori[i].Total);
            }

            if (serie.Points.Count == 0)
            {
                chartOperatoriSesiuni.Titles.Add(new Title(
                    "Nu există operatori înregistrați.",
                    Docking.Top, _fTitluGrafic, Design.TextMuted));
                return;
            }

            // Spațiu la capătul axei ca eticheta ultimei bare să încapă.
            double maxim = 0;
            foreach (var p in serie.Points) maxim = Math.Max(maxim, p.YValues[0]);
            var axaValori = chartOperatoriSesiuni.ChartAreas["bar"].AxisY;
            axaValori.Maximum = maxim > 0 ? maxim * 1.18 : double.NaN;

            chartOperatoriSesiuni.Titles.Add(new Title(
                "Top 5 operatori după încasarea totală",
                Docking.Top, _fTitluGrafic, Design.TextMuted));
        }

        private void IncarcaAlerte(DashboardData date)
        {
            DashboardAlerta aEchipamente = GasesteAlerta(date, "Echipamente");
            DashboardAlerta aRezervari = GasesteAlerta(date, "Rezervari");
            DashboardAlerta aSesiuni = GasesteAlerta(date, "Sesiuni");

            int nrEchipamente = aEchipamente?.Numar ?? 0;
            string detaliuEchipamente = nrEchipamente > 0
                ? $"cea mai veche: {aEchipamente.ZileMax ?? 0} zile"
                : "toate în termen";
            SeteazaAlerta(0, nrEchipamente,
                $"Echipamente libere de peste {ZileEchipamentStagnanta} de zile",
                detaliuEchipamente);

            int nrRezervari = aRezervari?.Numar ?? 0;
            string detaliuRezervari = nrRezervari > 0 && aRezervari.DataMin.HasValue
                ? $"următoarea: {aRezervari.DataMin.Value:dd.MM.yyyy HH:mm}"
                : "nicio programare apropiată";
            SeteazaAlerta(1, nrRezervari,
                $"Rezervări programate în următoarele {ZileRezervariViitoare} zile",
                detaliuRezervari);

            int nrSesiuni = aSesiuni?.Numar ?? 0;
            string detaliuSesiuni = nrSesiuni > 0
                ? $"cea mai veche: {aSesiuni.ZileMax ?? 0} zile"
                : "toate în termen";
            SeteazaAlerta(2, nrSesiuni,
                $"Sesiuni în așteptare de peste {ZileSesiuneInAsteptare} de zile",
                detaliuSesiuni);

            int totalAlerte = 0;
            for (int i = 0; i < 3; i++) totalAlerte += numarAlerta[i];

            lblRezumatAlerte.Text = totalAlerte > 0
                ? $"{totalAlerte} ALERTE ACTIVE" : "FĂRĂ ALERTE";
            lblRezumatAlerte.ForeColor = totalAlerte > 0
                ? Design.WarningBright : Design.SuccessBright;

            // Repoziționează ca să nu se suprapună peste linkul „Arată tot".
            if (_alertCard != null && _lnkArataTot != null)
            {
                using (var g = this.CreateGraphics())
                {
                    var sz = TextRenderer.MeasureText(
                        lblRezumatAlerte.Text, lblRezumatAlerte.Font);
                    lblRezumatAlerte.Size = new Size(sz.Width + 6, 20);
                }
                lblRezumatAlerte.Location = new Point(
                    _alertCard.Width - _lnkArataTot.Width - lblRezumatAlerte.Width
                        - Design.Sp4 - Design.Sp3, 17);
            }
        }

        private static DashboardAlerta GasesteAlerta(DashboardData date, string categorie)
        {
            foreach (DashboardAlerta a in date.Alerte)
                if (string.Equals(a.Categorie, categorie, StringComparison.OrdinalIgnoreCase))
                    return a;
            return null;
        }

        private void SeteazaAlerta(int index, int numar, string descriere, string detaliu)
        {
            bool activa = numar > 0;
            numarAlerta[index] = numar;

            // Punct de 6px: portocaliu aprins (activ) / verde aprins (inactiv).
            punctAlerta[index].BackColor = activa ? Design.WarningBright : Design.SuccessBright;

            lblAlertaText[index].Text = $"{descriere}  ·  {numar}";
            lblAlertaText[index].ForeColor = activa ? Design.TextSecondary : Design.TextDim;

            lblAlertaDetaliu[index].Text = detaliu;
        }

        private void RidicaAlerta(int index)
        {
            bool arataTot = numarAlerta[index] <= 0;
            AlertaDoubleClick?.Invoke(index, arataTot);
        }

        private void ActualizeazaStatus(string text, Color culoare)
        {
            var gasite = this.Controls.Find("lblStatus", true);
            if (gasite.Length == 0) return;
            var lbl = (Label)gasite[0];
            lbl.ForeColor = culoare;
            lbl.Text = text;
        }

        /// <summary>
        /// Afișează/ascunde indicatorul central. Overlay-ul este păstrat
        /// până la sfârșitul încărcării curente (nu se suprapun două).
        /// </summary>
        private void ArataIncarcare(bool activ)
        {
            if (activ)
            {
                if (_overlayIncarcare != null) return;

                var overlay = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Design.Bg
                };

                var tlp = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 2,
                    BackColor = Design.Bg
                };
                tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
                tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

                var progress = new ProgressBar
                {
                    Style = ProgressBarStyle.Marquee,
                    MarqueeAnimationSpeed = 30,
                    Width = 240,
                    Height = 6,
                    Anchor = AnchorStyles.None
                };

                var lbl = new Label
                {
                    Text = "Se încarcă datele tabloului de bord…",
                    Font = Design.FontBody,
                    ForeColor = Design.TextMuted,
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
                    AppLogger.Warn("Eliminare indicator incarcare: " + ex.Message,
                        "PanouDashboard.ArataIncarcare");
                }
                _overlayIncarcare = null;
            }
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
                return new DateTime(2000, luna.Value, 1).ToString("MMMM", CulturaRo) + " (toți anii)";
            if (an.HasValue) return $"anul {an.Value}";
            return "toată perioada";
        }

    }
}
