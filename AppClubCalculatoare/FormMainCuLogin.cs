using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using AppClubCalculatoare.UI;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Fereastra principală după autentificare — design modern.
    /// Layout: TopBar (60px) + Sidebar (240px) + Content (dashboard).
    ///
    /// Structura statică (topBar, sidebar, bottomBar, content) e în
    /// FormMainCuLogin.Designer.cs; aici rămân elementele dinamice
    /// (meniul sidebar după permisiuni, datele utilizatorului, dashboard)
    /// și toată logica:
    ///   Q6  — buton "Schimba parola"
    ///   Q15 — buton "Gestionare Utilizatori" (Admin)
    ///   Sistem drepturi granulare (PermissionService)
    ///   Auto-logout la inactivitate
    /// </summary>
    public partial class FormMainCuLogin : Form
    {
        private readonly string connectionString = DatabaseConfig.ConnectionString;
        private readonly string rolUtilizator;
        private readonly string numeUtilizator;

        private PanouDashboard panouDashboard;
        private NavButton activeNav;

        // ── Sala Live (harta stațiilor în timp real) ─────────────
        private NavButton navSalaLive;
        private HartaSala hartaSala;

        // ── Butoane de control ferestre (chrome borderless) ───────
        private WindowButton btnMin, btnMax, btnClose;
        private FormWindowState _stareInainteDeMaximizare = FormWindowState.Normal;

        private bool EsteAdmin => rolUtilizator == "Administrator";

        /// <summary>Protecție anti-recursivitate pentru reașezarea meniului lateral.</summary>
        private bool _reasezMeniuInCurs;

        /// <summary>
        /// Numele utilizatorului fără sufixul de rol din paranteze
        /// (nume_complet din BD este de forma „Mihail (Utilizator)").
        /// </summary>
        private string NumeAfisat
        {
            get
            {
                if (string.IsNullOrWhiteSpace(numeUtilizator)) return "";
                int i = numeUtilizator.IndexOf('(');
                return (i > 0 ? numeUtilizator.Substring(0, i) : numeUtilizator).Trim();
            }
        }

        // ════════════════════════════════════════════════════════════
        //  CHROME FEREASTRĂ BORDERLESS (umbră, colturi, butoane)
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Activează fereastra fără bordură: adaugă butoanele de control
        /// (min / max / close) în colțul din dreapta-sus al topBar-ului și
        /// leagă evenimentul de tragere pe zona de titlu.
        /// </summary>
        private void ConfigurareChromeFereastra()
        {
            // NU setăm FormBorderStyle = None: acel stil elimină WS_THICKFRAME și
            // rupe redimensionarea din margini + Aero Snap. Rămâne Sizable
            // (implicit); rama vizibilă este eliminată complet prin WM_NCCALCSIZE=0
            // din WindowChrome.ProcessMessage, păstrând toate funcțiile de resize.

            btnMin   = new WindowButton { Kind = WindowButtonKind.Minimize, Name = "btnWinMin" };
            btnMax   = new WindowButton { Kind = WindowButtonKind.Maximize,  Name = "btnWinMax" };
            btnClose = new WindowButton { Kind = WindowButtonKind.Close,    Name = "btnWinClose" };

            MaterialUIHelper.AtasareTooltip(btnMin,   "Minimizare", "Minimizează fereastra în bara de activități.");
            MaterialUIHelper.AtasareTooltip(btnMax,   "Maximizare", "Mărește fereastra pe tot ecranul (sau o readuce la dimensiunea anterioară).");
            MaterialUIHelper.AtasareTooltip(btnClose, "Închidere",  "Închide aplicația.");

            btnMin.Click   += (s, e) => WindowState = FormWindowState.Minimized;
            btnMax.Click   += (s, e) => ComutaMaximizare();
            btnClose.Click += (s, e) => Close();

            // Z-order: close cel mai din dreapta, apoi max, apoi min.
            topBar.Controls.Add(btnMin);
            topBar.Controls.Add(btnMax);
            topBar.Controls.Add(btnClose);
            btnClose.BringToFront();
            btnMax.BringToFront();
            btnMin.BringToFront();

            // Tragere fereastră prin bara de titlu (logo + brand + spațiu gol).
            WindowChrome.AttachDrag(this, topBar, lblBrand, pnlLogo);

            // Repoziționează butoanele când se schimbă lățimea barei.
            TopBar_Resize(this, EventArgs.Empty);

            // Colțuri rotunjite Win11 (ignorat pe versiuni mai vechi).
            this.HandleCreated += (s, e) => WindowChrome.ApplySystemChrome(this);
            if (IsHandleCreated) WindowChrome.ApplySystemChrome(this);
        }

        private void ComutaMaximizare()
        {
            if (WindowState == FormWindowState.Maximized)
            {
                WindowState = _stareInainteDeMaximizare == FormWindowState.Maximized
                    ? FormWindowState.Normal : _stareInainteDeMaximizare;
                btnMax.Kind = WindowButtonKind.Maximize;
            }
            else
            {
                _stareInainteDeMaximizare = WindowState == FormWindowState.Normal
                    ? FormWindowState.Normal : WindowState;
                WindowState = FormWindowState.Maximized;
                btnMax.Kind = WindowButtonKind.Restore;
            }
        }

        // Umbră moale prin stilul de clasă CS_DROPSHADOW (Win10; Win11 foloseste DWM).
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= WindowChrome.CS_DROPSHADOW;
                return cp;
            }
        }

        // Punctul central al chrome-ului borderless: redimensionare + hit-testing.
        protected override void WndProc(ref Message m)
        {
            if (!WindowChrome.ProcessMessage(this, ref m))
                base.WndProc(ref m);
        }

        public FormMainCuLogin(string rol, string nume)
        {
            rolUtilizator  = rol;
            numeUtilizator = nume;
            InitializeComponent();

            // FIX BUG-02: lățimea câmpului de căutare este administrată de noi
            // (vezi AranjeazaCampulCautare). Scoatem ancora Right, altfel motorul
            // de layout o recalculează și anulează valoarea corectă.
            if (txtSearch != null)
                txtSearch.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            ConfigurareChromeFereastra();
            IncarcaDateUtilizator();
            AplicaPictograme();
            ConstruiesteNavSalaLive();
            ConstruiesteContinut();
            CreeazaTimerInactivitate();

            // FIX BUG-01/BUG-11: meniul lateral se reașază și la redimensionarea
            // ferestrei (înălțimea disponibilă se schimbă).
            this.Resize += (s, e) => ReaseazaMeniuSidebar();
        }

        // ════════════════════════════════════════════════════════════
        //  DATE UTILIZATOR (top bar + bara de jos)
        // ════════════════════════════════════════════════════════════

        private void IncarcaDateUtilizator()
        {
            // Avatar cu inițiala numelui
            lblInitial.Text = string.IsNullOrEmpty(numeUtilizator) ? "?"
                : numeUtilizator.Substring(0, 1).ToUpper();

            lblNume.Text  = numeUtilizator;
            lblRol.Text   = EsteAdmin ? "Administrator" : "Utilizator";

            // FIX_20: nume_complet din BD conține deja rolul între paranteze („Alexandru (Administrator)") — nu-l mai adăugăm o dată.
            string afisUtilizator = numeUtilizator;
            if (!string.IsNullOrEmpty(rolUtilizator) &&
                afisUtilizator.IndexOf(rolUtilizator, StringComparison.OrdinalIgnoreCase) < 0)
                afisUtilizator += "  (" + rolUtilizator + ")";
            lblStatusUser.Text = "Conectat: " + afisUtilizator;
        }

        // ════════════════════════════════════════════════════════════
        //  PICTOGRAME SIDEBAR — glife Segoe Fluent Icons (MDL2).
        //  Designerul stochează vechile emoji; le suprascriem în cod cu
        //  codepoints MDL2 stabile pe toate versiunile de Windows (§5.3).
        // ════════════════════════════════════════════════════════════
        private void AplicaPictograme()
        {
            if (navEchipamente != null) navEchipamente.Icon = "\uEC0B";  // laptop
            if (navSali         != null) navSali.Icon         = "\uE7F8";  // clădire
            if (navMembri       != null) navMembri.Icon       = "\uE716";  // persoane
            if (navSesiuni      != null) navSesiuni.Icon      = "\uE8F5";  // listă
            if (navRezervari    != null) navRezervari.Icon    = "\uE787";  // calendar
            if (navFavorite     != null) navFavorite.Icon     = "\uE734";  // stea
            if (navCalculator   != null) navCalculator.Icon   = "\uE8EF";  // calculator
            if (navOperatori    != null) navOperatori.Icon    = "\uE77C";  // persoană
            if (navRapoarte     != null) navRapoarte.Icon     = "\uE9F5";  // grafic
            if (navUtilizatori  != null) navUtilizatori.Icon  = "\uE77B";  // grup
            if (navProfil       != null) navProfil.Icon       = "\uE77B";  // profil
            if (navParola       != null) navParola.Icon       = "\uE192";  // cheie
            if (navDeconectare  != null) navDeconectare.Icon  = "\uE7E8";  // ieșire
        }

        // ════════════════════════════════════════════════════════════
        //  EVENIMENTE DE LAYOUT (legate în FormMainCuLogin.Designer.cs)
        // ════════════════════════════════════════════════════════════

        private void FormMainCuLogin_Shown(object sender, EventArgs e)
        {
            // FIX_22b: la afișare readucem fereastra în zona vizibilă — după schimbarea rezoluției/monitorului
            // putea rămâne parțial în afara ecranului, cu butonul „Deconectare" tăiat de marginea ecranului.
            try
            {
                var zona = Screen.FromControl(this).WorkingArea;
                int x = this.Left, y = this.Top;
                bool mutat = false;
                if (this.Right > zona.Right)  { x = zona.Right - this.Width;   mutat = true; }
                if (x < zona.Left)            { x = zona.Left;                 mutat = true; }
                if (this.Bottom > zona.Bottom){ y = zona.Bottom - this.Height; mutat = true; }
                if (y < zona.Top)             { y = zona.Top;                  mutat = true; }
                if (mutat) this.Location = new Point(Math.Max(zona.Left, x), Math.Max(zona.Top, y));
            }
            catch (Exception ex) { AppLogger.Warn("Aducere fereastra pe ecran esuata: " + ex.Message, "FormMainCuLogin"); }

            // Colțurile rotunjite sunt desenate cu antialiasing în paint-uri
            // (fără Region decupat — acela lăsa muchii „dințate").

            // Sigla + avatarul, desenate în gradient (același limbaj ca login-ul).
            pnlLogo.Paint += LogoBrand_Paint;
            avatar.Paint += Avatar_Paint;

            // Accesibilitate: tooltip + nume accesibile pentru elementele pur iconice
            MaterialUIHelper.AtasareTooltip(lblSearchIcon, "Căutare",
                "Caută în echipamente, săli, membri, sesiuni, rezervări și operatori. Apasă Enter.");
            MaterialUIHelper.AtasareTooltip(avatar, "Utilizator curent",
                string.IsNullOrEmpty(numeUtilizator) ? "Utilizator conectat." : "Utilizator conectat: " + numeUtilizator + ".");
            if (txtSearch != null)
                txtSearch.AccessibleName = "Căutare globală";

            // FIX BUG-13: „Profilul meu" exista doar ca punct în sidebar, iar
            // acesta cădea sub marginea ferestrei (BUG-01), deci pagina de profil
            // era inaccesibilă. Avatarul + panoul utilizatorului din bara de sus
            // devin acum o a doua cale de acces, mereu vizibilă.
            foreach (Control c in new Control[] { userPanel, avatar, lblNume, lblRol, lblInitial })
            {
                if (c == null) continue;
                c.Cursor = Cursors.Hand;
                c.Click += (s, e) => DeschideFormProfil();
            }
            MaterialUIHelper.AtasareTooltip(userPanel, "Profilul meu",
                "Deschide pagina profilului tău (date cont, istoric, favorite).");

            AplicaPermisiuniSidebar();

            // Fără control focalizat la deschidere: altfel primul ComboBox
            // („An") apare cu evidențierea albastră nativă, ca un câmp activ
            // pe care utilizatorul nu l-a cerut.
            this.ActiveControl = null;
        }

        private void TopBar_Paint(object sender, PaintEventArgs e)
        {
            // Separator de 1px sub bara de sus (fără linie de accent colorată).
            Design.DrawSeparator(e.Graphics, topBar.Height - 1, topBar.Width, Design.Border);
        }

        private void TopBar_Resize(object sender, EventArgs e)
        {
            // Butoanele de control ferestre, aliniate la dreapta (3 × 46px),
            // cu un mic decupaj (8px) față de marginea de redimensionare (6px),
            // ca aceasta să nu intercepteze click-ul pe „Închide".
            if (btnMin != null && btnMax != null && btnClose != null)
            {
                int btnW = btnClose.Width;                    // 46
                int totalW = btnW * 3;                        // 138
                int rightInset = 8;                           // > ResizeBorder (6)
                int by = Math.Max(0, (topBar.Height - btnClose.Height) / 2);

                btnClose.Location = new Point(topBar.Width - rightInset - btnW,     by);
                btnMax.Location   = new Point(topBar.Width - rightInset - btnW * 2, by);
                btnMin.Location   = new Point(topBar.Width - rightInset - btnW * 3, by);

                // userPanel se oprește înaintea butoanelor de control.
                userPanel.Location = new Point(topBar.Width - totalW - userPanel.Width - 24,
                    Math.Max(0, (topBar.Height - userPanel.Height) / 2));
                // searchBox se oprește înaintea userPanel (fără suprapunere).
                searchBox.Width = Math.Max(200, userPanel.Location.X - searchBox.Left - 12);
            }
            else
            {
                userPanel.Location = new Point(topBar.Width - userPanel.Width - 20,
                    Math.Max(0, (topBar.Height - userPanel.Height) / 2));
                searchBox.Width = Math.Max(200, topBar.Width - 640);
            }

            // FIX BUG-02: odată ce bara de căutare și-a schimbat lățimea, trebuie
            // recalculată și lățimea câmpului de text din interiorul ei.
            AranjeazaCampulCautare();
        }

        /// <summary>
        /// FIX BUG-02: așază iconița + câmpul de text în interiorul barei de căutare.
        ///
        /// Înainte: bara (searchBox) era lărgită din cod până la ~676px, dar câmpul
        /// de text rămânea la ~110px din cele ~350px proiectate. Efectul vizibil:
        /// placeholder-ul „Caută echipamente, membri, sesiuni..." era tăiat la
        /// „Caută echipament", iar ~3/4 din bara de căutare era zonă moartă —
        /// click-ul pe acea porțiune NU aducea focusul în câmp.
        ///
        /// Calculăm explicit poziția și lățimea pornind de la marginea dreaptă a
        /// iconiței, deci rezultatul e corect indiferent de scalarea DPI/font.
        /// </summary>
        private void AranjeazaCampulCautare()
        {
            if (searchBox == null || txtSearch == null || lblSearchIcon == null) return;

            int stanga = lblSearchIcon.Right + 8;
            int latime = searchBox.ClientSize.Width - stanga - 12;
            if (latime < 80) latime = 80;   // niciodată mai îngust decât câteva cuvinte

            txtSearch.Left  = stanga;
            txtSearch.Width = latime;
        }

        private void SearchBox_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Fundalul din spatele colțurilor + umplerea rotunjită a câmpului
            // (înlocuiește vechiul Region, care lăsa muchii „dințate").
            g.Clear(Design.ResolveOpaqueBackground(searchBox, Design.Surface));

            var rect = new Rectangle(0, 0, Math.Max(2, searchBox.Width - 1),
                Math.Max(2, searchBox.Height - 1));
            using (var path = Design.RoundedPath(rect, Design.RadiusSmall))
            {
                using (var brush = new SolidBrush(Design.SurfaceRaised))
                    g.FillPath(brush, path);
                using (var pen = new Pen(Design.Border, 1f))
                    g.DrawPath(pen, path);
            }
        }

        /// <summary>Sigla din bara de sus: pătrat rotunjit în culoarea de accent.</summary>
        private void LogoBrand_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Design.ResolveOpaqueBackground(pnlLogo, Design.Surface));
            var rect = new Rectangle(0, 0, Math.Max(2, pnlLogo.Width - 1), Math.Max(2, pnlLogo.Height - 1));

            using (var path = Design.RoundedPath(rect, Design.RadiusSmall))
            using (var brush = new SolidBrush(Design.Accent))
                g.FillPath(brush, path);
        }

        /// <summary>Avatarul utilizatorului: cerc plat în culoarea de accent.</summary>
        private void Avatar_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Design.ResolveOpaqueBackground(avatar, Design.Surface));
            var rect = new Rectangle(0, 0, Math.Max(2, avatar.Width - 1), Math.Max(2, avatar.Height - 1));

            using (var path = Design.RoundedPath(rect, avatar.Width / 2))
            using (var brush = new SolidBrush(Design.Accent))
                g.FillPath(brush, path);
        }

        private void BottomBar_Paint(object sender, PaintEventArgs e)
        {
            Design.DrawSeparator(e.Graphics, 0, bottomBar.Width, Design.Border);
        }

        // ════════════════════════════════════════════════════════════
        //  CĂUTARE GLOBALĂ (bara de sus) — Enter deschide prima tabelă
        //  la care utilizatorul are drept și care conține rezultate.
        // ════════════════════════════════════════════════════════════

        private const string PlaceholderCautare = "Caută...";

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            CautaGlobal();
        }

        private void TxtSearch_GotFocus(object sender, EventArgs e)
        {
            if (txtSearch.Text.StartsWith("Caută"))
            {
                txtSearch.Text = "";
                txtSearch.ForeColor = Design.Text;
            }
        }

        private void TxtSearch_LostFocus(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                txtSearch.Text = PlaceholderCautare;
                txtSearch.ForeColor = Design.TextDim;
            }
        }

        /// <summary>
        /// Căutare globală: la Enter în bara de sus, deschide prima tabelă
        /// (în ordinea din meniu) la care utilizatorul are drept de vizualizare
        /// și care conține cel puțin o înregistrare potrivită. Dacă nicio tabelă
        /// nu e permisă ⇒ mesaj de acces restricționat (fail-closed); dacă nu
        /// există rezultate ⇒ mesaj explicit.
        /// </summary>
        private void CautaGlobal()
        {
            string text = txtSearch.Text;
            if (text == PlaceholderCautare) text = "";
            text = (text ?? "").Trim();

            if (text.Length == 0) return;   // câmp gol ⇒ nimic

            string[] tabele = { "Echipamente", "Sali", "Membri",
                                "Sesiuni", "Rezervari", "Operatori" };

            bool areVreunDrept = false;
            foreach (string tabela in tabele)
            {
                string cod = PuncteAcces.CodMeniu(tabela);
                if (cod == null) continue;                       // fail-closed
                if (!PermissionService.VerificaAre(cod, EsteAdmin)) continue;
                areVreunDrept = true;

                if (!ExistaRezultate(tabela, text)) continue;

                DeschideFormCRUD(tabela, TitluPentruTabela(tabela), text);
                return;
            }

            if (!areVreunDrept)
            {
                AppLogger.Security(
                    $"Acces blocat: '{SessionInfo.Username}' a incercat cautarea globala");
                PermissionService.AfiseazaMesajAcces();
                return;
            }

            MessageBox.Show(
                $"Nu s-au găsit rezultate pentru «{text}».",
                "Căutare", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string TitluPentruTabela(string tabela)
        {
            switch (tabela)
            {
                case "Echipamente": return "Gestionare Echipamente";
                case "Sali":         return "Gestionare Sali";
                case "Membri":       return "Gestionare Membri";
                case "Sesiuni":      return "Gestionare Sesiuni";
                case "Rezervari":    return "Gestionare Rezervari";
                case "Operatori":    return "Gestionare Operatori";
                default:             return tabela;
            }
        }

        /// <summary>
        /// Verifică rapid în baza de date dacă tabela conține cel puțin o
        /// înregistrare a cărei coloane text (listă fixă = whitelist din cod)
        /// conțin termenul căutat. Interogarea folosește exact JOIN-urile din
        /// FormCRUD (de acolo provin coloanele afișate în grilă: nume_sala,
        /// operator_sef, echipament, membru, operator). Tabelele, coloanele și
        /// expresiile SQL provin din cod (nu de la utilizator); valoarea este
        /// trimisă ca <see cref="SqlParameter"/> (fără concatenare în SQL).
        /// Orice eroare de rețea ⇒ «nu există» (mesajul de rezultate-goale va
        /// apărea, nu un crash).
        /// </summary>
        private bool ExistaRezultate(string tabela, string text)
        {
            // FIX B1: aliasurile nume_sala / operator_sef / echipament / membru /
            // operator NU există în tabelele de bază — sunt create de JOIN-urile
            // din FormCRUD.IncarcaDate. Fără aceste JOIN-uri, interogarea eșua cu
            // „Invalid column name" pe 5 tabele, iar căutarea globală raporta
            // 0 rezultate pentru orice termen (doar „Membri" funcționa).
            string fromSql;
            string[] expresii;
            switch (tabela)
            {
                case "Echipamente":
                    fromSql = @"FROM Echipamente p
                                JOIN Sali b ON p.sala_id = b.id_sala
                                LEFT JOIN Operatori a ON b.operator_sef_id = a.id_operator";
                    expresii = new[] { "p.tip_echipament", "p.denumire", "p.oras", "p.status",
                                       "b.nume_sala", "a.nume + ' ' + a.prenume", "p.descriere" };
                    break;
                case "Sali":
                    fromSql = @"FROM Sali b
                                LEFT JOIN Operatori a ON b.operator_sef_id = a.id_operator";
                    expresii = new[] { "b.nume_sala", "b.oras", "b.adresa",
                                       "a.nume + ' ' + a.prenume" };
                    break;
                case "Operatori":
                    fromSql = @"FROM Operatori a
                                LEFT JOIN Sali b ON a.sala_id = b.id_sala";
                    expresii = new[] { "a.nume", "a.prenume", "a.rol_club", "a.telefon",
                                       "a.email", "b.nume_sala" };
                    break;
                case "Membri":
                    fromSql = "FROM Membri c";
                    expresii = new[] { "c.nume", "c.prenume", "c.telefon", "c.email",
                                       "c.tip_membru", "c.oras_interes" };
                    break;
                case "Sesiuni":
                    fromSql = @"FROM Sesiuni t
                                JOIN Echipamente p ON t.id_echipament = p.id_echipament
                                JOIN Membri     c ON t.id_membru      = c.id_membru
                                JOIN Operatori  a ON t.id_operator    = a.id_operator";
                    expresii = new[] { "p.denumire", "c.nume + ' ' + c.prenume",
                                       "a.nume + ' ' + a.prenume", "t.tip_sesiune",
                                       "t.status", "t.observatii" };
                    break;
                case "Rezervari":
                    fromSql = @"FROM Rezervari v
                                JOIN Echipamente p ON v.id_echipament = p.id_echipament
                                JOIN Membri     c ON v.id_membru      = c.id_membru
                                JOIN Operatori  a ON v.id_operator    = a.id_operator";
                    expresii = new[] { "p.denumire", "c.nume + ' ' + c.prenume",
                                       "a.nume + ' ' + a.prenume", "v.status",
                                       "v.observatii" };
                    break;
                default:
                    return false;
            }

            try
            {
                var cond = new string[expresii.Length];
                for (int i = 0; i < expresii.Length; i++)
                    cond[i] = expresii[i] + " LIKE @q";

                // Wildcard-urile LIKE (%, _, [) din textul utilizatorului sunt
                // neutralizate (aceeași semantică literală ca filtrarea din
                // grilă — FormCRUD.EscapeRowFilter), ca termenul să nu fie
                // interpretat ca tipar.
                string model = text.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");

                using (var conn = SqlDb.CreateConnection())
                using (var cmd = new SqlCommand(
                    $"SELECT TOP 1 1 {fromSql} WHERE {string.Join(" OR ", cond)}", conn))
                {
                    cmd.Parameters.Add("@q", SqlDbType.NVarChar, 200).Value = "%" + model + "%";
                    conn.Open();
                    return cmd.ExecuteScalar() != null;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Căutare globală eșuată pe '" + tabela + "': " + ex.Message,
                    "FormMainCuLogin.CautaGlobal");
                return false;
            }
        }


        // ════════════════════════════════════════════════════════════
        //  ACȚIUNI BARA DE JOS
        // ════════════════════════════════════════════════════════════

        private void BtnParolaBottom_Click(object sender, EventArgs e)
        {
            DeschideFormSchimbareParola();
        }

        private void BtnDeconBottom_Click(object sender, EventArgs e)
        {
            // FIX BUG-04: dialog propriu cu butoane românești (MessageBox-ul Win32
            // afișa „Да"/"Нет" pe un Windows cu altă limbă de interfață).
            if (DialogHelper.Confirma(this, "Deconectare",
                    "Ești sigur că vrei să te deconectezi?"))
            {
                this.DialogResult = DialogResult.Retry;
                this.Close();
            }
        }

        // ════════════════════════════════════════════════════════════
        //  DASHBOARD sau grila MenuCard (fallback fara drepturi)
        // ════════════════════════════════════════════════════════════

        private void ConstruiesteContinut()
        {
            if (PermissionService.AreUI(PuncteAcces.PanouDashboard))
            {
                // Dashboard-ul își ia înălțimea completă și stă în partea de sus;
                // derularea rămâne exclusiv în contentPanel (un singur scrollbar).
                panouDashboard = new PanouDashboard(connectionString)
                {
                    Dock = DockStyle.Top,
                    AutoSize = false
                };
                panouDashboard.AlertaDoubleClick += DeschideAlerta;
                contentPanel.Controls.Add(panouDashboard);
                contentPanel.Controls.SetChildIndex(panouDashboard, 0);
                panouDashboard.BringToFront();
            }
            else
            {
                ConstruiesteBunVenit();
                ConstruiesteMeniuCards();
            }
        }

        /// <summary>
        /// Mesaj de bun-venit pentru utilizatorii fara tablou de bord
        /// (ex: rolul User — doar vizualizare anunturi + schimbare parola).
        /// </summary>
        private void ConstruiesteBunVenit()
        {
            var bunVenit = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 92,
                BackColor = Color.Transparent,
                Padding   = new Padding(4)
            };
            var lblSalut = new Label
            {
                // FIX BUG-10: era textul fix „Salut Membru" — nu conținea numele
                // utilizatorului autentificat. Acum salutul este personalizat.
                Text      = string.IsNullOrEmpty(NumeAfisat)
                            ? "Bine ai venit!"
                            : "Bine ai venit, " + NumeAfisat + "!",
                Font      = Design.FontDisplay,
                ForeColor = Design.Text,
                BackColor = Color.Transparent,
                Dock      = DockStyle.Top,
                Height    = 44,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var lblSub = new Label
            {
                Text      = "Echipamentele libere le găsești în meniul Echipamente.",
                Font      = Design.FontSubtitle,
                ForeColor = Design.TextMuted,
                BackColor = Color.Transparent,
                Dock      = DockStyle.Top,
                Height    = 26,
                TextAlign = ContentAlignment.MiddleLeft
            };
            bunVenit.Controls.Add(lblSub);
            bunVenit.Controls.Add(lblSalut);
            contentPanel.Controls.Add(bunVenit);
        }

        /// <summary>
        /// Fallback Material: grila de MenuCard-uri pentru utilizatorii
        /// fara acces la dashboard. Fiecare card deschide modulul respectiv.
        /// </summary>
        private void ConstruiesteMeniuCards()
        {
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(4)
            };
            contentPanel.Controls.Add(flow);
            // FIX_23: Dock.Fill trebuie asezat PRIMUL in Z-order (index 0) ca sa fie
            // procesat ultimul la layout, dupa ce panoul de bun-venit (Dock.Top) isi ia
            // banda de 92px. Altfel flow umplea tot contentPanel de la y=0, iar cardurile
            // de sus erau acoperite de panoul de bun-venit (se vedea doar o fantă jos).
            contentPanel.Controls.SetChildIndex(flow, 0);
            flow.BringToFront();

            AddMenuCard(flow, "\uEC0B", "Echipamente", "Gestionare echipamente",
                PuncteAcces.MeniuEchipamente,
                () => DeschideFormCRUD("Echipamente", "Gestionare Echipamente"));
            AddMenuCard(flow, "\uE7F8", "Sali", "Gestionare sali",
                PuncteAcces.MeniuSali,
                () => DeschideFormCRUD("Sali", "Gestionare Sali"));
            AddMenuCard(flow, "\uE716", "Membri", "Gestionare membri",
                PuncteAcces.MeniuMembri,
                () => DeschideFormCRUD("Membri", "Gestionare Membri"));
            AddMenuCard(flow, "\uE8F5", "Sesiuni", "Gestionare sesiuni",
                PuncteAcces.MeniuSesiuni,
                () => DeschideFormCRUD("Sesiuni", "Gestionare Sesiuni"));
            AddMenuCard(flow, "\uE787", "Rezervări", "Gestionare rezervări",
                PuncteAcces.MeniuRezervari,
                () => DeschideFormCRUD("Rezervari", "Gestionare Rezervari"));
            AddMenuCard(flow, "\uE77C", "Operatori", "Gestionare operatori",
                PuncteAcces.MeniuOperatori,
                () => DeschideFormCRUD("Operatori", "Gestionare Operatori"));
            AddMenuCard(flow, "\uE9F5", "Rapoarte", "Rapoarte și statistici",
                PuncteAcces.MeniuRapoarte,
                () => DeschideFormRapoarte());
            AddMenuCard(flow, "\uE734", "Favorite", "Echipamentele tale favorite",
                PuncteAcces.Favorite,
                () => DeschideFormFavorite());
            AddMenuCard(flow, "\uE8EF", "Calculator Cost", "Cost orar, durată, pachet și reduceri",
                PuncteAcces.CalculatorCost,
                () => DeschideFormCalculatorCost());
        }

        private void AddMenuCard(Control parent, string icon, string titlu, string subtitlu,
            string codPermisiune, Action actiune)
        {
            if (!PermissionService.AreUI(codPermisiune)) return;
            var card = new MenuCard
            {
                Icon = icon,
                CardTitle = titlu,
                Subtitle = subtitlu,
                Size = new Size(240, 120),
                Margin = new Padding(Design.Sp2)
            };
            card.Click += (s, e) => actiune();
            parent.Controls.Add(card);
        }

        // ════════════════════════════════════════════════════════════
        //  SALA LIVE — buton de navigare + panou-harta stații
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Creează butonul „Sala Live" în sidebar (primul element din
        /// secțiunea PRINCIPAL) și deplasează restul elementelor în jos
        /// cu 44px pentru a face loc. Butoanele statice din Designer sunt
        /// poziționate absolut, deci mutarea se face o singură dată aici.
        /// </summary>
        private void ConstruiesteNavSalaLive()
        {
            // FIX BUG-01: deplasarea manuală (+44) a punctelor existente a fost
            // eliminată — pozițiile sunt acum calculate de ReaseazaMeniuSidebar(),
            // care ține cont de punctele vizibile după permisiuni. Deplasarea
            // hardcodată nu era scalată de WinForms (spre deosebire de pozițiile
            // din Designer), deci contribuia direct la ieșirea meniului din ecran.

            navSalaLive = new NavButton
            {
                Name = "navSalaLive",
                Icon = "\uE768",               // glif MDL2 „Play" — sesiuni live
                Text = "Sala Live",
                Size = new Size(212, Design.NavItemH),
                Location = new Point(0, 40),
                Visible = false
            };
            navSalaLive.Click += (s, e) => DeschideSalaLive();
            navScroll.Controls.Add(navSalaLive);

            MaterialUIHelper.AtasareTooltip(navSalaLive, "Sala Live",
                "Harta stațiilor în timp real: status, timp rămas din sesiune și tarif orar.");
        }

        private void DeschideSalaLive()
        {
            if (!PermissionService.AreUI(PuncteAcces.MeniuEchipamente) &&
                !PermissionService.AreUI(PuncteAcces.PanouDashboard))
            {
                AppLogger.Security(
                    $"Acces blocat: '{SessionInfo.Username}' a incercat Sala Live");
                PermissionService.AfiseazaMesajAcces();
                return;
            }

            if (hartaSala == null)
            {
                hartaSala = new HartaSala(connectionString) { Visible = false };
                hartaSala.StationSelectat += DeschideStatie;
                hartaSala.Inchide += RevenireDinSalaLive;
                contentPanel.Controls.Add(hartaSala);
            }
            hartaSala.PermiteInapoi = true;

            // Ascunde conținutul anterior: tabloul de bord sau
            // panoul de bun-venit + grila de carduri (fără drept de dashboard).
            if (panouDashboard != null) panouDashboard.Visible = false;
            foreach (Control c in contentPanel.Controls)
                if (c is Panel) c.Visible = false;

            hartaSala.Visible = true;
            hartaSala.BringToFront();
            hartaSala.Reincarca();
            SetActiveNav(navSalaLive);
        }

        private void RevenireDinSalaLive()
        {
            if (hartaSala != null) hartaSala.Visible = false;

            if (panouDashboard != null)
            {
                panouDashboard.Visible = true;
                panouDashboard.BringToFront();
            }
            else
            {
                foreach (Control c in contentPanel.Controls)
                    if (c is Panel) c.Visible = true;
            }
            SetActiveNav(null);
        }

        private void DeschideStatie(UI.StationInfo st)
        {
            if (st == null) return;
            // Deschide lista Echipamente filtrată pe stația selectată.
            DeschideFormCRUD("Echipamente", "Gestionare Echipamente", st.Denumire);
        }

        // ════════════════════════════════════════════════════════════
        //  SIDEBAR — butoanele sunt statice (în Designer); aici doar
        //  aplicăm vizibilitatea după permisiunile utilizatorului.
        // ════════════════════════════════════════════════════════════
        private void AplicaPermisiuniSidebar()
        {
            if (navSalaLive != null)
                navSalaLive.Visible = PermissionService.AreUI(PuncteAcces.MeniuEchipamente) ||
                                       PermissionService.AreUI(PuncteAcces.PanouDashboard);

            navEchipamente.Visible = PermissionService.AreUI(PuncteAcces.MeniuEchipamente);
            navSali.Visible     = PermissionService.AreUI(PuncteAcces.MeniuSali);
            navMembri.Visible     = PermissionService.AreUI(PuncteAcces.MeniuMembri);
            navSesiuni.Visible  = PermissionService.AreUI(PuncteAcces.MeniuSesiuni);
            navRezervari.Visible   = PermissionService.AreUI(PuncteAcces.MeniuRezervari);
            navFavorite.Visible    = PermissionService.AreUI(PuncteAcces.Favorite);
            navCalculator.Visible  = PermissionService.AreUI(PuncteAcces.CalculatorCost);

            bool adminSection = EsteAdmin ||
                PermissionService.AreUI(PuncteAcces.MeniuRapoarte) ||
                PermissionService.AreUI(PuncteAcces.GestionareUtilizatori) ||
                PermissionService.AreUI(PuncteAcces.MeniuOperatori);
            lblSecAdministrare.Visible = adminSection;

            navOperatori.Visible      = EsteAdmin || PermissionService.AreUI(PuncteAcces.MeniuOperatori);
            navRapoarte.Visible    = EsteAdmin || PermissionService.AreUI(PuncteAcces.MeniuRapoarte);
            navUtilizatori.Visible = EsteAdmin || PermissionService.AreUI(PuncteAcces.GestionareUtilizatori);

            // FIX BUG-01 + BUG-11: după ce s-a decis ce e vizibil, reașezăm meniul.
            ReaseazaMeniuSidebar();
        }

        // ════════════════════════════════════════════════════════════
        //  FIX BUG-01 + BUG-11 — așezarea meniului din sidebar
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Reașază compact punctele de meniu, ținând cont doar de cele vizibile.
        ///
        /// Înainte: pozițiile veneau din Designer și erau scalate de WinForms la
        /// rulare (×~1,31), în timp ce deplasarea din ConstruiesteNavSalaLive era
        /// hardcodată (+44, nescalează). Conținutul rezultat (~907px) depășea
        /// înălțimea panoului (738px), astfel încât secțiunea „CONT"
        /// (Profilul meu / Schimbă parola / Deconectare) cădea sub marginea de jos
        /// a ferestrei și era imposibil de accesat — chiar și cu fereastra
        /// maximizată, fiindcă AutoScroll nu genera nicio bară de derulare
        /// (GetScrollInfo → niciun scrollbar; rotița mouse-ului nu mișca nimic).
        /// Deoarece DeschideFormProfil() are un singur apelant (punctul
        /// „Profilul meu"), pagina de profil nu putea fi deschisă deloc.
        ///
        /// În plus, punctele ascunse de permisiuni își păstrau coordonatele
        /// absolute, lăsând goluri mari în meniu.
        ///
        /// Acum: punctele vizibile sunt așezate consecutiv. Dacă nu încap toate
        /// (multe puncte + fereastră joasă), înălțimea rândurilor scade până la o
        /// limită lizibilă; dacă nici așa nu încap, se activează derularea.
        /// </summary>
        private void ReaseazaMeniuSidebar()
        {
            if (navScroll == null || _reasezMeniuInCurs) return;
            _reasezMeniuInCurs = true;
            try
            {
                Control[] ordine =
                {
                    lblSecPrincipal,
                    navSalaLive, navEchipamente, navSali, navMembri, navSesiuni,
                    navRezervari, navFavorite, navCalculator,
                    lblSecAdministrare,
                    navOperatori, navRapoarte, navUtilizatori,
                    lblSecCont,
                    navProfil, navParola, navDeconectare
                };

                // O etichetă de secțiune rămasă fără puncte vizibile se ascunde
                // (ex. „ADMINISTRARE" pentru un utilizator fără drepturi de admin).
                AscundeEtichetaFaraPuncte(lblSecPrincipal,
                    navSalaLive, navEchipamente, navSali, navMembri,
                    navSesiuni, navRezervari, navFavorite, navCalculator);
                AscundeEtichetaFaraPuncte(lblSecAdministrare,
                    navOperatori, navRapoarte, navUtilizatori);

                int padSus = navScroll.Padding.Top;
                int padJos = navScroll.Padding.Bottom;

                // Dimensiunea „naturală" a unui rând = cea mai mare înălțime
                // curentă (deja scalată de WinForms pentru DPI-ul curent).
                int hRand = 0, wRand = 0, nrRanduri = 0, nrEtichete = 0, spatiuEtichete = 0;

                foreach (var c in ordine)
                {
                    if (c is NavButton nb && nb.Visible)
                    {
                        nrRanduri++;
                        if (nb.Height > hRand) hRand = nb.Height;
                        if (nb.Width  > wRand) wRand = nb.Width;
                    }
                    else if (c is Label l && l.Visible)
                    {
                        nrEtichete++;
                        spatiuEtichete += l.Height;
                    }
                }

                if (nrRanduri == 0) return;

                const int gap    = Design.Sp2;   // 8px între punctele aceleiași secțiuni
                const int gapSec = Design.Sp3;   // 12px suplimentar înaintea unei secțiuni noi
                const int hMin   = 30;           // sub asta rândurile devin greu de citit

                int disponibil = navScroll.ClientSize.Height - padSus - padJos;

                int nrElem = nrRanduri + nrEtichete;
                int spatiuFix = spatiuEtichete
                              + gap    * Math.Max(0, nrElem - 1)
                              + gapSec * Math.Max(0, nrEtichete - 1);

                int spatiuRanduri = disponibil - spatiuFix;
                if (spatiuRanduri > 0)
                {
                    int hPotrivita = spatiuRanduri / nrRanduri;
                    if (hPotrivita < hRand) hRand = Math.Max(hMin, hPotrivita);
                }

                // Uniformizăm TOATE rândurile la înălțimea finală (și „Sala Live",
                // creat în cod cu 210×40, nescalează) — DUPĂ calculul înălțimii,
                // altfel rândurile s-ar suprapune.
                foreach (var c in ordine)
                    if (c is NavButton nb2 && nb2.Visible)
                        nb2.Size = new Size(wRand, hRand);

                int y = padSus;
                bool primul = true;
                foreach (var c in ordine)
                {
                    if (c == null || !c.Visible) continue;

                    if (c is Label)
                    {
                        if (!primul) y += gapSec;
                        c.Location = new Point(c.Left, y);
                        y += c.Height + gap;
                    }
                    else
                    {
                        c.Location = new Point(c.Left, y);
                        y += hRand + gap;
                    }
                    primul = false;
                }

                // Plasă de siguranță: dacă tot depășește zona vizibilă, activăm
                // derularea verticală (înainte lipsea complet).
                int necesar = y - gap + padJos;
                navScroll.AutoScrollMinSize = new Size(0, Math.Max(0, necesar));
                navScroll.PerformLayout();
            }
            finally
            {
                _reasezMeniuInCurs = false;
            }
        }

        /// <summary>Ascunde o etichetă de secțiune dacă nu are niciun punct vizibil.</summary>
        private static void AscundeEtichetaFaraPuncte(Label eticheta, params Control[] puncte)
        {
            if (eticheta == null) return;
            foreach (var p in puncte)
                if (p != null && p.Visible) return;
            eticheta.Visible = false;
        }

        private void NavStatic_Click(object sender, EventArgs e)
        {
            // Dacă eram în Sala Live, readucem conținutul anterior.
            if (hartaSala != null && hartaSala.Visible)
                RevenireDinSalaLive();

            if (sender == navEchipamente)      DeschideFormCRUD("Echipamente", "Gestionare Echipamente");
            else if (sender == navSali)     DeschideFormCRUD("Sali", "Gestionare Sali");
            else if (sender == navMembri)     DeschideFormCRUD("Membri", "Gestionare Membri");
            else if (sender == navSesiuni)  DeschideFormCRUD("Sesiuni", "Gestionare Sesiuni");
            else if (sender == navRezervari)   DeschideFormCRUD("Rezervari", "Gestionare Rezervari");
            else if (sender == navFavorite)    DeschideFormFavorite();
            else if (sender == navCalculator)  DeschideFormCalculatorCost();
            else if (sender == navOperatori)      DeschideFormCRUD("Operatori", "Gestionare Operatori");
            else if (sender == navRapoarte)    DeschideFormRapoarte();
            else if (sender == navUtilizatori) DeschideFormUtilizatori();
            else if (sender == navProfil)      DeschideFormProfil();
            else if (sender == navParola)      DeschideFormSchimbareParola();

            SetActiveNav(sender as NavButton);
        }

        private void SetActiveNav(NavButton btn)
        {
            if (activeNav != null) activeNav.Active = false;
            activeNav = btn;
            if (activeNav != null) activeNav.Active = true;
        }

        // ════════════════════════════════════════════════════════════
        //  AUTO-LOGOUT la inactivitate (neschimbat funcțional)
        // ════════════════════════════════════════════════════════════
        private System.Windows.Forms.Timer _timerInactivitate;
        // FIX_04: păstrăm referința la filtru ca să-l putem scoate la închidere (fără leak după login/logout repetat).
        private FiltruActivitate _filtru;
        private DateTime _ultimaActivitate = DateTime.Now;
        // FIX_06.3: afișăm dialogul de avertizare o singură dată per ciclu de expirare.
        private bool _avertismentExpirareAratat;

        private void CreeazaTimerInactivitate()
        {
            int timeoutMin = DatabaseConfig.SessionTimeoutMinutes;
            if (timeoutMin <= 0) return;

            _filtru = new FiltruActivitate(this);
            Application.AddMessageFilter(_filtru);

            _timerInactivitate = new System.Windows.Forms.Timer { Interval = 30000 };
            _timerInactivitate.Tick += (s, e) =>
            {
                double ramas = timeoutMin - (DateTime.Now - _ultimaActivitate).TotalMinutes;

                if (ramas >= 1.0)   // din nou activ / încă mult timp — resetăm avertizarea
                {
                    _avertismentExpirareAratat = false;
                    return;
                }

                // FIX_06.3: la aproximativ o minut înainte de expirare, avertizăm
                // și oferim posibilitatea de a prelungi sesiunea.
                if (ramas > 0 && !_avertismentExpirareAratat)
                {
                    _avertismentExpirareAratat = true;
                    if (ArataAvertizareExpirare())
                    {
                        _ultimaActivitate = DateTime.Now;      // prelungire sesiune
                        _avertismentExpirareAratat = false;
                        return;
                    }
                }

                if ((DateTime.Now - _ultimaActivitate).TotalMinutes >= timeoutMin)
                {
                    _timerInactivitate.Stop();
                    AppLogger.Security(
                        $"Sesiune expirata (inactivitate > {timeoutMin} min): {SessionInfo.Username}");

                    MessageBox.Show(
                        $"Sesiunea a expirat dupa {timeoutMin} minute de inactivitate.\n" +
                        "Te rugam sa te autentifici din nou.",
                        "Sesiune expirata", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    SessionInfo.Reset();
                    this.DialogResult = DialogResult.Retry;
                    this.Close();
                }
            };
            _timerInactivitate.Start();
            // FIX_04: curățăm timer-ul și filtrul la închiderea formei — fără „Sesiune expirata" din sesiuni moarte.
            this.FormClosed += (s, e) => CurataResurseInactivitate();
        }

        /// <summary>
        /// Dialog de avertizare înainte de deconectarea automată.
        /// Întoarce true dacă utilizatorul alege să prelungească sesiunea.
        /// </summary>
        private bool ArataAvertizareExpirare()
        {
            // FIX BUG-04: dialog propriu (butoane în română, în tema aplicației).
            bool ramaneConectat = DialogHelper.Confirma(this,
                "Sesiune pe cale să expire",
                "Sesiunea va expira în mai puțin de un minut din cauza inactivității.\n\n" +
                "Dorești să rămâi conectat?",
                "Rămân conectat", "Deconectare");

            if (ramaneConectat)
                AppLogger.Info("Sesiune prelungită de utilizator la avertizarea de expirare.",
                    "FormMainCuLogin");
            return ramaneConectat;
        }

        // FIX_04: oprește timer-ul și scoate message-filter-ul (anti-leak după cicluri login/logout).
        private void CurataResurseInactivitate()
        {
            try { _timerInactivitate?.Stop(); _timerInactivitate?.Dispose(); _timerInactivitate = null; }
            catch (Exception ex) { AppLogger.Warn("Cleanup timer: " + ex.Message, "FormMainCuLogin"); }
            if (_filtru != null)
            {
                try { Application.RemoveMessageFilter(_filtru); }
                catch (Exception ex) { AppLogger.Warn("Cleanup filter: " + ex.Message, "FormMainCuLogin"); }
                _filtru = null;
            }
        }

        private class FiltruActivitate : IMessageFilter
        {
            private readonly FormMainCuLogin _form;
            public FiltruActivitate(FormMainCuLogin form) { _form = form; }

            public bool PreFilterMessage(ref Message m)
            {
                const int WM_MOUSEMOVE = 0x0200;
                const int WM_KEYDOWN   = 0x0100;
                if (m.Msg == WM_MOUSEMOVE || m.Msg == WM_KEYDOWN)
                    _form._ultimaActivitate = DateTime.Now;
                return false;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  ALERTE → CRUD
        // ════════════════════════════════════════════════════════════
        private void DeschideAlerta(int tipAlerta, bool arataTot)
        {
            string tabela, titlu, filtru;
            switch (tipAlerta)
            {
                case 0:
                    tabela = "Echipamente";
                    titlu  = "Echipamente libere de peste 90 de zile";
                    filtru = "Liber";
                    break;
                case 1:
                    tabela = "Rezervari";
                    titlu  = "Rezervari programate in urmatoarele 7 zile";
                    filtru = "Programata";
                    break;
                default:
                    tabela = "Sesiuni";
                    titlu  = "Sesiuni în așteptare de peste 30 de zile";
                    filtru = "În așteptare";
                    break;
            }
            if (arataTot) filtru = null;
            DeschideFormCRUD(tabela, titlu, filtru);
        }

        private void DeschideFormCRUD(string tabela, string titlu, string filtru)
        {
            // FIX: si calea prin alerte (dublu-clic din dashboard) verifica
            // dreptul de vizualizare — altfel un user cu PANOU_DASHBOARD, dar
            // fara MENIU_<tabela>, deschidea lista modulului interzis.
            if (!VerificaAccesPentruTabela(tabela)) return;

            try
            {
                var frm = new FormCRUD(tabela, titlu, connectionString, rolUtilizator, filtru);
                frm.ShowDialog();
                panouDashboard?.Reincarca();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormMainCuLogin.DeschideFormCRUD");
                MessageBox.Show("Nu s-a putut deschide fereastra: " + ex.Message,
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string CodPentruTabela(string tabela)
            => PuncteAcces.CodMeniu(tabela);

        private bool VerificaAccesPentruTabela(string tabela)
        {
            string cod = CodPentruTabela(tabela);
            // FIX_06: fail-closed — tabelă fără mapare de permisiune nu se deschide implicit.
            if (cod == null) return false;
            if (PermissionService.VerificaAre(cod, EsteAdmin)) return true;

            AppLogger.Security(
                $"Acces blocat: '{SessionInfo.Username}' a incercat functia '{cod}'");
            PermissionService.AfiseazaMesajAcces();
            return false;
        }

        private void DeschideFormCRUD(string tabela, string titlu)
        {
            if (!VerificaAccesPentruTabela(tabela)) return;

            try
            {
                var frm = new FormCRUD(tabela, titlu, connectionString, rolUtilizator);
                frm.ShowDialog();
                panouDashboard?.Reincarca();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormMainCuLogin.DeschideFormCRUD");
                MessageBox.Show("Nu s-a putut deschide fereastra: " + ex.Message,
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeschideFormRapoarte()
        {
            if (!PermissionService.VerificaAre(PuncteAcces.MeniuRapoarte, EsteAdmin))
            {
                AppLogger.Security(
                    $"Acces blocat: '{SessionInfo.Username}' a incercat Rapoarte");
                PermissionService.AfiseazaMesajAcces();
                return;
            }

            try
            {
                var frm = new FormRapoarte(connectionString);
                frm.ShowDialog();
                panouDashboard?.Reincarca();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormMainCuLogin.DeschideFormRapoarte");
                MessageBox.Show("Eroare: " + ex.Message, "Eroare",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeschideFormUtilizatori()
        {
            if (!PermissionService.VerificaAre(PuncteAcces.GestionareUtilizatori, EsteAdmin))
            {
                AppLogger.Security(
                    $"Acces blocat: '{SessionInfo.Username}' a incercat Gestionare Utilizatori");
                PermissionService.AfiseazaMesajAcces();
                return;
            }

            try
            {
                var frm = new FormUtilizatori();
                frm.ShowDialog();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormMainCuLogin.DeschideFormUtilizatori");
                MessageBox.Show("Eroare: " + ex.Message, "Eroare",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  PROFIL + FAVORITE (functionalitati utilizator)
        // ════════════════════════════════════════════════════════════
        private void DeschideFormProfil()
        {
            try
            {
                var frm = new FormProfil();
                frm.ShowDialog();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormMainCuLogin.DeschideFormProfil");
                MessageBox.Show("Eroare: " + ex.Message, "Eroare",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeschideFormFavorite()
        {
            if (!PermissionService.VerificaAre(PuncteAcces.Favorite, EsteAdmin))
            {
                AppLogger.Security(
                    $"Acces blocat: '{SessionInfo.Username}' a incercat Favorite");
                PermissionService.AfiseazaMesajAcces();
                return;
            }

            try
            {
                var frm = new FormFavorite();
                frm.ShowDialog();
                panouDashboard?.Reincarca();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormMainCuLogin.DeschideFormFavorite");
                MessageBox.Show("Eroare: " + ex.Message, "Eroare",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeschideFormCalculatorCost()
        {
            if (!PermissionService.VerificaAre(PuncteAcces.CalculatorCost, EsteAdmin))
            {
                AppLogger.Security(
                    $"Acces blocat: '{SessionInfo.Username}' a incercat Calculator Cost");
                PermissionService.AfiseazaMesajAcces();
                return;
            }

            try
            {
                var frm = new FormCalculatorCost();
                frm.ShowDialog();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormMainCuLogin.DeschideFormCalculatorCost");
                MessageBox.Show("Eroare: " + ex.Message, "Eroare",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeschideFormSchimbareParola()
        {
            try
            {
                // FIX_03: transmitem login-ul real (SessionInfo.Username), nu numele complet — altfel AuthService nu găsește utilizatorul.
                var frm = new FormSchimbareParola(SessionInfo.Username);
                frm.ShowDialog();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormMainCuLogin.DeschideFormSchimbareParola");
                MessageBox.Show("Eroare: " + ex.Message, "Eroare",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
