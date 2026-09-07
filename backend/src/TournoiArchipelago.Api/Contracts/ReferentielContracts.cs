namespace TournoiArchipelago.Api.Contracts;

public record JoueurDto(int Id, string Nom);

public record JoueurUpsertRequest(string? Nom);

public record JeuDto(int Id, string Nom);

public record JeuUpsertRequest(string? Nom);

/// <summary>Equipe et son roster.</summary>
public record EquipeDto(int Id, string Nom, IReadOnlyList<JoueurDto> Membres);

public record EquipeUpsertRequest(string? Nom, IReadOnlyList<int>? JoueurIds);
