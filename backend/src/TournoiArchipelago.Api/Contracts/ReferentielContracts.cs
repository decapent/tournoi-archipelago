namespace TournoiArchipelago.Api.Contracts;

public record JoueurDto(int Id, string Nom);

public record JoueurUpsertRequest(string? Nom);

public record JeuDto(int Id, string Nom);

public record JeuUpsertRequest(string? Nom);

/// <summary>Membre d'un roster.</summary>
public record MembreDto(int Id, string Nom, bool EstCapitaine);

/// <summary>Equipe et son roster.</summary>
public record EquipeDto(int Id, string Nom, IReadOnlyList<MembreDto> Membres)
{
    /// <summary>Capitaine du roster, s'il a ete designe.</summary>
    public MembreDto? Capitaine => Membres.FirstOrDefault(membre => membre.EstCapitaine);
}

public record EquipeUpsertRequest(
    string? Nom,
    IReadOnlyList<int>? JoueurIds,
    /// <summary>Capitaine, qui doit figurer dans le roster. Optionnel.</summary>
    int? CapitaineId);
