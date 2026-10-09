/* ================================================================
   AppClubCalculatoare — MigrareFoto.sql
   Baza de date: ClubCalculatoare

   Ordinea de executie in SSMS:
     1. ClubCalculatoare.sql
     2. AuditLog.sql
     3. Autentificare_Update.sql
     4. Permisiuni.sql
     5. MigrareFoto.sql            (acest script)
     6. Favorite.sql
     7. CalculatorCost.sql

   Continut:
     a) Tabela EchipamentFoto (poze echipamente — doar cai si
        metadate; fisierele stau pe disc, NU BLOB in BD)
     b) Index IX_Foto_Echipament (id_echipament, ordine)
     c) Index unic filtrat UX_Foto_Principala (o singura
        fotografie principala per echipament)
     d) Codurile de permisiuni FOTO_VIZUALIZARE_ECHIPAMENTE si
        FOTO_MODIFICARE_ECHIPAMENTE in catalogul Permisiuni
     e) Reparare post-reinstalare: curatare orfani + (re)creare
        FK_Foto_Echipament (ClubCalculatoare.sql sterge FK-urile
        domeniului la reinstalare completa)

   Scriptul este IDEMPOTENT: poate fi rulat de mai multe ori
   fara erori. Toate crearile sunt protejate cu
   IF NOT EXISTS / IF OBJECT_ID(...) IS NULL.
   ================================================================ */

USE ClubCalculatoare;
GO

/* ----------------------------------------------------------------
   a) Tabela EchipamentFoto
   ---------------------------------------------------------------- */
IF OBJECT_ID('EchipamentFoto', 'U') IS NULL
BEGIN
    CREATE TABLE EchipamentFoto (
        id_foto          INT PRIMARY KEY IDENTITY(1,1),
        id_echipament   INT NOT NULL,
        nume_fisier      NVARCHAR(200) NOT NULL,   -- nume Guid-based pe disc
        nume_original    NVARCHAR(200) NULL,       -- numele initial (afisare)
        descriere        NVARCHAR(300) NULL,
        este_principala  BIT NOT NULL DEFAULT 0,
        ordine           INT NOT NULL DEFAULT 0,
        latime           INT NULL,
        inaltime         INT NULL,
        dimensiune_kb    INT NULL,
        data_adaugare    DATETIME NOT NULL DEFAULT GETDATE(),
        adaugat_de       NVARCHAR(50) NULL,
        CONSTRAINT FK_Foto_Echipament FOREIGN KEY (id_echipament)
            REFERENCES Echipamente(id_echipament) ON DELETE CASCADE
    );
    PRINT 'Tabelul EchipamentFoto a fost creat.';
END
ELSE
BEGIN
    PRINT 'Tabelul EchipamentFoto exista deja — omis.';
END
GO

/* ----------------------------------------------------------------
   b) Index IX_Foto_Echipament (id_echipament, ordine)
   ---------------------------------------------------------------- */
IF OBJECT_ID('EchipamentFoto', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE object_id = OBJECT_ID('EchipamentFoto')
                     AND name = 'IX_Foto_Echipament')
BEGIN
    CREATE INDEX IX_Foto_Echipament ON EchipamentFoto(id_echipament, ordine);
    PRINT 'Indexul IX_Foto_Echipament a fost creat.';
END
ELSE
BEGIN
    PRINT 'Indexul IX_Foto_Echipament exista deja — omis.';
END
GO

/* ----------------------------------------------------------------
   c) Index unic filtrat UX_Foto_Principala
      Garantie: o singura fotografie principala per echipament.
   ---------------------------------------------------------------- */
IF OBJECT_ID('EchipamentFoto', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE object_id = OBJECT_ID('EchipamentFoto')
                     AND name = 'UX_Foto_Principala')
BEGIN
    CREATE UNIQUE INDEX UX_Foto_Principala ON EchipamentFoto(id_echipament)
        WHERE este_principala = 1;
    PRINT 'Indexul UX_Foto_Principala a fost creat.';
END
ELSE
BEGIN
    PRINT 'Indexul UX_Foto_Principala exista deja — omis.';
END
GO

/* ----------------------------------------------------------------
   d) Permisiuni foto in catalogul Permisiuni
      (necesita Permisiuni.sql aplicat in prealabil)
   ---------------------------------------------------------------- */
IF OBJECT_ID('Permisiuni', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'FOTO_VIZUALIZARE_ECHIPAMENTE')
        INSERT INTO Permisiuni (cod, descriere)
        VALUES ('FOTO_VIZUALIZARE_ECHIPAMENTE', 'Fotografii echipamente — vizualizare');

    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'FOTO_MODIFICARE_ECHIPAMENTE')
        INSERT INTO Permisiuni (cod, descriere)
        VALUES ('FOTO_MODIFICARE_ECHIPAMENTE', 'Fotografii echipamente — adaugare/stergere');

    PRINT 'Catalogul Permisiuni completat (coduri FOTO_*).';
END
ELSE
BEGIN
    PRINT 'ATENTIE: tabela Permisiuni lipseste — ruleaza mai intai Permisiuni.sql.';
END
GO

/* ----------------------------------------------------------------
   e) Reparare post-reinstalare (idempotent)
      ClubCalculatoare.sql (sectiunea 2) sterge TOATE constrangerile FK
      spre tabelele domeniului — inclusiv FK_Foto_Echipament —
      la reinstalare completa. Curatam randurile orfane si
      readucem constrangerea daca lipseste (ON DELETE CASCADE,
      ca la crearea initiala).
   ---------------------------------------------------------------- */
IF OBJECT_ID('EchipamentFoto', 'U') IS NOT NULL
BEGIN
    -- sqlcmd ruleaza cu QUOTED_IDENTIFIER OFF (spre deosebire de
    -- SSMS); DELETE pe tabela cu index filtrat cere ON.
    SET QUOTED_IDENTIFIER ON;
    DELETE f
    FROM EchipamentFoto f
    LEFT JOIN Echipamente p ON p.id_echipament = f.id_echipament
    WHERE p.id_echipament IS NULL;
    IF @@ROWCOUNT > 0
        PRINT 'Randuri orfane sterse din EchipamentFoto.';

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
                   WHERE name = 'FK_Foto_Echipament'
                     AND parent_object_id = OBJECT_ID('EchipamentFoto'))
    BEGIN
        ALTER TABLE EchipamentFoto
            ADD CONSTRAINT FK_Foto_Echipament FOREIGN KEY (id_echipament)
                REFERENCES Echipamente(id_echipament) ON DELETE CASCADE;
        PRINT 'Constrangerea FK_Foto_Echipament a fost (re)creata.';
    END
    ELSE
    BEGIN
        PRINT 'Constrangerea FK_Foto_Echipament exista deja — omis.';
    END
END
GO

PRINT 'MigrareFoto.sql finalizat cu succes.';
GO


SELECT COUNT(*) FROM EchipamentFoto;