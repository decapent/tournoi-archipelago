using TournoiArchipelago.Api.Services;
using static TournoiArchipelago.Api.Tests.ConstructeurDonnees;

namespace TournoiArchipelago.Api.Tests;

public class ScoringServiceTests
{
    private static readonly Api.Domain.Joueur Alice = Joueur(1, "Alice");
    private static readonly Api.Domain.Joueur Bob = Joueur(2, "Bob");
    private static readonly Api.Domain.Joueur Chloe = Joueur(3, "Chloe");
    private static readonly Api.Domain.Joueur David = Joueur(4, "David");

    private static readonly Api.Domain.Jeu Alttp = Jeu(1, "A Link to the Past");
    private static readonly Api.Domain.Jeu Metroid = Jeu(2, "Super Metroid");

    private static readonly Api.Domain.Equipe EquipeA = Equipe(10, "Les Nous_", Alice, Bob);
    private static readonly Api.Domain.Equipe EquipeB = Equipe(20, "No M's Land", Chloe, David);

    [Fact]
    public void Le_total_le_plus_faible_gagne()
    {
        var classement = ScoringService.ClasserMatch(
            [
                Ligne(Alice, Alttp, tempsFinalSecs: 3_600),
                Ligne(Bob, Metroid, tempsFinalSecs: 3_000),   // equipe A : 6 600 s
                Ligne(Chloe, Alttp, tempsFinalSecs: 4_000),
                Ligne(David, Metroid, tempsFinalSecs: 3_500), // equipe B : 7 500 s
            ],
            [EquipeA, EquipeB]);

        Assert.Equal(2, classement.Count);

        var premiere = classement[0];
        Assert.Equal(EquipeA.Id, premiere.EquipeId);
        Assert.Equal(1, premiere.Position);
        Assert.True(premiere.EstGagnante);
        Assert.Equal(6_600, premiere.TempsTotalSecs);
        Assert.False(premiere.EstAbandon);

        var seconde = classement[1];
        Assert.Equal(EquipeB.Id, seconde.EquipeId);
        Assert.Equal(2, seconde.Position);
        Assert.False(seconde.EstGagnante);
        Assert.Equal(7_500, seconde.TempsTotalSecs);
    }

    [Fact]
    public void Chaque_equipe_regroupe_les_deux_lignes_de_ses_membres()
    {
        var classement = ScoringService.ClasserMatch(
            [
                Ligne(Alice, Alttp, tempsFinalSecs: 100),
                Ligne(Bob, Metroid, tempsFinalSecs: 200),
                Ligne(Chloe, Alttp, tempsFinalSecs: 300),
                Ligne(David, Metroid, tempsFinalSecs: 400),
            ],
            [EquipeA, EquipeB]);

        Assert.All(classement, equipe => Assert.Equal(2, equipe.Lignes.Count));
        Assert.Equal("Les Nous_", classement.Single(e => e.EquipeId == EquipeA.Id).EquipeNom);
        Assert.Equal("No M's Land", classement.Single(e => e.EquipeId == EquipeB.Id).EquipeNom);
    }

    [Fact]
    public void A_egalite_parfaite_les_deux_equipes_partagent_la_premiere_place()
    {
        var classement = ScoringService.ClasserMatch(
            [
                Ligne(Alice, Alttp, tempsFinalSecs: 1_000),
                Ligne(Bob, Metroid, tempsFinalSecs: 2_000),
                Ligne(Chloe, Alttp, tempsFinalSecs: 1_500),
                Ligne(David, Metroid, tempsFinalSecs: 1_500),
            ],
            [EquipeA, EquipeB]);

        Assert.All(classement, equipe =>
        {
            Assert.Equal(1, equipe.Position);
            Assert.True(equipe.EstGagnante);
            Assert.Equal(3_000, equipe.TempsTotalSecs);
        });
    }

    [Fact]
    public void Une_equipe_avec_un_abandon_passe_apres_une_equipe_complete_plus_lente()
    {
        var classement = ScoringService.ClasserMatch(
            [
                // Equipe A : Bob abandonne, malgre le temps tres rapide d'Alice.
                Ligne(Alice, Alttp, tempsFinalSecs: 60),
                Ligne(Bob, Metroid, tempsFinalSecs: null),
                // Equipe B : les deux terminent, mais lentement.
                Ligne(Chloe, Alttp, tempsFinalSecs: 20_000),
                Ligne(David, Metroid, tempsFinalSecs: 20_000),
            ],
            [EquipeA, EquipeB]);

        Assert.Equal(EquipeB.Id, classement[0].EquipeId);
        Assert.True(classement[0].EstGagnante);

        Assert.Equal(EquipeA.Id, classement[1].EquipeId);
        Assert.True(classement[1].EstAbandon);
        Assert.False(classement[1].EstGagnante);
        Assert.Equal(2, classement[1].Position);
    }

    [Fact]
    public void Entre_deux_abandons_le_plus_grand_nombre_de_checks_trouves_gagne()
    {
        var classement = ScoringService.ClasserMatch(
            [
                // Equipe A : 40 checks trouves, mais un temps partiel tres rapide.
                Ligne(Alice, Alttp, tempsFinalSecs: 100, nbChecks: 30),
                Ligne(Bob, Metroid, tempsFinalSecs: null, nbChecks: 10),
                // Equipe B : 120 checks trouves.
                Ligne(Chloe, Alttp, tempsFinalSecs: null, nbChecks: 70),
                Ligne(David, Metroid, tempsFinalSecs: null, nbChecks: 50),
            ],
            [EquipeA, EquipeB]);

        Assert.All(classement, equipe => Assert.True(equipe.EstAbandon));

        Assert.Equal(EquipeB.Id, classement[0].EquipeId);
        Assert.Equal(120, classement[0].ChecksTrouves);
        Assert.Equal(1, classement[0].Position);

        Assert.Equal(EquipeA.Id, classement[1].EquipeId);
        Assert.Equal(40, classement[1].ChecksTrouves);
        Assert.Equal(2, classement[1].Position);
    }

    [Fact]
    public void Le_pourcentage_de_completion_agrege_les_checks_des_deux_joueurs()
    {
        var classement = ScoringService.ClasserMatch(
            [
                Ligne(Alice, Alttp, tempsFinalSecs: 100, nbChecks: 60, totalChecks: 100),
                Ligne(Bob, Metroid, tempsFinalSecs: 100, nbChecks: 30, totalChecks: 200),
            ],
            [EquipeA]);

        var equipe = Assert.Single(classement);
        Assert.Equal(90, equipe.ChecksTrouves);
        Assert.Equal(300, equipe.TotalChecks);
        Assert.Equal(0.30, equipe.PourcentComplete!.Value, precision: 6);
    }

    [Fact]
    public void Le_pourcentage_est_absent_quand_le_total_de_checks_est_inconnu()
    {
        var classement = ScoringService.ClasserMatch(
            [
                Ligne(Alice, Alttp, tempsFinalSecs: 100, nbChecks: 60),
                Ligne(Bob, Metroid, tempsFinalSecs: 100, nbChecks: 30),
            ],
            [EquipeA]);

        var equipe = Assert.Single(classement);
        Assert.Null(equipe.TotalChecks);
        Assert.Null(equipe.PourcentComplete);
        Assert.Equal(90, equipe.ChecksTrouves);
    }

    [Fact]
    public void Un_joueur_sans_equipe_est_regroupe_a_part_sans_faire_echouer_le_classement()
    {
        var eve = Joueur(5, "Eve");

        var classement = ScoringService.ClasserMatch(
            [
                Ligne(Alice, Alttp, tempsFinalSecs: 500),
                Ligne(Bob, Metroid, tempsFinalSecs: 500),
                Ligne(eve, Alttp, tempsFinalSecs: 100),
            ],
            [EquipeA]);

        Assert.Equal(2, classement.Count);

        var orpheline = classement.Single(e => e.EquipeId is null);
        Assert.Equal(ScoringService.NomEquipeInconnue, orpheline.EquipeNom);
        Assert.Equal(100, orpheline.TempsTotalSecs);

        // Elle reste classee sur son temps : ici le plus rapide.
        Assert.Equal(1, orpheline.Position);
    }

    [Fact]
    public void Les_lignes_d_une_equipe_sont_triees_par_nom_de_joueur()
    {
        var classement = ScoringService.ClasserMatch(
            [
                Ligne(Bob, Metroid, tempsFinalSecs: 100),
                Ligne(Alice, Alttp, tempsFinalSecs: 200),
            ],
            [EquipeA]);

        var equipe = Assert.Single(classement);
        Assert.Equal(["Alice", "Bob"], equipe.Lignes.Select(l => l.JoueurNom));
    }

    [Fact]
    public void Le_classement_s_adapte_a_trois_joueurs_par_equipe()
    {
        var eve = Joueur(5, "Eve");
        var frank = Joueur(6, "Frank");
        var equipeA = Equipe(10, "Les Nous_", Alice, Bob, eve);
        var equipeB = Equipe(20, "No M's Land", Chloe, David, frank);

        // Format demi-finale : trois joueurs de chaque cote, aucun nombre n'est code en dur.
        var classement = ScoringService.ClasserMatch(
            [
                Ligne(Alice, Alttp, tempsFinalSecs: 1_000),
                Ligne(Bob, Metroid, tempsFinalSecs: 1_000),
                Ligne(eve, Alttp, tempsFinalSecs: 1_000),
                Ligne(Chloe, Alttp, tempsFinalSecs: 2_000),
                Ligne(David, Metroid, tempsFinalSecs: 2_000),
                Ligne(frank, Metroid, tempsFinalSecs: 2_000),
            ],
            [equipeA, equipeB]);

        Assert.Equal(3_000, classement[0].TempsTotalSecs);
        Assert.Equal("Les Nous_", classement[0].EquipeNom);
        Assert.Equal(3, classement[0].Lignes.Count);
        Assert.Equal(6_000, classement[1].TempsTotalSecs);
    }

    [Fact]
    public void Le_classement_s_adapte_a_quatre_joueurs_par_equipe()
    {
        var eve = Joueur(5, "Eve");
        var frank = Joueur(6, "Frank");
        var gina = Joueur(7, "Gina");
        var hugo = Joueur(8, "Hugo");
        var equipeA = Equipe(10, "Les Nous_", Alice, Bob, eve, gina);
        var equipeB = Equipe(20, "No M's Land", Chloe, David, frank, hugo);

        // Format finale : quatre joueurs de chaque cote.
        var classement = ScoringService.ClasserMatch(
            [
                Ligne(Alice, Alttp, tempsFinalSecs: 100),
                Ligne(Bob, Metroid, tempsFinalSecs: 100),
                Ligne(eve, Alttp, tempsFinalSecs: 100),
                Ligne(gina, Metroid, tempsFinalSecs: 100),
                Ligne(Chloe, Alttp, tempsFinalSecs: 50),
                Ligne(David, Metroid, tempsFinalSecs: 50),
                Ligne(frank, Alttp, tempsFinalSecs: 50),
                Ligne(hugo, Metroid, tempsFinalSecs: 50),
            ],
            [equipeA, equipeB]);

        Assert.Equal("No M's Land", classement[0].EquipeNom);
        Assert.Equal(200, classement[0].TempsTotalSecs);
        Assert.Equal(4, classement[0].Lignes.Count);
        Assert.Equal(400, classement[1].TempsTotalSecs);
    }

    [Fact]
    public void Un_match_sans_resultat_donne_un_classement_vide()
    {
        Assert.Empty(ScoringService.ClasserMatch([], [EquipeA, EquipeB]));
    }
}
