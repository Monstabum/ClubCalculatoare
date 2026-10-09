using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using AppClubCalculatoare.UI;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Formular de gestionare utilizatori — disponibil doar pentru Administrator.
    /// Intrebarea 15: adaugare, resetare parola, activare/dezactivare conturi.
    /// </summary>
    public class FormUtilizatori : Form
    {
        private DataGridView dgv;
        private readonly string _connStr = DatabaseConfig.ConnectionString;

        public FormUtilizatori()
        {
            this.Text          = "Gestionare Utilizatori  [Admin]";
            this.Size          = new Size(1120, 680);
            this.MinimumSize   = new Size(980, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor     = MaterialTheme.Background;
            this.Font          = MaterialTheme.FontBody;
            BuildUI();
            IncarcaUtilizatori();
        }

        private Label lblTotalUtilizatori;

        private void BuildUI()
        {
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

            dgv = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            MaterialUIHelper.StyleGrid(dgv, MaterialTheme.Primary);
            gridInner.Controls.Add(dgv);

            // ── Bara inferioara split (Dock.Bottom) ─────────────────
            FlowLayoutPanel leftBottom, rightBottom;
            var bottomBar = MaterialUIHelper.SplitBottomBar(out leftBottom, out rightBottom);
            this.Controls.Add(bottomBar);

            CreBtn(leftBottom, "➕  Adaugă", MaterialButtonStyle.Success, 130).Click += BtnAdauga_Click;
            CreBtn(leftBottom, "🔑  Resetează parola", MaterialButtonStyle.Primary, 170).Click += BtnReseteazaParola_Click;
            CreBtn(leftBottom, "🔄  Activ / Inactiv", MaterialButtonStyle.Secondary, 155).Click += BtnToggleActiv_Click;
            CreBtn(leftBottom, "🎭  Schimbă rol", MaterialButtonStyle.Secondary, 140).Click += BtnSchimbaRol_Click;
            CreBtn(leftBottom, "🛡️  Permisiuni", MaterialButtonStyle.Secondary, 140).Click += BtnPermisiuni_Click;

            CreBtn(rightBottom, "🕘  Istoric", MaterialButtonStyle.Secondary, 130).Click += BtnIstoricDrepturi_Click;
            CreBtn(rightBottom, "✖  Închide", MaterialButtonStyle.Outline, 120).Click += (s, e) => this.Close();

            // ── Toolbar sus (Dock.Top, 60px): refresh + total ───────
            var toolbar = MaterialUIHelper.Toolbar();
            this.Controls.Add(toolbar);

            var btnRefresh = MaterialUIHelper.Button("🔁  Reîncarcă", MaterialButtonStyle.Secondary, 140, 36);
            btnRefresh.Location = new Point(12, 11);
            btnRefresh.Click += (s, e) => IncarcaUtilizatori();
            toolbar.Controls.Add(btnRefresh);

            lblTotalUtilizatori = new Label
            {
                Text = "Total: 0 utilizatori",
                Font = MaterialTheme.FontLabel,
                ForeColor = MaterialTheme.TextSecondary,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            toolbar.Controls.Add(lblTotalUtilizatori);
            toolbar.Resize += (s, e) =>
            {
                lblTotalUtilizatori.Location = new Point(
                    Math.Max(300, toolbar.Width - lblTotalUtilizatori.Width - 16), 20);
            };
            lblTotalUtilizatori.Location = new Point(800, 20);

            // ── Header colorat (Dock.Top) — adaugat ULTIMUL ─────────
            var header = MaterialUIHelper.Header(
                "Gestionare utilizatori", "Administrare conturi · roluri · permisiuni [Admin]",
                null, MaterialTheme.Primary);
            this.Controls.Add(header);
        }

        // ══════════════════════════════════════════════════════════
        //  ISTORIC DREPTURI (audit dedicat — vizibil doar aici)
        // ══════════════════════════════════════════════════════════
        private void BtnIstoricDrepturi_Click(object sender, EventArgs e)
        {
            using (var frm = new FormAuditDrepturi(_connStr))
                frm.ShowDialog();
        }

        private void IncarcaUtilizatori()
        {
            try
            {
                using (var conn = new SqlConnection(_connStr))
                {
                    // Nu expunem hash-ul parolei in grila
                    const string sql = @"
                        SELECT id_utilizator       AS ID,
                               username            AS Username,
                               rol                 AS Rol,
                               nume_complet        AS Nume_Complet,
                               CASE activ
                                   WHEN 1 THEN 'Activ'
                                   ELSE 'Inactiv'
                               END                 AS Status,
                               CONVERT(VARCHAR, data_creare, 103) AS Data_Creare,
                               ISNULL(CONVERT(VARCHAR, ultima_autentificare, 103),
                                      'Niciodata') AS Ultima_Autentificare
                        FROM   Utilizatori
                        ORDER  BY id_utilizator";

                    var da = new SqlDataAdapter(sql, conn);
                    var dt = new DataTable();
                    da.Fill(dt);
                    dgv.DataSource = dt;

                    if (lblTotalUtilizatori != null)
                        lblTotalUtilizatori.Text = "Total: " + dt.Rows.Count + " utilizatori";

                    // Coloreaza randurile inactive
                    foreach (DataGridViewRow row in dgv.Rows)
                        if (row.Cells["Status"].Value?.ToString() == "Inactiv")
                            row.DefaultCellStyle.BackColor =
                                System.Drawing.Color.FromArgb(255, 230, 230);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormUtilizatori.IncarcaUtilizatori");
                MessageBox.Show("Eroare la incarcare: " + ex.Message,
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  ADAUGA UTILIZATOR
        // ════════════════════════════════════════════════════════════
        private void BtnAdauga_Click(object sender, EventArgs e)
        {
            using (var frm = new FormAdaugaUtilizator(_connStr))
            {
                if (frm.ShowDialog() == DialogResult.OK)
                    IncarcaUtilizatori();
            }
        }

        // ════════════════════════════════════════════════════════════
        //  RESETEAZA PAROLA
        // ════════════════════════════════════════════════════════════
        private void BtnReseteazaParola_Click(object sender, EventArgs e)
        {
            if (dgv.SelectedRows.Count == 0)
            {
                MessageBox.Show("Selecteaza un utilizator!", "Atentie",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string username = dgv.SelectedRows[0].Cells["Username"].Value.ToString();

            // FIX_17: dialog custom cu PasswordChar + confirmare — parola nu mai este vizibilă pe ecran (InputBox o arăta în clar).
            string parolaNoua;
            using (var dlg = new FormResetareParola(username))
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                parolaNoua = dlg.ParolaNoua;
            }

            if (string.IsNullOrWhiteSpace(parolaNoua)) return;

            if (parolaNoua.Length < 6)
            {
                MessageBox.Show("Parola trebuie sa aiba minim 6 caractere!",
                    "Parola prea scurta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string hash = AuthService.HashParola(parolaNoua);

                using (var conn = new SqlConnection(_connStr))
                {
                    conn.Open();
                    const string sql = @"
                        UPDATE Utilizatori
                        SET    parola_hash = @hash
                        WHERE  username    = @user";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.Add("@hash", SqlDbType.NVarChar, 256).Value = hash;
                        cmd.Parameters.Add("@user", SqlDbType.NVarChar, 50).Value  = username;
                        cmd.ExecuteNonQuery();
                    }
                }

                AppLogger.Security(
                    $"Admin '{SessionInfo.Username}' a resetat parola pentru '{username}'");
                PermissionService.AuditDrepturi("UPDATE",
                    $"Admin '{SessionInfo.Username}' a resetat parola lui '{username}'");
                MessageBox.Show($"Parola pentru '{username}' a fost resetata cu succes!",
                    "Succes", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormUtilizatori.ReseteazaParola");
                MessageBox.Show("Eroare: " + ex.Message, "Eroare",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  ACTIVEAZA / DEZACTIVEAZA CONT
        // ════════════════════════════════════════════════════════════
        private void BtnToggleActiv_Click(object sender, EventArgs e)
        {
            if (dgv.SelectedRows.Count == 0)
            {
                MessageBox.Show("Selecteaza un utilizator!", "Atentie",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int    id          = (int)dgv.SelectedRows[0].Cells["ID"].Value;
            string username    = dgv.SelectedRows[0].Cells["Username"].Value.ToString();
            bool   esteActiv   = dgv.SelectedRows[0].Cells["Status"].Value.ToString() == "Activ";
            string actiuneText = esteActiv ? "dezactiva" : "activa";

            // Nu permite dezactivarea contului propriu
            if (username == SessionInfo.Username)
            {
                MessageBox.Show("Nu iti poti dezactiva propriul cont!",
                    "Operatie nepermisa", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show(
                $"Esti sigur ca vrei sa {actiuneText}zi contul '{username}'?",
                "Confirmare", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                != DialogResult.Yes) return;

            try
            {
                using (var conn = new SqlConnection(_connStr))
                {
                    conn.Open();
                    const string sql = @"
                        UPDATE Utilizatori SET activ = @activ
                        WHERE  id_utilizator = @id";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.Add("@activ", SqlDbType.Bit).Value = !esteActiv;
                        cmd.Parameters.Add("@id",    SqlDbType.Int).Value = id;
                        cmd.ExecuteNonQuery();
                    }
                }

                AppLogger.Security(
                    $"Admin '{SessionInfo.Username}' a {actiuneText}t contul '{username}'");
                PermissionService.AuditDrepturi("UPDATE",
                    $"Admin '{SessionInfo.Username}' a {actiuneText}t contul '{username}'");
                IncarcaUtilizatori();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormUtilizatori.ToggleActiv");
                MessageBox.Show("Eroare: " + ex.Message, "Eroare",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  SCHIMBA ROL (Admin → User sau invers)
        // ══════════════════════════════════════════════════════════
        private void BtnSchimbaRol_Click(object sender, EventArgs e)
        {
            if (dgv.SelectedRows.Count == 0)
            {
                MessageBox.Show("Selecteaza un utilizator!", "Atentie",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int    id       = (int)dgv.SelectedRows[0].Cells["ID"].Value;
            string username = dgv.SelectedRows[0].Cells["Username"].Value.ToString();
            string rolCurent = dgv.SelectedRows[0].Cells["Rol"].Value.ToString();

            // Nu isi poate schimba propriul rol (ar putea ramane fara admin)
            if (username == SessionInfo.Username)
            {
                MessageBox.Show("Nu iti poti schimba propriul rol!",
                    "Operatie nepermisa", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string rolNou = rolCurent == "Admin" ? "User" : "Admin";

            if (MessageBox.Show(
                $"Schimbi rolul utilizatorului '{username}' din '{rolCurent}' in '{rolNou}'?",
                "Confirmare schimbare rol", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes) return;

            try
            {
                using (var conn = new SqlConnection(_connStr))
                {
                    conn.Open();
                    const string sql = @"
                        UPDATE Utilizatori SET rol = @rol
                        WHERE  id_utilizator = @id";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.Add("@rol", SqlDbType.NVarChar, 20).Value = rolNou;
                        cmd.Parameters.Add("@id",  SqlDbType.Int).Value         = id;
                        cmd.ExecuteNonQuery();
                    }
                }

                AppLogger.Security(
                    $"Admin '{SessionInfo.Username}' a schimbat rolul lui '{username}': {rolCurent} -> {rolNou}");
                PermissionService.AuditDrepturi("UPDATE",
                    $"Admin '{SessionInfo.Username}' a schimbat rolul lui '{username}': {rolCurent} -> {rolNou}");
                PermissionService.InvalideazaCache();
                IncarcaUtilizatori();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormUtilizatori.SchimbaRol");
                MessageBox.Show("Eroare: " + ex.Message, "Eroare",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  DREPTURI DE ACCES / PERMISIUNI
        // ══════════════════════════════════════════════════════════
        private void BtnPermisiuni_Click(object sender, EventArgs e)
        {
            if (dgv.SelectedRows.Count == 0)
            {
                MessageBox.Show("Selecteaza un utilizator!", "Atentie",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int    id       = (int)dgv.SelectedRows[0].Cells["ID"].Value;
            string username = dgv.SelectedRows[0].Cells["Username"].Value.ToString();
            string rol      = dgv.SelectedRows[0].Cells["Rol"].Value.ToString();

            using (var frm = new FormPermisiuni(_connStr, id, username, rol))
                frm.ShowDialog();
        }

        /// <summary>
        /// Creeaza un buton Material si il adauga in containerul dat.
        /// Textul este mereu vizibil datorita OnPaint custom.
        /// </summary>
        private static MaterialButton CreBtn(Control parent, string text,
            MaterialButtonStyle style, int width)
        {
            var btn = MaterialUIHelper.Button(text, style, width, 38);
            btn.Margin = new Padding(0, 0, 8, 0);
            parent.Controls.Add(btn);
            return btn;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  FORMULAR INTERN: Adauga utilizator nou
    // ════════════════════════════════════════════════════════════════
    internal class FormAdaugaUtilizator : Form
    {
        private readonly string _connStr;
        private TextBox  txtUsername, txtParola, txtNumeComplet;
        private ComboBox cmbRol;
        private Label    lblMesaj;

        public FormAdaugaUtilizator(string connStr)
        {
            _connStr             = connStr;
            this.Text            = "Adauga utilizator nou";
            this.Size            = new Size(460, 440);
            this.MinimumSize     = new Size(460, 440);
            this.StartPosition   = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox     = false;
            this.BackColor       = MaterialTheme.Background;
            this.Font            = MaterialTheme.FontBody;
            BuildUI();
        }

        private void BuildUI()
        {
            // Continut (Fill) primul
            var content = MaterialUIHelper.ContentPanel();
            this.Controls.Add(content);

            var card = new MaterialCard { Dock = DockStyle.Fill };
            content.Controls.Add(card);

            // Bara jos: Anulare / Salveaza
            FlowLayoutPanel lb, rb;
            var bottomBar = MaterialUIHelper.SplitBottomBar(out lb, out rb);
            this.Controls.Add(bottomBar);

            var btnAn = MaterialUIHelper.Button("✕  Anulare", MaterialButtonStyle.Outline, 120);
            btnAn.Click += (s, e) => this.Close();
            rb.Controls.Add(btnAn);

            var btnSalv = MaterialUIHelper.Button("💾  Salvează", MaterialButtonStyle.Success, 140);
            btnSalv.Click += BtnSalveaza_Click;
            rb.Controls.Add(btnSalv);
            this.AcceptButton = btnSalv;
            this.CancelButton = btnAn;

            // Header sus (ultimul)
            var header = MaterialUIHelper.Header(
                "Utilizator nou", "Completează datele contului",
                null, MaterialTheme.Primary);
            this.Controls.Add(header);

            int y = 16;
            int cw = 220;

            AdaugaLabelCard(card, "Username: *", y);
            txtUsername = AdaugaTxtCard(card, y, cw); y += 56;

            AdaugaLabelCard(card, "Parola initiala: *", y);
            txtParola = AdaugaTxtCard(card, y, cw);
            txtParola.PasswordChar = '●'; y += 56;

            AdaugaLabelCard(card, "Nume complet: *", y);
            txtNumeComplet = AdaugaTxtCard(card, y, cw); y += 56;

            AdaugaLabelCard(card, "Rol: *", y);
            cmbRol = new ComboBox();
            cmbRol.Location      = new System.Drawing.Point(20, y + 20);
            cmbRol.Size          = new System.Drawing.Size(cw, 28);
            cmbRol.Font          = MaterialTheme.FontBody;
            cmbRol.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbRol.Items.AddRange(new object[] { "Admin", "User" });
            cmbRol.SelectedIndex = 1; // User implicit
            cmbRol.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            card.Controls.Add(cmbRol);
            y += 56;

            lblMesaj           = new Label();
            lblMesaj.Location  = new System.Drawing.Point(20, y);
            lblMesaj.Size      = new System.Drawing.Size(360, 20);
            lblMesaj.Font      = MaterialTheme.FontSmall;
            lblMesaj.BackColor = System.Drawing.Color.Transparent;
            lblMesaj.Visible   = false;
            card.Controls.Add(lblMesaj);
        }

        private void BtnSalveaza_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text)     ||
                string.IsNullOrWhiteSpace(txtParola.Text)       ||
                string.IsNullOrWhiteSpace(txtNumeComplet.Text))
            {
                AfiseazaMesaj("Completeaza toate campurile obligatorii!",
                    System.Drawing.Color.Red);
                return;
            }

            if (txtParola.Text.Length < 6)
            {
                AfiseazaMesaj("Parola trebuie sa aiba minim 6 caractere!",
                    System.Drawing.Color.Red);
                return;
            }

            try
            {
                string hash = AuthService.HashParola(txtParola.Text);

                using (var conn = new SqlConnection(_connStr))
                {
                    conn.Open();
                    const string sql = @"
                        INSERT INTO Utilizatori
                            (username, parola_hash, rol, nume_complet, activ)
                        VALUES
                            (@user, @hash, @rol, @nume, 1)";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.Add("@user", SqlDbType.NVarChar, 50).Value  =
                            txtUsername.Text.Trim();
                        cmd.Parameters.Add("@hash", SqlDbType.NVarChar, 256).Value = hash;
                        cmd.Parameters.Add("@rol",  SqlDbType.NVarChar, 20).Value  =
                            cmbRol.SelectedItem.ToString();
                        cmd.Parameters.Add("@nume", SqlDbType.NVarChar, 100).Value =
                            txtNumeComplet.Text.Trim();
                        cmd.ExecuteNonQuery();
                    }
                }

                AppLogger.Security(
                    $"Admin '{SessionInfo.Username}' a creat contul '{txtUsername.Text.Trim()}'");
                PermissionService.AuditDrepturi("INSERT",
                    $"Admin '{SessionInfo.Username}' a creat contul '{txtUsername.Text.Trim()}' " +
                    $"cu rolul {cmbRol.SelectedItem}");
                MessageBox.Show("Utilizatorul a fost creat cu succes!",
                    "Succes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
            }
            catch (SqlException ex) when (ex.Number == 2627)
            {
                AfiseazaMesaj("Username-ul exista deja! Alege altul.",
                    System.Drawing.Color.Red);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormAdaugaUtilizator");
                AfiseazaMesaj("Eroare: " + ex.Message, System.Drawing.Color.Red);
            }
        }

        private void AdaugaLabel(string text, int y)
        {
            AdaugaLabelCard(this, text, y);
        }

        private static void AdaugaLabelCard(Control parent, string text, int y)
        {
            Label lbl = new Label();
            lbl.Text     = text;
            lbl.Font     = MaterialTheme.FontLabel;
            lbl.ForeColor = MaterialTheme.TextSecondary;
            lbl.BackColor = System.Drawing.Color.Transparent;
            lbl.Location = new System.Drawing.Point(20, y);
            lbl.Size     = new Size(320, 18);
            parent.Controls.Add(lbl);
        }

        private TextBox AdaugaTxt(int y, int cw)
        {
            return AdaugaTxtCard(this, y, cw);
        }

        private static TextBox AdaugaTxtCard(Control parent, int y, int cw)
        {
            TextBox txt = new TextBox();
            txt.Location  = new System.Drawing.Point(20, y + 20);
            txt.Size      = new System.Drawing.Size(cw, 28);
            txt.Font      = MaterialTheme.FontBody;
            txt.ForeColor = MaterialTheme.TextPrimary;
            txt.BackColor = System.Drawing.Color.White;
            txt.BorderStyle = BorderStyle.FixedSingle;
            txt.MaxLength = 100;
            txt.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            parent.Controls.Add(txt);
            return txt;
        }

        private void AfiseazaMesaj(string mesaj, System.Drawing.Color culoare)
        {
            lblMesaj.Text      = mesaj;
            lblMesaj.ForeColor = culoare;
            lblMesaj.Visible   = true;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  FORMULAR INTERN: Drepturi de acces / Permisiuni
    //  Administratorul bifeaza exact ce puncte/functii poate folosi
    //  utilizatorul selectat. Adminii au automat acces la toate —
    //  casutele sunt debifate si dezactivate, cu mentiune explicativa.
    // ════════════════════════════════════════════════════════════════
    internal class FormPermisiuni : Form
    {
        private readonly string _connStr;
        private readonly int    _idUtilizator;
        private readonly string _username;
        private readonly string _rol;
        private readonly List<CheckBox> _casute = new List<CheckBox>();
        private Label lblInfo;

        public FormPermisiuni(string connStr, int idUtilizator,
                              string username, string rol)
        {
            _connStr       = connStr;
            _idUtilizator  = idUtilizator;
            _username      = username;
            _rol           = rol;

            this.Text            = $"Drepturi de acces / Permisiuni — {username}";
            this.Size            = new Size(540, 700);
            this.MinimumSize     = new Size(540, 600);
            this.StartPosition   = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox     = false;
            this.BackColor       = MaterialTheme.Background;
            this.Font            = MaterialTheme.FontBody;
            BuildUI();
            IncarcaPermisiuni();
        }

        private void BuildUI()
        {
            // Continut Fill primul
            var content = MaterialUIHelper.ContentPanel();
            this.Controls.Add(content);

            var card = new MaterialCard { Dock = DockStyle.Fill };
            content.Controls.Add(card);

            // Bara jos: Anulare / Salveaza
            FlowLayoutPanel lb, rb;
            var bottomBar = MaterialUIHelper.SplitBottomBar(out lb, out rb);
            this.Controls.Add(bottomBar);

            var btnAnulare = MaterialUIHelper.Button("✕  Anulare", MaterialButtonStyle.Outline, 130);
            btnAnulare.Click += (s, e) => this.Close();
            rb.Controls.Add(btnAnulare);

            var btnSalveaza = MaterialUIHelper.Button("💾  Salvează drepturile", MaterialButtonStyle.Success, 190);
            btnSalveaza.Enabled = _rol != "Admin";
            btnSalveaza.Click += BtnSalveaza_Click;
            rb.Controls.Add(btnSalveaza);
            this.AcceptButton = btnSalveaza;
            this.CancelButton = btnAnulare;

            // Header sus (ultimul)
            var header = MaterialUIHelper.Header(
                "Permisiuni — " + _username, "Rol: " + _rol,
                null, MaterialTheme.Primary);
            this.Controls.Add(header);

            lblInfo = new Label
            {
                Text = _rol == "Admin"
                    ? "Administratorii au automat acces la toate punctele."
                    : "Bifează punctele și acțiunile permise:",
                Font      = MaterialTheme.FontSmall,
                ForeColor = MaterialTheme.TextSecondary,
                BackColor = System.Drawing.Color.Transparent,
                Location  = new System.Drawing.Point(16, 12),
                Size      = new Size(440, 20)
            };
            card.Controls.Add(lblInfo);

            // ── Panou derulant: grupuri Vizualizare / Modificare / Stergere ──
            var panouCasute = new Panel
            {
                Location    = new System.Drawing.Point(16, 38),
                Size        = new System.Drawing.Size(440, 400),
                Anchor      = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoScroll  = true,
                BackColor   = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            card.Controls.Add(panouCasute);

            int y = 10;
            string grupCurent = null;
            foreach (var punct in PuncteAcces.Toti)
            {
                // Antet de grup inaintea fiecarui nivel de permisiune nou
                string grup = DeterminaGrup(punct.Key);
                if (grup != grupCurent)
                {
                    grupCurent = grup;
                    var lblGrup = new Label
                    {
                        Text      = grup + ":",
                        Font      = MaterialTheme.FontLabel,
                        ForeColor = MaterialTheme.Primary,
                        BackColor = System.Drawing.Color.Transparent,
                        Location  = new System.Drawing.Point(10, y),
                        AutoSize  = true
                    };
                    panouCasute.Controls.Add(lblGrup);
                    y += 26;
                }

                var chk = new CheckBox
                {
                    Text      = punct.Value,
                    Location  = new System.Drawing.Point(28, y),
                    AutoSize  = true,
                    Font      = MaterialTheme.FontBody,
                    ForeColor = MaterialTheme.TextPrimary,
                    Enabled   = _rol != "Admin",
                    Tag       = punct.Key
                };
                panouCasute.Controls.Add(chk);
                _casute.Add(chk);
                y += 28;
            }
        }

        /// <summary>Grupul de afisare pentru un cod de permisiune.</summary>
        private static string DeterminaGrup(string cod)
        {
            if (cod.StartsWith("MODIFICARE_")) return "Modificare (adaugare / editare)";
            if (cod.StartsWith("STERGE_"))    return "Stergere";
            if (cod == PuncteAcces.GestionareUtilizatori) return "Functii transversale";
            return "Vizualizare";
        }

        /// <summary>Încarcă drepturile curente din UtilizatoriPermisiuni.</summary>
        private void IncarcaPermisiuni()
        {
            try
            {
                const string sql = @"
                    SELECT cod_permisiune
                    FROM   UtilizatoriPermisiuni
                    WHERE  id_utilizator = @id";

                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@id", SqlDbType.Int).Value = _idUtilizator;
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                        {
                            string cod = reader.GetString(0);
                            foreach (var chk in _casute)
                                if ((string)chk.Tag == cod) chk.Checked = true;
                        }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormPermisiuni.IncarcaPermisiuni");
                MessageBox.Show("Eroare la incarcarea drepturilor: " + ex.Message,
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Rescrie complet drepturile utilizatorului (delete + insert).</summary>
        private void BtnSalveaza_Click(object sender, EventArgs e)
        {
            try
            {
                using (var conn = new SqlConnection(_connStr))
                {
                    conn.Open();
                    using (var tranz = conn.BeginTransaction())
                    {
                        // 1. Sterge drepturile vechi
                        using (var cmd = new SqlCommand(
                            "DELETE FROM UtilizatoriPermisiuni WHERE id_utilizator = @id",
                            conn, tranz))
                        {
                            cmd.Parameters.Add("@id", SqlDbType.Int).Value = _idUtilizator;
                            cmd.ExecuteNonQuery();
                        }

                        // 2. Insereaza doar casutele bifate
                        foreach (var chk in _casute)
                        {
                            if (!chk.Checked) continue;

                            using (var cmd = new SqlCommand(@"
                                INSERT INTO UtilizatoriPermisiuni
                                    (id_utilizator, cod_permisiune, acordata_de)
                                VALUES (@id, @cod, @admin)", conn, tranz))
                            {
                                cmd.Parameters.Add("@id",   SqlDbType.Int).Value        = _idUtilizator;
                                cmd.Parameters.Add("@cod",  SqlDbType.NVarChar, 50).Value = (string)chk.Tag;
                                cmd.Parameters.Add("@admin",SqlDbType.NVarChar, 50).Value = SessionInfo.Username;
                                cmd.ExecuteNonQuery();
                            }
                        }

                        tranz.Commit();
                    }
                }

                // Daca utilizatorul modificat este cel logat chiar acum,
                // reimprospatam si sesiunea lui (permisiunile noi intra in vigore).
                if (_username == SessionInfo.Username)
                    PermissionService.IncarcaPermisiuni(_username, false);
                PermissionService.InvalideazaCache();

                // ── Audit dedicat drepturilor (istoric complet) ──────────
                var acordate = new List<string>();
                foreach (var chk in _casute)
                    if (chk.Checked) acordate.Add((string)chk.Tag);
                PermissionService.AuditDrepturi("UPDATE",
                    $"Admin '{SessionInfo.Username}' a actualizat permisiunile lui " +
                    $"'{_username}': [{string.Join(", ", acordate)}]");
                MessageBox.Show("Drepturile de acces au fost salvate cu succes!",
                    "Succes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormPermisiuni.Salveaza");
                MessageBox.Show("Eroare la salvare: " + ex.Message,
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // FIX_17: dialog custom pentru resetarea parolei — maschează caracterele (PasswordChar='●') + cere confirmare.
    internal class FormResetareParola : Form
    {
        public string ParolaNoua { get; private set; } = "";

        private readonly TextBox _txtParola;
        private readonly TextBox _txtConfirm;
        private readonly Label _lblMesaj;

        public FormResetareParola(string username)
        {
            this.Text = "Resetare parolă — " + username;
            // FIX_17b: înălțime mărită (320→400) — cardul Dock.Fill era prea scund și tăia al doilea câmp (vezi screenshot).
            this.Size = new Size(440, 400);
            this.MinimumSize = new Size(440, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = MaterialTheme.Background;
            this.Font = MaterialTheme.FontBody;

            var content = MaterialUIHelper.ContentPanel();
            this.Controls.Add(content);
            var card = new MaterialCard { Dock = DockStyle.Fill };
            content.Controls.Add(card);

            FlowLayoutPanel lb, rb;
            var bottomBar = MaterialUIHelper.SplitBottomBar(out lb, out rb);
            this.Controls.Add(bottomBar);

            var btnAn = MaterialUIHelper.Button("✕  Anulare", MaterialButtonStyle.Outline, 120);
            btnAn.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            rb.Controls.Add(btnAn);
            var btnOk = MaterialUIHelper.Button("💾  Resetează", MaterialButtonStyle.Success, 140);
            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtParola.Text) || string.IsNullOrWhiteSpace(_txtConfirm.Text))
                {
                    _lblMesaj.Text = "Completează ambele câmpuri!"; _lblMesaj.Visible = true; return;
                }
                if (_txtParola.Text.Length < 6)
                {
                    _lblMesaj.Text = "Parola trebuie să aibă minim 6 caractere!"; _lblMesaj.Visible = true; return;
                }
                if (_txtParola.Text != _txtConfirm.Text)
                {
                    _lblMesaj.Text = "Parolele nu coincid!"; _lblMesaj.Visible = true; return;
                }
                ParolaNoua = _txtParola.Text;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            rb.Controls.Add(btnOk);
            this.AcceptButton = btnOk;
            this.CancelButton = btnAn;

            var header = MaterialUIHelper.Header("Resetare parolă", "Cont: " + username, null, MaterialTheme.Primary);
            this.Controls.Add(header);

            int y = 14;
            TextBox t1;
            MaterialUIHelper.LabeledTextBox("Parolă nouă (minim 6 caractere)", card, ref y, 20, 320, false, out t1);
            _txtParola = t1;
            _txtParola.PasswordChar = '●';
            _txtParola.MaxLength = 100;
            TextBox t2;
            MaterialUIHelper.LabeledTextBox("Confirmă parola nouă", card, ref y, 20, 320, false, out t2);
            _txtConfirm = t2;
            _txtConfirm.PasswordChar = '●';
            _txtConfirm.MaxLength = 100;

            _lblMesaj = new Label
            {
                Location = new System.Drawing.Point(20, y + 4),
                Size = new Size(320, 20),
                Font = MaterialTheme.FontSmall,
                ForeColor = MaterialTheme.Danger,
                BackColor = System.Drawing.Color.Transparent,
                Visible = false
            };
            card.Controls.Add(_lblMesaj);
        }
    }
}
