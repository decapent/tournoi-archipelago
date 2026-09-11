using TournoiArchipelago.Api.Domain;
using TournoiArchipelago.Api.Services;

namespace TournoiArchipelago.Api.Tests;

public class StatsServiceTests
{
    [Fact]
    public void La_mediane_d_un_nombre_impair_de_valeurs_est_la_valeur_centrale()
    {
        Assert.Equal(20, StatsService.Mediane([10, 20, 30]));
    }

    [Fact]
    public void La_mediane_d_un_nombre_pair_de_valeurs_est_la_moyenne_des_deux_centrales()
    {
        Assert.Equal(25, StatsService.Mediane([10, 20, 30, 40]));
    }

    [Fact]
    public void La_mediane_arrondit_a_l_entier_le_plus_proche()
    {
        Assert.Equal(16, StatsService.Mediane([10, 21]));
    }

    [Fact]
    public void La_mediane_d_une_liste_vide_est_absente()
    {
        Assert.Null(StatsService.Mediane([]));
    }

    [Fact]
    public async Task Le_classement_cumule_victoires_et_temps_sur_plusieurs_matchs()
    {
        using var contexte = new ContexteDeTest();
        var donnees = await SemerAsync(contexte);

        // Match 1 : equipe A (seed la plus longue 3 600 s) devant equipe B (4 000 s).
        await AjouterMatchAsync(contexte, TypeMatch.TOURNOI, new DateOnly(2026, 9, 1),
        [
            new(donnees.Alice, donnees.Alttp, 3_600, 100, 200),
            new(donnees.Bob, donnees.Metroid, 3_000, 100, 200),
            new(donnees.Chloe, donnees.Alttp, 4_000, 50, 200),
            new(donnees.David, donnees.Metroid, 3_500, 50, 200),
        ]);

        // Match 2 : equipe B (1 000 s) devant equipe A (2 500 s).
        await AjouterMatchAsync(contexte, TypeMatch.TOURNOI, new DateOnly(2026, 9, 2),
        [
            new(donnees.Alice, donnees.Alttp, 2_500, 10, 200),
            new(donnees.Bob, donnees.Metroid, 2_500, 10, 200),
            new(donnees.Chloe, donnees.Alttp, 1_000, 20, 200),
            new(donnees.David, donnees.Metroid, 1_000, 20, 200),
        ]);

        var service = new StatsService(contexte.Creer());
        var classement = await service.ClassementAsync(type: null, TriClassement.Victoires);

        Assert.Equal(2, classement.Count);
        Assert.All(classement, ligne =>
        {
            Assert.Equal(2, ligne.MatchsJoues);
            Assert.Equal(1, ligne.Victoires);
            Assert.Equal(0, ligne.Abandons);
        });

        var equipeA = classement.Single(l => l.EquipeNom == "Les Nous_");
        var equipeB = classement.Single(l => l.EquipeNom == "No M's Land");

        // Le cumul additionne les scores par match, chacun etant la seed la plus longue.
        Assert.Equal(3_600 + 2_500, equipeA.TempsCumuleSecs);
        Assert.Equal(4_000 + 1_000, equipeB.TempsCumuleSecs);
        Assert.Equal(3_050, equipeA.TempsMoyenSecs);
        Assert.Equal(220, equipeA.ChecksTrouves);

        // A egalite de victoires, le plus petit temps cumule passe devant.
        Assert.Equal("No M's Land", classement[0].EquipeNom);
        Assert.Equal(1, classement[0].Position);
    }

    [Fact]
    public async Task Le_tri_par_temps_ignore_le_nombre_de_victoires()
    {
        using var contexte = new ContexteDeTest();
        var donnees = await SemerAsync(contexte);

        // Equipe A gagne, mais avec un temps cumule plus eleve que celui de l'equipe B.
        await AjouterMatchAsync(contexte, TypeMatch.TOURNOI, new DateOnly(2026, 9, 1),
        [
            new(donnees.Alice, donnees.Alttp, 100),
            new(donnees.Bob, donnees.Metroid, 100),
            new(donnees.Chloe, donnees.Alttp, 1_000),
            new(donnees.David, donnees.Metroid, 200, 5, 100, EstAbandon: true),
        ]);

        var service = new StatsService(contexte.Creer());

        var parVictoires = await service.ClassementAsync(null, TriClassement.Victoires);
        Assert.Equal("Les Nous_", parVictoires[0].EquipeNom);

        var parTemps = await service.ClassementAsync(null, TriClassement.Temps);
        Assert.Equal("Les Nous_", parTemps[0].EquipeNom);
        Assert.Equal(100, parTemps[0].TempsCumuleSecs);

        // L'abandon de David (200 s majorees d'une heure) devient la seed la plus longue de
        // son equipe, devant les 1 000 s de Chloe.
        var equipeB = parTemps.Single(l => l.EquipeNom == "No M's Land");
        Assert.Equal(1, equipeB.Abandons);
        Assert.Equal(3_600, equipeB.PenaliteCumuleeSecs);
        Assert.Equal(200 + 3_600, equipeB.TempsCumuleSecs);
        Assert.Equal(3_800, equipeB.TempsMoyenSecs);
    }

    [Fact]
    public async Task Le_classement_filtre_par_type_de_match()
    {
        using var contexte = new ContexteDeTest();
        var donnees = await SemerAsync(contexte);

        await AjouterMatchAsync(contexte, TypeMatch.QUALIFICATION, new DateOnly(2026, 8, 1),
        [
            new(donnees.Alice, donnees.Alttp, 100),
            new(donnees.Bob, donnees.Metroid, 100),
            new(donnees.Chloe, donnees.Alttp, 900),
            new(donnees.David, donnees.Metroid, 900),
        ]);

        var service = new StatsService(contexte.Creer());

        var qualifications = await service.ClassementAsync(TypeMatch.QUALIFICATION, TriClassement.Victoires);
        Assert.Equal(1, qualifications.Single(l => l.EquipeNom == "Les Nous_").MatchsJoues);

        var tournois = await service.ClassementAsync(TypeMatch.TOURNOI, TriClassement.Victoires);
        Assert.All(tournois, ligne => Assert.Equal(0, ligne.MatchsJoues));
    }

    [Fact]
    public async Task Une_equipe_sans_match_est_reportee_en_fin_de_classement()
    {
        using var contexte = new ContexteDeTest();
        var donnees = await SemerAsync(contexte);

        var eve = new Joueur { Nom = "Eve" };
        var frank = new Joueur { Nom = "Frank" };
        contexte.Db.Joueurs.AddRange(eve, frank);
        await contexte.Db.SaveChangesAsync();
        contexte.Db.Equipes.Add(EquipeAvec("AGreatTeam", eve, frank));
        await contexte.Db.SaveChangesAsync();

        await AjouterMatchAsync(contexte, TypeMatch.TOURNOI, new DateOnly(2026, 9, 1),
        [
            new(donnees.Alice, donnees.Alttp, 100),
            new(donnees.Bob, donnees.Metroid, 100),
            new(donnees.Chloe, donnees.Alttp, 900),
            new(donnees.David, donnees.Metroid, 900),
        ]);

        var service = new StatsService(contexte.Creer());
        var classement = await service.ClassementAsync(null, TriClassement.Temps);

        Assert.Equal(3, classement.Count);
        Assert.Equal("AGreatTeam", classement[^1].EquipeNom);
        Assert.Equal(0, classement[^1].MatchsJoues);
    }

    [Fact]
    public async Task Les_stats_par_jeu_agregent_temps_checks_et_abandons()
    {
        using var contexte = new ContexteDeTest();
        var donnees = await SemerAsync(contexte);

        await AjouterMatchAsync(contexte, TypeMatch.TOURNOI, new DateOnly(2026, 9, 1),
        [
            new(donnees.Alice, donnees.Alttp, 100, 40, 200),
            new(donnees.Bob, donnees.Metroid, 500, 100, 200),
            new(donnees.Chloe, donnees.Alttp, 300, 60, 200),
            new(donnees.David, donnees.Alttp, 900, 20, 200, EstAbandon: true),
        ]);

        var service = new StatsService(contexte.Creer());
        var stats = await service.StatsParJeuAsync(type: null);

        var alttp = stats.Single(s => s.JeuId == donnees.Alttp.Id);
        Assert.Equal(3, alttp.NbParties);
        Assert.Equal(1, alttp.NbAbandons);

        // L'abandon de David (900 s) est exclu des temps : il ne mesure pas une completion.
        Assert.Equal(200, alttp.TempsMoyenSecs);
        Assert.Equal(200, alttp.TempsMedianSecs);
        Assert.Equal(100, alttp.MeilleurTempsSecs);
        Assert.Equal("Alice", alttp.MeilleurJoueurNom);
        Assert.Equal(40, alttp.NbChecksMoyen);
        Assert.Equal(0.20, alttp.PourcentCompleteMoyen!.Value, precision: 6);

        var metroid = stats.Single(s => s.JeuId == donnees.Metroid.Id);
        Assert.Equal(1, metroid.NbParties);
        Assert.Equal(500, metroid.MeilleurTempsSecs);
        Assert.Equal("Bob", metroid.MeilleurJoueurNom);
    }

    [Fact]
    public async Task Un_jeu_jamais_joue_apparait_sans_statistique()
    {
        using var contexte = new ContexteDeTest();
        await SemerAsync(contexte);

        var service = new StatsService(contexte.Creer());
        var stats = await service.StatsParJeuAsync(type: null);

        var jamaisJoue = stats.Single(s => s.JeuNom == "Hollow Knight");
        Assert.Equal(0, jamaisJoue.NbParties);
        Assert.Equal(0, jamaisJoue.NbAbandons);
        Assert.Null(jamaisJoue.TempsMoyenSecs);
        Assert.Null(jamaisJoue.TempsMedianSecs);
        Assert.Null(jamaisJoue.MeilleurTempsSecs);
        Assert.Null(jamaisJoue.MeilleurJoueurNom);
        Assert.Null(jamaisJoue.NbChecksMoyen);
        Assert.Null(jamaisJoue.PourcentCompleteMoyen);
    }

    [Fact]
    public async Task Le_bilan_par_joueur_cumule_seeds_victoires_temps_et_checks()
    {
        using var contexte = new ContexteDeTest();
        var donnees = await SemerAsync(contexte);

        // Match 1 : equipe A gagne, la seed d'Alice (3 600 s) etant la plus longue des deux.
        await AjouterMatchAsync(contexte, TypeMatch.TOURNOI, new DateOnly(2026, 9, 1),
        [
            new(donnees.Alice, donnees.Alttp, 3_600, 100, 200),
            new(donnees.Bob, donnees.Metroid, 3_000, 80, 200),
            new(donnees.Chloe, donnees.Alttp, 4_000, 50, 200),
            new(donnees.David, donnees.Metroid, 3_500, 60, 200),
        ]);

        // Match 2 : equipe B gagne, et cette fois c'est Bob qui fait attendre son equipe.
        await AjouterMatchAsync(contexte, TypeMatch.TOURNOI, new DateOnly(2026, 9, 2),
        [
            new(donnees.Alice, donnees.Alttp, 1_800, 150, 200),
            new(donnees.Bob, donnees.Metroid, 2_000, 90, 200),
            new(donnees.Chloe, donnees.Alttp, 1_000, 70, 200),
            new(donnees.David, donnees.Metroid, 900, 60, 200),
        ]);

        var service = new StatsService(contexte.Creer());
        var stats = await service.StatsParJoueurAsync(type: null);

        var alice = stats.Single(s => s.JoueurNom == "Alice");

        Assert.Equal("Les Nous_", alice.EquipeNom);
        Assert.Equal(2, alice.SeedsJouees);
        Assert.Equal(1, alice.Victoires);
        Assert.Equal(0, alice.NbAbandons);

        Assert.Equal(2_700, alice.TempsMoyenSecs);
        Assert.Equal(2_700, alice.TempsMedianSecs);
        Assert.Equal(1_800, alice.MeilleurTempsSecs);
        Assert.Equal("A Link to the Past", alice.MeilleurJeuNom);

        Assert.Equal(250, alice.ChecksTrouves);
        Assert.Equal(0.625, alice.PourcentCompleteMoyen);

        // 250 checks en 5 400 s de jeu, soit une heure et demie.
        Assert.Equal(166.67, alice.ChecksParHeure!.Value, 2);

        // Chaque equipe a ete retenue une fois sur la seed d'Alice, une fois sur celle de Bob.
        Assert.Equal(1, alice.SeedsDeterminantes);
        Assert.Equal(1, stats.Single(s => s.JoueurNom == "Bob").SeedsDeterminantes);

        // Cote adverse, Chloe a ete la plus lente des deux fois.
        Assert.Equal(2, stats.Single(s => s.JoueurNom == "Chloe").SeedsDeterminantes);
        Assert.Equal(0, stats.Single(s => s.JoueurNom == "David").SeedsDeterminantes);
    }

    [Fact]
    public async Task Un_abandon_compte_comme_seed_jouee_mais_sort_des_temps()
    {
        using var contexte = new ContexteDeTest();
        var donnees = await SemerAsync(contexte);

        await AjouterMatchAsync(contexte, TypeMatch.TOURNOI, new DateOnly(2026, 9, 1),
        [
            new(donnees.Alice, donnees.Alttp, 60, 20, 200, EstAbandon: true),
            new(donnees.Bob, donnees.Metroid, 3_000, 80, 200),
            new(donnees.Chloe, donnees.Alttp, 5_000, 50, 200),
            new(donnees.David, donnees.Metroid, 4_000, 60, 200),
        ]);

        var service = new StatsService(contexte.Creer());
        var stats = await service.StatsParJoueurAsync(type: null);

        var alice = stats.Single(s => s.JoueurNom == "Alice");

        Assert.Equal(1, alice.SeedsJouees);
        Assert.Equal(1, alice.NbAbandons);

        // Un abandon ne mesure pas une completion : il ne nourrit ni moyenne, ni mediane,
        // ni meilleur temps, ni rythme.
        Assert.Null(alice.TempsMoyenSecs);
        Assert.Null(alice.TempsMedianSecs);
        Assert.Null(alice.MeilleurTempsSecs);
        Assert.Null(alice.MeilleurJeuNom);
        Assert.Null(alice.ChecksParHeure);

        // Ses checks restent comptes, eux.
        Assert.Equal(20, alice.ChecksTrouves);
        Assert.Equal(0.1, alice.PourcentCompleteMoyen);

        // Son abandon majore d'une heure donne 3 660 s, devant les 3 000 s de Bob : c'est
        // donc elle qui a fixe le temps de l'equipe, malgre un temps brut de 60 s.
        Assert.Equal(1, alice.SeedsDeterminantes);
        Assert.Equal(0, stats.Single(s => s.JoueurNom == "Bob").SeedsDeterminantes);

        // L'equipe gagne quand meme : 3 660 s contre 5 000 s.
        Assert.Equal(1, alice.Victoires);
    }

    [Fact]
    public async Task Le_bilan_par_joueur_respecte_le_filtre_de_type()
    {
        using var contexte = new ContexteDeTest();
        var donnees = await SemerAsync(contexte);

        await AjouterMatchAsync(contexte, TypeMatch.QUALIFICATION, new DateOnly(2026, 8, 1),
        [
            new(donnees.Alice, donnees.Alttp, 3_600, 100, 200),
            new(donnees.Bob, donnees.Metroid, 3_000, 80, 200),
            new(donnees.Chloe, donnees.Alttp, 4_000, 50, 200),
            new(donnees.David, donnees.Metroid, 3_500, 60, 200),
        ]);

        var service = new StatsService(contexte.Creer());

        Assert.Equal(1, (await service.StatsParJoueurAsync(TypeMatch.QUALIFICATION))
            .Single(s => s.JoueurNom == "Alice").SeedsJouees);

        Assert.Equal(0, (await service.StatsParJoueurAsync(TypeMatch.TOURNOI))
            .Single(s => s.JoueurNom == "Alice").SeedsJouees);
    }

    [Fact]
    public async Task Un_joueur_sans_match_apparait_sans_statistique()
    {
        using var contexte = new ContexteDeTest();
        await SemerAsync(contexte);

        var service = new StatsService(contexte.Creer());
        var stats = await service.StatsParJoueurAsync(type: null);

        var alice = stats.Single(s => s.JoueurNom == "Alice");

        Assert.Equal(0, alice.SeedsJouees);
        Assert.Equal(0, alice.Victoires);
        Assert.Equal(0, alice.ChecksTrouves);
        Assert.Equal(0, alice.SeedsDeterminantes);
        Assert.Null(alice.TempsMoyenSecs);
        Assert.Null(alice.PourcentCompleteMoyen);
        Assert.Null(alice.ChecksParHeure);

        // Il figure quand meme dans la liste, avec son equipe.
        Assert.Equal("Les Nous_", alice.EquipeNom);
    }

    /// <summary>Equipe prete a etre inseree, avec son roster.</summary>
    private static Equipe EquipeAvec(string nom, params Joueur[] membres) => new()
    {
        Nom = nom,
        Membres = [.. membres.Select((joueur, rang) => new EquipeJoueur
        {
            JoueurId = joueur.Id,
            EstCapitaine = rang == 0,
        })],
    };

    private static async Task<DonneesSemees> SemerAsync(ContexteDeTest contexte)
    {
        var db = contexte.Db;

        var alice = new Joueur { Nom = "Alice" };
        var bob = new Joueur { Nom = "Bob" };
        var chloe = new Joueur { Nom = "Chloe" };
        var david = new Joueur { Nom = "David" };
        db.Joueurs.AddRange(alice, bob, chloe, david);
        await db.SaveChangesAsync();

        db.Equipes.AddRange(
            EquipeAvec("Les Nous_", alice, bob),
            EquipeAvec("No M's Land", chloe, david));
        await db.SaveChangesAsync();

        // Les jeux viennent du seed du modele.
        var alttp = db.Jeux.Single(j => j.Nom == "A Link to the Past");
        var metroid = db.Jeux.Single(j => j.Nom == "Super Metroid");

        return new DonneesSemees(alice, bob, chloe, david, alttp, metroid);
    }

    private static async Task AjouterMatchAsync(
        ContexteDeTest contexte,
        TypeMatch type,
        DateOnly date,
        IEnumerable<Saisie> lignes)
    {
        await using var db = contexte.Creer();

        var saisies = lignes.ToList();

        // Les equipes engagees se deduisent des joueurs semes : un match doit les porter,
        // sans quoi ScoringService ne le considere jamais complet.
        var joueurIds = saisies.Select(l => l.Joueur.Id).ToList();
        var equipeIds = db.EquipeJoueurs
            .Where(ej => joueurIds.Contains(ej.JoueurId))
            .Select(ej => ej.EquipeId)
            .Distinct()
            .ToList();

        var match = new Match
        {
            Date = date,
            Type = type,
            Equipes = [.. equipeIds.Select(id => new MatchEquipe { EquipeId = id })],
        };

        db.Matchs.Add(match);
        await db.SaveChangesAsync();

        foreach (var ligne in saisies)
        {
            db.MatchJeux.Add(new MatchJeu
            {
                MatchId = match.Id,
                JeuId = ligne.Jeu.Id,
                JoueurId = ligne.Joueur.Id,
                TempsFinalSecs = ligne.Temps,
                EstAbandon = ligne.EstAbandon,
                NbChecks = ligne.NbChecks,
                TotalChecks = ligne.TotalChecks,
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Une ligne de resultat a semer. <c>Temps</c> a null signifie un resultat encore a saisir.
    /// </summary>
    private sealed record Saisie(
        Joueur Joueur,
        Jeu Jeu,
        int? Temps,
        int? NbChecks = null,
        int? TotalChecks = null,
        bool EstAbandon = false);

    private sealed record DonneesSemees(
        Joueur Alice,
        Joueur Bob,
        Joueur Chloe,
        Joueur David,
        Jeu Alttp,
        Jeu Metroid);
}
