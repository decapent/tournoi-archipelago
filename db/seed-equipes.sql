/*
    Insere les huit equipes du tournoi, leurs trente-deux joueurs et leurs capitaines.

        sqlcmd -S localhost -E -b -d Archipelago -i db/seed-equipes.sql

    Le script est idempotent : les joueurs et les equipes deja presents sont laisses en place,
    les appartenances manquantes sont ajoutees et les capitaines sont realignes sur cette liste.
    Aucun match n'est touche.

    Il refuse de s'executer si un joueur de cette liste appartient deja a une autre equipe,
    l'unicite de l'appartenance etant ce qui permet de rattacher un resultat a son camp.
*/

:on error exit

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Obligatoire pour toute ecriture dans EquipeJoueur : la table porte un index filtre
-- (UQ_EquipeJoueur_capitaine), et sqlcmd desactive QUOTED_IDENTIFIER par defaut.
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;

DECLARE @roster TABLE (
    equipe       nvarchar(100) NOT NULL,
    joueur       nvarchar(100) NOT NULL,
    estCapitaine bit           NOT NULL);

-- Le premier joueur de chaque equipe en est le capitaine.
INSERT INTO @roster (equipe, joueur, estCapitaine) VALUES
    (N'Les Nous_',        N'Moi_Eva',             1),
    (N'Les Nous_',        N'Moi_Sophia',          0),
    (N'Les Nous_',        N'Moi_Flodarien',       0),
    (N'Les Nous_',        N'Moi_Thunderbuzz',     0),

    (N'No M''s Land',     N'_oli_an22',           1),
    (N'No M''s Land',     N'_C_La_Sorciere',      0),
    (N'No M''s Land',     N'_annix86',            0),
    (N'No M''s Land',     N'_oo__oony',           0),

    (N'O.J.M.I.',         N'JoueurSansFromage',   1),
    (N'O.J.M.I.',         N'01Ogopogo',           0),
    (N'O.J.M.I.',         N'MrDude042',           0),
    (N'O.J.M.I.',         N'Icy_Dragon1986',      0),

    (N'4G0L',             N'Nicolas McLovin',     1),
    (N'4G0L',             N'Kenichi099',          0),
    (N'4G0L',             N'Cuistot7',            0),
    (N'4G0L',             N'MarthSR',             0),

    (N'MyNameIsPending',  N'MyNameIsJoey',        1),
    (N'MyNameIsPending',  N'Mastafredz7',         0),
    (N'MyNameIsPending',  N'Deln',                0),
    (N'MyNameIsPending',  N'Holgart',             0),

    (N'What is a Name ?', N'Dairst',              1),
    (N'What is a Name ?', N'Mechabear',           0),
    (N'What is a Name ?', N'CrebleStar',          0),
    (N'What is a Name ?', N'Scorbrut',            0),

    (N'AGreatTeam',       N'AGreatWaffle',        1),
    (N'AGreatTeam',       N'Jonsu',               0),
    (N'AGreatTeam',       N'Silentbutdeadlyfart', 0),
    (N'AGreatTeam',       N'Simventor',           0),

    (N'ElsaipasGG',       N'AgedTurtles',         1),
    (N'ElsaipasGG',       N'JimbeauGG',           0),
    (N'ElsaipasGG',       N'YoureWithStupid',     0),
    (N'ElsaipasGG',       N'Elsaitu',             0);

-- Garde : un joueur de la liste deja engage ailleurs rendrait le rattachement ambigu. ----
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
INSERT INTO EquipeJoueur (equipe_id, joueur_id, est_capitaine)
SELECT e.id, j.id, 0
FROM @roster r
JOIN Equipe e ON e.nom = r.equipe
JOIN Joueur j ON j.nom = r.joueur
WHERE NOT EXISTS (
    SELECT 1 FROM EquipeJoueur ej WHERE ej.equipe_id = e.id AND ej.joueur_id = j.id);

-- 4. Capitaines --------------------------------------------------------------------------
-- Retires puis reattribues : l'index filtre n'en tolere qu'un par equipe.
UPDATE ej
SET ej.est_capitaine = 0
FROM EquipeJoueur ej
JOIN Equipe e ON e.id = ej.equipe_id
WHERE e.nom IN (SELECT DISTINCT equipe FROM @roster);

UPDATE ej
SET ej.est_capitaine = 1
FROM EquipeJoueur ej
JOIN Equipe e ON e.id = ej.equipe_id
JOIN Joueur j ON j.id = ej.joueur_id
JOIN @roster r ON r.equipe = e.nom AND r.joueur = j.nom
WHERE r.estCapitaine = 1;

COMMIT TRANSACTION;
GO

-- Recapitulatif --------------------------------------------------------------------------
SELECT
    e.nom AS equipe,
    COUNT(ej.joueur_id) AS membres,
    MAX(CASE WHEN ej.est_capitaine = 1 THEN j.nom END) AS capitaine
FROM Equipe e
LEFT JOIN EquipeJoueur ej ON ej.equipe_id = e.id
LEFT JOIN Joueur j ON j.id = ej.joueur_id
GROUP BY e.nom
ORDER BY e.nom;
GO
