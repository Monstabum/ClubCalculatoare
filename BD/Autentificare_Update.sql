/* ================================================================
   ACTUALIZARE SISTEM AUTENTIFICARE
   Baza de date: ClubCalculatoare
   Aplicație: AppClubCalculatoare

   Executați acest script în SQL Server Management Studio conectat
   la baza de date ClubCalculatoare.

   NOTĂ: dacă ați rulat deja ClubCalculatoare.sql integral, tabelul
   Utilizatori și conturile există deja — scriptul este idempotent
   și poate fi rulat în siguranță din nou.
   ================================================================ */

USE ClubCalculatoare;
GO

/* ----------------------------------------------------------------
   0. Coloana email (Q13) — INSERT-urile de mai jos o folosesc.
   FIX: scriptul cadea cu „Invalid column name 'email'" daca era rulat
   inainte de AuditLog.sql / pe o baza unde coloana lipseste.
   ---------------------------------------------------------------- */
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Utilizatori' AND COLUMN_NAME = 'email'
)
BEGIN
    ALTER TABLE Utilizatori ADD email NVARCHAR(100) NULL;
    PRINT 'Coloana email adaugata in tabelul Utilizatori.';
END
GO

/* ----------------------------------------------------------------
   1. CREARE TABEL Utilizatori (dacă nu există deja)
   ---------------------------------------------------------------- */
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'Utilizatori'
)
BEGIN
    CREATE TABLE Utilizatori (
        id_utilizator  INT           PRIMARY KEY IDENTITY(1,1),
        username       NVARCHAR(50)  NOT NULL UNIQUE,
        parola_hash    NVARCHAR(256) NOT NULL,   -- SHA-256 hex
        rol            NVARCHAR(20)  NOT NULL     -- 'Admin' sau 'User'
                       CHECK (rol IN ('Admin', 'User')),
        nume_complet   NVARCHAR(100) NOT NULL,
        activ          BIT           NOT NULL DEFAULT 1,
        data_creare    DATETIME      NOT NULL DEFAULT GETDATE(),
        ultima_autentificare DATETIME NULL
    );

    PRINT 'Tabelul Utilizatori a fost creat cu succes.';
END
ELSE
BEGIN
    PRINT 'Tabelul Utilizatori exista deja.';
END
GO

/* ----------------------------------------------------------------
   2. ȘTERGERE conturi vechi (admin/admin și user/user) dacă există
   ---------------------------------------------------------------- */
DELETE FROM Utilizatori
WHERE username IN ('admin', 'user');
GO

/* ----------------------------------------------------------------
   3. INSERARE conturi noi

   Hash-urile sunt calculate direct în SQL Server, pentru a fi
   identice cu cele generate de AuthService.cs (SHA-256 + hex
   lowercase), deci autentificarea funcționează garantat.

   IMPORTANT (fix): style 2 este definit doar pentru VARCHAR,
   nu pentru NVARCHAR. Folosim VARCHAR(64) + VARBINARY(50) explicit.

   Conturi implicite:
     Alexandru / 121212  → Admin (Administrator)
     Mihail     / 575757  → User  (Utilizator)
   ---------------------------------------------------------------- */

DECLARE @hash_admin  NVARCHAR(256);
DECLARE @hash_user   NVARCHAR(256);

-- SQL Server calculează SHA-256 și îl convertește în hex lowercase
-- pentru compatibilitate cu implementarea C# (SHA256 + format "x2")
SELECT @hash_admin = LOWER(CONVERT(VARCHAR(64),
    HASHBYTES('SHA2_256', CONVERT(VARBINARY(50), '121212')), 2));

SELECT @hash_user = LOWER(CONVERT(VARCHAR(64),
    HASHBYTES('SHA2_256', CONVERT(VARBINARY(50), '575757')), 2));

PRINT 'Hash admin (parola 121212): ' + @hash_admin;
PRINT 'Hash user  (parola 575757): ' + @hash_user;

-- Șterge conturile dacă există deja (pentru re-rularea scriptului).
-- Se șterg atât numele noi, cât și cele vechi, pentru migrare sigură.
DELETE FROM Utilizatori WHERE username IN ('Alexandru', 'Mihail', 'Constantin', 'Maxim');

-- ── Cont 1: ADMINISTRATOR ────────────────────────────────────────
INSERT INTO Utilizatori (username, parola_hash, rol, nume_complet, email, activ)
VALUES ('Alexandru', @hash_admin, 'Admin', 'Alexandru (Administrator)',
        'admin@clubcalculatoare.ro', 1);

-- ── Cont 2: UTILIZATOR ──────────────────────────────────────────
INSERT INTO Utilizatori (username, parola_hash, rol, nume_complet, email, activ)
VALUES ('Mihail', @hash_user, 'User', 'Mihail (Utilizator)',
        'user@clubcalculatoare.ro', 1);

PRINT '';
PRINT '================================================================';
PRINT 'Conturi create cu succes:';
PRINT '  Username: Alexandru | Parola: 121212 | Rol: Admin (Administrator)';
PRINT '  Username: Mihail    | Parola: 575757 | Rol: User (Utilizator)';
PRINT '================================================================';
GO

/* ----------------------------------------------------------------
   
   ---------------------------------------------------------------- */

/*
ALTER TABLE Utilizatori
    DROP CONSTRAINT UQ__Utilizato__<constraint_name>;

CREATE UNIQUE INDEX UX_Utilizatori_Username_Rol
    ON Utilizatori (username, rol);
*/

/* ----------------------------------------------------------------
   5. VERIFICARE finală — LEN trebuie să fie 64 pentru fiecare hash
   ---------------------------------------------------------------- */
SELECT
    id_utilizator,
    username,
    parola_hash,
    LEN(parola_hash) AS lungime_hash,   -- trebuie să fie 64
    rol,
    nume_complet,
    email,
    activ,
    data_creare
FROM Utilizatori
ORDER BY id_utilizator;
GO

PRINT 'Script finalizat. Verificati rezultatele de mai sus.';
GO

