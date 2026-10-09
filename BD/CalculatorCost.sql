/* ================================================================
   AppClubCalculatoare — CalculatorCost.sql
   Baza de date: ClubCalculatoare

   Modulul CALCULATOR COST SESIUNE/ABONAMENT — permisiunea CALCULATOR_COST.

   Continut (totul IDEMPOTENT — poate fi rulat de mai multe ori):
     a) Permisiunea CALCULATOR_COST in tabela Permisiuni
     b) Acordarea permisiunii catre utilizatorii cu rol User care
        nu o au inca (doar codurile lipsa se completeaza)

   Ordine de executare in SSMS (dupa Favorite.sql):
     7. CalculatorCost.sql   (acest script)
   ================================================================ */

USE ClubCalculatoare;
GO

/* ----------------------------------------------------------------
   a) Permisiunea CALCULATOR_COST
   ---------------------------------------------------------------- */
IF OBJECT_ID('Permisiuni', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'CALCULATOR_COST')
    BEGIN
        INSERT INTO Permisiuni (cod, descriere)
        VALUES ('CALCULATOR_COST', 'Calculator cost sesiune/abonament');
        PRINT 'Permisiunea CALCULATOR_COST a fost adaugata.';
    END
    ELSE
        PRINT 'Permisiunea CALCULATOR_COST exista deja — omisa.';
END
GO

/* ----------------------------------------------------------------
   b) Acordarea permisiunii catre utilizatorii User existenti
      (doar celor care nu o au inca)
   ---------------------------------------------------------------- */
IF OBJECT_ID('UtilizatoriPermisiuni', 'U') IS NOT NULL
   AND OBJECT_ID('Permisiuni', 'U') IS NOT NULL
BEGIN
    DECLARE @acordate INT = 0;

    INSERT INTO UtilizatoriPermisiuni (id_utilizator, cod_permisiune, acordata_de)
    SELECT u.id_utilizator, 'CALCULATOR_COST', 'implicit'
    FROM   Utilizatori u
    JOIN   Permisiuni p ON p.cod = 'CALCULATOR_COST'
    WHERE  u.rol = 'User'
      AND  NOT EXISTS (SELECT 1 FROM UtilizatoriPermisiuni up
                       WHERE up.id_utilizator = u.id_utilizator
                         AND up.cod_permisiune = 'CALCULATOR_COST');

    SET @acordate = @@ROWCOUNT;
    PRINT 'Permisiunea CALCULATOR_COST acordata la ' + CAST(@acordate AS VARCHAR)
        + ' utilizator(i) cu rol User.';
END
GO

PRINT 'CalculatorCost.sql finalizat cu succes.';
GO