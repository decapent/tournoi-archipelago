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
        Assert.Equal(0, premiere.PenaliteSecs);
        Assert.Equal(0, premiere.NbAbandons);

        var seconde = classement[1];
        Assert.Equal(EquipeB.Id, seconde.EquipeId);
        Assert.Equal(2, seconde.Position);
        Assert.False(seconde.EstGagnante);
        Assert.Equal(7_500, seconde.TempsTotalSecs);
    }

    [Fact]
    public void Un_abandon_ajoute_une_heure_au_temps_du_joueur()
    {
        var classement = ScoringService.ClasserMatch(
            [
                Ligne(Alice, Alttp, tempsFinalSecs: 1_000),
                Ligne(Bob, Metroid, tempsFinalSecs: 2_000, estAbandon: true),
            ],
            [EquipeA]);

        var equipe = Assert.Single(classement);

        Assert.Equal(3_000, equipe.TempsBrutSecs);
        Assert.Equal(ScoringService.PenaliteAbandonSecs, equipe.PenaliteSecs);
        Assert.Equal(3_000 + 3_600, equipe.TempsTotalSecs);
        Assert.Equal(1, equipe.NbAbandons);

        var abandonnee = equipe.Lignes.Single(ligne => ligne.EstAbandon);
        Assert.Equal(2_000, abandonnee.TempsFinalSecs);
        Assert.Equal(5_600, abandonnee.TempsEffectifSecs);

        var terminee = equipe.Lignes.Single(ligne => !ligne.EstAbandon);
        Assert.Equal(1_000, terminee.TempsFinalSecs);
        Assert.Equal(1_000, terminee.TempsEffectifSecs);
    }

    [Fact]
    public void Chaque_abandon_de_l_equipe_est_penalise()
    {
        var classement = ScoringService.ClasserMatch(
            [
                Ligne(Alice, Alttp, tempsFinalSecs: 100, estAbandon: true),
                Ligne(Bob, Metroid, tempsFinalSecs: 200, estAbandon: true),
            ],
            [EquipeA]);

        var equipe = Assert.Single(classement);

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
        var classement = ScoringService.ClasserMatch(
            [
                Ligne(Alice, Alttp, tempsFinalSecs: 60),
                Ligne(Bob, Metroid, tempsFinalSecs: 60, estAbandon: true),
                Ligne(Chloe, Alttp, tempsFinalSecs: 2_000),
                Ligne(David, Metroid, tempsFinalSecs: 2_000),
            ],
            [EquipeA, EquipeB]);

        Assert.Equal(EquipeA.Id, classement[0].EquipeId);
        Assert.Equal(3_720, classement[0].TempsTotalSecs);
        Assert.True(classement[0].EstGagnante);
        Assert.Equal(1, classement[0].NbAbandons);

        Assert.Equal(EquipeB.Id, classement[1].EquipeId);
        Assert.Equal(4_000, classement[1].TempsTotalSecs);
    }

    [Fact]
    public void La_penalite_peut_faire_perdre_une_equipe_pourtant_plus_rapide()
    {
        // Equipe A : 100 s bruts, mais un abandon la porte a 3 700 s.
        // Equipe B : 2 000 s bruts, sans abandon.
        var classement = ScoringService.ClasserMatch(
            [
                Ligne(Alice, Alttp, tempsFinalSecs: 50),
                Ligne(Bob, Metroid, tempsFinalSecs: 50, estAbandon: true),
                Ligne(Chloe, Alttp, tempsFinalSecs: 1_000),
                Ligne(David, Metroid, tempsFinalSecs: 1_000),
            ],
            [EquipeA, EquipeB]);

        Assert.Equal(EquipeB.Id, classement[0].EquipeId);
        Assert.Equal(2_000, classement[0].TempsTotalSecs);

        Assert.Equal(EquipeA.Id, classement[1].EquipeId);
        Assert.Equal(100, classement[1].TempsBrutSecs);
        Assert.Equal(3_700, classement[1].TempsTotalSecs);
    }

    [Fact]
    public void Chaque_equipe_regroupe_les_lignes_de_ses_membres()
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
    public void Une_penalite_peut_creer_une_egalite()
    {
        // Equipe A : 400 s bruts + 3 600 s de penalite = 4 000 s.
        // Equipe B : 4 000 s bruts, sans abandon.
        var classement = ScoringService.ClasserMatch(
            [
                Ligne(Alice, Alttp, tempsFinalSecs: 200),
                Ligne(Bob, Metroid, tempsFinalSecs: 200, estAbandon: true),
                Ligne(Chloe, Alttp, tempsFinalSecs: 2_000),
                Ligne(David, Metroid, tempsFinalSecs: 2_000),
            ],
            [EquipeA, EquipeB]);

        Assert.All(classement, equipe =>
        {
            Assert.Equal(4_000, equipe.TempsTotalSecs);
            Assert.Equal(1, equipe.Position);
            Assert.True(equipe.EstGagnante);
        });
    }

    [Fact]
    public void Le_pourcentage_de_completion_agrege_les_checks_des_participants()
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

        // Format finale : quatre joueurs de chaque cote, dont un abandon cote B.
        var classement = ScoringService.ClasserMatch(
            [
                Ligne(Alice, Alttp, tempsFinalSecs: 100),
                Ligne(Bob, Metroid, tempsFinalSecs: 100),
                Ligne(eve, Alttp, tempsFinalSecs: 100),
                Ligne(gina, Metroid, tempsFinalSecs: 100),
                Ligne(Chloe, Alttp, tempsFinalSecs: 50),
                Ligne(David, Metroid, tempsFinalSecs: 50),
                Ligne(frank, Alttp, tempsFinalSecs: 50),
                Ligne(hugo, Metroid, tempsFinalSecs: 50, estAbandon: true),
            ],
            [equipeA, equipeB]);

        Assert.Equal("Les Nous_", classement[0].EquipeNom);
        Assert.Equal(400, classement[0].TempsTotalSecs);
        Assert.Equal(4, classement[0].Lignes.Count);

        Assert.Equal(200 + 3_600, classement[1].TempsTotalSecs);
        Assert.Equal(1, classement[1].NbAbandons);
    }

    [Fact]
    public void Un_match_sans_resultat_donne_un_classement_vide()
    {
        Assert.Empty(ScoringService.ClasserMatch([], [EquipeA, EquipeB]));
    }
}
