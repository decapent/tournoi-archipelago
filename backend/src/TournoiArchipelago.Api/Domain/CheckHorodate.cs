namespace TournoiArchipelago.Api.Domain;

/// <summary>
/// Instant auquel un joueur a trouve un check, dans un match donne.
///
/// Le temps est stocke en secondes depuis le depart de la course, comme le reste du modele,
/// plutot qu'en date absolue : la progression se trace ainsi directement, et sans se soucier
/// du fuseau du serveur Archipelago qui a produit le journal.
/// </summary>
public class CheckHorodate
{
    public int Id { get; set; }

    public int MatchId { get; set; }

    public int JoueurId { get; set; }

    /// <summary>Secondes ecoulees depuis le depart de la course.</summary>
    public int Secondes { get; set; }

    /// <summary>
    /// Vrai quand la ligne vient d'une rafale de collecte ou de liberation plutot que d'une
    /// fouille. Elle ne compte pas dans les checks trouves, mais la courbe la montre : c'est
    /// elle qui la mene jusqu'a la taille du monde.
    /// </summary>
    public bool EstRafale { get; set; }

    public Match? Match { get; set; }

    public Joueur? Joueur { get; set; }
}
