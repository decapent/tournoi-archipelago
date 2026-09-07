namespace TournoiArchipelago.Api.Domain;

/// <summary>
/// Appartenance d'un joueur a une equipe. Un joueur ne figure que dans une seule equipe :
/// c'est ce qui permet de regrouper sans ambiguite les lignes d'un match par equipe, la table
/// Match n'ayant aucun lien vers Equipe.
/// </summary>
public class EquipeJoueur
{
    public int EquipeId { get; set; }

    public int JoueurId { get; set; }

    public Equipe? Equipe { get; set; }

    public Joueur? Joueur { get; set; }
}
