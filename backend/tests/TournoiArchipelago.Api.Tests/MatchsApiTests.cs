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
                new(plateau.Alice.Id, plateau.Alttp.Id, "seed-1", 200, 200, 3_600, false),
                new(plateau.Bob.Id, plateau.Metroid.Id, "seed-2", 150, 150, 3_000, false),
                new(plateau.Chloe.Id, plateau.Alttp.Id, "seed-3", 200, 180, 4_000, false),
                new(plateau.David.Id, plateau.Metroid.Id, "seed-4", 150, 140, 3_500, false),
            ]),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);

        var match = await reponse.Content.ReadFromJsonAsync<MatchDetailDto>(ApiDeTest.Json);
        Assert.NotNull(match);
        Assert.Equal(TypeMatch.TOURNOI, match.Type);
        Assert.Equal(2, match.Equipes.Count);

        var gagnante = match.Equipes.Single(e => e.EstGagnante);
        Assert.Equal(plateau.EquipeA.Id, gagnante.EquipeId);
        // Score = la seed la plus longue de l'equipe, pas la somme des deux.
        Assert.Equal(3_600, gagnante.TempsTotalSecs);
        Assert.Equal(2, gagnante.Lignes.Count);

        // Le match remonte aussi dans l'historique et dans le classement.
        var historique = await client.GetFromJsonAsync<List<MatchSommaireDto>>("/api/matchs", ApiDeTest.Json);
        Assert.Equal("Les Nous_", Assert.Single(historique!).EquipeGagnanteNom);

        var classement = await client.GetFromJsonAsync<List<ClassementEquipeDto>>(
            "/api/stats/classement", ApiDeTest.Json);
        Assert.Equal(1, classement!.Single(l => l.EquipeNom == "Les Nous_").Victoires);
    }

    [Fact]
    public async Task Un_abandon_est_penalise_d_une_heure()
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
                new(plateau.Alice.Id, plateau.Alttp.Id, null, 200, 200, 60, false),
                new(plateau.Bob.Id, plateau.Metroid.Id, null, 150, 20, 60, true),
                new(plateau.Chloe.Id, plateau.Alttp.Id, null, 200, 200, 20_000, false),
                new(plateau.David.Id, plateau.Metroid.Id, null, 150, 150, 20_000, false),
            ]),
            ApiDeTest.Json);

        reponse.EnsureSuccessStatusCode();
        var match = await reponse.Content.ReadFromJsonAsync<MatchDetailDto>(ApiDeTest.Json);

        // La seed la plus longue de l'equipe A est l'abandon de Bob : 60 s majorees d'une
        // heure, soit 3 660 s.
        var abandonnante = match!.Equipes.Single(e => e.EquipeId == plateau.EquipeA.Id);
        Assert.Equal(1, abandonnante.NbAbandons);
        Assert.Equal(60, abandonnante.TempsBrutSecs);
        Assert.Equal(3_600, abandonnante.PenaliteSecs);
        Assert.Equal(3_660, abandonnante.TempsTotalSecs);

        var ligneAbandon = Assert.Single(abandonnante.Lignes, ligne => ligne.EstAbandon);
        Assert.Equal(60, ligneAbandon.TempsFinalSecs);
        Assert.Equal(3_660, ligneAbandon.TempsEffectifSecs);

        // La penalite etant la sanction, l'equipe A gagne malgre l'abandon :
        // 3 660 s contre 20 000 s.
        Assert.True(abandonnante.EstGagnante);
        Assert.Equal(1, abandonnante.Position);

        var equipeB = match.Equipes.Single(e => e.EquipeId == plateau.EquipeB.Id);
        Assert.Equal(20_000, equipeB.TempsTotalSecs);
        Assert.Equal(0, equipeB.PenaliteSecs);
    }

    [Fact]
    public async Task Un_match_avec_un_seul_joueur_par_equipe_est_accepte_mais_incomplet()
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
                new(plateau.Alice.Id, plateau.Alttp.Id, null, null, null, 100, false),
                new(plateau.Chloe.Id, plateau.Metroid.Id, null, null, null, 100, false),
            ]),
            ApiDeTest.Json);

        // La saisie progressive interdit d'exiger un effectif complet a l'enregistrement :
        // le match est accepte, mais il n'est pas considere comme termine.
        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);

        var match = await reponse.Content.ReadFromJsonAsync<MatchDetailDto>(ApiDeTest.Json);
        Assert.False(match!.EstComplet);
        Assert.All(match.Equipes, equipe => Assert.False(equipe.EstGagnante));

        // Et il reste hors du classement general.
        var classement = await client.GetFromJsonAsync<List<ClassementEquipeDto>>(
            "/api/stats/classement", ApiDeTest.Json);
        Assert.All(classement!, ligne => Assert.Equal(0, ligne.MatchsJoues));
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
                new(plateau.Alice.Id, plateau.Alttp.Id, null, null, null, 100, false),
                new(plateau.Bob.Id, plateau.Metroid.Id, null, null, null, 100, false),
                new(plateau.Chloe.Id, plateau.Alttp.Id, null, null, null, 100, false),
                new(eve.Id, plateau.Metroid.Id, null, null, null, 100, false),
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
                new(plateau.Alice.Id, plateau.Alttp.Id, null, null, null, 100, false),
                new(plateau.Alice.Id, plateau.Metroid.Id, null, null, null, 100, false),
                new(plateau.Chloe.Id, plateau.Alttp.Id, null, null, null, 100, false),
                new(plateau.David.Id, plateau.Metroid.Id, null, null, null, 100, false),
            ]),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Contains("une seule fois par match", await reponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_temps_non_positif_est_refuse()
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
                // Un abandon porte l'instant ou le joueur a arrete : un temps reste requis.
                new(plateau.Alice.Id, plateau.Alttp.Id, null, null, null, 0, true),
                new(plateau.Bob.Id, plateau.Metroid.Id, null, null, null, 100, false),
                new(plateau.Chloe.Id, plateau.Alttp.Id, null, null, null, 100, false),
                new(plateau.David.Id, plateau.Metroid.Id, null, null, null, 100, false),
            ]),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Contains(
            "temps d'abandon doit etre positif",
            await reponse.Content.ReadAsStringAsync());
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
                new(plateau.Alice.Id, plateau.Alttp.Id, null, 100, 150, 100, false),
                new(plateau.Bob.Id, plateau.Metroid.Id, null, null, null, 100, false),
                new(plateau.Chloe.Id, plateau.Alttp.Id, null, null, null, 100, false),
                new(plateau.David.Id, plateau.Metroid.Id, null, null, null, 100, false),
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
            new EquipeUpsertRequest("O.J.M.I.", [plateau.Alice.Id, eve.Id, frank.Id, gina.Id], null),
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
            new EquipeUpsertRequest("4G0L", [alice.Id, bob.Id], null),
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
            new EquipeUpsertRequest("4G0L", [alice.Id, bob.Id, chloe.Id, alice.Id], null),
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
            new EquipeUpsertRequest(plateau.EquipeA.Nom, membres, null),
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
                new(plateau.Alice.Id, plateau.Alttp.Id, null, null, null, 100, false),
                new(plateau.Bob.Id, plateau.Metroid.Id, null, null, null, 100, false),
                new(plateau.Anna.Id, plateau.Alttp.Id, null, null, null, 100, false),
                new(plateau.Arthur.Id, plateau.Metroid.Id, null, null, null, 100, false),
                new(plateau.Chloe.Id, plateau.Alttp.Id, null, null, null, 200, false),
                new(plateau.David.Id, plateau.Metroid.Id, null, null, null, 200, false),
                new(plateau.Claire.Id, plateau.Alttp.Id, null, null, null, 200, false),
                new(plateau.Damien.Id, plateau.Metroid.Id, null, null, null, 200, false),
            ]),
            ApiDeTest.Json);

        reponse.EnsureSuccessStatusCode();
        var match = await reponse.Content.ReadFromJsonAsync<MatchDetailDto>(ApiDeTest.Json);

        var gagnante = match!.Equipes.Single(e => e.EstGagnante);
        Assert.Equal(plateau.EquipeA.Id, gagnante.EquipeId);
        Assert.Equal(4, gagnante.Lignes.Count);
        Assert.Equal(100, gagnante.TempsTotalSecs);
        Assert.Equal(200, match.Equipes.Single(e => !e.EstGagnante).TempsTotalSecs);
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
                new(plateau.Alice.Id, plateau.Alttp.Id, null, null, null, 100, false),
                new(plateau.Bob.Id, plateau.Metroid.Id, null, null, null, 100, false),
                new(plateau.Anna.Id, plateau.Alttp.Id, null, null, null, 100, false),
                new(plateau.Chloe.Id, plateau.Alttp.Id, null, null, null, 200, false),
                new(plateau.David.Id, plateau.Metroid.Id, null, null, null, 200, false),
                new(plateau.Claire.Id, plateau.Alttp.Id, null, null, null, 200, false),
            ]),
            ApiDeTest.Json);

        reponse.EnsureSuccessStatusCode();
        var match = await reponse.Content.ReadFromJsonAsync<MatchDetailDto>(ApiDeTest.Json);

        Assert.All(match!.Equipes, equipe => Assert.Equal(3, equipe.Lignes.Count));
        Assert.Equal(100, match.Equipes.Single(e => e.EstGagnante).TempsTotalSecs);
    }

    [Fact]
    public async Task Un_match_ou_les_equipes_n_alignent_pas_le_meme_nombre_de_joueurs_reste_incomplet()
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
                new(plateau.Alice.Id, plateau.Alttp.Id, null, null, null, 100, false),
                new(plateau.Bob.Id, plateau.Metroid.Id, null, null, null, 100, false),
                new(plateau.Anna.Id, plateau.Alttp.Id, null, null, null, 100, false),
                new(plateau.Chloe.Id, plateau.Alttp.Id, null, null, null, 200, false),
                new(plateau.David.Id, plateau.Metroid.Id, null, null, null, 200, false),
            ]),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);

        var match = await reponse.Content.ReadFromJsonAsync<MatchDetailDto>(ApiDeTest.Json);
        Assert.False(match!.EstComplet);
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
                new(plateau.Alice.Id, plateau.Alttp.Id, null, null, null, 100, false),
                new(plateau.Bob.Id, plateau.Alttp.Id, null, null, null, 9_000, false),
                new(plateau.Chloe.Id, plateau.Alttp.Id, null, null, null, 100, false),
                new(plateau.David.Id, plateau.Metroid.Id, null, null, null, 100, false),
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
    public async Task Un_match_peut_etre_cree_sans_aucun_resultat()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var plateau = await PreparerPlateauAsync(api, client);

        var reponse = await client.PostAsJsonAsync("/api/matchs", new MatchUpsertRequest(
            Date: new DateOnly(2026, 9, 20),
            Type: TypeMatch.QUALIFICATION,
            EquipeAId: plateau.EquipeA.Id,
            EquipeBId: plateau.EquipeB.Id,
            Resultats: null),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);

        var match = await reponse.Content.ReadFromJsonAsync<MatchDetailDto>(ApiDeTest.Json);
        Assert.False(match!.EstComplet);

        // Les deux equipes engagees sont connues, meme sans le moindre resultat.
        Assert.Equal(2, match.Equipes.Count);
        Assert.All(match.Equipes, equipe => Assert.Empty(equipe.Lignes));
        Assert.Equal(
            [plateau.EquipeA.Nom, plateau.EquipeB.Nom],
            match.Equipes.Select(e => e.EquipeNom).OrderBy(nom => nom));

        // Et il apparait dans l'historique, marque comme non termine.
        var historique = await client.GetFromJsonAsync<List<MatchSommaireDto>>(
            "/api/matchs", ApiDeTest.Json);
        Assert.False(Assert.Single(historique!).EstComplet);
    }

    [Fact]
    public async Task Un_resultat_peut_etre_saisi_sans_temps_puis_complete()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var plateau = await PreparerPlateauAsync(api, client);

        // Etape 1 : on connait les jeux et les seeds, pas encore les temps.
        var creation = await client.PostAsJsonAsync("/api/matchs", new MatchUpsertRequest(
            Date: new DateOnly(2026, 9, 21),
            Type: TypeMatch.QUALIFICATION,
            EquipeAId: plateau.EquipeA.Id,
            EquipeBId: plateau.EquipeB.Id,
            Resultats:
            [
                new(plateau.Alice.Id, plateau.Alttp.Id, "seed-a", 216, null, null, false),
                new(plateau.Bob.Id, plateau.Metroid.Id, "seed-b", 100, null, null, false),
                new(plateau.Chloe.Id, plateau.Alttp.Id, "seed-c", 216, null, null, false),
                new(plateau.David.Id, plateau.Metroid.Id, "seed-d", 100, null, null, false),
            ]),
            ApiDeTest.Json);

        creation.EnsureSuccessStatusCode();
        var brouillon = await creation.Content.ReadFromJsonAsync<MatchDetailDto>(ApiDeTest.Json);

        Assert.False(brouillon!.EstComplet);
        Assert.All(brouillon.Equipes, equipe =>
        {
            Assert.Equal(2, equipe.NbResultatsEnAttente);
            Assert.Null(equipe.TempsTotalSecs);
        });
        Assert.All(
            brouillon.Equipes.SelectMany(e => e.Lignes),
            ligne =>
            {
                Assert.True(ligne.EstEnAttente);
                Assert.Null(ligne.TempsFinalSecs);
                Assert.Null(ligne.TempsEffectifSecs);
            });

        // Etape 2 : les temps arrivent.
        var completion = await client.PutAsJsonAsync(
            $"/api/matchs/{brouillon.Id}",
            new MatchUpsertRequest(
                Date: new DateOnly(2026, 9, 21),
                Type: TypeMatch.QUALIFICATION,
                EquipeAId: plateau.EquipeA.Id,
                EquipeBId: plateau.EquipeB.Id,
                Resultats:
                [
                    new(plateau.Alice.Id, plateau.Alttp.Id, "seed-a", 216, 216, 3_600, false),
                    new(plateau.Bob.Id, plateau.Metroid.Id, "seed-b", 100, 100, 3_000, false),
                    new(plateau.Chloe.Id, plateau.Alttp.Id, "seed-c", 216, 200, 4_000, false),
                    new(plateau.David.Id, plateau.Metroid.Id, "seed-d", 100, 95, 3_500, false),
                ]),
            ApiDeTest.Json);

        completion.EnsureSuccessStatusCode();
        var termine = await completion.Content.ReadFromJsonAsync<MatchDetailDto>(ApiDeTest.Json);

        Assert.True(termine!.EstComplet);
        Assert.Equal(plateau.EquipeA.Id, termine.Equipes.Single(e => e.EstGagnante).EquipeId);

        // Le match rejoint alors le classement general.
        var classement = await client.GetFromJsonAsync<List<ClassementEquipeDto>>(
            "/api/stats/classement", ApiDeTest.Json);
        Assert.Equal(1, classement!.Single(l => l.EquipeNom == plateau.EquipeA.Nom).Victoires);
    }

    [Fact]
    public async Task Un_match_incomplet_est_absent_des_statistiques_par_jeu()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var plateau = await PreparerPlateauAsync(api, client);

        await client.PostAsJsonAsync("/api/matchs", new MatchUpsertRequest(
            Date: new DateOnly(2026, 9, 22),
            Type: TypeMatch.QUALIFICATION,
            EquipeAId: plateau.EquipeA.Id,
            EquipeBId: plateau.EquipeB.Id,
            Resultats:
            [
                // Le temps d'Alice est connu, mais celui de Bob manque encore.
                new(plateau.Alice.Id, plateau.Alttp.Id, null, 216, 216, 3_600, false),
                new(plateau.Bob.Id, plateau.Metroid.Id, null, 100, null, null, false),
                new(plateau.Chloe.Id, plateau.Alttp.Id, null, 216, 200, 4_000, false),
                new(plateau.David.Id, plateau.Metroid.Id, null, 100, 95, 3_500, false),
            ]),
            ApiDeTest.Json);

        var stats = await client.GetFromJsonAsync<List<StatsJeuDto>>("/api/stats/jeux", ApiDeTest.Json);

        // Aucune partie comptee, y compris pour les lignes deja renseignees.
        Assert.All(stats!, jeu => Assert.Equal(0, jeu.NbParties));
    }

    [Fact]
    public async Task Un_effectif_au_dela_du_roster_est_refuse()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var plateau = await PreparerPlateauAsync(api, client);

        // Cinq joueurs cote A : impossible, le roster n'en compte que quatre. On y arrive en
        // ajoutant un membre de l'equipe B a la place d'un des siens.
        var reponse = await client.PostAsJsonAsync("/api/matchs", new MatchUpsertRequest(
            Date: new DateOnly(2026, 9, 23),
            Type: TypeMatch.TOURNOI,
            EquipeAId: plateau.EquipeA.Id,
            EquipeBId: plateau.EquipeB.Id,
            Resultats:
            [
                new(plateau.Alice.Id, plateau.Alttp.Id, null, null, null, 100, false),
                new(plateau.Bob.Id, plateau.Metroid.Id, null, null, null, 100, false),
                new(plateau.Anna.Id, plateau.Alttp.Id, null, null, null, 100, false),
                new(plateau.Arthur.Id, plateau.Metroid.Id, null, null, null, 100, false),
                new(plateau.Alice.Id, plateau.Metroid.Id, null, null, null, 100, false),
            ]),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Contains("une seule fois par match", await reponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Le_capitaine_est_expose_et_place_en_tete_du_roster()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();
        var plateau = await PreparerPlateauAsync(api, client);

        var equipes = await client.GetFromJsonAsync<List<EquipeDto>>("/api/equipes", ApiDeTest.Json);
        var equipeA = equipes!.Single(e => e.Id == plateau.EquipeA.Id);

        Assert.Equal(plateau.Alice.Id, equipeA.Capitaine!.Id);
        Assert.True(equipeA.Membres[0].EstCapitaine);
        Assert.Single(equipeA.Membres, membre => membre.EstCapitaine);
    }

    [Fact]
    public async Task Un_capitaine_hors_du_roster_est_refuse()
    {
        using var api = new ApiDeTest();
        var client = await api.CreerClientAdminAsync();

        var membres = new List<JoueurDto>();
        foreach (var nom in new[] { "Eve", "Frank", "Gina", "Hugo" })
        {
            membres.Add(await CreerJoueurAsync(client, nom));
        }

        var intrus = await CreerJoueurAsync(client, "Intrus");

        var reponse = await client.PostAsJsonAsync(
            "/api/equipes",
            new EquipeUpsertRequest("ElsaipasGG", [.. membres.Select(m => m.Id)], intrus.Id),
            ApiDeTest.Json);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Contains("faire partie du roster", await reponse.Content.ReadAsStringAsync());
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
                new(plateau.Alice.Id, plateau.Alttp.Id, null, 200, 200, 100, false),
                new(plateau.Bob.Id, plateau.Metroid.Id, null, 150, 150, 100, false),
                new(plateau.Chloe.Id, plateau.Alttp.Id, null, 200, 200, 500, false),
                new(plateau.David.Id, plateau.Metroid.Id, null, 150, 150, 500, false),
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
            // Le premier membre est designe capitaine, comme dans le tournoi.
            new EquipeUpsertRequest(nom, [.. membres.Select(j => j.Id)], membres[0].Id),
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
