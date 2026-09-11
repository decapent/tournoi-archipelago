namespace TournoiArchipelago.Api.Domain;

/// <summary>
/// Rencontre entre deux equipes. L'engagement des equipes est porte par la table de liaison
/// <see cref="MatchEquipe"/> : un match peut etre cree avant qu'aucun resultat ne soit saisi,
/// il faut donc pouvoir savoir qui s'affronte sans passer par les lignes <see cref="MatchJeu"/>.
/// </summary>
public class Match
{
    public int Id { get; set; }

    /// <summary>Jour du match (la colonne SQL est de type <c>date</c>, sans heure).</summary>
    public DateOnly Date { get; set; }

    public TypeMatch Type { get; set; }

    public ICollection<MatchEquipe> Equipes { get; set; } = new List<MatchEquipe>();

    public ICollection<MatchJeu> MatchJeux { get; set; } = new List<MatchJeu>();

    /// <summary>Progression importee d'un journal Archipelago, s'il y en a eu un.</summary>
    public ICollection<CheckHorodate> Checks { get; set; } = new List<CheckHorodate>();

    /// <summary>Identifiants des equipes engagees.</summary>
    public IEnumerable<int> EquipeIds => Equipes.Select(engagement => engagement.EquipeId);
}
