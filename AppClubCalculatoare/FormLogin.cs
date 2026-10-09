using System;
using System.Configuration;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Formular de autentificare — logică (designul e în FormAuth.Designer.cs).
    /// Card centrat cu bară de accent, câmpuri stilizate, link resetare.
    /// </summary>
    public partial class FormLogin : Form
    {
        public bool   Autentificat   { get; private set; } = false;
        public string RolUtilizator  { get; private set; } = "";
        public string NumeUtilizator { get; private set; } = "";
        // FIX_01: логин для SessionInfo (NumeUtilizator = ФИО для UI, Username = логин для БД/аудита)
        public string Username { get; private set; } = "";

        private int          _tentativeEsuate = 0;
        private readonly int _maxTentative    = DatabaseConfig.MaxLoginAttempts;

        public FormLogin()
        {
            InitializeComponent();
        }

        // ════════════════════════════════════════════════════════════
        //  EVENIMENTE UI (legate în FormAuth.Designer.cs)
        // ════════════════════════════════════════════════════════════

        private void FormLogin_Shown(object sender, EventArgs e)
        {
            txtUser.Focus();
        }

        private void Content_Resize(object sender, EventArgs e)
        {
            card.Location = new System.Drawing.Point(
                System.Math.Max(0, (content.ClientSize.Width - card.Width) / 2), 0);
        }

        private void BottomBar_Paint(object sender, PaintEventArgs e)
        {
            using (var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(0xE0, 0xE0, 0xE0), 1f))
                e.Graphics.DrawLine(pen, 0, 0, bottomBar.Width, 0);
        }

        private void BtnAnulare_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private async void BtnLogin_Click(object sender, EventArgs e)
        {
            await VerificaAutentificareAsync(txtUser.Text.Trim(), txtParola.Text);
        }

        private void TxtUser_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                btnLogin.PerformClick();
            }
        }

        private void TxtParola_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                btnLogin.PerformClick();
            }
        }

        private async void LnkResetare_Click(object sender, EventArgs e)
        {
            await TrimiteEmailResetareAsync(txtUser.Text.Trim());
        }

        // ════════════════════════════════════════════════════════════
        //  LOGICA DE AUTENTIFICARE (neschimbată)
        // ════════════════════════════════════════════════════════════
        private async Task VerificaAutentificareAsync(string username, string parola)
        {
            lblMesajEroare.Visible = false;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(parola))
            {
                AfiseazaEroare("Completează atât utilizatorul cât și parola!");
                return;
            }

            if (_maxTentative > 0 && _tentativeEsuate >= _maxTentative)
            {
                AfiseazaEroare($"Cont blocat după {_maxTentative} tentative.\nRepornește aplicația.");
                btnLogin.Enabled = false;
                return;
            }

            SetFormEnabled(false);
            lblMesajEroare.Text      = "Se verifică...";
            lblMesajEroare.ForeColor = UI.MaterialTheme.TextSecondary;
            lblMesajEroare.Visible   = true;

            AuthResult rezultat;
            try
            {
                rezultat = await AuthService.AutentificaAsync(username, parola);
            }
            finally
            {
                SetFormEnabled(true);
            }

            if (rezultat.Succes)
            {
                _tentativeEsuate   = 0;
                Autentificat       = true;
                RolUtilizator      = rezultat.Rol == "Admin" ? "Administrator" : "Utilizator";
                NumeUtilizator     = rezultat.NumeComplet;
                // FIX_01: păstrăm login-ul real pentru SessionInfo/audit/permisiuni (NumeUtilizator rămâne ФИО pentru UI)
                Username = username;

                PermissionService.IncarcaPermisiuni(username, RolUtilizator == "Administrator");

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                _tentativeEsuate++;
                lblMesajEroare.ForeColor = UI.MaterialTheme.Danger;

                if (_maxTentative > 0)
                {
                    int ramase = _maxTentative - _tentativeEsuate;

                    pbTentative.Maximum = _maxTentative;
                    pbTentative.Value   = Math.Min(_tentativeEsuate, _maxTentative);
                    pbTentative.Visible = true;

                    lblTentativeInfo.Visible   = true;
                    lblTentativeInfo.ForeColor = ramase <= 1 ? UI.MaterialTheme.Danger : UI.MaterialTheme.Warning;

                    if (ramase > 0)
                        lblTentativeInfo.Text =
                            $"⚠️ Atenție: mai ai {ramase} încercare(i) înainte de blocare!";
                    else
                    {
                        lblTentativeInfo.Text = "Cont blocat! Repornește aplicația.";
                        btnLogin.Enabled      = false;
                    }
                }

                AfiseazaEroare(rezultat.MesajEroare);
                txtParola.Clear();
                txtParola.Focus();
            }
        }

        private async Task TrimiteEmailResetareAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show("Introdu mai întâi username-ul tău în câmpul 'Utilizator'!",
                    "Atenție", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection(
                    DatabaseConfig.ConnectionString))
                {
                    await conn.OpenAsync();

                    string sql = @"SELECT email FROM Utilizatori
                                   WHERE username = @u AND activ = 1";
                    using (var cmd = new System.Data.SqlClient.SqlCommand(sql, conn))
                    {
                        cmd.Parameters.Add("@u",
                            System.Data.SqlDbType.NVarChar, 50).Value = username;

                        object email = await cmd.ExecuteScalarAsync();

                        if (email == null || email == System.DBNull.Value ||
                            string.IsNullOrWhiteSpace(email.ToString()))
                        {
                            MessageBox.Show(
                                "Nu s-a găsit un email asociat contului '" + username + "'.\n" +
                                "Contactează administratorul pentru resetarea parolei.",
                                "Email negăsit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        string parolaTemp = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                        string hashTemp   = AuthService.HashParola(parolaTemp);

                        // ── Configurare SMTP din App.config (appSettings) ──
                        string smtpHost     = ConfigurationManager.AppSettings["SmtpHost"];
                        string smtpPortRaw  = ConfigurationManager.AppSettings["SmtpPort"];
                        string smtpUser     = ConfigurationManager.AppSettings["SmtpUser"];
                        string smtpPassword = ConfigurationManager.AppSettings["SmtpPassword"];
                        string smtpFrom     = ConfigurationManager.AppSettings["SmtpFrom"];

                        if (string.IsNullOrWhiteSpace(smtpHost) ||
                            string.IsNullOrWhiteSpace(smtpUser) ||
                            string.IsNullOrWhiteSpace(smtpPassword))
                        {
                            MessageBox.Show(
                                "Serverul SMTP nu este configurat.\n" +
                                "Completeaza cheile SmtpHost / SmtpPort / SmtpUser / " +
                                "SmtpPassword / SmtpFrom in App.config (<appSettings>),\n" +
                                "apoi reincearca. Parola nu a fost modificata.",
                                "SMTP neconfigurat", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        int smtpPort = 587;
                        if (!int.TryParse(smtpPortRaw, out smtpPort) || smtpPort <= 0)
                            smtpPort = 587;

                        if (string.IsNullOrWhiteSpace(smtpFrom))
                            smtpFrom = "noreply@clubcalculatoare.ro";

                        string updSql = @"UPDATE Utilizatori SET parola_hash = @hash
                                          WHERE username = @u";
                        using (var cmdUpd = new System.Data.SqlClient.SqlCommand(updSql, conn))
                        {
                            cmdUpd.Parameters.Add("@hash",
                                System.Data.SqlDbType.NVarChar, 256).Value = hashTemp;
                            cmdUpd.Parameters.Add("@u",
                                System.Data.SqlDbType.NVarChar, 50).Value  = username;
                            await cmdUpd.ExecuteNonQueryAsync();
                        }

                        using (var msg = new System.Net.Mail.MailMessage())
                        {
                            msg.To.Add(email.ToString());
                            msg.From    = new System.Net.Mail.MailAddress(smtpFrom);
                            msg.Subject = "Resetare parola - Club de Calculatoare";
                            msg.Body    =
                                $"Salut {username},\n\n" +
                                $"Parola ta temporară este: {parolaTemp}\n\n" +
                                "Te rugăm să o schimbi imediat după autentificare.\n\n" +
                                "Club de Calculatoare";

                            using (var smtp = new System.Net.Mail.SmtpClient(smtpHost, smtpPort))
                            {
                                smtp.EnableSsl    = true;
                                smtp.Credentials  = new System.Net.NetworkCredential(
                                    smtpUser,
                                    smtpPassword);
                                await smtp.SendMailAsync(msg);
                            }
                        }

                        AppLogger.Security(
                            $"Resetare parola trimisa pe '{email}' pentru '{username}'");
                        MessageBox.Show(
                            "O parolă temporară a fost trimisă la adresa de email asociată contului.\n" +
                            "Schimb-o imediat după autentificare!",
                            "Email trimis", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormLogin.TrimiteEmailResetare");
                MessageBox.Show(
                    "Eroare la trimiterea emailului: " + ex.Message + "\n\n" +
                    "Verifică configurația SMTP din App.config (<appSettings>: " +
                    "SmtpHost, SmtpPort, SmtpUser, SmtpPassword, SmtpFrom).",
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AfiseazaEroare(string mesaj)
        {
            lblMesajEroare.Text      = "⚠️ " + mesaj;
            lblMesajEroare.ForeColor = UI.MaterialTheme.Danger;
            lblMesajEroare.Visible   = true;
        }

        private void SetFormEnabled(bool enabled)
        {
            txtUser.Enabled   = enabled;
            txtParola.Enabled = enabled;
            btnLogin.Enabled  = enabled;
            this.Cursor = enabled ? Cursors.Default : Cursors.WaitCursor;
        }
    }
}
