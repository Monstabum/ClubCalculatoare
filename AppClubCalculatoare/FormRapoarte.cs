using System;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppClubCalculatoare.UI;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Fereastra Rapoarte si Statistici.
    ///
    /// Imbunatatiri adaugate:
    ///   Q3  — Butoane "Export Excel (CSV)" si "Tipareste / PDF".
    ///   Q7  — Filtre pentru raportul "Echipamente disponibile": tip si luna.
    ///   Q11 — "Top operatori după încasare" include si operatorii cu 0 incasare,
    ///          HAVING eliminat, ISNULL aplicat, coloana RANK() adaugata.
    /// </summary>
    public partial class FormRapoarte : AsyncFormBase
    {
        private readonly string connectionString;
        private DataGridView dgvRapoarte;

        // Q7: controale filtre Echipamente disponibile
        private Panel    panelFiltre;
        private ComboBox cmbTipEchipament;
        private ComboBox cmbLuna;

        public FormRapoarte(string connString)
        {
            // ── Sistem drepturi: aparare anti post-injection ─────────
            // Chiar daca fereastra ar fi construita programatic (link direct),
            // se inchide imediat daca utilizatorul nu are punctul de acces.
            if (!PermissionService.VerificaAre(PuncteAcces.MeniuRapoarte,
                    SessionInfo.EsteAdmin))
            {
                AppLogger.Security(
                    $"Acces blocat: '{SessionInfo.Username}' a incercat sa deschida Rapoarte direct");
                PermissionService.AfiseazaMesajAcces();
                this.Load += (s, e) => this.Close();
            }

            this.connectionString = connString;
            this.Text             = "Rapoarte si Statistici";
            this.Size             = new Size(1000, 650);
            this.StartPosition    = FormStartPosition.CenterParent;
            InitializeComponent();

            // DPI: scalare uniformă (fereastra este DPI-aware) + titlu închis.
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode       = AutoScaleMode.Dpi;
            WindowChrome.EnableDarkTitleBar(this);
        }

        private void InitializeComponent()
        {
            this.BackColor = MaterialTheme.Background;
            this.Font = MaterialTheme.FontBody;
            this.Size = new Size(1080, 700);
            this.MinimumSize = new Size(900, 560);

            // ── Zona centrala (Dock.Fill) — adaugata PRIMA ─────────
            var content = MaterialUIHelper.ContentPanel();
            this.Controls.Add(content);

            var gridCard = new MaterialCard { Dock = DockStyle.Fill };
            content.Controls.Add(gridCard);

            var gridInner = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(4)
            };
            gridCard.Controls.Add(gridInner);

            dgvRapoarte = new DataGridView
            {
                Name = "dgvRapoarte",
                Dock = DockStyle.Fill,
                ReadOnly = true,
                // FIX vizibilitate coloane: rapoartele au 7-12 coloane.
                // Fill inghesuia totul (~80px/coloana) => header + celule
                // trunchiate cu "...". Cu DisplayedCells fiecare coloana
                // isi ia latimea continutului si apare scrollbar orizontal.
                ScrollBars = ScrollBars.Both
            };
            MaterialUIHelper.StyleGrid(dgvRapoarte, MaterialTheme.Primary);
            dgvRapoarte.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            dgvRapoarte.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvRapoarte.ColumnHeadersHeight = 36;
            gridInner.Controls.Add(dgvRapoarte);

            // ── Bara inferioara (Dock.Bottom): Export / Tipareste / Inchide
            FlowLayoutPanel leftBottom, rightBottom;
            var bottomBar = MaterialUIHelper.SplitBottomBar(out leftBottom, out rightBottom);
            this.Controls.Add(bottomBar);

            var btnExcel = MaterialUIHelper.Button("📄  Export Excel", MaterialButtonStyle.Success, 150);
            btnExcel.Click += BtnExportExcel_Click;
            leftBottom.Controls.Add(btnExcel);

            var btnPdf = MaterialUIHelper.Button("🖨  Tipărește / PDF", MaterialButtonStyle.Secondary, 170);
            btnPdf.Click += BtnPrint_Click;
            leftBottom.Controls.Add(btnPdf);

            var btnInchide = MaterialUIHelper.Button("✕  Închide", MaterialButtonStyle.Outline, 130);
            btnInchide.Click += (s, e) => this.Close();
            rightBottom.Controls.Add(btnInchide);

            // ── Toolbar sus (Dock.Top): selector rapoarte + filtre ──
            // FIX taiere rand 2 butoane: Panel AutoSize + FlowLayoutPanel
            // Dock.Top AutoSize se masoara cu latime infinita (1 rand),
            // apoi e constrans la latimea formei si face wrap la 2 randuri
            // fara sa creasca parintele => randul 2 ramanea acoperit.
            // TableLayoutPanel cu 2 randuri AutoSize constrange corect
            // latimea flow-ului la latimea formei, deci inaltimea wrap-ului
            // e calculata corect la orice latime/DPI. Randul de filtre
            // se colapseaza automat cand Visible=false.
            var toolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                // Suprafata petrol (nu alb): pe tema inchisa o bara alba
                // atarna imediat sub header si rupea unitatea vizuala; in
                // plus, butoanele Material isi rezolva fundalul mergand
                // pe lancul de parinti pana la prima culoare opaca — deci
                // ar fi fost desenate tot pe alb.
                BackColor = MaterialTheme.Surface,
                Padding = new Padding(16, 10, 16, 10),
                Margin = new Padding(0)
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            toolbar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            toolbar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            toolbar.Paint += (s, e) =>
            {
                using (var pen = new Pen(MaterialTheme.Divider, 1f))
                    e.Graphics.DrawLine(pen, 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1);
            };
            this.Controls.Add(toolbar);

            var flpRapoarte = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0),
                AutoScroll = false
            };
            toolbar.Controls.Add(flpRapoarte, 0, 0);

            // ── Filtre Echipamente disponibile (Q7) — sub butoane ───
            panelFiltre = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 30,
                MinimumSize = new Size(0, 30),
                BackColor = Color.Transparent,
                Visible = false,  // afisat doar pt raportul "Echipamente disponibile"
                Margin = new Padding(0, 8, 0, 0),
                AutoSize = false
            };
            toolbar.Controls.Add(panelFiltre, 0, 1);

            var rapoarte = new[]
            {
                new { Text = "Echipamente pe sali", Key = "Echipamente pe sali" },
                new { Text = "Sesiuni complete", Key = "Sesiuni complete" },
                new { Text = "Echipamente disponibile", Key = "Echipamente disponibile" },
                new { Text = "Top operatori după încasare", Key = "Top operatori după încasare" },
                new { Text = "Statistici sali", Key = "Statistici sali" },
                new { Text = "🎮 PC Gaming", Key = "🎮 PC Gaming" },
                new { Text = "🏆 Top sesiuni", Key = "🏆 Top sesiuni" }
            };

            foreach (var rap in rapoarte)
            {
                int latime = Math.Max(100,
                    TextRenderer.MeasureText(rap.Text, MaterialTheme.FontButton).Width + 32);
                // Secundar, nu Primary: 7 butoane cyan ar domina ecranul și ar
                // transforma accentul într-un fundal. Accentul rămâne
                // pentru acțiunea principală a fiecărui ecran.
                var btn = MaterialUIHelper.Button(rap.Text, MaterialButtonStyle.Secondary, latime, 32);
                btn.Tag = rap.Key;
                btn.Margin = new Padding(0, 0, 8, 8);
                btn.Click += BtnRaport_Click;
                flpRapoarte.Controls.Add(btn);
            }

            var lblFiltruTip = new Label
            {
                Text = "Tip:",
                Font = MaterialTheme.FontLabel,
                ForeColor = MaterialTheme.TextSecondary,
                Location = new Point(0, 7),
                AutoSize = true
            };
            panelFiltre.Controls.Add(lblFiltruTip);

            cmbTipEchipament = new ComboBox
            {
                Location = new Point(34, 3),
                Size = new Size(140, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = MaterialTheme.FontBody
            };
            cmbTipEchipament.Items.AddRange(new object[]
                { "Toate", "PC Gaming", "Consola", "VR", "Server", "Simulator" });
            cmbTipEchipament.SelectedIndex = 0;
            MaterialUIHelper.StyleInput(cmbTipEchipament);
            panelFiltre.Controls.Add(cmbTipEchipament);

            var lblFiltruLuna = new Label
            {
                Text = "Luna:",
                Font = MaterialTheme.FontLabel,
                ForeColor = MaterialTheme.TextSecondary,
                Location = new Point(186, 7),
                AutoSize = true
            };
            panelFiltre.Controls.Add(lblFiltruLuna);

            cmbLuna = new ComboBox
            {
                Location = new Point(230, 3),
                Size = new Size(140, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = MaterialTheme.FontBody
            };
            cmbLuna.Items.Add("Toate lunile");
            for (int i = 1; i <= 12; i++)
                // FIX_21: lunile în română (ro-RO), nu în limba OS-ului.
                cmbLuna.Items.Add(new DateTime(2025, i, 1).ToString("MMMM", new CultureInfo("ro-RO")));
            cmbLuna.SelectedIndex = 0;
            MaterialUIHelper.StyleInput(cmbLuna);
            panelFiltre.Controls.Add(cmbLuna);

            var btnAplicaFiltru = MaterialUIHelper.Button("Aplică", MaterialButtonStyle.Success, 90, 28);
            btnAplicaFiltru.Location = new Point(382, 1);
            btnAplicaFiltru.Click += async (s, e) =>
                await EfectueazaRaportAsync("Echipamente disponibile");
            panelFiltre.Controls.Add(btnAplicaFiltru);

            // ── Header colorat (Dock.Top) — adaugat ULTIMUL ─────────
            var header = MaterialUIHelper.Header(
                "Rapoarte și statistici", "Selectează un raport din bara de sus",
                null, MaterialTheme.Primary);
            this.Controls.Add(header);
        }

        // ════════════════════════════════════════════════════════════
        //  HANDLER BUTOANE RAPOARTE
        // ════════════════════════════════════════════════════════════
        private async void BtnRaport_Click(object sender, EventArgs e)
        {
            string raport = ((Button)sender).Tag.ToString();

            // Q7: arata/ascunde panelul de filtre.
            // PerformLayout forteaza recalcularea randului AutoSize din
            // toolbar (TableLayoutPanel), altfel inaltimea ramane cea veche.
            panelFiltre.Visible = (raport == "Echipamente disponibile");
            panelFiltre.Parent?.PerformLayout();
            this.PerformLayout();

            await EfectueazaRaportAsync(raport);
        }

        private async Task EfectueazaRaportAsync(string raport)
        {
            string query = BuildQuery(raport);
            if (string.IsNullOrEmpty(query)) return;

            // Filtrele se citesc pe UI-thread inainte de await.
            string tip = null;
            int? luna = null;
            if (raport == "Echipamente disponibile")
            {
                if (cmbTipEchipament != null && cmbTipEchipament.SelectedIndex > 0)
                    tip = cmbTipEchipament.SelectedItem.ToString();
                if (cmbLuna != null && cmbLuna.SelectedIndex > 0)
                    luna = cmbLuna.SelectedIndex;
            }

            try
            {
                DataTable dt = await SqlDb.QueryDataTableAsync(
                    query,
                    p =>
                    {
                        if (raport != "Echipamente disponibile") return;
                        p.Add("@tip", SqlDbType.NVarChar, 30).Value =
                            (object)tip ?? DBNull.Value;
                        p.Add("@luna", SqlDbType.Int).Value =
                            luna.HasValue ? (object)luna.Value : DBNull.Value;
                    },
                    default,
                    SqlDb.ReportTimeoutSeconds);

                if (IsDisposed) return;

                dgvRapoarte.DataSource = dt;
                AjusteazaLatimiColoane();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormRapoarte.EfectueazaRaportAsync");
                if (!IsDisposed)
                {
                    MessageBox.Show("Eroare la generarea raportului: " + ex.Message,
                        "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// Dimensionare coloane dupa continut, cu revenire la Fill cand
        /// raportul e ingust (altfel ramane mult spatiu gol in dreapta).
        /// Previne trunchierea cu "..." din screenshot-uri.
        /// </summary>
        private void AjusteazaLatimiColoane()
        {
            if (dgvRapoarte == null || dgvRapoarte.Columns.Count == 0) return;

            dgvRapoarte.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dgvRapoarte.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);

            int total = 0;
            foreach (DataGridViewColumn c in dgvRapoarte.Columns)
                if (c.Visible) total += c.Width;

            // Daca totul incape lejer, umple latimea gridului ca inainte.
            if (total > 0 && total < dgvRapoarte.ClientSize.Width)
                dgvRapoarte.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            else
                dgvRapoarte.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        }

        private string BuildQuery(string raport)
        {
            switch (raport)
            {
                case "Echipamente pe sali":
                    return @"SELECT b.nume_sala,
                                    b.oras,
                                    COUNT(p.id_echipament)          AS numar_echipamente,
                                    ISNULL(SUM(p.tarif_ora), 0)         AS valoare_portofoliu,
                                    a.nume + ' ' + a.prenume         AS operator_sef
                             FROM Sali b
                             LEFT JOIN Echipamente p ON b.id_sala     = p.sala_id
                             LEFT JOIN Operatori      a ON b.operator_sef_id = a.id_operator
                             GROUP BY b.id_sala, b.nume_sala, b.oras, a.nume, a.prenume
                             ORDER BY numar_echipamente DESC, b.nume_sala";

                case "Sesiuni complete":
                    return "SELECT * FROM vw_SesiuniComplete ORDER BY data_sesiune DESC";

                // Q7: Echipamente disponibile cu filtre dinamice
                // FIX_07: filtre parametrizate (@tip/@luna) — fără concatenare de valori în SQL.
                case "Echipamente disponibile":
                {
                    return @"SELECT id_echipament, tip_echipament, denumire, oras,
                                      putere_watt, ram_gb, tarif_ora, data_adaugare,
                                      nume_sala, operator_sef,
                                      DATEDIFF(DAY, data_adaugare, GETDATE()) AS zile_in_portofoliu
                               FROM vw_EchipamenteDisponibile
                               WHERE 1=1 AND (@tip IS NULL OR tip_echipament = @tip) AND (@luna IS NULL OR MONTH(data_adaugare) = @luna)
                               ORDER BY data_adaugare DESC";
                }

                // Q11: include operatori cu 0 incasare (HAVING eliminat, ISNULL adaugat)
                case "Top operatori după încasare":
                    return @"SELECT
                                 RANK() OVER (ORDER BY ISNULL(SUM(t.incasare), 0) DESC) AS pozitie,
                                 a.nume + ' ' + a.prenume        AS operator,
                                 a.rol_club,
                                 ISNULL(b.nume_sala, '-')       AS sala,
                                 COUNT(t.id_sesiune)          AS numar_sesiuni,
                                 ISNULL(SUM(t.incasare), 0)      AS total_incasare,
                                 ISNULL(AVG(t.cost_total), 0)    AS cost_mediu
                             FROM Operatori a
                             LEFT JOIN Sesiuni t ON a.id_operator = t.id_operator
                             LEFT JOIN Sali    b ON a.sala_id = b.id_sala
                             GROUP BY a.id_operator, a.nume, a.prenume, a.rol_club, b.nume_sala
                             ORDER BY total_incasare DESC, operator";

                case "Statistici sali":
                    return @"SELECT b.nume_sala                      AS sala,
                                    b.oras,
                                    a.nume + ' ' + a.prenume    AS operator_sef,
                                    (SELECT COUNT(*) FROM Operatori ag
                                      WHERE ag.sala_id = b.id_sala)      AS numar_operatori,
                                    (SELECT COUNT(*) FROM Echipamente p
                                      WHERE p.sala_id = b.id_sala)       AS numar_echipamente,
                                    (SELECT COUNT(*) FROM Sesiuni t
                                       JOIN Operatori ag2 ON t.id_operator = ag2.id_operator
                                      WHERE ag2.sala_id = b.id_sala)     AS numar_sesiuni,
                                    ISNULL((SELECT SUM(t2.incasare) FROM Sesiuni t2
                                              JOIN Operatori ag3 ON t2.id_operator = ag3.id_operator
                                             WHERE ag3.sala_id = b.id_sala), 0) AS incasare_total
                             FROM Sali b
                             LEFT JOIN Operatori a ON b.operator_sef_id = a.id_operator
                             ORDER BY incasare_total DESC, b.nume_sala";

                case "🎮 PC Gaming":
                    return @"SELECT p.id_echipament                     AS ID,
                                    p.denumire                            AS Denumire,
                                    p.oras                              AS Oras,
                                    p.putere_watt                      AS Putere_watt,
                                    p.ram_gb                            AS RAM_gb,
                                    p.numar_locuri                      AS Locuri,
                                    ISNULL(CONVERT(VARCHAR, p.an_achizitie), '-') AS An_achizitie,
                                    p.tarif_ora                         AS Tarif_ora,
                                    p.status                           AS Status,
                                    b.nume_sala                       AS Sala,
                                    a.nume + ' ' + a.prenume           AS Operator_sef
                             FROM Echipamente p
                             INNER JOIN Sali b ON p.sala_id       = b.id_sala
                             INNER JOIN Operatori  a ON b.operator_sef_id   = a.id_operator
                             WHERE p.tip_echipament = 'PC Gaming'
                             ORDER BY p.oras, p.tarif_ora DESC";

                case "🏆 Top sesiuni":
                    return @"SELECT TOP 50 b.nume_sala                              AS Sala,
                                    b.oras                                     AS Oras,
                                    p.denumire                                   AS Echipament,
                                    p.tip_echipament                          AS Tip_echipament,
                                    c.nume + ' ' + c.prenume                   AS Membru,
                                    a.nume + ' ' + a.prenume                   AS Operator,
                                    t.tip_sesiune                           AS Tip_sesiune,
                                    CONVERT(VARCHAR, t.data_sesiune, 103)   AS Data,
                                    t.cost_total                                AS Cost_total,
                                    t.incasare                                 AS Incasare,
                                    t.status                                   AS Status,
                                    ISNULL(t.observatii, '-')                  AS Observatii
                             FROM Sesiuni t
                             INNER JOIN Echipamente p ON t.id_echipament = p.id_echipament
                             INNER JOIN Membri     c ON t.id_membru      = c.id_membru
                             INNER JOIN Operatori      a ON t.id_operator       = a.id_operator
                             LEFT JOIN  Sali     b ON a.sala_id       = b.id_sala
                             ORDER BY t.cost_total DESC";

                default:
                    return null;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  Q3 — EXPORT EXCEL (CSV)
        // ════════════════════════════════════════════════════════════
        private void BtnExportExcel_Click(object sender, EventArgs e)
        {
            DataTable dt = dgvRapoarte.DataSource as DataTable;
            if (dt == null)
            {
                MessageBox.Show("Genereaza mai intai un raport!", "Atentie",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter   = "CSV Excel (*.csv)|*.csv|Toate fisierele (*.*)|*.*";
            sfd.FileName = $"Raport_{DateTime.Now:yyyyMMdd_HHmm}.csv";

            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                var sb = new StringBuilder();
                // FIX_10: separatorul listei din cultura curentă (RO „;”, EN „,") — CSV-ul se deschide corect în Excel pe orice locale.
                string sep = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator;

                // FIX BUG-05: NU mai adăugăm manual '\uFEFF' aici. Encoding.UTF8
                // scrie deja BOM-ul la File.WriteAllText, deci rezultatul avea BOM
                // dublu (EF BB BF EF BB BF), iar Excel/LibreOffice afișa „ï»¿" în
                // prima celulă. BOM-ul necesar pentru diacritice rămâne cel scris
                // de Encoding.UTF8.
                sb.AppendLine(string.Join(sep,
                    dt.Columns.Cast<DataColumn>().Select(c => $"\"{c.ColumnName}\"")));

                // Date
                foreach (DataRow row in dt.Rows)
                    sb.AppendLine(string.Join(sep,
                        row.ItemArray.Select(v => $"\"{v?.ToString()?.Replace("\"", "\"\"")}\"")));

                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);

                // FIX BUG-04: dialog propriu cu butoane românești.
                if (DialogHelper.Confirma(this, "Succes",
                    "Exportul a fost realizat cu succes!\n" + sfd.FileName +
                    "\n\nVrei să deschizi fișierul?",
                    "Deschide", "Nu"))
                {
                    // FIX B2: pe .NET 8 Process.Start(path) folosește implicit UseShellExecute=false,
                    // deci tratează .csv-ul drept executabil și eșuează cu „The specified executable
                    // is not a valid application for this OS platform" (nu e lipsa asocierii .csv).
                    // Cu UseShellExecute=true fișierul se deschide prin shell, cu aplicația asociată.
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName)
                        {
                            UseShellExecute = true
                        });
                    }
                    catch (Exception exOpen)
                    {
                        AppLogger.Warn("Nu s-a putut deschide CSV-ul: " + exOpen.Message, "FormRapoarte");
                        MessageBox.Show("Fișierul a fost salvat, dar nu s-a putut deschide automat:\n" + exOpen.Message,
                            "Deschidere eșuată", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }

                AppLogger.Info($"Export CSV: {sfd.FileName}", "FormRapoarte");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormRapoarte.ExportExcel");
                MessageBox.Show("Eroare la export: " + ex.Message, "Eroare",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  Q3 — TIPARIRE / PDF
        // ════════════════════════════════════════════════════════════
        private void BtnPrint_Click(object sender, EventArgs e)
        {
            DataTable dt = dgvRapoarte.DataSource as DataTable;
            if (dt == null)
            {
                MessageBox.Show("Genereaza mai intai un raport!", "Atentie",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int randCurent   = 0;
            int paginaCurenta = 0;

            // FIX_11: fonturile se creează o singură dată (înainte de PrintPage) și se eliberează după previzualizare — fără leak în bucla de pagini.
            using (PrintDocument pd = new PrintDocument())
            using (Font fontTitlu = new Font(MaterialTheme.FontFamily, 10, FontStyle.Bold))
            using (Font fontHeader = new Font(MaterialTheme.FontFamily, 8, FontStyle.Bold))
            using (Font fontData = new Font(MaterialTheme.FontFamily, 8))
            using (Font fontFooter = new Font(MaterialTheme.FontFamily, 7, FontStyle.Italic))
            {
                pd.DefaultPageSettings.Landscape = true;

                pd.PrintPage += (s2, ev) =>
                {
                    Graphics g       = ev.Graphics;
                    float pageW      = ev.PageBounds.Width;
                    float pageH      = ev.PageBounds.Height;
                    float left       = 20f;
                    float lineH      = 20f;
                    float y          = 15f;

                    // Titlu
                    g.DrawString(
                        $"Raport: {this.Text}   |   {DateTime.Now:dd.MM.yyyy HH:mm}   |   " +
                        $"{dt.Rows.Count} inregistrari",
                        fontTitlu, Brushes.DarkBlue, left, y);
                    y += 22f;

                    // Calcul latimi coloane (sarim prima coloana daca pare ID)
                    int colStart = dt.Columns.Count > 1 ? 1 : 0;
                    int numCols  = dt.Columns.Count - colStart;
                    if (numCols <= 0) { ev.HasMorePages = false; return; }
                    float colW   = (pageW - left * 2) / numCols;

                    // Header coloane
                    float xc = left;
                    for (int ci = colStart; ci < dt.Columns.Count; ci++)
                    {
                        using (var brushHeader = new SolidBrush(Color.FromArgb(0x27, 0x27, 0x2A)))
                            g.FillRectangle(brushHeader,
                                new RectangleF(xc, y, colW, lineH));
                        g.DrawString(dt.Columns[ci].ColumnName, fontHeader, Brushes.White,
                            new RectangleF(xc + 2, y + 2, colW - 4, lineH));
                        xc += colW;
                    }
                    y += lineH;

                    // Randuri alternante
                    bool alt = false;
                    while (randCurent < dt.Rows.Count)
                    {
                        if (y + lineH > pageH - 30f)
                        {
                            g.DrawString($"Pagina {paginaCurenta + 1}",
                                fontFooter, Brushes.Gray, pageW - 75f, pageH - 25f);
                            ev.HasMorePages = true;
                            paginaCurenta++;
                            return;
                        }

                        if (alt)
                            using (var brushAlt = new SolidBrush(Color.FromArgb(0xF4, 0xF4, 0xF5)))
                                g.FillRectangle(brushAlt,
                                    new RectangleF(left, y, pageW - left * 2, lineH));

                        xc = left;
                        for (int ci = colStart; ci < dt.Columns.Count; ci++)
                        {
                            g.DrawString(dt.Rows[randCurent][ci]?.ToString() ?? "", fontData,
                                Brushes.Black, new RectangleF(xc + 2, y + 2, colW - 4, lineH));
                            xc += colW;
                        }
                        y += lineH;
                        alt = !alt;
                        randCurent++;
                    }

                    g.DrawString($"Pagina {paginaCurenta + 1}",
                        fontFooter, Brushes.Gray, pageW - 75f, pageH - 25f);
                    ev.HasMorePages = false;
                };

                // PrintPreviewDialog permite atat previzualizarea cat si
                // salvarea ca PDF (daca e instalata o imprimanta PDF, ex: "Microsoft Print to PDF")
                // FIX BUG-14: dialogul nu era eliberat (fără using) → la fiecare
                // previzualizare rămâneau în urmă handle-uri GDI/ferestre ascunse.
                using (var preview = new PrintPreviewDialog())
                {
                    preview.Document    = pd;
                    preview.WindowState = FormWindowState.Maximized;
                    preview.ShowDialog();
                }
            }
            AppLogger.Info($"Tiparire raport: {dt.Rows.Count} randuri", "FormRapoarte");
        }
    }
}
