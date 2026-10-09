using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using AppClubCalculatoare.UI;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Fereastra de audit a sistemului de drepturi.
    ///
    /// Afiseaza istoricul complet al actiunilor legate de acces:
    ///   - acordarea/revocarea permisiunilor (FormPermisiuni)
    ///   - schimbarile de rol
    ///   - crearea/resetarea parolelor, activarea/dezactivarea conturilor
    ///   - tentatile de acces blocate (logate de PermissionService)
    ///
    /// Sursa: tabelul AuditLog, cu tabela = 'Drepturi'.
    /// Vizibila exclusiv din FormUtilizatori (acces doar Admin / permisiune).
    /// </summary>
    internal class FormAuditDrepturi : Form
    {
        private readonly string _connStr;
        private DataGridView dgv;
        private ComboBox cmbFiltru;
        private const string TabelaAudit = "Drepturi";

        public FormAuditDrepturi(string connStr)
        {
            _connStr = connStr;

            this.Text            = "Istoric drepturi (audit acces)";
            this.Size            = new Size(950, 600);
            this.MinimumSize     = new Size(800, 480);
            this.StartPosition   = FormStartPosition.CenterParent;
            this.BackColor       = MaterialTheme.Background;
            this.Font            = MaterialTheme.FontBody;

            BuildUI();
            IncarcaIstoric(null);
        }

        private void BuildUI()
        {
            // ── Grila in card (Dock.Fill) — adaugata PRIMA ─────────
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

            dgv = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            MaterialUIHelper.StyleGrid(dgv, MaterialTheme.Primary);
            gridInner.Controls.Add(dgv);

            // ── Bara inferioara (Dock.Bottom): Inchide ──────────────
            FlowLayoutPanel lb, rb;
            var bottomBar = MaterialUIHelper.SplitBottomBar(out lb, out rb);
            this.Controls.Add(bottomBar);

            var btnInchide = MaterialUIHelper.Button("✕  Închide", MaterialButtonStyle.Outline, 130);
            btnInchide.Click += (s, e) => this.Close();
            rb.Controls.Add(btnInchide);

            // ── Toolbar filtre (Dock.Top, 60px) ─────────────────────
            var panel = MaterialUIHelper.Toolbar();
            this.Controls.Add(panel);

            var lblFiltru = new Label
            {
                Text      = "Filtrează acțiune:",
                Font      = MaterialTheme.FontLabel,
                ForeColor = MaterialTheme.TextSecondary,
                Location  = new Point(12, 21),
                AutoSize  = true
            };
            panel.Controls.Add(lblFiltru);

            cmbFiltru = new ComboBox
            {
                Location      = new Point(140, 16),
                Size          = new Size(160, 28),
                Font          = MaterialTheme.FontBody,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor        = AnchorStyles.Top | AnchorStyles.Left
            };
            cmbFiltru.Items.AddRange(new object[] { "Toate", "INSERT", "UPDATE", "DELETE" });
            cmbFiltru.SelectedIndex = 0;
            cmbFiltru.SelectedIndexChanged += (s, e) =>
                IncarcaIstoric(cmbFiltru.SelectedItem.ToString() == "Toate"
                    ? null : cmbFiltru.SelectedItem.ToString());
            panel.Controls.Add(cmbFiltru);

            var btnRefresh = MaterialUIHelper.Button("🔁  Reîncarcă", MaterialButtonStyle.Secondary, 140, 36);
            btnRefresh.Location = new Point(312, 11);
            btnRefresh.Click += (s, e) =>
                IncarcaIstoric(cmbFiltru.SelectedIndex == 0
                    ? null : cmbFiltru.SelectedItem.ToString());
            panel.Controls.Add(btnRefresh);

            // ── Header (Dock.Top) — adaugat ULTIMUL ─────────────────
            var header = MaterialUIHelper.Header(
                "Istoric drepturi", "Audit acces — acordări / revocări / blocări",
                null, MaterialTheme.Primary);
            this.Controls.Add(header);
        }

        /// <summary>Încarcă istoricul din AuditLog, cu filtru opțional pe acțiune.</summary>
        private void IncarcaIstoric(string actiune)
        {
            try
            {
                string sql = @"
                    SELECT CONVERT(VARCHAR, data_ora, 120)      AS Data_Ora,
                           username                             AS Utilizator,
                           actiune                              AS Actiune,
                           ISNULL(detalii, '')                  AS Detalii
                    FROM   AuditLog
                    WHERE  tabela = @tabela";

                if (actiune != null)
                    sql += " AND actiune = @actiune";

                sql += " ORDER BY data_ora DESC";

                using (var conn = new SqlConnection(_connStr))
                using (var da = new SqlDataAdapter(sql, conn))
                {
                    da.SelectCommand.Parameters.Add("@tabela", SqlDbType.NVarChar, 50)
                                  .Value = TabelaAudit;
                    if (actiune != null)
                        da.SelectCommand.Parameters.Add("@actiune", SqlDbType.NVarChar, 10)
                                      .Value = actiune;

                    var dt = new DataTable();
                    da.Fill(dt);
                    dgv.DataSource = dt;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormAuditDrepturi.IncarcaIstoric");
                MessageBox.Show("Eroare la incarcarea istoricului: " + ex.Message,
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
