namespace TournoiArchipelago.Api.Domain;

public class Joueur : INomme
{
    public int Id { get; set; }

    public string Nom { get; set; } = string.Empty;

    /// <summary>Au plus une, l'unicite etant garantie par la base.</summary>
    public ICollection<EquipeJoueur> Appartenances { get; set; } = new List<EquipeJoueur>();

    public ICollection<MatchJeu> MatchJeux { get; set; } = new List<MatchJeu>();
}
