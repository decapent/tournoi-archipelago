using TournoiArchipelago.Api.Contracts;
using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Services;

/// <summary>
/// Regroupe les lignes d'un match par equipe et les classe.
///
/// C'est le seul endroit ou vit la regle de classement :
///   1. le score d'une equipe est la somme des <c>temps_final_secs</c> de ses participants ;
///   2. le plus petit total gagne ;
///   3. une equipe dont au moins un participant a abandonne (temps nul) passe apres toutes
///      les equipes complettes, et les abandons sont departages par nombre de checks trouves.
///
/// Le nombre de participants par equipe n'est pas fixe : il se deduit des lignes saisies
/// (deux en qualification, trois en demi-finale, quatre en finale).
///
/// En cas d'egalite parfaite, les equipes partagent la meme position et comptent chacune
/// une victoire.
/// </summary>
public static class ScoringService
{
    public const string NomEquipeInconnue = "Sans equipe";

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
            .OrderBy(g => g.Cle.RangAbandon)
            .ThenBy(g => g.Cle.CleePrincipale)
            .ThenBy(g => g.Cle.CleeSecondaire)
            .ThenBy(g => g.Nom, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var resultats = new List<EquipeResultatDto>(ordonnes.Count);
        var position = 0;
        CleTri? clePrecedente = null;

        for (var i = 0; i < ordonnes.Count; i++)
        {
            var groupe = ordonnes[i];

            // Classement sportif : deux equipes a egalite partagent la meme position.
            if (clePrecedente is null || !clePrecedente.Value.Equals(groupe.Cle))
            {
                position = i + 1;
                clePrecedente = groupe.Cle;
            }

            resultats.Add(new EquipeResultatDto(
                EquipeId: groupe.EquipeId,
                EquipeNom: groupe.Nom,
                Position: position,
                EstGagnante: position == 1,
                TempsTotalSecs: groupe.TempsTotalSecs,
                EstAbandon: groupe.EstAbandon,
                ChecksTrouves: groupe.ChecksTrouves,
                TotalChecks: groupe.TotalChecks,
                PourcentComplete: groupe.PourcentComplete,
                Lignes: groupe.Lignes));
        }

        return resultats;
    }

    private static GroupeEquipe Agreger(Equipe? equipe, List<MatchJeu> lignes)
    {
        var estAbandon = lignes.Any(l => l.TempsFinalSecs is null);

        var tempsRenseignes = lignes.Where(l => l.TempsFinalSecs is not null).ToList();
        int? tempsTotal = tempsRenseignes.Count > 0
            ? tempsRenseignes.Sum(l => l.TempsFinalSecs!.Value)
            : null;

        var checksTrouves = lignes.Sum(l => l.NbChecks ?? 0);

        var totauxRenseignes = lignes.Where(l => l.TotalChecks is not null).ToList();
        int? totalChecks = totauxRenseignes.Count > 0
            ? totauxRenseignes.Sum(l => l.TotalChecks!.Value)
            : null;

        double? pourcentComplete = totalChecks is > 0
            ? (double)checksTrouves / totalChecks.Value
            : null;

        // Une equipe complette est classee sur son temps ; un abandon est departage par ses
        // checks, d'ou la cle negative qui remet le tri en ordre croissant.
        var cle = new CleTri(
            RangAbandon: estAbandon ? 1 : 0,
            CleePrincipale: estAbandon ? -checksTrouves : tempsTotal ?? int.MaxValue,
            CleeSecondaire: tempsTotal ?? int.MaxValue);

        return new GroupeEquipe(
            EquipeId: equipe?.Id,
            Nom: equipe?.Nom ?? NomEquipeInconnue,
            EstAbandon: estAbandon,
            TempsTotalSecs: tempsTotal,
            ChecksTrouves: checksTrouves,
            TotalChecks: totalChecks,
            PourcentComplete: pourcentComplete,
            Cle: cle,
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
        EstAbandon: ligne.EstAbandon,
        PourcentComplete: ligne.PourcentComplete);

    private readonly record struct CleTri(int RangAbandon, int CleePrincipale, int CleeSecondaire);

    private sealed record GroupeEquipe(
        int? EquipeId,
        string Nom,
        bool EstAbandon,
        int? TempsTotalSecs,
        int ChecksTrouves,
        int? TotalChecks,
        double? PourcentComplete,
        CleTri Cle,
        IReadOnlyList<LigneResultatDto> Lignes);
}
