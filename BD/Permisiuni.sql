/* ================================================================
   AppClubCalculatoare — Permisiuni.sql
   Baza de date: ClubCalculatoare

   Ordinea de executie in SSMS:
     1. ClubCalculatoare.sql
     2. AuditLog.sql          (creeaza AuditLog + coloana email)
     3. Autentificare_Update.sql  (INSERT foloseste coloana email,
                                   deci email trebuie sa existe deja)
     4. Permisiuni.sql        (acest script)

   Scriptul este IDEMPOTENT: poate fi rulat de mai multe ori
   fara erori. Toate crearile sunt protejate cu
   IF NOT EXISTS / IF OBJECT_ID(...) IS NULL.
   ================================================================ */

USE ClubCalculatoare;
GO

/* ----------------------------------------------------------------
   a) Tabela Permisiuni (cod PK, descriere)
   ---------------------------------------------------------------- */
IF OBJECT_ID('Permisiuni', 'U') IS NULL
BEGIN
    CREATE TABLE Permisiuni (
        cod        NVARCHAR(50)  NOT NULL PRIMARY KEY,
        descriere  NVARCHAR(200) NOT NULL
    );
    PRINT 'Tabelul Permisiuni a fost creat.';
END
ELSE
BEGIN
    PRINT 'Tabelul Permisiuni exista deja — omis.';
END
GO

/* Catalogul de baza din PuncteAcces (PermissionService.cs): 21 coduri
   (MENIU_* 7 + PANOU_DASHBOARD + MODIFICARE_* 6 + STERGE_* 6 +
   GESTIONARE_UTILIZATORI). Codurile modulelor se adauga din scripturile
   proprii, in ordine: FOTO_* din MigrareFoto.sql, FAVORITE din
   Favorite.sql, CALCULATOR_COST din CalculatorCost.sql (total 25).
   Fiecare INSERT este idempotent (WHERE NOT EXISTS). */
IF OBJECT_ID('Permisiuni', 'U') IS NOT NULL
BEGIN
    -- ── MENIU_* (7) ──
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'MENIU_ECHIPAMENTE')
        INSERT INTO Permisiuni (cod, descriere) VALUES ('MENIU_ECHIPAMENTE', 'Echipamente - vizualizare');
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'MENIU_SALI')
        INSERT INTO Permisiuni (cod, descriere) VALUES ('MENIU_SALI', 'Sali - vizualizare');
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'MENIU_MEMBRI')
        INSERT INTO Permisiuni (cod, descriere) VALUES ('MENIU_MEMBRI', 'Membri - vizualizare');
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'MENIU_SESIUNI')
        INSERT INTO Permisiuni (cod, descriere) VALUES ('MENIU_SESIUNI', 'Sesiuni - vizualizare');
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'MENIU_REZERVARI')
        INSERT INTO Permisiuni (cod, descriere) VALUES ('MENIU_REZERVARI', 'Rezervari - vizualizare');
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'MENIU_OPERATORI')
        INSERT INTO Permisiuni (cod, descriere) VALUES ('MENIU_OPERATORI', 'Operatori - vizualizare');
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'MENIU_RAPOARTE')
        INSERT INTO Permisiuni (cod, descriere) VALUES ('MENIU_RAPOARTE', 'Rapoarte si Statistici');

    -- ── Functie transversala: dashboard ──
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'PANOU_DASHBOARD')
        INSERT INTO Permisiuni (cod, descriere) VALUES ('PANOU_DASHBOARD', 'Tablou de bord (KPI)');

    -- ── MODIFICARE_* (6) ──
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'MODIFICARE_ECHIPAMENTE')
        INSERT INTO Permisiuni (cod, descriere) VALUES ('MODIFICARE_ECHIPAMENTE', 'Echipamente - adaugare/modificare');
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'MODIFICARE_SALI')
        INSERT INTO Permisiuni (cod, descriere) VALUES ('MODIFICARE_SALI', 'Sali - adaugare/modificare');
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'MODIFICARE_MEMBRI')
        INSERT INTO Permisiuni (cod, descriere) VALUES ('MODIFICARE_MEMBRI', 'Membri - adaugare/modificare');
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'MODIFICARE_SESIUNI')
        INSERT INTO Permisiuni (cod, descriere) VALUES ('MODIFICARE_SESIUNI', 'Sesiuni - adaugare/modificare');
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'MODIFICARE_REZERVARI')
        INSERT INTO Permisiuni (cod, descriere) VALUES ('MODIFICARE_REZERVARI', 'Rezervari - adaugare/modificare');
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'MODIFICARE_OPERATORI')
        INSERT INTO Permisiuni (cod, descriere) VALUES ('MODIFICARE_OPERATORI', 'Operatori - adaugare/modificare');

    -- ── STERGE_* (6) ──
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'STERGE_ECHIPAMENTE')
        INSERT INTO Permisiuni (cod, descriere) VALUES ('STERGE_ECHIPAMENTE', 'Echipamente - stergere');
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'STERGE_SALI')
        INSERT INTO Permisiuni (cod, descriere) VALUES ('STERGE_SALI', 'Sali - stergere');
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'STERGE_MEMBRI')
        INSERT INTO Permisiuni (cod, descriere) VALUES ('STERGE_MEMBRI', 'Membri - stergere');
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'STERGE_SESIUNI')
        INSERT INTO Permisiuni (cod, descriere) VALUES ('STERGE_SESIUNI', 'Sesiuni - stergere');
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'STERGE_REZERVARI')
        INSERT INTO Permisiuni (cod, descriere) VALUES ('STERGE_REZERVARI', 'Rezervari - stergere');
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'STERGE_OPERATORI')
        INSERT INTO Permisiuni (cod, descriere) VALUES ('STERGE_OPERATORI', 'Operatori - stergere');

    -- ── Gestionare utilizatori ──
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'GESTIONARE_UTILIZATORI')
        INSERT INTO Permisiuni (cod, descriere) VALUES ('GESTIONARE_UTILIZATORI', 'Gestionare Utilizatori');

    PRINT 'Catalogul Permisiuni completat (21 coduri).';
END
GO

/* ----------------------------------------------------------------
   b) Tabela UtilizatoriPermisiuni
   ---------------------------------------------------------------- */
IF OBJECT_ID('UtilizatoriPermisiuni', 'U') IS NULL
BEGIN
    CREATE TABLE UtilizatoriPermisiuni (
        id_utilizator   INT           NOT NULL,
        cod_permisiune  NVARCHAR(50)  NOT NULL,
        acordata_de     NVARCHAR(50)  NULL,
        data_acordare   DATETIME      NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_UtilizatoriPermisiuni
            PRIMARY KEY (id_utilizator, cod_permisiune),
        CONSTRAINT FK_UtilizatoriPermisiuni_Utilizatori
            FOREIGN KEY (id_utilizator) REFERENCES Utilizatori (id_utilizator)
            ON DELETE CASCADE,
        CONSTRAINT FK_UtilizatoriPermisiuni_Permisiuni
            FOREIGN KEY (cod_permisiune) REFERENCES Permisiuni (cod)
            ON DELETE CASCADE
    );
    PRINT 'Tabelul UtilizatoriPermisiuni a fost creat.';
END
ELSE
BEGIN
    PRINT 'Tabelul UtilizatoriPermisiuni exista deja — omis.';
END
GO

/* ----------------------------------------------------------------
   c) Coloane pentru blocare persistenta (AuthService le foloseste).
      Se adauga DOAR daca lipsesc.
   ---------------------------------------------------------------- */
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Utilizatori' AND COLUMN_NAME = 'tentative_esuate'
)
BEGIN
    ALTER TABLE Utilizatori
        ADD tentative_esuate INT NOT NULL DEFAULT 0;
    PRINT 'Coloana tentative_esuate adaugata in Utilizatori.';
END
ELSE
BEGIN
    PRINT 'Coloana tentative_esuate exista deja — omisa.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Utilizatori' AND COLUMN_NAME = 'blocat_pana_la'
)
BEGIN
    ALTER TABLE Utilizatori
        ADD blocat_pana_la DATETIME NULL;
    PRINT 'Coloana blocat_pana_la adaugata in Utilizatori.';
END
ELSE
BEGIN
    PRINT 'Coloana blocat_pana_la exista deja — omisa.';
END
GO

/* ----------------------------------------------------------------
   d) View vw_SesiuniComplete (idempotent: stub + ALTER)
   ---------------------------------------------------------------- */
IF OBJECT_ID('vw_SesiuniComplete', 'V') IS NULL
    EXEC sp_executesql N'CREATE VIEW vw_SesiuniComplete AS SELECT 1 AS stub;';
GO

ALTER VIEW vw_SesiuniComplete AS
SELECT t.*,
       p.denumire AS echipament,
       c.nume + ' ' + c.prenume AS membru,
       a.nume + ' ' + a.prenume AS operator
FROM Sesiuni t
JOIN Echipamente p ON t.id_echipament = p.id_echipament
JOIN Membri c ON t.id_membru = c.id_membru
JOIN Operatori a ON t.id_operator = a.id_operator;
GO

/* ----------------------------------------------------------------
   e) View vw_EchipamenteDisponibile (idempotent: stub + ALTER)
   ---------------------------------------------------------------- */
IF OBJECT_ID('vw_EchipamenteDisponibile', 'V') IS NULL
    EXEC sp_executesql N'CREATE VIEW vw_EchipamenteDisponibile AS SELECT 1 AS stub;';
GO

ALTER VIEW vw_EchipamenteDisponibile AS
SELECT p.*,
       b.nume_sala,
       a.nume + ' ' + a.prenume AS operator_sef
FROM Echipamente p
JOIN Sali b ON p.sala_id = b.id_sala
LEFT JOIN Operatori a ON b.operator_sef_id = a.id_operator
WHERE p.status = 'Liber';
GO

PRINT 'Permisiuni.sql finalizat cu succes.';
GO
