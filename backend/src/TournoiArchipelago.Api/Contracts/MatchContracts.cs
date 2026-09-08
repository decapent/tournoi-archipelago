using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Contracts;

/// <summary>
/// Resultat d'un joueur a saisir dans un match. <c>TempsFinalSecs</c> est toujours requis :
/// c'est le temps de completion, ou l'instant de l'abandon quand <c>EstAbandon</c> est vrai.
/// La penalite d'abandon est appliquee au classement, pas a la saisie.
/// </summary>
public record ResultatUpsertRequest(
    int JoueurId,
    int JeuId,
    string? Seed,
    int? TotalChecks,
    int? NbChecks,
    int TempsFinalSecs,
    bool EstAbandon);

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
    /// <summary>Temps brut saisi : completion, ou instant de l'abandon.</summary>
    int TempsFinalSecs,
    /// <summary>Temps retenu au classement, penalite d'abandon incluse.</summary>
    int TempsEffectifSecs,
    bool EstAbandon,
    double? PourcentComplete);

/// <summary>Une equipe et son resultat au sein d'un match.</summary>
public record EquipeResultatDto(
    int? EquipeId,
    string EquipeNom,
    int Position,
    bool EstGagnante,
    /// <summary>Score de l'equipe : somme des temps effectifs de ses participants.</summary>
    int TempsTotalSecs,
    /// <summary>Somme des temps saisis, avant penalite.</summary>
    int TempsBrutSecs,
    /// <summary>Total des penalites d'abandon incluses dans le score.</summary>
    int PenaliteSecs,
    int NbAbandons,
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
