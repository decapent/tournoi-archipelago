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

    /// <summary>
    /// Extrait fidele au journal du tournoi : les deux joueurs ont pose un <c>!alias</c>, donc
    /// le serveur les designe « Pseudo (SLOT) », et la completion du premier declenche une
    /// collecte qui puise dans le monde du second.
    /// </summary>
    private const string JournalAvecAlias = """
        [2026-09-14 00:10:00,000]: Notice (all): W1ALTTP (Team #1) playing A Link to the Past has joined. Client(0.6.7), ['AP'].
        [2026-09-14 00:10:01,000]: Notice (all): W1SM (Team #1) playing Super Metroid has joined. Client(0.6.7), ['AP'].
        [2026-09-14 00:11:00,000]: Notice (all): W1ALTTP: !alias Moi_Eva
        [2026-09-14 00:20:00,000]: (Team #1) W1ALTTP sent Bow to W1ALTTP (Eastern Palace - Boss)
        [2026-09-14 00:21:00,000]: (Team #1) W1SM sent Bombs to W1ALTTP (Missile (Draygon))
        [2026-09-14 00:58:56,114]: Notice (all): Moi_Eva (W1ALTTP) (Team #1) has completed their goal.
        [2026-09-14 00:58:56,114]: Notice (all): W1ALTTP (Team #1) has collected their items from other worlds.
        [2026-09-14 00:58:56,114]: (Team #1) W1ALTTP sent Bombos to W1ALTTP (Ice Palace - Freezor Chest)
        [2026-09-14 00:58:56,121]: (Team #1) W1SM sent Big Key to W1ALTTP (Ice Beam)
        [2026-09-14 00:58:56,121]: (Team #1) W1SM sent Small Key to W1ALTTP (Missile (pink Maridia))
        [2026-09-14 00:58:56,122]: Notice (all): W1ALTTP (Team #1) has released all remaining items from their world.
        [2026-09-14 00:58:56,122]: (Team #1) W1ALTTP sent Energy Tank to W1SM (Skull Woods - Big Chest)
        [2026-09-14 00:58:56,122]: (Team #1) W1ALTTP sent Energy Tank to W1SM (Turtle Rock - Eye Bridge)
        """;

    [Fact]
    public void L_objectif_est_reconnu_meme_quand_le_joueur_a_pose_un_pseudonyme()
    {
        var rapport = AnalyseurLogArchipelago.Analyser(JournalAvecAlias);

        // Le serveur ecrit « Moi_Eva (W1ALTTP) », mais les lignes d'envoi ne connaissent que
        // l'emplacement : c'est donc lui qui doit etre retenu, sans quoi l'objectif est
        // rattache a un joueur fantome et le vrai passe pour un abandon.
        var alttp = Joueur(rapport, "W1ALTTP");
        Assert.False(alttp.EstAbandon);
        Assert.NotNull(alttp.Objectif);
        Assert.DoesNotContain(rapport.Joueurs, j => j.Alias == "Moi_Eva");
    }

    [Fact]
    public void La_collecte_d_un_joueur_ne_credite_pas_de_checks_a_un_autre()
    {
        var rapport = AnalyseurLogArchipelago.Analyser(JournalAvecAlias);

        // W1SM n'a trouve qu'une localisation. Les deux autres lignes a son nom viennent de
        // la collecte de W1ALTTP, qui a vide son monde des objets lui appartenant : il n'a
        // rien fouille. Aucune regle fondee sur l'objectif de W1SM ne pourrait le voir, il
        // n'a pas encore termine.
        var metroid = Joueur(rapport, "W1SM");
        Assert.Equal(1, metroid.ChecksTrouves);
        Assert.True(metroid.EstAbandon);
        Assert.Null(metroid.TotalChecks);
    }

    [Fact]
    public void Le_total_du_monde_compte_toutes_les_lignes_emises_par_le_joueur()
    {
        var rapport = AnalyseurLogArchipelago.Analyser(JournalAvecAlias);
        var alttp = Joueur(rapport, "W1ALTTP");

        // Une localisation de son monde produit exactement une ligne a son nom, qu'il l'ait
        // trouvee (1), qu'elle ait ete collectee par son proprietaire (1) ou qu'il l'ait
        // liberee (2).
        Assert.Equal(1, alttp.ChecksTrouves);
        Assert.Equal(4, alttp.TotalChecks);
    }

    [Fact]
    public void La_rafale_ne_figure_pas_dans_la_progression()
    {
        var rapport = AnalyseurLogArchipelago.Analyser(JournalAvecAlias);

        // La courbe ne doit pas finir par un saut vertical jusqu'a la taille du monde.
        Assert.Equal([new DateTime(2026, 9, 14, 0, 20, 0)], Joueur(rapport, "W1ALTTP").Horodatages);
        Assert.Equal([new DateTime(2026, 9, 14, 0, 21, 0)], Joueur(rapport, "W1SM").Horodatages);
    }

    [Fact]
    public void Un_spectateur_est_reconnu_et_ne_devient_pas_un_joueur()
    {
        var journal = """
            [2026-09-14 00:10:00,000]: Notice (all): W1SM (Team #1) viewing Super Metroid has joined. Client(0.6.7), ['Tracker'].
            [2026-09-14 00:10:01,000]: Notice (all): Moi_Eva (W1ALTTP) (Team #1) viewing A Link to the Past has joined. Client(0.6.7), ['Tracker'].
            [2026-09-14 00:20:00,000]: Notice (all): Moi_Eva (W1ALTTP) (Team #1) has stopped viewing the game.
            """;

        var rapport = AnalyseurLogArchipelago.Analyser(journal);
        var signaux = rapport.Signaux.ToDictionary(s => s.Signal, s => s.Occurrences);

        Assert.Equal(2, signaux[SignauxLog.SuiviDemarre]);
        Assert.Equal(1, signaux[SignauxLog.SuiviArrete]);
        Assert.False(signaux.ContainsKey(SignauxLog.Inconnu));
        Assert.Empty(rapport.Joueurs);
    }

    [Fact]
    public void Le_depart_est_estime_au_premier_check_pas_a_l_ouverture_du_serveur()
    {
        var rapport = AnalyseurLogArchipelago.Analyser(Journal);

        // Le serveur ouvre a 22:46:55 et les joueurs se connectent aussitot, mais l'hote ne
        // lance qu'a 23:00 : treize minutes d'attente qui ne comptent pas dans la course.
        Assert.Equal(new DateTime(2026, 9, 3, 22, 46, 55, 884), rapport.Debut);
        Assert.Equal(new DateTime(2026, 9, 3, 23, 0, 0), rapport.DepartEstime);
    }

    [Fact]
    public void Un_check_isole_avant_le_depart_ne_fixe_pas_le_depart()
    {
        // Un joueur tatonne a 22:50, puis plus rien pendant une heure : la course commence
        // vraiment a 23:50.
        var journal = """
            [2026-09-03 22:40:00,000]: Notice (all): ALPHA (Team #1) playing Doom has joined. Client(0.6.7), ['AP'].
            [2026-09-03 22:50:00,000]: (Team #1) ALPHA sent Sword to ALPHA (Lieu 1)
            [2026-09-03 23:50:00,000]: (Team #1) ALPHA sent Bow to ALPHA (Lieu 2)
            [2026-09-03 23:52:00,000]: (Team #1) ALPHA sent Key to ALPHA (Lieu 3)
            """;

        var rapport = AnalyseurLogArchipelago.Analyser(journal);

        Assert.Equal(new DateTime(2026, 9, 3, 23, 50, 0), rapport.DepartEstime);
    }

    [Fact]
    public void Sans_aucun_check_le_depart_reste_inconnu()
    {
        var journal = """
            [2026-09-03 22:40:00,000]: Hosting game at archipelago.gg:42009
            [2026-09-03 22:41:00,000]: Notice (all): ALPHA (Team #1) playing Doom has joined. Client(0.6.7), ['AP'].
            """;

        var rapport = AnalyseurLogArchipelago.Analyser(journal);

        Assert.Null(rapport.DepartEstime);
        Assert.NotNull(rapport.Debut);
    }

    [Fact]
    public void Un_seul_check_dans_le_journal_fixe_le_depart()
    {
        var journal = """
            [2026-09-03 22:40:00,000]: Notice (all): ALPHA (Team #1) playing Doom has joined. Client(0.6.7), ['AP'].
            [2026-09-03 23:10:00,000]: (Team #1) ALPHA sent Sword to ALPHA (Lieu 1)
            """;

        var rapport = AnalyseurLogArchipelago.Analyser(journal);

        Assert.Equal(new DateTime(2026, 9, 3, 23, 10, 0), rapport.DepartEstime);
    }

    [Fact]
    public void Le_depart_estime_tient_compte_de_tous_les_joueurs()
    {
        // Le plus rapide a trouver sa premiere localisation donne le repere, meme si un autre
        // joueur met une demi-heure a demarrer.
        var journal = """
            [2026-09-03 22:40:00,000]: Notice (all): ALPHA (Team #1) playing Doom has joined. Client(0.6.7), ['AP'].
            [2026-09-03 22:40:01,000]: Notice (all): BETA (Team #1) playing Jigsaw has joined. Client(0.6.7), ['AP'].
            [2026-09-03 23:05:00,000]: (Team #1) BETA sent Sword to ALPHA (Lieu 1)
            [2026-09-03 23:06:00,000]: (Team #1) ALPHA sent Bow to BETA (Lieu 2)
            """;

        var rapport = AnalyseurLogArchipelago.Analyser(journal);

        Assert.Equal(new DateTime(2026, 9, 3, 23, 5, 0), rapport.DepartEstime);
    }

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
    public void Le_check_qui_declenche_l_objectif_compte_bien_qu_il_soit_au_meme_instant()
    {
        // Une rafale se reconnait a son annonce, pas a l'horodatage : le check gagnant, emis
        // dans la meme milliseconde que l'objectif, reste un vrai check.
        var journal = """
            [2026-09-03 22:00:00,000]: Notice (all): A (Team #1) playing Jeu has joined. Client(0.6.7), ['AP'].
            [2026-09-03 23:00:00,000]: (Team #1) A sent X to A (Lieu 1)
            [2026-09-03 23:30:00,500]: (Team #1) A sent Y to A (Lieu 2)
            [2026-09-03 23:30:00,500]: Notice (all): A (Team #1) has completed their goal.
            """;

        var a = AnalyseurLogArchipelago.Analyser(journal).Joueurs.Single();

        Assert.Equal(2, a.ChecksTrouves);
        Assert.Equal(2, a.TotalChecks);
    }

    [Fact]
    public void Chaque_famille_de_ligne_est_reconnue()
    {
        var rapport = AnalyseurLogArchipelago.Analyser(Journal);
        var signaux = rapport.Signaux.ToDictionary(s => s.Signal, s => s.Occurrences);

        // Trois vrais checks : les deux envois de la rafale sont comptes en liberation, avec
        // son annonce, plutot que confondus avec des checks.
        Assert.Equal(3, signaux[SignauxLog.Check]);
        Assert.Equal(3, signaux[SignauxLog.Liberation]);
        Assert.Equal(2, signaux[SignauxLog.Connexion]);
        Assert.Equal(1, signaux[SignauxLog.Objectif]);
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
