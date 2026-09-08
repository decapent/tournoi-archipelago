using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Tests;

/// <summary>Raccourcis de construction d'entites pour les tests.</summary>
internal static class ConstructeurDonnees
{
    public static Joueur Joueur(int id, string nom) => new() { Id = id, Nom = nom };

    public static Jeu Jeu(int id, string nom) => new() { Id = id, Nom = nom };

    /// <summary>
    /// Equipe portant un nom et un roster, sans passer par la base. Le premier membre est
    /// designe capitaine, comme dans le tournoi.
    /// </summary>
    public static Equipe Equipe(int id, string nom, params Joueur[] membres) => new()
    {
        Id = id,
        Nom = nom,
        Membres = [.. membres.Select((joueur, rang) => new EquipeJoueur
        {
            EquipeId = id,
            JoueurId = joueur.Id,
            Joueur = joueur,
            EstCapitaine = rang == 0,
        })],
    };

    /// <summary>
    /// Match opposant deux equipes, avec ses resultats. Les resultats peuvent etre absents ou
    /// partiels : un match se remplit au fur et a mesure.
    /// </summary>
    public static Api.Domain.Match Match(
        Equipe equipeA,
        Equipe equipeB,
        IEnumerable<MatchJeu>? resultats = null,
        int id = 1,
        TypeMatch type = TypeMatch.TOURNOI,
        DateOnly? date = null)
    {
        var match = new Api.Domain.Match
        {
            Id = id,
            Date = date ?? new DateOnly(2026, 9, 1),
            Type = type,
            MatchJeux = [.. resultats ?? []],
        };

        match.Equipes =
        [
            new MatchEquipe { MatchId = id, EquipeId = equipeA.Id, Equipe = equipeA, Match = match },
            new MatchEquipe { MatchId = id, EquipeId = equipeB.Id, Equipe = equipeB, Match = match },
        ];

        foreach (var ligne in match.MatchJeux)
        {
            ligne.MatchId = id;
        }

        return match;
    }

    /// <summary>
    /// Resultat d'un joueur. <paramref name="tempsFinalSecs"/> est le temps brut : temps de
    /// completion, ou instant de l'abandon quand <paramref name="estAbandon"/> est vrai.
    /// <c>null</c> signifie que le resultat reste a saisir.
    /// </summary>
    public static MatchJeu Ligne(
        Joueur joueur,
        Jeu jeu,
        int? tempsFinalSecs,
        bool estAbandon = false,
        int? nbChecks = null,
        int? totalChecks = null,
        int matchId = 1,
        string? seed = null) => new()
    {
        MatchId = matchId,
        JeuId = jeu.Id,
        JoueurId = joueur.Id,
        Joueur = joueur,
        Jeu = jeu,
        Seed = seed,
        NbChecks = nbChecks,
        TotalChecks = totalChecks,
        TempsFinalSecs = tempsFinalSecs,
        EstAbandon = estAbandon,
    };
}
