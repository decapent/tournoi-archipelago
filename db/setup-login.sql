/*
    Cree le login SQL utilise par l'API.

    Necessaire parce qu'un conteneur Docker ne peut pas utiliser l'authentification
    Windows integree vers l'instance SQL Server de l'hote.

    A executer depuis l'hote, avec un compte administrateur. Le mot de passe est OBLIGATOIRE :

        sqlcmd -S localhost -E -b -i db/setup-login.sql -v password="MonMotDePasse"

    Relancer le script avec un autre mot de passe le met a jour (ALTER LOGIN).
    Reporter ensuite le meme mot de passe dans .env (ConnectionStrings__Tournoi).

    Aucune valeur par defaut n'est definie volontairement. Un ':setvar' en tete de script
    prendrait le pas sur l'argument -v, et le login se retrouverait avec un mot de passe
    lisible par quiconque ouvre ce depot. Le bloc de garde ci-dessous arrete le script quand
    la substitution n'a pas eu lieu : sans -v, sqlcmd laisse le jeton '$(password)' tel quel
    et se contenterait d'un avertissement.

    Le mot de passe ne doit pas contenir d'apostrophe : il est injecte tel quel dans le script.
*/

:on error exit

SET NOCOUNT ON;
GO

-- Garde : refuse un mot de passe absent, non substitue ou trop court. -----------
DECLARE @motDePasse nvarchar(128) = N'$(password)';

-- Le marqueur est reconstruit par concatenation : ecrire la sequence telle quelle
-- pousserait sqlcmd a tenter de la substituer ici aussi.
IF @motDePasse = N'' OR CHARINDEX(N'$' + N'(', @motDePasse) > 0
BEGIN
    RAISERROR (
        N'Mot de passe manquant. Relancer avec : sqlcmd -S localhost -E -b -i db/setup-login.sql -v password="..."',
        16, 1);
END
ELSE IF LEN(@motDePasse) < 12
BEGIN
    RAISERROR (N'Mot de passe trop court : 12 caracteres au minimum.', 16, 1);
END
GO

IF DB_ID(N'Archipelago') IS NULL
BEGIN
    RAISERROR (N'La base [Archipelago] est introuvable sur cette instance.', 16, 1);
END
GO

USE [master];
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'tournoi_app')
BEGIN
    CREATE LOGIN [tournoi_app]
        WITH PASSWORD = N'$(password)',
             CHECK_POLICY = ON,
             DEFAULT_DATABASE = [Archipelago];
    PRINT 'Login [tournoi_app] cree.';
END
ELSE
BEGIN
    ALTER LOGIN [tournoi_app] WITH PASSWORD = N'$(password)';
    PRINT 'Login [tournoi_app] deja present : mot de passe mis a jour.';
END
GO

USE [Archipelago];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'tournoi_app')
BEGIN
    CREATE USER [tournoi_app] FOR LOGIN [tournoi_app];
    PRINT 'Utilisateur [tournoi_app] cree dans [Archipelago].';
END
GO

-- db_owner est requis parce que les migrations EF Core modifient le schema.
ALTER ROLE [db_owner] ADD MEMBER [tournoi_app];
GO

PRINT 'Termine. Tester avec : sqlcmd -S localhost -U tournoi_app -d Archipelago -Q "SELECT 1"';
GO
