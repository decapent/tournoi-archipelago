using TournoiArchipelago.Api.Contracts;

namespace TournoiArchipelago.Api.Domain;

/// <summary>
/// Instant auquel un joueur a demande un indice, dans un match donne, et ce que la demande a
/// donne.
///
/// Comme <see cref="CheckHorodate"/>, le temps est stocke en secondes depuis le depart de la
/// course : la frise des indices se trace ainsi sur le meme axe que la progression des checks.
/// </summary>
public class IndiceHorodate
{
    public int Id { get; set; }

    public int MatchId { get; set; }

    public int JoueurId { get; set; }

    /// <summary>Secondes ecoulees depuis le depart de la course.</summary>
    public int Secondes { get; set; }

    public ResultatIndice Resultat { get; set; }

    /// <summary>
    /// Solde de points d'indice au moment de la demande, quand le serveur l'annonce — ce qu'il
    /// ne fait qu'en refusant ou en rappelant son tarif.
    /// </summary>
    public int? PointsRestants { get; set; }

    public Match? Match { get; set; }

    public Joueur? Joueur { get; set; }
}
