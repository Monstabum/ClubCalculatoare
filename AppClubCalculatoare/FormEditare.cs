using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppClubCalculatoare.UI;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Formular generic de adaugare/modificare/rezervare a unei inregistrari.
    ///
    /// Imbunatatiri adaugate:
    ///   Q2  — Validare date: data sesiunei nu poate fi in viitor
    ///          (DateTimePicker blocat vizual la MaxDate = astazi), iar
    ///          rezervarile programate nu pot fi in trecut.
    ///   Q5  — Audit trail: salveaza INSERT/UPDATE in tabelul AuditLog.
    ///   Q10 — Parametru readOnly: toate controalele dezactivate, butonul
    ///          Salveaza ascuns, titlul se schimba in "Detalii".
    ///
    /// Tabele gestionate: Echipamente, Sali, Membri, Operatori,
    ///                    Sesiuni, Rezervari.
    /// </summary>
    public partial class FormEditare : AsyncFormBase
    {
        private readonly string tabela;
        private readonly int    idEditare;
        private readonly bool   _readOnly;
        private readonly Dictionary<string, Control> controale =
            new Dictionary<string, Control>();
        // FIX_09: checkbox-uri „N/A" pentru câmpurile int nullable
        // (ram_gb, numar_locuri, an_achizitie) → DBNull.Value.
        private readonly Dictionary<string, CheckBox> chkNull =
            new Dictionary<string, CheckBox>();

        // ── Liste fixe pentru ComboBox-urile statice ────────────────
        private static readonly string[] TipuriEchipament =
            { "PC Gaming", "Consola", "VR", "Server", "Simulator" };
        private static readonly string[] StatusuriEchipament =
            { "Liber", "Ocupat", "Rezervat", "În service" };
        private static readonly string[] TipuriMembru =
            { "Standard", "Premium", "Student", "Companie" };
        private static readonly string[] TipuriSesiune =
            { "Joc", "Internet", "Curs", "Eveniment", "Muncă" };
        private static readonly string[] StatusuriSesiune =
            { "Finalizata", "În așteptare", "Anulata" };
        private static readonly string[] StatusuriRezervare =
            { "Programata", "Efectuata", "Anulata" };

        // Constructor original (compatibil cu codul existent)
        public FormEditare(string tabela, string connString, int id)
            : this(tabela, connString, id, false) { }

        // Constructor extins cu parametru readOnly (Q10)
        public FormEditare(string tabela, string connString, int id, bool readOnly)
        {
            this.tabela           = tabela;
            this.idEditare        = id;
            this._readOnly        = readOnly;

            if (readOnly)
                this.Text = $"Detalii {tabela}";
            else
                this.Text = id == -1 ? $"Adauga {tabela}" : $"Modifica {tabela}";

            this.Size            = new System.Drawing.Size(560, 640);
            this.MinimumSize     = new Size(520, 480);
            this.StartPosition   = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox     = false;
            this.BackColor       = MaterialTheme.Background;
            this.Font            = MaterialTheme.FontBody;

            // DPI: scalare uniformă (fereastra este DPI-aware) + titlu închis.
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode       = AutoScaleMode.Dpi;
            WindowChrome.EnableDarkTitleBar(this);

            InitializeComponent();

            // FIX SelectedIndex: ComboBox-urile legate la DataSource au
            // Items.Goale pana cand formularul este afisat (handle creat) —
            // setarea SelectedIndex/SelectedValue in constructor arunca
            // ArgumentOutOfRangeException ("Value '0' is not valid for
            // 'SelectedIndex'") si SelectValue-ul FK esueaza tacut,
            // lasand prima valoare gresita. Incarcarea se face in OnShown,
            // dupa popularea asincrona a combo-urilor.

            // Q10: dezactiveaza controalele in modul readonly
            if (_readOnly)
                AplicheazaModReadOnly();
        }

        private readonly List<Func<Task>> _incarcariCombo = new List<Func<Task>>();

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _ = RunOperationAsync(async ct =>
            {
                foreach (Func<Task> incarca in _incarcariCombo)
                    await incarca();

                if (idEditare > 0)
                    await IncarcaDateExistenteAsync(ct);
            }, "Eroare la încărcarea datelor");
        }

        private void InitializeComponent()
        {
            this.BackColor = MaterialTheme.Background;

            // ── Continut central (Dock.Fill) — adaugat PRIMUL ─────
            var content = MaterialUIHelper.ContentPanel();
            this.Controls.Add(content);

            var card = new MaterialCard
            {
                Dock = DockStyle.Fill
            };
            content.Controls.Add(card);

            var panelForm = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(8),
                AutoScroll = true
            };
            card.Controls.Add(panelForm);

            // ── Bara inferioara (Dock.Bottom): Anulare / Salveaza ──
            FlowLayoutPanel leftBottom, rightBottom;
            var bottomBar = MaterialUIHelper.SplitBottomBar(out leftBottom, out rightBottom);
            this.Controls.Add(bottomBar);

            var btnSalveaza = MaterialUIHelper.Button("💾  Salvează", MaterialButtonStyle.Success, 150);
            btnSalveaza.Name = "btnSalveaza";
            btnSalveaza.Visible = !_readOnly;
            btnSalveaza.Click += BtnSalveaza_Click;
            rightBottom.Controls.Add(btnSalveaza);

            var btnAnuleaza = MaterialUIHelper.Button(
                _readOnly ? "✕  Închide" : "✕  Anulează", MaterialButtonStyle.Outline, 150);
            btnAnuleaza.Click += (s, e) => this.DialogResult = DialogResult.Cancel;
            rightBottom.Controls.Add(btnAnuleaza);
            this.AcceptButton = _readOnly ? (IButtonControl)btnAnuleaza : btnSalveaza;
            this.CancelButton = btnAnuleaza;

            var lblHintBottom = new Label
            {
                Text = _readOnly ? "Mod vizualizare — câmpurile nu pot fi modificate." : "* Câmp obligatoriu",
                Font = MaterialTheme.FontSmall,
                ForeColor = MaterialTheme.TextSecondary,
                AutoSize = true,
                Margin = new Padding(6, 10, 0, 0)
            };
            leftBottom.Controls.Add(lblHintBottom);

            // ── Header colorat (Dock.Top) — adaugat ULTIMUL ────────
            string operatie = _readOnly ? "Detalii" : (idEditare == -1 ? "Adaugă" : "Modifică");
            var header = MaterialUIHelper.Header(
                operatie + " — " + tabela, "Completează câmpurile și apasă Salvează",
                null, _readOnly ? MaterialTheme.TextSecondary : MaterialTheme.Primary);
            this.Controls.Add(header);

            int y  = 12;
            int lw = 160;
            int cw = 280;

            switch (tabela)
            {
                // ── Echipamente ─────────────────────────────────────
                case "Echipamente":
                    AdaugaComboOptiuni(panelForm, "Tip echipament: *", "tip_echipament",
                        y, lw, cw, TipuriEchipament);                                  y += 40;
                    AdaugaCamp(panelForm, "Denumire: *",    "denumire",       y, lw, cw); y += 40;
                    AdaugaCamp(panelForm, "Oras: *",        "oras",           y, lw, cw); y += 40;
                    AdaugaCampDecimal(panelForm, "Putere (W): *", "putere_watt", y, lw, cw); y += 40;
                    AdaugaCampNumericNullable(panelForm, "RAM (GB):", "ram_gb",
                        y, lw, cw, 0, 50, 2);                                        y += 40;
                    AdaugaCampNumericNullable(panelForm, "Număr locuri:", "numar_locuri",
                        y, lw, cw, 0, 100, 1);                                      y += 40;
                    AdaugaCampNumericNullable(panelForm, "An achizitie:", "an_achizitie",
                        y, lw, cw, 1800, DateTime.Today.Year, 2000);                 y += 40;
                    AdaugaCampDecimal(panelForm, "Tarif (EUR/oră): *", "tarif_ora", y, lw, cw); y += 40;
                    AdaugaComboOptiuni(panelForm, "Status: *", "status",
                        y, lw, cw, StatusuriEchipament);                               y += 40;
                    AdaugaComboSali(panelForm, "Sala: *", "sala_id",   y, lw, cw); y += 40;
                    AdaugaCamp(panelForm, "Descriere:", "descriere", y, lw, cw, true);  y += 72;
                    break;

                // ── Sali ─────────────────────────────────────────
                case "Sali":
                    AdaugaCamp(panelForm, "Nume sala: *", "nume_sala",   y, lw, cw); y += 40;
                    AdaugaCamp(panelForm, "Oras: *",       "oras",         y, lw, cw); y += 40;
                    AdaugaCamp(panelForm, "Adresa:",       "adresa",       y, lw, cw); y += 40;
                    AdaugaComboOperatori(panelForm, "Operator sef: *", "operator_sef_id", y, lw, cw); y += 40;
                    AdaugaCampData(panelForm, "Data deschidere:", "data_deschidere", y, lw, cw); y += 40;
                    break;

                // ── Membri ─────────────────────────────────────────
                case "Membri":
                    AdaugaCamp(panelForm, "Nume: *",       "nume",         y, lw, cw); y += 40;
                    AdaugaCamp(panelForm, "Prenume: *",    "prenume",      y, lw, cw); y += 40;
                    AdaugaCamp(panelForm, "Telefon: *",    "telefon",      y, lw, cw); y += 40;
                    AdaugaCamp(panelForm, "Email:",        "email",        y, lw, cw); y += 40;
                    AdaugaComboOptiuni(panelForm, "Tip membru: *", "tip_membru",
                        y, lw, cw, TipuriMembru);                                       y += 40;
                    AdaugaCampDecimal(panelForm, "Buget maxim:", "buget_max", y, lw, cw); y += 40;
                    AdaugaCamp(panelForm, "Oras interes:", "oras_interes", y, lw, cw); y += 40;
                    break;

                // ── Operatori ──────────────────────────────────────────
                case "Operatori":
                    AdaugaCamp(panelForm, "Nume: *",         "nume",         y, lw, cw); y += 40;
                    AdaugaCamp(panelForm, "Prenume: *",      "prenume",      y, lw, cw); y += 40;
                    AdaugaCamp(panelForm, "Rol club: *",     "rol_club", y, lw, cw); y += 40;
                    AdaugaCamp(panelForm, "Telefon:",        "telefon",      y, lw, cw); y += 40;
                    AdaugaCamp(panelForm, "Email:",          "email",        y, lw, cw); y += 40;
                    AdaugaComboSali(panelForm, "Sala:",  "sala_id",     y, lw, cw); y += 40;
                    AdaugaCampData(panelForm, "Data angajare:", "data_angajare", y, lw, cw); y += 40;
                    break;

                // ── Sesiuni ──────────────────────────────────────
                case "Sesiuni":
                    AdaugaComboEchipamente(panelForm, "Echipament: *", "id_echipament", y, lw, cw); y += 40;
                    AdaugaComboMembri(panelForm, "Membru: *",          "id_membru",      y, lw, cw); y += 40;
                    AdaugaComboOperatori(panelForm, "Operator: *",            "id_operator",       y, lw, cw); y += 40;
                    AdaugaComboOptiuni(panelForm, "Tip sesiune: *", "tip_sesiune",
                        y, lw, cw, TipuriSesiune);                                              y += 40;
                    // Q2: data sesiunei nu poate fi in viitor
                    AdaugaCampDataSesiune(panelForm, "Data sesiune: *",
                        "data_sesiune", y, lw, cw);                                             y += 40;
                    AdaugaCampNumeric(panelForm, "Durată (minute): *", "durata_minute",
                        y, lw, cw, 5, 2880, 60); y += 40;
                    AdaugaCampDecimal(panelForm, "Cost total: *", "cost_total", y, lw, cw); y += 40;
                    AdaugaCampDecimal(panelForm, "Incasare: *",   "incasare",   y, lw, cw); y += 40;
                    AdaugaComboOptiuni(panelForm, "Status: *", "status",
                        y, lw, cw, StatusuriSesiune);                                           y += 40;
                    AdaugaCamp(panelForm, "Observatii:", "observatii", y, lw, cw, true); y += 72;
                    break;

                // ── Rezervari ───────────────────────────────────────
                case "Rezervari":
                    AdaugaComboEchipamente(panelForm, "Echipament: *", "id_echipament", y, lw, cw); y += 40;
                    AdaugaComboMembri(panelForm, "Membru: *",          "id_membru",      y, lw, cw); y += 40;
                    AdaugaComboOperatori(panelForm, "Operator: *",            "id_operator",       y, lw, cw); y += 40;
                    AdaugaCampDataOra(panelForm, "Data rezervare: *",
                        "data_rezervare", y, lw, cw);                                              y += 40;
                    AdaugaComboOptiuni(panelForm, "Status: *", "status",
                        y, lw, cw, StatusuriRezervare);                                            y += 40;
                    AdaugaCamp(panelForm, "Observatii:", "observatii", y, lw, cw, true); y += 72;
                    break;
            }

            // Dimensiunea ferestrei se adapteaza la numarul de campuri
            // (Echipamente si Sesiuni au cele mai multe).
            ConfigureazaAutoNaPentruServer(); // FIX_09: Server → N/A automat
            int inaltimeNecesara = y + MaterialTheme.HeaderH + MaterialTheme.BottomBarH + 120;
            this.Size = new System.Drawing.Size(560,
                Math.Max(480, Math.Min(inaltimeNecesara, 840)));

            // ── MODUL_FOTO: tab Fotografii pentru Echipamente ─────
            AtaseazaTabFotografii(card, panelForm, y);
        }

        // ── MODUL_FOTO: galeria foto (doar tabela Echipamente) ────
        private TabControl _tabFoto;
        private PhotoGalleryControl _galerieFoto;

        /// <summary>
        /// Ataseaza tab-ul "Fotografii (N)" pentru echipamente existente;
        /// pentru obiecte noi afiseaza mesajul de salvare prealabila.
        /// </summary>
        private void AtaseazaTabFotografii(MaterialCard card, Panel panelForm, int yLiber)
        {
            if (tabela != "Echipamente") return;

            if (idEditare <= 0)
            {
                var lblFotoInfo = new Label
                {
                    Text = "Fotografiile se pot adăuga după salvarea obiectului.",
                    Font = MaterialTheme.FontSmall,
                    ForeColor = MaterialTheme.TextSecondary,
                    Location = new System.Drawing.Point(20, yLiber),
                    Size = new System.Drawing.Size(460, 22)
                };
                panelForm.Controls.Add(lblFotoInfo);
                this.Size = new System.Drawing.Size(this.Width, this.Height + 36);
                return;
            }

            var tab = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = MaterialTheme.FontBody
            };
            var pagDate = new TabPage("Date generale");
            var pagFoto = new TabPage("Fotografii");

            card.Controls.Remove(panelForm);
            pagDate.Controls.Add(panelForm);

            _galerieFoto = new PhotoGalleryControl();
            _galerieFoto.Dock = DockStyle.Fill;
            _galerieFoto.DoarCitire = _readOnly;
            pagFoto.Controls.Add(_galerieFoto);

            tab.TabPages.Add(pagDate);
            tab.TabPages.Add(pagFoto);
            card.Controls.Add(tab);
            _tabFoto = tab;

            _galerieFoto.FotoAdaugata += (s, e) => ActualizeazaTitluTabFoto();
            _galerieFoto.FotoStearsa += (s, e) => ActualizeazaTitluTabFoto();
            _galerieFoto.Incarca(idEditare);
            ActualizeazaTitluTabFoto();
            this.FormClosing += FormEditare_FormClosingFoto;
        }

        private void ActualizeazaTitluTabFoto()
        {
            if (_tabFoto == null || _tabFoto.TabPages.Count < 2 || _galerieFoto == null)
                return;
            _tabFoto.TabPages[1].Text =
                string.Format("Fotografii ({0})", _galerieFoto.Count);
        }

        private void FormEditare_FormClosingFoto(object sender, FormClosingEventArgs e)
        {
            if (_galerieFoto == null || !_galerieFoto.AreModificariNesalvate)
                return;
            // FIX BUG-04: dialog propriu cu butoane românești (înainte: Yes/No/Cancel
            // afișate în limba sistemului de operare).
            DialogResult raspuns = DialogHelper.Intreaba(this,
                "Fotografii",
                "Descrierea fotografiei a fost modificată.\nSalvezi modificarea?",
                "Salvează", "Nu salva", "Anulează");
            if (raspuns == DialogResult.Yes)
            {
                if (!_galerieFoto.SalveazaDescriereaCurenta())
                    e.Cancel = true;
            }
            else if (raspuns == DialogResult.Cancel)
                e.Cancel = true;
        }

        // Q10: dezactiveaza toate controalele in modul readonly
        private void AplicheazaModReadOnly()
        {
            foreach (var ctrl in controale.Values)
            {
                ctrl.Enabled = false;
                // FIX vizibilitate: fundalul alb (#F5F5F5) + ForeColor alb
                // (TextPrimary) faceau textul complet invizibil in modul
                // „Vizualizare detalii". Folosim suprafata mai jos (Surface)
                // si pastram textul lizibil — diferenta de fundal semnalizeaza
                // campul ca fiind needitabil.
                MaterialUIHelper.StyleInputReadOnly(ctrl);
            }
            foreach (var chk in chkNull.Values) chk.Enabled = false; // FIX_09
        }

        // ════════════════════════════════════════════════════════════
        //  VALIDARE CAMPURI
        // ════════════════════════════════════════════════════════════
        private List<string> Valideaza()
        {
            var erori = new List<string>();

            switch (tabela)
            {
                case "Echipamente":
                    ValidareCampCombo(erori, "tip_echipament", "Tipul de echipament");
                    ValidareCampText(erori, "denumire", "Denumirea", min: 3, max: 150);
                    ValidareCampText(erori, "oras",   "Orasul",  min: 2, max: 50);
                    ValidarePuterea(erori);
                    ValidareAnAchizitie(erori);
                    ValidarePret(erori, "tarif_ora", "Tariful");
                    ValidareCampCombo(erori, "status",   "Statusul");
                    ValidareCampCombo(erori, "sala_id", "Sala");
                    break;

                case "Sali":
                    ValidareCampText(erori, "nume_sala", "Numele sălii", min: 2, max: 50);
                    ValidareCampText(erori, "oras",       "Orasul",          min: 2, max: 50);
                    ValidareCampCombo(erori, "operator_sef_id", "Operatorul sef");
                    ValidareCampData(erori, "data_deschidere", "Data deschiderii",
                        1950, DateTime.Today.Year);
                    break;

                case "Membri":
                    ValidareCampText(erori, "nume",    "Numele",   min: 2, max: 50);
                    ValidareCampText(erori, "prenume", "Prenumele", min: 2, max: 50);
                    ValidareCampText(erori, "telefon", "Telefonul", min: 7, max: 15);
                    ValidareTelefon(erori);
                    ValidareEmail(erori);
                    ValidareCampCombo(erori, "tip_membru", "Tipul de membru");
                    ValidareBugetOptional(erori);
                    break;

                case "Operatori":
                    ValidareCampText(erori, "nume",         "Numele",        min: 2, max: 50);
                    ValidareCampText(erori, "prenume",      "Prenumele",     min: 2, max: 50);
                    ValidareCampText(erori, "rol_club", "Rolul în club", min: 2, max: 50);
                    ValidareTelefon(erori);
                    ValidareEmail(erori);
                    ValidareCampData(erori, "data_angajare", "Data angajarii",
                        1950, DateTime.Today.Year);
                    break;

                case "Sesiuni":
                    ValidareCampCombo(erori, "id_echipament", "Echipamentul");
                    ValidareCampCombo(erori, "id_membru",      "Membrul");
                    ValidareCampCombo(erori, "id_operator",       "Operatorul");
                    ValidareCampCombo(erori, "tip_sesiune", "Tipul sesiunei");
                    ValidareCampCombo(erori, "status",         "Statusul");
                    ValidarePret(erori, "cost_total", "Costul total");
                    ValidareIncasare(erori);
                    ValidareDurataSesiune(erori);
                    // Q2: data sesiunei nu poate fi in viitor
                    ValidareDataSesiune(erori);
                    break;

                case "Rezervari":
                    ValidareCampCombo(erori, "id_echipament", "Echipamentul");
                    ValidareCampCombo(erori, "id_membru",      "Membrul");
                    ValidareCampCombo(erori, "id_operator",       "Operatorul");
                    ValidareCampCombo(erori, "status",         "Statusul");
                    ValidareDataRezervare(erori);
                    break;
            }

            return erori;
        }

        // ── Q2: Validare data sesiunei ───────────────────────────
        /// <summary>
        /// Valideaza durata unei sesiuni: intre 5 minute si 48 de ore (2880 minute).
        /// </summary>
        private void ValidareDurataSesiune(List<string> erori)
        {
            if (!controale.ContainsKey("durata_minute")) return;

            int durata = (int)((NumericUpDown)controale["durata_minute"]).Value;
            if (durata < 5 || durata > 2880)
                erori.Add("Durata sesiunii trebuie sa fie intre 5 si 2880 de minute.");
        }

        /// <summary>
        /// Valideaza data sesiunei:
        ///   - Nu poate fi in viitor (DateTimePicker e blocat vizual la
        ///     MaxDate = astazi, verificarea ramane ca plasa de siguranta).
        ///   - Nu poate fi mai veche de anul 2000.
        /// </summary>
        private void ValidareDataSesiune(List<string> erori)
        {
            if (!controale.ContainsKey("data_sesiune")) return;

            DateTime data = ((DateTimePicker)controale["data_sesiune"]).Value.Date;
            DateTime azi  = DateTime.Today;

            if (data > azi)
                erori.Add("Data sesiunei nu poate fi in viitor.");

            if (data.Year < 2000)
                erori.Add($"Data sesiunei pare incorecta (anul {data.Year}).");
        }

        /// <summary>
        /// Valideaza data unei rezervari: o rezervare inca "Programata"
        /// nu poate fi stabilita in trecut.
        /// </summary>
        private void ValidareDataRezervare(List<string> erori)
        {
            if (!controale.ContainsKey("data_rezervare")) return;

            DateTime data   = ((DateTimePicker)controale["data_rezervare"]).Value;
            string   status = ValoareComboText("status");

            if (status == "Programata" && data < DateTime.Now)
                erori.Add("O rezervare cu statusul 'Programata' nu poate fi in trecut.");
        }

        /// <summary>
        /// Valideaza puterea: trebuie sa fie mai mare decat 0
        /// si mai mica decat 100 000 mp.
        /// </summary>
        private static bool TryParseDecimalInvariant(string text, out decimal value)
        {
            // Accepta atat "." cat si "," ca separator zecimal (RO "," / EN ".")
            // -> "78,50" si "78.50" ambele devin 78.50, fara a strica InvariantCulture.
            string t = (text ?? "").Trim().Replace(',', '.');
            return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        private void ValidarePuterea(List<string> erori)
        {
            if (!controale.ContainsKey("putere_watt")) return;

            string val = ((TextBox)controale["putere_watt"]).Text.Trim();
            if (!TryParseDecimalInvariant(val, out decimal puterea))
            { erori.Add("Puterea trebuie sa fie un numar zecimal valid (ex: 750)."); return; }

            if (puterea <= 0)       erori.Add("Puterea trebuie sa fie mai mare decat 0.");
            if (puterea >= 100000m) erori.Add("Puterea nu poate depasi 100000 W.");
        }

        /// <summary>Valideaza pretul: strict pozitiv.</summary>
        private void ValidarePret(List<string> erori, string camp, string label)
        {
            if (!controale.ContainsKey(camp)) return;

            string val = ((TextBox)controale[camp]).Text.Trim();
            if (!TryParseDecimalInvariant(val, out decimal pret))
            { erori.Add($"{label} trebuie sa fie un numar zecimal valid (ex: 25)."); return; }

            if (pret <= 0)            erori.Add($"{label} trebuie sa fie mai mare decat 0.");
            if (pret > 99999999m)     erori.Add($"{label} pare nerealist de mare.");
        }

        /// <summary>Valideaza anul achizitiei: intre 1800 si anul curent.</summary>
        private void ValidareAnAchizitie(List<string> erori)
        {
            if (!controale.ContainsKey("an_achizitie")) return;
            // FIX_09: „N/A" bifat → câmp NULL, validarea se sare.
            if (EsteNaBifat("an_achizitie")) return;

            int an = (int)((NumericUpDown)controale["an_achizitie"]).Value;
            if (an < 1800 || an > DateTime.Today.Year)
                erori.Add($"Anul achiziției trebuie sa fie intre 1800 si {DateTime.Today.Year}.");
        }

        /// <summary>Valideaza încasarea: nu poate fi negativ.</summary>
        private void ValidareIncasare(List<string> erori)
        {
            if (!controale.ContainsKey("incasare")) return;

            string val = ((TextBox)controale["incasare"]).Text.Trim();
            if (!TryParseDecimalInvariant(val, out decimal incasare))
            { erori.Add("Încasarea trebuie sa fie un numar zecimal valid (ex: 4260)."); return; }

            if (incasare < 0) erori.Add("Încasarea nu poate fi negativ.");
        }

        /// <summary>Bugetul maxim este optional, dar nu poate fi negativ.</summary>
        private void ValidareBugetOptional(List<string> erori)
        {
            if (!controale.ContainsKey("buget_max")) return;

            string val = ((TextBox)controale["buget_max"]).Text.Trim();
            if (string.IsNullOrEmpty(val)) return;

            if (!TryParseDecimalInvariant(val, out decimal buget))
            { erori.Add("Bugetul maxim trebuie sa fie un numar zecimal valid."); return; }

            if (buget < 0) erori.Add("Bugetul maxim nu poate fi negativ.");
        }

        private void ValidareCampText(List<string> erori, string camp, string label,
            int min = 1, int max = 255, bool obligatoriu = true)
        {
            if (!controale.ContainsKey(camp)) return;
            string val = ((TextBox)controale[camp]).Text.Trim();

            if (obligatoriu && string.IsNullOrEmpty(val))
            { erori.Add($"Câmpul „{label}” este obligatoriu."); return; }

            if (!string.IsNullOrEmpty(val))
            {
                if (val.Length < min) erori.Add($"{label} trebuie să aibă cel puțin {min} caractere.");
                if (val.Length > max) erori.Add($"{label} nu poate depăși {max} caractere.");
            }
        }

        private void ValidareCampCombo(List<string> erori, string camp, string label)
        {
            if (!controale.ContainsKey(camp)) return;
            var cmb = (ComboBox)controale[camp];
            if (cmb.SelectedValue == null || cmb.SelectedValue.ToString() == "0" ||
                cmb.SelectedIndex < 0)
                erori.Add($"Câmpul „{label}” trebuie completat.");
        }

        private void ValidareCampData(List<string> erori, string camp, string label,
            int minAn = 1900, int maxAn = 2100)
        {
            if (!controale.ContainsKey(camp)) return;
            var dtp = (DateTimePicker)controale[camp];
            if (dtp.Value.Year < minAn || dtp.Value.Year > maxAn)
                erori.Add($"{label} trebuie să fie între {minAn} și {maxAn}.");
        }

        private void ValidareTelefon(List<string> erori)
        {
            if (!controale.ContainsKey("telefon")) return;
            string val = ((TextBox)controale["telefon"]).Text.Trim();
            if (!string.IsNullOrEmpty(val) &&
                !Regex.IsMatch(val, @"^[\+0-9\s\-\(\)]{7,15}$"))
                erori.Add("Numărul de telefon are un format invalid.");
        }

        private void ValidareEmail(List<string> erori)
        {
            if (!controale.ContainsKey("email")) return;
            string val = ((TextBox)controale["email"]).Text.Trim();
            if (!string.IsNullOrEmpty(val))
            {
                if (!Regex.IsMatch(val, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                    erori.Add("Adresa de email are un format invalid.");
                if (val.Length > 100)
                    erori.Add("Adresa de email nu poate depăși 100 de caractere.");
            }
        }

        // ════════════════════════════════════════════════════════════
        //  INCARCARE DATE EXISTENTE
        // ════════════════════════════════════════════════════════════
        private async Task IncarcaDateExistenteAsync(CancellationToken ct)
        {
            string query = ObtieneQuerySelectPentruTabela();
            if (string.IsNullOrEmpty(query)) return;

            DataTable dt = await SqlDb.QueryDataTableAsync(
                query,
                p => p.Add("@id", SqlDbType.Int).Value = idEditare,
                ct);

            if (IsDisposed || ct.IsCancellationRequested) return;
            if (dt.Rows.Count == 0) return;

            DataRow row = dt.Rows[0];
            foreach (var kvp in controale)
            {
                if (!dt.Columns.Contains(kvp.Key)) continue;

                object val = row[kvp.Key];
                // FIX_09: NULL pentru câmpurile nullable cu N/A → bifează checkbox-ul.
                if (val == DBNull.Value)
                {
                    if (chkNull.ContainsKey(kvp.Key))
                        SetNaBifat(kvp.Key, true);
                    continue;
                }

                Control ctrl = kvp.Value;
                if (ctrl is TextBox txt)
                {
                    // FIX: decimal din BD trebuie formatat invariant ("78.50" nu "78,50"),
                    // altfel Validare/PARSE cu InvariantCulture vede "78,50" ca 7850 sau invalid
                    // si Valideaza() raporteaza "mai mare decat 0" / salveaza valori corupte.
                    if (val is decimal dec) txt.Text = dec.ToString(CultureInfo.InvariantCulture);
                    else if (val is double dbl) txt.Text = dbl.ToString(CultureInfo.InvariantCulture);
                    else if (val is float fl) txt.Text = fl.ToString(CultureInfo.InvariantCulture);
                    else txt.Text = val.ToString();
                }
                else if (ctrl is DateTimePicker dtp) dtp.Value = Convert.ToDateTime(val);
                else if (ctrl is NumericUpDown nud)  nud.Value = Convert.ToDecimal(val);
                else if (ctrl is ComboBox cmb)
                {
                    // ComboBox static (tipuri / statusuri): in baza de date
                    // valoarea este salvata ca text, deci cautam textul afisat.
                    if (EsteComboStatic(cmb))
                    {
                        // FIX: FindStringExact necesita handle creat — in constructor handle-ul nu exista inca
                        // si returneaza -1 fals (de aici "Valoarea 'PC Gaming' ... nu mai exista").
                        // Cautam direct in DataTable (case-insensitive, trim) si fallback pe GetItemText.
                        string target = val.ToString().Trim();
                        int idx = -1;
                        if (cmb.DataSource is DataTable dtStatic)
                        {
                            for (int i = 0; i < dtStatic.Rows.Count; i++)
                            {
                                string den = dtStatic.Rows[i]["denumire"]?.ToString().Trim();
                                if (string.Equals(den, target, StringComparison.OrdinalIgnoreCase))
                                { idx = i; break; }
                            }
                        }
                        if (idx < 0)
                            idx = cmb.FindStringExact(target);
                        if (idx < 0)
                        {
                            for (int i = 0; i < cmb.Items.Count; i++)
                            {
                                string itemText = cmb.GetItemText(cmb.Items[i]).Trim();
                                if (string.Equals(itemText, target, StringComparison.OrdinalIgnoreCase))
                                { idx = i; break; }
                            }
                        }
                        if (idx < 0)
                        {
                            AppLogger.Warn($"Valoare '{val}' lipseste din combo '{kvp.Key}' (tabela {tabela})", "FormEditare");
                            MessageBox.Show(
                                $"Valoarea '{val}' pentru câmpul '{kvp.Key}' nu mai există în listă.\n" +
                                "A fost selectată prima valoare — verifică înainte de salvare!",
                                "Valoare lipsă", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
                            else cmb.SelectedIndex = -1;
                        }
                        // Plasa de siguranta: idx vine din DataTable,
                        // dar SelectedIndex e valid doar in Items
                        // (altfel ArgumentOutOfRangeException).
                        else if (idx < cmb.Items.Count) cmb.SelectedIndex = idx;
                        else
                        {
                            AppLogger.Warn($"Idx {idx} in afara Items ({cmb.Items.Count}) pentru combo '{kvp.Key}' — lasat neselectat.", "FormEditare");
                            cmb.SelectedIndex = -1;
                        }
                    }
                    else
                    {
                        // FIX_05: verificăm că valoarea FK există efectiv în sursă — altfel SelectedValue rămâne null și salvarea ar eșua pe NOT NULL/FK.
                        cmb.SelectedValue = val;
                        if (cmb.SelectedValue == null)
                        {
                            AppLogger.Warn($"Valoare FK '{val}' lipseste din combo '{kvp.Key}' (tabela {tabela})", "FormEditare");
                            MessageBox.Show(
                                $"Valoarea FK '{val}' pentru câmpul '{kvp.Key}' nu mai există în listă.\n" +
                                "Selectează o valoare validă înainte de salvare!",
                                "Valoare lipsă", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            // FIX BUG-09: lăsăm câmpul NESELECTAT. Înainte se
                            // selecta prima înregistrare (ex. Sala #1), deci
                            // mesajul „selectează o valoare validă" era contrazis
                            // de comportament, iar o salvare neatentă re-atașa
                            // înregistrarea la o sală greșită. Acum validarea de
                            // la salvare (ValidareCampCombo) blochează salvarea
                            // până când utilizatorul alege explicit.
                            cmb.SelectedIndex = -1;
                        }
                    }
                }
            }
        }

        private string ObtieneQuerySelectPentruTabela()
        {
            switch (tabela)
            {
                case "Echipamente": return "SELECT * FROM Echipamente WHERE id_echipament = @id";
                case "Sali":     return "SELECT * FROM Sali     WHERE id_sala       = @id";
                case "Membri":     return "SELECT * FROM Membri     WHERE id_membru      = @id";
                case "Operatori":      return "SELECT * FROM Operatori      WHERE id_operator       = @id";
                case "Sesiuni":  return "SELECT * FROM Sesiuni  WHERE id_sesiune  = @id";
                case "Rezervari":   return "SELECT * FROM Rezervari   WHERE id_rezervare   = @id";
                default:            return null;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  SALVARE
        // ════════════════════════════════════════════════════════════
        private bool _salvareInCurs;

        private async void BtnSalveaza_Click(object sender, EventArgs e)
        {
            if (_readOnly || _salvareInCurs) return;

            // ── Sistem drepturi: aparare anti post-injection (nivel backend) ──
            // Chiar daca formularul a fost deschis programatic in mod scriere,
            // salvarea este blocata fara dreptul de MODIFICARE al punctului.
            if (!SessionInfo.EsteAdmin)
            {
                string cod = PuncteAcces.CodModificare(tabela);
                if (!PermissionService.VerificaAre(cod, false))
                {
                    AppLogger.Security(
                        $"Acces blocat in FormEditare: '{SessionInfo.Username}' -> salvare {tabela}");
                    PermissionService.AfiseazaMesajAcces();
                    return;
                }
            }

            var erori = Valideaza();
            if (erori.Count > 0)
            {
                var sb = new StringBuilder("Corecteaza urmatoarele erori:\n\n");
                foreach (string err in erori) sb.AppendLine("  • " + err);
                MessageBox.Show(sb.ToString(), "Date invalide",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _salvareInCurs = true;
            UseWaitCursor = true;
            try
            {
                int idPropActualizata = 0;
                string statusNouProp = null;
                string actiune = idEditare == -1 ? "INSERT" : "UPDATE";

                bool modificat = await SqlDb.InTransactionAsync(async (conn, tr, ct) =>
                {
                    // Salvarea sesiunii si actualizarea statusului echipamentului
                    // se fac in aceeasi tranzactie: fie ambele, fie niciuna.
                    using (SqlCommand cmd = idEditare == -1
                        ? CreazaComandaInsert(conn)
                        : CreazaComandaUpdate(conn))
                    {
                        cmd.Transaction = tr;
                        cmd.CommandTimeout = SqlDb.DefaultTimeoutSeconds;
                        int rowsAffected = await cmd.ExecuteNonQueryAsync(ct);
                        if (rowsAffected == 0) return false;
                    }

                    if (tabela == "Sesiuni")
                    {
                        var rezultatStatus = await ActualizeazaStatusEchipamentAsync(
                            conn, tr, ct);
                        idPropActualizata = rezultatStatus.Id;
                        statusNouProp = rezultatStatus.Status;
                    }

                    // Q5: audit in aceeasi tranzactie (nu mai rămâne operația
                    // aplicată fără urmă dacă auditul eșuează).
                    await SalveazaAuditAsync(conn, tr, tabela, actiune, idEditare, ct);
                    if (idPropActualizata > 0)
                        await SalveazaAuditAsync(conn, tr, "Echipamente", "UPDATE",
                            idPropActualizata, ct);

                    return true;
                });

                if (!modificat)
                {
                    MessageBox.Show("Nicio inregistrare nu a fost modificata.",
                        "Atentie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (idPropActualizata > 0)
                {
                    AppLogger.Info(
                        $"Status echipament id={idPropActualizata} -> '{statusNouProp}' " +
                        $"(sesiune finalizata) by '{SessionInfo.Username}'", "FormEditare");
                }
                AppLogger.Info($"{actiune} {tabela} id={idEditare} by '{SessionInfo.Username}'",
                    "FormEditare");

                MessageBox.Show(
                    idEditare == -1
                        ? "Inregistrare adaugata cu succes!"
                        : "Inregistrare modificata cu succes!",
                    "Succes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                AppLogger.Error(ex, "FormEditare.BtnSalveaza");
                MessageBox.Show("Exista deja o inregistrare cu aceste date unice.",
                    "Duplicat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                AppLogger.Error(ex, "FormEditare.BtnSalveaza");
                MessageBox.Show(
                    "Inregistrarea nu poate fi salvata din cauza unei constrangeri de cheie externa.",
                    "Eroare FK", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FormEditare.BtnSalveaza");
                MessageBox.Show("Eroare la salvare: " + ex.Message,
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _salvareInCurs = false;
                if (!IsDisposed) UseWaitCursor = false;
            }
        }

        // ── Q5: Audit trail ────────────────────────────────────────
        private async Task SalveazaAuditAsync(SqlConnection conn, SqlTransaction tr,
            string tabelaAudit, string actiune, int idRec, CancellationToken ct)
        {
            try
            {
                const string sql = @"
                    IF OBJECT_ID('AuditLog','U') IS NOT NULL
                        INSERT INTO AuditLog (tabela, id_inregistrare, actiune, username)
                        VALUES (@tabela, @id, @actiune, @user)";
                using (var cmd = new SqlCommand(sql, conn, tr)
                {
                    CommandTimeout = SqlDb.DefaultTimeoutSeconds
                })
                {
                    cmd.Parameters.Add("@tabela",  SqlDbType.NVarChar, 50).Value  = tabelaAudit;
                    cmd.Parameters.Add("@id",      SqlDbType.Int).Value           = idRec < 0 ? 0 : idRec;
                    cmd.Parameters.Add("@actiune", SqlDbType.NVarChar, 10).Value  = actiune;
                    cmd.Parameters.Add("@user",    SqlDbType.NVarChar, 50).Value  = SessionInfo.Username;
                    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Audit trail esuat: " + ex.Message, "FormEditare");
            }
        }

        // ── Status echipament în funcție de statusul sesiunii ──
        /// <summary>
        /// Corespondența status sesiune → status echipament
        /// (Finalizata → Liber, În așteptare → Ocupat; Anulata nu schimbă nimic).
        /// </summary>
        private static string StatusEchipamentPentruSesiune(string statusSesiune)
        {
            switch (statusSesiune)
            {
                case "Finalizata":    return "Liber";
                case "În așteptare": return "Ocupat";
                default:            return null;
            }
        }

        /// <summary>
        /// Daca sesiunea salvata are un status care afectează echipamentul
        /// („Finalizata" eliberează stația, „În așteptare" o marchează ocupată),
        /// actualizeaza automat statusul echipamentei, in aceeasi tranzactie.
        /// Intoarce id-ul echipamentului modificat sau 0 daca nu s-a schimbat nimic.
        /// </summary>
        private async Task<(int Id, string Status)> ActualizeazaStatusEchipamentAsync(
            SqlConnection conn, SqlTransaction tr, CancellationToken ct)
        {
            string statusNou = StatusEchipamentPentruSesiune(ValoareComboText("status"));
            if (statusNou == null) return (0, null);

            int idEchipament = Convert.ToInt32(
                ((ComboBox)controale["id_echipament"]).SelectedValue);

            using (var cmd = new SqlCommand(
                @"UPDATE Echipamente SET status = @status
                  WHERE id_echipament = @id AND status <> @status", conn, tr)
            {
                CommandTimeout = SqlDb.DefaultTimeoutSeconds
            })
            {
                cmd.Parameters.Add("@status", SqlDbType.NVarChar, 20).Value = statusNou;
                cmd.Parameters.Add("@id",     SqlDbType.Int).Value          = idEchipament;
                int rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                return (rows > 0 ? idEchipament : 0, statusNou);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  COMENZI SQL
        // ════════════════════════════════════════════════════════════
        private SqlCommand CreazaComandaInsert(SqlConnection conn)
        {
            var cmd = new SqlCommand { Connection = conn };
            switch (tabela)
            {
                case "Echipamente":
                    cmd.CommandText = @"INSERT INTO Echipamente
                        (tip_echipament, denumire, oras, putere_watt, ram_gb, numar_locuri,
                         an_achizitie, tarif_ora, status, sala_id, descriere)
                        VALUES (@tip_echipament, @denumire, @oras, @putere_watt, @ram_gb,
                                @numar_locuri, @an_achizitie, @tarif_ora, @status, @sala_id, @descriere)";
                    AdaugaParametriEchipamente(cmd); break;

                case "Sali":
                    cmd.CommandText = @"INSERT INTO Sali
                        (nume_sala, oras, adresa, operator_sef_id, data_deschidere)
                        VALUES (@nume_sala, @oras, @adresa, @operator_sef_id, @data_deschidere)";
                    AdaugaParametriSali(cmd); break;

                case "Membri":
                    cmd.CommandText = @"INSERT INTO Membri
                        (nume, prenume, telefon, email, tip_membru, buget_max, oras_interes)
                        VALUES (@nume, @prenume, @telefon, @email, @tip_membru, @buget_max, @oras_interes)";
                    AdaugaParametriMembri(cmd); break;

                case "Operatori":
                    cmd.CommandText = @"INSERT INTO Operatori
                        (nume, prenume, rol_club, telefon, email, sala_id, data_angajare)
                        VALUES (@nume, @prenume, @rol_club, @telefon, @email, @sala_id, @data_angajare)";
                    AdaugaParametriOperatori(cmd); break;

                case "Sesiuni":
                    cmd.CommandText = @"INSERT INTO Sesiuni
                        (id_echipament, id_membru, id_operator, tip_sesiune, data_sesiune,
                         durata_minute, cost_total, incasare, status, observatii)
                        VALUES (@id_echipament, @id_membru, @id_operator, @tip_sesiune,
                                @data_sesiune, @durata_minute, @cost_total, @incasare, @status, @observatii)";
                    AdaugaParametriSesiuni(cmd); break;

                case "Rezervari":
                    cmd.CommandText = @"INSERT INTO Rezervari
                        (id_echipament, id_membru, id_operator, data_rezervare, status, observatii)
                        VALUES (@id_echipament, @id_membru, @id_operator, @data_rezervare,
                                @status, @observatii)";
                    AdaugaParametriRezervari(cmd); break;
            }
            return cmd;
        }

        private SqlCommand CreazaComandaUpdate(SqlConnection conn)
        {
            var cmd = new SqlCommand { Connection = conn };
            switch (tabela)
            {
                case "Echipamente":
                    cmd.CommandText = @"UPDATE Echipamente SET
                        tip_echipament=@tip_echipament, denumire=@denumire, oras=@oras,
                        putere_watt=@putere_watt, ram_gb=@ram_gb, numar_locuri=@numar_locuri,
                        an_achizitie=@an_achizitie, tarif_ora=@tarif_ora, status=@status,
                        sala_id=@sala_id, descriere=@descriere
                        WHERE id_echipament=@id";
                    AdaugaParametriEchipamente(cmd); break;

                case "Sali":
                    cmd.CommandText = @"UPDATE Sali SET
                        nume_sala=@nume_sala, oras=@oras, adresa=@adresa,
                        operator_sef_id=@operator_sef_id, data_deschidere=@data_deschidere
                        WHERE id_sala=@id";
                    AdaugaParametriSali(cmd); break;

                case "Membri":
                    cmd.CommandText = @"UPDATE Membri SET
                        nume=@nume, prenume=@prenume, telefon=@telefon, email=@email,
                        tip_membru=@tip_membru, buget_max=@buget_max, oras_interes=@oras_interes
                        WHERE id_membru=@id";
                    AdaugaParametriMembri(cmd); break;

                case "Operatori":
                    cmd.CommandText = @"UPDATE Operatori SET
                        nume=@nume, prenume=@prenume, rol_club=@rol_club,
                        telefon=@telefon, email=@email, sala_id=@sala_id,
                        data_angajare=@data_angajare
                        WHERE id_operator=@id";
                    AdaugaParametriOperatori(cmd); break;

                case "Sesiuni":
                    cmd.CommandText = @"UPDATE Sesiuni SET
                        id_echipament=@id_echipament, id_membru=@id_membru, id_operator=@id_operator,
                        tip_sesiune=@tip_sesiune, data_sesiune=@data_sesiune,
                        durata_minute=@durata_minute, cost_total=@cost_total, incasare=@incasare, status=@status,
                        observatii=@observatii
                        WHERE id_sesiune=@id";
                    AdaugaParametriSesiuni(cmd); break;

                case "Rezervari":
                    cmd.CommandText = @"UPDATE Rezervari SET
                        id_echipament=@id_echipament, id_membru=@id_membru, id_operator=@id_operator,
                        data_rezervare=@data_rezervare, status=@status, observatii=@observatii
                        WHERE id_rezervare=@id";
                    AdaugaParametriRezervari(cmd); break;
            }
            cmd.Parameters.Add("@id", SqlDbType.Int).Value = idEditare;
            return cmd;
        }

        // ── Parametri pentru fiecare tabela ─────────────────────────
        //  Toate valorile sunt transmise ca parametri SQL
        //  (protectie anti SQL injection).

        private void AdaugaParametriEchipamente(SqlCommand cmd)
        {
            // FIX_09: checkbox „N/A" → DBNull.Value (nullable în BD);
            // NumericUpDown singur nu poate reprezenta NULL.
            cmd.Parameters.Add("@tip_echipament", SqlDbType.NVarChar, 30).Value
                = ValoareComboText("tip_echipament");
            cmd.Parameters.Add("@denumire", SqlDbType.NVarChar, 150).Value
                = ((TextBox)controale["denumire"]).Text.Trim();
            cmd.Parameters.Add("@oras", SqlDbType.NVarChar, 50).Value
                = ((TextBox)controale["oras"]).Text.Trim();
            AdaugaParametruDecimal(cmd, "@putere_watt",
                ((TextBox)controale["putere_watt"]).Text.Trim(), 8, 2);
            cmd.Parameters.Add("@ram_gb", SqlDbType.Int).Value
                = ValoareIntSauNull("ram_gb");
            cmd.Parameters.Add("@numar_locuri", SqlDbType.Int).Value
                = ValoareIntSauNull("numar_locuri");
            cmd.Parameters.Add("@an_achizitie", SqlDbType.Int).Value
                = ValoareIntSauNull("an_achizitie");
            AdaugaParametruDecimal(cmd, "@tarif_ora",
                ((TextBox)controale["tarif_ora"]).Text.Trim(), 12, 2);
            cmd.Parameters.Add("@status", SqlDbType.NVarChar, 20).Value
                = ValoareComboText("status");
            cmd.Parameters.Add("@sala_id", SqlDbType.Int).Value
                = ((ComboBox)controale["sala_id"]).SelectedValue;
            cmd.Parameters.Add("@descriere", SqlDbType.NVarChar, 500).Value
                = ValoareSauNull((TextBox)controale["descriere"]);
        }

        private void AdaugaParametriSali(SqlCommand cmd)
        {
            cmd.Parameters.Add("@nume_sala", SqlDbType.NVarChar, 50).Value
                = ((TextBox)controale["nume_sala"]).Text.Trim();
            cmd.Parameters.Add("@oras", SqlDbType.NVarChar, 50).Value
                = ((TextBox)controale["oras"]).Text.Trim();
            cmd.Parameters.Add("@adresa", SqlDbType.NVarChar, 100).Value
                = ValoareSauNull((TextBox)controale["adresa"]);
            cmd.Parameters.Add("@operator_sef_id", SqlDbType.Int).Value
                = ((ComboBox)controale["operator_sef_id"]).SelectedValue;
            cmd.Parameters.Add("@data_deschidere", SqlDbType.Date).Value
                = ((DateTimePicker)controale["data_deschidere"]).Value.Date;
        }

        private void AdaugaParametriMembri(SqlCommand cmd)
        {
            cmd.Parameters.Add("@nume", SqlDbType.NVarChar, 50).Value
                = ((TextBox)controale["nume"]).Text.Trim();
            cmd.Parameters.Add("@prenume", SqlDbType.NVarChar, 50).Value
                = ((TextBox)controale["prenume"]).Text.Trim();
            cmd.Parameters.Add("@telefon", SqlDbType.NVarChar, 15).Value
                = ((TextBox)controale["telefon"]).Text.Trim();
            cmd.Parameters.Add("@email", SqlDbType.NVarChar, 100).Value
                = ValoareSauNull((TextBox)controale["email"]);
            cmd.Parameters.Add("@tip_membru", SqlDbType.NVarChar, 20).Value
                = ValoareComboText("tip_membru");
            // Bugetul maxim este optional: 0 sau gol se salveaza ca NULL
            AdaugaParametruDecimalOptional(cmd, "@buget_max",
                ((TextBox)controale["buget_max"]).Text.Trim(), 12, 2);
            cmd.Parameters.Add("@oras_interes", SqlDbType.NVarChar, 50).Value
                = ValoareSauNull((TextBox)controale["oras_interes"]);
        }

        private void AdaugaParametriOperatori(SqlCommand cmd)
        {
            cmd.Parameters.Add("@nume", SqlDbType.NVarChar, 50).Value
                = ((TextBox)controale["nume"]).Text.Trim();
            cmd.Parameters.Add("@prenume", SqlDbType.NVarChar, 50).Value
                = ((TextBox)controale["prenume"]).Text.Trim();
            cmd.Parameters.Add("@rol_club", SqlDbType.NVarChar, 50).Value
                = ((TextBox)controale["rol_club"]).Text.Trim();
            cmd.Parameters.Add("@telefon", SqlDbType.NVarChar, 15).Value
                = ValoareSauNull((TextBox)controale["telefon"]);
            cmd.Parameters.Add("@email", SqlDbType.NVarChar, 100).Value
                = ValoareSauNull((TextBox)controale["email"]);
            cmd.Parameters.Add("@sala_id", SqlDbType.Int).Value
                = ValoareComboSauNull("sala_id");
            cmd.Parameters.Add("@data_angajare", SqlDbType.Date).Value
                = ((DateTimePicker)controale["data_angajare"]).Value.Date;
        }

        private void AdaugaParametriSesiuni(SqlCommand cmd)
        {
            cmd.Parameters.Add("@id_echipament", SqlDbType.Int).Value
                = ((ComboBox)controale["id_echipament"]).SelectedValue;
            cmd.Parameters.Add("@id_membru", SqlDbType.Int).Value
                = ((ComboBox)controale["id_membru"]).SelectedValue;
            cmd.Parameters.Add("@id_operator", SqlDbType.Int).Value
                = ((ComboBox)controale["id_operator"]).SelectedValue;
            cmd.Parameters.Add("@tip_sesiune", SqlDbType.NVarChar, 20).Value
                = ValoareComboText("tip_sesiune");
            cmd.Parameters.Add("@data_sesiune", SqlDbType.Date).Value
                = ((DateTimePicker)controale["data_sesiune"]).Value.Date;
            cmd.Parameters.Add("@durata_minute", SqlDbType.Int).Value
                = (int)((NumericUpDown)controale["durata_minute"]).Value;
            AdaugaParametruDecimal(cmd, "@cost_total",
                ((TextBox)controale["cost_total"]).Text.Trim(), 12, 2);
            AdaugaParametruDecimal(cmd, "@incasare",
                ((TextBox)controale["incasare"]).Text.Trim(), 10, 2);
            cmd.Parameters.Add("@status", SqlDbType.NVarChar, 20).Value
                = ValoareComboText("status");
            cmd.Parameters.Add("@observatii", SqlDbType.NVarChar, 200).Value
                = ValoareSauNull((TextBox)controale["observatii"]);
        }

        private void AdaugaParametriRezervari(SqlCommand cmd)
        {
            cmd.Parameters.Add("@id_echipament", SqlDbType.Int).Value
                = ((ComboBox)controale["id_echipament"]).SelectedValue;
            cmd.Parameters.Add("@id_membru", SqlDbType.Int).Value
                = ((ComboBox)controale["id_membru"]).SelectedValue;
            cmd.Parameters.Add("@id_operator", SqlDbType.Int).Value
                = ((ComboBox)controale["id_operator"]).SelectedValue;
            cmd.Parameters.Add("@data_rezervare", SqlDbType.DateTime).Value
                = ((DateTimePicker)controale["data_rezervare"]).Value;
            cmd.Parameters.Add("@status", SqlDbType.NVarChar, 20).Value
                = ValoareComboText("status");
            cmd.Parameters.Add("@observatii", SqlDbType.NVarChar, 200).Value
                = ValoareSauNull((TextBox)controale["observatii"]);
        }

        // ── Helpers pentru parametri ────────────────────────────────

        /// <summary>Text sau DBNull.Value daca e gol.</summary>
        private static object ValoareSauNull(TextBox txt)
        {
            string val = txt.Text.Trim();
            return string.IsNullOrEmpty(val) ? (object)DBNull.Value : val;
        }

        // ── FIX_09: helpers N/A → NULL ──────────────────────────
        private bool EsteNaBifat(string camp)
            => chkNull.TryGetValue(camp, out CheckBox chk) && chk.Checked;

        private void SetNaBifat(string camp, bool bifat)
        {
            if (chkNull.TryGetValue(camp, out CheckBox chk))
                chk.Checked = bifat; // handler-ul CheckedChanged dezactivează NUD-ul
        }

        private object ValoareIntSauNull(string camp)
        {
            if (EsteNaBifat(camp)) return DBNull.Value;
            return (int)((NumericUpDown)controale[camp]).Value;
        }

        /// <summary>Valoarea afisata a unui ComboBox static (text).</summary>
        private string ValoareComboText(string camp)
        {
            if (!controale.ContainsKey(camp)) return null;
            var cmb = (ComboBox)controale[camp];
            return cmb.SelectedIndex < 0 ? null : cmb.Text;
        }

        /// <summary>Valoarea (ID) a unui ComboBox cu cheie externa, sau NULL.</summary>
        private object ValoareComboSauNull(string camp)
        {
            if (!controale.ContainsKey(camp)) return DBNull.Value;
            var cmb = (ComboBox)controale[camp];
            return cmb.SelectedValue == null ? (object)DBNull.Value : cmb.SelectedValue;
        }

        /// <summary>Parametru DECIMAL cu precizie si scala explicite.</summary>
        private static void AdaugaParametruDecimal(SqlCommand cmd, string nume,
            string text, byte precizie, byte scala)
        {
            var p = cmd.Parameters.Add(nume, SqlDbType.Decimal);
            p.Precision = precizie;
            p.Scale     = scala;
            string t = (text ?? "").Trim().Replace(',', '.');
            p.Value     = decimal.Parse(t, NumberStyles.Number, CultureInfo.InvariantCulture);
        }

        /// <summary>Parametru DECIMAL optional: 0 sau text gol devine NULL.</summary>
        private static void AdaugaParametruDecimalOptional(SqlCommand cmd, string nume,
            string text, byte precizie, byte scala)
        {
            var p = cmd.Parameters.Add(nume, SqlDbType.Decimal);
            p.Precision = precizie;
            p.Scale     = scala;

            decimal valoare;
            string t = (text ?? "").Trim().Replace(',', '.');
            p.Value = (!decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out valoare) || valoare <= 0)
                ? (object)DBNull.Value
                : valoare;
        }

        // ════════════════════════════════════════════════════════════
        //  HELPER — creare controale UI (Material, fara decupaje)
        // ════════════════════════════════════════════════════════════
        private void AdaugaCamp(Panel parent, string label, string nume,
            int y, int lw, int cw, bool multiline = false)
        {
            AdaugaLabel(parent, label, y, lw);
            var txt = new TextBox
            {
                Name      = nume,
                Location  = new System.Drawing.Point(20 + lw, y),
                Size      = new System.Drawing.Size(cw, multiline ? 52 : 28),
                Multiline = multiline,
                MaxLength = 500,
                Font      = MaterialTheme.FontBody,
                ForeColor = MaterialTheme.TextPrimary,
                BackColor = MaterialTheme.SurfaceAlt,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            parent.Controls.Add(txt);
            controale[nume] = txt;
        }

        // Q2: DateTimePicker pentru data sesiunei — blocat la ziua curenta
        private void AdaugaCampDataSesiune(Panel parent, string label, string nume,
            int y, int lw, int cw)
        {
            AdaugaLabel(parent, label, y, lw);
            var dtp = new DateTimePicker
            {
                Name     = nume,
                Location = new System.Drawing.Point(20 + lw, y),
                Size     = new System.Drawing.Size(cw, 28),
                Format   = DateTimePickerFormat.Short,
                Font     = MaterialTheme.FontBody,
                MaxDate  = DateTime.Today,   // nu permite date din viitor
                Value    = DateTime.Today
            };
            MaterialUIHelper.StyleInput(dtp);
            parent.Controls.Add(dtp);
            controale[nume] = dtp;
        }

        private void AdaugaCampData(Panel parent, string label, string nume, int y, int lw, int cw)
        {
            AdaugaLabel(parent, label, y, lw);
            var dtp = new DateTimePicker
            {
                Name     = nume,
                Location = new System.Drawing.Point(20 + lw, y),
                Size     = new System.Drawing.Size(cw, 28),
                Format   = DateTimePickerFormat.Short,
                Font     = MaterialTheme.FontBody
            };
            MaterialUIHelper.StyleInput(dtp);
            parent.Controls.Add(dtp);
            controale[nume] = dtp;
        }

        // Rezervari: data + ora (DATETIME)
        private void AdaugaCampDataOra(Panel parent, string label, string nume, int y, int lw, int cw)
        {
            AdaugaLabel(parent, label, y, lw);
            var dtp = new DateTimePicker
            {
                Name         = nume,
                Location     = new System.Drawing.Point(20 + lw, y),
                Size         = new System.Drawing.Size(cw, 28),
                Format       = DateTimePickerFormat.Custom,
                CustomFormat = "dd.MM.yyyy HH:mm",
                ShowUpDown   = true,
                Font         = MaterialTheme.FontBody,
                Value        = DateTime.Now
            };
            MaterialUIHelper.StyleInput(dtp);
            parent.Controls.Add(dtp);
            controale[nume] = dtp;
        }

        private void AdaugaCampNumeric(Panel parent, string label, string nume, int y,
            int lw, int cw, int minim = 0, int maxim = 100, int valoareImplicita = 0)
        {
            AdaugaLabel(parent, label, y, lw);
            var nud = new NumericUpDown
            {
                Name     = nume,
                Location = new System.Drawing.Point(20 + lw, y),
                Size     = new System.Drawing.Size(cw, 28),
                Minimum  = minim,
                Maximum  = maxim,
                Font     = MaterialTheme.FontBody
            };
            MaterialUIHelper.StyleInput(nud);
            if (valoareImplicita >= minim && valoareImplicita <= maxim)
                nud.Value = valoareImplicita;

            parent.Controls.Add(nud);
            controale[nume] = nud;
        }

        // FIX_09: Numeric + checkbox „N/A" → NULL în BD (ex: Server nu are locuri).
        private void AdaugaCampNumericNullable(Panel parent, string label, string nume, int y,
            int lw, int cw, int minim, int maxim, int valoareImplicita)
        {
            AdaugaLabel(parent, label, y, lw);
            int latNud = Math.Max(120, cw - 110);
            var nud = new NumericUpDown
            {
                Name     = nume,
                Location = new System.Drawing.Point(20 + lw, y),
                Size     = new System.Drawing.Size(latNud, 28),
                Minimum  = minim,
                Maximum  = maxim,
                Font     = MaterialTheme.FontBody
            };
            MaterialUIHelper.StyleInput(nud);
            if (valoareImplicita >= minim && valoareImplicita <= maxim)
                nud.Value = valoareImplicita;
            parent.Controls.Add(nud);
            controale[nume] = nud;

            var chk = new CheckBox
            {
                Name      = nume + "_na",
                Text      = "N/A",
                Checked   = false,
                AutoSize  = true,
                Location  = new System.Drawing.Point(20 + lw + latNud + 8, y + 5),
                Font      = MaterialTheme.FontSmall,
                ForeColor = MaterialTheme.TextSecondary,
                BackColor = Color.Transparent
            };
            chk.CheckedChanged += (s, e) => nud.Enabled = !chk.Checked;
            parent.Controls.Add(chk);
            chkNull[nume] = chk;
        }

        // FIX_09: la tip „Server" se bifează automat N/A pentru locuri (utilizatorul poate debifa).
        private void ConfigureazaAutoNaPentruServer()
        {
            if (tabela != "Echipamente" || !controale.ContainsKey("tip_echipament")) return;
            var cmb = (ComboBox)controale["tip_echipament"];
            cmb.SelectedIndexChanged += (s, e) =>
            {
                if (ValoareComboText("tip_echipament") == "Server")
                {
                    SetNaBifat("ram_gb", true);
                    SetNaBifat("numar_locuri", true);
                    SetNaBifat("an_achizitie", true);
                }
            };
        }

        private void AdaugaCampDecimal(Panel parent, string label, string nume, int y, int lw, int cw)
        {
            AdaugaLabel(parent, label, y, lw);
            var txt = new TextBox
            {
                Name     = nume,
                Location = new System.Drawing.Point(20 + lw, y),
                Size     = new System.Drawing.Size(cw, 28),
                Text     = "0",
                Font     = MaterialTheme.FontBody,
                ForeColor = MaterialTheme.TextPrimary,
                BackColor = MaterialTheme.SurfaceAlt,
                BorderStyle = BorderStyle.FixedSingle
            };
            parent.Controls.Add(txt);
            controale[nume] = txt;
        }

        private void AdaugaCombo(Panel parent, string label, string nume,
            int y, int lw, int cw, Func<ComboBox, Task> loader)
        {
            AdaugaLabel(parent, label, y, lw);
            var cmb = new ComboBox
            {
                Name          = nume,
                Location      = new System.Drawing.Point(20 + lw, y),
                Size          = new System.Drawing.Size(cw, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = MaterialTheme.FontBody
            };
            MaterialUIHelper.StyleInput(cmb);
            _incarcariCombo.Add(() => loader(cmb));
            parent.Controls.Add(cmb);
            controale[nume] = cmb;
        }

        /// <summary>
        /// ComboBox cu lista fixa de optiuni (tipuri / statusuri).
        /// Valorile sunt construite ca DataTable in memorie, ca sa
        /// functioneze identic cu combo-urile care incarca din baza de date.
        /// </summary>
        private void AdaugaComboOptiuni(Panel parent, string label, string nume,
            int y, int lw, int cw, string[] optiuni)
        {
            AdaugaCombo(parent, label, nume, y, lw, cw, cmb =>
            {
                cmb.DataSource    = CreeazaTabelOptiuni(optiuni);
                cmb.DisplayMember = "denumire";
                cmb.ValueMember   = "id";
                return Task.CompletedTask;
            });
        }

        private static DataTable CreeazaTabelOptiuni(string[] optiuni)
        {
            var dt = new DataTable();
            dt.Columns.Add("id",       typeof(int));
            dt.Columns.Add("denumire", typeof(string));
            for (int i = 0; i < optiuni.Length; i++)
                dt.Rows.Add(i + 1, optiuni[i]);
            return dt;
        }

        /// <summary>True pentru ComboBox-urile cu lista fixa (valorile sunt texte).</summary>
        private static bool EsteComboStatic(ComboBox cmb)
            => cmb.ValueMember == "id" &&
               cmb.DataSource is DataTable dt && dt.Columns.Contains("denumire");

        private void AdaugaComboSali(Panel parent, string label, string nume, int y, int lw, int cw)
            => AdaugaCombo(parent, label, nume, y, lw, cw, IncarcaSali);
        private void AdaugaComboOperatori(Panel parent, string label, string nume, int y, int lw, int cw)
            => AdaugaCombo(parent, label, nume, y, lw, cw, IncarcaOperatori);
        private void AdaugaComboEchipamente(Panel parent, string label, string nume, int y, int lw, int cw)
            => AdaugaCombo(parent, label, nume, y, lw, cw, IncarcaEchipamente);
        private void AdaugaComboMembri(Panel parent, string label, string nume, int y, int lw, int cw)
            => AdaugaCombo(parent, label, nume, y, lw, cw, IncarcaMembri);

        private void AdaugaLabel(Panel parent, string text, int y, int lw)
        {
            var lbl = new Label
            {
                Text      = text,
                Font      = MaterialTheme.FontLabel,
                ForeColor = MaterialTheme.TextSecondary,
                BackColor = System.Drawing.Color.Transparent,
                Location  = new System.Drawing.Point(20, y + 4),
                Size      = new System.Drawing.Size(lw, 22),
                AutoEllipsis = true
            };
            parent.Controls.Add(lbl);
        }

        // ── Incarcare ComboBox-uri din baza de date ─────────────────
        private Task IncarcaSali(ComboBox cmb)
            => IncarcaComboAsync(cmb, "SELECT id_sala, nume_sala FROM Sali ORDER BY nume_sala",
                "id_sala", "nume_sala");
        private Task IncarcaOperatori(ComboBox cmb)
            => IncarcaComboAsync(cmb,
                "SELECT id_operator, nume + ' ' + prenume AS n FROM Operatori ORDER BY nume",
                "id_operator", "n");
        private Task IncarcaEchipamente(ComboBox cmb)
            => IncarcaComboAsync(cmb,
                "SELECT id_echipament, denumire + ' (' + oras + ')' AS n FROM Echipamente ORDER BY oras, denumire",
                "id_echipament", "n");
        private Task IncarcaMembri(ComboBox cmb)
            => IncarcaComboAsync(cmb,
                "SELECT id_membru, nume + ' ' + prenume AS n FROM Membri ORDER BY nume",
                "id_membru", "n");

        private async Task IncarcaComboAsync(ComboBox cmb, string query,
            string valueMember, string displayMember)
        {
            try
            {
                DataTable dt = await SqlDb.QueryDataTableAsync(query);
                if (cmb.IsDisposed) return;
                cmb.DataSource    = dt;
                cmb.DisplayMember = displayMember;
                cmb.ValueMember   = valueMember;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, $"FormEditare.IncarcaCombo({valueMember})");
            }
        }
    }
}
