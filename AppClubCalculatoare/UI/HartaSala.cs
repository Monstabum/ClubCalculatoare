using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppClubCalculatoare.UI
{
    /// <summary>
    /// Stație (echipament) afișată pe harta sălii: denumire, sală, tip,
    /// status și — dacă există o sesiune activă — timpul rămas.
    /// </summary>
    internal class StationInfo
    {
        public int      IdEchipament { get; set; }
        public string   Denumire    { get; set; }
        public string   Tip         { get; set; }
        public string   Sala        { get; set; }
        public decimal  TarifOra    { get; set; }
        public string   Status      { get; set; }
        public int?     MinuteRamase { get; set; }   // null = fără sesiune activă
        public bool     TimpDepasit { get; set; }
    }

    /// <summary>Statusurile posibile ale unei stații (valorile din BD).</summary>
    internal static class StatusStatie
    {
        public const string Liber      = "Liber";
        public const string Ocupat     = "Ocupat";
        public const string Rezervat   = "Rezervat";
        public const string InService  = "În service";

        /// <summary>Culoarea asociată unui status (paleta MaterialTheme).</summary>
        public static Color Culoare(string status)
        {
            switch (status)
            {
                case Liber:     return MaterialTheme.Success;
                case Ocupat:    return MaterialTheme.Primary;
                case Rezervat:  return MaterialTheme.Warning;
                default:        return MaterialTheme.Danger;   // În service / necunoscut
            }
        }

        /// <summary>Ordinea de sortare: mai întâi stațiile ocupate.</summary>
        public static int Prioritate(string status)
        {
            switch (status)
            {
                case Ocupat:    return 0;
                case Rezervat:  return 1;
                case Liber:     return 2;
                default:        return 3;
            }
        }
    }

    /// <summary>
    /// Tile pentru o stație pe harta sălii: bandă de status colorată,
    /// denumire, sală/tip, badge de status, timp rămas și tarif orar.
    /// Desenat complet în OnPaint (fără controale copii), colțuri rotunjite.
    /// </summary>
    internal class StationTile : Panel
    {
        private bool _hover;
        private bool _selectat;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public StationInfo Info { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Selectat
        {
            get { return _selectat; }
            set { _selectat = value; Invalidate(); }
        }

        public StationTile()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor   = MaterialTheme.Surface;
            Cursor      = Cursors.Hand;
            Size        = new Size(214, 122);
            Margin      = new Padding(6);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        // Colțurile rotunjite sunt desenate cu antialiasing în OnPaint.
        // (Fără Region decupat — acela lăsa muchii „dințate".)

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.PixelOffsetMode   = PixelOffsetMode.HighQuality;

            Color fundal = MaterialTheme.ResolveOpaqueBackground(this, MaterialTheme.Background);
            if (Info == null) { g.Clear(fundal); return; }

            Color cStatus = StatusStatie.Culoare(Info.Status);

            // Fundal: Surface, „sticlă" la hover/selectare.
            g.Clear(fundal);

            var rect = new Rectangle(1, 1, Math.Max(2, Width - 3), Math.Max(2, Height - 3));

            using (var path = MaterialTheme.RoundedPath(rect, MaterialTheme.CardRadius))
            {
                // Suprafață plată: Surface, ridicată la hover/selectare.
                Color fundalCard = (_selectat || _hover)
                    ? MaterialTheme.SurfaceAlt : MaterialTheme.Surface;
                using (var br = new SolidBrush(fundalCard))
                    g.FillPath(br, path);

                // Bordură: accent/status când e selectat sau hover, altfel Divider.
                Color bordura = _selectat ? cStatus : (_hover ? cStatus : MaterialTheme.Divider);
                using (var pen = new Pen(bordura, _selectat ? 2f : 1f))
                    g.DrawPath(pen, path);
            }

            // Bandă verticală de status în stânga (reper colorat).
            using (var brush = new SolidBrush(cStatus))
            {
                var stripe = new Rectangle(10, 14, 4, Math.Max(8, Height - 28));
                using (var path = MaterialTheme.RoundedPath(stripe, 2))
                    g.FillPath(brush, path);
            }

            int x = 26;
            int w = Math.Max(40, Width - x - 16);

            // Rând 1: denumirea stației + glif dispozitiv în dreapta.
            TextRenderer.DrawText(g, Info.Denumire ?? "", MaterialTheme.BodyBold,
                new Rectangle(x, 12, w - 26, 20), MaterialTheme.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            MaterialTheme.DrawGlyph(g, "\uEC0B", new Point(Width - 40, 12), 14f, MaterialTheme.TextDim);

            // Rând 2: sala + tipul stației.
            string salaTip = "Sala: " + (string.IsNullOrEmpty(Info.Sala) ? "—" : Info.Sala)
                             + "  ·  " + (Info.Tip ?? "");
            TextRenderer.DrawText(g, salaTip, MaterialTheme.FontSmall,
                new Rectangle(x, 34, w, 16), MaterialTheme.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // Rând 3: badge de status (pastilă colorată).
            string statusText = (Info.Status ?? "").ToUpperInvariant();
            var fBadge = MaterialTheme.SmallBold;
            int badgeW = TextRenderer.MeasureText(statusText, fBadge).Width + 18;
            var badge = new Rectangle(x, 56, badgeW, 20);
            using (var path = MaterialTheme.RoundedPath(badge, 10))
            {
                using (var brush = new SolidBrush(MaterialTheme.WithAlpha(cStatus, 60)))
                    g.FillPath(brush, path);
                using (var pen = new Pen(cStatus, 1f))
                    g.DrawPath(pen, path);
            }
            TextRenderer.DrawText(g, statusText, fBadge, badge, cStatus,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // Rând 4: timp rămas (dacă există sesiune activă) + tarif orar.
            string tarif = Info.TarifOra.ToString("N0") + " RON/oră";
            if (Info.MinuteRamase.HasValue)
            {
                string timer = Info.TimpDepasit
                    ? "Timp depășit!"
                    : "Rămas " + Info.MinuteRamase.Value + " min";
                Color cTimer = Info.TimpDepasit ? MaterialTheme.Danger : cStatus;

                TextRenderer.DrawText(g, timer, MaterialTheme.SmallBold,
                    new Rectangle(x, 82, w - 30, 16), cTimer,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                TextRenderer.DrawText(g, tarif, MaterialTheme.Caption,
                    new Rectangle(x, 100, w, 14), MaterialTheme.TextDim,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
            else
            {
                TextRenderer.DrawText(g, "Tarif " + tarif, MaterialTheme.Caption,
                    new Rectangle(x, 84, w, 14), MaterialTheme.TextDim,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }

    /// <summary>
    /// Panoul „Sala Live": harta stațiilor clubului în timp real.
    ///
    /// Layout: header (titlu + butoane + filtre + legendă), grilă de
    /// StationTile-uri (FlowLayoutPanel cu wrap) și bara de status jos.
    /// Datele se încarcă asincron (overlay de încărcare, ca în
    /// PanouDashboard) și se reîmprospătează automat la 30 de secunde.
    /// </summary>
    internal class HartaSala : UserControl
    {
        /// <summary>Click pe o stație — deschide detaliile echipamentului.</summary>
        public event Action<StationInfo> StationSelectat;

        /// <summary>Cerere de întoarcere la tabloul de bord.</summary>
        public event Action Inchide;

        /// <summary>Arată butonul „← Tabloul de bord" (întoarcerea la ecranul anterior).</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool PermiteInapoi
        {
            get { return _permiteInapoi; }
            set
            {
                _permiteInapoi = value;
                if (_btnInapoi != null) _btnInapoi.Visible = value;
            }
        }

        private bool _permiteInapoi;

        private readonly string _connectionString;

        private FlowLayoutPanel _grid;
        private Label _lblContoare;
        private Label _lblStatus;
        private Label _lblEmpty;
        private ModernButton _btnInapoi;
        private ModernButton _btnRefresh;
        private readonly List<ModernButton> _chipFiltre = new List<ModernButton>();

        private readonly List<StationInfo> _statii = new List<StationInfo>();
        private string _filtru;                       // null = toate stațiile
        private Panel _overlayIncarcare;

        private const string StatusSesiuneActiva = "În desfășurare";
        private const int IntervalRefreshMs = 30000;

        private readonly Timer _timerRefresh;

        public HartaSala(string connectionString)
        {
            _connectionString = connectionString;
            BackColor = MaterialTheme.Background;
            Dock = DockStyle.Fill;

            ConstruiesteInterfata();

            _timerRefresh = new Timer { Interval = IntervalRefreshMs };
            _timerRefresh.Tick += (s, e) => { if (Visible) IncarcaAsincron(false); };
            _timerRefresh.Start();

            // Prima încărcare cu overlay (apare indicatorul).
            IncarcaAsincron(true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _timerRefresh.Stop(); _timerRefresh.Dispose(); }
                catch (Exception ex) { AppLogger.Warn("Cleanup timer Sala Live: " + ex.Message, "HartaSala"); }
            }
            base.Dispose(disposing);
        }

        // ════════════════════════════════════════════════════════════
        //  CONSTRUIRE INTERFAȚĂ
        // ════════════════════════════════════════════════════════════
        private void ConstruiesteInterfata()
        {
            // ── Grila de stații (Dock.Fill) — adăugată PRIMĂ ───────
            // Ordinea de adăugare e critică în WinForms: controalele
            // adăugate mai târziu primesc prioritate la docking, deci
            // header/footer se adaugă DUPA grilă (ca în MaterialUIHelper).
            _grid = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true,
                BackColor = MaterialTheme.Background,
                Padding = new Padding(14, 12, 14, 8)
            };

            // Mesaj „stare goală" deasupra grilei (același strat Fill).
            _lblEmpty = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = MaterialTheme.FontSubtitle,
                ForeColor = MaterialTheme.TextSecondary,
                BackColor = MaterialTheme.Background,
                Visible = false
            };
            _grid.Controls.Add(_lblEmpty);
            _lblEmpty.BringToFront();

            // ── Bara de status (Dock.Bottom) — înaintea header-ului ─
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                BackColor = MaterialTheme.BottomBar,
                Padding = new Padding(20, 4, 20, 4)
            };
            _lblStatus = new Label
            {
                Text = "",
                Font = MaterialTheme.CaptionItalic,
                ForeColor = MaterialTheme.TextDim,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(20, 6)
            };
            footer.Controls.Add(_lblStatus);

            // ── Header (Dock.Top) — adăugat ULTIMul ─────────────────
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 126,
                BackColor = MaterialTheme.Surface,
                Padding = new Padding(20, 12, 20, 8)
            };
            header.Paint += (s, e) =>
            {
                using (var pen = new Pen(MaterialTheme.Divider, 1f))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            var lblTitlu = new Label
            {
                Text = "SALA LIVE",
                Font = MaterialTheme.H2,
                ForeColor = MaterialTheme.TextPrimary,
                AutoSize = true,
                Location = new Point(20, 12),
                BackColor = Color.Transparent
            };
            header.Controls.Add(lblTitlu);

            var lblSub = new Label
            {
                Text = "Starea stațiilor în timp real · click pe o stație pentru detalii",
                Font = MaterialTheme.FontSmall,
                ForeColor = MaterialTheme.TextSecondary,
                AutoSize = true,
                Location = new Point(20, 38),
                BackColor = Color.Transparent
            };
            header.Controls.Add(lblSub);

            // Butoane din dreapta (repoziționate la resize).
            _btnInapoi = new ModernButton
            {
                Text = "←  Tabloul de bord",
                Variant = ButtonVariant.Ghost,
                Size = new Size(150, 30),
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnInapoi.Click += (s, e) => Inchide?.Invoke();
            header.Controls.Add(_btnInapoi);

            _btnRefresh = new ModernButton
            {
                Text = "Reîncarcă",
                Variant = ButtonVariant.Secondary,
                Size = new Size(110, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnRefresh.Click += (s, e) => IncarcaAsincron(false);
            header.Controls.Add(_btnRefresh);

            header.Resize += (s, e) =>
            {
                _btnRefresh.Location = new Point(header.Width - _btnRefresh.Width - 20, 12);
                _btnInapoi.Location = new Point(header.Width - _btnRefresh.Width - _btnInapoi.Width - 30, 12);
            };

            // Filtre rapide pe status.
            var chips = new FlowLayoutPanel
            {
                Location = new Point(20, 68),
                Size = new Size(460, 30),
                BackColor = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            AdaugaChipFiltru(chips, "Toate", null);
            AdaugaChipFiltru(chips, "Liber", StatusStatie.Liber);
            AdaugaChipFiltru(chips, "Ocupat", StatusStatie.Ocupat);
            AdaugaChipFiltru(chips, "Rezervat", StatusStatie.Rezervat);
            AdaugaChipFiltru(chips, "În service", StatusStatie.InService);
            header.Controls.Add(chips);

            // Legendă (punct colorat + text), la stânga sub filtre.
            var legenda = new FlowLayoutPanel
            {
                Location = new Point(20, 100),
                Size = new Size(560, 18),
                BackColor = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            AdaugaElementLegenda(legenda, StatusStatie.Liber);
            AdaugaElementLegenda(legenda, StatusStatie.Ocupat);
            AdaugaElementLegenda(legenda, StatusStatie.Rezervat);
            AdaugaElementLegenda(legenda, StatusStatie.InService);
            header.Controls.Add(legenda);

            // Contoare (dreapta, aliniate cu filtrele).
            _lblContoare = new Label
            {
                Text = "",
                Font = MaterialTheme.SmallBold,
                ForeColor = MaterialTheme.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleRight
            };
            header.Controls.Add(_lblContoare);
            header.Resize += (s, e) =>
            {
                _lblContoare.Location = new Point(
                    Math.Max(500, header.Width - _lblContoare.Width - 20), 70);
            };

            // Adăugarea în această ordine e esențială pentru docking:
            // grila (Fill) prima, apoi footer-ul (Bottom), apoi header-ul (Top).
            this.Controls.Add(_grid);
            this.Controls.Add(footer);
            this.Controls.Add(header);
        }

        private void AdaugaChipFiltru(FlowLayoutPanel parent, string text, string status)
        {
            var chip = new ModernButton
            {
                Text = text,
                Variant = ButtonVariant.Ghost,
                Size = new Size(88, 26),
                Margin = new Padding(0, 0, 8, 0),
                Tag = status      // null = filtru „Toate"
            };
            chip.Click += (s, e) =>
            {
                _filtru = status;
                ActualizeazaChipuri();
                ReconstruiesteGrilă();
            };
            _chipFiltre.Add(chip);
            parent.Controls.Add(chip);
        }

        private void ActualizeazaChipuri()
        {
            foreach (var chip in _chipFiltre)
            {
                string statusChip = chip.Tag as string;   // null = „Toate"
                chip.Variant = (statusChip == _filtru)
                    ? ButtonVariant.Primary
                    : ButtonVariant.Ghost;
            }
        }

        private void AdaugaElementLegenda(FlowLayoutPanel parent, string status)
        {
            var dot = new RoundLabel
            {
                Size = new Size(10, 10),
                Margin = new Padding(0, 4, 6, 0),
                BackColor = StatusStatie.Culoare(status),
                CornerRadius = 5
            };

            var lbl = new Label
            {
                Text = status,
                Font = MaterialTheme.Caption,
                ForeColor = MaterialTheme.TextSecondary,
                AutoSize = true,
                Margin = new Padding(0, 0, 16, 0)
            };
            parent.Controls.Add(dot);
            parent.Controls.Add(lbl);
        }

        // ════════════════════════════════════════════════════════════
        //  ÎNCĂRCARE DATE
        // ════════════════════════════════════════════════════════════

        /// <summary>Reîmprospătare cerută din exterior (butonul panoului).</summary>
        public void Reincarca()
        {
            IncarcaAsincron(false);
        }

        private async void IncarcaAsincron(bool cuOverlay)
        {
            // Overlay doar la prima încărcare (listă goală) ca să nu
            // clipiască grila la refresh-ul automat.
            if (cuOverlay) ArataIncarcare(true);
            await Task.Delay(60);   // lasă message loop-ul să picteze indicatorul
            try
            {
                IncarcaDate();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "HartaSala.IncarcaDate");
                _lblStatus.ForeColor = MaterialTheme.Danger;
                _lblStatus.Text = "Harta sălii nu a putut fi încărcată: " + ex.Message;
            }
            finally
            {
                if (cuOverlay) ArataIncarcare(false);
            }
        }

        private void IncarcaDate()
        {
            var statii = new List<StationInfo>();

            using (var conn = SqlDb.CreateConnection())
            {
                conn.Open();

                using (var cmd = new SqlCommand(@"
                        SELECT e.id_echipament, e.denumire, e.tip_echipament,
                               e.tarif_ora, e.status, s.nume_sala
                        FROM Echipamente e
                        LEFT JOIN Sali s ON s.id_sala = e.sala_id
                        ORDER BY s.nume_sala, e.denumire", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        statii.Add(new StationInfo
                        {
                            IdEchipament = reader.GetInt32(0),
                            Denumire = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            Tip = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            TarifOra = reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3)),
                            Status = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            Sala = reader.IsDBNull(5) ? "" : reader.GetString(5)
                        });
                    }
                }

                // Sesiuni active (timp rămas pe fiecare stație ocupată).
                using (var cmd = new SqlCommand(@"
                        SELECT s.id_echipament, s.durata_minute, s.data_sesiune
                        FROM Sesiuni s
                        WHERE s.status = @status
                        ORDER BY s.data_sesiune DESC", conn))
                {
                    cmd.Parameters.Add("@status", SqlDbType.NVarChar, 20).Value = StatusSesiuneActiva;

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int idEq = reader.GetInt32(0);
                            if (reader.IsDBNull(1) || reader.IsDBNull(2)) continue;

                            int durataMinute = reader.GetInt32(1);
                            DateTime dataStart = reader.GetDateTime(2);

                            double scurse = (DateTime.Now - dataStart).TotalMinutes;
                            int ramase = (int)Math.Ceiling(durataMinute - scurse);

                            foreach (var st in statii)
                            {
                                if (st.IdEchipament != idEq) continue;
                                if (st.MinuteRamase.HasValue) break;   // cea mai recentă sesiune
                                st.MinuteRamase = ramase;
                                st.TimpDepasit = ramase <= 0;
                                break;
                            }
                        }
                    }
                }
            }

            _statii.Clear();
            _statii.AddRange(statii);

            ReconstruiesteGrilă();
            ActualizeazaContoare();
            ActualizeazaChipuri();

            _lblStatus.ForeColor = MaterialTheme.TextDim;
            _lblStatus.Text = "Actualizat la " + DateTime.Now.ToString("HH:mm:ss")
                            + "  ·  reîmprospătare automată la 30 s";
        }

        private void ReconstruiesteGrilă()
        {
            _grid.SuspendLayout();
            _grid.Controls.Clear();

            var afisate = new List<StationInfo>();
            foreach (var st in _statii)
            {
                if (_filtru == null || string.Equals(st.Status, _filtru, StringComparison.Ordinal))
                    afisate.Add(st);
            }
            afisate.Sort((a, b) =>
            {
                int p = StatusStatie.Prioritate(a.Status).CompareTo(StatusStatie.Prioritate(b.Status));
                return p != 0 ? p : string.Compare(a.Denumire, b.Denumire, StringComparison.Ordinal);
            });

            foreach (var st in afisate)
            {
                var tile = new StationTile { Info = st };
                tile.AccessibleName = st.Denumire + " — " + st.Status;
                MaterialUIHelper.AtasareTooltip(tile, st.Denumire,
                    "Sala: " + st.Sala + "  ·  " + st.Tip + "\nStatus: " + st.Status +
                    (st.MinuteRamase.HasValue
                        ? (st.TimpDepasit ? "\nTimp depășit" : "\nRămas: " + st.MinuteRamase + " min")
                        : "") +
                    "\nTarif: " + st.TarifOra.ToString("N0") + " RON/oră");
                tile.Click += (s, e) => StationSelectat?.Invoke(st);
                _grid.Controls.Add(tile);
            }

            _grid.ResumeLayout();

            _lblEmpty.Visible = afisate.Count == 0;
            if (afisate.Count == 0)
                _lblEmpty.Text = _statii.Count == 0
                    ? "Nicio stație înregistrată. Adaugă echipamente din meniul Echipamente."
                    : "Nicio stație cu filtrul selectat.";
        }

        private void ActualizeazaContoare()
        {
            int liber = 0, ocupat = 0, rezervat = 0, service = 0;
            foreach (var st in _statii)
            {
                switch (st.Status)
                {
                    case StatusStatie.Liber:     liber++; break;
                    case StatusStatie.Ocupat:    ocupat++; break;
                    case StatusStatie.Rezervat:  rezervat++; break;
                    default:                     service++; break;
                }
            }
            _lblContoare.Text = liber + " libere · " + ocupat + " ocupate · "
                              + rezervat + " rezervate · " + service + " în service";
        }

        // ════════════════════════════════════════════════════════════
        //  INDICATOR DE ÎNCĂRCARE (overlay)
        // ════════════════════════════════════════════════════════════
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
                    Width = 240,
                    Height = 6,
                    Anchor = AnchorStyles.None
                };
                var lbl = new Label
                {
                    Text = "Se încarcă harta sălii…",
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
                    AppLogger.Warn("Eliminare indicator Sala Live: " + ex.Message, "HartaSala");
                }
                _overlayIncarcare = null;
            }
        }
    }
}
