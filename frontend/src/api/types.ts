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
  /** Null signifie un abandon. */
  tempsFinalSecs: number | null
  estAbandon: boolean
  /** Part des checks trouves, entre 0 et 1. */
  pourcentComplete: number | null
}

export interface EquipeResultat {
  equipeId: number | null
  equipeNom: string
  position: number
  estGagnante: boolean
  tempsTotalSecs: number | null
  estAbandon: boolean
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
  tempsFinalSecs: number | null
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
