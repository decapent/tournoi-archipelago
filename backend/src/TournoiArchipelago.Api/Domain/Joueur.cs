namespace TournoiArchipelago.Api.Domain;

public class Joueur : INomme
{
    public int Id { get; set; }

    public string Nom { get; set; } = string.Empty;

    public ICollection<MatchJeu> MatchJeux { get; set; } = new List<MatchJeu>();
}
