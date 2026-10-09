using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using AppClubCalculatoare.UI;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Profilul utilizatorului curent: datele contului din BD,
    /// permisiunile acordate (din PuncteAcces.Toti ∩ sesiune) si
    /// statistica de favorite. Buton direct catre schimbarea parolei.
    ///
    /// Accesibil oricarui utilizator autentificat (sectiunea CONT).
    /// </summary>
    public class FormProfil : Form
    {
        private readonly string _connStr = DatabaseConfig.ConnectionString;

        private Label lblInc;
        private ListBox lstPermisiuni;
        private Label lblRezumat;

        public FormProfil()
        {
            this.Text            = "Profilul meu";
            this.Size            = new Size(640, 620);
            this.MinimumSize     = new Size(560, 520);
            this.StartPosition   = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox     = true;
            this.BackColor       = MaterialTheme.Background;
            this.Font            = MaterialTheme.FontBody;
            BuildUI();
            IncarcaDate();
        }

        private void BuildUI()
        {
            // ── Continut central (Dock.Fill) — adaugat PRIMUL ─────
            var content = MaterialUIHelper.ContentPanel();
            content.Padding = new Padding(24, 16, 24, 12);
            this.Controls.Add(content);

            // ── Bara inferioara ───────────────────────────────────
            FlowLayoutPanel leftFlow, rightFlow;
            var bottomBar = MaterialUIHelper.SplitBottomBar(out leftFlow, out rightFlow);
            this.Controls.Add(bottomBar);

            var btnParola = MaterialUIHelper.Button("🔑  Schimbă parola",
                MaterialButtonStyle.Secondary, 170);
            btnParola.Click += (s, e) =>
            {
                var frm = new FormSchimbareParola(SessionInfo.Username);
                if (frm.ShowDialog(this) == DialogResult.OK) IncarcaDate();
            };
            leftFlow.Controls.Add(btnParola);

            var btnInchide = MaterialUIHelper.Button("✖  Închide", MaterialButtonStyle.Outline, 120);
            btnInchide.Click += (s, e) => this.Close();
            rightFlow.Controls.Add(btnInchide);
            this.CancelButton = btnInchide;

            // ── Header colorat (Dock.Top) — adaugat ULTIMUL ───────
            var header = MaterialUIHelper.Header(
                "Profilul meu",
                "Cont: " + SessionInfo.Username + "  ·  Rol: " + SessionInfo.Rol,
                null, MaterialTheme.Primary);
            this.Controls.Add(header);

            // ── Card 1: date cont ─────────────────────────────────
            var cardDate = new MaterialCard
            {
                Location = new Point(0, 0),
                Size     = new Size(560, 210),
                Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            content.Controls.Add(cardDate);

            var lblTitluDate = new Label
            {
                Text      = "Date cont",
                Font      = MaterialTheme.FontSubtitle,
                ForeColor = MaterialTheme.TextPrimary,
                AutoSize  = true,
                Location  = new Point(20, 14),
                BackColor = Color.Transparent
            };
            cardDate.Controls.Add(lblTitluDate);

            lblInc = new Label
            {
                Location = new Point(20, 44),
                Size     = new Size(520, 150),
                Font     = MaterialTheme.FontBody,
                ForeColor = MaterialTheme.TextSecondary,
                BackColor = Color.Transparent
            };
            cardDate.Controls.Add(lblInc);

            // ── Card 2: permisiuni ────────────────────────────────
            var cardPerm = new MaterialCard
            {
                Location = new Point(0, 224),
                Size     = new Size(560, 260),
                Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            content.Controls.Add(cardPerm);

            var lblTitluPerm = new Label
            {
                Text      = "Permisiunile tale",
                Font      = MaterialTheme.FontSubtitle,
                ForeColor = MaterialTheme.TextPrimary,
                AutoSize  = true,
                Location  = new Point(20, 14),
                BackColor = Color.Transparent
            };
            cardPerm.Controls.Add(lblTitluPerm);

            lstPermisiuni = new ListBox
            {
                Location   = new Point(20, 40),
                Size       = new Size(520, 170),
                Anchor     = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderStyle = BorderStyle.FixedSingle,
                Font       = MaterialTheme.FontSmall
            };
            cardPerm.Controls.Add(lstPermisiuni);

            lblRezumat = new Label
            {
                Location  = new Point(20, 216),
                Size      = new Size(520, 34),
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font      = MaterialTheme.FontSmall,
                ForeColor = MaterialTheme.TextSecondary,
                BackColor = Color.Transparent
            };
            cardPerm.Controls.Add(lblRezumat);

            // Re-dimensionare cardurilor la resize-ul ferestrei
            this.Resize += (s, e) =>
            {
                int w = content.ClientSize.Width - content.Padding.Horizontal;
                cardDate.Width = w;
                cardPerm.Width = w;
                lblInc.Width   = w - 40;
                lstPermisiuni.Width = w - 40;
                lblRezumat.Width    = w - 40;
            };
        }

        private void IncarcaDate()
        {
            // ── Date cont din BD ──────────────────────────────────
            string nume = SessionInfo.NumeComplet, email = "—",
                   creare = "—", ultima = "—";
            try
            {
                const string sql = @"
                    SELECT nume_complet, ISNULL(email, '—'),
                           CONVERT(VARCHAR, data_creare, 103),
                           CONVERT(VARCHAR, ultima_autentificare, 103) + ' ' +
                           CONVERT(VARCHAR, ultima_autentificare, 108)
                    FROM   Utilizatori
                    WHERE  username = @user";
                using (var conn = new SqlConnection(_connStr))
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@user", System.Data.SqlDbType.NVarChar, 50)
                                  .Value = SessionInfo.Username;
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            nume    = reader.GetString(0);
                            email   = reader.GetString(1);
                            creare  = reader.GetString(2);
                            ultima  = reader.IsDBNull(3) ? "—" : reader.GetString(3);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Profil: nu s-au putut citi datele contului: " + ex.Message,
                    "FormProfil");
            }

            lblInc.Text =
                "Nume complet:     " + nume + "\n" +
                "Utilizator:       " + SessionInfo.Username + "\n" +
                "Rol:              " + SessionInfo.Rol + "\n" +
                "Email:            " + email + "\n" +
                "Cont creat:       " + creare + "\n" +
                "Ultima accesare:  " + ultima;

            // ── Permisiunile din sesiune ──────────────────────────
            lstPermisiuni.Items.Clear();
            int acordate = 0;
            foreach (KeyValuePair<string, string> punct in PuncteAcces.Toti)
            {
                bool are = SessionInfo.ArePermisiune(punct.Key);
                if (are) acordate++;
                lstPermisiuni.Items.Add((are ? "✅  " : "⛔  ") + punct.Value);
            }

            // ── Statistica favorite (daca tabela exista) ──────────
            string favInfo = "";
            try
            {
                if (FavoriteService.TabelaExista())
                    favInfo = "  ·  Favorite: " + FavoriteService.GetIduriFavorite().Count;
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Profil: favorite indisponibile: " + ex.Message, "FormProfil");
            }

            lblRezumat.Text =
                (SessionInfo.EsteAdmin
                    ? "Administrator — acces complet la toate functiile."
                    : "Drepturi acordate: " + acordate + " din " + PuncteAcces.Toti.Length +
                      " (se administreaza din Gestionare Utilizatori).") +
                favInfo;
        }
    }
}
