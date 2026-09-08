using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Services;

/// <summary>
/// Classe les equipes engagees dans un match.
///
/// C'est le seul endroit ou vit la regle de classement :
///   1. un joueur qui abandonne compte son temps d'abandon majore de
///      <see cref="PenaliteAbandonSecs"/> ;
///   2. le score d'une equipe est la somme des temps ainsi obtenus pour ses participants ;
///   3. le plus petit total gagne.
///
/// La penalite etant la sanction, une equipe avec un abandon est classee comme les autres.
///
/// Un match peut etre rempli au fur et a mesure : tant qu'il n'est pas complet, il est classe
/// a titre indicatif mais ne designe aucun vainqueur, et les statistiques l'ignorent.
/// </summary>
public static class ScoringService
{
    /// <summary>Majoration appliquee au temps d'un joueur qui abandonne : une heure.</summary>
    public const int PenaliteAbandonSecs = 3600;

    /// <summary>Nombre d'equipes qui s'affrontent dans un match.</summary>
    public const int NbEquipesParMatch = 2;

    /// <summary>Nombre minimal de participants par equipe (format qualification).</summary>
    public const int NbJoueursMinParEquipe = 2;

    /// <summary>Nombre maximal de participants par equipe (roster complet, format finale).</summary>
    public const int NbJoueursMaxParEquipe = 4;

    public const string NomEquipeInconnue = "Sans equipe";

    /// <summary>Temps retenu au classement, ou <c>null</c> si le resultat reste a saisir.</summary>
    public static int? TempsEffectifSecs(MatchJeu ligne) =>
        ligne.TempsFinalSecs is null
            ? null
            : ligne.TempsFinalSecs.Value + (ligne.EstAbandon ? PenaliteAbandonSecs : 0);

    /// <summary>
    /// Classe les equipes engagees dans un match. Les navigations <c>Equipe</c>, <c>Joueur</c>
    /// et <c>Jeu</c> sont utilisees pour les libelles lorsqu'elles sont chargees.
    /// </summary>
    public static ClassementMatch ClasserMatch(Match match)
    {
        var lignesParEquipe = RepartirLignes(match);

        var groupes = lignesParEquipe
            .Select(paire => Agreger(paire.Equipe, paire.Lignes))
            .ToList();

        var estComplet = EstComplet(groupes);

        var ordonnes = groupes
            .OrderBy(g => g.TempsTotalSecs ?? int.MaxValue)
            .ThenBy(g => g.Nom, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var resultats = new List<EquipeResultatDto>(ordonnes.Count);
        var position = 0;
        int? totalPrecedent = null;
        var premiereLigne = true;

        foreach (var groupe in ordonnes)
        {
            // Classement sportif : deux equipes a egalite partagent la meme position.
            if (premiereLigne || totalPrecedent != groupe.TempsTotalSecs)
            {
                position = resultats.Count + 1;
                totalPrecedent = groupe.TempsTotalSecs;
                premiereLigne = false;
            }

            resultats.Add(new EquipeResultatDto(
                EquipeId: groupe.EquipeId,
                EquipeNom: groupe.Nom,
                Position: position,
                // Tant que le match n'est pas complet, aucun vainqueur n'est designe.
                EstGagnante: estComplet && position == 1,
                TempsTotalSecs: groupe.TempsTotalSecs,
                TempsBrutSecs: groupe.TempsBrutSecs,
                PenaliteSecs: groupe.PenaliteSecs,
                NbAbandons: groupe.NbAbandons,
                NbResultatsEnAttente: groupe.NbResultatsEnAttente,
                ChecksTrouves: groupe.ChecksTrouves,
                TotalChecks: groupe.TotalChecks,
                PourcentComplete: groupe.PourcentComplete,
                Lignes: groupe.Lignes));
        }

        return new ClassementMatch(estComplet, resultats);
    }

    /// <summary>
    /// Repartit les resultats entre les equipes engagees. Une equipe engagee sans aucun
    /// resultat apparait quand meme, avec une liste vide : c'est le cas d'un match tout juste
    /// cree. Un resultat dont le joueur n'appartient a aucune des deux equipes est regroupe a
    /// part plutot que perdu.
    /// </summary>
    private static List<(Equipe? Equipe, List<MatchJeu> Lignes)> RepartirLignes(Match match)
    {
        var equipeParJoueur = new Dictionary<int, Equipe>();
        var parEquipe = new Dictionary<int, List<MatchJeu>>();
        var engagees = new List<Equipe>();

        foreach (var engagement in match.Equipes.OrderBy(e => e.EquipeId))
        {
            if (engagement.Equipe is null)
            {
                continue;
            }

            engagees.Add(engagement.Equipe);
            parEquipe[engagement.Equipe.Id] = [];

            foreach (var membre in engagement.Equipe.Membres)
            {
                equipeParJoueur[membre.JoueurId] = engagement.Equipe;
            }
        }

        var orphelines = new List<MatchJeu>();

        foreach (var ligne in match.MatchJeux)
        {
            if (equipeParJoueur.TryGetValue(ligne.JoueurId, out var equipe))
            {
                parEquipe[equipe.Id].Add(ligne);
            }
            else
            {
                orphelines.Add(ligne);
            }
        }

        var repartition = engagees
            .Select(equipe => ((Equipe?)equipe, parEquipe[equipe.Id]))
            .ToList();

        if (orphelines.Count > 0)
        {
            repartition.Add((null, orphelines));
        }

        return repartition;
    }

    /// <summary>
    /// Un match est complet quand les deux equipes alignent le meme nombre de participants,
    /// dans les bornes du format, et que chacun a un temps.
    /// </summary>
    private static bool EstComplet(IReadOnlyList<GroupeEquipe> groupes)
    {
        if (groupes.Count != NbEquipesParMatch || groupes.Any(g => g.EquipeId is null))
        {
            return false;
        }

        if (groupes.Any(g => g.NbResultatsEnAttente > 0))
        {
            return false;
        }

        var effectifs = groupes.Select(g => g.Lignes.Count).Distinct().ToList();

        return effectifs.Count == 1
            && effectifs[0] >= NbJoueursMinParEquipe
            && effectifs[0] <= NbJoueursMaxParEquipe;
    }

    private static GroupeEquipe Agreger(Equipe? equipe, List<MatchJeu> lignes)
    {
        var nbEnAttente = lignes.Count(ligne => ligne.EstEnAttente);
        var nbAbandons = lignes.Count(ligne => ligne.EstAbandon);
        var penalite = nbAbandons * PenaliteAbandonSecs;

        // Le score n'a de sens qu'une fois tous les temps de l'equipe saisis.
        var tousSaisis = lignes.Count > 0 && nbEnAttente == 0;
        int? tempsBrut = tousSaisis ? lignes.Sum(ligne => ligne.TempsFinalSecs!.Value) : null;

        var checksTrouves = lignes.Sum(ligne => ligne.NbChecks ?? 0);

        var totauxRenseignes = lignes.Where(ligne => ligne.TotalChecks is not null).ToList();
        int? totalChecks = totauxRenseignes.Count > 0
            ? totauxRenseignes.Sum(ligne => ligne.TotalChecks!.Value)
            : null;

        return new GroupeEquipe(
            EquipeId: equipe?.Id,
            Nom: equipe?.Nom ?? NomEquipeInconnue,
            TempsBrutSecs: tempsBrut,
            PenaliteSecs: penalite,
            TempsTotalSecs: tempsBrut is null ? null : tempsBrut + penalite,
            NbAbandons: nbAbandons,
            NbResultatsEnAttente: nbEnAttente,
            ChecksTrouves: checksTrouves,
            TotalChecks: totalChecks,
            PourcentComplete: totalChecks is > 0 ? (double)checksTrouves / totalChecks.Value : null,
            Lignes: [.. lignes.OrderBy(l => l.Joueur?.Nom ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                              .ThenBy(l => l.JoueurId)
                              .Select(VersLigneDto)]);
    }

    private static LigneResultatDto VersLigneDto(MatchJeu ligne) => new(
        JoueurId: ligne.JoueurId,
        JoueurNom: ligne.Joueur?.Nom ?? $"#{ligne.JoueurId}",
        JeuId: ligne.JeuId,
        JeuNom: ligne.Jeu?.Nom ?? $"#{ligne.JeuId}",
        Seed: ligne.Seed,
        TotalChecks: ligne.TotalChecks,
        NbChecks: ligne.NbChecks,
        TempsFinalSecs: ligne.TempsFinalSecs,
        TempsEffectifSecs: TempsEffectifSecs(ligne),
        EstAbandon: ligne.EstAbandon,
        EstEnAttente: ligne.EstEnAttente,
        PourcentComplete: ligne.PourcentComplete);

    private sealed record GroupeEquipe(
        int? EquipeId,
        string Nom,
        int? TempsBrutSecs,
        int PenaliteSecs,
        int? TempsTotalSecs,
        int NbAbandons,
        int NbResultatsEnAttente,
        int ChecksTrouves,
        int? TotalChecks,
        double? PourcentComplete,
        IReadOnlyList<LigneResultatDto> Lignes);
}
