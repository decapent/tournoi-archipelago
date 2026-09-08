namespace TournoiArchipelago.Api.Domain;

/// <summary>
/// Resultat d'un joueur sur un jeu donne, dans un match donne.
/// </summary>
public class MatchJeu
{
    public int MatchId { get; set; }

    public int JeuId { get; set; }

    public int JoueurId { get; set; }

    /// <summary>Identifiant du seed Archipelago genere pour ce joueur.</summary>
    public string? Seed { get; set; }

    /// <summary>Nombre total de checks existant dans le jeu.</summary>
    public int? TotalChecks { get; set; }

    /// <summary>Nombre de checks trouves par le joueur.</summary>
    public int? NbChecks { get; set; }

    /// <summary>
    /// Temps brut en secondes : temps de completion, ou instant de l'abandon quand
    /// <see cref="EstAbandon"/> est vrai. <c>null</c> signifie que le resultat n'a pas encore
    /// ete saisi, un match pouvant etre rempli au fur et a mesure.
    /// </summary>
    public int? TempsFinalSecs { get; set; }

    /// <summary>Vrai quand le joueur n'a pas termine sa seed.</summary>
    public bool EstAbandon { get; set; }

    /// <summary>Vrai quand le temps de ce joueur reste a saisir.</summary>
    public bool EstEnAttente => TempsFinalSecs is null;

    public Match? Match { get; set; }

    public Jeu? Jeu { get; set; }

    public Joueur? Joueur { get; set; }

    /// <summary>Part des checks trouves, entre 0 et 1. <c>null</c> si le total est inconnu.</summary>
    public double? PourcentComplete =>
        TotalChecks is > 0 && NbChecks is not null
            ? (double)NbChecks.Value / TotalChecks.Value
            : null;
}
