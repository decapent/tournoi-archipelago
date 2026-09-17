/** Types miroirs des DTOs de l'API (voir backend/src/TournoiArchipelago.Api/Contracts). */

export type TypeMatch = 'QUALIFICATION' | 'TOURNOI'

export const TYPES_MATCH: readonly TypeMatch[] = ['QUALIFICATION', 'TOURNOI']

export type TriClassement = 'Victoires' | 'Temps'

export interface Joueur {
  id: number
  nom: string
}

export interface Jeu {
  id: number
  nom: string
}

export interface Membre extends Joueur {
  estCapitaine: boolean
}

export interface Equipe {
  id: number
  nom: string
  /** Roster de quatre joueurs, dont 2 a 4 participent selon l'etape. Capitaine en tete. */
  membres: Membre[]
  capitaine: Membre | null
}

export interface LigneResultat {
  joueurId: number
  joueurNom: string
  jeuId: number
  jeuNom: string
  seed: string | null
  totalChecks: number | null
  nbChecks: number | null
  /** Temps brut : completion, ou instant de l'abandon. Null s'il reste a saisir. */
  tempsFinalSecs: number | null
  /** Temps retenu au classement, penalite incluse. Null s'il reste a saisir. */
  tempsEffectifSecs: number | null
  estAbandon: boolean
  /** Vrai quand le temps de ce joueur reste a saisir. */
  estEnAttente: boolean
  /** Part des checks trouves, entre 0 et 1. */
  pourcentComplete: number | null
}

export interface EquipeResultat {
  equipeId: number | null
  equipeNom: string
  position: number
  estGagnante: boolean
  /**
   * Score de l'equipe : le plus long des temps effectifs de ses participants, pas leur somme.
   * Null tant qu'un resultat de l'equipe reste a saisir.
   */
  tempsTotalSecs: number | null
  /** Temps brut de la seed determinante, avant penalite. */
  tempsBrutSecs: number | null
  /** Penalite portee par la seed determinante : une heure si c'est un abandon, sinon zero. */
  penaliteSecs: number
  nbAbandons: number
  /** Nombre de participants dont le temps reste a saisir. */
  nbResultatsEnAttente: number
  checksTrouves: number
  totalChecks: number | null
  pourcentComplete: number | null
  lignes: LigneResultat[]
}

export interface MatchSommaire {
  id: number
  /** Date au format ISO (AAAA-MM-JJ). */
  date: string
  type: TypeMatch
  /** Faux tant qu'un resultat reste a saisir : le match est alors hors statistiques. */
  estComplet: boolean
  nbResultatsEnAttente: number
  equipeGagnanteNom: string | null
  equipeNoms: string[]
}

export interface MatchDetail {
  id: number
  date: string
  type: TypeMatch
  estComplet: boolean
  equipes: EquipeResultat[]
}

export interface ClassementEquipe {
  position: number
  equipeId: number
  equipeNom: string
  matchsJoues: number
  victoires: number
  tempsCumuleSecs: number
  tempsMoyenSecs: number | null
  /**
   * Temps de l'equipe rapporte a celui de son adversaire, moyenne sur ses matchs. Sous 1,
   * l'equipe a ete plus rapide ; au-dela, plus lente. Null sans aucun match joue.
   */
  tempsRelatif: number | null
  checksTrouves: number
  pourcentCompleteMoyen: number | null
  abandons: number
  /** Total des penalites d'abandon comprises dans le temps cumule. */
  penaliteCumuleeSecs: number
}

/**
 * Bilan d'un joueur sur le tournoi. Un joueur court une seule seed par match : le nombre de
 * seeds jouees est donc aussi son nombre de matchs.
 */
export interface StatsJoueur {
  joueurId: number
  joueurNom: string
  equipeId: number | null
  equipeNom: string
  seedsJouees: number
  /** Matchs complets remportes par son equipe. */
  victoires: number
  /** Moyenne des seeds terminees : les abandons ne mesurent pas une completion. */
  tempsMoyenSecs: number | null
  tempsMedianSecs: number | null
  meilleurTempsSecs: number | null
  meilleurJeuNom: string | null
  checksTrouves: number
  pourcentCompleteMoyen: number | null
  /** Rythme sur les seeds terminees : checks trouves par heure de jeu. */
  checksParHeure: number | null
  nbAbandons: number
  /** Nombre de fois ou sa seed a fixe le temps de son equipe, en etant la plus longue. */
  seedsDeterminantes: number
  /** Demandes d'indice, abouties ou non : ce qu'il a cherche. */
  indicesDemandes: number
  /** Emplacements que les indices lui ont reveles : l'aide reellement recue. */
  indicesObtenus: number
}

export interface StatsJeu {
  jeuId: number
  jeuNom: string
  nbParties: number
  tempsMoyenSecs: number | null
  tempsMedianSecs: number | null
  meilleurTempsSecs: number | null
  meilleurJoueurNom: string | null
  nbChecksMoyen: number | null
  pourcentCompleteMoyen: number | null
  nbAbandons: number
}

export interface ResultatUpsert {
  joueurId: number
  jeuId: number
  seed: string | null
  totalChecks: number | null
  nbChecks: number | null
  /** Temps de completion, ou instant de l'abandon. Null tant qu'il reste a saisir. */
  tempsFinalSecs: number | null
  estAbandon: boolean
}

export interface MatchUpsert {
  date: string
  type: TypeMatch
  equipeAId: number
  equipeBId: number
  /** Peut etre vide a la creation : les resultats se completent ensuite. */
  resultats: ResultatUpsert[]
}

export interface SessionAdmin {
  token: string
  expireLe: string
  username: string
}

export interface FiltresMatchs {
  type?: TypeMatch
  du?: string
  au?: string
}

/** Ce qu'un journal Archipelago apprend sur un joueur, avant tout rapprochement. */
export interface JoueurLog {
  /** Pseudonyme dans la partie, a rapprocher d'un joueur du roster. */
  alias: string
  /** Jeu annonce a la connexion, tel qu'ecrit par Archipelago. */
  jeu: string | null
  checksTrouves: number
  /** Taille du monde. Connue seulement si le joueur a termine et libere son monde. */
  totalChecks: number | null
  premierCheck: string | null
  dernierCheck: string | null
  objectif: string | null
  estAbandon: boolean
  horodatages: string[]
  /** Chaque demande d'indice, dans l'ordre. */
  indices: IndiceLog[]
  /** Emplacements differents reellement reveles : un reaffichage ne compte pas deux fois. */
  indicesDistincts: number
  /** Parmi eux, ceux pointant un lieu deja visite. L'indice n'apprend alors rien. */
  indicesDejaTrouves: number
}

/** Une demande d'indice telle que le journal la raconte. */
export interface IndiceLog {
  horodatage: string
  terme: string
  resultat: ResultatIndice
  pointsRestants: number | null
  cout: number | null
}

/** Une famille de lignes reconnue dans le journal, avec un exemple. */
export interface SignalLog {
  signal: string
  occurrences: number
  exemple: string
}

export interface RapportLog {
  joueurs: JoueurLog[]
  signaux: SignalLog[]
  /** Premiere ligne horodatee : le serveur demarre, pas la course. */
  debut: string | null
  /**
   * Depart approxime au premier check. Les joueurs attendent longtemps que l'hote lance, et
   * le vrai depart n'apparait nulle part dans le journal.
   */
  departEstime: string | null
  fin: string | null
  lignesLues: number
  lignesIgnorees: number
}

/** Rapprochement d'un pseudonyme du journal avec un joueur du roster. */
export interface CorrespondanceAlias {
  alias: string
  joueurId: number
}

export interface ImportLog {
  contenu: string
  /** Depart commun de la course, au format `AAAA-MM-JJTHH:MM:SS`. */
  departCourse: string
  correspondances: CorrespondanceAlias[]
}

/** Ce qu'une demande d'indice a donne. */
export type ResultatIndice = 'Obtenu' | 'Refuse' | 'SansReponse'

/** Une demande d'indice situee dans la course. */
export interface IndiceProgression {
  secondes: number
  resultat: ResultatIndice
  /** Solde de points au moment de la demande. Le serveur ne l'annonce qu'en refusant. */
  pointsRestants: number | null
}

/** Progression d'un joueur : instant de chaque check, en secondes depuis le depart. */
export interface ProgressionJoueur {
  joueurId: number
  joueurNom: string
  equipeId: number | null
  equipeNom: string
  jeuNom: string
  /** Taille du monde, pour situer la progression par rapport a son terme. */
  totalChecks: number | null
  /** Instants des checks reellement trouves. */
  secondes: number[]
  /**
   * Instants des lignes de rafale, collecte et liberation. La courbe les trace a la suite
   * des precedents : elle rejoint ainsi la taille du monde a l'instant de la completion.
   */
  secondesRafale: number[]
  indices: IndiceProgression[]
}

export interface ProgressionMatch {
  matchId: number
  joueurs: ProgressionJoueur[]
}
