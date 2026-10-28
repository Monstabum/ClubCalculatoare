using System;
using System.Configuration;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using AppClubCalculatoare.UI;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Formular de autentificare — logică (designul e în FormLogin.Designer.cs).
    /// Card centrat cu bară de accent, câmpuri stilizate, link resetare.
    /// </summary>
    public partial class FormLogin : Form
    {
        public bool   Autentificat   { get; private set; } = false;
        public string RolUtilizator  { get; private set; } = "";
        public string NumeUtilizator { get; private set; } = "";
        // FIX_01: login-ul real pentru SessionInfo (NumeUtilizator = nume complet pentru UI, Username = login pentru BD/audit)
        public string Username { get; private set; } = "";

        private int          _tentativeEsuate = 0;
        private readonly int _maxTentative    = DatabaseConfig.MaxLoginAttempts;

        public FormLogin()
        {
            InitializeComponent();

            // Bară de titlu închisă, consecventă cu tema aplicației.
            WindowChrome.EnableDarkTitleBar(this);

            // FIX BUG-08: când utilizatorul părăsește câmpul „Utilizator" verificăm
            // în BD dacă acel cont este blocat și afișăm ora deblocării.
            if (txtUser != null)
                txtUser.Leave += TxtUser_Leave;

            // Conturul câmpurilor devine accent la focus (nu doar la hover).
            if (txtUser != null)
            {
                txtUser.GotFocus  += (s, e) => userBox.Invalidate();
                txtUser.LostFocus += (s, e) => userBox.Invalidate();
            }
            if (txtParola != null)
            {
                txtParola.GotFocus  += (s, e) => passBox.Invalidate();
                txtParola.LostFocus += (s, e) => passBox.Invalidate();
            }
        }

        // ════════════════════════════════════════════════════════════
        //  EVENIMENTE UI (legate în FormLogin.Designer.cs)
        // ════════════════════════════════════════════════════════════

        private void FormLogin_Shown(object sender, EventArgs e)
        {
            txtUser.Focus();

            // Colțurile rotunjite sunt desenate cu antialiasing în paint-uri
            // (fără Region decupat — acela lăsa muchii „dințate").

            // Suprafața cardului + sigla, desenate plat, în culoarea de accent.
            card.Paint += Card_Paint;
            logo.Paint += Logo_Paint;

            // Lățimile finale ale panourilor sunt cunoscute abia acum, după
            // scalarea DPI; cardul se așază pe zona utilă a content-ului.
            AseazaCardul();

            // Bara de tentative în galben de avertisment (Win10+: PBM_SETSTATE).
            SeteazaStareBaraProgres(PBST_PAUSED);

            // FIX BUG-03: dacă serverul SMTP nu este configurat, linkul de
            // resetare nu poate funcționa — îl dezactivăm vizibil, ca
            // utilizatorul să nu creadă că poate recupera parola.
            if (!SmtpConfigurat())
            {
                lnkResetare.Enabled     = false;
                lnkResetare.TabStop     = false;
                lnkResetare.LinkColor   = Design.TextDim;
                lnkResetare.Cursor      = Cursors.Default;
                lnkResetare.LinkBehavior = LinkBehavior.NeverUnderline;
                var tip = new ToolTip();
                tip.SetToolTip(lnkResetare,
                    "Resetarea parolei prin email nu este disponibilă: " +
                    "serverul SMTP nu este configurat în App.config. " +
                    "Contactează administratorul.");
                lnkResetare.Disposed += (s2, e2) => tip.Dispose();
            }
        }

        /// <summary>
        /// FIX BUG-03: verifică dacă serverul SMTP este configurat în
        /// App.config (aceleași chei folosite de TrimiteEmailResetareAsync).
        /// </summary>
        private static bool SmtpConfigurat()
        {
            string host = ConfigurationManager.AppSettings["SmtpHost"];
            string user = ConfigurationManager.AppSettings["SmtpUser"];
            string pass = ConfigurationManager.AppSettings["SmtpPassword"];
            return !string.IsNullOrWhiteSpace(host)
                && !string.IsNullOrWhiteSpace(user)
                && !string.IsNullOrWhiteSpace(pass);
        }

        private void Content_Resize(object sender, EventArgs e)
        {
            if (!IsHandleCreated || IsDisposed)
                return;

            // Layout-ul difera in timpul scalarii DPI (content deja scalat,
            // cardul inca la dimensiunea de design); amanam pana dupa ce
            // scalarea s-a incheiat, altfel ancora + pozitionarea manuala
            // ingusteaza cardul si taie controalele din el.
            BeginInvoke((Action)AseazaCardul);
        }

        private void AseazaCardul()
        {
            if (IsDisposed || content.IsDisposed)
                return;

            var zona = content.DisplayRectangle;
            card.SetBounds(zona.X, zona.Y, zona.Width, card.Height);
        }

        // ── Pictarea elementelor tematice (handlers legate în Designer) ──

        /// <summary>
        /// Header neutru: suprafață <see cref="Design.Surface"/> cu un
        /// separator de 1px jos (fără gradient, fără linie HUD colorată).
        /// </summary>
        private void Header_Paint(object sender, PaintEventArgs e)
        {
            Design.DrawSeparator(e.Graphics, header.Height - 1, header.Width, Design.Border);
        }

        /// <summary>
        /// Cardul de login: suprafață <see cref="Design.Surface"/> cu contur
        /// 1px <see cref="Design.Border"/> și colțuri mari — fără „glass".
        /// </summary>
        private void Card_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Design.ResolveOpaqueBackground(card, Design.Bg));

            Design.PaintCard(g,
                new Rectangle(0, 0, Math.Max(2, card.Width - 1), Math.Max(2, card.Height - 1)),
                Design.RadiusLarge);
        }

        /// <summary>Sigla aplicației: pătrat rotunjit plat în culoarea de accent.</summary>
        private void Logo_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Design.ResolveOpaqueBackground(logo, Design.Bg));
            var rect = new Rectangle(0, 0, Math.Max(2, logo.Width - 1), Math.Max(2, logo.Height - 1));

            using (var path = Design.RoundedPath(rect, Design.RadiusLarge))
            using (var brush = new SolidBrush(Design.Accent))
                g.FillPath(brush, path);
        }

        private void BottomBar_Paint(object sender, PaintEventArgs e)
        {
            Design.DrawSeparator(e.Graphics, 0, bottomBar.Width, Design.Border);
        }

        // Câmpurile de input: suprafață SurfaceRaised, contur Border,
        // iar la focus conturul trece în culoarea de accent.
        private void UserBox_Paint(object sender, PaintEventArgs e)
        {
            DeseneazaBorduraCamp(userBox, e);
        }

        private void PassBox_Paint(object sender, PaintEventArgs e)
        {
            DeseneazaBorduraCamp(passBox, e);
        }

        private void DeseneazaBorduraCamp(Control box, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Colțuri netede: curățăm cu fundalul părintelui, apoi umplem și
            // conturăm traseul rotunjit (fără Region decupat).
            g.Clear(Design.ResolveOpaqueBackground(box, Design.Bg));

            bool focus = box.ContainsFocus;
            var rect = new Rectangle(0, 0, Math.Max(2, box.Width - 1), Math.Max(2, box.Height - 1));
            using (var path = Design.RoundedPath(rect, Design.RadiusSmall))
            {
                using (var brush = new SolidBrush(Design.SurfaceRaised))
                    g.FillPath(brush, path);
                using (var pen = new Pen(focus ? Design.Accent : Design.Border, 1f))
                    g.DrawPath(pen, path);
            }
        }

        // ── Bara de tentative: culoare de avertisment (Win10+, PBM_SETSTATE) ──
        private const int PBM_SETSTATE = 0x0410;   // WM_USER + 16
        private const int PBST_PAUSED = 3;         // umplere galben

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private void SeteazaStareBaraProgres(int stare)
        {
            try
            {
                if (pbTentative.IsHandleCreated)
                    SendMessage(pbTentative.Handle, PBM_SETSTATE, stare, 0);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Setare culoare bara tentative: " + ex.Message, "FormLogin");
            }
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

            // FIX BUG-08: a fost eliminată blocarea locală („Cont blocat după N
            // tentative. Repornește aplicația."). Blocarea este persistentă, în
            // baza de date, deci doar serverul știe când expiră — iar repornirea
            // aplicației NU debloca contul. Mai rău, butonul rămânea dezactivat,
            // așa că utilizatorul nu putea reîncerca nici după expirarea blocării.
            // Acum lăsăm AuthService să răspundă și afișăm ora exactă a deblocării.

            SetFormEnabled(false);
            lblMesajEroare.Text      = "Se verifică...";
            lblMesajEroare.ForeColor = Design.TextMuted;
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
                // FIX_01: păstrăm login-ul real pentru SessionInfo/audit/permisiuni (NumeUtilizator rămâne nume complet pentru UI)
                Username = username;

                await PermissionService.IncarcaPermisiuniAsync(
                    username, RolUtilizator == "Administrator");

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                _tentativeEsuate++;
                lblMesajEroare.ForeColor = Design.DangerBright;

                // FIX BUG-08: cont blocat temporar → arătăm starea reală din BD
                // (ora deblocării) și NU dezactivăm butonul: utilizatorul trebuie
                // să poată reîncerca după expirarea blocării, fără repornire.
                if (rezultat.Blocat)
                {
                    pbTentative.Maximum = Math.Max(1, _maxTentative);
                    pbTentative.Value   = Math.Max(1, _maxTentative);
                    pbTentative.Visible = true;

                    lblTentativeInfo.Visible   = true;
                    lblTentativeInfo.ForeColor = Design.DangerBright;
                    lblTentativeInfo.Text      = rezultat.BlocatPanaLa.HasValue
                        ? $"Cont blocat până la ora {rezultat.BlocatPanaLa.Value:HH:mm}."
                        : "Cont blocat temporar.";
                }
                else if (_maxTentative > 0)
                {
                    int ramase = _maxTentative - _tentativeEsuate;

                    pbTentative.Maximum = _maxTentative;
                    pbTentative.Value   = Math.Min(_tentativeEsuate, _maxTentative);
                    pbTentative.Visible = true;

                    lblTentativeInfo.Visible   = true;
                    lblTentativeInfo.ForeColor = ramase <= 1 ? Design.DangerBright : Design.WarningBright;

                    if (ramase > 0)
                        lblTentativeInfo.Text =
                            $"Atenție: mai ai {ramase} încercare(i) înainte de blocare!";
                    else
                        lblTentativeInfo.Text = "Cont blocat temporar.";
                }

                AfiseazaEroare(rezultat.MesajEroare);
                txtParola.Clear();
                txtParola.Focus();
            }
        }

        /// <summary>
        /// FIX BUG-08: la părăsirea câmpului „Utilizator" verificăm în baza de date
        /// dacă respectivul cont este blocat și, dacă da, afișăm imediat ora
        /// deblocării. Blocarea este persistentă, deci un utilizator care repornește
        /// aplicația trebuie să vadă adevărul, nu să afle abia după o încercare.
        /// </summary>
        private async void TxtUser_Leave(object sender, EventArgs e)
        {
            string u = txtUser.Text.Trim();
            if (string.IsNullOrWhiteSpace(u)) return;

            DateTime? panaLa = await AuthService.ObtineBlocarePanaLaAsync(u);
            if (IsDisposed) return;
            if (panaLa.HasValue)
            {
                pbTentative.Maximum = Math.Max(1, _maxTentative);
                pbTentative.Value   = Math.Max(1, _maxTentative);
                pbTentative.Visible = true;

                lblTentativeInfo.Visible   = true;
                lblTentativeInfo.ForeColor = Design.DangerBright;
                lblTentativeInfo.Text      = $"Cont blocat până la ora {panaLa.Value:HH:mm}.";

                AfiseazaEroare($"Contul '{u}' este blocat temporar.\n" +
                               $"Poți reîncerca după ora {panaLa.Value:HH:mm}.");
            }
            else
            {
                // Cont deblocat (între timp) — curățăm avertismentul vechi.
                lblTentativeInfo.Visible = false;
                pbTentative.Visible      = false;
                if (lblMesajEroare.Text != null &&
                    lblMesajEroare.Text.StartsWith("Contul '", StringComparison.Ordinal))
                    lblMesajEroare.Visible = false;
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
                using (var conn = SqlDb.CreateConnection())
                {
                    await conn.OpenAsync();

                    string sql = @"SELECT email FROM Utilizatori
                                   WHERE username = @u AND activ = 1";
                    using (var cmd = new SqlCommand(sql, conn))
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

                        // ── FIX BUG-03: întâi TRIMITEM emailul, abia apoi schimbăm
                        // parola în baza de date. Înainte ordinea era inversă:
                        // parola era deja modificată (UPDATE) înainte de
                        // SendMailAsync, iar blocul catch doar afișa o eroare.
                        // Dacă trimiterea eșua (SMTP greșit, rețea, port), contul
                        // rămânea cu o parolă temporară pe care utilizatorul nu o
                        // primea niciodată → pierdea complet accesul la cont.
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

                        // Abia acum, după confirmarea trimiterii, aplicăm parola nouă.
                        string updSql = @"UPDATE Utilizatori SET parola_hash = @hash
                                          WHERE username = @u";
                        using (var cmdUpd = new SqlCommand(updSql, conn))
                        {
                            cmdUpd.Parameters.Add("@hash",
                                System.Data.SqlDbType.NVarChar, 256).Value = hashTemp;
                            cmdUpd.Parameters.Add("@u",
                                System.Data.SqlDbType.NVarChar, 50).Value  = username;
                            await cmdUpd.ExecuteNonQueryAsync();
                        }

                        // FIX BUG-03 (audit): și resetarea prin email trebuie să
                        // lase urmă în jurnalul de securitate, ca restul acțiunilor
                        // administrative.
                        AppLogger.Security(
                            $"Resetare parola trimisa pe '{email}' pentru '{username}'",
                            username);
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
            lblMesajEroare.Text      = mesaj;
            lblMesajEroare.ForeColor = Design.DangerBright;
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
