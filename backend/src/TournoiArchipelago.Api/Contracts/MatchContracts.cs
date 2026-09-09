using TournoiArchipelago.Api.Domain;

namespace TournoiArchipelago.Api.Contracts;

/// <summary>
/// Resultat d'un joueur a saisir dans un match. <c>TempsFinalSecs</c> peut rester a null tant
/// que le resultat n'est pas connu : c'est le temps de completion, ou l'instant de l'abandon
/// quand <c>EstAbandon</c> est vrai. La penalite d'abandon est appliquee au classement, pas a
/// la saisie.
/// </summary>
public record ResultatUpsertRequest(
    int JoueurId,
    int JeuId,
    string? Seed,
    int? TotalChecks,
    int? NbChecks,
    int? TempsFinalSecs,
    bool EstAbandon);

/// <summary>
/// Un match oppose exactement deux equipes. Les resultats peuvent etre omis a la creation et
/// completes plus tard : seules la date, le type et les deux equipes sont requis.
/// </summary>
public record MatchUpsertRequest(
    DateOnly Date,
    TypeMatch Type,
    int EquipeAId,
    int EquipeBId,
    IReadOnlyList<ResultatUpsertRequest>? Resultats);

public record LigneResultatDto(
    int JoueurId,
    string JoueurNom,
    int JeuId,
    string JeuNom,
    string? Seed,
    int? TotalChecks,
    int? NbChecks,
    /// <summary>Temps brut saisi : completion, ou instant de l'abandon. Null si a saisir.</summary>
    int? TempsFinalSecs,
    /// <summary>Temps retenu au classement, penalite d'abandon incluse. Null si a saisir.</summary>
    int? TempsEffectifSecs,
    bool EstAbandon,
    /// <summary>Vrai quand le temps de ce joueur reste a saisir.</summary>
    bool EstEnAttente,
    double? PourcentComplete);

/// <summary>Une equipe et son resultat au sein d'un match.</summary>
public record EquipeResultatDto(
    int? EquipeId,
    string EquipeNom,
    int Position,
    bool EstGagnante,
    /// <summary>
    /// Score de l'equipe : le plus long des temps effectifs de ses participants, pas leur
    /// somme. Null tant qu'un resultat de l'equipe reste a saisir.
    /// </summary>
    int? TempsTotalSecs,
    /// <summary>Temps brut de la seed determinante, avant penalite.</summary>
    int? TempsBrutSecs,
    /// <summary>
    /// Penalite portee par la seed determinante : une heure si c'est un abandon, sinon zero.
    /// </summary>
    int PenaliteSecs,
    int NbAbandons,
    /// <summary>Nombre de participants dont le temps reste a saisir.</summary>
    int NbResultatsEnAttente,
    int ChecksTrouves,
    int? TotalChecks,
    double? PourcentComplete,
    IReadOnlyList<LigneResultatDto> Lignes);

public record MatchSommaireDto(
    int Id,
    DateOnly Date,
    TypeMatch Type,
    /// <summary>Faux tant qu'un resultat reste a saisir : le match est alors hors statistiques.</summary>
    bool EstComplet,
    int NbResultatsEnAttente,
    string? EquipeGagnanteNom,
    IReadOnlyList<string> EquipeNoms);

public record MatchDetailDto(
    int Id,
    DateOnly Date,
    TypeMatch Type,
    bool EstComplet,
    IReadOnlyList<EquipeResultatDto> Equipes);
