using System.Net;
using System.Net.Http.Json;
using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Tests;

public class ImportLogApiTests
{
    private static readonly DateTime Depart = new(2026, 9, 3, 22, 0, 0);

    /// <summary>
    /// Deux joueurs de la meme equipe. ALPHA termine et libere le reste de son monde, BETA
    /// abandonne. Les temps se comptent depuis <see cref="Depart"/>.
    /// </summary>
    private const string Journal = """
        [2026-09-03 22:10:00,000]: Notice (all): ALPHA (Team #1) playing A Link to the Past has joined. Client(0.6.7), ['AP'].
        [2026-09-03 22:10:01,000]: Notice (all): BETA (Team #1) playing Super Metroid has joined. Client(0.6.7), ['AP'].
        [2026-09-03 22:30:00,000]: (Team #1) ALPHA sent Sword to BETA (Lieu 1)
        [2026-09-03 22:40:00,000]: (Team #1) ALPHA sent Bow to ALPHA (Lieu 2)
        [2026-09-03 22:45:00,000]: (Team #1) BETA sent Missile to ALPHA (Brinstar 1)
        [2026-09-03 23:00:00,000]: Notice (all): ALPHA (Team #1) has completed their goal.
        [2026-09-03 23:00:00,005]: Notice (all): ALPHA (Team #1) has released all remaining items from their world.
        [2026-09-03 23:00:00,006]: (Team #1) ALPHA sent Potion to ALPHA (Lieu jamais visite)
        """;

    [Fact]
    public async Task L_analyse_d_un_journal_n_exige_aucun_match()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();

        var reponse = await client.PostAsJsonAsync(
            "/api/logs/analyse", new AnalyseLogRequest(Journal), ApiDeTest.Json);

        reponse.EnsureSuccessStatusCode();
        var rapport = await reponse.Content.ReadFromJsonAsync<RapportLogDto>(ApiDeTest.Json);

        var alpha = rapport!.Joueurs.Single(j => j.Alias == "ALPHA");
        Assert.Equal("A Link to the Past", alpha.Jeu);
        Assert.Equal(2, alpha.ChecksTrouves);
        Assert.Equal(3, alpha.TotalChecks);

        var beta = rapport.Joueurs.Single(j => j.Alias == "BETA");
        Assert.True(beta.EstAbandon);
        Assert.Null(beta.TotalChecks);
    }

    [Fact]
    public async Task L_analyse_exige_un_jeton()
    {
        using var api = new ApiDeTest();

        var reponse = await api.CreerClientAnonyme().PostAsJsonAsync(
            "/api/logs/analyse", new AnalyseLogRequest(Journal), ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
    }

    [Fact]
    public async Task L_import_complete_les_resultats_de_l_equipe()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var p = await PreparerAsync(api, client);

        var reponse = await client.PostAsJsonAsync(
            $"/api/matchs/{p.MatchId}/equipes/{p.EquipeA.Id}/log",
            new ImportLogRequest(Journal, Depart,
            [
                new("ALPHA", p.Alice.Id),
                new("BETA", p.Bob.Id),
            ]),
            ApiDeTest.Json);

        reponse.EnsureSuccessStatusCode();
        var match = await reponse.Content.ReadFromJsonAsync<MatchDetailDto>(ApiDeTest.Json);

        var equipe = match!.Equipes.Single(e => e.EquipeId == p.EquipeA.Id);
        var alice = equipe.Lignes.Single(l => l.JoueurId == p.Alice.Id);
        var bob = equipe.Lignes.Single(l => l.JoueurId == p.Bob.Id);

        // Alice a termine a 23:00, une heure apres le depart.
        Assert.Equal(2, alice.NbChecks);
        Assert.Equal(3, alice.TotalChecks);
        Assert.Equal(3_600, alice.TempsFinalSecs);
        Assert.False(alice.EstAbandon);

        // Bob a abandonne : son temps est celui de son dernier check, a 22:45.
        Assert.Equal(1, bob.NbChecks);
        Assert.True(bob.EstAbandon);
        Assert.Equal(2_700, bob.TempsFinalSecs);

        // Le total du monde de Bob reste celui qui avait ete saisi : sans liberation, le
        // journal ne le revele pas.
        Assert.Equal(150, bob.TotalChecks);
    }

    [Fact]
    public async Task L_import_enregistre_la_progression_en_secondes_depuis_le_depart()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var p = await PreparerAsync(api, client);

        await ImporterAsync(client, p);

        var progression = await client.GetFromJsonAsync<ProgressionMatchDto>(
            $"/api/matchs/{p.MatchId}/progression", ApiDeTest.Json);

        var alice = progression!.Joueurs.Single(j => j.JoueurId == p.Alice.Id);
        Assert.Equal([1_800, 2_400], alice.Secondes);
        Assert.Equal("Les Nous_", alice.EquipeNom);
        Assert.Equal("A Link to the Past", alice.JeuNom);

        // La liberation n'apparait pas dans la courbe.
        Assert.DoesNotContain(3_600, alice.Secondes);

        var bob = progression.Joueurs.Single(j => j.JoueurId == p.Bob.Id);
        Assert.Equal([2_700], bob.Secondes);
    }

    [Fact]
    public async Task Reimporter_remplace_la_progression_au_lieu_de_l_empiler()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var p = await PreparerAsync(api, client);

        await ImporterAsync(client, p);
        await ImporterAsync(client, p);

        var progression = await client.GetFromJsonAsync<ProgressionMatchDto>(
            $"/api/matchs/{p.MatchId}/progression", ApiDeTest.Json);

        Assert.Equal(2, progression!.Joueurs.Single(j => j.JoueurId == p.Alice.Id).Secondes.Count);
    }

    [Fact]
    public async Task L_import_ne_touche_pas_aux_lignes_de_l_autre_equipe()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var p = await PreparerAsync(api, client);

        var match = await ImporterAsync(client, p);

        var adverse = match.Equipes.Single(e => e.EquipeId == p.EquipeB.Id);
        Assert.All(adverse.Lignes, ligne =>
        {
            Assert.Equal(500, ligne.TempsFinalSecs);
            Assert.Equal(10, ligne.NbChecks);
        });
    }

    [Fact]
    public async Task Un_joueur_hors_de_l_equipe_est_refuse()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var p = await PreparerAsync(api, client);

        var reponse = await client.PostAsJsonAsync(
            $"/api/matchs/{p.MatchId}/equipes/{p.EquipeA.Id}/log",
            new ImportLogRequest(Journal, Depart, [new("ALPHA", p.Chloe.Id)]),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Contains("n'appartient pas a l'equipe", await reponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_pseudonyme_absent_du_journal_est_refuse()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var p = await PreparerAsync(api, client);

        var reponse = await client.PostAsJsonAsync(
            $"/api/matchs/{p.MatchId}/equipes/{p.EquipeA.Id}/log",
            new ImportLogRequest(Journal, Depart, [new("INCONNU", p.Alice.Id)]),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Contains("absent du journal", await reponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_joueur_sans_ligne_de_resultat_est_refuse()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var p = await PreparerAsync(api, client);

        // Anna est bien dans l'equipe, mais ne participe pas a ce match.
        var reponse = await client.PostAsJsonAsync(
            $"/api/matchs/{p.MatchId}/equipes/{p.EquipeA.Id}/log",
            new ImportLogRequest(Journal, Depart, [new("ALPHA", p.Anna.Id)]),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Contains("saisir son jeu et son seed avant d'importer", await reponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Une_equipe_etrangere_au_match_est_refusee()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var p = await PreparerAsync(api, client);

        var reponse = await client.PostAsJsonAsync(
            $"/api/matchs/{p.MatchId}/equipes/9999/log",
            new ImportLogRequest(Journal, Depart, [new("ALPHA", p.Alice.Id)]),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Contains("ne participe pas au match", await reponse.Content.ReadAsStringAsync());
    }

    private static async Task<MatchDetailDto> ImporterAsync(HttpClient client, Plateau p)
    {
        var reponse = await client.PostAsJsonAsync(
            $"/api/matchs/{p.MatchId}/equipes/{p.EquipeA.Id}/log",
            new ImportLogRequest(Journal, Depart,
            [
                new("ALPHA", p.Alice.Id),
                new("BETA", p.Bob.Id),
            ]),
            ApiDeTest.Json);

        reponse.EnsureSuccessStatusCode();
        return (await reponse.Content.ReadFromJsonAsync<MatchDetailDto>(ApiDeTest.Json))!;
    }

    /// <summary>Deux equipes, un match en cours dont les jeux et seeds sont deja saisis.</summary>
    private static async Task<Plateau> PreparerAsync(ApiDeTest api, HttpClient client)
    {
        async Task<JoueurDto> Joueur(string nom)
        {
            var r = await client.PostAsJsonAsync("/api/joueurs", new JoueurUpsertRequest(nom), ApiDeTest.Json);
            r.EnsureSuccessStatusCode();
            return (await r.Content.ReadFromJsonAsync<JoueurDto>(ApiDeTest.Json))!;
        }

        async Task<EquipeDto> Equipe(string nom, params JoueurDto[] membres)
        {
            var r = await client.PostAsJsonAsync(
                "/api/equipes",
                new EquipeUpsertRequest(nom, [.. membres.Select(m => m.Id)], membres[0].Id),
                ApiDeTest.Json);
            r.EnsureSuccessStatusCode();
            return (await r.Content.ReadFromJsonAsync<EquipeDto>(ApiDeTest.Json))!;
        }

        var alice = await Joueur("Alice");
        var bob = await Joueur("Bob");
        var anna = await Joueur("Anna");
        var arthur = await Joueur("Arthur");
        var chloe = await Joueur("Chloe");
        var david = await Joueur("David");
        var claire = await Joueur("Claire");
        var damien = await Joueur("Damien");

        var equipeA = await Equipe("Les Nous_", alice, bob, anna, arthur);
        var equipeB = await Equipe("No M's Land", chloe, david, claire, damien);

        var jeux = await client.GetFromJsonAsync<List<JeuDto>>("/api/jeux", ApiDeTest.Json);
        var alttp = jeux!.Single(j => j.Nom == "A Link to the Past");
        var metroid = jeux.Single(j => j.Nom == "Super Metroid");

        var creation = await client.PostAsJsonAsync("/api/matchs", new MatchUpsertRequest(
            Date: new DateOnly(2026, 9, 3),
            Type: TypeMatch.QUALIFICATION,
            EquipeAId: equipeA.Id,
            EquipeBId: equipeB.Id,
            Resultats:
            [
                // Jeux et seeds saisis d'avance ; checks et temps viendront du journal.
                new(alice.Id, alttp.Id, "seed-a", 200, null, null, false),
                new(bob.Id, metroid.Id, "seed-b", 150, null, null, false),
                new(chloe.Id, alttp.Id, "seed-c", 200, 10, 500, false),
                new(david.Id, metroid.Id, "seed-d", 150, 10, 500, false),
            ]),
            ApiDeTest.Json);

        creation.EnsureSuccessStatusCode();
        var match = (await creation.Content.ReadFromJsonAsync<MatchDetailDto>(ApiDeTest.Json))!;

        return new Plateau(match.Id, equipeA, equipeB, alice, bob, anna, chloe);
    }

    private sealed record Plateau(
        int MatchId,
        EquipeDto EquipeA,
        EquipeDto EquipeB,
        JoueurDto Alice,
        JoueurDto Bob,
        JoueurDto Anna,
        JoueurDto Chloe);
}
