/*
    Aligne la table Jeu sur la liste des jeux du tournoi.

        sqlcmd -S localhost -E -b -d Archipelago -i db/seed-jeux.sql

    Sur Azure SQL :

        sqlcmd -S noreset.database.windows.net -d archipelago -U tournoi_app -P <motDePasse> \
               -b -i db/seed-jeux.sql

    Le script est idempotent : les jeux manquants sont ajoutes, ceux deja presents sont laisses
    tels quels. Leur identifiant ne bouge pas, donc les resultats de match qui les referencent
    restent valides.

    Les jeux hors liste sont supprimes UNIQUEMENT s'ils n'ont jamais ete joues. Ceux qui
    portent des resultats sont conserves et signales : a l'administrateur de trancher.

    Note : la migration AjoutNbChecksEtAjustements seme une douzaine de jeux a la creation du
    schema. Ce script prend le relais et remplace cette liste de depart.

    Une table temporaire plutot qu'une variable de table : elle survit aux GO, ce qui permet
    au recapitulatif de s'y referer sans redonner la liste.

    ATTENTION A L'ENCODAGE : ce fichier est en UTF-8 AVEC BOM, et doit le rester. Sans BOM,
    sqlcmd le lit dans la page de codes ANSI et « Pokémon » devient « PokÃ©mon » en base.
    A defaut de BOM, passer -f 65001 a sqlcmd.
*/

:on error exit

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

DROP TABLE IF EXISTS #jeux;
CREATE TABLE #jeux (nom nvarchar(200) NOT NULL PRIMARY KEY);

INSERT INTO #jeux (nom) VALUES
    (N'The Legend of Zelda'),
    (N'A Link to the Past'),
    (N'Ocarina of Time'),
    (N'Final Fantasy'),
    (N'Final Fantasy 4'),
    (N'Final Fantasy: Mystic Quest'),
    (N'Pokémon Emerald'),
    (N'Super Metroid'),
    (N'Castlevania: Symphony of the Night'),
    (N'Mega Man 2'),
    (N'Mega Man X'),
    (N'Donkey Kong Country 2: Diddy''s Kong Quest'),
    (N'Sonic the Hedgehog'),
    (N'Kirby Dream Land 3'),
    (N'Super Mario 64'),
    (N'Super Mario World'),
    (N'Wario Land'),
    (N'Mario Kart 64'),
    (N'Doom'),
    (N'Tetris Attack'),
    (N'Paint'),
    (N'Jigsaw');
GO

BEGIN TRANSACTION;

-- 1. Ajout des jeux manquants ------------------------------------------------------------
INSERT INTO Jeu (nom)
SELECT t.nom
FROM #jeux t
WHERE NOT EXISTS (SELECT 1 FROM Jeu j WHERE j.nom = t.nom);

-- 2. Retrait des jeux hors liste, seulement s'ils n'ont jamais ete joues -------------------
DELETE FROM Jeu
WHERE NOT EXISTS (SELECT 1 FROM #jeux t WHERE t.nom = Jeu.nom)
  AND NOT EXISTS (SELECT 1 FROM MatchJeu mj WHERE mj.jeu_id = Jeu.id);

COMMIT TRANSACTION;
GO

-- Signalement : jeux hors liste conserves parce qu'ils portent des resultats ---------------
IF EXISTS (SELECT 1 FROM Jeu WHERE NOT EXISTS (SELECT 1 FROM #jeux t WHERE t.nom = Jeu.nom))
BEGIN
    PRINT 'Jeux hors liste conserves car deja joues :';

    SELECT j.nom AS jeu, COUNT(mj.jeu_id) AS resultats
    FROM Jeu j
    JOIN MatchJeu mj ON mj.jeu_id = j.id
    WHERE NOT EXISTS (SELECT 1 FROM #jeux t WHERE t.nom = j.nom)
    GROUP BY j.nom
    ORDER BY j.nom;
END
GO

-- Recapitulatif ---------------------------------------------------------------------------
SELECT id, nom FROM Jeu ORDER BY nom;
GO

DROP TABLE IF EXISTS #jeux;
GO
