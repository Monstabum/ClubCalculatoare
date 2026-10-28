using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Generator de poze demo pentru echipamente (2 imagini / obiect:
    /// Exterior + Interior). Cu prioritate se folosesc
    /// fotografiile REALE din folderul PozeDemo (via ImportPozeFolder);
    /// fallback-ul deseneaza procedural cu System.Drawing (fara fisiere
    /// externe, fara internet) si se salveaza prin
    /// <see cref="PhotoService.AdaugaFoto"/> — deci beneficiaza de
    /// thumb / medium / audit / poza principala.
    ///
    /// Idempotent: echipamentele care au deja fotografii sunt omise,
    /// deci butonul "Poze demo" poate fi apasat repetat fara duplicate.
    /// Conventie (ca PhotoService): fara cod UI, fara throw spre UI;
    /// erorile se logheaza si se intorc prin mesajul de rezultat.
    /// </summary>
    public static class DemoPoze
    {
        private const int LatimeImagine = 1024;
        private const int InaltimeImagine = 768;

        /// <summary>True daca userul curent poate genera poze demo.</summary>
        public static bool AreDrept()
        {
            return PhotoService.AreDreptModificareFoto();
        }

        /// <summary>
        /// Id-urile echipamentelor care nu au nicio fotografie.
        /// Intoarce lista goala la eroare (cu Warn in log).
        /// </summary>
        public static List<int> EchipamenteFaraPoze()
        {
            List<int> rezultat = new List<int>();
            try
            {
                using (var conn = SqlDb.CreateConnection())
                {
                    conn.Open();
                    const string sql = @"
                        SELECT p.id_echipament
                        FROM Echipamente p
                        LEFT JOIN EchipamentFoto f
                            ON f.id_echipament = p.id_echipament
                        GROUP BY p.id_echipament
                        HAVING COUNT(f.id_foto) = 0
                        ORDER BY p.id_echipament";
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            rezultat.Add(reader.GetInt32(0));
                    }
                }
            }
            catch (SqlException ex) when (ex.Message.Contains("EchipamentFoto"))
            {
                // Baza fără migrare — toate echipamentele sunt "goale".
                AppLogger.Warn(
                    "Tabela EchipamentFoto lipsește — rulează BD\\MigrareFoto.sql. " +
                    "Se considera toate echipamentele fara poze.",
                    "DemoPoze");
                rezultat = ToateEchipamentele();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "DemoPoze.EchipamenteFaraPoze");
            }
            return rezultat;
        }

        /// <summary>
        /// Genereaza 2 poze demo (Exterior + Interior) pentru fiecare
        /// echipament fara fotografii. Intoarce numarul de fisiere
        /// adaugate cu succes; mesajul descrie rezultatul pentru UI.
        /// </summary>
        public static int GenereazaPozeDemo(out string mesaj)
        {
            mesaj = "";
            if (!AreDrept())
            {
                AppLogger.Security(
                    "Acces blocat in DemoPoze: '" + SessionInfo.Username +
                    "' -> generare poze demo");
                mesaj = "Nu ai dreptul de modificare a fotografiilor.";
                return 0;
            }

            List<EchipamentScurta> goale = EchipamenteGoaleCuDetalii();
            if (goale == null)
            {
                mesaj = "Tabela EchipamentFoto lipsește — rulează mai întâi BD\\MigrareFoto.sql.";
                return 0;
            }
            if (goale.Count == 0)
            {
                mesaj = "Toate echipamentele au deja fotografii — nimic de generat.";
                return 0;
            }

            PhotoService.EnsureFolders();

            // POZE REALE: daca folderul PozeDemo exista cu
            // imagini reale, le folosim pe acestea in locul desenelor
            // procedurale. Fallback-ul procedural de mai jos ramane
            // pentru cazul in care folderul lipsește / e gol.
            int adaugateReale;
            int esuateReale;
            string mesajReal;
            if (IncearcaPozeReale(goale, out adaugateReale, out esuateReale,
                out mesajReal))
            {
                mesaj = mesajReal;
                AppLogger.Info(string.Format(
                    "Poze demo (reale): {0} adaugate, {1} esuate, {2} echipamente.",
                    adaugateReale, esuateReale, goale.Count), "DemoPoze");
                return adaugateReale;
            }

            int adaugate = 0;
            int esuate = 0;
            for (int i = 0; i < goale.Count; i++)
            {
                EchipamentScurta p = goale[i];
                string[] vederi = { "Exterior", "Interior" };
                for (int v = 0; v < vederi.Length; v++)
                {
                    string caleTemp = null;
                    Bitmap desen = null;
                    try
                    {
                        desen = DeseneazaImagine(
                            p.Tip, p.Oras, p.Denumire, p.Id, vederi[v]);
                        caleTemp = CaleTemporara();
                        SalveazaJpeg(desen, caleTemp, 90);
                        string descriere = vederi[v] + " (demo) — " + p.Tip +
                            ", " + p.Oras;
                        int idFoto = PhotoService.AdaugaFoto(
                            p.Id, caleTemp, descriere);
                        if (idFoto > 0) adaugate++;
                        else esuate++;
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error(ex, "DemoPoze.GenereazaPozeDemo");
                        esuate++;
                    }
                    finally
                    {
                        if (desen != null) desen.Dispose();
                        StergeSilentios(caleTemp);
                    }
                }
            }

            AppLogger.Info(string.Format(
                "Poze demo: {0} adaugate, {1} esuate, {2} echipamente.",
                adaugate, esuate, goale.Count), "DemoPoze");
            if (esuate == 0)
                mesaj = string.Format(
                    "Au fost generate {0} poze demo pentru {1} echipamente.",
                    adaugate, goale.Count);
            else
                mesaj = string.Format(
                    "Au fost generate {0} poze demo ({1} au eșuat — vezi app.log).",
                    adaugate, esuate);
            return adaugate;
        }

        // -- Poze reale din folderul PozeDemo ---------------------

        /// <summary>
        /// Incearca sa adauge poze REALE (vedere exterioara + interior) din
        /// folderul PozeDemo pentru echipamentele goale. Intoarce true
        /// daca folderul contine imagini importabile (chiar daca unele
        /// au eșuat) — apelantul nu mai deseneaza procedural.
        /// False = folder lipsa/gol sau fara corespondenta > fallback
        /// la desenele procedurale.
        /// </summary>
        private static bool IncearcaPozeReale(List<EchipamentScurta> goale,
            out int adaugate, out int esuate, out string mesaj)
        {
            adaugate = 0;
            esuate = 0;
            mesaj = "";
            try
            {
                if (goale == null || goale.Count == 0)
                    return false;
                string folder = ImportPozeFolder.GasesteFolder();
                if (string.IsNullOrEmpty(folder))
                    return false;
                Dictionary<int, List<string>> plan =
                    ImportPozeFolder.ConstruiestePlan(folder);
                if (plan == null || plan.Count == 0)
                    return false;

                // Doar echipamentele goale primesc poze (idempotent).
                for (int i = 0; i < goale.Count; i++)
                {
                    EchipamentScurta p = goale[i];
                    List<string> fisiere;
                    if (!plan.TryGetValue(p.Id, out fisiere) ||
                        fisiere == null || fisiere.Count == 0)
                        continue;
                    // Preferam 1 exterior + 1 interior dupa nume;
                    // altfel primele 2 fisiere din plan.
                    List<string> alese = AlegeExteriorInterior(fisiere);
                    for (int f = 0; f < alese.Count; f++)
                    {
                        try
                        {
                            string vedere = alese[f].IndexOf("interior",
                                StringComparison.OrdinalIgnoreCase) >= 0
                                ? "Interior" : "Exterior";
                            string descriere = vedere + " (demo) — " +
                                p.Tip + ", " + p.Oras;
                            int idFoto = PhotoService.AdaugaFoto(
                                p.Id, alese[f], descriere);
                            if (idFoto > 0) adaugate++;
                            else esuate++;
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Error(ex,
                                "DemoPoze.IncearcaPozeReale");
                            esuate++;
                        }
                    }
                }

                if (adaugate == 0 && esuate == 0)
                    return false; // nicio corespondenta > procedural
                if (esuate == 0)
                    mesaj = string.Format(
                        "Au fost adaugate {0} poze demo reale (din PozeDemo) " +
                        "pentru {1} echipamente.", adaugate, goale.Count);
                else
                    mesaj = string.Format(
                        "Au fost adaugate {0} poze demo reale ({1} au eșuat — " +
                        "vezi app.log).", adaugate, esuate);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "DemoPoze.IncearcaPozeReale");
                return false;
            }
        }

        /// <summary>
        /// Alege max 2 fisiere: prefera unul cu "exterior" si unul cu
        /// "interior" in nume (ex. Echip01_Exterior.jpg), altfel primele 2.
        /// </summary>
        private static List<string> AlegeExteriorInterior(List<string> fisiere)
        {
            List<string> rezultat = new List<string>(2);
            string ext = null;
            string inter = null;
            for (int i = 0; i < fisiere.Count; i++)
            {
                string nume = fisiere[i] ?? "";
                if (inter == null && nume.IndexOf("interior",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                    inter = fisiere[i];
                else if (ext == null && nume.IndexOf("exterior",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                    ext = fisiere[i];
            }
            if (ext != null) rezultat.Add(ext);
            if (inter != null && inter != ext) rezultat.Add(inter);
            for (int i = 0; i < fisiere.Count && rezultat.Count < 2; i++)
                if (!rezultat.Contains(fisiere[i]))
                    rezultat.Add(fisiere[i]);
            return rezultat;
        }

        // -- Citire echipamente ------------------------------------

        private class EchipamentScurta
        {
            public int Id;
            public string Tip;
            public string Oras;
            public string Denumire;
        }

        /// <summary>
        /// Echipamentele fara poze + detalii pentru desen.
        /// Null daca tabela EchipamentFoto lipsește (mesaj spre SSMS).
        /// </summary>
        private static List<EchipamentScurta> EchipamenteGoaleCuDetalii()
        {
            try
            {
                List<EchipamentScurta> rezultat = new List<EchipamentScurta>();
                using (var conn = SqlDb.CreateConnection())
                {
                    conn.Open();
                    const string sql = @"
                        SELECT p.id_echipament, p.tip_echipament, p.oras, p.denumire
                        FROM Echipamente p
                        LEFT JOIN EchipamentFoto f
                            ON f.id_echipament = p.id_echipament
                        GROUP BY p.id_echipament, p.tip_echipament, p.oras, p.denumire
                        HAVING COUNT(f.id_foto) = 0
                        ORDER BY p.id_echipament";
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            EchipamentScurta p = new EchipamentScurta();
                            p.Id = reader.GetInt32(0);
                            p.Tip = reader.IsDBNull(1) ? "Echipament" :
                                reader.GetString(1);
                            p.Oras = reader.IsDBNull(2) ? "" : reader.GetString(2);
                            p.Denumire = reader.IsDBNull(3) ? "" : reader.GetString(3);
                            rezultat.Add(p);
                        }
                    }
                }
                return rezultat;
            }
            catch (SqlException ex) when (ex.Message.Contains("EchipamentFoto"))
            {
                AppLogger.Warn(
                    "Tabela EchipamentFoto lipsește — rulează BD\\MigrareFoto.sql.",
                    "DemoPoze");
                return null;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "DemoPoze.EchipamenteGoaleCuDetalii");
                return new List<EchipamentScurta>();
            }
        }

        private static List<int> ToateEchipamentele()
        {
            List<int> rezultat = new List<int>();
            try
            {
                using (var conn = SqlDb.CreateConnection())
                {
                    const string sql = "SELECT id_echipament FROM Echipamente ORDER BY id_echipament";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        conn.Open();
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                rezultat.Add(reader.GetInt32(0));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "DemoPoze.ToateEchipamentele");
            }
            return rezultat;
        }

        // -- Desen procedural --------------------------------------

        /// <summary>
        /// Deseneaza o imagine demo 1024x768. Apelantul elibereaza Bitmap-ul.
        /// </summary>
        internal static Bitmap DeseneazaImagine(
            string tip, string oras, string adresa, int id, string vedere)
        {
            Bitmap bmp = new Bitmap(LatimeImagine, InaltimeImagine);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                Paleta paleta = PaletaPentru(tip, vedere);
                if (vedere == "Interior")
                {
                    using (var perete = new SolidBrush(paleta.CerJos))
                        g.FillRectangle(perete, 0, 0,
                            LatimeImagine, InaltimeImagine);
                    DeseneazaInterior(g, paleta, tip);
                }
                else
                {
                    DeseneazaFundal(g, paleta);
                    DeseneazaSoare(g);
                    DeseneazaExterior(g, paleta, tip);
                }
                DeseneazaBandaTitlu(g, tip, oras, adresa, id, vedere);
            }
            return bmp;
        }

        private struct Paleta
        {
            public Color CerSus;
            public Color CerJos;
            public Color Sol;
            public Color Cladire;
            public Color Acoperis;
            public Color Accent;
        }

        private static Paleta PaletaPentru(string tip, string vedere)
        {
            Paleta p = new Paleta();
            if (vedere == "Interior")
            {
                p.CerSus = Color.FromArgb(0xEF, 0xE6, 0xD8);
                p.CerJos = Color.FromArgb(0xE0, 0xD0, 0xB8);
                p.Sol = Color.FromArgb(0x8D, 0x6E, 0x63);
                p.Cladire = Color.FromArgb(0xFF, 0xF8, 0xE7);
                p.Acoperis = Color.FromArgb(0xA1, 0x88, 0x7F);
                p.Accent = Color.FromArgb(0x2E, 0x7D, 0x32);
                return p;
            }
            string t = (tip ?? "").Trim();
            // Tipurile clubului refolosesc desenele vechi:
            // PC Gaming=bloc (implicit), Consola=casa, Server=teren,
            // Simulator=magazin, VR=sala.
            if (t == "Consola") t = "Casa";
            else if (t == "Server") t = "Teren";
            else if (t == "Simulator") t = "Spatiu comercial";
            else if (t == "VR") t = "Birou";
            if (t == "Casa")
            {
                p.CerSus = Color.FromArgb(0x4F, 0xC3, 0xF7);
                p.CerJos = Color.FromArgb(0xDC, 0xF5, 0xFF);
                p.Sol = Color.FromArgb(0x7C, 0xB3, 0x42);
                p.Cladire = Color.FromArgb(0xFF, 0xF3, 0xE0);
                p.Acoperis = Color.FromArgb(0xBF, 0x36, 0x0C);
                p.Accent = Color.FromArgb(0x2E, 0x7D, 0x32);
            }
            else if (t == "Teren")
            {
                p.CerSus = Color.FromArgb(0x29, 0xB6, 0xF6);
                p.CerJos = Color.FromArgb(0xE1, 0xF5, 0xFE);
                p.Sol = Color.FromArgb(0x66, 0xBB, 0x6A);
                p.Cladire = Color.FromArgb(0xA5, 0xD6, 0xA7);
                p.Acoperis = Color.FromArgb(0x1B, 0x5E, 0x20);
                p.Accent = Color.FromArgb(0x5D, 0x40, 0x37);
            }
            else if (t == "Spatiu comercial")
            {
                p.CerSus = Color.FromArgb(0x5C, 0x6B, 0xC0);
                p.CerJos = Color.FromArgb(0xE8, 0xEA, 0xF6);
                p.Sol = Color.FromArgb(0x90, 0xA4, 0xAE);
                p.Cladire = Color.FromArgb(0xFF, 0xEC, 0xB3);
                p.Acoperis = Color.FromArgb(0xFF, 0x6F, 0x00);
                p.Accent = Color.FromArgb(0x0D, 0x47, 0xA1);
            }
            else if (t == "Birou")
            {
                p.CerSus = Color.FromArgb(0x37, 0x47, 0x4F);
                p.CerJos = Color.FromArgb(0xCF, 0xD8, 0xDC);
                p.Sol = Color.FromArgb(0x60, 0x7D, 0x8B);
                p.Cladire = Color.FromArgb(0x90, 0xCA, 0xF9);
                p.Acoperis = Color.FromArgb(0x0D, 0x47, 0xA1);
                p.Accent = Color.FromArgb(0xFF, 0xC1, 0x07);
            }
            else // PC Gaming + implicit
            {
                p.CerSus = Color.FromArgb(0x42, 0xA5, 0xF5);
                p.CerJos = Color.FromArgb(0xE3, 0xF2, 0xFD);
                p.Sol = Color.FromArgb(0x9E, 0x9E, 0x9E);
                p.Cladire = Color.FromArgb(0xFA, 0xFA, 0xFA);
                p.Acoperis = Color.FromArgb(0x3F, 0x51, 0xB5);
                p.Accent = Color.FromArgb(0xFF, 0x8F, 0x00);
            }
            return p;
        }

        private static void DeseneazaFundal(Graphics g, Paleta p)
        {
            using (var pensula = new LinearGradientBrush(
                new Rectangle(0, 0, LatimeImagine, InaltimeImagine),
                p.CerSus, p.CerJos, LinearGradientMode.Vertical))
            {
                g.FillRectangle(pensula, 0, 0, LatimeImagine, InaltimeImagine);
            }
            using (var sol = new SolidBrush(p.Sol))
            {
                g.FillRectangle(sol, 0, InaltimeImagine - 190,
                    LatimeImagine, 190);
            }
            // Linie orizont discreta.
            using (var pen = new Pen(Color.FromArgb(90, 255, 255, 255), 2f))
            {
                g.DrawLine(pen, 0, InaltimeImagine - 190,
                    LatimeImagine, InaltimeImagine - 190);
            }
        }

        private static void DeseneazaSoare(Graphics g)
        {
            using (var sol = new SolidBrush(Color.FromArgb(0xFF, 0xF2, 0x8A)))
            using (var halo = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
            {
                g.FillEllipse(halo, 830, 30, 130, 130);
                g.FillEllipse(sol, 855, 55, 80, 80);
            }
            // Nori simpli.
            using (var nor = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
            {
                g.FillEllipse(nor, 120, 70, 130, 44);
                g.FillEllipse(nor, 170, 55, 110, 44);
                g.FillEllipse(nor, 620, 120, 120, 40);
                g.FillEllipse(nor, 665, 108, 95, 38);
            }
        }

        private static void DeseneazaExterior(Graphics g, Paleta p, string tip)
        {
            string t = (tip ?? "").Trim();
            // Tipurile clubului refolosesc desenele vechi:
            // PC Gaming=bloc (implicit), Consola=casa, Server=teren,
            // Simulator=magazin, VR=sala.
            if (t == "Consola") t = "Casa";
            else if (t == "Server") t = "Teren";
            else if (t == "Simulator") t = "Spatiu comercial";
            else if (t == "VR") t = "Birou";
            if (t == "Casa")
                DeseneazaCasa(g, p, 312, 250, 400, 330);
            else if (t == "Teren")
                DeseneazaTeren(g, p);
            else if (t == "Spatiu comercial")
                DeseneazaMagazin(g, p, 232, 260, 560, 320);
            else if (t == "Birou")
                DeseneazaSala(g, p, 352, 130, 320, 450);
            else
                DeseneazaBloc(g, p, 292, 150, 440, 430);
            DeseneazaCopaci(g, p);
        }

        private static void DeseneazaBloc(Graphics g, Paleta p, int x, int y, int w, int h)
        {
            using (var corp = new SolidBrush(p.Cladire))
            using (var margine = new Pen(p.Acoperis, 5f))
            using (var geam = new SolidBrush(Color.FromArgb(0xBB, 0xDE, 0xFB)))
            using (var geamMargine = new Pen(Color.FromArgb(0x90, 0xA4, 0xAE), 2f))
            using (var usa = new SolidBrush(p.Accent))
            {
                g.FillRectangle(corp, x, y, w, h);
                g.DrawRectangle(margine, x, y, w, h);
                // Acoperis.
                // FIX: pensula eliberata prin using (era new SolidBrush fara dispose).
                using (var acoperis = new SolidBrush(p.Acoperis))
                    g.FillRectangle(acoperis, x - 12, y - 26, w + 24, 26);
                // Ferestre 4 x 3.
                int cols = 4, rows = 3;
                int mx = 28, my = 30;
                int gw = (w - mx * (cols + 1)) / cols;
                int gh = 62;
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        int fx = x + mx + c * (gw + mx);
                        int fy = y + 34 + r * (gh + my);
                        g.FillRectangle(geam, fx, fy, gw, gh);
                        g.DrawRectangle(geamMargine, fx, fy, gw, gh);
                        g.DrawLine(geamMargine, fx + gw / 2, fy, fx + gw / 2, fy + gh);
                    }
                // Usa.
                int uw = 90, uh = 110;
                g.FillRectangle(usa, x + w / 2 - uw / 2, y + h - uh, uw, uh);
            }
        }

        private static void DeseneazaCasa(Graphics g, Paleta p, int x, int y, int w, int h)
        {
            using (var corp = new SolidBrush(p.Cladire))
            using (var acoperis = new SolidBrush(p.Acoperis))
            using (var geam = new SolidBrush(Color.FromArgb(0xBB, 0xDE, 0xFB)))
            using (var usa = new SolidBrush(Color.FromArgb(0x5D, 0x40, 0x37)))
            {
                g.FillRectangle(corp, x, y + 110, w, h - 110);
                Point[] tri = {
                    new Point(x - 30, y + 115),
                    new Point(x + w / 2, y),
                    new Point(x + w + 30, y + 115)
                };
                g.FillPolygon(acoperis, tri);
                // Cos.
                // FIX: pensula + pen-uri eliberate prin using (erau new fara dispose).
                using (var cos = new SolidBrush(Color.FromArgb(0x6D, 0x4C, 0x41)))
                    g.FillRectangle(cos,
                        x + w - 110, y + 18, 34, 70);
                // Usa + ferestre.
                g.FillRectangle(usa, x + w / 2 - 45, y + h - 140, 90, 140);
                g.FillRectangle(geam, x + 40, y + 170, 90, 80);
                g.FillRectangle(geam, x + w - 130, y + 170, 90, 80);
                using (var chenarGeam = new Pen(Color.FromArgb(0x8D, 0x6E, 0x63), 3f))
                {
                    g.DrawRectangle(chenarGeam,
                        x + 40, y + 170, 90, 80);
                    g.DrawRectangle(chenarGeam,
                        x + w - 130, y + 170, 90, 80);
                }
                // Gard.
                using (var gard = new SolidBrush(Color.FromArgb(0x8D, 0x6E, 0x63)))
                {
                    for (int gx = x - 90; gx < x + w + 90; gx += 34)
                        g.FillRectangle(gard, gx, y + h - 60, 16, 60);
                    g.FillRectangle(gard, x - 100, y + h - 62, w + 200, 12);
                }
            }
        }

        private static void DeseneazaTeren(Graphics g, Paleta p)
        {
            // Gard perimetral + copaci + panou "Teren".
            using (var stalp = new SolidBrush(p.Accent))
            {
                for (int gx = 60; gx < LatimeImagine - 40; gx += 90)
                    g.FillRectangle(stalp, gx, InaltimeImagine - 330, 14, 150);
                g.FillRectangle(stalp, 50, InaltimeImagine - 320,
                    LatimeImagine - 100, 12);
                g.FillRectangle(stalp, 50, InaltimeImagine - 260,
                    LatimeImagine - 100, 12);
            }
            // Panou.
            using (var panou = new SolidBrush(Color.FromArgb(0xFF, 0xFF, 0xFF)))
            using (var chenar = new Pen(p.Accent, 4f))
            using (var font = new Font("Segoe UI", 30f, FontStyle.Bold))
            {
                g.FillRectangle(panou, 362, 300, 300, 120);
                g.DrawRectangle(chenar, 362, 300, 300, 120);
                // FIX: pensula eliberata prin using (era new SolidBrush fara dispose).
                using (var textPensula = new SolidBrush(p.Accent))
                    g.DrawString("TEREN", font, textPensula, 435, 330);
            }
        }

        private static void DeseneazaMagazin(Graphics g, Paleta p, int x, int y, int w, int h)
        {
            using (var corp = new SolidBrush(p.Cladire))
            using (var geam = new SolidBrush(Color.FromArgb(0xBB, 0xDE, 0xFB)))
            using (var usa = new SolidBrush(p.Accent))
            {
                g.FillRectangle(corp, x, y, w, h);
                // FIX: pen eliberat prin using (era new Pen fara dispose).
                using (var chenarMagazin = new Pen(p.Accent, 5f))
                    g.DrawRectangle(chenarMagazin, x, y, w, h);
                // Copertina cu dungi.
                int dungi = 10;
                int dw = w / dungi;
                for (int i = 0; i < dungi; i++)
                {
                    using (var dunga = new SolidBrush(
                        i % 2 == 0 ? p.Acoperis : Color.White))
                    {
                        g.FillRectangle(dunga, x + i * dw, y, dw, 54);
                    }
                }
                // FIX: pen eliberat prin using (era new Pen fara dispose).
                using (var chenarCopertina = new Pen(p.Accent, 3f))
                    g.DrawRectangle(chenarCopertina, x, y, w, 54);
                // Vitrine.
                g.FillRectangle(geam, x + 30, y + 110, 190, 130);
                g.FillRectangle(geam, x + w - 220, y + 110, 190, 130);
                g.FillRectangle(usa, x + w / 2 - 55, y + 110, 110, 210);
            }
        }

        private static void DeseneazaSala(Graphics g, Paleta p, int x, int y, int w, int h)
        {
            using (var corp = new SolidBrush(p.Cladire))
            using (var geam = new SolidBrush(Color.FromArgb(0x0D, 0x47, 0xA1)))
            {
                g.FillRectangle(corp, x, y, w, h);
                // FIX: pen eliberat prin using (era new Pen fara dispose).
                using (var chenarSala = new Pen(p.Acoperis, 5f))
                    g.DrawRectangle(chenarSala, x, y, w, h);
                int rows = 7, cols = 4;
                int mx = 22, my = 22;
                int gw = (w - mx * (cols + 1)) / cols;
                int gh = (h - 120 - my * (rows + 1)) / rows;
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                        g.FillRectangle(geam,
                            x + mx + c * (gw + mx),
                            y + 24 + r * (gh + my), gw, gh);
                // Intrare.
                using (var usa = new SolidBrush(p.Accent))
                {
                    g.FillRectangle(usa, x + w / 2 - 60, y + h - 90, 120, 90);
                }
            }
        }

        private static void DeseneazaCopaci(Graphics g, Paleta p)
        {
            DeseneazaCopac(g, 120, InaltimeImagine - 300, 1.0f);
            DeseneazaCopac(g, LatimeImagine - 180, InaltimeImagine - 320, 1.2f);
        }

        private static void DeseneazaCopac(Graphics g, int x, int y, float s)
        {
            using (var trunchi = new SolidBrush(Color.FromArgb(0x5D, 0x40, 0x37)))
            using (var coroana = new SolidBrush(Color.FromArgb(0x2E, 0x7D, 0x32)))
            {
                g.FillRectangle(trunchi, x, y + (int)(70 * s), (int)(22 * s), (int)(70 * s));
                g.FillEllipse(coroana, x - (int)(45 * s), y, (int)(112 * s), (int)(95 * s));
            }
        }

        private static void DeseneazaInterior(Graphics g, Paleta p, string tip)
        {
            // Perete in doua tonuri + podea.
            using (var perete = new SolidBrush(p.Cladire))
            using (var podea = new SolidBrush(p.Sol))
            using (var chenarPerete = new Pen(p.Acoperis, 4f))
            {
                g.FillRectangle(perete, 60, 130, LatimeImagine - 120, 330);
                g.FillRectangle(podea, 60, 460, LatimeImagine - 120, 200);
                // FIX: pen eliberat prin using (era new Pen fara dispose).
                g.DrawRectangle(chenarPerete, 60, 130,
                    LatimeImagine - 120, 530);
            }
            // Fereastra cu vedere.
            using (var rama = new Pen(p.Acoperis, 6f))
            using (var cer = new SolidBrush(Color.FromArgb(0xBB, 0xDE, 0xFB)))
            {
                g.FillRectangle(cer, 130, 180, 240, 190);
                g.DrawRectangle(rama, 130, 180, 240, 190);
                g.DrawLine(rama, 250, 180, 250, 370);
                g.DrawLine(rama, 130, 275, 370, 275);
            }
            // Canapea.
            using (var sofa = new SolidBrush(p.Acoperis))
            using (var perna = new SolidBrush(p.Accent))
            {
                g.FillRectangle(sofa, 470, 400, 330, 110);
                g.FillRectangle(sofa, 450, 330, 60, 180);
                g.FillRectangle(sofa, 760, 330, 60, 180);
                g.FillRectangle(perna, 500, 420, 80, 60);
                g.FillRectangle(perna, 600, 420, 80, 60);
            }
            // Masuta + lampa.
            using (var lemn = new SolidBrush(Color.FromArgb(0x6D, 0x4C, 0x41)))
            using (var abajur = new SolidBrush(Color.FromArgb(0xFF, 0xF2, 0x8A)))
            {
                g.FillRectangle(lemn, 480, 530, 300, 22);
                g.FillRectangle(lemn, 500, 552, 16, 60);
                g.FillRectangle(lemn, 744, 552, 16, 60);
                g.FillRectangle(lemn, 880, 380, 14, 150);
                Point[] aba = {
                    new Point(845, 380), new Point(929, 380),
                    new Point(915, 320), new Point(859, 320)
                };
                g.FillPolygon(abajur, aba);
            }
            // Tablou cu tipul echipamentei.
            using (var rama = new Pen(p.Accent, 4f))
            using (var font = new Font("Segoe UI", 20f, FontStyle.Bold))
            using (var textPensula = new SolidBrush(p.Accent))
            {
                g.DrawRectangle(rama, 640, 190, 250, 120);
                // FIX: pensula eliberata prin using (era new SolidBrush fara dispose).
                g.DrawString((tip ?? "Interior").ToUpperInvariant(), font,
                    textPensula, 655, 225);
            }
        }

        private static void DeseneazaBandaTitlu(Graphics g,
            string tip, string oras, string adresa, int id, string vedere)
        {
            // Banda inferioara semi-transparenta cu titlu.
            using (var banda = new SolidBrush(Color.FromArgb(210, 20, 20, 20)))
            {
                g.FillRectangle(banda, 0, InaltimeImagine - 120,
                    LatimeImagine, 120);
            }
            // Badge ID.
            using (var badge = new SolidBrush(Color.FromArgb(0xFF, 0x8F, 0x00)))
            using (var fontMic = new Font("Segoe UI", 18f, FontStyle.Bold))
            using (var fontMare = new Font("Segoe UI", 30f, FontStyle.Bold))
            using (var fontSub = new Font("Segoe UI", 19f))
            {
                string eticheta = string.Format("ID {0:D2} · {1}", id, vedere);
                g.FillRectangle(badge, 30, InaltimeImagine - 100, 250, 40);
                g.DrawString(eticheta, fontMic, Brushes.White, 42, InaltimeImagine - 96);

                string titlu = string.Format("{0} · {1}", tip ?? "Echipament", oras ?? "");
                g.DrawString(titlu.Trim(' ', '·'), fontMare, Brushes.White,
                    300, InaltimeImagine - 108);
                if (!string.IsNullOrWhiteSpace(adresa))
                    g.DrawString(adresa.Trim() + "  ·  imagine demo",
                        fontSub, Brushes.White, 302, InaltimeImagine - 60);
            }
        }

        // -- Fisiere temporare -------------------------------------

        private static string CaleTemporara()
        {
            return Path.Combine(Path.GetTempPath(),
                "poza_demo_" + Guid.NewGuid().ToString("N") + ".jpg");
        }

        private static void SalveazaJpeg(Image imagine, string cale, int calitate)
        {
            string dir = Path.GetDirectoryName(cale);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            ImageCodecInfo codec = null;
            ImageCodecInfo[] codecuri = ImageCodecInfo.GetImageEncoders();
            for (int i = 0; i < codecuri.Length; i++)
            {
                if (codecuri[i].MimeType == "image/jpeg")
                {
                    codec = codecuri[i];
                    break;
                }
            }
            if (codec == null)
            {
                imagine.Save(cale, ImageFormat.Jpeg);
                return;
            }
            using (EncoderParameters param = new EncoderParameters(1))
            {
                param.Param[0] = new EncoderParameter(Encoder.Quality, (long)calitate);
                imagine.Save(cale, codec, param);
            }
        }

        private static void StergeSilentios(string cale)
        {
            try
            {
                if (!string.IsNullOrEmpty(cale) && File.Exists(cale))
                    File.Delete(cale);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Nu s-a putut sterge temporarul " + cale +
                    ": " + ex.Message, "DemoPoze");
            }
        }
    }
}
