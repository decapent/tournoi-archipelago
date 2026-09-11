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
    /// <summary>Premiere ligne horodatee du journal : le serveur demarre, pas la course.</summary>
    DateTime? Debut,
    /// <summary>
    /// Depart approxime au premier check de la course. Les joueurs se connectent longtemps
    /// avant que l'hote ne lance -- quarante minutes sur le journal de reference -- et le vrai
    /// depart n'apparait nulle part dans le journal. L'admin corrige au besoin.
    /// </summary>
    DateTime? DepartEstime,
    DateTime? Fin,
    int LignesLues,
    int LignesIgnorees);

/// <summary>Journal soumis pour analyse, sans rien enregistrer.</summary>
public record AnalyseLogRequest(string? Contenu);

/// <summary>Rapprochement d'un pseudonyme du journal avec un joueur du roster.</summary>
public record CorrespondanceAliasDto(string Alias, int JoueurId);

/// <summary>
/// Import d'un journal pour une equipe d'un match. Le depart est commun a tous les joueurs :
/// c'est de lui que se compte le temps de chacun.
/// </summary>
public record ImportLogRequest(
    string? Contenu,
    DateTime DepartCourse,
    IReadOnlyList<CorrespondanceAliasDto>? Correspondances);

/// <summary>Progression d'un joueur, en secondes depuis le depart de la course.</summary>
public record ProgressionJoueurDto(
    int JoueurId,
    string JoueurNom,
    int? EquipeId,
    string EquipeNom,
    string JeuNom,
    IReadOnlyList<int> Secondes);

/// <summary>Courbes de progression d'un match, pretes a tracer.</summary>
public record ProgressionMatchDto(
    int MatchId,
    IReadOnlyList<ProgressionJoueurDto> Joueurs);
