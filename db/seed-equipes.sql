/*
    Insere les huit equipes du tournoi et leurs trente-deux joueurs.

        sqlcmd -S localhost -E -b -d Archipelago -i db/seed-equipes.sql

    Le script est idempotent : les joueurs et les equipes deja presents sont laisses en place,
    et les appartenances manquantes sont ajoutees. Aucun match n'est touche.

    Il refuse de s'executer si un joueur de cette liste appartient deja a une autre equipe,
    l'unicite de l'appartenance etant ce qui permet de regrouper les resultats par equipe.
*/

:on error exit

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

DECLARE @roster TABLE (equipe nvarchar(100) NOT NULL, joueur nvarchar(100) NOT NULL);

INSERT INTO @roster (equipe, joueur) VALUES
    (N'Les Nous_',        N'Moi_Eva'),
    (N'Les Nous_',        N'Moi_Sophia'),
    (N'Les Nous_',        N'Moi_Flodarien'),
    (N'Les Nous_',        N'Moi_Thunderbuzz'),

    (N'No M''s Land',     N'_oli_an22'),
    (N'No M''s Land',     N'_C_La_Sorciere'),
    (N'No M''s Land',     N'_annix86'),
    (N'No M''s Land',     N'_oo__oony'),

    (N'O.J.M.I.',         N'JoueurSansFromage'),
    (N'O.J.M.I.',         N'01Ogopogo'),
    (N'O.J.M.I.',         N'MrDude042'),
    (N'O.J.M.I.',         N'Icy_Dragon1986'),

    (N'4G0L',             N'Nicolas McLovin'),
    (N'4G0L',             N'Kenichi099'),
    (N'4G0L',             N'Cuistot7'),
    (N'4G0L',             N'MarthSR'),

    (N'MyNameIsPending',  N'MyNameIsJoey'),
    (N'MyNameIsPending',  N'Mastafredz7'),
    (N'MyNameIsPending',  N'Deln'),
    (N'MyNameIsPending',  N'Holgart'),

    (N'What is a Name ?', N'Dairst'),
    (N'What is a Name ?', N'Mechabear'),
    (N'What is a Name ?', N'CrebleStar'),
    (N'What is a Name ?', N'Scorbrut'),

    (N'AGreatTeam',       N'AGreatWaffle'),
    (N'AGreatTeam',       N'Jonsu'),
    (N'AGreatTeam',       N'Silentbutdeadlyfart'),
    (N'AGreatTeam',       N'Simventor'),

    (N'ElsaipasGG',       N'AgedTurtles'),
    (N'ElsaipasGG',       N'JimbeauGG'),
    (N'ElsaipasGG',       N'YoureWithStupid'),
    (N'ElsaipasGG',       N'Elsaitu');

-- Garde : un joueur de la liste deja engage ailleurs rendrait le regroupement ambigu. -----
IF EXISTS (
    SELECT 1
    FROM @roster r
    JOIN Joueur j ON j.nom = r.joueur
    JOIN EquipeJoueur ej ON ej.joueur_id = j.id
    JOIN Equipe e ON e.id = ej.equipe_id
    WHERE e.nom <> r.equipe)
BEGIN
    SELECT r.joueur AS joueur, r.equipe AS equipe_attendue, e.nom AS equipe_actuelle
    FROM @roster r
    JOIN Joueur j ON j.nom = r.joueur
    JOIN EquipeJoueur ej ON ej.joueur_id = j.id
    JOIN Equipe e ON e.id = ej.equipe_id
    WHERE e.nom <> r.equipe;

    RAISERROR (
        N'Des joueurs de cette liste appartiennent deja a une autre equipe (voir ci-dessus). Corriger avant de rejouer le seed.',
        16, 1);
END

-- 1. Joueurs -----------------------------------------------------------------------------
INSERT INTO Joueur (nom)
SELECT DISTINCT r.joueur
FROM @roster r
WHERE NOT EXISTS (SELECT 1 FROM Joueur j WHERE j.nom = r.joueur);

-- 2. Equipes -----------------------------------------------------------------------------
INSERT INTO Equipe (nom)
SELECT DISTINCT r.equipe
FROM @roster r
WHERE NOT EXISTS (SELECT 1 FROM Equipe e WHERE e.nom = r.equipe);

-- 3. Appartenances -----------------------------------------------------------------------
INSERT INTO EquipeJoueur (equipe_id, joueur_id)
SELECT e.id, j.id
FROM @roster r
JOIN Equipe e ON e.nom = r.equipe
JOIN Joueur j ON j.nom = r.joueur
WHERE NOT EXISTS (
    SELECT 1 FROM EquipeJoueur ej WHERE ej.equipe_id = e.id AND ej.joueur_id = j.id);

COMMIT TRANSACTION;
GO

-- Recapitulatif --------------------------------------------------------------------------
SELECT e.nom AS equipe, COUNT(ej.joueur_id) AS membres
FROM Equipe e
LEFT JOIN EquipeJoueur ej ON ej.equipe_id = e.id
GROUP BY e.nom
ORDER BY e.nom;
GO
