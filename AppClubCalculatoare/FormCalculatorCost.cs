using System;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using AppClubCalculatoare.UI;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Calculator de cost sesiune/abonament.
    /// Estimeaza costul pe baza: tarif orar, durata sesiunii, numar de
    /// sesiuni pe luna, pachet (discount de volum) si reducere de membru.
    /// Afiseaza totalul + un tabel comparativ intre pachete.
    ///
    /// Permisiune necesară: PuncteAcces.CalculatorCost.
    /// </summary>
    public class FormCalculatorCost : Form
    {
        // ── Câmpuri de intrare ─────────────────────────────────────
        private TextBox txtTarif;
        private TextBox txtDurata;
        private TextBox txtSesiuni;
        private ComboBox cmbPachet;
        private TextBox txtReducere;

        // Reducerile pachetelor (procente).
        private static readonly string[] NumePachete =
            { "Fără pachet", "Pachet 5 ore", "Pachet 10 ore", "Pachet 20 ore", "Abonament lunar" };
        private static readonly double[] DiscountPachete = { 0, 5, 10, 15, 20 };

        // ── Rezultate ──────────────────────────────────────────────
        private Label lblCostBrut;
        private Label lblDiscountPachet;
        private Label lblReducereMembru;
        private Label lblTotal;
        private Label lblCostMediu;

        // ── Tabel comparativ pachete ───────────────────────────────
        private DataGridView dgvComparativ;

        public FormCalculatorCost()
        {
            this.Text            = "Calculator Cost Sesiune";
            this.Size            = new Size(860, 700);
            this.MinimumSize     = new Size(740, 600);
            this.StartPosition   = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox     = true;
            this.BackColor       = MaterialTheme.Background;
            this.Font            = MaterialTheme.FontBody;
            BuildUI();
        }

        private void BuildUI()
        {
            // ── Conținut central (Dock.Fill) — adăugat PRIMUL ─────
            var content = MaterialUIHelper.ContentPanel();
            content.Padding = new Padding(20, 16, 20, 10);
            this.Controls.Add(content);

            // ── Bara inferioară (Dock.Bottom) ──────────────────────
            FlowLayoutPanel leftFlow, rightFlow;
            var bottomBar = MaterialUIHelper.SplitBottomBar(out leftFlow, out rightFlow);
            this.Controls.Add(bottomBar);

            var btnCalculeaza = MaterialUIHelper.Button("🧮  Calculează", MaterialButtonStyle.Primary, 160);
            btnCalculeaza.Click += (s, e) => Calculeaza();
            rightFlow.Controls.Add(btnCalculeaza);

            var btnExport = MaterialUIHelper.Button("📋  Copiază tabel", MaterialButtonStyle.Secondary, 160);
            btnExport.Click += (s, e) => CopiazaTabel();
            rightFlow.Controls.Add(btnExport);

            var btnInchide = MaterialUIHelper.Button("✕  Închide", MaterialButtonStyle.Outline, 120);
            btnInchide.Click += (s, e) => this.Close();
            rightFlow.Controls.Add(btnInchide);

            this.AcceptButton = btnCalculeaza;
            this.CancelButton = btnInchide;

            // ── Header colorat (Dock.Top) — adăugat ULTIMUL ───────
            var header = MaterialUIHelper.Header(
                "Calculator Cost Sesiune",
                "Cost orar × durată × sesiuni, cu discount de pachet și reducere de membru",
                SessionInfo.NumeComplet, MaterialTheme.Info);
            this.Controls.Add(header);

            // ── Layout principal: 2 coloane ────────────────────────
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.Controls.Add(layout);

            // ═══ COLOANA STÂNGĂ: parametri ═════════════════════════
            var cardParams = new MaterialCard
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16)
            };
            layout.Controls.Add(cardParams, 0, 0);

            int y = 12;
            AdaugaEtichetaSectiune(cardParams, "PARAMETRI SESIUNE", ref y);
            y += 8;

            txtTarif = AdaugaCampNumeric(cardParams, "Tarif orar (lei/oră)", ref y);
            txtTarif.Text = "25";

            txtDurata = AdaugaCampNumeric(cardParams, "Durată sesiune (minute)", ref y);
            txtDurata.Text = "120";

            txtSesiuni = AdaugaCampNumeric(cardParams, "Sesiuni pe lună", ref y);
            txtSesiuni.Text = "8";

            cmbPachet = AdaugaComboPachet(cardParams, "Pachet", ref y);

            txtReducere = AdaugaCampNumeric(cardParams, "Reducere membru (%)", ref y);
            txtReducere.Text = "0";

            y += 10;
            AdaugaEtichetaSectiune(cardParams, "REZULTATE", ref y);
            y += 8;

            lblCostBrut       = AdaugaRandRezultat(cardParams, "Cost brut:", ref y);
            lblDiscountPachet = AdaugaRandRezultat(cardParams, "Discount pachet:", ref y);
            lblReducereMembru = AdaugaRandRezultat(cardParams, "Reducere membru:", ref y);
            lblTotal          = AdaugaRandRezultat(cardParams, "Total de plată:", ref y);
            lblCostMediu      = AdaugaRandRezultat(cardParams, "Cost mediu / sesiune:", ref y);

            // ═══ COLOANA DREAPTĂ: tabel comparativ ═════════════════
            var panelTabel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 0, 0, 0)
            };
            layout.Controls.Add(panelTabel, 1, 0);

            var lblTabel = new Label
            {
                Text = "Comparație pachete (aceeași configurație)",
                Font = MaterialTheme.FontSubtitle,
                ForeColor = MaterialTheme.TextPrimary,
                BackColor = Color.Transparent,
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft
            };
            panelTabel.Controls.Add(lblTabel);

            dgvComparativ = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = MaterialTheme.Surface,
                BorderStyle = BorderStyle.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 34,
                RowTemplate = { Height = 26 },
                Font = MaterialTheme.FontSmall,
                EnableHeadersVisualStyles = false
            };
            dgvComparativ.ColumnHeadersDefaultCellStyle.BackColor = MaterialTheme.Info;
            dgvComparativ.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvComparativ.ColumnHeadersDefaultCellStyle.Font = MaterialTheme.FontLabel;
            dgvComparativ.ColumnHeadersDefaultCellStyle.SelectionBackColor = MaterialTheme.Info;
            // Tema închisă: selecție violetă cu text deschis (lizibil), rânduri
            // alternate închise cu text deschis, grid linie discretă.
            dgvComparativ.GridColor = MaterialTheme.Divider;
            dgvComparativ.DefaultCellStyle.BackColor = MaterialTheme.Surface;
            dgvComparativ.DefaultCellStyle.ForeColor = MaterialTheme.TextPrimary;
            dgvComparativ.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0x3B, 0x2A, 0x6B); // violet selection
            dgvComparativ.DefaultCellStyle.SelectionForeColor = MaterialTheme.TextPrimary;
            dgvComparativ.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
            dgvComparativ.AlternatingRowsDefaultCellStyle.BackColor = MaterialTheme.Background;
            dgvComparativ.AlternatingRowsDefaultCellStyle.ForeColor = MaterialTheme.TextPrimary;
            dgvComparativ.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(0x3B, 0x2A, 0x6B);
            dgvComparativ.AlternatingRowsDefaultCellStyle.SelectionForeColor = MaterialTheme.TextPrimary;
            panelTabel.Controls.Add(dgvComparativ);
            dgvComparativ.BringToFront();
        }

        // ── Calculul costului ──────────────────────────────────────
        private void Calculeaza()
        {
            double tarif, reducerePct;
            int durataMinute, nrSesiuni;

            if (!Parseaza(txtTarif.Text, out tarif) || tarif <= 0)
            { AfiseazaEroare("Tariful orar trebuie să fie un număr pozitiv."); return; }

            if (!int.TryParse(txtDurata.Text.Trim(), out durataMinute) || durataMinute < 15 || durataMinute > 720)
            { AfiseazaEroare("Durata sesiunii trebuie să fie între 15 și 720 de minute."); return; }

            if (!int.TryParse(txtSesiuni.Text.Trim(), out nrSesiuni) || nrSesiuni < 1 || nrSesiuni > 100)
            { AfiseazaEroare("Numărul de sesiuni trebuie să fie între 1 și 100."); return; }

            if (!Parseaza(txtReducere.Text, out reducerePct) || reducerePct < 0 || reducerePct > 90)
            { AfiseazaEroare("Reducerea de membru trebuie să fie între 0% și 90%."); return; }

            int idxPachet = Math.Max(0, cmbPachet.SelectedIndex);
            double oreTotale = durataMinute / 60.0 * nrSesiuni;
            double brut = tarif * oreTotale;

            double rez = CalculeazaTotal(brut, DiscountPachete[idxPachet], reducerePct);
            double discountPachet = brut * DiscountPachete[idxPachet] / 100.0;
            double bazaDupaPachet = brut - discountPachet;
            double valReducere = bazaDupaPachet * reducerePct / 100.0;

            // Afișare rezultate
            lblCostBrut.Text       = FormateazaLei(brut);
            lblDiscountPachet.Text = "−" + FormateazaLei(discountPachet) + $" ({DiscountPachete[idxPachet]:N0}%)";
            lblReducereMembru.Text = "−" + FormateazaLei(valReducere) + $" ({reducerePct:N2}%)";
            lblTotal.Text          = FormateazaLei(rez);
            lblCostMediu.Text      = FormateazaLei(rez / nrSesiuni) + " / sesiune";

            lblTotal.ForeColor     = MaterialTheme.Primary;
            lblCostMediu.ForeColor = MaterialTheme.TextPrimary;

            // ── Tabel comparativ pe pachete ────────────────────────
            dgvComparativ.Columns.Clear();
            dgvComparativ.Columns.Add("Pachet", "Pachet");
            dgvComparativ.Columns.Add("Discount", "Discount");
            dgvComparativ.Columns.Add("Total", "Total lună");
            dgvComparativ.Columns.Add("Mediu", "Mediu / sesiune");
            dgvComparativ.Columns.Add("Economie", "Economie vs fără pachet");
            dgvComparativ.Rows.Clear();

            double totalFaraPachet = CalculeazaTotal(brut, 0, reducerePct);
            for (int i = 0; i < NumePachete.Length; i++)
            {
                double tot = CalculeazaTotal(brut, DiscountPachete[i], reducerePct);
                dgvComparativ.Rows.Add(
                    NumePachete[i] + (i == idxPachet ? "  ✓" : ""),
                    DiscountPachete[i].ToString("N0") + "%",
                    FormateazaLei(tot),
                    FormateazaLei(tot / nrSesiuni),
                    FormateazaLei(totalFaraPachet - tot));
            }

            foreach (DataGridViewColumn col in dgvComparativ.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        private static double CalculeazaTotal(double brut, double discountPachetPct, double reducerePct)
        {
            double dupaPachet = brut * (1 - discountPachetPct / 100.0);
            return dupaPachet * (1 - reducerePct / 100.0);
        }

        // ── Helpers UI ─────────────────────────────────────────────
        private void AdaugaEtichetaSectiune(Control parent, string text, ref int y)
        {
            var lbl = new Label
            {
                Text = text,
                Font = MaterialTheme.FontLabel,
                ForeColor = MaterialTheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(16, y),
                AutoSize = true
            };
            parent.Controls.Add(lbl);
            y += 24;
        }

        private TextBox AdaugaCampNumeric(Control parent, string label, ref int y)
        {
            TextBox txt;
            MaterialUIHelper.LabeledTextBox(label, parent, ref y, 16, 270, false, out txt);
            return txt;
        }

        private ComboBox AdaugaComboPachet(Control parent, string label, ref int y)
        {
            var lbl = new Label
            {
                Text = label,
                Font = MaterialTheme.FontLabel,
                ForeColor = MaterialTheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(16, y),
                AutoSize = true
            };
            parent.Controls.Add(lbl);
            y += 20;

            var cmb = new ComboBox
            {
                Location = new Point(16, y),
                Size = new Size(270, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = MaterialTheme.FontBody
            };
            cmb.Items.AddRange(NumePachete);
            cmb.SelectedIndex = 0;
            parent.Controls.Add(cmb);
            y += 36;
            return cmb;
        }

        private Label AdaugaRandRezultat(Control parent, string label, ref int y)
        {
            var lbl = new Label
            {
                Text = label,
                Font = MaterialTheme.FontBody,
                ForeColor = MaterialTheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(16, y),
                AutoSize = true
            };
            parent.Controls.Add(lbl);

            var val = new Label
            {
                Text = "—",
                Font = MaterialTheme.FontSubtitle,
                ForeColor = MaterialTheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(16, y + 20),
                AutoSize = true
            };
            parent.Controls.Add(val);
            y += 46;
            return val;
        }

        private void CopiazaTabel()
        {
            if (dgvComparativ.Rows.Count == 0)
            {
                MessageBox.Show("Calculează mai întâi costul.",
                    "Calculator Cost", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Pachet\tDiscount\tTotal lună\tMediu / sesiune\tEconomie vs fără pachet");
            foreach (DataGridViewRow row in dgvComparativ.Rows)
            {
                sb.AppendLine(string.Join("\t",
                    row.Cells[0].Value, row.Cells[1].Value,
                    row.Cells[2].Value, row.Cells[3].Value, row.Cells[4].Value));
            }
            Clipboard.SetText(sb.ToString());
            MessageBox.Show("Tabelul a fost copiat în clipboard (format TSV).",
                "Calculator Cost", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void AfiseazaEroare(string mesaj)
        {
            MessageBox.Show(mesaj, "Date invalide",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static bool Parseaza(string text, out double valoare)
        {
            string t = (text ?? "").Trim().Replace(',', '.');
            return double.TryParse(t, NumberStyles.Number,
                CultureInfo.InvariantCulture, out valoare);
        }

        private static string FormateazaLei(double valoare)
        {
            return valoare.ToString("N2", CultureInfo.CurrentCulture) + " lei";
        }
    }
}
