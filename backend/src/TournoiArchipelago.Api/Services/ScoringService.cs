using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Services;

/// <summary>
/// Regroupe les lignes d'un match par equipe et les classe.
///
/// C'est le seul endroit ou vit la regle de classement :
///   1. un joueur qui abandonne compte son temps d'abandon majore de
///      <see cref="PenaliteAbandonSecs"/> ;
///   2. le score d'une equipe est la somme des temps ainsi obtenus pour ses participants ;
///   3. le plus petit total gagne.
///
/// La penalite etant la sanction, une equipe avec un abandon est classee comme les autres.
///
/// Le nombre de participants par equipe n'est pas fixe : il se deduit des lignes saisies
/// (deux en qualification, trois en demi-finale, quatre en finale).
///
/// En cas d'egalite parfaite, les equipes partagent la meme position et comptent chacune
/// une victoire.
/// </summary>
public static class ScoringService
{
    /// <summary>Majoration appliquee au temps d'un joueur qui abandonne : une heure.</summary>
    public const int PenaliteAbandonSecs = 3600;

    public const string NomEquipeInconnue = "Sans equipe";

    /// <summary>Temps retenu au classement : le temps brut, majore en cas d'abandon.</summary>
    public static int TempsEffectifSecs(MatchJeu ligne) =>
        ligne.TempsFinalSecs + (ligne.EstAbandon ? PenaliteAbandonSecs : 0);

    /// <summary>
    /// Classe les equipes presentes dans un match. Les navigations <c>Joueur</c> et <c>Jeu</c>
    /// des lignes sont utilisees pour les libelles lorsqu'elles sont chargees.
    /// </summary>
    public static IReadOnlyList<EquipeResultatDto> ClasserMatch(
        IEnumerable<MatchJeu> lignes,
        IEnumerable<Equipe> equipes)
    {
        var equipeParJoueur = new Dictionary<int, Equipe>();
        foreach (var equipe in equipes)
        {
            foreach (var joueurId in equipe.MembreIds)
            {
                equipeParJoueur[joueurId] = equipe;
            }
        }

        var groupes = lignes
            .GroupBy(ligne => equipeParJoueur.GetValueOrDefault(ligne.JoueurId))
            .Select(groupe => Agreger(groupe.Key, [.. groupe]))
            .ToList();

        var ordonnes = groupes
            .OrderBy(g => g.TempsTotalSecs)
            .ThenBy(g => g.Nom, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var resultats = new List<EquipeResultatDto>(ordonnes.Count);
        var position = 0;
        int? totalPrecedent = null;

        for (var i = 0; i < ordonnes.Count; i++)
        {
            var groupe = ordonnes[i];

            // Classement sportif : deux equipes a egalite partagent la meme position.
            if (totalPrecedent is null || totalPrecedent != groupe.TempsTotalSecs)
            {
                position = i + 1;
                totalPrecedent = groupe.TempsTotalSecs;
            }

            resultats.Add(new EquipeResultatDto(
                EquipeId: groupe.EquipeId,
                EquipeNom: groupe.Nom,
                Position: position,
                EstGagnante: position == 1,
                TempsTotalSecs: groupe.TempsTotalSecs,
                TempsBrutSecs: groupe.TempsBrutSecs,
                PenaliteSecs: groupe.PenaliteSecs,
                NbAbandons: groupe.NbAbandons,
                ChecksTrouves: groupe.ChecksTrouves,
                TotalChecks: groupe.TotalChecks,
                PourcentComplete: groupe.PourcentComplete,
                Lignes: groupe.Lignes));
        }

        return resultats;
    }

    private static GroupeEquipe Agreger(Equipe? equipe, List<MatchJeu> lignes)
    {
        var tempsBrut = lignes.Sum(ligne => ligne.TempsFinalSecs);
        var nbAbandons = lignes.Count(ligne => ligne.EstAbandon);
        var penalite = nbAbandons * PenaliteAbandonSecs;

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
            TempsTotalSecs: tempsBrut + penalite,
            NbAbandons: nbAbandons,
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
        PourcentComplete: ligne.PourcentComplete);

    private sealed record GroupeEquipe(
        int? EquipeId,
        string Nom,
        int TempsBrutSecs,
        int PenaliteSecs,
        int TempsTotalSecs,
        int NbAbandons,
        int ChecksTrouves,
        int? TotalChecks,
        double? PourcentComplete,
        IReadOnlyList<LigneResultatDto> Lignes);
}
