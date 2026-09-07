using System.Net;
using System.Net.Http.Json;
using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Tests;

public class MatchsApiTests
{
    [Fact]
    public async Task Une_ecriture_sans_jeton_est_refusee()
    {
        using var api = new ApiDeTest();
        var client = api.CreerClientAnonyme();

        var reponse = await client.PostAsJsonAsync("/api/joueurs", new JoueurUpsertRequest("Alice"), ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
    }

    [Fact]
    public async Task Les_lectures_restent_publiques()
    {
        using var api = new ApiDeTest();
        var client = api.CreerClientAnonyme();

        var jeux = await client.GetFromJsonAsync<List<JeuDto>>("/api/jeux", ApiDeTest.Json);

        Assert.NotNull(jeux);
        Assert.NotEmpty(jeux);
    }

    [Fact]
    public async Task Un_mauvais_mot_de_passe_est_refuse()
    {
        using var api = new ApiDeTest();
        var client = api.CreerClientAnonyme();

        var reponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(ApiDeTest.Username, "mauvais"),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
    }

    [Fact]
    public async Task Un_match_complet_est_enregistre_et_classe()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var plateau = await PreparerPlateauAsync(api, client);

        var reponse = await client.PostAsJsonAsync("/api/matchs", new MatchUpsertRequest(
            Date: new DateOnly(2026, 9, 5),
            Type: TypeMatch.TOURNOI,
            EquipeAId: plateau.EquipeA.Id,
            EquipeBId: plateau.EquipeB.Id,
            Resultats:
            [
                new(plateau.Alice.Id, plateau.Alttp.Id, "seed-1", 200, 200, 3_600),
                new(plateau.Bob.Id, plateau.Metroid.Id, "seed-2", 150, 150, 3_000),
                new(plateau.Chloe.Id, plateau.Alttp.Id, "seed-3", 200, 180, 4_000),
                new(plateau.David.Id, plateau.Metroid.Id, "seed-4", 150, 140, 3_500),
            ]),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);

        var match = await reponse.Content.ReadFromJsonAsync<MatchDetailDto>(ApiDeTest.Json);
        Assert.NotNull(match);
        Assert.Equal(TypeMatch.TOURNOI, match.Type);
        Assert.Equal(2, match.Equipes.Count);

        var gagnante = match.Equipes.Single(e => e.EstGagnante);
        Assert.Equal(plateau.EquipeA.Id, gagnante.EquipeId);
        Assert.Equal(6_600, gagnante.TempsTotalSecs);
        Assert.Equal(2, gagnante.Lignes.Count);

        // Le match remonte aussi dans l'historique et dans le classement.
        var historique = await client.GetFromJsonAsync<List<MatchSommaireDto>>("/api/matchs", ApiDeTest.Json);
        Assert.Equal("Les Nous_", Assert.Single(historique!).EquipeGagnanteNom);

        var classement = await client.GetFromJsonAsync<List<ClassementEquipeDto>>(
            "/api/stats/classement", ApiDeTest.Json);
        Assert.Equal(1, classement!.Single(l => l.EquipeNom == "Les Nous_").Victoires);
    }

    [Fact]
    public async Task Un_temps_vide_enregistre_un_abandon()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var plateau = await PreparerPlateauAsync(api, client);

        var reponse = await client.PostAsJsonAsync("/api/matchs", new MatchUpsertRequest(
            Date: new DateOnly(2026, 9, 6),
            Type: TypeMatch.QUALIFICATION,
            EquipeAId: plateau.EquipeA.Id,
            EquipeBId: plateau.EquipeB.Id,
            Resultats:
            [
                new(plateau.Alice.Id, plateau.Alttp.Id, null, 200, 200, 60),
                new(plateau.Bob.Id, plateau.Metroid.Id, null, 150, 20, null),
                new(plateau.Chloe.Id, plateau.Alttp.Id, null, 200, 200, 20_000),
                new(plateau.David.Id, plateau.Metroid.Id, null, 150, 150, 20_000),
            ]),
            ApiDeTest.Json);

        reponse.EnsureSuccessStatusCode();
        var match = await reponse.Content.ReadFromJsonAsync<MatchDetailDto>(ApiDeTest.Json);

        var abandonnante = match!.Equipes.Single(e => e.EquipeId == plateau.EquipeA.Id);
        Assert.True(abandonnante.EstAbandon);
        Assert.False(abandonnante.EstGagnante);
        Assert.Equal(2, abandonnante.Position);
        Assert.Contains(abandonnante.Lignes, ligne => ligne.EstAbandon && ligne.TempsFinalSecs is null);

        Assert.True(match.Equipes.Single(e => e.EquipeId == plateau.EquipeB.Id).EstGagnante);
    }

    [Fact]
    public async Task Un_match_avec_un_seul_joueur_par_equipe_est_refuse()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var plateau = await PreparerPlateauAsync(api, client);

        var reponse = await client.PostAsJsonAsync("/api/matchs", new MatchUpsertRequest(
            Date: new DateOnly(2026, 9, 5),
            Type: TypeMatch.TOURNOI,
            EquipeAId: plateau.EquipeA.Id,
            EquipeBId: plateau.EquipeB.Id,
            Resultats:
            [
                // Un seul joueur de chaque cote : sous le minimum du format qualification.
                new(plateau.Alice.Id, plateau.Alttp.Id, null, null, null, 100),
                new(plateau.Chloe.Id, plateau.Metroid.Id, null, null, null, 100),
            ]),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Contains("entre 2 et 4 joueurs", await reponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_joueur_etranger_aux_deux_equipes_est_refuse()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var plateau = await PreparerPlateauAsync(api, client);

        var eve = await CreerJoueurAsync(client, "Eve");

        var reponse = await client.PostAsJsonAsync("/api/matchs", new MatchUpsertRequest(
            Date: new DateOnly(2026, 9, 5),
            Type: TypeMatch.TOURNOI,
            EquipeAId: plateau.EquipeA.Id,
            EquipeBId: plateau.EquipeB.Id,
            Resultats:
            [
                new(plateau.Alice.Id, plateau.Alttp.Id, null, null, null, 100),
                new(plateau.Bob.Id, plateau.Metroid.Id, null, null, null, 100),
                new(plateau.Chloe.Id, plateau.Alttp.Id, null, null, null, 100),
                new(eve.Id, plateau.Metroid.Id, null, null, null, 100),
            ]),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);

        Assert.Contains(
            "ne font pas partie des deux equipes",
            await reponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_joueur_saisi_deux_fois_dans_le_meme_match_est_refuse()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var plateau = await PreparerPlateauAsync(api, client);

        var reponse = await client.PostAsJsonAsync("/api/matchs", new MatchUpsertRequest(
            Date: new DateOnly(2026, 9, 5),
            Type: TypeMatch.TOURNOI,
            EquipeAId: plateau.EquipeA.Id,
            EquipeBId: plateau.EquipeB.Id,
            Resultats:
            [
                new(plateau.Alice.Id, plateau.Alttp.Id, null, null, null, 100),
                new(plateau.Alice.Id, plateau.Metroid.Id, null, null, null, 100),
                new(plateau.Chloe.Id, plateau.Alttp.Id, null, null, null, 100),
                new(plateau.David.Id, plateau.Metroid.Id, null, null, null, 100),
            ]),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Contains("une seule fois par match", await reponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Des_checks_trouves_superieurs_au_total_sont_refuses()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var plateau = await PreparerPlateauAsync(api, client);

        var reponse = await client.PostAsJsonAsync("/api/matchs", new MatchUpsertRequest(
            Date: new DateOnly(2026, 9, 5),
            Type: TypeMatch.TOURNOI,
            EquipeAId: plateau.EquipeA.Id,
            EquipeBId: plateau.EquipeB.Id,
            Resultats:
            [
                new(plateau.Alice.Id, plateau.Alttp.Id, null, 100, 150, 100),
                new(plateau.Bob.Id, plateau.Metroid.Id, null, null, null, 100),
                new(plateau.Chloe.Id, plateau.Alttp.Id, null, null, null, 100),
                new(plateau.David.Id, plateau.Metroid.Id, null, null, null, 100),
            ]),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Contains("depasser le total du jeu", await reponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_joueur_ne_peut_appartenir_qu_a_une_seule_equipe()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var plateau = await PreparerPlateauAsync(api, client);

        var eve = await CreerJoueurAsync(client, "Eve");
        var frank = await CreerJoueurAsync(client, "Frank");
        var gina = await CreerJoueurAsync(client, "Gina");

        // Alice appartient deja a l'equipe A.
        var reponse = await client.PostAsJsonAsync(
            "/api/equipes",
            new EquipeUpsertRequest("O.J.M.I.", [plateau.Alice.Id, eve.Id, frank.Id, gina.Id]),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Contains(
            "fait deja partie de l'equipe Les Nous_",
            await reponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_roster_incomplet_est_refuse()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var alice = await CreerJoueurAsync(client, "Alice");
        var bob = await CreerJoueurAsync(client, "Bob");

        var reponse = await client.PostAsJsonAsync(
            "/api/equipes",
            new EquipeUpsertRequest("4G0L", [alice.Id, bob.Id]),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Contains("exactement 4 joueurs distincts", await reponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_roster_avec_un_joueur_en_double_est_refuse()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var alice = await CreerJoueurAsync(client, "Alice");
        var bob = await CreerJoueurAsync(client, "Bob");
        var chloe = await CreerJoueurAsync(client, "Chloe");

        // Les doublons sont dedupliques : il ne reste que trois joueurs distincts.
        var reponse = await client.PostAsJsonAsync(
            "/api/equipes",
            new EquipeUpsertRequest("4G0L", [alice.Id, bob.Id, chloe.Id, alice.Id]),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Contains("exactement 4 joueurs distincts", await reponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Deux_equipes_ne_peuvent_pas_porter_le_meme_nom()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var plateau = await PreparerPlateauAsync(api, client);

        var membres = new List<int>();
        foreach (var nom in new[] { "Eve", "Frank", "Gina", "Hugo" })
        {
            membres.Add((await CreerJoueurAsync(client, nom)).Id);
        }

        var reponse = await client.PostAsJsonAsync(
            "/api/equipes",
            new EquipeUpsertRequest(plateau.EquipeA.Nom, membres),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Contains("porte deja le nom", await reponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_match_de_finale_aligne_quatre_joueurs_par_equipe()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var plateau = await PreparerPlateauAsync(api, client);

        var reponse = await client.PostAsJsonAsync("/api/matchs", new MatchUpsertRequest(
            Date: new DateOnly(2026, 10, 1),
            Type: TypeMatch.TOURNOI,
            EquipeAId: plateau.EquipeA.Id,
            EquipeBId: plateau.EquipeB.Id,
            Resultats:
            [
                new(plateau.Alice.Id, plateau.Alttp.Id, null, null, null, 100),
                new(plateau.Bob.Id, plateau.Metroid.Id, null, null, null, 100),
                new(plateau.Anna.Id, plateau.Alttp.Id, null, null, null, 100),
                new(plateau.Arthur.Id, plateau.Metroid.Id, null, null, null, 100),
                new(plateau.Chloe.Id, plateau.Alttp.Id, null, null, null, 200),
                new(plateau.David.Id, plateau.Metroid.Id, null, null, null, 200),
                new(plateau.Claire.Id, plateau.Alttp.Id, null, null, null, 200),
                new(plateau.Damien.Id, plateau.Metroid.Id, null, null, null, 200),
            ]),
            ApiDeTest.Json);

        reponse.EnsureSuccessStatusCode();
        var match = await reponse.Content.ReadFromJsonAsync<MatchDetailDto>(ApiDeTest.Json);

        var gagnante = match!.Equipes.Single(e => e.EstGagnante);
        Assert.Equal(plateau.EquipeA.Id, gagnante.EquipeId);
        Assert.Equal(4, gagnante.Lignes.Count);
        Assert.Equal(400, gagnante.TempsTotalSecs);
        Assert.Equal(800, match.Equipes.Single(e => !e.EstGagnante).TempsTotalSecs);
    }

    [Fact]
    public async Task Un_match_de_demi_finale_aligne_trois_joueurs_par_equipe()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var plateau = await PreparerPlateauAsync(api, client);

        var reponse = await client.PostAsJsonAsync("/api/matchs", new MatchUpsertRequest(
            Date: new DateOnly(2026, 9, 20),
            Type: TypeMatch.TOURNOI,
            EquipeAId: plateau.EquipeA.Id,
            EquipeBId: plateau.EquipeB.Id,
            Resultats:
            [
                new(plateau.Alice.Id, plateau.Alttp.Id, null, null, null, 100),
                new(plateau.Bob.Id, plateau.Metroid.Id, null, null, null, 100),
                new(plateau.Anna.Id, plateau.Alttp.Id, null, null, null, 100),
                new(plateau.Chloe.Id, plateau.Alttp.Id, null, null, null, 200),
                new(plateau.David.Id, plateau.Metroid.Id, null, null, null, 200),
                new(plateau.Claire.Id, plateau.Alttp.Id, null, null, null, 200),
            ]),
            ApiDeTest.Json);

        reponse.EnsureSuccessStatusCode();
        var match = await reponse.Content.ReadFromJsonAsync<MatchDetailDto>(ApiDeTest.Json);

        Assert.All(match!.Equipes, equipe => Assert.Equal(3, equipe.Lignes.Count));
        Assert.Equal(300, match.Equipes.Single(e => e.EstGagnante).TempsTotalSecs);
    }

    [Fact]
    public async Task Un_match_ou_les_equipes_n_alignent_pas_le_meme_nombre_de_joueurs_est_refuse()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var plateau = await PreparerPlateauAsync(api, client);

        var reponse = await client.PostAsJsonAsync("/api/matchs", new MatchUpsertRequest(
            Date: new DateOnly(2026, 10, 2),
            Type: TypeMatch.TOURNOI,
            EquipeAId: plateau.EquipeA.Id,
            EquipeBId: plateau.EquipeB.Id,
            Resultats:
            [
                // Trois joueurs d'un cote, deux de l'autre.
                new(plateau.Alice.Id, plateau.Alttp.Id, null, null, null, 100),
                new(plateau.Bob.Id, plateau.Metroid.Id, null, null, null, 100),
                new(plateau.Anna.Id, plateau.Alttp.Id, null, null, null, 100),
                new(plateau.Chloe.Id, plateau.Alttp.Id, null, null, null, 200),
                new(plateau.David.Id, plateau.Metroid.Id, null, null, null, 200),
            ]),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Contains("le meme nombre de joueurs", await reponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_nom_de_joueur_deja_pris_est_refuse()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        await CreerJoueurAsync(client, "Alice");

        var reponse = await client.PostAsJsonAsync(
            "/api/joueurs",
            new JoueurUpsertRequest("Alice"),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
    }

    [Fact]
    public async Task Un_joueur_avec_des_resultats_ne_peut_pas_etre_supprime()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var plateau = await PreparerPlateauAsync(api, client);
        await EnregistrerMatchSimpleAsync(client, plateau);

        var reponse = await client.DeleteAsync($"/api/joueurs/{plateau.Alice.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Contains("resultats enregistres", await reponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task La_correction_d_un_match_remplace_ses_resultats()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var plateau = await PreparerPlateauAsync(api, client);
        var match = await EnregistrerMatchSimpleAsync(client, plateau);

        // Bob change de jeu, ce qui modifie la cle primaire de sa ligne, et devient plus lent.
        var reponse = await client.PutAsJsonAsync($"/api/matchs/{match.Id}", new MatchUpsertRequest(
            Date: new DateOnly(2026, 9, 7),
            Type: TypeMatch.QUALIFICATION,
            EquipeAId: plateau.EquipeA.Id,
            EquipeBId: plateau.EquipeB.Id,
            Resultats:
            [
                new(plateau.Alice.Id, plateau.Alttp.Id, null, null, null, 100),
                new(plateau.Bob.Id, plateau.Alttp.Id, null, null, null, 9_000),
                new(plateau.Chloe.Id, plateau.Alttp.Id, null, null, null, 100),
                new(plateau.David.Id, plateau.Metroid.Id, null, null, null, 100),
            ]),
            ApiDeTest.Json);

        reponse.EnsureSuccessStatusCode();
        var corrige = await reponse.Content.ReadFromJsonAsync<MatchDetailDto>(ApiDeTest.Json);

        Assert.Equal(new DateOnly(2026, 9, 7), corrige!.Date);
        Assert.Equal(TypeMatch.QUALIFICATION, corrige.Type);
        Assert.Equal(plateau.EquipeB.Id, corrige.Equipes.Single(e => e.EstGagnante).EquipeId);

        // Toujours quatre lignes en base : les anciennes ont bien ete remplacees.
        await using var db = api.CreerContexte();
        Assert.Equal(4, db.MatchJeux.Count(mj => mj.MatchId == match.Id));
    }

    [Fact]
    public async Task La_suppression_d_un_match_emporte_ses_resultats()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var plateau = await PreparerPlateauAsync(api, client);
        var match = await EnregistrerMatchSimpleAsync(client, plateau);

        var reponse = await client.DeleteAsync($"/api/matchs/{match.Id}");
        Assert.Equal(HttpStatusCode.NoContent, reponse.StatusCode);

        await using var db = api.CreerContexte();
        Assert.Empty(db.MatchJeux.Where(mj => mj.MatchId == match.Id));
        Assert.Empty(db.Matchs.Where(m => m.Id == match.Id));
    }

    [Fact]
    public async Task Un_match_inconnu_renvoie_404()
    {
        using var api = new ApiDeTest();
        var client = api.CreerClientAnonyme();

        var reponse = await client.GetAsync("/api/matchs/9999");

        Assert.Equal(HttpStatusCode.NotFound, reponse.StatusCode);
    }

    private static async Task<MatchDetailDto> EnregistrerMatchSimpleAsync(HttpClient client, Plateau plateau)
    {
        var reponse = await client.PostAsJsonAsync("/api/matchs", new MatchUpsertRequest(
            Date: new DateOnly(2026, 9, 5),
            Type: TypeMatch.TOURNOI,
            EquipeAId: plateau.EquipeA.Id,
            EquipeBId: plateau.EquipeB.Id,
            Resultats:
            [
                new(plateau.Alice.Id, plateau.Alttp.Id, null, 200, 200, 100),
                new(plateau.Bob.Id, plateau.Metroid.Id, null, 150, 150, 100),
                new(plateau.Chloe.Id, plateau.Alttp.Id, null, 200, 200, 500),
                new(plateau.David.Id, plateau.Metroid.Id, null, 150, 150, 500),
            ]),
            ApiDeTest.Json);

        reponse.EnsureSuccessStatusCode();
        return (await reponse.Content.ReadFromJsonAsync<MatchDetailDto>(ApiDeTest.Json))!;
    }

    private static async Task<JoueurDto> CreerJoueurAsync(HttpClient client, string nom)
    {
        var reponse = await client.PostAsJsonAsync("/api/joueurs", new JoueurUpsertRequest(nom), ApiDeTest.Json);
        reponse.EnsureSuccessStatusCode();
        return (await reponse.Content.ReadFromJsonAsync<JoueurDto>(ApiDeTest.Json))!;
    }

    /// <summary>
    /// Deux equipes de quatre joueurs et deux jeux, via les endpoints publics. Les matchs de
    /// test n'alignent que les deux premiers joueurs de chaque roster (format qualification).
    /// </summary>
    private static async Task<Plateau> PreparerPlateauAsync(ApiDeTest api, HttpClient client)
    {
        var alice = await CreerJoueurAsync(client, "Alice");
        var bob = await CreerJoueurAsync(client, "Bob");
        var anna = await CreerJoueurAsync(client, "Anna");
        var arthur = await CreerJoueurAsync(client, "Arthur");

        var chloe = await CreerJoueurAsync(client, "Chloe");
        var david = await CreerJoueurAsync(client, "David");
        var claire = await CreerJoueurAsync(client, "Claire");
        var damien = await CreerJoueurAsync(client, "Damien");

        var equipeA = await CreerEquipeAsync(client, "Les Nous_", alice, bob, anna, arthur);
        var equipeB = await CreerEquipeAsync(client, "No M's Land", chloe, david, claire, damien);

        var jeux = await client.GetFromJsonAsync<List<JeuDto>>("/api/jeux", ApiDeTest.Json);
        var alttp = jeux!.Single(j => j.Nom == "A Link to the Past");
        var metroid = jeux.Single(j => j.Nom == "Super Metroid");

        return new Plateau(
            alice, bob, anna, arthur,
            chloe, david, claire, damien,
            equipeA, equipeB, alttp, metroid);
    }

    private static async Task<EquipeDto> CreerEquipeAsync(
        HttpClient client,
        string nom,
        params JoueurDto[] membres)
    {
        var reponse = await client.PostAsJsonAsync(
            "/api/equipes",
            new EquipeUpsertRequest(nom, [.. membres.Select(j => j.Id)]),
            ApiDeTest.Json);

        reponse.EnsureSuccessStatusCode();
        return (await reponse.Content.ReadFromJsonAsync<EquipeDto>(ApiDeTest.Json))!;
    }

    private sealed record Plateau(
        JoueurDto Alice,
        JoueurDto Bob,
        JoueurDto Anna,
        JoueurDto Arthur,
        JoueurDto Chloe,
        JoueurDto David,
        JoueurDto Claire,
        JoueurDto Damien,
        EquipeDto EquipeA,
        EquipeDto EquipeB,
        JeuDto Alttp,
        JeuDto Metroid);
}
