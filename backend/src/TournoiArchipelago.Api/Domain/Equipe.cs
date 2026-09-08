namespace TournoiArchipelago.Api.Domain;

/// <summary>
/// Equipe du tournoi. Le roster compte quatre joueurs ; le nombre de participants a un match
/// donne varie selon l'etape (deux en qualification, trois en demi-finale, quatre en finale)
/// et se deduit des lignes <see cref="MatchJeu"/> saisies.
/// </summary>
public class Equipe : INomme
{
    public int Id { get; set; }

    public string Nom { get; set; } = string.Empty;

    public ICollection<EquipeJoueur> Membres { get; set; } = new List<EquipeJoueur>();

    /// <summary>Identifiants des joueurs du roster.</summary>
    public IEnumerable<int> MembreIds => Membres.Select(membre => membre.JoueurId);

    /// <summary>Capitaine du roster, s'il a ete designe.</summary>
    public EquipeJoueur? Capitaine => Membres.FirstOrDefault(membre => membre.EstCapitaine);
}
