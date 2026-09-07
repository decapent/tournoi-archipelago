namespace TournoiArchipelago.Api.Contracts;

public record JoueurDto(int Id, string Nom);

public record JoueurUpsertRequest(string? Nom);

public record JeuDto(int Id, string Nom);

public record JeuUpsertRequest(string? Nom);

/// <summary>Duo de joueurs. <c>Nom</c> est un libelle derive des deux membres.</summary>
public record EquipeDto(int Id, string Nom, JoueurDto Joueur1, JoueurDto Joueur2);

public record EquipeUpsertRequest(int Joueur1Id, int Joueur2Id);
