-- ═══════════════════════════════════════════════════════════════
-- AppClubCalculatoare — Scripturi SQL suplimentare
-- Intrebarea Q5  : Tabel AuditLog pentru istoricul modificarilor.
-- Intrebarea Q13 : Coloana email in tabelul Utilizatori.
-- ═══════════════════════════════════════════════════════════════
-- Structura tabelului rămâne identică celei din versiunea sportivă;
-- s-au schimbat doar numele bazei de date și comentariile.

USE ClubCalculatoare;
GO

-- ────────────────────────────────────────────────────────────────
-- Q5: Tabel AuditLog
-- Inregistreaza fiecare INSERT / UPDATE / DELETE efectuat din aplicatie,
-- pentru tabelele: Operatori, Sali, Echipamente, Membri, Sesiuni,
-- Rezervari. Se creaza DOAR daca nu exista deja.
-- ────────────────────────────────────────────────────────────────
IF OBJECT_ID('AuditLog', 'U') IS NULL
BEGIN
    CREATE TABLE AuditLog (
        id_audit         INT           PRIMARY KEY IDENTITY(1,1),
        tabela           NVARCHAR(50)  NOT NULL,
        id_inregistrare  INT           NOT NULL DEFAULT 0,
        actiune          NVARCHAR(10)  NOT NULL,   -- 'INSERT' / 'UPDATE' / 'DELETE'
        username         NVARCHAR(50)  NOT NULL,
        data_ora         DATETIME      NOT NULL DEFAULT GETDATE(),
        detalii          NVARCHAR(MAX) NULL         -- rezervat pentru viitor (JSON)
    );

    PRINT 'Tabel AuditLog creat cu succes.';
END
ELSE
BEGIN
    PRINT 'Tabelul AuditLog exista deja — omis.';
END
GO

-- ────────────────────────────────────────────────────────────────
-- Q13: Coloana email in tabelul Utilizatori
-- Folosita pentru resetarea parolei prin email.
-- Se adauga DOAR daca nu exista deja.
-- ────────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1
    FROM   INFORMATION_SCHEMA.COLUMNS
    WHERE  TABLE_NAME  = 'Utilizatori'
      AND  COLUMN_NAME = 'email'
)
BEGIN
    ALTER TABLE Utilizatori
        ADD email NVARCHAR(100) NULL;

    PRINT 'Coloana email adaugata in tabelul Utilizatori.';
END
ELSE
BEGIN
    PRINT 'Coloana email exista deja in Utilizatori — omisa.';
END
GO

-- ────────────────────────────────────────────────────────────────
-- Optional: actualizeaza email-urile utilizatorilor existenti
-- (inlocuieste cu adresele reale)
-- ────────────────────────────────────────────────────────────────
-- UPDATE Utilizatori SET email = 'admin@clubcalculatoare.ro' WHERE username = 'Constantin';
-- UPDATE Utilizatori SET email = 'user@clubcalculatoare.ro'  WHERE username = 'Maxim';
-- GO

-- ────────────────────────────────────────────────────────────────
-- Vizualizare continut AuditLog (pentru verificare)
-- ────────────────────────────────────────────────────────────────
-- SELECT TOP 50 * FROM AuditLog ORDER BY data_ora DESC;
GO

-- ────────────────────────────────────────────────────────────────
-- Raport rapid: ultimele 20 de modificări din aplicație
-- ────────────────────────────────────────────────────────────────
SELECT TOP 20
    a.data_ora,
    a.actiune,
    a.tabela,
    a.id_inregistrare,
    a.username
FROM AuditLog a
ORDER BY a.data_ora DESC;
GO
