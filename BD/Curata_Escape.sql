/* ─────────────────────────────────────────────────────────────
   BD\Curata_Escape.sql
   Curățenie de date: unele versiuni demo ale bazei au înregistrat
   escape-uri \uXXXX ca text literal (ex: „PC-01 \u2014 Str. ..." în
   loc de „PC-01 — Str. ...", „În așteptare" scris cu escape-uri).
   Scriptul înlocuiește escape-urile cu caracterele reale în
   Echipamente.denumire și Sesiuni.status.

   Rulare pe o bază existentă:
     sqlcmd -S .\SQLEXPRESS -E -d ClubCalculatoare -i "BD\Curata_Escape.sql"
   ───────────────────────────────────────────────────────────── */

UPDATE Echipamente
   SET denumire = REPLACE(denumire, '\u2014', NCHAR(0x2014));

UPDATE Echipamente
   SET status = REPLACE(status, '\u00ce', NCHAR(0x00CE));

UPDATE Sesiuni
   SET status = REPLACE(REPLACE(status, '\u00ce', NCHAR(0x00CE)), '\u0219', NCHAR(0x0219));
GO

-- Verificare: nu mai trebuie să existe escape-uri în date
SELECT 'Echipamente' AS tabela, denumire AS valoare FROM Echipamente WHERE denumire LIKE '%\u%'
UNION ALL
SELECT 'Sesiuni.status', status FROM Sesiuni WHERE status LIKE '%\u%';
GO
