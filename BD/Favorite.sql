/* ================================================================
   AppClubCalculatoare — Favorite.sql
   Baza de date: ClubCalculatoare

   Modulul ECHIPAMENTE FAVORITE + permisiunile de baza ale
   utilizatorului simplu (rol 'User').

   Continut (totul IDEMPOTENT — poate fi rulat de mai multe ori):
     a) Permisiunea FAVORITE in tabela Permisiuni
     b) Tabela Favorite (id_utilizator + id_echipament)
     c) Permisiuni de baza pentru utilizatorii non-admin care nu au
        inca NIMIC acordat: PANOU_DASHBOARD, MENIU_ECHIPAMENTE,
        MENIU_SALI, MENIU_MEMBRI, MENIU_SESIUNI,
        MENIU_REZERVARI, FAVORITE  (doar VIZUALIZARE + favorite,
        fara MODIFICARE si STERGE)

   Ordine de executare in SSMS:
     1. ClubCalculatoare.sql
     2. AuditLog.sql
     3. Autentificare_Update.sql
     4. Permisiuni.sql
     5. MigrareFoto.sql
     6. Favorite.sql            (acest script)
     7. CalculatorCost.sql

   Observatii:
     - acordarea punctului c) se face DOAR pentru utilizatorii care
       nu au inca nicio permisiune in UtilizatoriPermisiuni, astfel
       incat configurarile facute manual de admin raman intacte.
     - dupa rulare, utilizatorii noi creati de admin primesc
       drepturile din Gestionare Utilizatori (FormUtilizatori).
   ================================================================ */

USE ClubCalculatoare;
GO

/* ----------------------------------------------------------------
   a) Permisiunea FAVORITE (catalogul din PermissionService.cs)
   ---------------------------------------------------------------- */
IF OBJECT_ID('Permisiuni', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM Permisiuni WHERE cod = 'FAVORITE')
    BEGIN
        INSERT INTO Permisiuni (cod, descriere)
        VALUES ('FAVORITE', 'Echipamente favorite (per utilizator)');
        PRINT 'Permisiunea FAVORITE a fost adaugata.';
    END
    ELSE
        PRINT 'Permisiunea FAVORITE exista deja — omisa.';
END
GO

/* ----------------------------------------------------------------
   b) Tabela Favorite
   ---------------------------------------------------------------- */
IF OBJECT_ID('Favorite', 'U') IS NULL
BEGIN
    CREATE TABLE Favorite (
        id_utilizator   INT      NOT NULL,
        id_echipament  INT      NOT NULL,
        data_adaugare   DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_Favorite PRIMARY KEY (id_utilizator, id_echipament),
        CONSTRAINT FK_Favorite_Utilizatori
            FOREIGN KEY (id_utilizator) REFERENCES Utilizatori (id_utilizator)
            ON DELETE CASCADE,
        CONSTRAINT FK_Favorite_Echipamente
            FOREIGN KEY (id_echipament) REFERENCES Echipamente (id_echipament)
            ON DELETE CASCADE
    );
    PRINT 'Tabelul Favorite a fost creat.';
END
ELSE
BEGIN
    PRINT 'Tabelul Favorite exista deja — omis.';
END
GO

/* ----------------------------------------------------------------
   c) Permisiuni de baza pentru utilizatorii non-admin.
   Politica minima: rolul User vede doar echipamentele (MENIU_ECHIPAMENTE
   + FAVORITE) si isi schimba parola — fara dashboard, fara celelalte
   module, fara modificare/stergere.
   FIX: acordare per cod lipsa (nu doar utilizatorilor cu ZERO
   permisiuni). Varianta veche sarea peste conturi ca Alexandru_user,
   care primeau deja drepturi din ClubCalculatoare.sql (MODIFICARE_*), deci
   acestea nu primeau niciodata FAVORITE — butonul „Comuta favorite"
   ramanea ascuns pentru ele, desi README promite Favorite la rolul User.
   Configurarile manuale ale adminului raman intacte (doar lipsurile
   se completeaza, nimic nu se sterge).
   ---------------------------------------------------------------- */
IF OBJECT_ID('UtilizatoriPermisiuni', 'U') IS NOT NULL
   AND OBJECT_ID('Permisiuni', 'U') IS NOT NULL
BEGIN
    DECLARE @acordate INT = 0;

    ;WITH CoduriDeBaza (cod) AS (
        SELECT 'MENIU_ECHIPAMENTE'  UNION ALL
        SELECT 'FAVORITE'
    )
    INSERT INTO UtilizatoriPermisiuni (id_utilizator, cod_permisiune, acordata_de)
    SELECT u.id_utilizator, c.cod, 'implicit'
    FROM   Utilizatori u
    CROSS  JOIN CoduriDeBaza c
    JOIN   Permisiuni p ON p.cod = c.cod
    WHERE  u.rol = 'User'
      AND  NOT EXISTS (SELECT 1 FROM UtilizatoriPermisiuni up
                       WHERE up.id_utilizator = u.id_utilizator
                         AND up.cod_permisiune = c.cod);

    SET @acordate = @@ROWCOUNT;
    PRINT 'Permisiuni de baza acordate: ' + CAST(@acordate AS VARCHAR)
        + ' permisiuni (doar codurile lipsa) pentru utilizatorii cu rol User.';
END
GO

PRINT 'Favorite.sql finalizat cu succes.';
GO
