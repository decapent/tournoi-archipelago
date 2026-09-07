/*
    Baseline EF Core sur le schema deja cree a la main.

    Les 5 tables du modele existaient avant l'introduction des migrations. Plutot que de les
    supprimer et les recreer (ce qui ferait perdre le diagramme dbo.sysdiagrams), ce script :

      1. cree la table d'historique des migrations ;
      2. cree les quatre index de support de cle etrangere que la migration InitialCreate
         aurait crees, afin que la base corresponde exactement a l'etat decrit par EF Core ;
      3. enregistre InitialCreate comme deja appliquee.

    A executer UNE FOIS, avant le premier `dotnet ef database update` :

        sqlcmd -S localhost -E -d Archipelago -i db/baseline.sql

    Le script est idempotent : le relancer ne fait rien.
*/

SET NOCOUNT ON;
GO

-- 1. Table d'historique ------------------------------------------------------
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
    PRINT 'Table [__EFMigrationsHistory] creee.';
END
GO

-- 2. Index de support de cle etrangere attendus par InitialCreate ------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Equipe_joueur1_id' AND object_id = OBJECT_ID(N'[Equipe]'))
    CREATE INDEX [IX_Equipe_joueur1_id] ON [Equipe] ([joueur1_id]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Equipe_joueur2_id' AND object_id = OBJECT_ID(N'[Equipe]'))
    CREATE INDEX [IX_Equipe_joueur2_id] ON [Equipe] ([joueur2_id]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MatchJeu_jeu_id' AND object_id = OBJECT_ID(N'[MatchJeu]'))
    CREATE INDEX [IX_MatchJeu_jeu_id] ON [MatchJeu] ([jeu_id]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MatchJeu_joueur_id' AND object_id = OBJECT_ID(N'[MatchJeu]'))
    CREATE INDEX [IX_MatchJeu_joueur_id] ON [MatchJeu] ([joueur_id]);
GO

-- 3. Enregistrement de la migration de baseline ------------------------------
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260907205232_InitialCreate')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260907205232_InitialCreate', N'9.0.19');
    PRINT 'InitialCreate marquee comme appliquee.';
END
ELSE
BEGIN
    PRINT 'InitialCreate etait deja enregistree : rien a faire.';
END
GO

PRINT 'Baseline terminee. Poursuivre avec : dotnet ef database update';
GO
