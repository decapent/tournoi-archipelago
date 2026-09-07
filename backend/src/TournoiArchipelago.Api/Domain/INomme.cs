namespace TournoiArchipelago.Api.Domain;

/// <summary>
/// Entite de reference identifiee par un nom unique. Permet de partager la verification
/// d'unicite entre Joueur et Jeu.
/// </summary>
public interface INomme
{
    int Id { get; }

    string Nom { get; }
}
