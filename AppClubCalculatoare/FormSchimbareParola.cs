using System;
using System.Data.SqlClient;
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
            lblSubHeader.Text = "Cont: " + _username;
        }

        // ════════════════════════════════════════════════════════════
        //  EVENIMENTE UI (legate în FormSchimbareParola.Designer.cs)
        // ════════════════════════════════════════════════════════════

        private void BottomBar_Paint(object sender, PaintEventArgs e)
        {
            using (var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(0xE0, 0xE0, 0xE0), 1f))
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
                AfiseazaMesaj("Completeaza toate campurile!", System.Drawing.Color.Red);
                return;
            }

            if (txtParolaNoua.Text != txtConfirmare.Text)
            {
                AfiseazaMesaj("Parolele noi nu coincid!", System.Drawing.Color.Red);
                return;
            }

            if (txtParolaNoua.Text.Length < 6)
            {
                AfiseazaMesaj("Parola noua trebuie sa aiba minim 6 caractere!",
                    System.Drawing.Color.Red);
                return;
            }

            if (txtParolaNoua.Text == txtParolaVeche.Text)
            {
                AfiseazaMesaj("Parola noua trebuie sa fie diferita de cea actuala!",
                    System.Drawing.Color.OrangeRed);
                return;
            }

            // 2. Verifica parola actuala prin AuthService
            AfiseazaMesaj("Se verifica...", System.Drawing.Color.Gray);
            var rezultat = await AuthService.AutentificaAsync(_username, txtParolaVeche.Text);

            if (!rezultat.Succes)
            {
                AfiseazaMesaj("Parola actuala este incorecta!", System.Drawing.Color.Red);
                txtParolaVeche.Clear();
                txtParolaVeche.Focus();
                return;
            }

            // 3. Actualizeaza parola in DB
            try
            {
                string hashNou = AuthService.HashParola(txtParolaNoua.Text);

                using (var conn = new SqlConnection(_connStr))
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
                AfiseazaMesaj("Eroare la salvare: " + ex.Message, System.Drawing.Color.Red);
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
