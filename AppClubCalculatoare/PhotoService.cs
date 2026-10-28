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
    /// Serviciu pentru gestionarea fotografiilor echipamentelor.
    ///
    /// Stocare: fisierele stau pe disc (NU BLOB in BD), in BD se
    /// pastreaza doar caile si metadatele (tabela EchipamentFoto).
    /// Structura pe disc:
    ///   PhotosRootPath\Echipamente{id:D3}\orig\   (originale, intacte)
    ///   PhotosRootPath\Echipamente{id:D3}\thumb\  (200x200, crop centru)
    ///   PhotosRootPath\Echipamente{id:D3}\medium\ (max 1200px, proportii)
    ///
    /// Conventii (ca AuthService): fara cod UI, toate interogarile
    /// parametrizate, erorile se logheaza si se intorc coduri
    /// (-1 / null / false) in loc de throw.
    /// </summary>
    public static class PhotoService
    {
        // ── Constante ─────────────────────────────────────────────

        /// <summary>Tag-ul EXIF pentru orientare (PropertyTagOrientation).</summary>
        private const int ExifOrientationId = 0x0112;

        private static readonly string[] ExtensiiPermise =
            { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };

        // ── Configurare (via DatabaseConfig, cu fallback) ─────────

        private static string RadacinaFoto => DatabaseConfig.PhotosRootPath;

        private static int MaxSizeMb => DatabaseConfig.PhotoMaxSizeMB;

        private static int ThumbSize => DatabaseConfig.PhotoThumbSize;

        private static int MediumSize => DatabaseConfig.PhotoMediumSize;

        // ── Permisiuni (defence in depth) ─────────────────────────

        /// <summary>
        /// True daca utilizatorul curent poate vedea fotografiile.
        /// Implicit: oricine are MENIU_ECHIPAMENTE.
        /// </summary>
        public static bool AreDreptVizualizareFoto()
        {
            if (SessionInfo.EsteAdmin) return true;
            if (PermissionService.VerificaAre(
                    PuncteAcces.FotoVizualizareEchipamente, false)) return true;
            return PermissionService.VerificaAre(
                PuncteAcces.MeniuEchipamente, false);
        }

        /// <summary>
        /// True daca utilizatorul curent poate adauga/sterge fotografii.
        /// Implicit: oricine are MODIFICARE_ECHIPAMENTE.
        /// </summary>
        public static bool AreDreptModificareFoto()
        {
            if (SessionInfo.EsteAdmin) return true;
            if (PermissionService.VerificaAre(
                    PuncteAcces.FotoModificareEchipamente, false)) return true;
            return PermissionService.VerificaAre(
                PuncteAcces.ModificareEchipamente, false);
        }

        // ── Initializare foldere ──────────────────────────────────

        /// <summary>
        /// Creeaza radacina foto la pornirea aplicatiei (idempotent).
        /// </summary>
        public static void EnsureFolders()
        {
            try
            {
                string radacina = RadacinaFoto;
                if (!Directory.Exists(radacina))
                    Directory.CreateDirectory(radacina);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PhotoService.EnsureFolders");
            }
        }

        // ── Cai pe disc ───────────────────────────────────────────

        private static string FolderEchipament(int idEchipament)
        {
            return Path.Combine(RadacinaFoto,
                string.Format("Echipamente{0:D3}", idEchipament));
        }

        private static void DirectoareFoto(int idEchipament,
            out string dirOrig, out string dirThumb, out string dirMedium)
        {
            string baza = FolderEchipament(idEchipament);
            dirOrig = Path.Combine(baza, "orig");
            dirThumb = Path.Combine(baza, "thumb");
            dirMedium = Path.Combine(baza, "medium");
            if (!Directory.Exists(dirOrig)) Directory.CreateDirectory(dirOrig);
            if (!Directory.Exists(dirThumb)) Directory.CreateDirectory(dirThumb);
            if (!Directory.Exists(dirMedium)) Directory.CreateDirectory(dirMedium);
        }

        private static string NumeDerivatJpeg(string numeFisier)
        {
            return Path.ChangeExtension(numeFisier, ".jpg");
        }

        private static void CompleteazaCai(FotoInfo foto)
        {
            string baza = FolderEchipament(foto.IdEchipament);
            foto.CaleOriginal = Path.Combine(baza, "orig", foto.NumeFisier);
            foto.CaleThumb = Path.Combine(baza, "thumb",
                NumeDerivatJpeg(foto.NumeFisier));
            foto.CaleMedium = Path.Combine(baza, "medium",
                NumeDerivatJpeg(foto.NumeFisier));
        }

        // ── Adaugare ──────────────────────────────────────────────

        /// <summary>
        /// Adauga o fotografie la o echipament: copiaza fisierul,
        /// genereaza thumb + medium, scrie in BD.
        /// Prima fotografie a obiectului devine automat principala.
        /// Intoarce id_foto sau -1 la eroare.
        /// </summary>
        public static int AdaugaFoto(int idEchipament, string caleOriginal,
            string descriere = null, IProgress<int> progress = null)
        {
            if (!AreDreptModificareFoto())
            {
                AppLogger.Security(
                    $"Acces blocat in PhotoService: '{SessionInfo.Username}' -> adaugare foto {idEchipament}");
                return -1;
            }

            if (progress != null) progress.Report(0);

            if (string.IsNullOrWhiteSpace(caleOriginal) || !File.Exists(caleOriginal))
            {
                AppLogger.Warn("Fisier inexistent: " + caleOriginal, "PhotoService.AdaugaFoto");
                return -1;
            }

            string extensie = Path.GetExtension(caleOriginal).ToLowerInvariant();
            if (Array.IndexOf(ExtensiiPermise, extensie) < 0)
            {
                AppLogger.Warn("Format neacceptat: " + caleOriginal, "PhotoService.AdaugaFoto");
                return -1;
            }

            long maxBytes = (long)MaxSizeMb * 1024L * 1024L;
            FileInfo infoOriginal = new FileInfo(caleOriginal);
            if (infoOriginal.Length > maxBytes)
            {
                AppLogger.Warn(string.Format(
                    "Fisier prea mare ({0} KB > {1} MB): {2}",
                    infoOriginal.Length / 1024L, MaxSizeMb, caleOriginal),
                    "PhotoService.AdaugaFoto");
                return -1;
            }

            string dirOrig;
            string dirThumb;
            string dirMedium;
            try
            {
                DirectoareFoto(idEchipament, out dirOrig, out dirThumb, out dirMedium);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PhotoService.AdaugaFoto");
                return -1;
            }

            string numeFisier = Guid.NewGuid().ToString("N") + extensie;
            string caleOrig = Path.Combine(dirOrig, numeFisier);
            string caleThumb = Path.Combine(dirThumb, NumeDerivatJpeg(numeFisier));
            string caleMedium = Path.Combine(dirMedium, NumeDerivatJpeg(numeFisier));

            try
            {
                File.Copy(caleOriginal, caleOrig, false);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                AppLogger.Error(ex, "PhotoService.AdaugaFoto");
                return -1;
            }

            Bitmap imagine = null;
            try
            {
                imagine = IncarcaImagineNormalizata(caleOrig);
                if (imagine == null)
                {
                    StergeFisierSilentios(caleOrig);
                    return -1;
                }

                if (progress != null) progress.Report(50);

                using (Bitmap thumb = CreeazaThumb(imagine, ThumbSize))
                    SalveazaJpeg(thumb, caleThumb, 80);
                using (Bitmap medium = CreeazaMedium(imagine, MediumSize))
                    SalveazaJpeg(medium, caleMedium, 85);

                int latime = imagine.Width;
                int inaltime = imagine.Height;
                int dimensiuneKb = (int)(infoOriginal.Length / 1024L);
                string numeOriginalAfisat = Path.GetFileName(caleOriginal);

                int idFoto = InsereazaInBd(idEchipament, numeFisier,
                    numeOriginalAfisat, descriere, latime, inaltime, dimensiuneKb);
                if (idFoto < 0)
                {
                    StergeFisierSilentios(caleOrig);
                    StergeFisierSilentios(caleThumb);
                    StergeFisierSilentios(caleMedium);
                    return -1;
                }

                AuditFoto("INSERT", idFoto,
                    string.Format("Adaugat de {0}, fisier: {1}",
                        SessionInfo.Username, numeOriginalAfisat));
                AppLogger.Info(string.Format(
                    "Foto adaugata: id={0}, echipament={1}, fisier={2}",
                    idFoto, idEchipament, numeOriginalAfisat), "PhotoService");
                if (progress != null) progress.Report(100);
                return idFoto;
            }
            catch (Exception ex) when (
                ex is IOException || ex is UnauthorizedAccessException ||
                ex is OutOfMemoryException || ex is ArgumentException)
            {
                AppLogger.Error(ex, "PhotoService.AdaugaFoto");
                StergeFisierSilentios(caleOrig);
                StergeFisierSilentios(caleThumb);
                StergeFisierSilentios(caleMedium);
                return -1;
            }
            finally
            {
                if (imagine != null) imagine.Dispose();
            }
        }

        /// <summary>
        /// Adaugare in masa (drag&amp;drop): un fisier stricat nu opreste
        /// restul pachetului. Intoarce lista id-urilor reusite.
        /// </summary>
        public static List<int> AdaugaFotos(int idEchipament,
            IEnumerable<string> caiOriginale, IProgress<int> progress = null)
        {
            List<int> rezultat = new List<int>();
            if (caiOriginale == null) return rezultat;

            List<string> cai = new List<string>(caiOriginale);
            for (int i = 0; i < cai.Count; i++)
            {
                try
                {
                    int id = AdaugaFoto(idEchipament, cai[i], null, null);
                    if (id > 0) rezultat.Add(id);
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "PhotoService.AdaugaFotos");
                }
                if (progress != null && cai.Count > 0)
                    progress.Report((i + 1) * 100 / cai.Count);
            }
            return rezultat;
        }

        private static int InsereazaInBd(int idEchipament, string numeFisier,
            string numeOriginal, string descriere,
            int latime, int inaltime, int dimensiuneKb)
        {
            try
            {
                using (var conn = SqlDb.CreateConnection())
                {
                    conn.Open();
                    int ordine;
                    bool prima;
                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*), ISNULL(MAX(ordine), -1) FROM EchipamentFoto WHERE id_echipament = @p", conn))
                    {
                        cmd.Parameters.Add("@p", SqlDbType.Int).Value = idEchipament;
                        using (var reader = cmd.ExecuteReader())
                        {
                            reader.Read();
                            int count = reader.GetInt32(0);
                            ordine = reader.GetInt32(1) + 1;
                            prima = count == 0;
                        }
                    }

                    const string sql = @"
                        INSERT INTO EchipamentFoto
                            (id_echipament, nume_fisier, nume_original, descriere,
                             este_principala, ordine, latime, inaltime,
                             dimensiune_kb, adaugat_de)
                        VALUES
                            (@p, @numefisier, @numeorig, @descr,
                             @principala, @ordine, @lat, @inalt, @kb, @user);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.Add("@p", SqlDbType.Int).Value = idEchipament;
                        cmd.Parameters.Add("@numefisier", SqlDbType.NVarChar, 200).Value = numeFisier;
                        cmd.Parameters.Add("@numeorig", SqlDbType.NVarChar, 200).Value =
                            string.IsNullOrEmpty(numeOriginal) ? (object)DBNull.Value : numeOriginal;
                        cmd.Parameters.Add("@descr", SqlDbType.NVarChar, 300).Value =
                            string.IsNullOrEmpty(descriere) ? (object)DBNull.Value : descriere;
                        cmd.Parameters.Add("@principala", SqlDbType.Bit).Value = prima;
                        cmd.Parameters.Add("@ordine", SqlDbType.Int).Value = ordine;
                        cmd.Parameters.Add("@lat", SqlDbType.Int).Value = latime;
                        cmd.Parameters.Add("@inalt", SqlDbType.Int).Value = inaltime;
                        cmd.Parameters.Add("@kb", SqlDbType.Int).Value = dimensiuneKb;
                        cmd.Parameters.Add("@user", SqlDbType.NVarChar, 50).Value = SessionInfo.Username;
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PhotoService.InsereazaInBd");
                return -1;
            }
        }

        // ── Citire ────────────────────────────────────────────────

        /// <summary>Lista fotografiilor obiectului, ordonata dupa ordine.</summary>
        public static List<FotoInfo> GetFotos(int idEchipament)
        {
            List<FotoInfo> rezultat = new List<FotoInfo>();
            try
            {
                using (var conn = SqlDb.CreateConnection())
                {
                    const string sql = @"
                        SELECT id_foto, id_echipament, nume_fisier, nume_original,
                               descriere, este_principala, ordine, latime, inaltime,
                               dimensiune_kb, data_adaugare, adaugat_de
                        FROM EchipamentFoto
                        WHERE id_echipament = @p
                        ORDER BY ordine, id_foto";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.Add("@p", SqlDbType.Int).Value = idEchipament;
                        conn.Open();
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                FotoInfo foto = CitesteFoto(reader);
                                CompleteazaCai(foto);
                                rezultat.Add(foto);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PhotoService.GetFotos");
            }
            return rezultat;
        }

        /// <summary>Fotografia principala a obiectului (sau null).</summary>
        public static FotoInfo GetFotoPrincipala(int idEchipament)
        {
            try
            {
                using (var conn = SqlDb.CreateConnection())
                {
                    const string sql = @"
                        SELECT TOP 1 id_foto, id_echipament, nume_fisier, nume_original,
                               descriere, este_principala, ordine, latime, inaltime,
                               dimensiune_kb, data_adaugare, adaugat_de
                        FROM EchipamentFoto
                        WHERE id_echipament = @p AND este_principala = 1";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.Add("@p", SqlDbType.Int).Value = idEchipament;
                        conn.Open();
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read()) return null;
                            FotoInfo foto = CitesteFoto(reader);
                            CompleteazaCai(foto);
                            return foto;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PhotoService.GetFotoPrincipala");
                return null;
            }
        }

        /// <summary>
        /// Calea fisierului pentru o fotografie.
        /// marime: "orig" | "thumb" | "medium". Null daca lipseste randul.
        /// </summary>
        public static string GetCaleFisier(int idFoto, string marime)
        {
            string m = (marime ?? "").Trim().ToLowerInvariant();
            if (m != "orig" && m != "thumb" && m != "medium") return null;
            try
            {
                using (var conn = SqlDb.CreateConnection())
                {
                    const string sql = @"
                        SELECT id_echipament, nume_fisier
                        FROM EchipamentFoto WHERE id_foto = @f";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.Add("@f", SqlDbType.Int).Value = idFoto;
                        conn.Open();
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read()) return null;
                            int idEchipament = reader.GetInt32(0);
                            string numeFisier = reader.GetString(1);
                            string baza = FolderEchipament(idEchipament);
                            if (m == "orig")
                                return Path.Combine(baza, "orig", numeFisier);
                            if (m == "thumb")
                                return Path.Combine(baza, "thumb", NumeDerivatJpeg(numeFisier));
                            return Path.Combine(baza, "medium", NumeDerivatJpeg(numeFisier));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PhotoService.GetCaleFisier");
                return null;
            }
        }

        private static FotoInfo CitesteFoto(SqlDataReader reader)
        {
            FotoInfo foto = new FotoInfo();
            foto.IdFoto = reader.GetInt32(reader.GetOrdinal("id_foto"));
            foto.IdEchipament = reader.GetInt32(reader.GetOrdinal("id_echipament"));
            foto.NumeFisier = reader.GetString(reader.GetOrdinal("nume_fisier"));
            foto.NumeOriginal = CitesteString(reader, "nume_original");
            foto.Descriere = CitesteString(reader, "descriere");
            foto.EstePrincipala = Convert.ToBoolean(reader["este_principala"]);
            foto.Ordine = Convert.ToInt32(reader["ordine"]);
            foto.Latime = CitesteInt(reader, "latime");
            foto.Inaltime = CitesteInt(reader, "inaltime");
            foto.DimensiuneKb = CitesteInt(reader, "dimensiune_kb");
            object data = reader["data_adaugare"];
            foto.DataAdaugare = data == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(data);
            foto.AdaugatDe = CitesteString(reader, "adaugat_de");
            return foto;
        }

        private static string CitesteString(SqlDataReader reader, string coloana)
        {
            object val = reader[coloana];
            return val == DBNull.Value ? null : Convert.ToString(val);
        }

        private static int CitesteInt(SqlDataReader reader, string coloana)
        {
            object val = reader[coloana];
            return val == DBNull.Value ? 0 : Convert.ToInt32(val);
        }

        // ── Modificare ────────────────────────────────────────────

        /// <summary>
        /// Seteaza fotografia principala (sesiune: scoate flag-ul
        /// vechi, pune pe cel nou). Intoarce true la succes.
        /// </summary>
        public static bool SeteazaPrincipala(int idFoto)
        {
            if (!AreDreptModificareFoto())
            {
                AppLogger.Security(
                    $"Acces blocat in PhotoService: '{SessionInfo.Username}' -> foto principala {idFoto}");
                return false;
            }
            try
            {
                using (var conn = SqlDb.CreateConnection())
                {
                    conn.Open();
                    int idEchipament;
                    using (var cmd = new SqlCommand(
                        "SELECT id_echipament FROM EchipamentFoto WHERE id_foto = @f", conn))
                    {
                        cmd.Parameters.Add("@f", SqlDbType.Int).Value = idFoto;
                        object val = cmd.ExecuteScalar();
                        if (val == null || val == DBNull.Value) return false;
                        idEchipament = Convert.ToInt32(val);
                    }

                    using (var tr = conn.BeginTransaction())
                    {
                        try
                        {
                            using (var cmd = new SqlCommand(
                                "UPDATE EchipamentFoto SET este_principala = 0 WHERE id_echipament = @p", conn, tr))
                            {
                                cmd.Parameters.Add("@p", SqlDbType.Int).Value = idEchipament;
                                cmd.ExecuteNonQuery();
                            }
                            using (var cmd = new SqlCommand(
                                "UPDATE EchipamentFoto SET este_principala = 1 WHERE id_foto = @f AND id_echipament = @p", conn, tr))
                            {
                                cmd.Parameters.Add("@f", SqlDbType.Int).Value = idFoto;
                                cmd.Parameters.Add("@p", SqlDbType.Int).Value = idEchipament;
                                int rows = cmd.ExecuteNonQuery();
                                if (rows == 0)
                                {
                                    tr.Rollback();
                                    return false;
                                }
                            }
                            tr.Commit();
                        }
                        catch
                        {
                            tr.Rollback();
                            throw;
                        }
                    }

                    AuditFoto("UPDATE", idFoto,
                        string.Format("Foto principala schimbata la {0} de {1}",
                            idFoto, SessionInfo.Username));
                    AppLogger.Info("Foto principala: id=" + idFoto, "PhotoService");
                    return true;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PhotoService.SeteazaPrincipala");
                return false;
            }
        }

        /// <summary>Actualizeaza descrierea unei fotografii.</summary>
        public static bool ActualizeazaDescriere(int idFoto, string descriere)
        {
            if (!AreDreptModificareFoto())
            {
                AppLogger.Security(
                    $"Acces blocat in PhotoService: '{SessionInfo.Username}' -> descriere foto {idFoto}");
                return false;
            }
            try
            {
                using (var conn = SqlDb.CreateConnection())
                {
                    const string sql = @"
                        UPDATE EchipamentFoto SET descriere = @d WHERE id_foto = @f";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.Add("@d", SqlDbType.NVarChar, 300).Value =
                            string.IsNullOrEmpty(descriere) ? (object)DBNull.Value : descriere;
                        cmd.Parameters.Add("@f", SqlDbType.Int).Value = idFoto;
                        conn.Open();
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PhotoService.ActualizeazaDescriere");
                return false;
            }
        }

        /// <summary>
        /// Reordoneaza fotografiile (update de ordine intr-o singura
        /// sesiune, in ordinea listei primite).
        /// </summary>
        public static bool Reordoneaza(int idEchipament, List<int> idFotosInOrdine)
        {
            if (!AreDreptModificareFoto())
            {
                AppLogger.Security(
                    $"Acces blocat in PhotoService: '{SessionInfo.Username}' -> reordonare foto {idEchipament}");
                return false;
            }
            if (idFotosInOrdine == null || idFotosInOrdine.Count == 0) return false;
            try
            {
                using (var conn = SqlDb.CreateConnection())
                {
                    conn.Open();
                    using (var tr = conn.BeginTransaction())
                    {
                        try
                        {
                            for (int i = 0; i < idFotosInOrdine.Count; i++)
                            {
                                using (var cmd = new SqlCommand(
                                    "UPDATE EchipamentFoto SET ordine = @o WHERE id_foto = @f AND id_echipament = @p",
                                    conn, tr))
                                {
                                    cmd.Parameters.Add("@o", SqlDbType.Int).Value = i;
                                    cmd.Parameters.Add("@f", SqlDbType.Int).Value = idFotosInOrdine[i];
                                    cmd.Parameters.Add("@p", SqlDbType.Int).Value = idEchipament;
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            tr.Commit();
                            return true;
                        }
                        catch
                        {
                            tr.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PhotoService.Reordoneaza");
                return false;
            }
        }

        /// <summary>
        /// Roteste fotografia cu 90° (toate cele 3 fisiere + dimensiuni).
        /// Extensie in afara contractului de baza (pentru meniul galeriei).
        /// </summary>
        public static bool RotesteFoto90(int idFoto)
        {
            if (!AreDreptModificareFoto())
            {
                AppLogger.Security(
                    $"Acces blocat in PhotoService: '{SessionInfo.Username}' -> rotire foto {idFoto}");
                return false;
            }
            FotoInfo foto = GetFotoDupaId(idFoto);
            if (foto == null) return false;
            CompleteazaCai(foto);
            if (!File.Exists(foto.CaleOriginal)) return false;
            Bitmap imagine = null;
            try
            {
                imagine = IncarcaImagineNormalizata(foto.CaleOriginal);
                if (imagine == null) return false;
                imagine.RotateFlip(RotateFlipType.Rotate90FlipNone);
                int latime = imagine.Width;
                int inaltime = imagine.Height;
                SalveazaImagineInFormat(imagine, foto.CaleOriginal, 90);
                using (Bitmap thumb = CreeazaThumb(imagine, ThumbSize))
                    SalveazaJpeg(thumb, foto.CaleThumb, 80);
                using (Bitmap medium = CreeazaMedium(imagine, MediumSize))
                    SalveazaJpeg(medium, foto.CaleMedium, 85);

                using (var conn = SqlDb.CreateConnection())
                {
                    const string sql = @"
                        UPDATE EchipamentFoto SET latime = @lat, inaltime = @inalt WHERE id_foto = @f";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.Add("@lat", SqlDbType.Int).Value = latime;
                        cmd.Parameters.Add("@inalt", SqlDbType.Int).Value = inaltime;
                        cmd.Parameters.Add("@f", SqlDbType.Int).Value = idFoto;
                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
                AppLogger.Info("Foto rotita 90: id=" + idFoto, "PhotoService");
                return true;
            }
            catch (Exception ex) when (
                ex is IOException || ex is UnauthorizedAccessException ||
                ex is OutOfMemoryException || ex is ArgumentException)
            {
                AppLogger.Error(ex, "PhotoService.RotesteFoto90");
                return false;
            }
            finally
            {
                if (imagine != null) imagine.Dispose();
            }
        }

        private static FotoInfo GetFotoDupaId(int idFoto)
        {
            try
            {
                using (var conn = SqlDb.CreateConnection())
                {
                    const string sql = @"
                        SELECT id_foto, id_echipament, nume_fisier, nume_original,
                               descriere, este_principala, ordine, latime, inaltime,
                               dimensiune_kb, data_adaugare, adaugat_de
                        FROM EchipamentFoto WHERE id_foto = @f";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.Add("@f", SqlDbType.Int).Value = idFoto;
                        conn.Open();
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read()) return null;
                            return CitesteFoto(reader);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PhotoService.GetFotoDupaId");
                return null;
            }
        }

        // ── Stergere ──────────────────────────────────────────────

        /// <summary>
        /// Sterge o fotografie (rand BD + cele 3 fisiere). Daca era
        /// principala, cea mai veche ramasa devine principala.
        /// </summary>
        public static bool StergeFoto(int idFoto)
        {
            if (!AreDreptModificareFoto())
            {
                AppLogger.Security(
                    $"Acces blocat in PhotoService: '{SessionInfo.Username}' -> stergere foto {idFoto}");
                return false;
            }
            FotoInfo foto = GetFotoDupaId(idFoto);
            if (foto == null) return false;
            CompleteazaCai(foto);
            try
            {
                using (var conn = SqlDb.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(
                        "DELETE FROM EchipamentFoto WHERE id_foto = @f", conn))
                    {
                        cmd.Parameters.Add("@f", SqlDbType.Int).Value = idFoto;
                        if (cmd.ExecuteNonQuery() == 0) return false;
                    }

                    if (foto.EstePrincipala)
                    {
                        using (var cmd = new SqlCommand(
                            @"UPDATE EchipamentFoto SET este_principala = 1
                               WHERE id_foto = (SELECT TOP 1 id_foto FROM EchipamentFoto
                                                WHERE id_echipament = @p ORDER BY ordine, id_foto)", conn))
                        {
                            cmd.Parameters.Add("@p", SqlDbType.Int).Value = foto.IdEchipament;
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                StergeFisierSilentios(foto.CaleOriginal);
                StergeFisierSilentios(foto.CaleThumb);
                StergeFisierSilentios(foto.CaleMedium);

                AuditFoto("DELETE", idFoto,
                    string.Format("Sters de {0}, fisier: {1}",
                        SessionInfo.Username, foto.NumeFisier));
                AppLogger.Info("Foto stearsa: id=" + idFoto, "PhotoService");
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PhotoService.StergeFoto");
                return false;
            }
        }

        /// <summary>
        /// Sterge toate fotografiile obiectului (randuri BD + folderul
        /// de pe disc). Util la stergerea echipamentei (FK CASCADE
        /// sterge randurile, dar fisierele trebuie curatate).
        /// </summary>
        public static bool StergeToateFotos(int idEchipament)
        {
            if (!AreDreptModificareFoto())
            {
                AppLogger.Security(
                    $"Acces blocat in PhotoService: '{SessionInfo.Username}' -> stergere toate foto {idEchipament}");
                return false;
            }
            try
            {
                int count;
                using (var conn = SqlDb.CreateConnection())
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(
                        "DELETE FROM EchipamentFoto WHERE id_echipament = @p", conn))
                    {
                        cmd.Parameters.Add("@p", SqlDbType.Int).Value = idEchipament;
                        count = cmd.ExecuteNonQuery();
                    }
                }

                try
                {
                    string folder = FolderEchipament(idEchipament);
                    if (Directory.Exists(folder))
                        Directory.Delete(folder, true);
                }
                catch (Exception ex)
                {
                    AppLogger.Warn("Nu s-a putut sterge folderul foto: " + ex.Message,
                        "PhotoService.StergeToateFotos");
                }

                if (count > 0)
                    AuditFoto("DELETE", 0,
                        string.Format("Sterse {0} fotografii ale echipamentei {1} de {2}",
                            count, idEchipament, SessionInfo.Username));
                AppLogger.Info(string.Format(
                    "Sterse {0} fotografii ale echipamentei {1}", count, idEchipament),
                    "PhotoService");
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PhotoService.StergeToateFotos");
                return false;
            }
        }

        // ── Orfani si integritate ─────────────────────────────────

        /// <summary>
        /// Gaseste orfanii: fisiere fara rand in BD + id-uri din BD
        /// fara fisier pe disc. Functie de service pentru admin.
        /// </summary>
        public static (List<string> faraBd, List<int> faraFisier) GasesteOrfani()
        {
            List<string> faraBd = new List<string>();
            List<int> faraFisier = new List<int>();
            try
            {
                Dictionary<string, int> inBd = new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);
                List<Tuple<int, string>> caiDb = new List<Tuple<int, string>>();

                using (var conn = SqlDb.CreateConnection())
                {
                    const string sql = @"
                        SELECT id_foto, id_echipament, nume_fisier FROM EchipamentFoto";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        conn.Open();
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int id = reader.GetInt32(0);
                                int idProp = reader.GetInt32(1);
                                string nume = reader.GetString(2);
                                inBd[nume] = id;
                                string caleOrig = Path.Combine(
                                    FolderEchipament(idProp), "orig", nume);
                                caiDb.Add(Tuple.Create(id, caleOrig));
                            }
                        }
                    }
                }

                for (int i = 0; i < caiDb.Count; i++)
                {
                    if (!File.Exists(caiDb[i].Item2))
                        faraFisier.Add(caiDb[i].Item1);
                }

                string radacina = RadacinaFoto;
                if (Directory.Exists(radacina))
                {
                    string[] directoare = Directory.GetDirectories(radacina, "Echipamente*");
                    for (int d = 0; d < directoare.Length; d++)
                    {
                        string dirOrig = Path.Combine(directoare[d], "orig");
                        if (!Directory.Exists(dirOrig)) continue;
                        string[] fisiere = Directory.GetFiles(dirOrig);
                        for (int f = 0; f < fisiere.Length; f++)
                        {
                            if (!inBd.ContainsKey(Path.GetFileName(fisiere[f])))
                                faraBd.Add(fisiere[f]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PhotoService.GasesteOrfani");
            }
            return (faraBd, faraFisier);
        }

        /// <summary>
        /// Sterge fisierele-orfan (fara rand in BD). Intoarce numarul sters.
        /// </summary>
        public static int CurataOrfani()
        {
            if (!AreDreptModificareFoto()) return 0;
            var orfani = GasesteOrfani();
            int sterse = 0;
            for (int i = 0; i < orfani.faraBd.Count; i++)
            {
                try
                {
                    File.Delete(orfani.faraBd[i]);
                    sterse++;
                }
                catch (Exception ex)
                {
                    AppLogger.Warn("Nu s-a putut sterge orfanul " +
                        orfani.faraBd[i] + ": " + ex.Message, "PhotoService.CurataOrfani");
                }
            }
            if (sterse > 0)
                AppLogger.Info("Curatare orfani: " + sterse + " fisiere", "PhotoService");
            return sterse;
        }

        /// <summary>
        /// Verificare la pornire: id-urile din BD fara fisier pe disc.
        /// </summary>
        public static List<int> VerificaIntegritate()
        {
            return GasesteOrfani().faraFisier;
        }

        // ── Audit ─────────────────────────────────────────────────

        private static void AuditFoto(string actiune, int idFoto, string detalii)
        {
            try
            {
                using (var conn = SqlDb.CreateConnection())
                {
                    const string sql = @"
                        IF OBJECT_ID('AuditLog','U') IS NOT NULL
                            INSERT INTO AuditLog (tabela, id_inregistrare, actiune,
                                                  username, data_ora, detalii)
                            VALUES ('EchipamentFoto', @id, @actiune, @user,
                                    GETDATE(), @detalii)";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.Add("@id", SqlDbType.Int).Value = idFoto;
                        cmd.Parameters.Add("@actiune", SqlDbType.NVarChar, 10).Value = actiune;
                        cmd.Parameters.Add("@user", SqlDbType.NVarChar, 50).Value = SessionInfo.Username;
                        cmd.Parameters.Add("@detalii", SqlDbType.NVarChar, -1).Value =
                            detalii == null ? (object)DBNull.Value : detalii;
                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Audit foto esuat: " + ex.Message, "PhotoService");
            }
        }

        // ── Prelucrare imagini ────────────────────────────────────

        /// <summary>
        /// Incarca imaginea si aplica corectia de orientare EXIF.
        /// Intoarce null daca fisierul este stricat (nu arunca).
        /// Apelantul elibereaza Bitmap-ul returnat.
        /// FIX: incarcare prin FileStream+FromStream (fara lock pe fisier,
        /// ca la new Bitmap(cale) care tinea fisierul blocat pana la Dispose).
        /// </summary>
        private static Bitmap IncarcaImagineNormalizata(string cale)
        {
            try
            {
                Bitmap imagine;
                using (FileStream fs = new FileStream(cale,
                    FileMode.Open, FileAccess.Read, FileShare.Read))
                using (Image tmp = Image.FromStream(fs))
                {
                    imagine = new Bitmap(tmp);
                }
                try
                {
                    RotateFlipType rotire = RotireDinExif(imagine);
                    if (rotire != RotateFlipType.RotateNoneFlipNone)
                        imagine.RotateFlip(rotire);
                }
                catch
                {
                    // EXIF ilizibil — folosim imaginea ca atare
                }
                return imagine;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is OutOfMemoryException)
            {
                AppLogger.Warn("Fisier imagine stricat: " + cale + " (" + ex.Message + ")",
                    "PhotoService");
                return null;
            }
        }

        private static RotateFlipType RotireDinExif(Image imagine)
        {
            try
            {
                foreach (PropertyItem prop in imagine.PropertyItems)
                {
                    if (prop.Id == ExifOrientationId && prop.Value != null && prop.Value.Length > 0)
                    {
                        switch (prop.Value[0])
                        {
                            case 2: return RotateFlipType.RotateNoneFlipX;
                            case 3: return RotateFlipType.Rotate180FlipNone;
                            case 4: return RotateFlipType.RotateNoneFlipY;
                            case 5: return RotateFlipType.Rotate90FlipX;
                            case 6: return RotateFlipType.Rotate90FlipNone;
                            case 7: return RotateFlipType.Rotate270FlipX;
                            case 8: return RotateFlipType.Rotate270FlipNone;
                        }
                    }
                }
            }
            catch
            {
                // Ignorat — imagine fara EXIF valid
            }
            return RotateFlipType.RotateNoneFlipNone;
        }

        /// <summary>
        /// Miniatura patrata: crop pe centru (fara deformare) + resize.
        /// </summary>
        private static Bitmap CreeazaThumb(Bitmap sursa, int marime)
        {
            int latura = Math.Min(sursa.Width, sursa.Height);
            int x = (sursa.Width - latura) / 2;
            int y = (sursa.Height - latura) / 2;
            Bitmap rezultat = new Bitmap(marime, marime);
            using (Graphics g = Graphics.FromImage(rezultat))
            {
                SeteazaCalitate(g);
                g.DrawImage(sursa,
                    new Rectangle(0, 0, marime, marime),
                    new Rectangle(x, y, latura, latura),
                    GraphicsUnit.Pixel);
            }
            return rezultat;
        }

        /// <summary>
        /// Versiune medie: proportii pastrate, latura lunga max dat.
        /// </summary>
        private static Bitmap CreeazaMedium(Bitmap sursa, int maxLatura)
        {
            int lat = sursa.Width;
            int inalt = sursa.Height;
            int lung = Math.Max(lat, inalt);
            if (lung <= maxLatura)
                return new Bitmap(sursa);

            double factor = (double)maxLatura / lung;
            int latNou = Math.Max(1, (int)(lat * factor));
            int inaltNou = Math.Max(1, (int)(inalt * factor));
            Bitmap rezultat = new Bitmap(latNou, inaltNou);
            using (Graphics g = Graphics.FromImage(rezultat))
            {
                SeteazaCalitate(g);
                g.DrawImage(sursa, new Rectangle(0, 0, latNou, inaltNou),
                    new Rectangle(0, 0, lat, inalt), GraphicsUnit.Pixel);
            }
            return rezultat;
        }

        private static void SeteazaCalitate(Graphics g)
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
        }

        private static void SalveazaJpeg(Image imagine, string cale, int calitate)
        {
            string dir = Path.GetDirectoryName(cale);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            ImageCodecInfo codec = CodecJpeg();
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

        private static void SalveazaImagineInFormat(Image imagine, string cale, int calitateJpeg)
        {
            string ext = Path.GetExtension(cale).ToLowerInvariant();
            if (ext == ".jpg" || ext == ".jpeg")
            {
                SalveazaJpeg(imagine, cale, calitateJpeg);
                return;
            }
            if (ext == ".png")
            {
                imagine.Save(cale, ImageFormat.Png);
                return;
            }
            if (ext == ".bmp")
            {
                imagine.Save(cale, ImageFormat.Bmp);
                return;
            }
            imagine.Save(cale);
        }

        private static ImageCodecInfo CodecJpeg()
        {
            ImageCodecInfo[] codecuri = ImageCodecInfo.GetImageEncoders();
            for (int i = 0; i < codecuri.Length; i++)
            {
                if (codecuri[i].MimeType == "image/jpeg")
                    return codecuri[i];
            }
            return null;
        }

        private static void StergeFisierSilentios(string cale)
        {
            try
            {
                if (!string.IsNullOrEmpty(cale) && File.Exists(cale))
                    File.Delete(cale);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Nu s-a putut sterge fisierul " + cale + ": " + ex.Message,
                    "PhotoService");
            }
        }
    }
}
