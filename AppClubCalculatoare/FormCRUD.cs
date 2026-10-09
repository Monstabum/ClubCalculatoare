using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;
using AppClubCalculatoare.UI;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Fereastra CRUD generica — design modern.
    ///
    /// Imbunatatiri functionale (neschimbate):
    ///   Q1  — Camp de cautare (filtru) deasupra tabelului.
    ///   Q4  — Dublu-clic pe un operator afiseaza echipamentele gestionate de el.
    ///   Q8  — Coloana incasare_total_operator calculata in query-ul Sesiuni.
    ///   Q9  — Verificare dependente FK inainte de stergere.
    ///   Q10 — Buton "Vizualizare detalii" pentru utilizatorii cu rol User.
    ///   Q12 — Buton "Tipareste" disponibil pentru toti utilizatorii.
    ///
    /// Design Material:
    ///   - Header colorat + toolbar sus (cautare + refresh + total)
    ///   - Tabel in MaterialCard centrat
    ///   - Bara inferioara split: Adauga/Modifica/Sterge (stanga),
    ///     Tipareste/Inchide (dreapta) — butoane MaterialButton cu
    ///     text mereu vizibil (OnPaint custom).
    /// </summary>
    public partial class FormCRUD : Form
    {
        private string tabela;
        private string connectionString;
        private string rolUtilizator;
        private DataGridView dgv;

        // MODUL_FOTO: cache miniaturi (id_foto -> Image) + placeholder.
        // Nu se reincarca de pe disc la fiecare redesenare; se elibereaza
        // la inchiderea formei (OnFormClosed).
        private readonly Dictionary<int, Image> _cacheFoto = new Dictionary<int, Image>();
        private Image _placeholderFoto;

        // Q1: cache pentru filtru
        private DataTable _dataCache;

        private bool EsteAdmin => rolUtilizator == "Administrator";

        // Filtru pre-aplicat la deschidere (folosit de alertele din tabloul de bord)
        private readonly string filtruInitial;

        public FormCRUD(string tabela, string titlu, string connString, string rol)
            : this(tabela, titlu, connString, rol, null) { }

        public FormCRUD(string tabela, string titlu, string connString, string rol,
            string filtruInitial)
        {
            this.tabela           = tabela;
            this.connectionString = connString;
            this.rolUtilizator    = rol;
            this.filtruInitial    = filtruInitial;
            this.Text             = EsteAdmin ? titlu : titlu + "  [doar vizualizare]";
            this.Size             = new Size(1200, 750);
            this.MinimumSize      = new Size(960, 600);
            this.StartPosition    = FormStartPosition.CenterScreen;
            this.BackColor        = MaterialTheme.Background;
            this.Font             = MaterialTheme.FontBody;

            InitializeComponent();
            IncarcaDate();

            if (!string.IsNullOrWhiteSpace(filtruInitial))
                AplicaFiltruInitial();
        }

        private void AplicaFiltruInitial()
        {
            var gasite = this.Controls.Find("txtCautare", true);
            if (gasite.Length == 0) return;

            var txtCautare = (TextBox)gasite[0];
            txtCautare.Text = filtruInitial;
            txtCautare.ForeColor = MaterialTheme.TextPrimary;
            FiltreazaDate(filtruInitial);
        }

        private Label lblTotal;

        private void InitializeComponent()
        {
            // ── Zona centrala (Dock.Fill) — adaugata PRIMA ─────────
            var content = MaterialUIHelper.ContentPanel();
            this.Controls.Add(content);

            var gridCard = new MaterialCard { Dock = DockStyle.Fill };
            content.Controls.Add(gridCard);

            var gridInner = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.Transparent,
                Padding = new Padding(4)
            };
            gridCard.Controls.Add(gridInner);

            dgv = new DataGridView { Dock = DockStyle.Fill };
            MaterialUIHelper.StyleGrid(dgv, EsteAdmin ? MaterialTheme.Primary : MaterialTheme.Success);
            dgv.CellDoubleClick += DgvOperator_CellDoubleClick;
            dgv.CellFormatting += DgvSesiuni_CellFormatting;
            gridInner.Controls.Add(dgv);

            // ── Bara inferioara (Dock.Bottom): actiuni + inchidere ──
            FlowLayoutPanel leftBottom, rightBottom;
            var bottomBar = MaterialUIHelper.SplitBottomBar(out leftBottom, out rightBottom);
            this.Controls.Add(bottomBar);

            if (EsteAdmin)
            {
                var btnAdauga = MaterialUIHelper.Button("\u2795  Adaug\u0103", MaterialButtonStyle.Success, 130);
                btnAdauga.Click += BtnAdauga_Click;
                leftBottom.Controls.Add(btnAdauga);

                var btnModifica = MaterialUIHelper.Button("\u2710  Modific\u0103", MaterialButtonStyle.Primary, 130);
                btnModifica.Click += BtnModifica_Click;
                leftBottom.Controls.Add(btnModifica);

                var btnSterge = MaterialUIHelper.Button("\U0001F5D1  \u0218terge", MaterialButtonStyle.Danger, 130);
                btnSterge.Click += BtnSterge_Click;
                leftBottom.Controls.Add(btnSterge);
            }
            else
            {
                string codMod = PuncteAcces.CodModificare(tabela);
                string codStg = PuncteAcces.CodStergere(tabela);

                if (codMod != null && PermissionService.AreUI(codMod))
                {
                    var btnAdauga = MaterialUIHelper.Button("\u2795  Adaug\u0103", MaterialButtonStyle.Success, 130);
                    btnAdauga.Click += BtnAdauga_Click;
                    leftBottom.Controls.Add(btnAdauga);

                    var btnModifica = MaterialUIHelper.Button("\u2710  Modific\u0103", MaterialButtonStyle.Primary, 130);
                    btnModifica.Click += BtnModifica_Click;
                    leftBottom.Controls.Add(btnModifica);
                }

                if (codStg != null && PermissionService.AreUI(codStg))
                {
                    var btnSterge = MaterialUIHelper.Button("\U0001F5D1  \u0218terge", MaterialButtonStyle.Danger, 130);
                    btnSterge.Click += BtnSterge_Click;
                    leftBottom.Controls.Add(btnSterge);
                }

                if (!PermissionService.AreUI(codMod) && !PermissionService.AreUI(codStg))
                {
                    var btnDetalii = MaterialUIHelper.Button("\U0001F441  Vizualizare detalii", MaterialButtonStyle.Primary, 200);
                    btnDetalii.Click += BtnDetalii_Click;
                    leftBottom.Controls.Add(btnDetalii);
                }
            }

            var btnPrint = MaterialUIHelper.Button("\U0001F5A8  Tip\u0103re\u0219te", MaterialButtonStyle.Secondary, 140);
            btnPrint.Click += BtnPrint_Click;
            rightBottom.Controls.Add(btnPrint);

            // FAVORITE: comutarea echipamentului selectat in lista de
            // favorite a utilizatorului curent (doar la Echipamente).
            if (tabela == "Echipamente" &&
                PermissionService.AreUI(PuncteAcces.Favorite))
            {
                var btnFav = MaterialUIHelper.Button(
                    "\u2B50  Comut\u0103 favorite", MaterialButtonStyle.Warning, 190);
                btnFav.Click += BtnFavorit_Click;
                leftBottom.Controls.Add(btnFav);
            }

            var btnInchide = MaterialUIHelper.Button("\u2716  \u00cenchide", MaterialButtonStyle.Outline, 120);
            btnInchide.Click += (s, e) => this.Close();
            rightBottom.Controls.Add(btnInchide);

            // ── Toolbar sus (Dock.Top, 60px): cautare + refresh + total
            var toolbar = MaterialUIHelper.Toolbar();
            this.Controls.Add(toolbar);

            var searchBox = new Panel
            {
                Location = new System.Drawing.Point(12, 11),
                Size = new System.Drawing.Size(380, 36),
                BackColor = MaterialTheme.Background,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            toolbar.Controls.Add(searchBox);

            var lblSearchIcon = new Label
            {
                Text = "\U0001F50D",
                Font = new System.Drawing.Font("Segoe UI Emoji", 11f),
                ForeColor = MaterialTheme.TextSecondary,
                AutoSize = true,
                Location = new System.Drawing.Point(10, 8),
                BackColor = System.Drawing.Color.Transparent
            };
            searchBox.Controls.Add(lblSearchIcon);

            var txtCautare = new TextBox
            {
                Name = "txtCautare",
                BorderStyle = BorderStyle.None,
                BackColor = MaterialTheme.Background,
                ForeColor = MaterialTheme.TextSecondary,
                Font = MaterialTheme.FontBody,
                Multiline = true,
                WordWrap = false,
                Text = "Caut\u0103...",
                Location = new System.Drawing.Point(38, 6),
                Size = new System.Drawing.Size(330, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            txtCautare.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) e.SuppressKeyPress = true;
            };
            txtCautare.GotFocus += (s, e) =>
            {
                if (txtCautare.Text == "Caut\u0103...")
                {
                    txtCautare.Text = "";
                    txtCautare.ForeColor = MaterialTheme.TextPrimary;
                }
            };
            txtCautare.LostFocus += (s, e) =>
            {
                if (string.IsNullOrEmpty(txtCautare.Text))
                {
                    txtCautare.Text = "Caut\u0103...";
                    txtCautare.ForeColor = MaterialTheme.TextSecondary;
                }
            };
            txtCautare.TextChanged += (s, e) =>
                FiltreazaDate(txtCautare.Text == "Caut\u0103..." ? "" : txtCautare.Text);
            searchBox.Controls.Add(txtCautare);

            var btnRefresh = MaterialUIHelper.Button("\U0001F504  Re\u00eencarc\u0103", MaterialButtonStyle.Secondary, 140, 36);
            btnRefresh.Location = new System.Drawing.Point(402, 11);
            btnRefresh.Click += (s, e) => IncarcaDate();
            toolbar.Controls.Add(btnRefresh);

            lblTotal = new Label
            {
                Text = "Total: 0 \u00eenregistr\u0103ri",
                Font = MaterialTheme.FontLabel,
                ForeColor = MaterialTheme.TextSecondary,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            toolbar.Controls.Add(lblTotal);
            toolbar.Resize += (s, e) =>
            {
                lblTotal.Location = new System.Drawing.Point(
                    System.Math.Max(560, toolbar.Width - lblTotal.Width - 16), 20);
            };
            lblTotal.Location = new System.Drawing.Point(800, 20);

            if (!string.IsNullOrWhiteSpace(filtruInitial))
            {
                var btnArataTot = MaterialUIHelper.Button("Arat\u0103 tot", MaterialButtonStyle.Success, 110, 36);
                btnArataTot.Location = new System.Drawing.Point(552, 11);
                btnArataTot.Click += (s, e) =>
                {
                    txtCautare.Text = "";
                    txtCautare.ForeColor = MaterialTheme.TextSecondary;
                    txtCautare.Focus();
                };
                toolbar.Controls.Add(btnArataTot);
            }

            // POZE DEMO / IMPORT POZE: doar la Echipamente si DOAR pentru
            // Administrator — utilizatorii (rol User) nu le vad deloc.
            if (tabela == "Echipamente" && EsteAdmin)
            {
                int xDemo = string.IsNullOrWhiteSpace(filtruInitial) ? 552 : 672;
                var btnPozeDemo = MaterialUIHelper.Button(
                    "Poze demo", MaterialButtonStyle.Primary, 140, 36);
                btnPozeDemo.Location = new System.Drawing.Point(xDemo, 11);
                btnPozeDemo.Click += BtnPozeDemo_Click;
                toolbar.Controls.Add(btnPozeDemo);

                // IMPORT POZE: imagini reale din folderul PozeDemo
                // (le inlocuieste pe cele generate procedural).
                var btnImportPoze = MaterialUIHelper.Button(
                    "Import\u0103 poze", MaterialButtonStyle.Secondary, 140, 36);
                btnImportPoze.Location = new System.Drawing.Point(xDemo + 150, 11);
                btnImportPoze.Click += BtnImportPoze_Click;
                toolbar.Controls.Add(btnImportPoze);
            }

            // ── Header colorat (Dock.Top) — adaugat ULTIMUL ─────────
            string subtitlu = EsteAdmin ? "Gestionare \u00eenregistr\u0103ri"
                : "\U0001F512 Mod vizualizare \u2014 modific\u0103rile nu sunt permise.";
            var header = MaterialUIHelper.Header(
                this.Text, subtitlu, null,
                EsteAdmin ? MaterialTheme.Primary : MaterialTheme.Success);
            this.Controls.Add(header);
        }

        private MaterialButton CreeazaButon(string text, MaterialButtonStyle style,
            int width, int height)
        {
            return MaterialUIHelper.Button(text, style, width, height);
        }

        private void ActualizeazaTotal()
        {
            if (lblTotal == null) return;
            int n = 0;
            try
            {
                if (dgv != null && dgv.DataSource is DataTable dt) n = dt.Rows.Count;
                else if (dgv != null && dgv.DataSource is DataView dv) n = dv.Count;
                else if (dgv != null) n = dgv.Rows.Count;
            }
            catch (Exception ex)
            {
                // FIX_02: nu înghițim excepția mut — o logăm.
                AppLogger.Warn("ActualizeazaTotal esuat: " + ex.Message, "FormCRUD");
            }
            lblTotal.Text = "Total: " + n + " înregistrări";
        }

        // ════════════════════════════════════════════════════════════
        //  Q1 — FILTRARE DATE (logică neschimbată)
        // ════════════════════════════════════════════════════════════
        private void FiltreazaDate(string text)
        {
            if (_dataCache == null) return;

            if (string.IsNullOrWhiteSpace(text))
            {
                dgv.DataSource = _dataCache;
                ActualizeazaTotal();
                return;
            }

            string[] coloane;
            switch (tabela)
            {
                case "Echipamente":
                    coloane = new[] { "tip_echipament", "denumire", "oras", "status",
                                      "nume_sala", "operator_sef", "descriere" };
                    break;
                case "Sali":
                    coloane = new[] { "nume_sala", "oras", "adresa", "operator_sef" };
                    break;
                case "Operatori":
                    coloane = new[] { "nume", "prenume", "rol_club", "telefon",
                                      "email", "nume_sala" };
                    break;
                case "Membri":
                    coloane = new[] { "nume", "prenume", "telefon", "email",
                                      "tip_membru", "oras_interes" };
                    break;
                case "Sesiuni":
                    coloane = new[] { "echipament", "membru", "operator",
                                      "tip_sesiune", "status", "observatii" };
                    break;
                case "Rezervari":
                    coloane = new[] { "echipament", "membru", "operator",
                                      "status", "observatii" };
                    break;
                default:
                    coloane = new string[0];
                    break;
            }

            string sigur = EscapeRowFilter(text);
            string filtru = null;

            foreach (string c in coloane)
            {
                if (!_dataCache.Columns.Contains(c)) continue;
                if (_dataCache.Columns[c].DataType != typeof(string)) continue;

                // FIX_02: RowFilter foloseste '*' si '?', nu '%' si '_'.
                // Numele coloanei vine din lista fixa (whitelist), valoarea e escapata.
                string conditie = $"[{c}] LIKE '*{sigur}*'";
                filtru = filtru == null ? conditie : filtru + " OR " + conditie;
            }

            if (filtru == null)
            {
                foreach (DataColumn col in _dataCache.Columns)
                    if (col.DataType == typeof(string))
                    {
                        // FIX_02: RowFilter foloseste '*' si '?', nu '%' si '_'.
                        filtru = $"[{col.ColumnName}] LIKE '*{sigur}*'";
                        break;
                    }
            }

            if (filtru == null) return;

            try
            {
                DataView dv = _dataCache.DefaultView;
                dv.RowFilter = filtru;
                dgv.DataSource = dv;
                ActualizeazaTotal();
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Filtrare esuata: " + ex.Message, "FormCRUD.FiltreazaDate");
            }
        }

        /// <summary>
        /// Escape pentru DataView.RowFilter (LIKE): dublam apostroful si
        /// neutralizam wildcard-urile *, ?, %, [ si ] puse de utilizator,
        /// ca sa nu sparga filtrul sau sa schimbe sensul cautarii.
        /// </summary>
        private static string EscapeRowFilter(string text)
        {
            string s = (text ?? "").Replace("'", "''");
            s = s.Replace("[", "[[]").Replace("]", "[]]");
            s = s.Replace("*", "[*]").Replace("%", "[%]").Replace("?", "[?]");
            return s;
        }

        // ════════════════════════════════════════════════════════════
        //  ÎNCĂRCARE DATE (logica neschimbată)
        // ════════════════════════════════════════════════════════════
        private void IncarcaDate()
        {
            var txtCautare = this.Controls.Find("txtCautare", true);
            if (txtCautare.Length > 0)
            {
                var txt = (TextBox)txtCautare[0];
                txt.Text = "Caută...";
                txt.ForeColor = MaterialTheme.TextSecondary;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query;
                    switch (tabela)
                    {
                        case "Echipamente":
                            query = @"SELECT p.id_echipament,
                                             p.tip_echipament,
                                             p.denumire,
                                             p.oras,
                                             p.putere_watt,
                                             p.ram_gb,
                                             p.numar_locuri,
                                             p.an_achizitie,
                                             p.tarif_ora,
                                             p.status,
                                             b.nume_sala,
                                             a.nume + ' ' + a.prenume AS operator_sef,
                                             CONVERT(VARCHAR, p.data_adaugare, 103) AS data_adaugare,
                                             p.descriere,
                                             (SELECT TOP 1 id_foto FROM EchipamentFoto
                                              WHERE id_echipament = p.id_echipament
                                                AND este_principala = 1) AS id_foto_principala
                                      FROM Echipamente p
                                      JOIN Sali b ON p.sala_id = b.id_sala
                                      LEFT JOIN Operatori a ON b.operator_sef_id = a.id_operator
                                      ORDER BY p.data_adaugare DESC";
                            break;

                        case "Sali":
                            query = @"SELECT b.id_sala, b.nume_sala, b.oras, b.adresa,
                                             a.nume + ' ' + a.prenume AS operator_sef,
                                             CONVERT(VARCHAR, b.data_deschidere, 103) AS data_deschidere
                                      FROM Sali b
                                      LEFT JOIN Operatori a ON b.operator_sef_id = a.id_operator
                                      ORDER BY b.nume_sala";
                            break;

                        case "Membri":
                            query = @"SELECT c.id_membru, c.nume, c.prenume, c.telefon, c.email,
                                             c.tip_membru, c.buget_max, c.oras_interes,
                                             CONVERT(VARCHAR, c.data_inregistrare, 103) AS data_inregistrare
                                      FROM Membri c
                                      ORDER BY c.data_inregistrare DESC";
                            break;

                        case "Sesiuni":
                            query = @"SELECT
                                          t.id_sesiune,
                                          p.denumire                       AS echipament,
                                          c.nume + ' ' + c.prenume        AS membru,
                                          a.nume + ' ' + a.prenume        AS operator,
                                          t.tip_sesiune,
                                          CONVERT(VARCHAR, t.data_sesiune, 103) AS data_sesiune,
                                          t.durata_minute,
                                          t.cost_total,
                                          t.incasare,
                                          t.status,
                                          t.observatii,
                                          (SELECT ISNULL(SUM(t2.incasare), 0)
                                           FROM   Sesiuni t2
                                           WHERE  t2.id_operator = t.id_operator) AS incasare_total_operator
                                      FROM Sesiuni t
                                      JOIN Echipamente p ON t.id_echipament = p.id_echipament
                                      JOIN Membri     c ON t.id_membru      = c.id_membru
                                      JOIN Operatori      a ON t.id_operator       = a.id_operator
                                      ORDER BY t.data_sesiune DESC";
                            break;

                        case "Rezervari":
                            query = @"SELECT
                                          v.id_rezervare,
                                          p.denumire                       AS echipament,
                                          c.nume + ' ' + c.prenume        AS membru,
                                          a.nume + ' ' + a.prenume        AS operator,
                                          CONVERT(VARCHAR, v.data_rezervare, 120) AS data_rezervare,
                                          v.status,
                                          v.observatii
                                      FROM Rezervari v
                                      JOIN Echipamente p ON v.id_echipament = p.id_echipament
                                      JOIN Membri     c ON v.id_membru      = c.id_membru
                                      JOIN Operatori      a ON v.id_operator       = a.id_operator
                                      ORDER BY v.data_rezervare DESC";
                            break;

                        case "Operatori":
                            query = @"SELECT a.id_operator, a.nume, a.prenume, a.rol_club,
                                             a.telefon, a.email,
                                             b.nume_sala,
                                             CONVERT(VARCHAR, a.data_angajare, 103) AS data_angajare
                                      FROM Operatori a
                                      LEFT JOIN Sali b ON a.sala_id = b.id_sala
                                      ORDER BY a.nume";
                            break;

                        default:
                            // FIX: whitelist — tabela vine din cod intern, dar nu
                            // construim SQL din valori arbitrare; tabele necunoscute = eroare.
                            throw new InvalidOperationException(
                                "Tabela necunoscuta pentru listare: " + tabela);
                    }

                    using (SqlDataAdapter da = new SqlDataAdapter(query, conn))
                    {
                        DataTable dt = new DataTable();
                    try
                    {
                        da.Fill(dt);
                    }
                    catch (SqlException exFill)
                    {
                        // MODUL_FOTO: baza fara migrare — lista merge si fara miniaturi.
                        if (tabela == "Echipamente" &&
                            exFill.Message.Contains("EchipamentFoto"))
                        {
                            AppLogger.Warn(
                                "Tabela EchipamentFoto lipseste — lista fara miniaturi. " +
                                "Ruleaza BD\\MigrareFoto.sql.", "FormCRUD.IncarcaDate");
                            da.SelectCommand.CommandText = query.Replace(
                                @",
                                             (SELECT TOP 1 id_foto FROM EchipamentFoto
                                              WHERE id_echipament = p.id_echipament
                                                AND este_principala = 1) AS id_foto_principala",
                                "");
                            dt.Clear();
                            da.Fill(dt);
                        }
                        else throw;
                    }

                    _dataCache = dt;
                    dgv.DataSource = dt;
                    ConfigureazaColoanaFoto();
                    ActualizeazaTotal();
                    // MODUL_FOTO: randurile au fost recreate — eliberam
                    // miniaturile vechi (se recitesc doar cele afisate).
                    GolesteCacheFoto();
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormCRUD.IncarcaDate");
                MessageBox.Show("Eroare la incarcarea datelor: " + ex.Message,
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  MODUL_FOTO — coloana cu miniatura fotografiei principale
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Adauga (o singura data) coloana-imagine "Foto" ca prima coloana
        /// si ascunde coloana tehnica id_foto_principala. Miniaturile vin
        /// din cache (ThumbPentruRand), randurile au inaltimea 62px.
        /// </summary>
        private void ConfigureazaColoanaFoto()
        {
            if (tabela != "Echipamente") return;

            if (dgv.Columns.Contains("id_foto_principala"))
                dgv.Columns["id_foto_principala"].Visible = false;

            if (_dataCache == null || !_dataCache.Columns.Contains("id_foto_principala"))
                return;

            if (!dgv.Columns.Contains("colFoto"))
            {
                var col = new DataGridViewImageColumn
                {
                    Name = "colFoto",
                    HeaderText = "Foto",
                    Width = 64,
                    ImageLayout = DataGridViewImageCellLayout.Zoom,
                    ReadOnly = true
                };
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                dgv.Columns.Insert(0, col);
            }
            dgv.RowTemplate.Height = Math.Max(62, dgv.RowTemplate.Height);
        }

        /// <summary>
        /// Miniatura pentru un rand (din cache; placeholder daca nu are poza).
        /// </summary>
        private Image ThumbPentruRand(int rowIndex)
        {
            try
            {
                if (dgv.Columns.Contains("id_foto_principala"))
                {
                    object val = dgv.Rows[rowIndex].Cells["id_foto_principala"].Value;
                    int idFoto;
                    if (val != null && val != DBNull.Value &&
                        int.TryParse(val.ToString(), out idFoto))
                    {
                        Image dinCache;
                        if (_cacheFoto.TryGetValue(idFoto, out dinCache))
                            return dinCache;

                        string cale = PhotoService.GetCaleFisier(idFoto, "thumb");
                        if (!string.IsNullOrEmpty(cale) && File.Exists(cale))
                        {
                            using (FileStream fs = new FileStream(cale,
                                FileMode.Open, FileAccess.Read, FileShare.Read))
                            using (Image tmp = Image.FromStream(fs))
                            {
                                Image copie = new Bitmap(tmp);
                                _cacheFoto[idFoto] = copie;
                                return copie;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Miniatura indisponibila la randul " + rowIndex +
                    ": " + ex.Message, "FormCRUD.ThumbPentruRand");
            }
            return PlaceholderFoto();
        }

        private Image PlaceholderFoto()
        {
            if (_placeholderFoto != null) return _placeholderFoto;
            Bitmap bmp = new Bitmap(56, 56);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(0xEE, 0xEE, 0xEE));
                using (Font f = new Font("Segoe UI", 8f))
                    g.DrawString("foto", f, Brushes.Gray, new PointF(12f, 20f));
                g.DrawRectangle(Pens.Gray, 0, 0, 55, 55);
            }
            _placeholderFoto = bmp;
            return _placeholderFoto;
        }

        /// <summary>
        /// Elibereaza miniaturile din cache. Se cheama dupa reincarcare
        /// (randurile vechi nu mai sunt afisate) si la inchiderea formei.
        /// </summary>
        private void GolesteCacheFoto()
        {
            foreach (Image img in _cacheFoto.Values)
            {
                if (img != null) img.Dispose();
            }
            _cacheFoto.Clear();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            GolesteCacheFoto();
            if (_placeholderFoto != null)
            {
                _placeholderFoto.Dispose();
                _placeholderFoto = null;
            }
            base.OnFormClosed(e);
        }

        // ════════════════════════════════════════════════════════════
        //  Q4 — DUBLU-CLIC PE OPERATOR (neschimbat)
        // ════════════════════════════════════════════════════════════
        private void DgvOperator_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (tabela != "Operatori" || e.RowIndex < 0) return;

            int idOperator = GetIdDinRand(dgv.Rows[e.RowIndex]);
            string numeOperator = "";

            if (dgv.Columns.Contains("nume") && dgv.Columns.Contains("prenume"))
                numeOperator = $"{dgv.Rows[e.RowIndex].Cells["nume"].Value} " +
                            $"{dgv.Rows[e.RowIndex].Cells["prenume"].Value}";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    const string query = @"
                        SELECT DISTINCT
                               p.id_echipament,
                               p.tip_echipament,
                               p.denumire,
                               p.oras,
                               p.putere_watt,
                               p.tarif_ora,
                               p.status,
                               b.nume_sala
                        FROM   Echipamente p
                        JOIN   Sali b ON p.sala_id = b.id_sala
                        WHERE  b.id_sala IN (SELECT id_sala FROM Sali
                                              WHERE operator_sef_id = @id)
                           OR  b.id_sala = (SELECT sala_id FROM Operatori
                                             WHERE id_operator = @id)
                           OR  p.id_echipament IN (SELECT id_echipament FROM Sesiuni
                                                    WHERE id_operator = @id)
                        ORDER BY p.oras, p.denumire";

                    DataTable dt;
                    using (var da = new SqlDataAdapter(query, conn))
                    {
                        da.SelectCommand.Parameters.Add("@id", SqlDbType.Int).Value = idOperator;
                        dt = new DataTable();
                        da.Fill(dt);
                    }

                    Form frmEchipamente = new Form();
                    frmEchipamente.Text          = $"Echipamentele gestionate de {numeOperator}";
                    frmEchipamente.Size          = new Size(820, 460);
                    frmEchipamente.StartPosition = FormStartPosition.CenterParent;
                    frmEchipamente.BackColor     = MaterialTheme.Background;
                    frmEchipamente.Font          = MaterialTheme.FontBody;

                    Label lbl = new Label
                    {
                        Text      = $"Operator: {numeOperator}  |  Echipamente: {dt.Rows.Count}",
                        Font      = MaterialTheme.FontSubtitle,
                        ForeColor = MaterialTheme.Primary,
                        Dock      = DockStyle.Top,
                        Height    = 34,
                        TextAlign = ContentAlignment.MiddleLeft,
                        Padding   = new Padding(14, 0, 0, 0)
                    };

                    var dgvE = new DataGridView { Dock = DockStyle.Fill };
                    MaterialUIHelper.StyleGrid(dgvE, MaterialTheme.Primary);
                    dgvE.DataSource = dt;

                    var card = new Panel
                    {
                        Dock      = DockStyle.Fill,
                        BackColor = MaterialTheme.Surface,
                        Padding   = new Padding(14)
                    };

                    frmEchipamente.Controls.Add(card);
                    card.Controls.Add(dgvE);
                    card.Controls.Add(lbl);

                    frmEchipamente.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormCRUD.DgvOperator_CellDoubleClick");
                MessageBox.Show("Eroare: " + ex.Message, "Eroare",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  Q8 — EVIDENȚIERE COLOANĂ INCASARE TOTAL OPERATOR
        // ════════════════════════════════════════════════════════════
        private void DgvSesiuni_CellFormatting(object sender,
            DataGridViewCellFormattingEventArgs e)
        {
            // MODUL_FOTO: miniatura fotografiei principale (Echipamente).
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 &&
                dgv.Columns[e.ColumnIndex].Name == "colFoto")
            {
                e.Value = ThumbPentruRand(e.RowIndex);
                e.FormattingApplied = true;
                return;
            }
            if (tabela != "Sesiuni" || e.RowIndex < 0) return;
            if (dgv.Columns[e.ColumnIndex].Name == "incasare_total_operator")
                e.CellStyle.BackColor = Color.FromArgb(0xFE, 0xF3, 0xC7);
        }

        // ════════════════════════════════════════════════════════════
        //  ACȚIUNI CRUD
        // ════════════════════════════════════════════════════════════
        private void BtnAdauga_Click(object sender, EventArgs e)
        {
            // FIX: drepturile granulare (MODIFICARE_*) trebuie sa functioneze si
            // pentru non-admin — verificarea se face prin permisiuni, nu prin rol.
            if (!VerificaPermisiuneActiune(PuncteAcces.CodModificare(tabela))) return;
            var frm = new FormEditare(tabela, connectionString, -1);
            if (frm.ShowDialog() == DialogResult.OK) IncarcaDate();
        }

        // POZE DEMO: genereaza 2 imagini/obiect pentru echipamentele goale.
        private void BtnPozeDemo_Click(object sender, EventArgs e)
        {
            if (!DemoPoze.AreDrept())
            {
                PermissionService.AfiseazaMesajAcces();
                return;
            }
            List<int> goale = DemoPoze.EchipamenteFaraPoze();
            if (goale.Count == 0)
            {
                MessageBox.Show(
                    "Toate echipamentele au deja fotografii.",
                    "Poze demo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(
                    string.Format(
                        "Se genereaza 2 poze demo pentru {0} echipamente fara fotografii?",
                        goale.Count),
                    "Poze demo", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes) return;

            this.Cursor = Cursors.WaitCursor;
            try
            {
                string mesaj;
                int adaugate = DemoPoze.GenereazaPozeDemo(out mesaj);
                MessageBox.Show(mesaj, "Poze demo", MessageBoxButtons.OK,
                    adaugate > 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                IncarcaDate();
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        // IMPORT POZE: adauga imaginile reale din folderul PozeDemo,
        // inlocuind fotografiile existente (cele procedurale "demo").
        private void BtnImportPoze_Click(object sender, EventArgs e)
        {
            if (!ImportPozeFolder.AreDrept())
            {
                PermissionService.AfiseazaMesajAcces();
                return;
            }

            string folder = ImportPozeFolder.GasesteFolder();
            if (folder == null)
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description =
                        "Selecteaza folderul cu fotografiile de importat (ex. PozeDemo):";
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    folder = dlg.SelectedPath;
                }
            }

            Dictionary<int, List<string>> plan =
                ImportPozeFolder.ConstruiestePlan(folder);
            int nrFisiere = 0;
            if (plan != null)
                foreach (List<string> fisiere in plan.Values)
                    nrFisiere += fisiere.Count;
            if (nrFisiere == 0)
            {
                MessageBox.Show(
                    "Folderul selectat nu contine imagini importabile " +
                    "(.jpg, .jpeg, .png, .bmp, .webp).",
                    "Import poze", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string numeFolder = Path.GetFileName(folder.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (MessageBox.Show(
                    string.Format(
                        "Se importa {0} imagini din \"{1}\" pentru {2} echipamente?\n\n" +
                        "Fotografiile existente ale acestor echipamente vor fi " +
                        "sterse si inlocuite.",
                        nrFisiere, numeFolder, plan.Count),
                    "Import poze", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes) return;

            this.Cursor = Cursors.WaitCursor;
            try
            {
                string mesaj;
                int adaugate = ImportPozeFolder.Importa(folder, true, out mesaj);
                MessageBox.Show(mesaj, "Import poze", MessageBoxButtons.OK,
                    adaugate > 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                IncarcaDate();
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private void BtnModifica_Click(object sender, EventArgs e)
        {
            // FIX: vezi BtnAdauga_Click — non-adminul cu MODIFICARE_* are drept de editare.
            if (!VerificaPermisiuneActiune(PuncteAcces.CodModificare(tabela))) return;
            if (dgv.SelectedRows.Count == 0)
            {
                MessageBox.Show("Selecteaza o inregistrare pentru modificare!",
                    "Atentie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            int id = GetIdDinRand(dgv.SelectedRows[0]);
            if (id < 0) return;
            var frm = new FormEditare(tabela, connectionString, id);
            DialogResult rezultat = frm.ShowDialog();
            // MODUL_FOTO: pozele se salveaza direct din tab-ul Fotografii,
            // chiar si la Cancel — reincarcam lista Echipamente mereu.
            if (rezultat == DialogResult.OK || tabela == "Echipamente") IncarcaDate();
        }

        private void BtnSterge_Click(object sender, EventArgs e)
        {
            // FIX: vezi BtnAdauga_Click — non-adminul cu STERGE_* are drept de stergere.
            if (!VerificaPermisiuneActiune(PuncteAcces.CodStergere(tabela))) return;
            if (dgv.SelectedRows.Count == 0)
            {
                MessageBox.Show("Selecteaza o inregistrare pentru stergere!",
                    "Atentie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int id = GetIdDinRand(dgv.SelectedRows[0]);
            if (id < 0) return;

            // MODUL_FOTO: colFoto a mutat indicii — numele se ia dupa coloana.
            string nume = "";
            DataGridViewRow randSelectat = dgv.SelectedRows[0];
            if (dgv.Columns.Contains("denumire") && randSelectat.Cells["denumire"].Value != null)
                nume = randSelectat.Cells["denumire"].Value.ToString();
            else if (dgv.Columns.Contains("adresa") && randSelectat.Cells["adresa"].Value != null)
                nume = randSelectat.Cells["adresa"].Value.ToString();
            else if (randSelectat.Cells.Count > 1 && randSelectat.Cells[1].Value != null)
                nume = randSelectat.Cells[1].Value.ToString();

            string mesajDep = VerificaDependente(id);
            if (mesajDep != null)
            {
                MessageBox.Show(
                    $"Nu se poate sterge inregistrarea '{nume}'!\n\n{mesajDep}\n\n" +
                    "Sterge mai intai inregistrarile asociate.",
                    "Stergere blocata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show($"Esti sigur ca vrei sa stergi '{nume}'?",
                    "Confirmare stergere", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.Yes)
                StergeInregistrare(id);
        }

        // ════════════════════════════════════════════════════════════
        //  Q9 — VERIFICARE DEPENDENȚE FK (neschimbat)
        // ════════════════════════════════════════════════════════════
        private string VerificaDependente(int id)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = null;
                    string mesaj = null;

                    switch (tabela)
                    {
                        case "Operatori":
                            query = @"SELECT
                                          (SELECT COUNT(*) FROM Sali    WHERE operator_sef_id = @id)
                                        + (SELECT COUNT(*) FROM Sesiuni WHERE id_operator    = @id)
                                        + (SELECT COUNT(*) FROM Rezervari  WHERE id_operator    = @id)";
                            mesaj = "Acest operator este asociat cu {0} inregistrare/inregistrari " +
                                    "(sali conduse, sesiuni sau rezervari).";
                            break;
                        case "Sali":
                            query = @"SELECT
                                          (SELECT COUNT(*) FROM Echipamente WHERE sala_id = @id)
                                        + (SELECT COUNT(*) FROM Operatori      WHERE sala_id = @id)";
                            mesaj = "Aceasta sală are {0} inregistrare/inregistrari asociate.";
                            break;
                        case "Echipamente":
                            query = @"SELECT
                                          (SELECT COUNT(*) FROM Sesiuni WHERE id_echipament = @id)
                                        + (SELECT COUNT(*) FROM Rezervari  WHERE id_echipament = @id)";
                            mesaj = "Acest echipament are {0} sesiune/sesiuni sau rezervari.";
                            break;
                        case "Membri":
                            query = @"SELECT
                                          (SELECT COUNT(*) FROM Sesiuni WHERE id_membru = @id)
                                        + (SELECT COUNT(*) FROM Rezervari  WHERE id_membru = @id)";
                            mesaj = "Acest membru are {0} sesiune/sesiuni sau rezervari.";
                            break;
                        default:
                            return null;
                    }

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
                        int count = (int)cmd.ExecuteScalar();
                        return count > 0 ? string.Format(mesaj, count) : null;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormCRUD.VerificaDependente");
                return null;
            }
        }

        private void StergeInregistrare(int id)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // MODUL_FOTO: curata fisierele (FK CASCADE sterge randurile,
                    // dar folderele de pe disc raman fara apelul de mai jos).
                    if (tabela == "Echipamente")
                    {
                        try { PhotoService.StergeToateFotos(id); }
                        catch (Exception exFoto)
                        {
                            AppLogger.Warn("Curatare foto esuata la stergerea " +
                                "echipamentului " + id + ": " + exFoto.Message,
                                "FormCRUD.StergeInregistrare");
                        }
                    }

                    string coloanaId;
                    switch (tabela)
                    {
                        case "Echipamente": coloanaId = "id_echipament"; break;
                        case "Sali":     coloanaId = "id_sala";       break;
                        case "Membri":     coloanaId = "id_membru";      break;
                        case "Sesiuni":  coloanaId = "id_sesiune";  break;
                        case "Operatori":      coloanaId = "id_operator";       break;
                        case "Rezervari":   coloanaId = "id_rezervare";   break;
                        default:
                            // FIX: whitelist — nu construim DELETE din nume arbitrare.
                            throw new InvalidOperationException(
                                "Tabela necunoscuta pentru stergere: " + tabela);
                    }

                    // FIX: SqlCommand eliberat prin using; tabela/coloana vin din whitelist.
                    using (var cmd = new SqlCommand(
                        $"DELETE FROM {tabela} WHERE {coloanaId} = @id", conn))
                    {
                        cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
                        cmd.ExecuteNonQuery();
                    }

                    // FIX_18: DELETE se înregistrează în AuditLog — ca INSERT/UPDATE din FormEditare.
                    SalveazaAuditStergere(conn, id);

                    AppLogger.Info(
                        $"DELETE {tabela} id={id} de catre '{SessionInfo.Username}'",
                        "FormCRUD");
                    MessageBox.Show("Inregistrare stearsa cu succes!",
                        "Succes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    IncarcaDate();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormCRUD.StergeInregistrare");
                MessageBox.Show("Eroare la stergere: " + ex.Message,
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // FIX_18: audit pentru DELETE — același mecanism ca SalveazaAudit din FormEditare (nu blochează fluxul la eșec).
        private void SalveazaAuditStergere(SqlConnection conn, int idRec)
        {
            try
            {
                const string sql = @"
                    IF OBJECT_ID('AuditLog','U') IS NOT NULL
                        INSERT INTO AuditLog (tabela, id_inregistrare, actiune, username)
                        VALUES (@tabela, @id, 'DELETE', @user)";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@tabela", SqlDbType.NVarChar, 50).Value = tabela;
                    cmd.Parameters.Add("@id", SqlDbType.Int).Value = idRec;
                    cmd.Parameters.Add("@user", SqlDbType.NVarChar, 50).Value = SessionInfo.Username;
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Audit DELETE esuat: " + ex.Message, "FormCRUD");
            }
        }

        // ════════════════════════════════════════════════════════════
        //  Q10 — VIZUALIZARE DETALII
        // ════════════════════════════════════════════════════════════
        private void BtnDetalii_Click(object sender, EventArgs e)
        {
            if (dgv.SelectedRows.Count == 0)
            {
                MessageBox.Show("Selecteaza o inregistrare pentru a vedea detaliile!",
                    "Atentie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            int id = GetIdDinRand(dgv.SelectedRows[0]);
            if (id < 0) return;
            var frm = new FormEditare(tabela, connectionString, id, readOnly: true);
            frm.ShowDialog();
        }

        // ══════════════════════════════════════════════════════════
        //  FAVORITE — comutarea echipamentului selectat in lista
        //  de favorite a utilizatorului curent (tabela Favorite).
        // ══════════════════════════════════════════════════════════
        private void BtnFavorit_Click(object sender, EventArgs e)
        {
            if (!VerificaPermisiuneActiune(PuncteAcces.Favorite)) return;

            if (dgv.SelectedRows.Count == 0)
            {
                MessageBox.Show("Selecteaza un echipament din lista pentru favorite!",
                    "Atentie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            int id = GetIdDinRand(dgv.SelectedRows[0]);
            if (id < 0) return;

            try
            {
                bool acumFavorit = FavoriteService.Comuteaza(id);
                MessageBox.Show(
                    acumFavorit
                        ? "Echipamentul a fost adaugat la favorite!\n" +
                          "O gasesti in meniul „⭐ Favorite”."
                        : "Echipamentul a fost eliminat din favorite.",
                    "Favorite", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                // 208 = invalid object name (tabela Favorite nu exista)
                AppLogger.Error(ex, "FormCRUD.BtnFavorit_Click");
                MessageBox.Show(
                    "Tabela Favorite nu exista in baza de date.\n" +
                    "Ruleaza scriptul BD\\Favorite.sql si incearca din nou.",
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormCRUD.BtnFavorit_Click");
                MessageBox.Show("Nu s-a putut actualiza lista de favorite: " + ex.Message,
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  Q12 — TIPĂRIRE (neschimbat)
        // ════════════════════════════════════════════════════════════
        private void BtnPrint_Click(object sender, EventArgs e)
        {
            if (_dataCache == null || _dataCache.Rows.Count == 0)
            {
                MessageBox.Show("Nu exista date de tiparit!", "Atentie",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int randCurent = 0;
            int paginaCurenta = 0;

            // FIX_11: fonturile se creează o singură dată (înainte de PrintPage) și se eliberează după previzualizare — fără leak în bucla de pagini.
            using (PrintDocument pd = new PrintDocument())
            using (Font fontTitlu = new Font(MaterialTheme.FontFamily, 10, FontStyle.Bold))
            using (Font fontHeader = new Font(MaterialTheme.FontFamily, 9, FontStyle.Bold))
            using (Font fontData = new Font(MaterialTheme.FontFamily, 8))
            using (Font fontFooter = new Font(MaterialTheme.FontFamily, 7, FontStyle.Italic))
            {
                pd.DefaultPageSettings.Landscape = true;

                pd.PrintPage += (s2, ev) =>
                {
                    DataTable dt = _dataCache;
                    Graphics g = ev.Graphics;
                    float lineH = 20f;
                    float y = 10f;
                    float pageW = ev.PageBounds.Width;
                    float pageH = ev.PageBounds.Height;
                    float leftMargin = 20f;

                    g.DrawString(
                        $"Tabel: {tabela}   |   {DateTime.Now:dd.MM.yyyy HH:mm}   |   " +
                        $"Total inregistrari: {dt.Rows.Count}",
                        fontTitlu, Brushes.DarkBlue, leftMargin, y);
                    y += 22f;

                    int colStart = dt.Columns.Count > 1 ? 1 : 0;
                    int numCols = dt.Columns.Count - colStart;
                    float colW = (pageW - leftMargin * 2) / numCols;

                    float xc = leftMargin;
                    for (int ci = colStart; ci < dt.Columns.Count; ci++)
                    {
                        using (var brushHeader = new SolidBrush(Color.FromArgb(0x3F, 0x51, 0xB5)))
                            g.FillRectangle(brushHeader,
                                new RectangleF(xc, y, colW, lineH));
                        g.DrawString(dt.Columns[ci].ColumnName, fontHeader,
                            Brushes.White, new RectangleF(xc + 2, y + 2, colW - 4, lineH));
                        xc += colW;
                    }
                    y += lineH;

                    bool alt = false;
                    while (randCurent < dt.Rows.Count)
                    {
                        if (y + lineH > pageH - 30f)
                        {
                            g.DrawString($"Pagina {paginaCurenta + 1}",
                                fontFooter, Brushes.Gray,
                                pageW - 80f, pageH - 25f);
                            ev.HasMorePages = true;
                            paginaCurenta++;
                            return;
                        }

                        if (alt)
                            using (var brushAlt = new SolidBrush(Color.FromArgb(240, 245, 255)))
                                g.FillRectangle(brushAlt,
                                    new RectangleF(leftMargin, y, pageW - leftMargin * 2, lineH));

                        xc = leftMargin;
                        for (int ci = colStart; ci < dt.Columns.Count; ci++)
                        {
                            g.DrawString(
                                dt.Rows[randCurent][ci]?.ToString() ?? "",
                                fontData, Brushes.Black,
                                new RectangleF(xc + 2, y + 2, colW - 4, lineH));
                            xc += colW;
                        }
                        y += lineH;
                        alt = !alt;
                        randCurent++;
                    }

                    g.DrawString($"Pagina {paginaCurenta + 1}",
                        fontFooter, Brushes.Gray,
                        pageW - 80f, pageH - 25f);
                    ev.HasMorePages = false;
                };

                PrintPreviewDialog preview = new PrintPreviewDialog();
                preview.Document = pd;
                preview.WindowState = FormWindowState.Maximized;
                preview.ShowDialog();
            }
            AppLogger.Info($"Tiparire CRUD {tabela}: {_dataCache.Rows.Count} randuri", "FormCRUD");
        }

        // ════════════════════════════════════════════════════════════
        //  HELPER: găsește ID după numele coloanei
        // ════════════════════════════════════════════════════════════
        private int GetIdDinRand(DataGridViewRow rand)
        {
            string numeColoanaId;
            switch (tabela)
            {
                case "Echipamente": numeColoanaId = "id_echipament"; break;
                case "Sali":     numeColoanaId = "id_sala";       break;
                case "Membri":     numeColoanaId = "id_membru";      break;
                case "Sesiuni":  numeColoanaId = "id_sesiune";  break;
                case "Operatori":      numeColoanaId = "id_operator";       break;
                case "Rezervari":   numeColoanaId = "id_rezervare";   break;
                default:            numeColoanaId = "id";             break;
            }

            if (dgv.Columns.Contains(numeColoanaId))
            {
                object val = rand.Cells[numeColoanaId].Value;
                int idOut;
                if (val != null && int.TryParse(val.ToString(), out idOut))
                    return idOut;
            }

            object valPrima = rand.Cells[0].Value;
            int idPrima;
            if (valPrima != null && int.TryParse(valPrima.ToString(), out idPrima))
                return idPrima;

            MessageBox.Show("Nu s-a putut determina ID-ul inregistrarii.",
                "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return -1;
        }

        private void MesajAccesNepermis()
        {
            MessageBox.Show(
                "Acces interzis!\n\nContul tau are doar drepturi de vizualizare.\n" +
                "Contacteaza administratorul pentru modificari.",
                "Acces restrictionat", MessageBoxButtons.OK, MessageBoxIcon.Stop);
        }

        private bool VerificaPermisiuneActiune(string cod)
        {
            if (PermissionService.VerificaAre(cod, EsteAdmin)) return true;

            AppLogger.Security(
                $"Acces blocat in FormCRUD: '{SessionInfo.Username}' -> {cod ?? "(fara cod)"}");
            PermissionService.AfiseazaMesajAcces();
            return false;
        }
    }
}
