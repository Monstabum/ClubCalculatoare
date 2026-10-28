/* ================================================================
   AppClubCalculatoare - 10_Performance_Indexes.sql
   Indexuri idempotente pentru SQL Server 2016+.
   Se pot rula de oricate ori; nu sterg date.

   Rulare:
     sqlcmd -S .\SQLEXPRESS -E -d ClubCalculatoare -i 10_Performance_Indexes.sql
   ================================================================ */

USE ClubCalculatoare;
GO
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

/* ── Sesiuni: dashboard, rapoarte, Sala Live, FK ─────────────────── */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Sesiuni_Data'
                 AND object_id = OBJECT_ID('dbo.Sesiuni'))
    CREATE INDEX IX_Sesiuni_Data
        ON dbo.Sesiuni(data_sesiune)
        INCLUDE (incasare, status, id_operator);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Sesiuni_Operator_Data'
                 AND object_id = OBJECT_ID('dbo.Sesiuni'))
    CREATE INDEX IX_Sesiuni_Operator_Data
        ON dbo.Sesiuni(id_operator, data_sesiune)
        INCLUDE (incasare);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Sesiuni_Echipament_Status'
                 AND object_id = OBJECT_ID('dbo.Sesiuni'))
    CREATE INDEX IX_Sesiuni_Echipament_Status
        ON dbo.Sesiuni(id_echipament, status)
        INCLUDE (data_sesiune, durata_minute, incasare);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Sesiuni_Membru_Data'
                 AND object_id = OBJECT_ID('dbo.Sesiuni'))
    CREATE INDEX IX_Sesiuni_Membru_Data
        ON dbo.Sesiuni(id_membru, data_sesiune)
        INCLUDE (incasare);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Sesiuni_Status_Data'
                 AND object_id = OBJECT_ID('dbo.Sesiuni'))
    CREATE INDEX IX_Sesiuni_Status_Data
        ON dbo.Sesiuni(status, data_sesiune DESC)
        INCLUDE (id_echipament, durata_minute);
GO

/* ── Rezervari: alerte, FK ────────────────────────────────────────── */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Rezervari_Status_Data'
                 AND object_id = OBJECT_ID('dbo.Rezervari'))
    CREATE INDEX IX_Rezervari_Status_Data
        ON dbo.Rezervari(status, data_rezervare)
        INCLUDE (id_echipament, id_membru, id_operator);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Rezervari_Echipament'
                 AND object_id = OBJECT_ID('dbo.Rezervari'))
    CREATE INDEX IX_Rezervari_Echipament ON dbo.Rezervari(id_echipament);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Rezervari_Membru'
                 AND object_id = OBJECT_ID('dbo.Rezervari'))
    CREATE INDEX IX_Rezervari_Membru ON dbo.Rezervari(id_membru);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Rezervari_Operator'
                 AND object_id = OBJECT_ID('dbo.Rezervari'))
    CREATE INDEX IX_Rezervari_Operator ON dbo.Rezervari(id_operator);
GO

/* ── Echipamente / Sali / Operatori: liste, KPI, FK ──────────────── */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Echipamente_Sala'
                 AND object_id = OBJECT_ID('dbo.Echipamente'))
    CREATE INDEX IX_Echipamente_Sala
        ON dbo.Echipamente(sala_id)
        INCLUDE (status, tarif_ora, denumire, tip_echipament, oras);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Echipamente_Status'
                 AND object_id = OBJECT_ID('dbo.Echipamente'))
    CREATE INDEX IX_Echipamente_Status
        ON dbo.Echipamente(status)
        INCLUDE (tarif_ora);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Echipamente_Tip'
                 AND object_id = OBJECT_ID('dbo.Echipamente'))
    CREATE INDEX IX_Echipamente_Tip
        ON dbo.Echipamente(tip_echipament)
        INCLUDE (status);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Sali_OperatorSef'
                 AND object_id = OBJECT_ID('dbo.Sali'))
    CREATE INDEX IX_Sali_OperatorSef ON dbo.Sali(operator_sef_id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Operatori_Sala'
                 AND object_id = OBJECT_ID('dbo.Operatori'))
    CREATE INDEX IX_Operatori_Sala ON dbo.Operatori(sala_id);
GO

/* ── AuditLog / drepturi / favorite / foto ───────────────────────── */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_AuditLog_Tabela_Data'
                 AND object_id = OBJECT_ID('dbo.AuditLog'))
    CREATE INDEX IX_AuditLog_Tabela_Data
        ON dbo.AuditLog(tabela, data_ora DESC)
        INCLUDE (id_inregistrare, actiune, username);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_UtilizatoriPermisiuni_Permisiune'
                 AND object_id = OBJECT_ID('dbo.UtilizatoriPermisiuni'))
    CREATE INDEX IX_UtilizatoriPermisiuni_Permisiune
        ON dbo.UtilizatoriPermisiuni(cod_permisiune)
        INCLUDE (id_utilizator);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Favorite_Echipament'
                 AND object_id = OBJECT_ID('dbo.Favorite'))
    CREATE INDEX IX_Favorite_Echipament ON dbo.Favorite(id_echipament);
GO

/* UX_Foto_Principala (filtru este_principala = 1) exista deja din
   MigrareFoto.sql; adaugam doar indexul de ordine daca lipseste. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_EchipamentFoto_Ordine'
                 AND object_id = OBJECT_ID('dbo.EchipamentFoto'))
    CREATE INDEX IX_EchipamentFoto_Ordine
        ON dbo.EchipamentFoto(id_echipament, ordine)
        INCLUDE (nume_fisier, este_principala);
GO

PRINT '10_Performance_Indexes.sql finalizat.';
GO
