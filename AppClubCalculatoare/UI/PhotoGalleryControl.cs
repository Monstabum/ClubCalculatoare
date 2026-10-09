using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppClubCalculatoare.UI
{
    /// <summary>
    /// Argumente pentru evenimentele galeriei (poarta id-ul fotografiei).
    /// </summary>
    public class FotoEvenimentArgs : EventArgs
    {
        /// <summary>Id-ul fotografiei vizate de eveniment.</summary>
        public int IdFoto { get; private set; }

        /// <summary>Construieste argumentele pentru o fotografie.</summary>
        public FotoEvenimentArgs(int idFoto)
        {
            IdFoto = idFoto;
        }
    }

    /// <summary>
    /// Galerie foto pentru o echipament (de incorporat in FormEditare).
    /// Nu cunoaste BD-ul direct — lucreaza doar prin PhotoService.
    ///
    /// Layout: stanga (60%) fotografia selectata mare + descriere,
    /// dreapta (40%) grila de miniaturi cu drag-to-reorder.
    /// Miniaturile sunt pastrate intr-un cache (nu se recitesc de pe
    /// disc la fiecare redesenare) si se elibereaza la Dispose.
    /// </summary>
    public class PhotoGalleryControl : UserControl
    {
        // ── Evenimente spre exterior ──────────────────────────────

        /// <summary>Se ridica dupa schimbarea fotografiei principale.</summary>
        public event EventHandler<FotoEvenimentArgs> FotoPrincipalaSchimbata;

        /// <summary>Se ridica dupa fiecare fotografie adaugata.</summary>
        public event EventHandler<FotoEvenimentArgs> FotoAdaugata;

        /// <summary>Se ridica dupa stergerea unei fotografii.</summary>
        public event EventHandler<FotoEvenimentArgs> FotoStearsa;

        // ── Stare ─────────────────────────────────────────────────

        private int _idEchipament = -1;
        private List<FotoInfo> _fotografii = new List<FotoInfo>();
        private readonly Dictionary<int, Image> _cache = new Dictionary<int, Image>();
        private FotoInfo _selectat;
        private Image _placeholder;
        private FotoInfo _contextFoto;

        private Control _dragControl;
        private Point _dragStart;

        // ── Controale ─────────────────────────────────────────────

        private SplitContainer _split;
        private PictureBox _pozaMare;
        private Label _lblNume;
        private Label _lblMeta;
        private TextBox _txtDescriere;
        private MaterialButton _btnSalveazaDescriere;
        private FlowLayoutPanel _listaThumb;
        private Label _lblPlaceholder;
        private MaterialButton _btnAdauga;
        private MaterialButton _btnSterge;
        private ProgressBar _progress;
        private ContextMenuStrip _meniu;

                        
        public int IdEchipament => _idEchipament;

        /// <summary>Numarul de fotografii din galerie.</summary>
        public int Count => _fotografii.Count;

        /// <summary>
        /// Daca este true, butoanele de modificare sunt dezactivate
        /// (modul "Detalii" din FormEditare).
        /// </summary>
        public bool DoarCitire { get; set; }

        /// <summary>
        /// True daca descrierea afisata difera de cea salvata
        /// (FormEditare intreaba la inchidere).
        /// </summary>
        public bool AreModificariNesalvate
        {
            get
            {
                if (_selectat == null) return false;
                string curent = _txtDescriere.Text.Trim();
                string salvat = _selectat.Descriere ?? "";
                return !string.Equals(curent, salvat, StringComparison.Ordinal);
            }
        }

        /// <summary>Construieste galeria (goala pana la Incarca).</summary>
        public PhotoGalleryControl()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = MaterialTheme.Background;
            this.Font = MaterialTheme.FontBody;
            this.AllowDrop = true;
            ConstruiesteInterfata();
            AplicaDrepturi();
        }

        // ── Constructie interfata ─────────────────────────────────

        private void ConstruiesteInterfata()
        {
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 6,
                BackColor = MaterialTheme.Background
            };
            this.Controls.Add(_split);
            this.Resize += (s, e) =>
            {
                if (this.Width > 0)
                {
                    try { _split.SplitterDistance = this.Width * 60 / 100; }
                    catch { /* fereastra inca nemasurata */ }
                }
            };

            // ── Stanga: poza mare + info ──────────────────────────
            var panStanga = new Panel { Dock = DockStyle.Fill, BackColor = MaterialTheme.Surface };
            _split.Panel1.Controls.Add(panStanga);

            _pozaMare = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(0x21, 0x21, 0x21),
                SizeMode = PictureBoxSizeMode.Zoom,
                AllowDrop = true
            };
            _pozaMare.DoubleClick += (s, e) => DeschideViewer();
            panStanga.Controls.Add(_pozaMare);

            var panInfo = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 132,
                BackColor = MaterialTheme.Surface,
                Padding = new Padding(10, 6, 10, 6)
            };
            panStanga.Controls.Add(panInfo);

            _lblNume = new Label
            {
                Font = MaterialTheme.FontLabel,
                ForeColor = MaterialTheme.TextPrimary,
                AutoSize = false,
                Location = new Point(10, 6),
                Size = new Size(400, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            panInfo.Controls.Add(_lblNume);

            _lblMeta = new Label
            {
                Font = MaterialTheme.FontSmall,
                ForeColor = MaterialTheme.TextSecondary,
                AutoSize = false,
                Location = new Point(10, 26),
                Size = new Size(400, 18),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            panInfo.Controls.Add(_lblMeta);

            var lblDescriereHint = new Label
            {
                Text = "Descriere (opțional, fără link):",
                Font = MaterialTheme.FontSmall,
                ForeColor = MaterialTheme.TextSecondary,
                AutoSize = false,
                Location = new Point(10, 46),
                Size = new Size(400, 14),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            panInfo.Controls.Add(lblDescriereHint);

            _txtDescriere = new TextBox
            {
                Location = new Point(10, 62),
                Size = new Size(400, 36),
                Multiline = true,
                MaxLength = 300,
                Font = MaterialTheme.FontBody,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AcceptsReturn = true,
                AcceptsTab = false,
                ScrollBars = ScrollBars.Vertical
            };
            // FIX: Enter in descriere nu trebuie sa declanseze Form.AcceptButton (Salvează echipamentul)
            // care valideaza Denumire/Oras/Putere/Tarif si afiseaza "Date invalide" peste galerie.
            // Descrierea se salveaza DOAR prin butonul "Salvează descrierea" si e optionala (fara link).
            _txtDescriere.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && e.Modifiers == Keys.None)
                {
                    // Enter simplu = salveaza descrierea, nu bubuie spre Form (evita validarea echipamentei)
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    SalveazaDescriereaCurenta();
                }
            };
            panInfo.Controls.Add(_txtDescriere);

            _btnSalveazaDescriere = MaterialUIHelper.Button(
                "Salvează descrierea", MaterialButtonStyle.Primary, 170, 30);
            _btnSalveazaDescriere.Location = new Point(10, 102);
            _btnSalveazaDescriere.Click += (s, e) => SalveazaDescriereaCurenta();
            panInfo.Controls.Add(_btnSalveazaDescriere);

            // ── Dreapta: toolbar + grila miniaturi ────────────────
            var panDreapta = new Panel { Dock = DockStyle.Fill, BackColor = MaterialTheme.Background };
            _split.Panel2.Controls.Add(panDreapta);

            var toolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = MaterialTheme.Background,
                Padding = new Padding(6)
            };
            panDreapta.Controls.Add(toolbar);

            _btnAdauga = MaterialUIHelper.Button("＋  Adaugă", MaterialButtonStyle.Success, 110, 34);
            _btnAdauga.Location = new Point(6, 7);
            _btnAdauga.Click += (s, e) => DeschideDialogAdaugare();
            toolbar.Controls.Add(_btnAdauga);

            _btnSterge = MaterialUIHelper.Button("Șterge", MaterialButtonStyle.Danger, 90, 34);
            _btnSterge.Location = new Point(122, 7);
            _btnSterge.Click += (s, e) => StergeSelectata();
            toolbar.Controls.Add(_btnSterge);

            _progress = new ProgressBar
            {
                Location = new Point(218, 12),
                Size = new Size(90, 24),
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            toolbar.Controls.Add(_progress);

            _listaThumb = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = true,
                BackColor = MaterialTheme.Background,
                Padding = new Padding(6),
                AllowDrop = true
            };
            _listaThumb.DragOver += Lista_DragOver;
            _listaThumb.DragDrop += Lista_DragDrop;
            panDreapta.Controls.Add(_listaThumb);

            _lblPlaceholder = new Label
            {
                Text = "Trage pozele aici sau click pentru a adăuga",
                Font = MaterialTheme.FontSubtitle,
                ForeColor = MaterialTheme.TextSecondary,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Cursor = Cursors.Hand,
                Visible = false
            };
            _lblPlaceholder.Click += (s, e) => DeschideDialogAdaugare();
            panDreapta.Controls.Add(_lblPlaceholder);
            _lblPlaceholder.BringToFront();

            // ── Meniu contextual pe miniaturi ─────────────────────
            _meniu = new ContextMenuStrip();
            _meniu.Items.Add("★  Fă principală", null, (s, e) => FaPrincipalaContext());
            _meniu.Items.Add("Editează descrierea", null, (s, e) => _txtDescriere.Focus());
            _meniu.Items.Add("Rotire 90°", null, (s, e) => RotesteContext());
            _meniu.Items.Add(new ToolStripSeparator());
            _meniu.Items.Add("Șterge", null, (s, e) => StergeContext());
            _meniu.Opening += (s, e) =>
            {
                bool editabil = !DoarCitire && PhotoService.AreDreptModificareFoto();
                foreach (ToolStripItem item in _meniu.Items)
                    item.Enabled = editabil;
            };
        }

        private void AplicaDrepturi()
        {
            bool editabil = !DoarCitire && PhotoService.AreDreptModificareFoto();
            if (_btnAdauga != null) _btnAdauga.Enabled = editabil;
            if (_btnSterge != null) _btnSterge.Enabled = editabil;
            if (_btnSalveazaDescriere != null) _btnSalveazaDescriere.Enabled = editabil;
            if (_txtDescriere != null) _txtDescriere.ReadOnly = !editabil;
        }

        // ── Incarcare ─────────────────────────────────────────────

        /// <summary>Incarca galeria pentru o echipament.</summary>
        public void Incarca(int idEchipament)
        {
            _idEchipament = idEchipament;
            Reincarca();
            AplicaDrepturi();
        }

        private void Reincarca()
        {
            int selectatAnterior = _selectat != null ? _selectat.IdFoto : -1;
            ElibereazaCache();

            if (_idEchipament > 0)
                _fotografii = PhotoService.GetFotos(_idEchipament);
            else
                _fotografii = new List<FotoInfo>();

            ReconstruiesteMiniaturi();

            _selectat = null;
            for (int i = 0; i < _fotografii.Count; i++)
            {
                if (_fotografii[i].IdFoto == selectatAnterior)
                    _selectat = _fotografii[i];
            }
            if (_selectat == null && _fotografii.Count > 0)
                _selectat = _fotografii[0];
            AfiseazaSelectia();
        }

        private void ReconstruiesteMiniaturi()
        {
            _listaThumb.SuspendLayout();
            _listaThumb.Controls.Clear();

            for (int i = 0; i < _fotografii.Count; i++)
                _listaThumb.Controls.Add(CreeazaPanouMiniatura(_fotografii[i]));

            _listaThumb.ResumeLayout(false);
            _lblPlaceholder.Visible = _fotografii.Count == 0;
            EvidentiazaSelectia();
        }

        private Panel CreeazaPanouMiniatura(FotoInfo foto)
        {
            var panou = new Panel
            {
                Size = new Size(104, 128),
                Margin = new Padding(4),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Tag = foto,
                Cursor = Cursors.Hand
            };

            var poza = new PictureBox
            {
                Location = new Point(4, 4),
                Size = new Size(96, 96),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White,
                Tag = foto,
                Cursor = Cursors.Hand
            };
            poza.Image = ImagineThumb(foto);
            poza.Click += (s, e) => Selecteaza(foto);
            poza.DoubleClick += (s, e) => DeschideViewerLa(foto);
            poza.MouseDown += Thumb_MouseDown;
            poza.MouseMove += Thumb_MouseMove;
            panou.Controls.Add(poza);

            var lblNume = new Label
            {
                Location = new Point(4, 102),
                Size = new Size(96, 22),
                Font = MaterialTheme.FontSmall,
                ForeColor = MaterialTheme.TextSecondary,
                Text = foto.EstePrincipala ? "★ principală" : (foto.NumeOriginal ?? ""),
                TextAlign = ContentAlignment.MiddleCenter,
                Tag = foto,
                Cursor = Cursors.Hand
            };
            if (foto.EstePrincipala)
            {
                lblNume.ForeColor = Color.FromArgb(0xB4, 0x86, 0x00);
                lblNume.Font = new Font(MaterialTheme.FontFamily, 9f, FontStyle.Bold);
            }
            lblNume.Click += (s, e) => Selecteaza(foto);
            lblNume.DoubleClick += (s, e) => DeschideViewerLa(foto);
            lblNume.MouseDown += Thumb_MouseDown;
            lblNume.MouseMove += Thumb_MouseMove;
            panou.Controls.Add(lblNume);

            panou.Click += (s, e) => Selecteaza(foto);
            panou.DoubleClick += (s, e) => DeschideViewerLa(foto);
            panou.MouseDown += Thumb_MouseDown;
            panou.MouseMove += Thumb_MouseMove;
            panou.ContextMenuStrip = _meniu;
            poza.ContextMenuStrip = _meniu;
            lblNume.ContextMenuStrip = _meniu;

            return panou;
        }

        private Image ImagineThumb(FotoInfo foto)
        {
            Image gasita;
            if (_cache.TryGetValue(foto.IdFoto, out gasita))
                return gasita;
            try
            {
                if (File.Exists(foto.CaleThumb))
                {
                    using (FileStream fs = new FileStream(
                        foto.CaleThumb, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (Image tmp = Image.FromStream(fs))
                    {
                        Image copie = new Bitmap(tmp);
                        _cache[foto.IdFoto] = copie;
                        return copie;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Nu s-a putut incarca miniatura " + foto.CaleThumb +
                    ": " + ex.Message, "PhotoGalleryControl");
            }
            if (_placeholder == null)
                _placeholder = new Bitmap(96, 96);
            return _placeholder;
        }

        // ── Selectie si afisare ───────────────────────────────────

        private void Selecteaza(FotoInfo foto)
        {
            _selectat = foto;
            AfiseazaSelectia();
        }

        private void AfiseazaSelectia()
        {
            ElibereazaPozaMare();

            if (_selectat == null)
            {
                _lblNume.Text = _fotografii.Count == 0 ? "Nicio fotografie" : "";
                _lblMeta.Text = "";
                _txtDescriere.Text = "";
                EvidentiazaSelectia();
                return;
            }

            string cale = File.Exists(_selectat.CaleMedium)
                ? _selectat.CaleMedium : _selectat.CaleOriginal;
            try
            {
                if (File.Exists(cale))
                {
                    using (FileStream fs = new FileStream(
                        cale, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (Image tmp = Image.FromStream(fs))
                        _pozaMare.Image = new Bitmap(tmp);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Nu s-a putut afisa imaginea " + cale +
                    ": " + ex.Message, "PhotoGalleryControl");
            }

            _lblNume.Text = (_selectat.EstePrincipala ? "★ " : "") +
                (_selectat.NumeOriginal ?? _selectat.NumeFisier);
            _lblMeta.Text = string.Format("{0}×{1} px · {2} KB · {3:dd.MM.yyyy}",
                _selectat.Latime, _selectat.Inaltime,
                _selectat.DimensiuneKb, _selectat.DataAdaugare);
            _txtDescriere.Text = _selectat.Descriere ?? "";
            EvidentiazaSelectia();
        }

        private void EvidentiazaSelectia()
        {
            foreach (Control c in _listaThumb.Controls)
            {
                FotoInfo foto = c.Tag as FotoInfo;
                c.BackColor = (foto != null && _selectat != null && foto.IdFoto == _selectat.IdFoto)
                    ? Color.FromArgb(0xE8, 0xEA, 0xF6)
                    : Color.White;
            }
        }

        private void DeschideViewer()
        {
            if (_selectat != null) DeschideViewerLa(_selectat);
        }

        private void DeschideViewerLa(FotoInfo foto)
        {
            Selecteaza(foto);
            int index = _fotografii.IndexOf(foto);
            using (var frm = new PhotoViewerForm(_fotografii, index))
                frm.ShowDialog(this);
        }

        // ── Adaugare ──────────────────────────────────────────────

        private void DeschideDialogAdaugare()
        {
            if (_idEchipament <= 0) return;
            if (!PhotoService.AreDreptModificareFoto())
            {
                PermissionService.AfiseazaMesajAcces();
                return;
            }
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Adaugă fotografii";
                dlg.Filter = "Imagini|*.jpg;*.jpeg;*.png;*.bmp;*.webp|Toate fișierele|*.*";
                dlg.Multiselect = true;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                AdaugaFisiere(dlg.FileNames);
            }
        }

        private async void AdaugaFisiere(string[] cai)
        {
            if (cai == null || cai.Length == 0) return;
            _progress.Visible = true;
            _progress.Value = 0;
            var progress = new Progress<int>(p =>
            {
                if (p >= _progress.Minimum && p <= _progress.Maximum)
                    _progress.Value = p;
            });
            try
            {
                int echipament = _idEchipament;
                List<int> iduri = await Task.Run(
                    () => PhotoService.AdaugaFotos(echipament, cai, progress));
                Reincarca();
                for (int i = 0; i < iduri.Count; i++)
                {
                    if (FotoAdaugata != null)
                        FotoAdaugata(this, new FotoEvenimentArgs(iduri[i]));
                }
                if (iduri.Count < cai.Length)
                    MessageBox.Show(
                        string.Format("Au fost adăugate {0} din {1} fișiere.\n" +
                            "Verifică jurnalul pentru detalii despre fișierele respinse.",
                            iduri.Count, cai.Length),
                        "Adăugare parțială", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _progress.Visible = false;
            }
        }

        // ── Stergere / principala / descriere / rotire ────────────

        private void StergeSelectata()
        {
            if (_selectat == null) return;
            if (!PhotoService.AreDreptModificareFoto())
            {
                PermissionService.AfiseazaMesajAcces();
                return;
            }
            string nume = _selectat.NumeOriginal ?? _selectat.NumeFisier;
            if (MessageBox.Show(string.Format("Ștergi fotografia '{0}'?", nume),
                    "Confirmare ștergere", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes) return;

            int id = _selectat.IdFoto;
            if (!PhotoService.StergeFoto(id))
            {
                MessageBox.Show("Ștergerea a eșuat. Verifică jurnalul.",
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            _selectat = null;
            Reincarca();
            if (FotoStearsa != null)
                FotoStearsa(this, new FotoEvenimentArgs(id));
        }

        private void FaPrincipalaContext()
        {
            FotoInfo tinta = _contextFoto ?? _selectat;
            if (tinta == null) return;
            if (!PhotoService.SeteazaPrincipala(tinta.IdFoto))
            {
                MessageBox.Show("Operația a eșuat. Verifică jurnalul.",
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Reincarca();
            if (FotoPrincipalaSchimbata != null)
                FotoPrincipalaSchimbata(this, new FotoEvenimentArgs(tinta.IdFoto));
        }

        private void StergeContext()
        {
            if (_contextFoto != null) Selecteaza(_contextFoto);
            StergeSelectata();
        }

        private void RotesteContext()
        {
            FotoInfo tinta = _contextFoto ?? _selectat;
            if (tinta == null) return;
            if (!PhotoService.RotesteFoto90(tinta.IdFoto))
            {
                MessageBox.Show("Rotirea a eșuat. Verifică jurnalul.",
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Reincarca();
        }

        /// <summary>Salveaza descrierea editata pentru fotografia selectata.</summary>
        public bool SalveazaDescriereaCurenta()
        {
            if (_selectat == null) return true;
            if (!AreModificariNesalvate) return true;
            string text = _txtDescriere.Text.Trim();
            if (!PhotoService.ActualizeazaDescriere(_selectat.IdFoto, text))
            {
                MessageBox.Show("Salvarea descrierii a eșuat.",
                    "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            _selectat.Descriere = text;
            return true;
        }

        // ── Drag & drop (reordonare + fisiere) ────────────────────

        private Control PanouRadacina(Control c)
        {
            Control curent = c;
            while (curent != null && curent.Parent != null && curent.Parent != _listaThumb)
                curent = curent.Parent;
            return curent;
        }

        private void Thumb_MouseDown(object sender, MouseEventArgs e)
        {
            Control radacina = PanouRadacina(sender as Control);
            if (radacina == null) return;
            // Retine tinta pentru meniul contextual (click-dreapta).
            _contextFoto = radacina.Tag as FotoInfo;
            if (e.Button != MouseButtons.Left) return;
            _dragControl = radacina;
            _dragStart = e.Location;
        }

        private void Thumb_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragControl == null || e.Button != MouseButtons.Left) return;
            int dx = Math.Abs(e.X - _dragStart.X);
            int dy = Math.Abs(e.Y - _dragStart.Y);
            if (dx < SystemInformation.DragSize.Width && dy < SystemInformation.DragSize.Height)
                return;
            Control deTras = _dragControl;
            _dragControl = null;
            _listaThumb.DoDragDrop(deTras, DragDropEffects.Move);
        }

        private void Lista_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(Panel)))
                e.Effect = DragDropEffects.Move;
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void Lista_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(Panel)))
            {
                Panel tras = e.Data.GetData(typeof(Panel)) as Panel;
                if (tras == null || !_listaThumb.Controls.Contains(tras)) return;
                Point punct = _listaThumb.PointToClient(new Point(e.X, e.Y));
                Control destinatie = _listaThumb.GetChildAtPoint(punct);
                Control tinta = destinatie != null ? PanouRadacina(destinatie) : null;
                int indexNou = tinta != null
                    ? _listaThumb.Controls.GetChildIndex(tinta)
                    : _listaThumb.Controls.Count - 1;
                _listaThumb.Controls.SetChildIndex(tras, indexNou);
                PersistaOrdinea();
                return;
            }
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] fisiere = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (fisiere != null && fisiere.Length > 0)
                {
                    if (!PhotoService.AreDreptModificareFoto())
                    {
                        PermissionService.AfiseazaMesajAcces();
                        return;
                    }
                    AdaugaFisiere(fisiere);
                }
            }
        }

        private void PersistaOrdinea()
        {
            List<int> iduri = new List<int>();
            // Ordinea vizuala in FlowLayoutPanel urmeaza indicii
            // copiilor: Controls[0] este prima miniatura afisata.
            for (int i = 0; i < _listaThumb.Controls.Count; i++)
            {
                FotoInfo foto = _listaThumb.Controls[i].Tag as FotoInfo;
                if (foto != null) iduri.Add(foto.IdFoto);
            }
            if (!PhotoService.Reordoneaza(_idEchipament, iduri))
            {
                Reincarca();
                return;
            }
            // Sincronizeaza lista locala cu ordinea vizuala
            Dictionary<int, FotoInfo> mapa = new Dictionary<int, FotoInfo>();
            for (int i = 0; i < _fotografii.Count; i++)
                mapa[_fotografii[i].IdFoto] = _fotografii[i];
            List<FotoInfo> reordonata = new List<FotoInfo>();
            for (int i = 0; i < iduri.Count; i++)
            {
                FotoInfo gasita;
                if (mapa.TryGetValue(iduri[i], out gasita))
                    reordonata.Add(gasita);
            }
            _fotografii = reordonata;
            EvidentiazaSelectia();
        }

        // ── Eliberare resurse ─────────────────────────────────────

        private void ElibereazaCache()
        {
            foreach (Image img in _cache.Values)
            {
                if (img != null && img != _placeholder)
                    img.Dispose();
            }
            _cache.Clear();
        }

        private void ElibereazaPozaMare()
        {
            if (_pozaMare != null && _pozaMare.Image != null)
            {
                Image veche = _pozaMare.Image;
                _pozaMare.Image = null;
                veche.Dispose();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ElibereazaCache();
                ElibereazaPozaMare();
                if (_placeholder != null)
                {
                    _placeholder.Dispose();
                    _placeholder = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
