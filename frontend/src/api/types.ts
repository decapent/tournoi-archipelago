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

export interface Equipe {
  id: number
  nom: string
  /** Roster de quatre joueurs, dont 2 a 4 participent selon l'etape. */
  membres: Joueur[]
}

export interface LigneResultat {
  joueurId: number
  joueurNom: string
  jeuId: number
  jeuNom: string
  seed: string | null
  totalChecks: number | null
  nbChecks: number | null
  /** Temps brut : completion, ou instant de l'abandon. */
  tempsFinalSecs: number
  /** Temps retenu au classement, penalite d'abandon incluse. */
  tempsEffectifSecs: number
  estAbandon: boolean
  /** Part des checks trouves, entre 0 et 1. */
  pourcentComplete: number | null
}

export interface EquipeResultat {
  equipeId: number | null
  equipeNom: string
  position: number
  estGagnante: boolean
  /** Score de l'equipe : somme des temps effectifs de ses participants. */
  tempsTotalSecs: number
  /** Somme des temps saisis, avant penalite. */
  tempsBrutSecs: number
  /** Total des penalites d'abandon incluses dans le score. */
  penaliteSecs: number
  nbAbandons: number
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
  equipeGagnanteNom: string | null
  equipeNoms: string[]
}

export interface MatchDetail {
  id: number
  date: string
  type: TypeMatch
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
  /** Toujours requis : temps de completion, ou instant de l'abandon. */
  tempsFinalSecs: number
  estAbandon: boolean
}

export interface MatchUpsert {
  date: string
  type: TypeMatch
  equipeAId: number
  equipeBId: number
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
