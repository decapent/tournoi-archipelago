using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Tests;

/// <summary>Raccourcis de construction d'entites pour les tests.</summary>
internal static class ConstructeurDonnees
{
    public static Joueur Joueur(int id, string nom) => new() { Id = id, Nom = nom };

    public static Jeu Jeu(int id, string nom) => new() { Id = id, Nom = nom };

    /// <summary>Equipe portant un nom et un roster, sans passer par la base.</summary>
    public static Equipe Equipe(int id, string nom, params Joueur[] membres) => new()
    {
        Id = id,
        Nom = nom,
        Membres = [.. membres.Select(joueur => new EquipeJoueur
        {
            EquipeId = id,
            JoueurId = joueur.Id,
            Joueur = joueur,
        })],
    };

    /// <summary>
    /// Resultat d'un joueur. <paramref name="tempsFinalSecs"/> est le temps brut : temps de
    /// completion, ou instant de l'abandon quand <paramref name="estAbandon"/> est vrai.
    /// </summary>
    public static MatchJeu Ligne(
        Joueur joueur,
        Jeu jeu,
        int tempsFinalSecs,
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
