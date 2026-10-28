/* ─────────────────────────────────────────────────────────────
   BD\Adauga_DurataMinute.sql
   Actualizare pentru o bază de date existentă: adaugă coloana
   Sesiuni.durata_minute (introdusă împreună cu domeniul
   „Club de calculatoare", vezi README §5).

   Rulare:
     sqlcmd -S .\SQLEXPRESS -E -d ClubCalculatoare -i "BD\Adauga_DurataMinute.sql"
   sau din SSMS pe baza ClubCalculatoare.

   Scriptul este idempotent: dacă coloana există deja, nu modifică nimic.
   ───────────────────────────────────────────────────────────── */

USE ClubCalculatoare;
GO

IF COL_LENGTH('Sesiuni', 'durata_minute') IS NULL
BEGIN
    ALTER TABLE Sesiuni ADD durata_minute INT NULL;
    PRINT 'Coloana Sesiuni.durata_minute a fost adaugata.';
END
ELSE
    PRINT 'Coloana Sesiuni.durata_minute exista deja — nimic de facut.';
GO
