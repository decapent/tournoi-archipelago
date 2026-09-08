namespace TournoiArchipelago.Api.Domain;

/// <summary>
/// Engagement d'une equipe dans un match. Un match en oppose exactement deux, ce que
/// MatchService verifie a la saisie : aucune contrainte SQL simple ne peut exprimer un
/// cardinal exact.
/// </summary>
public class MatchEquipe
{
    public int MatchId { get; set; }

    public int EquipeId { get; set; }

    public Match? Match { get; set; }

    public Equipe? Equipe { get; set; }
}
