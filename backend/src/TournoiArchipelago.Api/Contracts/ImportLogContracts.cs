namespace TournoiArchipelago.Api.Contracts;

/// <summary>Ce qu'un journal Archipelago apprend sur un joueur.</summary>
public record JoueurLogDto(
    /// <summary>Pseudonyme dans la partie, a rapprocher d'un joueur du roster.</summary>
    string Alias,
    /// <summary>Jeu annonce a la connexion, tel qu'ecrit par Archipelago.</summary>
    string? Jeu,
    /// <summary>Checks trouves avant l'objectif. Exclut la liberation qui suit la completion.</summary>
    int ChecksTrouves,
    /// <summary>
    /// Nombre total de localisations du monde. Connu seulement si le joueur a termine : c'est
    /// la liberation qui enumere celles qu'il n'avait pas trouvees.
    /// </summary>
    int? TotalChecks,
    DateTime? PremierCheck,
    DateTime? DernierCheck,
    /// <summary>Instant de la completion, ou <c>null</c> si le joueur n'a jamais termine.</summary>
    DateTime? Objectif,
    /// <summary>Vrai en l'absence d'objectif : le joueur n'a pas fini sa seed.</summary>
    bool EstAbandon,
    /// <summary>Horodatage de chaque check trouve, pour tracer la progression.</summary>
    IReadOnlyList<DateTime> Horodatages);

/// <summary>Une famille de lignes reconnue dans le journal, avec un exemple.</summary>
public record SignalLogDto(string Signal, int Occurrences, string Exemple);

/// <summary>Resultat de l'analyse d'un journal, avant tout rapprochement avec le tournoi.</summary>
public record RapportLogDto(
    IReadOnlyList<JoueurLogDto> Joueurs,
    IReadOnlyList<SignalLogDto> Signaux,
    DateTime? Debut,
    DateTime? Fin,
    int LignesLues,
    int LignesIgnorees);
