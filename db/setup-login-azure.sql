/*
    Cree l'utilisateur SQL dedie a l'application sur Azure SQL.

    A executer UNE FOIS, connecte a la base Archipelago avec le compte administrateur du
    serveur. Le mot de passe est OBLIGATOIRE :

        sqlcmd -S <serveur>.database.windows.net -d Archipelago -U <admin> -P <motDePasseAdmin> \
               -b -i db/setup-login-azure.sql -v password="MonMotDePasse"

    Difference avec db/setup-login.sql (SQL Server local) : Azure SQL n'autorise pas USE entre
    bases et n'a pas de logins au niveau serveur pour ce genre d'usage. On cree donc un
    UTILISATEUR CONTENU, dont le mot de passe vit dans la base elle-meme. Il n'y a rien a
    creer dans master.

    db_owner est requis parce que les migrations EF Core modifient le schema.

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
        N'Mot de passe manquant. Relancer avec : -v password="..."',
        16, 1);
END
ELSE IF LEN(@motDePasse) < 12
BEGIN
    RAISERROR (N'Mot de passe trop court : 12 caracteres au minimum.', 16, 1);
END
GO

IF DB_NAME() = N'master'
BEGIN
    RAISERROR (
        N'Se connecter a la base Archipelago, pas a master : un utilisateur contenu vit dans sa propre base.',
        16, 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'tournoi_app')
BEGIN
    CREATE USER [tournoi_app] WITH PASSWORD = N'$(password)';
    PRINT 'Utilisateur contenu [tournoi_app] cree.';
END
ELSE
BEGIN
    ALTER USER [tournoi_app] WITH PASSWORD = N'$(password)';
    PRINT 'Utilisateur [tournoi_app] deja present : mot de passe mis a jour.';
END
GO

ALTER ROLE [db_owner] ADD MEMBER [tournoi_app];
GO

PRINT 'Termine. Chaine de connexion a placer dans le secret MIGRATIONS_DB_CONNECTION :';
PRINT '  Server=tcp:<serveur>.database.windows.net,1433;Database=Archipelago;User ID=tournoi_app;Password=<motDePasse>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;';
GO
