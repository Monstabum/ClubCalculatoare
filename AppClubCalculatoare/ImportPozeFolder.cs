using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Text.RegularExpressions;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Import de fotografii reale dintr-un folder (implicit PozeDemo)
    /// in echipamente — alternativa la pozele procedurale "Poze demo".
    ///
    /// Corespondenta fisier -> echipament:
    ///   1. din nume: "Echip01_...", "Prop01_..." (compat), "10.jpg";
    ///   2. restul fisierelor (nume oarecare, ex. descarcari) se
    ///      distribuie uniform, in ordine alfabetica, incepand cu
    ///      echipamentele care nu au primit niciun fisier cu nume.
    ///
    /// Prin implicit importul INLOCUIESTE fotografiile existente ale
    /// echipamentelor atinse (cele demo generate), deci miniaturile
    /// din lista se schimba la reincarcare.
    ///
    /// Conventie (ca DemoPoze / PhotoService): fara cod UI, fara throw
    /// spre UI; erorile se logheaza si se intorc prin mesajul de rezultat.
    /// </summary>
    public static class ImportPozeFolder
    {
        private const string NumeFolderImplicit = "PozeDemo";

        private static readonly string[] ExtensiiPermise =
            { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };

        /// <summary>"Echip01_...", "Prop01_...", "echipament 7" -> id echipament.</summary>
        private static readonly Regex RegexEchipament =
            new Regex(@"(?i)\b(?:prop(?:rietate)?|echip(?:ament)?)[\s_\-]*(\d{1,4})(?!\d)");

        /// <summary>
        /// "10.jpg", "3 - Server" -> id. Hexazecimalele de tip hash
        /// ("5fe4f9...") sunt excluse prin (?![0-9A-Za-z]).
        /// </summary>
        private static readonly Regex RegexInitiala =
            new Regex(@"^(\d{1,4})(?![0-9A-Za-z])");

        /// <summary>True daca userul curent poate importa fotografii.</summary>
        public static bool AreDrept()
        {
            return PhotoService.AreDreptModificareFoto();
        }

        /// <summary>
        /// Gaseste folderul PozeDemo: langa executabil, in directorul
        /// curent, apoi urcand in parinti (bin\Debug -> proiect).
        /// Intoarce null daca nu exista (sau nu contine imagini).
        /// </summary>
        public static string GasesteFolder()
        {
            try
            {
                string[] pornire =
                {
                    AppDomain.CurrentDomain.BaseDirectory,
                    Directory.GetCurrentDirectory()
                };
                for (int s = 0; s < pornire.Length; s++)
                {
                    string dir = pornire[s];
                    for (int nivel = 0;
                         nivel < 6 && !string.IsNullOrEmpty(dir);
                         nivel++)
                    {
                        string cale = Path.Combine(dir, NumeFolderImplicit);
                        if (Directory.Exists(cale) &&
                            FisiereImportabile(cale).Count > 0)
                            return cale;
                        dir = Path.GetDirectoryName(dir.TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar));
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Folderul PozeDemo nu a putut fi gasit: " +
                    ex.Message, "ImportPozeFolder.GasesteFolder");
            }
            return null;
        }

        /// <summary>
        /// Fisierele importabile din folder (formate acceptate), ordonate
        /// alfabetic. Lista goala la folder inexistent / fara imagini.
        /// </summary>
        public static List<string> FisiereImportabile(string folder)
        {
            List<string> rezultat = new List<string>();
            try
            {
                if (string.IsNullOrWhiteSpace(folder) ||
                    !Directory.Exists(folder))
                    return rezultat;

                string[] fisiere = Directory.GetFiles(folder);
                Array.Sort(fisiere, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < fisiere.Length; i++)
                {
                    string extensie =
                        Path.GetExtension(fisiere[i]).ToLowerInvariant();
                    if (Array.IndexOf(ExtensiiPermise, extensie) >= 0)
                        rezultat.Add(fisiere[i]);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ImportPozeFolder.FisiereImportabile");
            }
            return rezultat;
        }

        /// <summary>
        /// Planul fisier -> echipament pentru un folder.
        /// Null daca folderul nu are imagini sau baza nu are echipamente.
        /// </summary>
        public static Dictionary<int, List<string>> ConstruiestePlan(
            string folder)
        {
            List<string> fisiere = FisiereImportabile(folder);
            if (fisiere.Count == 0) return null;

            List<int> echipamente = ToateEchipamentele();
            if (echipamente.Count == 0) return null;

            return ImpartePeEchipamente(fisiere, echipamente);
        }

        /// <summary>
        /// Importa imaginile din folder in echipamente. Daca
        /// <paramref name="inlocuieste"/> e true, fotografiile existente
        /// ale echipamentelor atinse sunt sterse inainte.
        /// Intoarce numarul de fotografii adaugate; mesajul descrie
        /// rezultatul pentru UI.
        /// </summary>
        public static int Importa(string folder, bool inlocuieste,
            out string mesaj)
        {
            mesaj = "";
            if (!AreDrept())
            {
                AppLogger.Security(
                    "Acces blocat in ImportPozeFolder: '" + SessionInfo.Username +
                    "' -> import poze din " + folder);
                mesaj = "Nu ai dreptul de modificare a fotografiilor.";
                return 0;
            }

            Dictionary<int, List<string>> plan = ConstruiestePlan(folder);
            if (plan == null || plan.Count == 0)
            {
                mesaj = "Folderul nu contine imagini importabile " +
                    "(.jpg, .jpeg, .png, .bmp, .webp) sau nu exista " +
                    "echipamente in baza de date.";
                return 0;
            }

            PhotoService.EnsureFolders();
            int adaugate = 0;
            int esuate = 0;
            List<int> echipamenteAtinse = new List<int>(plan.Keys);
            echipamenteAtinse.Sort();

            for (int i = 0; i < echipamenteAtinse.Count; i++)
            {
                int idEchipament = echipamenteAtinse[i];
                if (inlocuieste && !PhotoService.StergeToateFotos(idEchipament))
                    AppLogger.Warn(
                        "Fotografiile vechi nu au putut fi sterse la echipamenta " +
                        idEchipament, "ImportPozeFolder");

                List<string> fisiere = plan[idEchipament];
                for (int f = 0; f < fisiere.Count; f++)
                {
                    try
                    {
                        string descriere = "Import din folder: " +
                            Path.GetFileName(fisiere[f]);
                        int idFoto = PhotoService.AdaugaFoto(
                            idEchipament, fisiere[f], descriere);
                        if (idFoto > 0) adaugate++;
                        else esuate++;
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error(ex, "ImportPozeFolder.Importa");
                        esuate++;
                    }
                }
            }

            AppLogger.Info(string.Format(
                "Import poze din folder: {0} adaugate, {1} esuate, " +
                "{2} echipamente, folder: {3}",
                adaugate, esuate, echipamenteAtinse.Count, folder),
                "ImportPozeFolder");
            if (esuate == 0)
                mesaj = string.Format(
                    "Au fost importate {0} fotografii pentru {1} echipamente.",
                    adaugate, echipamenteAtinse.Count);
            else
                mesaj = string.Format(
                    "Au fost importate {0} fotografii ({1} esuate — " +
                    "vezi app.log) pentru {2} echipamente.",
                    adaugate, esuate, echipamenteAtinse.Count);
            return adaugate;
        }

        // ── Impartire fisiere pe echipamente ─────────────────────

        private static Dictionary<int, List<string>> ImpartePeEchipamente(
            List<string> fisiere, List<int> echipamente)
        {
            var exista = new HashSet<int>(echipamente);
            var plan = new Dictionary<int, List<string>>();
            var faraDenumire = new List<string>();

            // 1) Fisiere cu id in nume -> echipamenta respectiva.
            for (int i = 0; i < fisiere.Count; i++)
            {
                int id = IdDinNume(fisiere[i]);
                if (id >= 0 && exista.Contains(id))
                    AdaugaInPlan(plan, id, fisiere[i]);
                else
                    faraDenumire.Add(fisiere[i]);
            }

            // 2) Restul: intai echipamentele goale, apoi rotativ,
            //    ca sa nu ramana niciun obiect fara fotografie noua.
            var ordine = new List<int>();
            for (int i = 0; i < echipamente.Count; i++)
                if (!plan.ContainsKey(echipamente[i]))
                    ordine.Add(echipamente[i]);
            for (int i = 0; i < echipamente.Count; i++)
                if (plan.ContainsKey(echipamente[i]))
                    ordine.Add(echipamente[i]);

            for (int i = 0; i < faraDenumire.Count; i++)
                AdaugaInPlan(plan, ordine[i % ordine.Count], faraDenumire[i]);

            return plan;
        }

        private static void AdaugaInPlan(
            Dictionary<int, List<string>> plan, int idEchipament, string fisier)
        {
            List<string> lista;
            if (!plan.TryGetValue(idEchipament, out lista))
            {
                lista = new List<string>();
                plan[idEchipament] = lista;
            }
            lista.Add(fisier);
        }

        private static int IdDinNume(string cale)
        {
            string nume = Path.GetFileNameWithoutExtension(cale);
            Match m = RegexEchipament.Match(nume);
            if (!m.Success) m = RegexInitiala.Match(nume);
            if (!m.Success) return -1;
            int id;
            if (!int.TryParse(m.Groups[1].Value, out id)) return -1;
            return id;
        }

        // ── Citire echipamente ────────────────────────────────────

        private static List<int> ToateEchipamentele()
        {
            List<int> rezultat = new List<int>();
            try
            {
                using (var conn = SqlDb.CreateConnection())
                {
                    conn.Open();
                    const string sql =
                        "SELECT id_echipament FROM Echipamente ORDER BY id_echipament";
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            rezultat.Add(reader.GetInt32(0));
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ImportPozeFolder.ToateEchipamentele");
            }
            return rezultat;
        }
    }
}
