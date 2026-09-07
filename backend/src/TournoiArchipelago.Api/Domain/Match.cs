namespace TournoiArchipelago.Api.Domain;

public class Match
{
    public int Id { get; set; }

    /// <summary>Jour du match (la colonne SQL est de type <c>date</c>, sans heure).</summary>
    public DateOnly Date { get; set; }

    public TypeMatch Type { get; set; }

    public ICollection<MatchJeu> MatchJeux { get; set; } = new List<MatchJeu>();
}
