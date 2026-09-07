using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Tests;

/// <summary>Raccourcis de construction d'entites pour les tests.</summary>
internal static class ConstructeurDonnees
{
    public static Joueur Joueur(int id, string nom) => new() { Id = id, Nom = nom };

    public static Jeu Jeu(int id, string nom) => new() { Id = id, Nom = nom };

    public static Equipe Equipe(int id, Joueur joueur1, Joueur joueur2) => new()
    {
        Id = id,
        Joueur1Id = joueur1.Id,
        Joueur2Id = joueur2.Id,
        Joueur1 = joueur1,
        Joueur2 = joueur2,
    };

    public static MatchJeu Ligne(
        Joueur joueur,
        Jeu jeu,
        int? tempsFinalSecs,
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
    };
}
