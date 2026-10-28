using System;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Permite oricarui utilizator autentificat sa isi schimbe propria parola.
    /// Intrebarea 6: schimbare parola fara ajutorul administratorului.
    ///
    /// Flux:
    ///   1. Verifica parola actuala prin AuthService.
    ///   2. Valideaza parola noua (minim 6 caractere, cele doua campuri coincid).
    ///   3. Calculeaza SHA-256 si actualizeaza parola_hash in DB.
    ///
    /// Designul e în FormSchimbareParola.Designer.cs.
    /// </summary>
    public partial class FormSchimbareParola : Form
    {
        private readonly string _username;
        private readonly string _connStr = DatabaseConfig.ConnectionString;

        public FormSchimbareParola(string username)
        {
            _username = username;
            InitializeComponent();
            UI.WindowChrome.EnableDarkTitleBar(this);
            lblSubHeader.Text = "Cont: " + _username;
        }

        // ════════════════════════════════════════════════════════════
        //  EVENIMENTE UI (legate în FormSchimbareParola.Designer.cs)
        // ════════════════════════════════════════════════════════════

        private void BottomBar_Paint(object sender, PaintEventArgs e)
        {
            // Linia de separare foloseste Divider din tema inchisa; un gri
            // deschis (#E0E0E0) ar stralucea pe bara de jos inchisa.
            using (var pen = new System.Drawing.Pen(UI.MaterialTheme.Divider, 1f))
                e.Graphics.DrawLine(pen, 0, 0, bottomBar.Width, 0);
        }

        private void BtnAnulare_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private async void BtnSalveaza_Click(object sender, EventArgs e)
        {
            await SchimbaParolaAsync();
        }

        // ════════════════════════════════════════════════════════════
        //  LOGICA DE SCHIMBARE PAROLA (neschimbată)
        // ════════════════════════════════════════════════════════════
        private async Task SchimbaParolaAsync()
        {
            lblMesaj.Visible = false;

            // 1. Validare campuri
            if (string.IsNullOrWhiteSpace(txtParolaVeche.Text) ||
                string.IsNullOrWhiteSpace(txtParolaNoua.Text)  ||
                string.IsNullOrWhiteSpace(txtConfirmare.Text))
            {
                AfiseazaMesaj("Completează toate câmpurile!", UI.MaterialTheme.DangerBright);
                return;
            }

            if (txtParolaNoua.Text != txtConfirmare.Text)
            {
                AfiseazaMesaj("Parolele noi nu coincid!", UI.MaterialTheme.DangerBright);
                return;
            }

            if (txtParolaNoua.Text.Length < 6)
            {
                AfiseazaMesaj("Parola nouă trebuie să aibă minim 6 caractere!",
                    UI.MaterialTheme.DangerBright);
                return;
            }

            if (txtParolaNoua.Text == txtParolaVeche.Text)
            {
                AfiseazaMesaj("Parola nouă trebuie să fie diferită de cea actuală!",
                    UI.MaterialTheme.WarningBright);
                return;
            }

            // 2. Verifica parola actuala prin AuthService
            AfiseazaMesaj("Se verifică...", UI.MaterialTheme.TextSecondary);
            var rezultat = await AuthService.AutentificaAsync(_username, txtParolaVeche.Text);

            if (!rezultat.Succes)
            {
                AfiseazaMesaj("Parola actuală este incorectă!", UI.MaterialTheme.DangerBright);
                txtParolaVeche.Clear();
                txtParolaVeche.Focus();
                return;
            }

            // 3. Actualizeaza parola in DB
            try
            {
                string hashNou = AuthService.HashParola(txtParolaNoua.Text);

                using (var conn = SqlDb.CreateConnection())
                {
                    await conn.OpenAsync();
                    const string sql = @"
                        UPDATE Utilizatori
                        SET    parola_hash = @hash
                        WHERE  username    = @user";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.Add("@hash", SqlDbType.NVarChar, 256).Value = hashNou;
                        cmd.Parameters.Add("@user", SqlDbType.NVarChar, 50).Value  = _username;
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                AppLogger.Security($"Parola schimbata de utilizatorul '{_username}'");
                MessageBox.Show("Parola a fost schimbata cu succes!",
                    "Succes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormSchimbareParola");
                AfiseazaMesaj("Eroare la salvare: " + ex.Message, UI.MaterialTheme.DangerBright);
            }
        }

        private void AfiseazaMesaj(string mesaj, System.Drawing.Color culoare)
        {
            lblMesaj.Text      = mesaj;
            lblMesaj.ForeColor = culoare;
            lblMesaj.Visible   = true;
        }
    }
}
