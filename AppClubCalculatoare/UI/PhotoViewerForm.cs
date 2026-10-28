using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AppClubCalculatoare.UI;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Fereastra de vizualizare a unei fotografii la dimensiune mare,
    /// cu navigare Anterior / Urmator prin galeria echipamentei.
    /// Doar citire — nu modifica BD-ul si nu sterge fisiere.
    /// </summary>
    public class PhotoViewerForm : Form
    {
        private readonly List<FotoInfo> _fotografii;
        private int _index;

        private PictureBox _imagine;
        private Label _lblContor;
        private Label _lblDescriere;
        private MaterialButton _btnAnterior;
        private MaterialButton _btnUrmator;

        /// <summary>
        /// Construieste viewer-ul pe o lista deja incarcata.
        /// </summary>
        /// <param name="fotografii">Fotografiile echipamentei (ordine galerie).</param>
        /// <param name="indexInitial">Indexul fotografiei de afisat.</param>
        public PhotoViewerForm(List<FotoInfo> fotografii, int indexInitial)
        {
            _fotografii = fotografii ?? new List<FotoInfo>();
            _index = Math.Max(0, Math.Min(indexInitial, _fotografii.Count - 1));

            this.Text = "Vizualizare fotografie";
            this.Size = new Size(900, 700);
            this.MinimumSize = new Size(640, 480);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = MaterialTheme.Background;
            this.Font = MaterialTheme.FontBody;

            InitializeComponent();

            // DPI: scalare uniformă (fereastra este DPI-aware) + titlu închis.
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode       = AutoScaleMode.Dpi;
            WindowChrome.EnableDarkTitleBar(this);

            AfiseazaCurenta();
        }

        private void InitializeComponent()
        {
            // ── Continut central (Dock.Fill) — adaugat PRIMUL ─────
            var content = MaterialUIHelper.ContentPanel();
            this.Controls.Add(content);

            var card = new MaterialCard { Dock = DockStyle.Fill };
            content.Controls.Add(card);

            _imagine = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.None
            };
            card.Controls.Add(_imagine);

            // ── Bara inferioara (Dock.Bottom): navigare + inchidere
            FlowLayoutPanel leftBottom, rightBottom;
            var bottomBar = MaterialUIHelper.SplitBottomBar(out leftBottom, out rightBottom);
            this.Controls.Add(bottomBar);

            _btnAnterior = MaterialUIHelper.Button("◀  Anterior", MaterialButtonStyle.Secondary, 130);
            _btnAnterior.Click += (s, e) => Navigheaza(-1);
            leftBottom.Controls.Add(_btnAnterior);

            _btnUrmator = MaterialUIHelper.Button("Următor  ▶", MaterialButtonStyle.Secondary, 130);
            _btnUrmator.Click += (s, e) => Navigheaza(1);
            leftBottom.Controls.Add(_btnUrmator);

            _lblContor = new Label
            {
                Font = MaterialTheme.FontLabel,
                ForeColor = MaterialTheme.TextSecondary,
                AutoSize = true,
                Margin = new Padding(12, 10, 0, 0)
            };
            leftBottom.Controls.Add(_lblContor);

            _lblDescriere = new Label
            {
                Font = MaterialTheme.FontSmall,
                ForeColor = MaterialTheme.TextSecondary,
                AutoSize = true,
                Margin = new Padding(12, 10, 0, 0)
            };
            leftBottom.Controls.Add(_lblDescriere);

            var btnInchide = MaterialUIHelper.Button("✕  Închide", MaterialButtonStyle.Outline, 120);
            btnInchide.Click += (s, e) => this.Close();
            rightBottom.Controls.Add(btnInchide);
            this.CancelButton = btnInchide;

            // ── Header colorat (Dock.Top) — adaugat ULTIMUL ───────
            var header = MaterialUIHelper.Header(
                "Fotografie", "Navighează cu Anterior / Următor",
                null, MaterialTheme.Primary);
            this.Controls.Add(header);

            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Left) Navigheaza(-1);
                else if (e.KeyCode == Keys.Right) Navigheaza(1);
                else if (e.KeyCode == Keys.Escape) this.Close();
            };
        }

        private void Navigheaza(int pas)
        {
            if (_fotografii.Count == 0) return;
            _index = (_index + pas + _fotografii.Count) % _fotografii.Count;
            AfiseazaCurenta();
        }

        private void AfiseazaCurenta()
        {
            ElibereazaImaginea();

            if (_fotografii.Count == 0 || _index < 0 || _index >= _fotografii.Count)
            {
                _lblContor.Text = "0 / 0";
                _lblDescriere.Text = "";
                return;
            }

            FotoInfo foto = _fotografii[_index];
            string cale = File.Exists(foto.CaleMedium) ? foto.CaleMedium : foto.CaleOriginal;
            try
            {
                if (File.Exists(cale))
                {
                    using (FileStream fs = new FileStream(cale, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (Image tmp = Image.FromStream(fs))
                        _imagine.Image = new Bitmap(tmp);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Nu s-a putut incarca imaginea " + cale + ": " + ex.Message,
                    "PhotoViewerForm");
            }

            string nume = foto.NumeOriginal ?? foto.NumeFisier;
            this.Text = string.Format("Fotografie — {0}", nume);
            _lblContor.Text = string.Format("{0} / {1}", _index + 1, _fotografii.Count);
            _lblDescriere.Text = string.IsNullOrEmpty(foto.Descriere)
                ? string.Format("{0} · {1}×{2}", nume, foto.Latime, foto.Inaltime)
                : string.Format("{0} · {1}×{2}", foto.Descriere, foto.Latime, foto.Inaltime);

            bool multiplu = _fotografii.Count > 1;
            _btnAnterior.Enabled = multiplu;
            _btnUrmator.Enabled = multiplu;
        }

        private void ElibereazaImaginea()
        {
            if (_imagine != null && _imagine.Image != null)
            {
                Image veche = _imagine.Image;
                _imagine.Image = null;
                veche.Dispose();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            ElibereazaImaginea();
            base.OnFormClosed(e);
        }
    }
}
