using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using AppClubCalculatoare.UI;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Lista de ECHIPAMENTE FAVORITE ale utilizatorului curent
    /// (tabela Favorite). Design Material, acelasi limbaj vizual
    /// ca FormCRUD, dar fara CRUD: doar vizualizare, deschiderea
    /// detaliilor (read-only) si eliminarea din favorite.
    ///
    /// Permisiune necesara: PuncteAcces.Favorite (verificat la
    /// deschidere in FormMainCuLogin, ca orice alt punct de acces).
    /// </summary>
    public class FormFavorite : Form
    {
        private readonly string connectionString = DatabaseConfig.ConnectionString;
        private DataGridView dgv;
        private DataTable _dataCache;
        private Label lblTotal;
        private TextBox txtCautare;

        public FormFavorite()
        {
            this.Text          = "Echipamentele mele favorite";
            this.Size          = new Size(1100, 700);
            this.MinimumSize   = new Size(860, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor     = MaterialTheme.Background;
            this.Font          = MaterialTheme.FontBody;

            // DPI: scalare uniformă (fereastra este DPI-aware) + titlu închis.
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode       = AutoScaleMode.Dpi;
            WindowChrome.EnableDarkTitleBar(this);

            InitializeComponent();
            IncarcaDate();
        }

        private void InitializeComponent()
        {
            // ── Zona centrala (Dock.Fill) — adaugata PRIMA ─────────
            var content = MaterialUIHelper.ContentPanel();
            this.Controls.Add(content);

            var gridCard = new MaterialCard { Dock = DockStyle.Fill };
            content.Controls.Add(gridCard);

            var gridInner = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding   = new Padding(4)
            };
            gridCard.Controls.Add(gridInner);

            dgv = new DataGridView { Dock = DockStyle.Fill };
            MaterialUIHelper.StyleGrid(dgv, MaterialTheme.Accent);
            dgv.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) BtnDetalii_Click(s, e); };
            gridInner.Controls.Add(dgv);

            // ── Bara inferioara (Dock.Bottom) ──────────────────────
            FlowLayoutPanel leftBottom, rightBottom;
            var bottomBar = MaterialUIHelper.SplitBottomBar(out leftBottom, out rightBottom);
            this.Controls.Add(bottomBar);

            var btnDetalii = MaterialUIHelper.Button("\U0001F441  Vizualizare detalii",
                MaterialButtonStyle.Primary, 200);
            btnDetalii.Click += BtnDetalii_Click;
            leftBottom.Controls.Add(btnDetalii);

            var btnElimina = MaterialUIHelper.Button("\u2B50  Elimină din favorite",
                MaterialButtonStyle.Danger, 220);
            btnElimina.Click += BtnElimina_Click;
            leftBottom.Controls.Add(btnElimina);

            var btnInchide = MaterialUIHelper.Button("\u2716  \u00cenchide",
                MaterialButtonStyle.Outline, 120);
            btnInchide.Click += (s, e) => this.Close();
            rightBottom.Controls.Add(btnInchide);
            this.CancelButton = btnInchide;

            // ── Toolbar sus (Dock.Top): cautare + refresh + total ──
            var toolbar = MaterialUIHelper.Toolbar();
            this.Controls.Add(toolbar);

            var searchBox = new Panel
            {
                Location = new Point(12, 10),
                Size     = new Size(380, 36),
                Anchor   = AnchorStyles.Top | AnchorStyles.Left
            };
            MaterialUIHelper.StyleSearchBox(searchBox);
            toolbar.Controls.Add(searchBox);

            var lblSearchIcon = new Label
            {
                Text      = "\uE721",   // glif Segoe Fluent Icons: lupă
                Font      = new Font("Segoe Fluent Icons", 14f),
                ForeColor = MaterialTheme.TextSecondary,
                AutoSize  = true,
                Location  = new Point(10, 8),
                BackColor = Color.Transparent
            };
            searchBox.Controls.Add(lblSearchIcon);

            txtCautare = new TextBox
            {
                Name        = "txtCautare",
                BorderStyle = BorderStyle.None,
                BackColor   = Design.SurfaceRaised,
                ForeColor   = Design.TextDim,
                Font        = Design.FontBody,
                Multiline   = true,
                WordWrap    = false,
                Text        = "Caut\u0103...",
                Location    = new Point(38, 6),
                Size        = new Size(330, 24),
                Anchor      = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            txtCautare.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) e.SuppressKeyPress = true; };
            txtCautare.GotFocus += (s, e) =>
            {
                if (txtCautare.Text == "Caut\u0103...")
                {
                    txtCautare.Text = "";
                    txtCautare.ForeColor = Design.Text;
                }
            };
            txtCautare.LostFocus += (s, e) =>
            {
                if (string.IsNullOrEmpty(txtCautare.Text))
                {
                    txtCautare.Text = "Caut\u0103...";
                    txtCautare.ForeColor = Design.TextDim;
                }
            };
            txtCautare.TextChanged += (s, e) =>
                FiltreazaDate(txtCautare.Text == "Caut\u0103..." ? "" : txtCautare.Text);
            searchBox.Controls.Add(txtCautare);

            var btnRefresh = MaterialUIHelper.Button("\U0001F504  Re\u00eencarc\u0103",
                MaterialButtonStyle.Secondary, 140);
            btnRefresh.Location = new Point(402, 11);
            btnRefresh.Click += (s, e) => IncarcaDate();
            toolbar.Controls.Add(btnRefresh);

            lblTotal = new Label
            {
                Text      = "Total: 0 \u00eenregistr\u0103ri",
                Font      = MaterialTheme.FontLabel,
                ForeColor = MaterialTheme.TextSecondary,
                AutoSize  = true,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            toolbar.Controls.Add(lblTotal);
            toolbar.Resize += (s, e) =>
            {
                lblTotal.Location = new Point(
                    Math.Max(560, toolbar.Width - lblTotal.Width - 16), 20);
            };
            lblTotal.Location = new Point(700, 20);

            // ── Header colorat (Dock.Top) — adaugat ULTIMUL ───────
            var header = MaterialUIHelper.Header(
                this.Text,
                "Echipamente salvate în lista ta de favorite — " + SessionInfo.NumeComplet,
                null, MaterialTheme.Accent);
            this.Controls.Add(header);
        }

        // ══════════════════════════════════════════════════════════
        //  INCARCARE / FILTRARE
        // ══════════════════════════════════════════════════════════
        private void IncarcaDate()
        {
            txtCautare.Text = "Caut\u0103...";
            txtCautare.ForeColor = MaterialTheme.TextSecondary;

            try
            {
                const string query = @"
                    SELECT p.id_echipament,
                           p.tip_echipament,
                           p.denumire,
                           p.oras,
                           p.putere_watt,
                           p.ram_gb,
                           p.tarif_ora,
                           p.status,
                           b.nume_sala,
                           a.nume + ' ' + a.prenume AS operator_sef,
                           CONVERT(VARCHAR, f.data_adaugare, 103) AS data_adugarii
                    FROM   Favorite f
                    JOIN   Utilizatori u ON u.id_utilizator = f.id_utilizator
                    JOIN   Echipamente p ON p.id_echipament = f.id_echipament
                    LEFT   JOIN Sali b ON p.sala_id = b.id_sala
                    LEFT   JOIN Operatori  a ON b.operator_sef_id = a.id_operator
                    WHERE  u.username = @user
                    ORDER  BY f.data_adaugare DESC";

                using (var conn = SqlDb.CreateConnection())
                using (var da = new SqlDataAdapter(query, conn))
                {
                    da.SelectCommand.Parameters.Add("@user", SqlDbType.NVarChar, 50)
                                      .Value = SessionInfo.Username;
                    DataTable dt = new DataTable();
                    da.Fill(dt);

                    _dataCache = dt;
                    dgv.DataSource = dt;
                    ActualizeazaTotal();
                }
            }
            catch (SqlException ex)
            {
                bool tabelaLipsa = ex.Message.IndexOf("Favorite", StringComparison.OrdinalIgnoreCase) >= 0;
                AppLogger.Error(ex, "FormFavorite.IncarcaDate");
                MessageBox.Show(
                    (tabelaLipsa
                        ? "Tabela Favorite nu exista in baza de date.\n" +
                          "Ruleaza scriptul BD\\Favorite.sql si incearca din nou.\n\n"
                        : "Eroare la incarcarea datelor: ") + ex.Message,
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormFavorite.IncarcaDate");
                MessageBox.Show("Eroare la incarcarea datelor: " + ex.Message,
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                AppLogger.Warn("ActualizeazaTotal esuat: " + ex.Message, "FormFavorite");
            }
            lblTotal.Text = "Total: " + n + (n == 1 ? " echipament" : " echipamente");
        }

        private void FiltreazaDate(string text)
        {
            if (_dataCache == null) return;

            if (string.IsNullOrWhiteSpace(text))
            {
                dgv.DataSource = _dataCache;
                ActualizeazaTotal();
                return;
            }

            string sigur = text.Replace("'", "''");
            string filtru = null;
            string[] coloane = { "tip_echipament", "denumire", "oras", "status",
                                 "nume_sala", "operator_sef" };

            foreach (string c in coloane)
            {
                if (!_dataCache.Columns.Contains(c)) continue;
                if (_dataCache.Columns[c].DataType != typeof(string)) continue;
                string conditie = $"{c} LIKE '*{sigur}*'";
                filtru = filtru == null ? conditie : filtru + " OR " + conditie;
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
                AppLogger.Warn("Filtrare esuata: " + ex.Message, "FormFavorite.FiltreazaDate");
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ACTIUNI
        // ══════════════════════════════════════════════════════════
        private int GetIdRandSelectat()
        {
            if (dgv.SelectedRows.Count == 0)
            {
                MessageBox.Show("Selecteaza un echipament din lista!",
                    "Atentie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return -1;
            }
            object val = dgv.SelectedRows[0].Cells["id_echipament"].Value;
            int id;
            if (val != null && int.TryParse(val.ToString(), out id)) return id;
            MessageBox.Show("Nu s-a putut determina ID-ul echipamentului.",
                "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return -1;
        }

        private void BtnDetalii_Click(object sender, EventArgs e)
        {
            int id = GetIdRandSelectat();
            if (id < 0) return;
            try
            {
                var frm = new FormEditare("Echipamente", connectionString, id, readOnly: true);
                frm.ShowDialog(this);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormFavorite.BtnDetalii_Click");
                MessageBox.Show("Nu s-au putut incarca detaliile: " + ex.Message,
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnElimina_Click(object sender, EventArgs e)
        {
            int id = GetIdRandSelectat();
            if (id < 0) return;

            // FIX BUG-04: dialog propriu cu butoane românești.
            if (!DialogHelper.Confirma(this, "Confirmare",
                    "Elimini echipamentul selectat din lista ta de favorite?",
                    "Elimină", "Anulează")) return;

            try
            {
                FavoriteService.Sterge(id);
                IncarcaDate();
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                // Tabela inexista (invalid object name)
                AppLogger.Error(ex, "FormFavorite.BtnElimina_Click");
                MessageBox.Show(
                    "Tabela Favorite nu exista in baza de date.\n" +
                    "Ruleaza scriptul BD\\Favorite.sql.",
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormFavorite.BtnElimina_Click");
                MessageBox.Show("Nu s-a putut elimina echipamentul: " + ex.Message,
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
