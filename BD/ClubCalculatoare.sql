/* ================================================================
   SISTEM DE GESTIUNE CLUB DE CALCULATOARE — SCRIPT COMPLET
   Baza de date: ClubCalculatoare
   ================================================================
   Rulați acest script integral în SQL Server Management Studio.
   Scriptul este autosuficient: creează baza de date (dacă lipsește),
   tabelele, datele demo, view-urile, apoi verificările finale.

   CUPRINS:
     1. Creare bază de date (dacă nu există)
     2. Curățare obiecte existente (DROP FK / DROP VIEW / DROP TABLE)
     3. Creare tabele: Operatori, Sali, Echipamente, Membri,
                        Sesiuni, Rezervari
     4. Inserare date demo
     5. Creare view-uri
     6. Cele 10 cerințe (interogări complexe)
     7. Tabelul Utilizatori + conturi implicite (autentificare)
     7b. Tabelul Permisiuni + UtilizatoriPermisiuni + drepturi demo
     8. Tabelul AuditLog + coloana email (Q5 / Q13)
     9. Verificare finală

   FIX 2026-09-21:
     - Secțiunea 2 șterge acum mai întâi TOATE constrângerile FK
       care ating tabelele domeniului, rezolvând dependența
       circulară Operatori ↔ Sali și erorile Msg 3726 / 2714 / 2627.
     - Scriptul este idempotent la rerulare peste o bază populată.
   ================================================================ */

/* ----------------------------------------------------------------
   1. CREARE BAZĂ DE DATE
   ---------------------------------------------------------------- */
IF DB_ID('ClubCalculatoare') IS NULL
BEGIN
    CREATE DATABASE ClubCalculatoare;
    PRINT 'Baza de date ClubCalculatoare a fost creata.';
END
ELSE
    PRINT 'Baza de date ClubCalculatoare exista deja.';
GO

USE ClubCalculatoare;
GO

/* ----------------------------------------------------------------
   2. ȘTERGERE OBIECTE EXISTENTE
   ---------------------------------------------------------------- */

/* ── 2a. View-urile domeniului clubului ─────────────────────── */
DROP VIEW IF EXISTS vw_MembriCuSesiuni;
DROP VIEW IF EXISTS vw_EchipamenteDisponibile;
DROP VIEW IF EXISTS vw_SesiuniComplete;
DROP VIEW IF EXISTS vw_EchipamenteComplete;
GO

/* Curățare opțională a vechiului domeniu sportiv */
DROP VIEW IF EXISTS vw_RezultatePeEchipa;
DROP VIEW IF EXISTS vw_Fotbalisti;
DROP VIEW IF EXISTS vw_RezultateComplete;
DROP VIEW IF EXISTS vw_SportiviEchipaAntrenor;
GO

/* ── 2b. ȘTERGERE CONSTRÂNGERI FK (rezolvă circularitatea
       Operatori ↔ Sali și orice altă referință încrucișată
       care bloca DROP TABLE cu Msg 3726). ────────────────────── */
DECLARE @dropFK NVARCHAR(MAX) = N'';

SELECT @dropFK = @dropFK
    + N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id))
    + N'.' + QUOTENAME(OBJECT_NAME(fk.parent_object_id))
    + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';' + CHAR(10)
FROM sys.foreign_keys fk
WHERE OBJECT_NAME(fk.parent_object_id) IN
        ('Operatori','Sali','Echipamente','Membri','Sesiuni','Rezervari')
   OR OBJECT_NAME(fk.referenced_object_id) IN
        ('Operatori','Sali','Echipamente','Membri','Sesiuni','Rezervari');

IF LEN(@dropFK) > 0
BEGIN
    PRINT '--- Stergere constrangeri FK existente ---';
    PRINT @dropFK;
    EXEC sp_executesql @dropFK;
END
ELSE
    PRINT 'Nu exista constrangeri FK de sters.';
GO

/* ── 2c. Acum tabelele se pot șterge în orice ordine ─────────── */
DROP TABLE IF EXISTS Rezervari;
DROP TABLE IF EXISTS Sesiuni;
DROP TABLE IF EXISTS Echipamente;
DROP TABLE IF EXISTS Sali;
DROP TABLE IF EXISTS Operatori;
DROP TABLE IF EXISTS Membri;
GO

/* Tabele din vechiul domeniu sportiv */
DROP TABLE IF EXISTS Rezultate;
DROP TABLE IF EXISTS Sportivi;
DROP TABLE IF EXISTS Competitii;
DROP TABLE IF EXISTS Echipe;
DROP TABLE IF EXISTS Antrenori;
GO

/* ATENȚIE: tabelele Utilizatori și AuditLog NU se șterg niciodată
   (conțin conturile de acces și istoricul modificărilor). */

/* ----------------------------------------------------------------
   3. CREARE TABELE
   ---------------------------------------------------------------- */

/* ── Operatori (înlocuiește Antrenori) ────────────────────────────── */
CREATE TABLE Operatori (
    id_operator        INT PRIMARY KEY IDENTITY(1,1),
    nume            NVARCHAR(50)  NOT NULL,
    prenume         NVARCHAR(50)  NOT NULL,
    rol_club    NVARCHAR(50)  NOT NULL,
    telefon         NVARCHAR(15),
    email           NVARCHAR(100),
    data_angajare   DATE,
    sala_id        INT NULL
);
GO

/* ── Sali (înlocuiește Echipe) ──────────────────────────────── */
CREATE TABLE Sali (
    id_sala          INT PRIMARY KEY IDENTITY(1,1),
    nume_sala        NVARCHAR(50)  NOT NULL,
    oras              NVARCHAR(50)  NOT NULL,
    adresa            NVARCHAR(100),
    operator_sef_id      INT NOT NULL,
    data_deschidere   DATE,
    FOREIGN KEY (operator_sef_id) REFERENCES Operatori(id_operator)
        ON DELETE NO ACTION
        ON UPDATE NO ACTION
);
GO

/* Completare referință circulară: operatorul → salal lui */
ALTER TABLE Operatori
    ADD CONSTRAINT FK_Operatori_Sali FOREIGN KEY (sala_id)
        REFERENCES Sali(id_sala)
        ON DELETE NO ACTION
        ON UPDATE NO ACTION;
GO

/* ── Echipamente (înlocuiește Sportivi) ────────────────────────── */
CREATE TABLE Echipamente (
    id_echipament   INT PRIMARY KEY IDENTITY(1,1),
    tip_echipament  NVARCHAR(30)  NOT NULL,
    denumire         NVARCHAR(150) NOT NULL,
    oras             NVARCHAR(50)  NOT NULL,
    putere_watt     DECIMAL(8,2)  NOT NULL,
    ram_gb     INT,
    numar_locuri             INT,
    an_achizitie   INT,
    tarif_ora             DECIMAL(12,2) NOT NULL,
    status           NVARCHAR(20)  NOT NULL,
    sala_id         INT NOT NULL,
    data_adaugare    DATE NOT NULL DEFAULT GETDATE(),
    descriere        NVARCHAR(500),
    FOREIGN KEY (sala_id) REFERENCES Sali(id_sala)
        ON DELETE NO ACTION
        ON UPDATE NO ACTION
);
GO

/* ── Membri (înlocuiește Competitii) ──────────────────────────── */
CREATE TABLE Membri (
    id_membru          INT PRIMARY KEY IDENTITY(1,1),
    nume               NVARCHAR(50)  NOT NULL,
    prenume            NVARCHAR(50)  NOT NULL,
    telefon            NVARCHAR(15)  NOT NULL,
    email              NVARCHAR(100),
    tip_membru         NVARCHAR(20)  NOT NULL,
    buget_max          DECIMAL(12,2),
    oras_interes       NVARCHAR(50),
    data_inregistrare  DATE NOT NULL DEFAULT GETDATE()
);
GO

/* ── Sesiuni (înlocuiește Rezultate) ────────────────────────── */
CREATE TABLE Sesiuni (
    id_sesiune    INT PRIMARY KEY IDENTITY(1,1),
    id_echipament   INT NOT NULL,
    id_membru        INT NOT NULL,
    id_operator         INT NOT NULL,
    tip_sesiune   NVARCHAR(20)  NOT NULL,
    data_sesiune  DATE NOT NULL,
    durata_minute INT NULL,
    cost_total       DECIMAL(12,2) NOT NULL,
    incasare         DECIMAL(10,2) NOT NULL,
    status           NVARCHAR(20)  NOT NULL,
    observatii       NVARCHAR(200),
    FOREIGN KEY (id_echipament) REFERENCES Echipamente(id_echipament)
        ON DELETE NO ACTION ON UPDATE NO ACTION,
    FOREIGN KEY (id_membru)      REFERENCES Membri(id_membru)
        ON DELETE NO ACTION ON UPDATE NO ACTION,
    FOREIGN KEY (id_operator)       REFERENCES Operatori(id_operator)
        ON DELETE NO ACTION ON UPDATE NO ACTION
);
GO

/* ── Rezervari (tabel nou) ─────────────────────────────────────── */
CREATE TABLE Rezervari (
    id_rezervare     INT PRIMARY KEY IDENTITY(1,1),
    id_echipament   INT NOT NULL,
    id_membru        INT NOT NULL,
    id_operator         INT NOT NULL,
    data_rezervare   DATETIME NOT NULL,
    status           NVARCHAR(20),
    observatii       NVARCHAR(200),
    FOREIGN KEY (id_echipament) REFERENCES Echipamente(id_echipament)
        ON DELETE NO ACTION ON UPDATE NO ACTION,
    FOREIGN KEY (id_membru)      REFERENCES Membri(id_membru)
        ON DELETE NO ACTION ON UPDATE NO ACTION,
    FOREIGN KEY (id_operator)       REFERENCES Operatori(id_operator)
        ON DELETE NO ACTION ON UPDATE NO ACTION
);
GO

/* ----------------------------------------------------------------
   4. INSERARE DATE DEMO
   ---------------------------------------------------------------- */

/* ── 10 agenți ─────────────────────────────────────────────────── */
SET IDENTITY_INSERT Operatori ON;
INSERT INTO Operatori (id_operator, nume, prenume, rol_club, telefon, email, data_angajare, sala_id) VALUES
(1,  'Popescu',    'Ion',      'Operator Gaming', '0712345678', 'ion.popescu@clubcalculatoare.ro',      '2015-03-10', NULL),
(2,  'Ionescu',    'Maria',    'Casier',   '0723456789', 'maria.ionescu@clubcalculatoare.ro',    '2017-06-01', NULL),
(3,  'Vasilescu',  'George',   'Tehnician',    '0734567890', 'george.vasilescu@clubcalculatoare.ro', '2016-02-15', NULL),
(4,  'Dumitrescu', 'Elena',    'Supervizor',         '0745678901', 'elena.dumitrescu@clubcalculatoare.ro', '2019-09-20', NULL),
(5,  'Stoica',     'Andrei',   'Operator Gaming', '0756789012', 'andrei.stoica@clubcalculatoare.ro',    '2018-04-11', NULL),
(6,  'Radu',       'Cristina', 'Casier',   '0767890123', 'cristina.radu@clubcalculatoare.ro',    '2020-01-07', NULL),
(7,  'Marin',      'Alexandru','Supervizor',         '0778901234', 'alexandru.marin@clubcalculatoare.ro',  '2014-11-30', NULL),
(8,  'Stan',       'Ioana',    'Operator Gaming', '0789012345', 'ioana.stan@clubcalculatoare.ro',       '2021-05-19', NULL),
(9,  'Gheorghe',   'Mihai',    'Tehnician',    '0790123456', 'mihai.gheorghe@clubcalculatoare.ro',   '2013-08-05', NULL),
(10, 'Barbu',      'Ana',      'Casier',   '0701234567', 'ana.barbu@clubcalculatoare.ro',        '2022-03-28', NULL);
SET IDENTITY_INSERT Operatori OFF;
GO

/* ── 10 sali ────────────────────────────────────────────────── */
SET IDENTITY_INSERT Sali ON;
INSERT INTO Sali (id_sala, nume_sala, oras, adresa, operator_sef_id, data_deschidere) VALUES
(1,  'Sala Central',    'Bucuresti',   'Calea Victoriei 12',      1,  '2010-01-15'),
(2,  'Sala Dorobanti',  'Bucuresti',   'Bd. Dorobanti 45',        2,  '2012-05-20'),
(3,  'Sala Cluj',       'Cluj-Napoca', 'Str. Memorandumului 8',   3,  '2011-09-01'),
(4,  'Sala Timisoara',  'Timisoara',   'Piata Victoriei 4',       4,  '2013-03-12'),
(5,  'Sala Iasi',       'Iasi',        'Str. Palat 21',           5,  '2015-07-07'),
(6,  'Sala Brasov',     'Brasov',      'Str. Republicii 33',      6,  '2016-10-10'),
(7,  'Sala Lux Nord',   'Bucuresti',   'Sos. Primaverii 5',       7,  '2014-02-02'),
(8,  'Sala Oradea',     'Oradea',      'Str. Independentei 9',    8,  '2018-06-18'),
(9,  'Sala Constanta',  'Constanta',   'Bd. Mamaia 77',           9,  '2017-04-25'),
(10, 'Sala Sibiu',      'Sibiu',       'Str. Nicolae Balcescu 15', 10, '2019-11-11');
SET IDENTITY_INSERT Sali OFF;
GO

/* Legăm fiecare operator de salal în care lucrează */
UPDATE Operatori SET sala_id = 1  WHERE id_operator = 1;
UPDATE Operatori SET sala_id = 2  WHERE id_operator = 2;
UPDATE Operatori SET sala_id = 3  WHERE id_operator = 3;
UPDATE Operatori SET sala_id = 4  WHERE id_operator = 4;
UPDATE Operatori SET sala_id = 5  WHERE id_operator = 5;
UPDATE Operatori SET sala_id = 6  WHERE id_operator = 6;
UPDATE Operatori SET sala_id = 7  WHERE id_operator = 7;
UPDATE Operatori SET sala_id = 8  WHERE id_operator = 8;
UPDATE Operatori SET sala_id = 9  WHERE id_operator = 9;
UPDATE Operatori SET sala_id = 10 WHERE id_operator = 10;
GO

/* ── 10 clienți ────────────────────────────────────────────────── */
SET IDENTITY_INSERT Membri ON;
INSERT INTO Membri (id_membru, nume, prenume, telefon, email, tip_membru, buget_max, oras_interes, data_inregistrare) VALUES
(1,  'Georgescu', 'Andrei',  '0722111222', 'andrei.georgescu@email.com', 'Standard', 150000,  'Bucuresti',   '2024-01-12'),
(2,  'Munteanu',  'Vlad',    '0733222333', 'vlad.munteanu@email.com',    'Standard',  90000,  'Cluj-Napoca', '2024-02-03'),
(3,  'Popa',      'Darius',  '0744333444', 'darius.popa@email.com',      'Companie',    NULL,   'Bucuresti',   '2024-02-20'),
(4,  'Dobre',     'Robert',  '0755444555', 'robert.dobre@email.com',     'Student',      1500,  'Iasi',        '2024-03-08'),
(5,  'Tudor',     'Raluca',  '0766555666', 'raluca.tudor@email.com',     'Premium',  NULL,   'Timisoara',   '2024-03-27'),
(6,  'Lungu',     'Cristian','0777666777', 'cristian.lungu@email.com',   'Standard', 220000,  'Bucuresti',   '2024-04-14'),
(7,  'Barbu',     'Mihaela', '0788777888', 'mihaela.barbu@email.com',    'Student',       800,  'Brasov',      '2024-05-09'),
(8,  'Florea',    'Alexandru','0799888999','alexandru.florea@email.com', 'Standard',  60000,  'Oradea',      '2024-06-02'),
(9,  'Neagu',     'Ana',     '0711999000', 'ana.neagu@email.com',        'Premium',  NULL,   'Constanta',   '2024-06-21'),
(10, 'Costache',  'Ionut',   '0722000111', 'ionut.costache@email.com',   'Standard', 130000,  'Sibiu',       '2024-07-15');
SET IDENTITY_INSERT Membri OFF;
GO

/* ── 10 echipamente ──────────────────────────────────────────── */
SET IDENTITY_INSERT Echipamente ON;
INSERT INTO Echipamente (id_echipament, tip_echipament, denumire, oras, putere_watt, ram_gb, numar_locuri, an_achizitie, tarif_ora, status, sala_id, data_adaugare, descriere) VALUES
(1,  'PC Gaming', 'PC-01 — Str. Mihai Viteazu 10, ap. 4', 'Bucuresti',   650.00, 32,   1,    2023, 145, 'Liber', 1,  '2024-02-10', 'PC gaming RTX 4070, monitor 240Hz, scaun ergonomic'),
(2,  'Consola',   'CON-01 — Str. Lalelelor 22',           'Cluj-Napoca', 220.00, 16,   NULL, 2022, 320, 'Ocupat',     3,  '2024-03-05', 'Consola PS5 + TV 55 inch, canapea 2 locuri'),
(3,  'Server',    'SRV-01 — Str. Campului 5',             'Timisoara',   500.00, NULL, NULL, NULL,  75, 'Liber', 4,  '2024-01-20', 'Server dedicat gazduire jocuri, rack 12U'),
(4,  'Simulator', 'SIM-01 — Bd. Unirii 30',               'Bucuresti',   350.00, 16,   1,    2021, 250, 'Rezervat',  2,  '2024-04-15', 'Simulator curse cu volan direct-drive'),
(5,  'PC Gaming', 'PC-02 — Str. Pacurari 88, ap. 12',    'Iasi',        600.00, 32,   1,    2022,  92, 'Ocupat',     5,  '2024-05-02', 'PC gaming RTX 4060, monitor 165Hz'),
(6,  'VR',        'VR-01 — Str. Republicii 14, et. 3',   'Brasov',       95.00, 16,   1,    2023, 175, 'În service',   6,  '2024-06-11', 'Casca VR Meta Quest 3, spatiu de joc 4x4m'),
(7,  'PC Gaming', 'PC-03 — Calea Bucuresti 120, ap. 7',  'Oradea',      620.00, 32,   1,    2023, 118, 'Ocupat',     8,  '2024-07-19', 'PC gaming RTX 4060 Ti, monitor 180Hz'),
(8,  'Consola',   'CON-02 — Str. Primaverii 3',          'Constanta',   220.00, 16,   NULL, 2023, 410, 'Liber', 9,  '2024-08-23', 'Consola Xbox Series X + TV 65 inch'),
(9,  'PC Gaming', 'PC-04 — Str. Lunga 45, ap. 2',        'Brasov',       580.00, 16,   1,    2020,  88, 'Rezervat',  6,  '2024-09-09', 'PC lobby, monitor 144Hz, potrivit pentru cursuri'),
(10, 'Server',    'SRV-02 — Str. Plopilor 60',            'Sibiu',       750.00, NULL, NULL, NULL, 130, 'Liber', 10, '2025-01-15', 'Server backup + statie de streaming');
SET IDENTITY_INSERT Echipamente OFF;
GO

/* ── 10 sesiuni ────────────────────────────────────────────────── */
SET IDENTITY_INSERT Sesiuni ON;
INSERT INTO Sesiuni (id_sesiune, id_echipament, id_membru, id_operator, tip_sesiune, data_sesiune, durata_minute, cost_total, incasare, status, observatii) VALUES
(1,  1,  1,  1,  'Joc',       '2024-03-01', 120, 142, 4.26, 'Finalizata',   'Plata cu cardul aprobata'),
(2,  2,  2,  3,  'Joc',       '2024-03-20',  90, 310, 9.30, 'Finalizata',   'Sesiune cu plata integrala'),
(3,  3,  5,  4,  'Joc',       '2024-04-10',  60,  72, 2.16, 'În așteptare', 'Se asteapta confirmarea platii'),
(4,  4,  6,  2,  'Internet',  '2024-05-05', 300, 2.50, 1.25, 'Finalizata',  'Abonament internet 5 ore'),
(5,  5,  1,  5,  'Joc',       '2024-06-01', 110,  90, 2.70, 'Finalizata',   'Membru multumit de statie'),
(6,  6,  7,  6,  'Eveniment', '2024-06-25',  45, 175, 1.75, 'În așteptare', 'Eveniment privat, avans achitat'),
(7,  7,  1,  8,  'Joc',       '2024-08-01', 150, 115, 3.45, 'Finalizata',   'Turneu de seara, 3 participanti'),
(8,  9,  1,  6,  'Joc',       '2024-09-20',  30,  86, 2.58, 'Finalizata',   'Sesiune expres la pranz'),
(9,  1,  6,  10, 'Joc',       '2024-10-05', 130, 142, 4.26, 'Finalizata',   'A doua sesiune pe aceasta statie'),
(10, 5,  4,  2,  'Internet',  '2024-11-12', 240,  90, 0.90, 'Finalizata',   'Membru nou, pachet 4 ore');
SET IDENTITY_INSERT Sesiuni OFF;
GO

/* ── 8 rezervari ───────────────────────────────────────────────── */
SET IDENTITY_INSERT Rezervari ON;
INSERT INTO Rezervari (id_rezervare, id_echipament, id_membru, id_operator, data_rezervare, status, observatii) VALUES
(1, 1,  6,  10, '2024-10-01 11:00:00', 'Efectuata',  'Membru interesat de statia PC-01'),
(2, 2,  2,  3,  '2024-03-15 10:30:00', 'Efectuata',  'Rezervare finalizata cu oferta de abonament'),
(3, 4,  6,  2,  '2024-04-28 16:00:00', 'Efectuata',  'Simulator potrivit cerintelor grupului'),
(4, 5,  1,  5,  '2024-05-25 09:00:00', 'Efectuata',  'A doua rezervare a acestei statii'),
(5, 6,  7,  6,  '2026-10-05 12:00:00', 'Programata', 'Se asteapta confirmarea membrului'),
(6, 8,  9,  9,  '2026-10-12 14:00:00', 'Programata', 'Rezervare statie VR pentru weekend'),
(7, 9,  1,  6,  '2025-09-15 17:30:00', 'Efectuata',  'Membru multumit de statie'),
(8, 10, 8,  10, '2026-09-01 10:00:00', 'Anulata',    'Membrul a renuntat la rezervare');
SET IDENTITY_INSERT Rezervari OFF;
GO

/* ----------------------------------------------------------------
   5. CREARE VIEW-URI
   ---------------------------------------------------------------- */

CREATE VIEW vw_EchipamenteComplete AS
SELECT
    p.id_echipament,
    p.tip_echipament,
    p.denumire,
    p.oras,
    p.putere_watt,
    p.ram_gb,
    p.numar_locuri,
    p.an_achizitie,
    p.tarif_ora,
    p.status,
    p.data_adaugare,
    p.descriere,
    b.id_sala,
    b.nume_sala,
    b.oras                      AS oras_sala,
    a.nume + ' ' + a.prenume    AS operator_sef,
    a.rol_club              AS rol_operator
FROM Echipamente p
JOIN Sali b ON p.sala_id = b.id_sala
LEFT JOIN Operatori a ON b.operator_sef_id = a.id_operator;
GO

CREATE VIEW vw_SesiuniComplete AS
SELECT
    t.id_sesiune,
    t.id_echipament,
    p.denumire                    AS echipament,
    p.tip_echipament,
    p.oras                      AS oras_echipament,
    t.id_membru,
    c.nume + ' ' + c.prenume    AS membru,
    t.id_operator,
    a.nume + ' ' + a.prenume    AS operator,
    b.nume_sala                AS sala,
    t.tip_sesiune,
    t.data_sesiune,
    t.cost_total,
    t.incasare,
    t.status,
    t.observatii
FROM Sesiuni t
JOIN Echipamente p ON t.id_echipament = p.id_echipament
JOIN Membri     c ON t.id_membru      = c.id_membru
JOIN Operatori      a ON t.id_operator       = a.id_operator
LEFT JOIN Sali b ON a.sala_id      = b.id_sala;
GO

CREATE VIEW vw_EchipamenteDisponibile AS
SELECT
    p.id_echipament,
    p.tip_echipament,
    p.denumire,
    p.oras,
    p.putere_watt,
    p.ram_gb,
    p.numar_locuri,
    p.an_achizitie,
    p.tarif_ora,
    p.status,
    p.data_adaugare,
    b.nume_sala,
    a.nume + ' ' + a.prenume AS operator_sef
FROM Echipamente p
JOIN Sali b ON p.sala_id = b.id_sala
LEFT JOIN Operatori a ON b.operator_sef_id = a.id_operator
WHERE p.status = 'Liber';
GO

CREATE VIEW vw_MembriCuSesiuni AS
SELECT
    c.id_membru,
    c.nume + ' ' + c.prenume    AS membru,
    c.tip_membru,
    c.oras_interes,
    COUNT(t.id_sesiune)      AS numar_sesiuni,
    ISNULL(SUM(t.cost_total), 0) AS total_cost,
    ISNULL(SUM(t.incasare), 0)   AS total_incasare
FROM Membri c
LEFT JOIN Sesiuni t ON c.id_membru = t.id_membru
GROUP BY c.id_membru, c.nume, c.prenume, c.tip_membru, c.oras_interes;
GO

/* ===================================================== */
/* CERINȚE PROFESOR - INTEROGĂRI FINALIZATE              */
/* ===================================================== */

PRINT '=== CERINȚA 1: Operatorii salilor cu incasare total peste media generală ===';
WITH IncasariSali AS (
    SELECT a.sala_id, SUM(t.incasare) AS total_incasare
    FROM Operatori a
    JOIN Sesiuni t ON a.id_operator = t.id_operator
    WHERE a.sala_id IS NOT NULL
    GROUP BY a.sala_id
),
MediaGenerala AS (
    SELECT AVG(1.0 * total_incasare) AS medie_incasare
    FROM IncasariSali
)
SELECT DISTINCT a.id_operator, a.nume, a.prenume, a.rol_club,
       b.nume_sala, cb.total_incasare
FROM Operatori a
JOIN Sali b ON a.sala_id = b.id_sala
JOIN IncasariSali cb ON b.id_sala = cb.sala_id
CROSS JOIN MediaGenerala mg
WHERE cb.total_incasare > mg.medie_incasare
ORDER BY cb.total_incasare DESC;
GO

PRINT '=== CERINȚA 2: Pentru fiecare echipament: denumire, sala, numar sesiuni, cost maxim ===';
SELECT
    p.id_echipament,
    p.denumire,
    p.tip_echipament,
    b.nume_sala                       AS Sala,
    COUNT(DISTINCT t.id_sesiune)    AS NumarSesiuni,
    ISNULL(MAX(t.cost_total), 0)       AS CostMaxim
FROM Echipamente p
JOIN Sali b ON p.sala_id = b.id_sala
LEFT JOIN Sesiuni t ON p.id_echipament = t.id_echipament
GROUP BY p.id_echipament, p.denumire, p.tip_echipament, b.nume_sala
ORDER BY NumarSesiuni DESC, CostMaxim DESC;
GO

PRINT '=== CERINȚA 3: Membrii care au folosit TOATE statiile ''PC Gaming'' din 2024 ===';
DECLARE @TotalPCGaming2024 INT;
SELECT @TotalPCGaming2024 = COUNT(*)
FROM Echipamente
WHERE tip_echipament = 'PC Gaming' AND YEAR(data_adaugare) = 2024;

SELECT c.id_membru, c.nume, c.prenume,
       COUNT(DISTINCT t.id_echipament) AS StatiiFolosite
FROM Membri c
JOIN Sesiuni t ON c.id_membru = t.id_membru
JOIN Echipamente p ON t.id_echipament = p.id_echipament
WHERE p.tip_echipament = 'PC Gaming' AND YEAR(p.data_adaugare) = 2024
GROUP BY c.id_membru, c.nume, c.prenume
HAVING COUNT(DISTINCT t.id_echipament) = @TotalPCGaming2024;
GO

PRINT '=== CERINȚA 4: Clasament sali dupa incasare totala + diferenta fata de lider ===';
WITH IncasariSali AS (
    SELECT
        b.id_sala,
        b.nume_sala,
        COALESCE(SUM(t.incasare), 0) AS total_incasare
    FROM Sali b
    LEFT JOIN Operatori a     ON b.id_sala = a.sala_id
    LEFT JOIN Sesiuni t ON a.id_operator = t.id_operator
    GROUP BY b.id_sala, b.nume_sala
)
SELECT
    nume_sala                                                        AS Sala,
    total_incasare                                                    AS IncasareTotal,
    RANK() OVER (ORDER BY total_incasare DESC)                        AS Pozitie,
    total_incasare - FIRST_VALUE(total_incasare)
        OVER (ORDER BY total_incasare DESC)                           AS DiferentaFataDeLider
FROM IncasariSali
ORDER BY total_incasare DESC;
GO

PRINT '=== CERINȚA 5: Perechi de membri din sali diferite, acelasi echipament, acelasi cost ===';
SELECT
    c1.nume + ' ' + c1.prenume AS Membru1,
    ISNULL(b1.nume_sala, '-') AS Sala1,
    c2.nume + ' ' + c2.prenume AS Membru2,
    ISNULL(b2.nume_sala, '-') AS Sala2,
    p.denumire                   AS Echipament,
    t1.cost_total              AS CostTotal
FROM Sesiuni t1
JOIN Sesiuni t2 ON t1.id_echipament = t2.id_echipament
                  AND t1.cost_total     = t2.cost_total
                  AND t1.id_membru      < t2.id_membru
JOIN Membri c1 ON t1.id_membru = c1.id_membru
JOIN Membri c2 ON t2.id_membru = c2.id_membru
JOIN Operatori  a1 ON t1.id_operator  = a1.id_operator
JOIN Operatori  a2 ON t2.id_operator  = a2.id_operator
JOIN Echipamente p ON t1.id_echipament = p.id_echipament
LEFT JOIN Sali b1 ON a1.sala_id = b1.id_sala
LEFT JOIN Sali b2 ON a2.sala_id = b2.id_sala
WHERE a1.sala_id <> a2.sala_id
ORDER BY p.denumire, t1.cost_total DESC;
GO

PRINT '=== CERINȚA 6: Operatori cu numar de echipamente gestionate peste media generala ===';
WITH EchipamenteDistincte AS (
    SELECT a.id_operator, a.nume, a.prenume, p.id_echipament
    FROM Operatori a
    JOIN Echipamente p ON a.sala_id = p.sala_id
    UNION
    SELECT a.id_operator, a.nume, a.prenume, p.id_echipament
    FROM Operatori a
    JOIN Sesiuni t ON a.id_operator = t.id_operator
    JOIN Echipamente p ON t.id_echipament = p.id_echipament
),
NumarEchipamente AS (
    SELECT id_operator, nume, prenume, COUNT(*) AS nr_echipamente
    FROM EchipamenteDistincte
    GROUP BY id_operator, nume, prenume
),
MediaEchipamente AS (
    SELECT AVG(1.0 * nr_echipamente) AS medie FROM NumarEchipamente
)
SELECT np.id_operator, np.nume, np.prenume, np.nr_echipamente, mp.medie AS MedieGenerala
FROM NumarEchipamente np
CROSS JOIN MediaEchipamente mp
WHERE np.nr_echipamente > mp.medie
ORDER BY np.nr_echipamente DESC;
GO

PRINT '=== CERINȚA 7: Echipamente fara nicio rezervare inregistrata ===';
SELECT p.id_echipament, p.tip_echipament, p.denumire, p.oras, p.tarif_ora, p.status
FROM Echipamente p
LEFT JOIN Rezervari v ON p.id_echipament = v.id_echipament
WHERE v.id_rezervare IS NULL;
GO

PRINT '=== CERINȚA 8: Numar sesiuni si suma incasari pe luni calendaristice ===';
WITH Luni AS (
    SELECT 1 AS Luna UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION SELECT 5 UNION SELECT 6
    UNION SELECT 7 UNION SELECT 8 UNION SELECT 9 UNION SELECT 10 UNION SELECT 11 UNION SELECT 12
)
SELECT
    l.Luna,
    DATENAME(MONTH, DATEFROMPARTS(2024, l.Luna, 1)) AS NumeLuna,
    COUNT(DISTINCT t.id_sesiune)                 AS NumarSesiuni,
    ISNULL(SUM(t.incasare), 0)                      AS TotalIncasare
FROM Luni l
LEFT JOIN Sesiuni t ON MONTH(t.data_sesiune) = l.Luna AND YEAR(t.data_sesiune) = 2024
GROUP BY l.Luna
ORDER BY l.Luna;
GO

PRINT '=== CERINȚA 9: Operatorul cu cea mai mare crestere a incasarii intre doua luni consecutive ===';
WITH SesiuniLunare AS (
    SELECT
        t.id_operator,
        a.nume + ' ' + a.prenume AS NumeOperator,
        DATEFROMPARTS(YEAR(t.data_sesiune), MONTH(t.data_sesiune), 1) AS luna,
        SUM(t.incasare)          AS incasare_lunar,
        LAG(SUM(t.incasare)) OVER (
            PARTITION BY t.id_operator
            ORDER BY DATEFROMPARTS(YEAR(t.data_sesiune), MONTH(t.data_sesiune), 1)
        ) AS incasare_anterior,
        LAG(DATEFROMPARTS(YEAR(t.data_sesiune), MONTH(t.data_sesiune), 1)) OVER (
            PARTITION BY t.id_operator
            ORDER BY DATEFROMPARTS(YEAR(t.data_sesiune), MONTH(t.data_sesiune), 1)
        ) AS luna_anterioara
    FROM Sesiuni t
    JOIN Operatori a ON t.id_operator = a.id_operator
    GROUP BY t.id_operator, a.nume, a.prenume,
             YEAR(t.data_sesiune), MONTH(t.data_sesiune)
),
Diferente AS (
    SELECT *,
           incasare_lunar - incasare_anterior AS crestere
    FROM SesiuniLunare
    WHERE incasare_anterior IS NOT NULL
)
SELECT TOP 1 WITH TIES
    NumeOperator,
    luna_anterioara  AS LunaPrecedenta,
    incasare_anterior AS IncasarePrecedent,
    luna             AS LunaUrmatoare,
    incasare_lunar   AS IncasareUrmator,
    crestere         AS CrestereIncasare
FROM Diferente
ORDER BY crestere DESC;
GO

PRINT '=== CERINȚA 10: Primii 3 operatori din fiecare sala dupa incasarea totala ===';
WITH IncasareOperatori AS (
    SELECT
        a.id_operator,
        a.nume + ' ' + a.prenume AS NumeOperator,
        b.nume_sala,
        ISNULL(SUM(t.incasare), 0) AS total_incasare
    FROM Operatori a
    JOIN Sali b ON a.sala_id = b.id_sala
    LEFT JOIN Sesiuni t ON a.id_operator = t.id_operator
    GROUP BY a.id_operator, a.nume, a.prenume, b.nume_sala
),
ClasamentPeSala AS (
    SELECT *,
           ROW_NUMBER() OVER (PARTITION BY nume_sala ORDER BY total_incasare DESC) AS rank_in_sala
    FROM IncasareOperatori
)
SELECT nume_sala AS Sala, NumeOperator, total_incasare AS IncasareTotal, rank_in_sala AS LocClasament
FROM ClasamentPeSala
WHERE rank_in_sala <= 3
ORDER BY nume_sala, rank_in_sala;
GO

/* ----------------------------------------------------------------
   7. TABELUL Utilizatori + CONTURI IMPLICITE
   ---------------------------------------------------------------- */
IF OBJECT_ID('Utilizatori', 'U') IS NULL
BEGIN
    CREATE TABLE Utilizatori (
        id_utilizator        INT           PRIMARY KEY IDENTITY(1,1),
        username             NVARCHAR(50)  NOT NULL UNIQUE,
        parola_hash          NVARCHAR(256) NOT NULL,
        rol                  NVARCHAR(20)  NOT NULL
                             CHECK (rol IN ('Admin', 'User')),
        nume_complet         NVARCHAR(100) NOT NULL,
        activ                BIT           NOT NULL DEFAULT 1,
        data_creare          DATETIME      NOT NULL DEFAULT GETDATE(),
        ultima_autentificare DATETIME      NULL
    );

    PRINT 'Tabelul Utilizatori a fost creat cu succes.';
END
ELSE
    PRINT 'Tabelul Utilizatori exista deja.';
GO

/* Coloana email (Q13 — resetarea parolei prin email) */
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Utilizatori' AND COLUMN_NAME = 'email'
)
BEGIN
    ALTER TABLE Utilizatori ADD email NVARCHAR(100) NULL;
    PRINT 'Coloana email adaugata in tabelul Utilizatori.';
END
GO

/* ── Coloane pentru blocare persistentă (brut-force) ───────────── */
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Utilizatori' AND COLUMN_NAME = 'tentative_esuate'
)
    ALTER TABLE Utilizatori ADD tentative_esuate INT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Utilizatori' AND COLUMN_NAME = 'blocat_pana_la'
)
    ALTER TABLE Utilizatori ADD blocat_pana_la DATETIME NULL;
GO

/* ── Conturi implicite — HASH CORECT ───────────────────────────────
   FIX: style 2 este definit doar pentru VARCHAR, nu pentru NVARCHAR.
   Folosim VARCHAR(64) ca tip țintă și VARBINARY(50) explicit.       */
DECLARE @hash_admin NVARCHAR(256);
DECLARE @hash_user  NVARCHAR(256);

SELECT @hash_admin = LOWER(CONVERT(VARCHAR(64),
    HASHBYTES('SHA2_256', CONVERT(VARBINARY(50), '121212')), 2));
SELECT @hash_user = LOWER(CONVERT(VARCHAR(64),
    HASHBYTES('SHA2_256', CONVERT(VARBINARY(50), '575757')), 2));

-- Sterge atat numele noi, cat si cele vechi (migrare sigura, idempotenta)
DELETE FROM Utilizatori WHERE username IN ('Alexandru', 'Mihail', 'Constantin', 'Maxim');

INSERT INTO Utilizatori (username, parola_hash, rol, nume_complet, email, activ)
VALUES ('Alexandru', @hash_admin, 'Admin', 'Alexandru (Administrator)',
        'admin@clubcalculatoare.ro', 1);

INSERT INTO Utilizatori (username, parola_hash, rol, nume_complet, email, activ)
VALUES ('Mihail', @hash_user, 'User', 'Mihail (Utilizator)',
        'user@clubcalculatoare.ro', 1);

PRINT '  Username: Alexandru | Parola: 121212 | Rol: Admin (Administrator)';
PRINT '  Username: Mihail    | Parola: 575757 | Rol: User (Utilizator)';
GO

/* ----------------------------------------------------------------
   7b. PERMISIUNI INDIVIDUALE (sistem drepturi de acces)
   FIX: fara DROP al UtilizatoriPermisiuni — stergerea neconditionata
   la fiecare rulare stergea toate drepturile configurate manual de
   admin (pierdere de date). Creatiile sunt idempotente (IF NOT EXISTS),
   ca in Permisiuni.sql.
   ---------------------------------------------------------------- */
IF OBJECT_ID('Permisiuni', 'U') IS NULL
BEGIN
    -- FIX: aceeasi forma ca in Permisiuni.sql (cod = PK), ca sa nu existe
    -- doua scheme diferite ale aceluiasi tabel (aici vs Permisiuni.sql).
    CREATE TABLE Permisiuni (
        cod            NVARCHAR(50)  NOT NULL PRIMARY KEY,
        descriere      NVARCHAR(100) NOT NULL
    );

    INSERT INTO Permisiuni (cod, descriere) VALUES
        ('MENIU_ECHIPAMENTE',      'Echipamente — vizualizare'),
        ('MENIU_SALI',          'Sali — vizualizare'),
        ('MENIU_MEMBRI',          'Membri — vizualizare'),
        ('MENIU_SESIUNI',       'Sesiuni — vizualizare'),
        ('MENIU_REZERVARI',        'Rezervari — vizualizare'),
        ('MENIU_OPERATORI',           'Operatori — vizualizare'),
        ('MENIU_RAPOARTE',         'Rapoarte si Statistici'),
        ('PANOU_DASHBOARD',        'Tablou de bord (KPI)'),
        ('MODIFICARE_ECHIPAMENTE', 'Echipamente — adaugare/modificare'),
        ('MODIFICARE_SALI',     'Sali — adaugare/modificare'),
        ('MODIFICARE_MEMBRI',     'Membri — adaugare/modificare'),
        ('MODIFICARE_SESIUNI',  'Sesiuni — adaugare/modificare'),
        ('MODIFICARE_REZERVARI',   'Rezervari — adaugare/modificare'),
        ('MODIFICARE_OPERATORI',      'Operatori — adaugare/modificare'),
        ('STERGE_ECHIPAMENTE',     'Echipamente — stergere'),
        ('STERGE_SALI',         'Sali — stergere'),
        ('STERGE_MEMBRI',         'Membri — stergere'),
        ('STERGE_SESIUNI',      'Sesiuni — stergere'),
        ('STERGE_REZERVARI',       'Rezervari — stergere'),
        ('STERGE_OPERATORI',          'Operatori — stergere'),
        ('GESTIONARE_UTILIZATORI', 'Gestionare Utilizatori');

    PRINT 'Tabelele Permisiuni create — 21 puncte de acces inregistrate.';
END
ELSE
    PRINT 'Tabelul Permisiuni exista deja — omis.';
GO

/* Catalogul se completeaza automat la rerulare (extensibilitate fara cod) */
INSERT INTO Permisiuni (cod, descriere)
SELECT v.cod, v.descriere
FROM (VALUES
    ('PANOU_DASHBOARD',        'Tablou de bord (KPI)'),
    ('MODIFICARE_ECHIPAMENTE', 'Echipamente — adaugare/modificare'),
    ('MODIFICARE_SALI',     'Sali — adaugare/modificare'),
    ('MODIFICARE_MEMBRI',     'Membri — adaugare/modificare'),
    ('MODIFICARE_SESIUNI',  'Sesiuni — adaugare/modificare'),
    ('MODIFICARE_REZERVARI',   'Rezervari — adaugare/modificare'),
    ('MODIFICARE_OPERATORI',      'Operatori — adaugare/modificare'),
    ('STERGE_ECHIPAMENTE',     'Echipamente — stergere'),
    ('STERGE_SALI',         'Sali — stergere'),
    ('STERGE_MEMBRI',         'Membri — stergere'),
    ('STERGE_SESIUNI',      'Sesiuni — stergere'),
    ('STERGE_REZERVARI',       'Rezervari — stergere'),
    ('STERGE_OPERATORI',          'Operatori — stergere')
) AS v(cod, descriere)
WHERE NOT EXISTS (SELECT 1 FROM Permisiuni p WHERE p.cod = v.cod);
GO

IF OBJECT_ID('UtilizatoriPermisiuni', 'U') IS NULL
BEGIN
    CREATE TABLE UtilizatoriPermisiuni (
        id_utilizator    INT NOT NULL,
        cod_permisiune   NVARCHAR(50) NOT NULL,
        acordata_de      NVARCHAR(50) NULL,
        data_acordare    DATETIME NOT NULL DEFAULT GETDATE(),
        PRIMARY KEY (id_utilizator, cod_permisiune),
        FOREIGN KEY (id_utilizator)  REFERENCES Utilizatori(id_utilizator)
            ON DELETE CASCADE ON UPDATE NO ACTION,
        FOREIGN KEY (cod_permisiune) REFERENCES Permisiuni(cod)
            ON DELETE CASCADE ON UPDATE NO ACTION
    );

    PRINT 'Tabelul UtilizatoriPermisiuni a fost creat cu succes.';
END
ELSE
    PRINT 'Tabelul UtilizatoriPermisiuni exista deja — omis.';
GO

/* ── Permisiuni demo pentru contul de USER ───────────────────────
   Politica minima: rolul User vede doar echipamentele (Echipamente +
   Favorite) si isi schimba parola — fara dashboard, fara celelalte
   module, fara modificare/stergere.
   FIX: acordare non-distructiva (doar codurile lipsa) — varianta veche
   (DELETE + INSERT) suprascria la fiecare rulare configurarile facute
   manual de admin pentru contul de User (Mihail). */
INSERT INTO UtilizatoriPermisiuni (id_utilizator, cod_permisiune, acordata_de)
SELECT u.id_utilizator, p.cod, 'sistem'
FROM   Utilizatori u
CROSS JOIN Permisiuni p
WHERE  u.username = 'Mihail'
  AND  p.cod IN ('MENIU_ECHIPAMENTE', 'FAVORITE')
  AND NOT EXISTS (SELECT 1 FROM UtilizatoriPermisiuni up
                  WHERE up.id_utilizator = u.id_utilizator
                    AND up.cod_permisiune = p.cod);
GO

/* ----------------------------------------------------------------
   8. TABELUL AuditLog (Q5)
   ---------------------------------------------------------------- */
IF OBJECT_ID('AuditLog', 'U') IS NULL
BEGIN
    CREATE TABLE AuditLog (
        id_audit         INT           PRIMARY KEY IDENTITY(1,1),
        tabela           NVARCHAR(50)  NOT NULL,
        id_inregistrare  INT           NOT NULL DEFAULT 0,
        actiune          NVARCHAR(10)  NOT NULL,
        username         NVARCHAR(50)  NOT NULL,
        data_ora         DATETIME      NOT NULL DEFAULT GETDATE(),
        detalii          NVARCHAR(MAX) NULL
    );

    PRINT 'Tabel AuditLog creat cu succes.';
END
ELSE
    PRINT 'Tabelul AuditLog exista deja — omis.';
GO

/* ===================================================== */
/* VERIFICARE FINALĂ                                     */
/* ===================================================== */

PRINT '=== VERIFICARE HASH-URI (LEN trebuie 64 pentru fiecare) ===';
SELECT username, parola_hash, LEN(parola_hash) AS lungime_hash, rol, activ
FROM Utilizatori
ORDER BY id_utilizator;
GO

PRINT '=== STATISTICĂ GENERALĂ ===';
SELECT 'Operatori' AS Entitate, COUNT(*) AS Total FROM Operatori
UNION ALL
SELECT 'Sali', COUNT(*) FROM Sali
UNION ALL
SELECT 'Echipamente', COUNT(*) FROM Echipamente
UNION ALL
SELECT 'Membri', COUNT(*) FROM Membri
UNION ALL
SELECT 'Sesiuni', COUNT(*) FROM Sesiuni
UNION ALL
SELECT 'Rezervari', COUNT(*) FROM Rezervari
UNION ALL
SELECT 'Utilizatori', COUNT(*) FROM Utilizatori
UNION ALL
SELECT 'Permisiuni', COUNT(*) FROM Permisiuni
UNION ALL
SELECT 'Echipamente complete (view)', COUNT(*) FROM vw_EchipamenteComplete
UNION ALL
SELECT 'Sesiuni complete (view)', COUNT(*) FROM vw_SesiuniComplete
UNION ALL
SELECT 'Echipamente disponibile (view)', COUNT(*) FROM vw_EchipamenteDisponibile
UNION ALL
SELECT 'Membri cu sesiuni (view)', COUNT(*) FROM vw_MembriCuSesiuni;
GO

PRINT '=== ECHIPAMENTELE DISPONIBILE ===';
SELECT * FROM vw_EchipamenteDisponibile ORDER BY oras, denumire;
GO

PRINT '=== SESIUNILE COMPLETE ===';
SELECT * FROM vw_SesiuniComplete ORDER BY data_sesiune DESC;
GO
