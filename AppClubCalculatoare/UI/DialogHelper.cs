using System;
using System.Drawing;
using System.Windows.Forms;

namespace AppClubCalculatoare.UI
{
    /// <summary>Tipul de dialog (dă culoarea barei de accent).</summary>
    internal enum DialogKind
    {
        Info,
        Atentie,
        Eroare,
        Intrebare
    }

    /// <summary>
    /// FIX BUG-04: dialoguri de decizie cu butoane în ROMÂNĂ, în tema aplicației.
    ///
    /// De ce era nevoie: <see cref="MessageBox"/> apelează MessageBoxW din
    /// user32.dll, iar textul butoanelor vine din resursele MUI ale sistemului de
    /// operare. Pe un Windows cu interfața în altă limbă (ex. rusă), un dialog
    /// „Da/Nu" apărea ca „Да/Нет" în mijlocul unei aplicații integral românești.
    /// CultureInfo.DefaultThreadCurrentUICulture NU are niciun efect asupra
    /// MessageBox-ului Win32 — deci singura soluție reală este un dialog propriu.
    ///
    /// Se folosește pentru dialogurile cu DA/NU/Anulează (acolo unde eticheta
    /// butonului contează pentru utilizator). Dialogurile cu un singur buton „OK"
    /// pot rămâne MessageBox: „OK" se scrie la fel în aproape toate limbile.
    /// </summary>
    internal static class DialogHelper
    {
        /// <summary>Întrebare cu două opțiuni (implicit „Da" / „Nu").</summary>
        public static bool Confirma(IWin32Window proprietar, string titlu, string mesaj,
            string textDa = "Da", string textNu = "Nu")
        {
            return Afiseaza(proprietar, titlu, mesaj, DialogKind.Intrebare, textDa, textNu);
        }

        /// <summary>Întrebare de confirmare a unei acțiuni ireversibile („Da" / „Anulează").</summary>
        public static bool ConfirmaActiune(IWin32Window proprietar, string titlu, string mesaj,
            string textDa = "Da", string textAnuleaza = "Anulează")
        {
            return Afiseaza(proprietar, titlu, mesaj, DialogKind.Intrebare, textDa, textAnuleaza);
        }

        /// <summary>
        /// Întrebare cu trei opțiuni (înlocuiește MessageBoxButtons.YesNoCancel).
        /// Întoarce Yes / No / Cancel.
        /// </summary>
        public static DialogResult Intreaba(IWin32Window proprietar, string titlu, string mesaj,
            string textDa = "Da", string textNu = "Nu", string textAnuleaza = "Anulează")
        {
            try
            {
                using (var dlg = new FormDialog(titlu, mesaj, DialogKind.Intrebare,
                                                textDa, textNu, textAnuleaza))
                    return dlg.ShowDialog(proprietar);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("DialogHelper a eșuat, fallback MessageBox: " + ex.Message,
                    "DialogHelper");
                return MessageBox.Show(mesaj, titlu, MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);
            }
        }

        private static bool Afiseaza(IWin32Window proprietar, string titlu, string mesaj,
            DialogKind fel, string textAccept, string textRefuz)
        {
            try
            {
                using (var dlg = new FormDialog(titlu, mesaj, fel, textAccept, textRefuz))
                    return dlg.ShowDialog(proprietar) == DialogResult.Yes;
            }
            catch (Exception ex)
            {
                // Plasă de siguranță: dacă dialogul propriu eșuează, revenim la
                // MessageBox ca să nu blocăm fluxul utilizatorului.
                AppLogger.Warn("DialogHelper a eșuat, fallback MessageBox: " + ex.Message,
                    "DialogHelper");
                return MessageBox.Show(mesaj, titlu, MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.Yes;
            }
        }
    }

    /// <summary>Dialog modal tematic, cu butoane proprii (vezi DialogHelper).</summary>
    internal class FormDialog : Form
    {
        private const int Latime = 440;

        public FormDialog(string titlu, string mesaj, DialogKind fel,
                          string textAccept = "OK", string textRefuz = null,
                          string textAnuleaza = null)
        {
            Text              = titlu;
            FormBorderStyle   = FormBorderStyle.FixedDialog;
            StartPosition     = FormStartPosition.CenterParent;
            MaximizeBox       = false;
            MinimizeBox       = false;
            ShowInTaskbar     = false;
            BackColor         = MaterialTheme.Background;
            Font              = MaterialTheme.FontBody;
            KeyPreview        = true;

            // DPI: fereastra este DPI-aware (app.manifest) — scalăm tot arborele
            // o singură dată, uniform (125% pe ecranele HiDPI), plus bară de titlu
            // închisă ca la restul aplicației.
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode       = AutoScaleMode.Dpi;
            WindowChrome.EnableDarkTitleBar(this);

            int latimeText = Latime - 48;

            // Măsurăm textul ca fereastra să se dimensioneze corect (și pentru
            // mesaje lungi, pe mai multe rânduri).
            Size marimeText = TextRenderer.MeasureText(
                mesaj ?? "", MaterialTheme.FontBody,
                new Size(latimeText, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

            int inaltimeMesaj = Math.Max(56, marimeText.Height + 40);
            const int inaltimeBaraJos = 56;
            ClientSize = new Size(Latime, 4 + inaltimeMesaj + inaltimeBaraJos);

            // Ordinea de adăugare contează pentru Dock: Fill PRIMUL, apoi
            // Bottom, apoi Top (WinForms așază în ordinea inversă a colecției).
            var lblMesaj = new Label
            {
                Text      = mesaj,
                Dock      = DockStyle.Fill,
                Font      = MaterialTheme.FontBody,
                ForeColor = MaterialTheme.TextPrimary,
                BackColor = Color.Transparent,
                Padding   = new Padding(24, 18, 24, 14),
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(lblMesaj);

            var baraJos = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = inaltimeBaraJos,
                BackColor = MaterialTheme.BottomBar
            };
            baraJos.Paint += (s, e) =>
                Design.DrawSeparator(e.Graphics, 0, baraJos.Width, Design.Border);
            Controls.Add(baraJos);

            var dreapta = new FlowLayoutPanel
            {
                Dock            = DockStyle.Right,
                FlowDirection   = FlowDirection.LeftToRight,
                WrapContents    = false,
                AutoSize        = true,
                AutoSizeMode    = AutoSizeMode.GrowAndShrink,
                BackColor       = Color.Transparent,
                Padding         = new Padding(0, 12, 18, 12),
                Margin          = new Padding(0)
            };
            baraJos.Controls.Add(dreapta);

            MaterialButton btnRefuz = null;
            if (!string.IsNullOrEmpty(textRefuz))
            {
                btnRefuz = MaterialUIHelper.Button(textRefuz, MaterialButtonStyle.Outline, 140);
                btnRefuz.DialogResult = DialogResult.No;
                btnRefuz.Margin = new Padding(0, 0, 10, 0);
                dreapta.Controls.Add(btnRefuz);
            }

            MaterialButton btnAnuleaza = null;
            if (!string.IsNullOrEmpty(textAnuleaza))
            {
                btnAnuleaza = MaterialUIHelper.Button(textAnuleaza, MaterialButtonStyle.Outline, 140);
                btnAnuleaza.DialogResult = DialogResult.Cancel;
                btnAnuleaza.Margin = new Padding(0, 0, 10, 0);
                dreapta.Controls.Add(btnAnuleaza);
            }

            var btnAccept = MaterialUIHelper.Button(textAccept,
                fel == DialogKind.Eroare ? MaterialButtonStyle.Danger : MaterialButtonStyle.Primary, 140);
            btnAccept.DialogResult = DialogResult.Yes;
            dreapta.Controls.Add(btnAccept);

            // Ordinea de tabulare: întâi acțiunea principală.
            btnAccept.TabIndex = 0;
            if (btnRefuz != null)    btnRefuz.TabIndex    = 1;
            if (btnAnuleaza != null) btnAnuleaza.TabIndex = 2;
            AcceptButton = btnAccept;
            if (btnAnuleaza != null)      CancelButton = btnAnuleaza;
            else if (btnRefuz != null)    CancelButton = btnRefuz;
            else                          CancelButton = btnAccept;

            // Fără pictogramă de sistem (altfel reapare aspectul Win32 vechi).
            ShowIcon = false;
        }

        // Colțurile ferestrei sunt rotunjite de DWM (ApplySystemChrome);
        // nu mai decupăm Region — acela lăsa margini „dințate".
    }
}
