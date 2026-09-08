using TournoiArchipelago.Api.Services;
using static TournoiArchipelago.Api.Tests.ConstructeurDonnees;

namespace TournoiArchipelago.Api.Tests;

public class ScoringServiceTests
{
    private static readonly Api.Domain.Joueur Alice = Joueur(1, "Alice");
    private static readonly Api.Domain.Joueur Bob = Joueur(2, "Bob");
    private static readonly Api.Domain.Joueur Anna = Joueur(3, "Anna");
    private static readonly Api.Domain.Joueur Arthur = Joueur(4, "Arthur");

    private static readonly Api.Domain.Joueur Chloe = Joueur(5, "Chloe");
    private static readonly Api.Domain.Joueur David = Joueur(6, "David");
    private static readonly Api.Domain.Joueur Claire = Joueur(7, "Claire");
    private static readonly Api.Domain.Joueur Damien = Joueur(8, "Damien");

    private static readonly Api.Domain.Jeu Alttp = Jeu(1, "A Link to the Past");
    private static readonly Api.Domain.Jeu Metroid = Jeu(2, "Super Metroid");

    private static readonly Api.Domain.Equipe EquipeA =
        Equipe(10, "Les Nous_", Alice, Bob, Anna, Arthur);

    private static readonly Api.Domain.Equipe EquipeB =
        Equipe(20, "No M's Land", Chloe, David, Claire, Damien);

    [Fact]
    public void Le_total_le_plus_faible_gagne()
    {
        var classement = ScoringService.ClasserMatch(Match(EquipeA, EquipeB,
        [
            Ligne(Alice, Alttp, tempsFinalSecs: 3_600),
            Ligne(Bob, Metroid, tempsFinalSecs: 3_000),   // equipe A : 6 600 s
            Ligne(Chloe, Alttp, tempsFinalSecs: 4_000),
            Ligne(David, Metroid, tempsFinalSecs: 3_500), // equipe B : 7 500 s
        ]));

        Assert.True(classement.EstComplet);
        Assert.Equal(2, classement.Equipes.Count);

        var premiere = classement.Equipes[0];
        Assert.Equal(EquipeA.Id, premiere.EquipeId);
        Assert.Equal(1, premiere.Position);
        Assert.True(premiere.EstGagnante);
        Assert.Equal(6_600, premiere.TempsTotalSecs);
        Assert.Equal(0, premiere.PenaliteSecs);
        Assert.Equal(0, premiere.NbResultatsEnAttente);

        var seconde = classement.Equipes[1];
        Assert.Equal(EquipeB.Id, seconde.EquipeId);
        Assert.Equal(2, seconde.Position);
        Assert.False(seconde.EstGagnante);
        Assert.Equal(7_500, seconde.TempsTotalSecs);
    }

    [Fact]
    public void Un_match_sans_aucun_resultat_montre_les_deux_equipes_engagees()
    {
        var classement = ScoringService.ClasserMatch(Match(EquipeA, EquipeB));

        Assert.False(classement.EstComplet);
        Assert.Equal(2, classement.Equipes.Count);

        Assert.All(classement.Equipes, equipe =>
        {
            Assert.Empty(equipe.Lignes);
            Assert.Null(equipe.TempsTotalSecs);
            Assert.False(equipe.EstGagnante);
        });

        Assert.Equal(["Les Nous_", "No M's Land"], classement.Equipes.Select(e => e.EquipeNom));
    }

    [Fact]
    public void Un_match_partiellement_saisi_ne_designe_aucun_vainqueur()
    {
        var classement = ScoringService.ClasserMatch(Match(EquipeA, EquipeB,
        [
            Ligne(Alice, Alttp, tempsFinalSecs: 100),
            Ligne(Bob, Metroid, tempsFinalSecs: null),
            Ligne(Chloe, Alttp, tempsFinalSecs: 5_000),
            Ligne(David, Metroid, tempsFinalSecs: 5_000),
        ]));

        Assert.False(classement.EstComplet);
        Assert.All(classement.Equipes, equipe => Assert.False(equipe.EstGagnante));

        var incomplete = classement.Equipes.Single(e => e.EquipeId == EquipeA.Id);
        Assert.Equal(1, incomplete.NbResultatsEnAttente);
        Assert.Null(incomplete.TempsTotalSecs);
        Assert.Null(incomplete.TempsBrutSecs);

        // L'autre equipe a bien son total, meme si le match n'est pas termine.
        var complete = classement.Equipes.Single(e => e.EquipeId == EquipeB.Id);
        Assert.Equal(0, complete.NbResultatsEnAttente);
        Assert.Equal(10_000, complete.TempsTotalSecs);
    }

    [Fact]
    public void Une_equipe_dont_le_total_manque_est_reportee_en_fin_de_classement()
    {
        var classement = ScoringService.ClasserMatch(Match(EquipeA, EquipeB,
        [
            Ligne(Alice, Alttp, tempsFinalSecs: null),
            Ligne(Bob, Metroid, tempsFinalSecs: null),
            Ligne(Chloe, Alttp, tempsFinalSecs: 5_000),
            Ligne(David, Metroid, tempsFinalSecs: 5_000),
        ]));

        Assert.Equal(EquipeB.Id, classement.Equipes[0].EquipeId);
        Assert.Equal(EquipeA.Id, classement.Equipes[1].EquipeId);
    }

    [Fact]
    public void Un_effectif_inegal_empeche_le_match_d_etre_complet()
    {
        var classement = ScoringService.ClasserMatch(Match(EquipeA, EquipeB,
        [
            Ligne(Alice, Alttp, tempsFinalSecs: 100),
            Ligne(Bob, Metroid, tempsFinalSecs: 100),
            Ligne(Anna, Alttp, tempsFinalSecs: 100),
            Ligne(Chloe, Alttp, tempsFinalSecs: 200),
            Ligne(David, Metroid, tempsFinalSecs: 200),
        ]));

        // Tous les temps sont saisis, mais trois joueurs contre deux.
        Assert.False(classement.EstComplet);
        Assert.All(classement.Equipes, equipe => Assert.Equal(0, equipe.NbResultatsEnAttente));
        Assert.All(classement.Equipes, equipe => Assert.False(equipe.EstGagnante));
    }

    [Fact]
    public void Un_seul_joueur_par_equipe_empeche_le_match_d_etre_complet()
    {
        var classement = ScoringService.ClasserMatch(Match(EquipeA, EquipeB,
        [
            Ligne(Alice, Alttp, tempsFinalSecs: 100),
            Ligne(Chloe, Alttp, tempsFinalSecs: 200),
        ]));

        Assert.False(classement.EstComplet);
    }

    [Fact]
    public void Un_abandon_ajoute_une_heure_au_temps_du_joueur()
    {
        var classement = ScoringService.ClasserMatch(Match(EquipeA, EquipeB,
        [
            Ligne(Alice, Alttp, tempsFinalSecs: 1_000),
            Ligne(Bob, Metroid, tempsFinalSecs: 2_000, estAbandon: true),
            Ligne(Chloe, Alttp, tempsFinalSecs: 10_000),
            Ligne(David, Metroid, tempsFinalSecs: 10_000),
        ]));

        var equipe = classement.Equipes.Single(e => e.EquipeId == EquipeA.Id);

        Assert.Equal(3_000, equipe.TempsBrutSecs);
        Assert.Equal(ScoringService.PenaliteAbandonSecs, equipe.PenaliteSecs);
        Assert.Equal(3_000 + 3_600, equipe.TempsTotalSecs);
        Assert.Equal(1, equipe.NbAbandons);

        var abandonnee = equipe.Lignes.Single(ligne => ligne.EstAbandon);
        Assert.Equal(2_000, abandonnee.TempsFinalSecs);
        Assert.Equal(5_600, abandonnee.TempsEffectifSecs);

        var terminee = equipe.Lignes.Single(ligne => !ligne.EstAbandon);
        Assert.Equal(1_000, terminee.TempsEffectifSecs);
    }

    [Fact]
    public void Chaque_abandon_de_l_equipe_est_penalise()
    {
        var classement = ScoringService.ClasserMatch(Match(EquipeA, EquipeB,
        [
            Ligne(Alice, Alttp, tempsFinalSecs: 100, estAbandon: true),
            Ligne(Bob, Metroid, tempsFinalSecs: 200, estAbandon: true),
            Ligne(Chloe, Alttp, tempsFinalSecs: 100),
            Ligne(David, Metroid, tempsFinalSecs: 100),
        ]));

        var equipe = classement.Equipes.Single(e => e.EquipeId == EquipeA.Id);

        Assert.Equal(2, equipe.NbAbandons);
        Assert.Equal(2 * ScoringService.PenaliteAbandonSecs, equipe.PenaliteSecs);
        Assert.Equal(300 + 7_200, equipe.TempsTotalSecs);
    }

    [Fact]
    public void Une_equipe_avec_un_abandon_est_classee_comme_les_autres()
    {
        // Equipe A : 60 s + un abandon a 60 s, soit 60 + 3 660 = 3 720 s.
        // Equipe B : deux completions a 2 000 s, soit 4 000 s.
        // La penalite etant la sanction, l'equipe A gagne malgre son abandon.
        var classement = ScoringService.ClasserMatch(Match(EquipeA, EquipeB,
        [
            Ligne(Alice, Alttp, tempsFinalSecs: 60),
            Ligne(Bob, Metroid, tempsFinalSecs: 60, estAbandon: true),
            Ligne(Chloe, Alttp, tempsFinalSecs: 2_000),
            Ligne(David, Metroid, tempsFinalSecs: 2_000),
        ]));

        Assert.True(classement.EstComplet);
        Assert.Equal(EquipeA.Id, classement.Equipes[0].EquipeId);
        Assert.Equal(3_720, classement.Equipes[0].TempsTotalSecs);
        Assert.True(classement.Equipes[0].EstGagnante);
        Assert.Equal(4_000, classement.Equipes[1].TempsTotalSecs);
    }

    [Fact]
    public void La_penalite_peut_faire_perdre_une_equipe_pourtant_plus_rapide()
    {
        var classement = ScoringService.ClasserMatch(Match(EquipeA, EquipeB,
        [
            Ligne(Alice, Alttp, tempsFinalSecs: 50),
            Ligne(Bob, Metroid, tempsFinalSecs: 50, estAbandon: true),
            Ligne(Chloe, Alttp, tempsFinalSecs: 1_000),
            Ligne(David, Metroid, tempsFinalSecs: 1_000),
        ]));

        Assert.Equal(EquipeB.Id, classement.Equipes[0].EquipeId);
        Assert.Equal(2_000, classement.Equipes[0].TempsTotalSecs);

        Assert.Equal(EquipeA.Id, classement.Equipes[1].EquipeId);
        Assert.Equal(100, classement.Equipes[1].TempsBrutSecs);
        Assert.Equal(3_700, classement.Equipes[1].TempsTotalSecs);
    }

    [Fact]
    public void A_egalite_parfaite_les_deux_equipes_partagent_la_premiere_place()
    {
        var classement = ScoringService.ClasserMatch(Match(EquipeA, EquipeB,
        [
            Ligne(Alice, Alttp, tempsFinalSecs: 1_000),
            Ligne(Bob, Metroid, tempsFinalSecs: 2_000),
            Ligne(Chloe, Alttp, tempsFinalSecs: 1_500),
            Ligne(David, Metroid, tempsFinalSecs: 1_500),
        ]));

        Assert.All(classement.Equipes, equipe =>
        {
            Assert.Equal(1, equipe.Position);
            Assert.True(equipe.EstGagnante);
            Assert.Equal(3_000, equipe.TempsTotalSecs);
        });
    }

    [Fact]
    public void Une_penalite_peut_creer_une_egalite()
    {
        // Equipe A : 400 s bruts + 3 600 s de penalite = 4 000 s, comme l'equipe B.
        var classement = ScoringService.ClasserMatch(Match(EquipeA, EquipeB,
        [
            Ligne(Alice, Alttp, tempsFinalSecs: 200),
            Ligne(Bob, Metroid, tempsFinalSecs: 200, estAbandon: true),
            Ligne(Chloe, Alttp, tempsFinalSecs: 2_000),
            Ligne(David, Metroid, tempsFinalSecs: 2_000),
        ]));

        Assert.All(classement.Equipes, equipe =>
        {
            Assert.Equal(4_000, equipe.TempsTotalSecs);
            Assert.Equal(1, equipe.Position);
            Assert.True(equipe.EstGagnante);
        });
    }

    [Fact]
    public void Le_pourcentage_de_completion_agrege_les_checks_des_participants()
    {
        var classement = ScoringService.ClasserMatch(Match(EquipeA, EquipeB,
        [
            Ligne(Alice, Alttp, tempsFinalSecs: 100, nbChecks: 60, totalChecks: 100),
            Ligne(Bob, Metroid, tempsFinalSecs: 100, nbChecks: 30, totalChecks: 200),
            Ligne(Chloe, Alttp, tempsFinalSecs: 100),
            Ligne(David, Metroid, tempsFinalSecs: 100),
        ]));

        var equipe = classement.Equipes.Single(e => e.EquipeId == EquipeA.Id);
        Assert.Equal(90, equipe.ChecksTrouves);
        Assert.Equal(300, equipe.TotalChecks);
        Assert.Equal(0.30, equipe.PourcentComplete!.Value, precision: 6);
    }

    [Fact]
    public void Le_pourcentage_est_absent_quand_le_total_de_checks_est_inconnu()
    {
        var classement = ScoringService.ClasserMatch(Match(EquipeA, EquipeB,
        [
            Ligne(Alice, Alttp, tempsFinalSecs: 100, nbChecks: 60),
            Ligne(Bob, Metroid, tempsFinalSecs: 100, nbChecks: 30),
            Ligne(Chloe, Alttp, tempsFinalSecs: 100),
            Ligne(David, Metroid, tempsFinalSecs: 100),
        ]));

        var equipe = classement.Equipes.Single(e => e.EquipeId == EquipeA.Id);
        Assert.Null(equipe.TotalChecks);
        Assert.Null(equipe.PourcentComplete);
        Assert.Equal(90, equipe.ChecksTrouves);
    }

    [Fact]
    public void Un_joueur_etranger_aux_deux_equipes_est_regroupe_a_part()
    {
        var eve = Joueur(99, "Eve");

        var classement = ScoringService.ClasserMatch(Match(EquipeA, EquipeB,
        [
            Ligne(Alice, Alttp, tempsFinalSecs: 500),
            Ligne(Bob, Metroid, tempsFinalSecs: 500),
            Ligne(Chloe, Alttp, tempsFinalSecs: 500),
            Ligne(David, Metroid, tempsFinalSecs: 500),
            Ligne(eve, Alttp, tempsFinalSecs: 100),
        ]));

        Assert.Equal(3, classement.Equipes.Count);

        var orpheline = classement.Equipes.Single(e => e.EquipeId is null);
        Assert.Equal(ScoringService.NomEquipeInconnue, orpheline.EquipeNom);
        Assert.Equal(100, orpheline.TempsTotalSecs);

        // Un groupe orphelin empeche le match d'etre considere complet.
        Assert.False(classement.EstComplet);
    }

    [Fact]
    public void Les_lignes_d_une_equipe_sont_triees_par_nom_de_joueur()
    {
        var classement = ScoringService.ClasserMatch(Match(EquipeA, EquipeB,
        [
            Ligne(Bob, Metroid, tempsFinalSecs: 100),
            Ligne(Alice, Alttp, tempsFinalSecs: 200),
            Ligne(Chloe, Alttp, tempsFinalSecs: 100),
            Ligne(David, Metroid, tempsFinalSecs: 100),
        ]));

        var equipe = classement.Equipes.Single(e => e.EquipeId == EquipeA.Id);
        Assert.Equal(["Alice", "Bob"], equipe.Lignes.Select(l => l.JoueurNom));
    }

    [Fact]
    public void Le_classement_s_adapte_a_trois_joueurs_par_equipe()
    {
        // Format demi-finale : aucun nombre de participants n'est code en dur.
        var classement = ScoringService.ClasserMatch(Match(EquipeA, EquipeB,
        [
            Ligne(Alice, Alttp, tempsFinalSecs: 1_000),
            Ligne(Bob, Metroid, tempsFinalSecs: 1_000),
            Ligne(Anna, Alttp, tempsFinalSecs: 1_000),
            Ligne(Chloe, Alttp, tempsFinalSecs: 2_000),
            Ligne(David, Metroid, tempsFinalSecs: 2_000),
            Ligne(Claire, Metroid, tempsFinalSecs: 2_000),
        ]));

        Assert.True(classement.EstComplet);
        Assert.Equal(3_000, classement.Equipes[0].TempsTotalSecs);
        Assert.Equal(3, classement.Equipes[0].Lignes.Count);
        Assert.Equal(6_000, classement.Equipes[1].TempsTotalSecs);
    }

    [Fact]
    public void Le_classement_s_adapte_a_quatre_joueurs_par_equipe()
    {
        // Format finale, avec un abandon cote B.
        var classement = ScoringService.ClasserMatch(Match(EquipeA, EquipeB,
        [
            Ligne(Alice, Alttp, tempsFinalSecs: 100),
            Ligne(Bob, Metroid, tempsFinalSecs: 100),
            Ligne(Anna, Alttp, tempsFinalSecs: 100),
            Ligne(Arthur, Metroid, tempsFinalSecs: 100),
            Ligne(Chloe, Alttp, tempsFinalSecs: 50),
            Ligne(David, Metroid, tempsFinalSecs: 50),
            Ligne(Claire, Alttp, tempsFinalSecs: 50),
            Ligne(Damien, Metroid, tempsFinalSecs: 50, estAbandon: true),
        ]));

        Assert.True(classement.EstComplet);
        Assert.Equal("Les Nous_", classement.Equipes[0].EquipeNom);
        Assert.Equal(400, classement.Equipes[0].TempsTotalSecs);
        Assert.Equal(4, classement.Equipes[0].Lignes.Count);

        Assert.Equal(200 + 3_600, classement.Equipes[1].TempsTotalSecs);
        Assert.Equal(1, classement.Equipes[1].NbAbandons);
    }

    [Fact]
    public void Le_premier_membre_du_roster_est_capitaine()
    {
        Assert.Equal(Alice.Id, EquipeA.Capitaine!.JoueurId);
        Assert.Equal(Chloe.Id, EquipeB.Capitaine!.JoueurId);
    }
}
