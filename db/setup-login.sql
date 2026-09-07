/*
    Cree le login SQL utilise par l'API.

    Necessaire parce qu'un conteneur Docker ne peut pas utiliser l'authentification
    Windows integree vers l'instance SQL Server de l'hote.

    A executer UNE FOIS, depuis l'hote, avec un compte administrateur :

        sqlcmd -S localhost -E -i db/setup-login.sql -v password="MonMotDePasse"

    Puis reporter le meme mot de passe dans le fichier .env (ConnectionStrings__Tournoi).
*/

:setvar password "ChangeMoi_MotDePasseFort1"

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

IF DB_ID(N'Archipelago') IS NULL
BEGIN
    RAISERROR (N'La base [Archipelago] est introuvable sur cette instance.', 16, 1);
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
