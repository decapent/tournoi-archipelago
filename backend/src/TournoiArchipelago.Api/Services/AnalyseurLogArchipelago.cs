using System.Globalization;
using System.Text.RegularExpressions;
using TournoiArchipelago.Api.Contracts;

namespace TournoiArchipelago.Api.Services;

/// <summary>
/// Lit un journal de serveur Archipelago et en tire, par joueur, le nombre de checks trouves,
/// le total de son monde et l'instant de sa completion.
///
/// Le point delicat est que toutes les lignes ne se valent pas. Trois evenements produisent la
/// meme forme « X sent ... to Y (lieu) », et deux d'entre eux ne sont pas des checks :
///
///   - un CHECK : le joueur a fouille son monde et trouve une localisation ;
///   - la LIBERATION : a la completion, le serveur vide d'un coup toutes les localisations du
///     monde du joueur qui appartenaient aux autres. L'emetteur est le joueur qui termine ;
///   - la COLLECTE : a la completion aussi, le serveur ramasse tous les objets du joueur ou
///     qu'ils soient. L'emetteur est alors le monde qui detenait la localisation, c'est-a-dire
///     souvent UN AUTRE JOUEUR, qui n'a rien fouille.
///
/// La collecte est la plus traitre : elle crediterait des checks a un joueur qui n'a pas encore
/// fini, et aucune regle fondee sur son propre objectif ne peut la voir. Les deux rafales sont
/// heureusement annoncees par une ligne dediee ; on les suit donc explicitement.
///
/// Le total du monde, lui, est le nombre de lignes dont le joueur est l'emetteur, tous
/// evenements confondus : chaque localisation de son monde en produit exactement une, qu'il
/// l'ait trouvee, qu'elle ait ete collectee par son proprietaire ou qu'il l'ait liberee. Ce
/// total n'a de sens que pour un joueur ayant termine, faute de quoi son monde n'a pas ete
/// vide.
/// </summary>
public static partial class AnalyseurLogArchipelago
{
    /// <summary>Nombre de lignes au-dela duquel on refuse le fichier.</summary>
    public const int MaxLignes = 200_000;

    /// <summary>Genre de rafale en cours, le temps de la traverser.</summary>
    private enum Rafale
    {
        Aucune,
        Collecte,
        Liberation,
    }

    public static RapportLogDto Analyser(string contenu)
    {
        var evenements = LireEvenements(contenu, out var lignesLues, out var lignesIgnorees);

        // Par alias : les checks reellement trouves, et toutes les lignes qu'il a emises.
        var trouves = new Dictionary<string, List<DateTime>>(StringComparer.Ordinal);
        var emises = new Dictionary<string, int>(StringComparer.Ordinal);
        var jeuParAlias = new Dictionary<string, string>(StringComparer.Ordinal);
        var objectifParAlias = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        var signaux = new Dictionary<string, (int Nb, string Exemple)>(StringComparer.Ordinal);

        void Compter(string signal, string exemple)
        {
            signaux[signal] = signaux.TryGetValue(signal, out var d)
                ? (d.Nb + 1, d.Exemple)
                : (1, exemple);
        }

        var rafale = Rafale.Aucune;
        var beneficiaire = string.Empty;

        foreach (var (horodatage, message) in evenements)
        {
            var check = Check().Match(message);
            if (check.Success)
            {
                var emetteur = check.Groups[1].Value;
                var destinataire = check.Groups[2].Value;

                emises[emetteur] = emises.GetValueOrDefault(emetteur) + 1;

                // Une rafale se reconnait a son sens : la collecte converge vers celui qui
                // termine, la liberation part de lui.
                var dansRafale = rafale switch
                {
                    Rafale.Collecte => destinataire == beneficiaire,
                    Rafale.Liberation => emetteur == beneficiaire,
                    _ => false,
                };

                if (dansRafale)
                {
                    Compter(rafale == Rafale.Collecte ? SignauxLog.Collecte : SignauxLog.Liberation, message);
                }
                else
                {
                    if (!trouves.TryGetValue(emetteur, out var siens))
                    {
                        trouves[emetteur] = siens = [];
                    }

                    siens.Add(horodatage);
                    Compter(SignauxLog.Check, message);
                }

                continue;
            }

            // Toute ligne qui n'est pas un envoi clot la rafale : les deux se suivent d'un
            // seul bloc, a la milliseconde.
            rafale = Rafale.Aucune;

            var collecte = Collecte().Match(message);
            if (collecte.Success)
            {
                rafale = Rafale.Collecte;
                beneficiaire = Slot(collecte);
                Compter(SignauxLog.Collecte, message);
                continue;
            }

            var liberation = Liberation().Match(message);
            if (liberation.Success)
            {
                rafale = Rafale.Liberation;
                beneficiaire = Slot(liberation);
                Compter(SignauxLog.Liberation, message);
                continue;
            }

            var connexion = Connexion().Match(message);
            if (connexion.Success)
            {
                // Un joueur peut se reconnecter : le premier jeu annonce fait foi.
                jeuParAlias.TryAdd(Slot(connexion), connexion.Groups[3].Value.Trim());
                Compter(SignauxLog.Connexion, message);
                continue;
            }

            var objectif = Objectif().Match(message);
            if (objectif.Success)
            {
                objectifParAlias.TryAdd(Slot(objectif), horodatage);
                Compter(SignauxLog.Objectif, message);
                continue;
            }

            Compter(Classer(message), message);
        }

        var alias = trouves.Keys
            .Concat(emises.Keys)
            .Concat(jeuParAlias.Keys)
            .Concat(objectifParAlias.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase);

        var joueurs = alias.Select(a =>
        {
            var siens = trouves.GetValueOrDefault(a) ?? [];
            siens.Sort();

            var objectif = objectifParAlias.TryGetValue(a, out var o) ? o : (DateTime?)null;

            return new JoueurLogDto(
                Alias: a,
                Jeu: jeuParAlias.GetValueOrDefault(a),
                ChecksTrouves: siens.Count,
                // Sans completion, le monde n'a pas ete vide : sa taille reste inconnue.
                TotalChecks: objectif is null ? null : emises.GetValueOrDefault(a),
                PremierCheck: siens.Count > 0 ? siens[0] : null,
                DernierCheck: siens.Count > 0 ? siens[^1] : null,
                Objectif: objectif,
                EstAbandon: objectif is null,
                Horodatages: siens);
        }).ToList();

        return new RapportLogDto(
            Joueurs: joueurs,
            Signaux: [.. signaux
                .Select(s => new SignalLogDto(s.Key, s.Value.Nb, Tronquer(s.Value.Exemple)))
                .OrderByDescending(s => s.Occurrences)
                .ThenBy(s => s.Signal, StringComparer.Ordinal)],
            Debut: evenements.Count > 0 ? evenements[0].Horodatage : null,
            DepartEstime: EstimerDepart(joueurs),
            Fin: evenements.Count > 0 ? evenements[^1].Horodatage : null,
            LignesLues: lignesLues,
            LignesIgnorees: lignesIgnorees);
    }

    /// <summary>
    /// Silence au-dela duquel un check n'est plus considere comme faisant partie de la course.
    /// Une fois l'hote parti, les checks tombent sans repit : sur les deux journaux de
    /// reference, aucun silence de plus de trois minutes dans les trente premiers checks.
    /// </summary>
    private static readonly TimeSpan SilenceHorsCourse = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Approxime le depart au premier check de la course.
    ///
    /// Le vrai depart n'est nulle part dans le journal : seul l'hote le connait. Mais rien ne
    /// se passe avant qu'il ne lance, alors que les joueurs sont connectes depuis longtemps —
    /// quarante minutes d'attente sur le journal de reference. Le premier check marque donc le
    /// depart a quelques minutes pres, celles qu'il faut au plus rapide pour trouver sa
    /// premiere localisation.
    ///
    /// Un joueur qui tatonne avant le depart produirait un check isole, suivi d'un long
    /// silence : celui-la est ecarte.
    /// </summary>
    private static DateTime? EstimerDepart(IEnumerable<JoueurLogDto> joueurs)
    {
        var checks = joueurs.SelectMany(j => j.Horodatages).Order().ToList();
        if (checks.Count == 0)
        {
            return null;
        }

        for (var i = 0; i < checks.Count - 1; i++)
        {
            if (checks[i + 1] - checks[i] <= SilenceHorsCourse)
            {
                return checks[i];
            }
        }

        // Que des checks isoles : le journal est trop maigre pour departager, on prend le premier.
        return checks[0];
    }

    /// <summary>Familles reconnues mais non exploitees pour le classement.</summary>
    private static string Classer(string message) => message switch
    {
        _ when message.Contains("has completed all of their games", StringComparison.Ordinal) => SignauxLog.EquipeTerminee,
        _ when message.Contains("has left the game", StringComparison.Ordinal) => SignauxLog.Depart,
        _ when message.Contains("has stopped tracking", StringComparison.Ordinal) => SignauxLog.SuiviArrete,
        _ when message.Contains("has stopped viewing", StringComparison.Ordinal) => SignauxLog.SuiviArrete,
        // Un tracker (PopTracker) ou un spectateur se branche : distinct de « playing », qui
        // est le joueur lui-meme.
        _ when Suivi().IsMatch(message) => SignauxLog.SuiviDemarre,
        _ when message.Contains("[Hint]:", StringComparison.Ordinal) => SignauxLog.Indice,
        _ when message.StartsWith("Hosting game at", StringComparison.Ordinal) => SignauxLog.Hebergement,
        _ when message.StartsWith("Loading embedded data package", StringComparison.Ordinal) => SignauxLog.ChargementJeu,
        _ when message.StartsWith("Loaded save file", StringComparison.Ordinal) => SignauxLog.Sauvegarde,
        _ when message.StartsWith("Shutting down", StringComparison.Ordinal) => SignauxLog.Arret,
        _ when message.StartsWith("A client connection was refused", StringComparison.Ordinal) => SignauxLog.ConnexionRefusee,
        _ when message.StartsWith("Notice (Player ", StringComparison.Ordinal) => SignauxLog.MessageServeur,
        _ when Bavardage().IsMatch(message) => SignauxLog.Bavardage,
        _ => SignauxLog.Inconnu,
    };

    /// <summary>
    /// Nom d'emplacement de l'acteur d'une ligne. Le serveur l'ecrit « W1ALTTP » tant que le
    /// joueur n'a pas pose de pseudonyme, puis « Moi_Eva (W1ALTTP) » apres un <c>!alias</c>.
    /// Seul l'emplacement se retrouve dans les lignes d'envoi : c'est lui qu'on retient.
    /// </summary>
    private static string Slot(Match correspondance) =>
        correspondance.Groups[1].Success ? correspondance.Groups[1].Value : correspondance.Groups[2].Value;

    private static List<(DateTime Horodatage, string Message)> LireEvenements(
        string contenu,
        out int lignesLues,
        out int lignesIgnorees)
    {
        var evenements = new List<(DateTime, string)>();
        lignesLues = 0;
        lignesIgnorees = 0;

        foreach (var ligne in LireLignes(contenu))
        {
            lignesLues++;

            if (lignesLues > MaxLignes)
            {
                throw new Infrastructure.RequeteInvalideException(
                    "log",
                    $"Le journal depasse {MaxLignes:N0} lignes.");
            }

            var entete = Entete().Match(ligne);
            if (!entete.Success)
            {
                lignesIgnorees++;
                continue;
            }

            if (!DateTime.TryParseExact(
                    entete.Groups[1].Value,
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var horodatage))
            {
                lignesIgnorees++;
                continue;
            }

            horodatage = horodatage.AddMilliseconds(int.Parse(entete.Groups[2].Value, CultureInfo.InvariantCulture));
            evenements.Add((horodatage, entete.Groups[3].Value));
        }

        return evenements;
    }

    private static IEnumerable<string> LireLignes(string contenu)
    {
        using var lecteur = new StringReader(contenu);
        while (lecteur.ReadLine() is { } ligne)
        {
            if (!string.IsNullOrWhiteSpace(ligne))
            {
                yield return ligne;
            }
        }
    }

    private static string Tronquer(string message) =>
        message.Length <= 120 ? message : message[..117] + "...";

    /// <summary>
    /// L'acteur d'une ligne, sous ses deux ecritures : « ALIAS (SLOT) » ou « SLOT » seul. Les
    /// deux groupes sont exclusifs, <see cref="Slot"/> choisit celui qui a matche.
    /// </summary>
    private const string Acteur = @"(?:.+? \(([^()]+)\)|(\S+))";

    [GeneratedRegex(@"^\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}),(\d{1,3})\]: (.*)$")]
    private static partial Regex Entete();

    /// <summary>
    /// « (Team #1) W2FF4 sent Sword to W7Z1 (Baron Castle -- 1F) » : hors rafale, le check
    /// appartient a l'EXPEDITEUR, qui l'a trouve dans son monde. Le destinataire n'est que le
    /// proprietaire de l'objet — mais il sert a reconnaitre le sens d'une rafale.
    /// </summary>
    [GeneratedRegex(@"^\(Team #\d+\) (\S+) sent .+? to (\S+) \(.+\)$")]
    private static partial Regex Check();

    [GeneratedRegex($@"^Notice \(all\): {Acteur} \(Team #\d+\) playing (.+?) has joined\.")]
    private static partial Regex Connexion();

    [GeneratedRegex($@"^Notice \(all\): {Acteur} \(Team #\d+\) has completed their goal\.$")]
    private static partial Regex Objectif();

    [GeneratedRegex($@"^Notice \(all\): {Acteur} \(Team #\d+\) has collected their items from other worlds\.$")]
    private static partial Regex Collecte();

    [GeneratedRegex($@"^Notice \(all\): {Acteur} \(Team #\d+\) has released all remaining items from their world\.$")]
    private static partial Regex Liberation();

    [GeneratedRegex(@"^Notice \(all\): \S+.*: ")]
    private static partial Regex Bavardage();

    [GeneratedRegex(@"^Notice \(all\): .*\(Team #\d+\) (tracking|viewing) .+ has joined\.")]
    private static partial Regex Suivi();
}

/// <summary>Noms des familles de lignes reconnues dans un journal.</summary>
public static class SignauxLog
{
    public const string Check = "check";
    public const string Connexion = "connexion";
    public const string ConnexionRefusee = "connexion-refusee";
    public const string Objectif = "objectif";
    public const string Liberation = "liberation";
    public const string Collecte = "collecte";
    public const string EquipeTerminee = "equipe-terminee";
    public const string Depart = "depart";
    public const string SuiviDemarre = "suivi-demarre";
    public const string SuiviArrete = "suivi-arrete";
    public const string Indice = "indice";
    public const string Bavardage = "bavardage";
    public const string MessageServeur = "message-serveur";
    public const string Hebergement = "hebergement";
    public const string ChargementJeu = "chargement-jeu";
    public const string Sauvegarde = "sauvegarde";
    public const string Arret = "arret";
    public const string Inconnu = "inconnu";
}
