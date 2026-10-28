/* ================================================================
   AppClubCalculatoare — Instalare.sql (SCRIPT MASTER)
   Baza de date: ClubCalculatoare

   Ruleaza TOATE scripturile BD intr-un singur pas, in ordinea corecta:
     1. ClubCalculatoare.sql            (baza + tabele + date demo + conturi)
     2. AuditLog.sql              (AuditLog + coloana email)
     3. Autentificare_Update.sql  (conturi pe baza existenta)
     4. Permisiuni.sql            (sistem permisiuni, 21 coduri de baza)
     5. MigrareFoto.sql           (tabela EchipamentFoto + FOTO_*)
     6. Favorite.sql              (tabela Favorite + FAVORITE + permis. de baza)
     7. CalculatorCost.sql      (permisiunea CALCULATOR_COST)
     8. Adauga_DurataMinute.sql   (coloana Sesiuni.durata_minute)
     9. 10_Performance_Indexes.sql (indexuri pentru dashboard/rapoarte)
    10. 20_usp_Dashboard.sql      (procedura unica pentru tabloul de bord)

   ATENTIE: pasul 1 RESETEAZA datele demo (DROP + re-CREATE tabele).
   Foloseste acest script doar pentru INSTALARE NOUA. Pentru o baza
   deja populata (cu poze/istoric), foloseste Instaleaza_BD.bat
   in modul UPDATE (doar pasii 2-10, idempotenti, fara pierderi).

   Rulare recomandata (din folderul BD):
     sqlcmd -S .\SQLEXPRESS -f 65001 -b -i Instalare.sql
   sau dublu-click pe Instaleaza_BD.bat (mod NOU).

   Rulare din SSMS: Query -> SQLCMD Mode (obligatoriu pentru :r),
   apoi executa tot scriptul. Daca SSMS nu porneste din folderul BD,
   editeaza linia :setvar BDPath de mai jos cu calea ABSOLUTA
   a folderului BD (ex: C:\Proiecte\AppClubCalculatoare\BD).
   ================================================================ */

:setvar BDPath "."

PRINT '=== 1/10 ClubCalculatoare.sql ===';
:r $(BDPath)\ClubCalculatoare.sql

PRINT '=== 2/10 AuditLog.sql ===';
:r $(BDPath)\AuditLog.sql

PRINT '=== 3/10 Autentificare_Update.sql ===';
:r $(BDPath)\Autentificare_Update.sql

PRINT '=== 4/10 Permisiuni.sql ===';
:r $(BDPath)\Permisiuni.sql

PRINT '=== 5/10 MigrareFoto.sql ===';
:r $(BDPath)\MigrareFoto.sql

PRINT '=== 6/10 Favorite.sql ===';
:r $(BDPath)\Favorite.sql

PRINT '=== 7/10 CalculatorCost.sql ===';
:r $(BDPath)\CalculatorCost.sql

PRINT '=== 8/10 Adauga_DurataMinute.sql ===';
:r $(BDPath)\Adauga_DurataMinute.sql

PRINT '=== 9/10 10_Performance_Indexes.sql ===';
:r $(BDPath)\10_Performance_Indexes.sql

PRINT '=== 10/10 20_usp_Dashboard.sql ===';
:r $(BDPath)\20_usp_Dashboard.sql

/* ----------------------------------------------------------------
   Verificare finala (rezumat instalare)
   ---------------------------------------------------------------- */
USE ClubCalculatoare;
GO

PRINT '--- Verificare finala ---';
SELECT 'Echipamente' AS tabela, COUNT(*) AS nr FROM Echipamente
UNION ALL SELECT 'Sali', COUNT(*) FROM Sali
UNION ALL SELECT 'Operatori', COUNT(*) FROM Operatori
UNION ALL SELECT 'Membri', COUNT(*) FROM Membri
UNION ALL SELECT 'Sesiuni', COUNT(*) FROM Sesiuni
UNION ALL SELECT 'Rezervari', COUNT(*) FROM Rezervari
UNION ALL SELECT 'Utilizatori', COUNT(*) FROM Utilizatori
UNION ALL SELECT 'Favorite', COUNT(*) FROM Favorite
UNION ALL SELECT 'EchipamentFoto', COUNT(*) FROM EchipamentFoto;
GO

IF OBJECT_ID('Permisiuni', 'U') IS NOT NULL
    SELECT cod FROM Permisiuni
    WHERE cod LIKE 'FOTO[_]%'
       OR cod LIKE 'MENIU[_]%'
       OR cod IN ('FAVORITE', 'CALCULATOR_COST')
    ORDER BY cod;
GO

PRINT 'Instalare.sql finalizat cu succes.';
GO
