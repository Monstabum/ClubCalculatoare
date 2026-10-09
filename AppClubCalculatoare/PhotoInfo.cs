namespace AppClubCalculatoare
{
    /// <summary>
    /// Model de date pentru o fotografie a unei echipamente.
    /// Corespunde unui rand din tabela <c>EchipamentFoto</c>.
    /// Fisierele stau pe disc (orig / thumb / medium), in BD se
    /// pastreaza doar caile si metadatele.
    /// </summary>
    public class FotoInfo
    {
        /// <summary>Cheia primara (id_foto).</summary>
        public int IdFoto { get; set; }

        /// <summary>Echipamenta de care apartine fotografia.</summary>
        public int IdEchipament { get; set; }

        /// <summary>Numele Guid-based al fisierului pe disc.</summary>
        public string NumeFisier { get; set; }

        /// <summary>Numele original al fisierului (pentru afisare).</summary>
        public string NumeOriginal { get; set; }

        /// <summary>Descrierea editabila a fotografiei.</summary>
        public string Descriere { get; set; }

        /// <summary>True daca este fotografia principala a obiectului.</summary>
        public bool EstePrincipala { get; set; }

        /// <summary>Pozitia in galerie (sortare).</summary>
        public int Ordine { get; set; }

        /// <summary>Latimea imaginii (px, dupa corectia EXIF).</summary>
        public int Latime { get; set; }

        /// <summary>Inaltimea imaginii (px, dupa corectia EXIF).</summary>
        public int Inaltime { get; set; }

        /// <summary>Dimensiunea fisierului original (KB).</summary>
        public int DimensiuneKb { get; set; }

        /// <summary>Data adaugarii in sistem.</summary>
        public System.DateTime DataAdaugare { get; set; }

        /// <summary>Utilizatorul care a adaugat fotografia.</summary>
        public string AdaugatDe { get; set; }

        /// <summary>Calea completa a miniaturii (200x200).</summary>
        public string CaleThumb { get; set; }

        /// <summary>Calea completa a versiunii medii (max 1200px).</summary>
        public string CaleMedium { get; set; }

        /// <summary>Calea completa a originalului.</summary>
        public string CaleOriginal { get; set; }
    }
}
