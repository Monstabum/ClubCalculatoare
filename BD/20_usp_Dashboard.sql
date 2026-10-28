/* ================================================================
   AppClubCalculatoare - 20_usp_Dashboard.sql
   O singura procedura pentru tot tabloul de bord: 7 result set-uri.
   SQL Server 2016+ (fara DATETRUNC / GENERATE_SERIES / STRING_AGG).

   Parametri:
     @an, @luna      - perioada selectata (NULL = toate)
     @autoPerioada   - 1 la prima incarcare: alege ultima luna cu sesiuni
     @zileStagnare   - alerta echipamente libere (implicit 90)
     @zileSesiuni    - alerta sesiuni in asteptare (implicit 30)
     @zileRezervari  - rezervari in urmatoarele N zile (implicit 7)

   Result set-uri:
     1. KPI + perioada rezolvata + ani disponibili?
     2. Total perioada + numar + total perioada anterioara
     3. Ultimele 6 luni (Total, Numar)
     4. Top 5 operatori
     5. Distributia pe tipuri de echipament
     6. Alerte (3 randuri)
     7. Anii disponibili
   ================================================================ */

USE ClubCalculatoare;
GO
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_Get
    @an            INT = NULL,
    @luna          INT = NULL,
    @autoPerioada  BIT = 0,
    @zileStagnare  INT = 90,
    @zileSesiuni   INT = 30,
    @zileRezervari INT = 7
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @azi  DATE = CAST(GETDATE() AS DATE);
    DECLARE @acum DATETIME2(0) = SYSDATETIME();

    /* La prima incarcare: ultima luna cu activitate (ca in vechiul cod). */
    IF @autoPerioada = 1 AND @an IS NULL AND @luna IS NULL
    BEGIN
        SELECT TOP (1)
               @an   = YEAR(data_sesiune),
               @luna = MONTH(data_sesiune)
        FROM   dbo.Sesiuni
        ORDER BY data_sesiune DESC, id_sesiune DESC;
    END

    /* Luna fara an: ultimul an care are activitate in acea luna
       (comportamentul vechiului RezolvaSelectiaAn). */
    IF @an IS NULL AND @luna IS NOT NULL
    BEGIN
        SELECT @an = MAX(YEAR(data_sesiune))
        FROM   dbo.Sesiuni
        WHERE  MONTH(data_sesiune) = @luna;
    END

    DECLARE @start DATE =
        CASE WHEN @an IS NULL THEN NULL
             WHEN @luna IS NULL THEN DATEFROMPARTS(@an, 1, 1)
             ELSE DATEFROMPARTS(@an, @luna, 1) END;

    DECLARE @end DATE =
        CASE WHEN @start IS NULL THEN NULL
             WHEN @luna IS NULL THEN DATEFROMPARTS(@an + 1, 1, 1)
             ELSE DATEADD(MONTH, 1, @start) END;

    DECLARE @prevStart DATE = NULL, @prevEnd DATE = NULL;
    IF @start IS NOT NULL
    BEGIN
        IF @luna IS NOT NULL
        BEGIN
            SET @prevStart = DATEADD(MONTH, -1, @start);
            SET @prevEnd   = @start;
        END
        ELSE
        BEGIN
            SET @prevStart = DATEFROMPARTS(@an - 1, 1, 1);
            SET @prevEnd   = DATEFROMPARTS(@an, 1, 1);
        END
    END

    /* ── 1. KPI + perioada rezolvata ────────────────────────────── */
    SELECT
        (SELECT COUNT(*) FROM dbo.Echipamente WHERE status = N'Liber')      AS EchipamenteLibere,
        (SELECT COUNT(*) FROM dbo.Echipamente)                              AS TotalEchipamente,
        (SELECT COUNT(*) FROM dbo.Sesiuni
          WHERE status IN (N'În așteptare', N'In a?teptare', N'In asteptare')) AS SesiuniInAsteptare,
        (SELECT COUNT(*) FROM dbo.Sesiuni)                                  AS TotalSesiuni,
        (SELECT COUNT(*) FROM dbo.Rezervari WHERE status = N'Programata')   AS RezervariProgramate,
        (SELECT ISNULL(SUM(incasare), 0) FROM dbo.Sesiuni)                  AS IncasareTotala,
        (SELECT ISNULL(SUM(tarif_ora), 0)
           FROM dbo.Echipamente WHERE status = N'Liber')                    AS Portofoliu,
        @an   AS AnSelectat,
        @luna AS LunaSelectat,
        (SELECT MAX(data_sesiune) FROM dbo.Sesiuni)                         AS UltimaDataSesiune;

    /* ── 2. Perioada curenta + perioada anterioara (trend) ──────── */
    SELECT
        ISNULL(SUM(CASE WHEN @start IS NULL
                          OR (data_sesiune >= @start AND data_sesiune < @end)
                        THEN incasare ELSE 0 END), 0) AS TotalPerioada,
        ISNULL(SUM(CASE WHEN @start IS NULL
                          OR (data_sesiune >= @start AND data_sesiune < @end)
                        THEN 1 ELSE 0 END), 0)       AS NumarPerioada,
        ISNULL(SUM(CASE WHEN @prevStart IS NOT NULL
                          AND data_sesiune >= @prevStart
                          AND data_sesiune <  @prevEnd
                        THEN incasare ELSE 0 END), 0) AS TotalAnterior
    FROM dbo.Sesiuni;

    /* ── 3. Ultimele 6 luni, cu ancorare pe perioada selectata ──── */
    DECLARE @anchor DATE = NULL;

    IF @an IS NOT NULL AND @luna IS NOT NULL
        SET @anchor = DATEFROMPARTS(@an, @luna, 1);
    ELSE IF @an IS NOT NULL
        SELECT @anchor = DATEFROMPARTS(YEAR(MAX(data_sesiune)),
                                       MONTH(MAX(data_sesiune)), 1)
        FROM   dbo.Sesiuni
        WHERE  data_sesiune >= DATEFROMPARTS(@an, 1, 1)
           AND data_sesiune <  DATEFROMPARTS(@an + 1, 1, 1);
    ELSE
        SELECT @anchor = DATEFROMPARTS(YEAR(MAX(data_sesiune)),
                                       MONTH(MAX(data_sesiune)), 1)
        FROM   dbo.Sesiuni;

    IF @anchor IS NOT NULL
    BEGIN
        DECLARE @chartStart DATE = DATEADD(MONTH, -5, @anchor);

        ;WITH Luni AS
        (
            SELECT @chartStart AS Luna
            UNION ALL
            SELECT DATEADD(MONTH, 1, Luna)
            FROM   Luni
            WHERE  Luna < @anchor
        )
        SELECT
            L.Luna,
            ISNULL(SUM(s.incasare), 0) AS Total,
            COUNT(s.id_sesiune)        AS Numar
        FROM   Luni L
        LEFT JOIN dbo.Sesiuni s
               ON s.data_sesiune >= L.Luna
              AND s.data_sesiune <  DATEADD(MONTH, 1, L.Luna)
        GROUP BY L.Luna
        ORDER BY L.Luna;
    END
    ELSE
    BEGIN
        SELECT CAST(NULL AS DATE) AS Luna,
               CAST(0 AS DECIMAL(18,2)) AS Total,
               CAST(0 AS INT) AS Numar
        WHERE 1 = 0;
    END

    /* ── 4. Top 5 operatori (inclusiv cei cu zero) ──────────────── */
    SELECT TOP (5)
        a.id_operator,
        a.nume + N' ' + a.prenume AS NumeOperator,
        ISNULL(SUM(t.incasare), 0) AS Total,
        COUNT(t.id_sesiune)        AS Numar
    FROM   dbo.Operatori a
    LEFT JOIN dbo.Sesiuni t ON t.id_operator = a.id_operator
    GROUP BY a.id_operator, a.nume, a.prenume
    ORDER BY Total DESC, NumeOperator;

    /* ── 5. Distributia pe tipuri de echipament ─────────────────── */
    SELECT tip_echipament, COUNT(*) AS Numar
    FROM   dbo.Echipamente
    GROUP BY tip_echipament
    ORDER BY Numar DESC, tip_echipament;

    /* ── 6. Alerte ──────────────────────────────────────────────── */
    SELECT N'Echipamente' AS Categorie,
           COUNT(*) AS Numar,
           ISNULL(MAX(DATEDIFF(DAY, e.data_adaugare, @azi)), 0) AS ZileMax,
           CAST(NULL AS DATETIME2(0)) AS DataMin
    FROM   dbo.Echipamente e
    WHERE  e.status = N'Liber'
      AND  e.data_adaugare < DATEADD(DAY, -@zileStagnare, @azi)

    UNION ALL

    SELECT N'Rezervari',
           COUNT(*),
           CAST(NULL AS INT),
           MIN(r.data_rezervare)
    FROM   dbo.Rezervari r
    WHERE  r.status = N'Programata'
      AND  r.data_rezervare >= @acum
      AND  r.data_rezervare <= DATEADD(DAY, @zileRezervari, @acum)

    UNION ALL

    SELECT N'Sesiuni',
           COUNT(*),
           ISNULL(MAX(DATEDIFF(DAY, s.data_sesiune, @azi)), 0),
           CAST(NULL AS DATETIME2(0))
    FROM   dbo.Sesiuni s
    WHERE  s.status IN (N'În așteptare', N'In a?teptare', N'In asteptare')
      AND  s.data_sesiune < DATEADD(DAY, -@zileSesiuni, @azi);

    /* ── 7. Anii disponibili pentru selector ────────────────────── */
    SELECT DISTINCT YEAR(data_sesiune) AS An
    FROM   dbo.Sesiuni
    ORDER BY An DESC;
END
GO

PRINT '20_usp_Dashboard.sql finalizat.';
GO
