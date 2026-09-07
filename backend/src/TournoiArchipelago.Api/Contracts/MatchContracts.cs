using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Contracts;

/// <summary>
/// Resultat d'un joueur a saisir dans un match. <c>TempsFinalSecs</c> laisse a null
/// signifie un abandon.
/// </summary>
public record ResultatUpsertRequest(
    int JoueurId,
    int JeuId,
    string? Seed,
    int? TotalChecks,
    int? NbChecks,
    int? TempsFinalSecs);

/// <summary>
/// Un match oppose exactement deux equipes, soit quatre lignes de resultat
/// (une par joueur des deux equipes).
/// </summary>
public record MatchUpsertRequest(
    DateOnly Date,
    TypeMatch Type,
    int EquipeAId,
    int EquipeBId,
    IReadOnlyList<ResultatUpsertRequest> Resultats);

public record LigneResultatDto(
    int JoueurId,
    string JoueurNom,
    int JeuId,
    string JeuNom,
    string? Seed,
    int? TotalChecks,
    int? NbChecks,
    int? TempsFinalSecs,
    bool EstAbandon,
    double? PourcentComplete);

/// <summary>Une equipe et son resultat au sein d'un match.</summary>
public record EquipeResultatDto(
    int? EquipeId,
    string EquipeNom,
    int Position,
    bool EstGagnante,
    int? TempsTotalSecs,
    bool EstAbandon,
    int ChecksTrouves,
    int? TotalChecks,
    double? PourcentComplete,
    IReadOnlyList<LigneResultatDto> Lignes);

public record MatchSommaireDto(
    int Id,
    DateOnly Date,
    TypeMatch Type,
    string? EquipeGagnanteNom,
    IReadOnlyList<string> EquipeNoms);

public record MatchDetailDto(
    int Id,
    DateOnly Date,
    TypeMatch Type,
    IReadOnlyList<EquipeResultatDto> Equipes);
