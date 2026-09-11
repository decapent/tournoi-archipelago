using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Services;

namespace TournoiArchipelago.Api.Tests;

public class AnalyseurLogArchipelagoTests
{
    /// <summary>
    /// Journal minimal mais representatif : deux joueurs, une connexion chacun, quelques
    /// checks, un objectif suivi de sa liberation, et du bruit (indice, bavardage, tracker).
    /// </summary>
    private const string Journal = """
        [2026-09-03 22:46:55,884]: Loading embedded data package for game Final Fantasy IV Free Enterprise
        [2026-09-03 22:46:55,926]: Hosting game at archipelago.gg:42009
        [2026-09-03 22:47:47,790]: Notice (all): ALPHA (Team #1) tracking Final Fantasy IV Free Enterprise has joined. Client(0.5.1), ['PopTracker'].
        [2026-09-03 22:47:57,388]: Notice (all): ALPHA (Team #1) playing Final Fantasy IV Free Enterprise has joined. Client(0.6.7), ['AP'].
        [2026-09-03 22:48:00,000]: Notice (all): BETA (Team #1) playing The Legend of Zelda has joined. Client(0.6.7), ['AP'].
        [2026-09-03 23:00:00,000]: (Team #1) ALPHA sent Sword to BETA (Baron Castle -- 1F)
        [2026-09-03 23:10:00,000]: (Team #1) ALPHA sent Small Key to ALPHA (Watery Pass -- B3F)
        [2026-09-03 23:20:00,000]: (Team #1) BETA sent Bow to ALPHA (Starting Sword Cave)
        [2026-09-03 23:30:00,000]: Notice (all): ALPHA: bien joue
        [2026-09-03 23:31:00,000]: Notice (Team #1): [Hint]: ALPHA's Magma Key is at Antlion Cave in ALPHA's World. (priority)
        [2026-09-04 00:00:00,000]: Notice (all): ALPHA (Team #1) has completed their goal.
        [2026-09-04 00:00:00,010]: Notice (all): ALPHA (Team #1) has collected their items from other worlds.
        [2026-09-04 00:00:00,020]: Notice (all): ALPHA (Team #1) has released all remaining items from their world.
        [2026-09-04 00:00:00,021]: (Team #1) ALPHA sent Potion to ALPHA (Lieu jamais visite 1)
        [2026-09-04 00:00:00,022]: (Team #1) ALPHA sent Ether to BETA (Lieu jamais visite 2)
        [2026-09-04 00:10:00,000]: Notice (all): BETA (Team #1) has left the game. Client(0.6.7), ['AP'].
        [2026-09-04 04:03:36,002]: Shutting down due to inactivity.
        """;

    private static JoueurLogDto Joueur(RapportLogDto rapport, string alias) =>
        rapport.Joueurs.Single(j => j.Alias == alias);

    [Fact]
    public void Le_check_est_credite_a_l_expediteur_pas_au_destinataire()
    {
        var rapport = AnalyseurLogArchipelago.Analyser(Journal);

        // ALPHA a trouve deux checks avant son objectif, BETA un seul, bien que les objets
        // soient partis chez l'autre : l'expediteur est celui qui fouille son monde.
        Assert.Equal(2, Joueur(rapport, "ALPHA").ChecksTrouves);
        Assert.Equal(1, Joueur(rapport, "BETA").ChecksTrouves);
    }

    [Fact]
    public void La_liberation_qui_suit_l_objectif_n_est_pas_comptee_comme_trouvee()
    {
        var rapport = AnalyseurLogArchipelago.Analyser(Journal);
        var alpha = Joueur(rapport, "ALPHA");

        // Quatre lignes au nom d'ALPHA, dont deux emises par la liberation.
        Assert.Equal(2, alpha.ChecksTrouves);
        Assert.Equal(4, alpha.TotalChecks);
    }

    [Fact]
    public void Le_total_du_monde_vient_de_la_liberation_donc_reste_inconnu_sans_objectif()
    {
        var rapport = AnalyseurLogArchipelago.Analyser(Journal);
        var beta = Joueur(rapport, "BETA");

        Assert.True(beta.EstAbandon);
        Assert.Null(beta.Objectif);
        Assert.Null(beta.TotalChecks);

        // Ses checks restent comptes : seule la taille de son monde est hors de portee.
        Assert.Equal(1, beta.ChecksTrouves);
    }

    [Fact]
    public void Le_jeu_et_l_objectif_sont_releves()
    {
        var rapport = AnalyseurLogArchipelago.Analyser(Journal);

        var alpha = Joueur(rapport, "ALPHA");
        Assert.Equal("Final Fantasy IV Free Enterprise", alpha.Jeu);
        Assert.False(alpha.EstAbandon);
        Assert.Equal(new DateTime(2026, 9, 4, 0, 0, 0), alpha.Objectif);

        Assert.Equal("The Legend of Zelda", Joueur(rapport, "BETA").Jeu);
    }

    [Fact]
    public void Les_horodatages_couvrent_les_checks_trouves_et_eux_seuls()
    {
        var rapport = AnalyseurLogArchipelago.Analyser(Journal);
        var alpha = Joueur(rapport, "ALPHA");

        Assert.Equal(alpha.ChecksTrouves, alpha.Horodatages.Count);
        Assert.Equal(new DateTime(2026, 9, 3, 23, 0, 0), alpha.PremierCheck);
        Assert.Equal(new DateTime(2026, 9, 3, 23, 10, 0), alpha.DernierCheck);

        // Croissants, et tous anterieurs a l'objectif.
        Assert.Equal([.. alpha.Horodatages.Order()], alpha.Horodatages);
        Assert.All(alpha.Horodatages, t => Assert.True(t < alpha.Objectif));
    }

    [Fact]
    public void Un_check_au_meme_instant_que_l_objectif_est_compte_comme_libere()
    {
        // La frontiere est stricte : sur le journal de reference, la rafale de liberation
        // commence dans la meme milliseconde que l'objectif.
        var journal = """
            [2026-09-03 22:00:00,000]: Notice (all): A (Team #1) playing Jeu has joined. Client(0.6.7), ['AP'].
            [2026-09-03 23:00:00,000]: (Team #1) A sent X to A (Lieu 1)
            [2026-09-03 23:30:00,500]: (Team #1) A sent Y to A (Lieu 2)
            [2026-09-03 23:30:00,500]: Notice (all): A (Team #1) has completed their goal.
            """;

        var a = AnalyseurLogArchipelago.Analyser(journal).Joueurs.Single();

        Assert.Equal(1, a.ChecksTrouves);
        Assert.Equal(2, a.TotalChecks);
    }

    [Fact]
    public void Chaque_famille_de_ligne_est_reconnue()
    {
        var rapport = AnalyseurLogArchipelago.Analyser(Journal);
        var signaux = rapport.Signaux.ToDictionary(s => s.Signal, s => s.Occurrences);

        Assert.Equal(5, signaux[SignauxLog.Check]);
        Assert.Equal(2, signaux[SignauxLog.Connexion]);
        Assert.Equal(1, signaux[SignauxLog.Objectif]);
        Assert.Equal(1, signaux[SignauxLog.Liberation]);
        Assert.Equal(1, signaux[SignauxLog.Collecte]);
        Assert.Equal(1, signaux[SignauxLog.Depart]);
        Assert.Equal(1, signaux[SignauxLog.SuiviDemarre]);
        Assert.Equal(1, signaux[SignauxLog.Indice]);
        Assert.Equal(1, signaux[SignauxLog.Bavardage]);
        Assert.Equal(1, signaux[SignauxLog.Hebergement]);
        Assert.Equal(1, signaux[SignauxLog.ChargementJeu]);
        Assert.Equal(1, signaux[SignauxLog.Arret]);

        // Aucune ligne ne doit tomber dans le fourre-tout.
        Assert.False(signaux.ContainsKey(SignauxLog.Inconnu));
    }

    [Fact]
    public void Les_lignes_sans_horodatage_sont_ignorees_sans_faire_echouer_l_analyse()
    {
        var journal = """
            ceci n'est pas une ligne de journal
            [2026-09-03 23:00:00,000]: (Team #1) A sent X to A (Lieu 1)

            [pas une date]: bruit
            """;

        var rapport = AnalyseurLogArchipelago.Analyser(journal);

        Assert.Equal(3, rapport.LignesLues);
        Assert.Equal(2, rapport.LignesIgnorees);
        Assert.Equal(1, rapport.Joueurs.Single().ChecksTrouves);
    }

    [Fact]
    public void Un_journal_vide_ne_rapporte_aucun_joueur()
    {
        var rapport = AnalyseurLogArchipelago.Analyser(string.Empty);

        Assert.Empty(rapport.Joueurs);
        Assert.Empty(rapport.Signaux);
        Assert.Null(rapport.Debut);
        Assert.Null(rapport.Fin);
    }

    [Fact]
    public void Les_bornes_temporelles_encadrent_le_journal()
    {
        var rapport = AnalyseurLogArchipelago.Analyser(Journal);

        Assert.Equal(new DateTime(2026, 9, 3, 22, 46, 55, 884), rapport.Debut);
        Assert.Equal(new DateTime(2026, 9, 4, 4, 3, 36, 2), rapport.Fin);
    }

    [Fact]
    public void Un_joueur_qui_se_reconnecte_garde_son_premier_jeu()
    {
        var journal = """
            [2026-09-03 22:00:00,000]: Notice (all): A (Team #1) playing Super Metroid has joined. Client(0.6.7), ['AP'].
            [2026-09-03 22:30:00,000]: Notice (all): A (Team #1) has left the game. Client(0.6.7), ['AP'].
            [2026-09-03 22:40:00,000]: Notice (all): A (Team #1) playing Super Metroid has joined. Client(0.6.7), ['AP'].
            """;

        Assert.Equal("Super Metroid", AnalyseurLogArchipelago.Analyser(journal).Joueurs.Single().Jeu);
    }
}
