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
  checksTrouves: number
  pourcentCompleteMoyen: number | null
  abandons: number
  /** Total des penalites d'abandon comprises dans le temps cumule. */
  penaliteCumuleeSecs: number
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
  debut: string | null
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

/** Progression d'un joueur : instant de chaque check, en secondes depuis le depart. */
export interface ProgressionJoueur {
  joueurId: number
  joueurNom: string
  equipeId: number | null
  equipeNom: string
  jeuNom: string
  secondes: number[]
}

export interface ProgressionMatch {
  matchId: number
  joueurs: ProgressionJoueur[]
}
