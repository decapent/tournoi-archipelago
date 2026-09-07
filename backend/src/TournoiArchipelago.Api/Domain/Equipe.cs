namespace TournoiArchipelago.Api.Domain;

/// <summary>
/// Duo de joueurs. L'equipe n'est pas rattachee a un match : le regroupement des lignes
/// <see cref="MatchJeu"/> en equipes se deduit de l'appartenance des joueurs, ce qui suppose
/// qu'un joueur n'est membre que d'une seule equipe (verifie par EquipeService).
/// </summary>
public class Equipe
{
    public int Id { get; set; }

    public int Joueur1Id { get; set; }

    public int Joueur2Id { get; set; }

    public Joueur? Joueur1 { get; set; }

    public Joueur? Joueur2 { get; set; }

    /// <summary>Identifiants des deux membres, dans l'ordre de stockage.</summary>
    public IEnumerable<int> MembreIds
    {
        get
        {
            yield return Joueur1Id;
            yield return Joueur2Id;
        }
    }
}
