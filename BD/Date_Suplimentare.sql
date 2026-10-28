/* ================================================================
   Date_Suplimentare.sql — date demo suplimentare (idempotent)
   Adauga sesiuni + rezervari ca tabloul de bord sa arate viu.
   Poate fi rulat de mai multe ori fara duplicate (guard COUNT).
   Rulare: sqlcmd -S .\SQLEXPRESS -d ClubCalculatoare -f 65001 -b -i BD\Date_Suplimentare.sql
   ================================================================ */
USE ClubCalculatoare;
GO

/* Daca exista deja date, nu duplicam: iesim pasnic. */
IF (SELECT COUNT(*) FROM Sesiuni) >= 20 AND (SELECT COUNT(*) FROM Rezervari) >= 5
BEGIN
    PRINT 'Datele exista deja (Sesiuni>=20 si Rezervari>=5) — nimic de adaugat.';
    RETURN;
END
GO

/* ---------------- 40 SESIUNI (mai 2026 - oct 2026 + istoric) ----------------
   Coloane: id_echipament, id_membru, id_operator, tip_sesiune,
            data_sesiune, durata_minute, cost_total, incasare, status, observatii */
INSERT INTO Sesiuni (id_echipament, id_membru, id_operator, tip_sesiune, data_sesiune, durata_minute, cost_total, incasare, status, observatii) VALUES
-- Mai 2026 (5)
(1, 1, 1, 'Joc', '2026-05-04', 120, 290.00, 290.00, 'Finalizata', 'Sesiune gaming 2h, plata card'),
(5, 2, 5, 'Joc', '2026-05-09',  90, 138.00, 138.00, 'Finalizata', 'Abonament seara'),
(2, 3, 3, 'Joc', '2026-05-14',  60, 320.00, 320.00, 'Finalizata', 'Consola PS5, grup 3 persoane'),
(4, 6, 2, 'Eveniment', '2026-05-19', 180, 750.00, 750.00, 'Finalizata', 'Eveniment privat simulator'),
(8, 9, 9, 'Joc', '2026-05-26',  90, 615.00, 615.00, 'Finalizata', 'Xbox Series X, plata integrala'),
-- Iunie 2026 (6)
(1, 6, 1, 'Joc', '2026-06-02', 150, 362.50, 362.50, 'Finalizata', 'Turneu de seara'),
(7, 1, 8, 'Joc', '2026-06-06', 120, 236.00, 236.00, 'Finalizata', 'Membru fidel, reducere 0'),
(5, 4, 2, 'Internet', '2026-06-10', 240, 368.00, 368.00, 'Finalizata', 'Pachet internet 4h'),
(3, 5, 4, 'Joc', '2026-06-15',  60,  75.00,  75.00, 'Finalizata', 'Server gazduire meci'),
(9, 7, 6, 'Joc', '2026-06-20',  90, 132.00, 132.00, 'Finalizata', 'Sesiune pranz expres'),
(10, 8, 10, 'Joc', '2026-06-27', 120, 260.00, 260.00, 'Finalizata', 'Streaming + backup'),
-- Iulie 2026 (6)
(1, 2, 1, 'Joc', '2026-07-03',  90, 217.50, 217.50, 'Finalizata', 'Vara, cerere mare'),
(2, 5, 3, 'Joc', '2026-07-08', 120, 640.00, 640.00, 'Finalizata', 'Weekend consola'),
(6, 7, 6, 'Eveniment', '2026-07-12',  60, 175.00, 175.00, 'Finalizata', 'Demo VR, avans achitat integral'),
(4, 1, 2, 'Joc', '2026-07-17',  60, 250.00, 250.00, 'Finalizata', 'Simulator curse'),
(5, 10, 5, 'Joc', '2026-07-22', 110, 168.66, 168.66, 'Finalizata', 'Sesiune standard'),
(8, 3, 9, 'Joc', '2026-07-29', 150, 1025.00, 1025.00, 'Finalizata', 'Maraton consola 2.5h'),
-- August 2026 (7)
(7, 6, 8, 'Joc', '2026-08-01', 150, 295.00, 295.00, 'Finalizata', 'Turneu august'),
(1, 4, 10, 'Joc', '2026-08-05', 130, 313.66, 313.66, 'Finalizata', 'Sesiune pe PC-01'),
(9, 1, 6, 'Joc', '2026-08-09',  60,  88.00,  88.00, 'Finalizata', 'Lobby cursuri'),
(3, 2, 4, 'Internet', '2026-08-13', 180, 225.00, 225.00, 'Finalizata', 'Server dedicat 3h'),
(2, 8, 3, 'Joc', '2026-08-18',  90, 480.00, 480.00, 'Finalizata', 'Seara consola'),
(10, 5, 7, 'Joc', '2026-08-23',  90, 195.00, 195.00, 'Finalizata', 'Statie streaming'),
(6, 9, 6, 'Eveniment', '2026-08-28',  45, 131.25, 131.25, 'Finalizata', 'Eveniment VR weekend'),
-- Septembrie 2026 (7)
(1, 10, 1, 'Joc', '2026-09-02', 120, 290.00, 290.00, 'Finalizata', 'Inceput de toamna'),
(5, 6, 5, 'Joc', '2026-09-06', 100, 153.33, 153.33, 'Finalizata', 'Abonament lunar'),
(4, 3, 2, 'Joc', '2026-09-10',  90, 375.00, 375.00, 'Finalizata', 'Simulator, grup firme'),
(8, 7, 9, 'Joc', '2026-09-14', 120, 820.00, 820.00, 'Finalizata', 'Consola 4K 2h'),
(7, 8, 8, 'Joc', '2026-09-18',  60, 118.00, 118.00, 'Finalizata', 'Sesiune scurta'),
(2, 1, 3, 'Joc', '2026-09-22', 110, 586.66, 586.66, 'Finalizata', 'Seara PS5'),
(9, 5, 4, 'Internet', '2026-09-26', 200, 293.33, 293.33, 'Finalizata', 'Pachet internet mare'),
-- Octombrie 2026 (5, toate in trecut fata de azi 11.10.2026)
(1, 3, 1, 'Joc', '2026-10-01',  90, 217.50, 217.50, 'Finalizata', 'Start octombrie'),
(5, 9, 2, 'Joc', '2026-10-03', 120, 184.00, 184.00, 'Finalizata', 'Weekend gaming'),
(8, 2, 9, 'Joc', '2026-10-05',  60, 410.00, 410.00, 'Finalizata', 'Consola premium 1h'),
(3, 4, 4, 'Joc', '2026-10-07',  90, 112.50, 112.50, 'În așteptare', 'Se asteapta confirmarea platii'),
(6, 7, 6, 'Eveniment', '2026-10-09',  60, 175.00, 175.00, 'În așteptare', 'Rezervare VR, avans partial'),
-- Istoric 2024-2025 (4, pentru filtrul An)
(1, 1, 1, 'Joc', '2024-03-01', 120, 290.00, 290.00, 'Finalizata', 'Istoric 2024'),
(2, 2, 3, 'Joc', '2024-11-12',  90, 480.00, 480.00, 'Finalizata', 'Istoric 2024'),
(5, 4, 2, 'Internet', '2025-06-15', 240, 368.00, 368.00, 'Finalizata', 'Istoric 2025'),
(9, 1, 6, 'Joc', '2025-09-15',  30,  44.00,  44.00, 'Finalizata', 'Istoric 2025');
PRINT 'Sesiuni inserate.';
GO

/* ---------------- 13 REZERVARI (trecut + viitor) ---------------- */
INSERT INTO Rezervari (id_echipament, id_membru, id_operator, data_rezervare, status, observatii) VALUES
-- Efectuate (trecut)
(1, 6, 10, '2026-09-02 11:00:00', 'Efectuata', 'Rezervare onorata PC-01'),
(2, 2, 3,  '2026-09-05 10:30:00', 'Efectuata', 'Rezervare consola + abonament'),
(4, 6, 2,  '2026-09-12 16:00:00', 'Efectuata', 'Simulator grup'),
(5, 1, 5,  '2026-09-20 09:00:00', 'Efectuata', 'Rezervare matinala'),
(9, 1, 6,  '2026-09-28 17:30:00', 'Efectuata', 'Membru multumit'),
(7, 4, 8,  '2026-10-02 18:00:00', 'Efectuata', 'Turneu seara onorat'),
-- Programate (VIITOR fata de 11.10.2026 — obligatoriu pentru validare)
(6, 7, 6,  '2026-10-15 12:00:00', 'Programata', 'Se asteapta confirmarea membrului'),
(8, 9, 9,  '2026-10-20 14:00:00', 'Programata', 'Rezervare consola weekend'),
(1, 3, 1,  '2026-10-25 16:00:00', 'Programata', 'Gaming 2h, grup studenti'),
(4, 5, 2,  '2026-11-02 15:00:00', 'Programata', 'Eveniment firma simulator'),
(10, 8, 10, '2026-11-10 10:00:00', 'Programata', 'Streaming eveniment'),
-- Anulate
(10, 8, 10, '2026-09-01 10:00:00', 'Anulata', 'Membrul a renuntat'),
(3, 5, 4,  '2026-09-18 13:00:00', 'Anulata', 'Server indisponibil atunci');
PRINT 'Rezervari inserate.';
GO

/* ---------------- Verificare ---------------- */
SELECT 'Sesiuni' AS tabela, COUNT(*) AS nr FROM Sesiuni
UNION ALL SELECT 'Rezervari', COUNT(*) FROM Rezervari
UNION ALL SELECT 'Sesiuni in asteptare', COUNT(*) FROM Sesiuni WHERE status='În așteptare'
UNION ALL SELECT 'Rezervari programate', COUNT(*) FROM Rezervari WHERE status='Programata'
UNION ALL SELECT 'Incasare totala', CAST(ISNULL(SUM(incasare),0) AS INT) FROM Sesiuni;
GO
SELECT TOP 3 a.nume + ' ' + a.prenume AS operator, ISNULL(SUM(t.incasare),0) AS total, COUNT(t.id_sesiune) AS nr
FROM Operatori a LEFT JOIN Sesiuni t ON a.id_operator=t.id_operator
GROUP BY a.id_operator, a.nume, a.prenume ORDER BY total DESC;
GO
