using System;
using System.Data;
using System.Data.SqlClient;
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

        // ── Butoane de control ferestre (chrome borderless) ───────
        private WindowButton btnMin, btnMax, btnClose;
        private FormWindowState _stareInainteDeMaximizare = FormWindowState.Normal;

        private bool EsteAdmin => rolUtilizator == "Administrator";

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
            ConfigurareChromeFereastra();
            IncarcaDateUtilizator();
            AplicaPictograme();
            ConstruiesteContinut();
            CreeazaTimerInactivitate();
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

            // Colțuri rotunjite pentru elementele decorative
            MaterialTheme.ApplyRounded(pnlLogo, 6);
            MaterialTheme.ApplyRounded(searchBox, 8);
            MaterialTheme.ApplyRounded(avatar, 19);

            // Accesibilitate: tooltip + nume accesibile pentru elementele pur iconice
            MaterialUIHelper.AtasareTooltip(lblSearchIcon, "Căutare",
                "Caută în echipamente, săli, membri, sesiuni, rezervări și operatori. Apasă Enter.");
            MaterialUIHelper.AtasareTooltip(avatar, "Utilizator curent",
                string.IsNullOrEmpty(numeUtilizator) ? "Utilizator conectat." : "Utilizator conectat: " + numeUtilizator + ".");
            if (txtSearch != null)
                txtSearch.AccessibleName = "Căutare globală";

            AplicaPermisiuniSidebar();
        }

        private void TopBar_Paint(object sender, PaintEventArgs e)
        {
            using (var pen = new Pen(MaterialTheme.Divider, 1f))
                e.Graphics.DrawLine(pen, 0, topBar.Height - 1, topBar.Width, topBar.Height - 1);
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
                int by = (topBar.Height - btnClose.Height) / 2;

                btnClose.Location = new Point(topBar.Width - rightInset - btnW,     by);
                btnMax.Location   = new Point(topBar.Width - rightInset - btnW * 2, by);
                btnMin.Location   = new Point(topBar.Width - rightInset - btnW * 3, by);

                // userPanel se oprește înaintea butoanelor de control.
                userPanel.Location = new Point(topBar.Width - totalW - userPanel.Width - 24, 8);
                // searchBox se oprește înaintea userPanel (fără suprapunere).
                searchBox.Width = Math.Max(200, userPanel.Location.X - searchBox.Left - 12);
            }
            else
            {
                userPanel.Location = new Point(topBar.Width - userPanel.Width - 20, 8);
                searchBox.Width = Math.Max(200, topBar.Width - 640);
            }
        }

        private void SearchBox_Paint(object sender, PaintEventArgs e)
        {
            using (var pen = new Pen(MaterialTheme.Divider, 1f))
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddArc(0, 0, 12, 12, 180, 90);
                path.AddArc(searchBox.Width - 12, 0, 12, 12, 270, 90);
                path.AddArc(searchBox.Width - 12, searchBox.Height - 12, 12, 12, 0, 90);
                path.AddArc(0, searchBox.Height - 12, 12, 12, 90, 90);
                path.CloseFigure();
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(pen, path);
            }
        }

        private void SidebarPanel_Paint(object sender, PaintEventArgs e)
        {
            using (var pen = new Pen(Theme.Border, 1f))
                e.Graphics.DrawLine(pen, sidebarPanel.Width - 1, 0,
                                         sidebarPanel.Width - 1, sidebarPanel.Height);
        }

        private void BottomBar_Paint(object sender, PaintEventArgs e)
        {
            using (var pen = new Pen(MaterialTheme.Divider, 1f))
                e.Graphics.DrawLine(pen, 0, 0, bottomBar.Width, 0);
        }

        // ════════════════════════════════════════════════════════════
        //  CĂUTARE GLOBALĂ (bara de sus) — Enter deschide prima tabelă
        //  la care utilizatorul are drept și care conține rezultate.
        // ════════════════════════════════════════════════════════════

        private const string PlaceholderCautare = "Caută echipamente, membri, sesiuni...";

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
                txtSearch.ForeColor = Theme.Text;
            }
        }

        private void TxtSearch_LostFocus(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                txtSearch.Text = PlaceholderCautare;
                txtSearch.ForeColor = Theme.TextMuted;
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
        /// conțin termenul căutat. Numele coloanelor provin din cod (nu de la
        /// utilizator); valoarea este trimisă ca <see cref="SqlParameter"/>
        /// (fără concatenare în SQL). Orice eroare de rețea ⇒ «nu există»
        /// (mesajul de rezultate-goale va apărea, nu un crash).
        /// </summary>
        private bool ExistaRezultate(string tabela, string text)
        {
            string[] coloane;
            switch (tabela)
            {
                case "Echipamente": coloane = new[] { "tip_echipament", "denumire", "oras", "status", "nume_sala", "operator_sef", "descriere" }; break;
                case "Sali":         coloane = new[] { "nume_sala", "oras", "adresa", "operator_sef" }; break;
                case "Operatori":    coloane = new[] { "nume", "prenume", "rol_club", "telefon", "email", "nume_sala" }; break;
                case "Membri":       coloane = new[] { "nume", "prenume", "telefon", "email", "tip_membru", "oras_interes" }; break;
                case "Sesiuni":      coloane = new[] { "echipament", "membru", "operator", "tip_sesiune", "status", "observatii" }; break;
                case "Rezervari":    coloane = new[] { "echipament", "membru", "operator", "status", "observatii" }; break;
                default:             return false;
            }

            try
            {
                var cond = new string[coloane.Length];
                for (int i = 0; i < coloane.Length; i++)
                    cond[i] = $"[{coloane[i]}] LIKE @q";

                using (var conn = new SqlConnection(connectionString))
                using (var cmd = new SqlCommand(
                    $"SELECT TOP 1 1 FROM [{tabela}] WHERE {string.Join(" OR ", cond)}", conn))
                {
                    cmd.Parameters.Add("@q", SqlDbType.NVarChar, 200).Value = "%" + text + "%";
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
            if (MessageBox.Show("Esti sigur ca vrei sa te deconectezi?",
                    "Deconectare", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                == DialogResult.Yes)
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
                // FIX_19: dashboard pe Dock.Fill (fără înălțime fixă 760) — conținutul real (~892px: KPI+grafice+alerte+status)
                // depășea 760 și rândul 3 de alerte + linia de status erau tăiate; cu Fill + AutoScroll (FIX_12) tot conținutul e accesibil prin scroll.
                panouDashboard = new PanouDashboard(connectionString)
                {
                    Dock = DockStyle.Fill,
                    AutoSize = false
                };
                panouDashboard.AlertaDoubleClick += DeschideAlerta;
                contentPanel.Controls.Add(panouDashboard);
                // Asigură Dock.Fill este primul, Bottom/Top nu acoperă conținutul
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
                Text      = "Salut Membru",
                Font      = MaterialTheme.FontTitle,
                ForeColor = MaterialTheme.Primary,
                BackColor = Color.Transparent,
                Dock      = DockStyle.Top,
                Height    = 44,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var lblSub = new Label
            {
                Text      = "Bine ai venit! Echipamentele libere le găsești în meniul Echipamente.",
                Font      = MaterialTheme.FontBody,
                ForeColor = MaterialTheme.TextSecondary,
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
                MaterialTheme.Primary, MaterialTheme.PrimaryLight, PuncteAcces.MeniuEchipamente,
                () => DeschideFormCRUD("Echipamente", "Gestionare Echipamente"));
            AddMenuCard(flow, "\uE7F8", "Sali", "Gestionare sali",
                MaterialTheme.Info, MaterialTheme.Accent, PuncteAcces.MeniuSali,
                () => DeschideFormCRUD("Sali", "Gestionare Sali"));
            AddMenuCard(flow, "\uE716", "Membri", "Gestionare membri",
                MaterialTheme.Success, MaterialTheme.Accent, PuncteAcces.MeniuMembri,
                () => DeschideFormCRUD("Membri", "Gestionare Membri"));
            AddMenuCard(flow, "\uE8F5", "Sesiuni", "Gestionare sesiuni",
                MaterialTheme.Warning, MaterialTheme.Accent, PuncteAcces.MeniuSesiuni,
                () => DeschideFormCRUD("Sesiuni", "Gestionare Sesiuni"));
            AddMenuCard(flow, "\uE787", "Rezervări", "Gestionare rezervări",
                MaterialTheme.Accent, MaterialTheme.PrimaryLight, PuncteAcces.MeniuRezervari,
                () => DeschideFormCRUD("Rezervari", "Gestionare Rezervari"));
            AddMenuCard(flow, "\uE77C", "Operatori", "Gestionare operatori",
                MaterialTheme.PrimaryDark, MaterialTheme.PrimaryLight, PuncteAcces.MeniuOperatori,
                () => DeschideFormCRUD("Operatori", "Gestionare Operatori"));
            AddMenuCard(flow, "\uE9F5", "Rapoarte", "Rapoarte și statistici",
                MaterialTheme.Primary, MaterialTheme.PrimaryLight, PuncteAcces.MeniuRapoarte,
                () => DeschideFormRapoarte());
            AddMenuCard(flow, "\uE734", "Favorite", "Echipamentele tale favorite",
                MaterialTheme.Accent, MaterialTheme.PrimaryLight, PuncteAcces.Favorite,
                () => DeschideFormFavorite());
            AddMenuCard(flow, "\uE8EF", "Calculator Cost", "Cost orar, durată, pachet și reduceri",
                MaterialTheme.Info, MaterialTheme.Accent, PuncteAcces.CalculatorCost,
                () => DeschideFormCalculatorCost());
        }

        private void AddMenuCard(Control parent, string icon, string titlu, string subtitlu,
            Color accent, Color accentGlow, string codPermisiune, Action actiune)
        {
            if (!PermissionService.AreUI(codPermisiune)) return;
            var card = new MenuCard
            {
                Icon = icon,
                CardTitle = titlu,
                Subtitle = subtitlu,
                Accent = accent,
                AccentGlow = accentGlow,
                Size = new Size(240, 120),
                Margin = new Padding(8)
            };
            card.Click += (s, e) => actiune();
            parent.Controls.Add(card);
        }

        // ════════════════════════════════════════════════════════════
        //  SIDEBAR — butoanele sunt statice (în Designer); aici doar
        //  aplicăm vizibilitatea după permisiunile utilizatorului.
        // ════════════════════════════════════════════════════════════
        private void AplicaPermisiuniSidebar()
        {
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
        }

        private void NavStatic_Click(object sender, EventArgs e)
        {
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
            var rezultat = MessageBox.Show(
                "Sesiunea va expira în mai puțin de un minut din cauza inactivității.\n\n" +
                "Dorești să rămâi conectat?",
                "Sesiune pe cale să expire",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (rezultat == DialogResult.Yes)
                AppLogger.Info("Sesiune prelungită de utilizator la avertizarea de expirare.",
                    "FormMainCuLogin");
            return rezultat == DialogResult.Yes;
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
