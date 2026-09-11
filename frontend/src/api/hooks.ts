import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { appelerApi } from './client'
import type {
  ClassementEquipe,
  Equipe,
  FiltresMatchs,
  ImportLog,
  Jeu,
  Joueur,
  MatchDetail,
  MatchSommaire,
  MatchUpsert,
  ProgressionMatch,
  RapportLog,
  StatsJeu,
  TriClassement,
  TypeMatch,
} from './types'

/** Cles de cache, regroupees pour rester coherentes entre lectures et invalidations. */
export const cles = {
  joueurs: ['joueurs'] as const,
  jeux: ['jeux'] as const,
  equipes: ['equipes'] as const,
  matchs: (filtres: FiltresMatchs = {}) => ['matchs', filtres] as const,
  match: (id: number) => ['matchs', id] as const,
  classement: (type?: TypeMatch, tri?: TriClassement) => ['classement', type, tri] as const,
  statsJeux: (type?: TypeMatch) => ['stats-jeux', type] as const,
  progression: (matchId: number) => ['matchs', matchId, 'progression'] as const,
}

export function useJoueurs() {
  return useQuery({ queryKey: cles.joueurs, queryFn: () => appelerApi<Joueur[]>('/joueurs') })
}

export function useJeux() {
  return useQuery({ queryKey: cles.jeux, queryFn: () => appelerApi<Jeu[]>('/jeux') })
}

export function useEquipes() {
  return useQuery({ queryKey: cles.equipes, queryFn: () => appelerApi<Equipe[]>('/equipes') })
}

export function useMatchs(filtres: FiltresMatchs = {}) {
  return useQuery({
    queryKey: cles.matchs(filtres),
    queryFn: () =>
      appelerApi<MatchSommaire[]>('/matchs', {
        parametres: { type: filtres.type, du: filtres.du, au: filtres.au },
      }),
  })
}

export function useMatch(id: number | undefined) {
  return useQuery({
    queryKey: cles.match(id ?? 0),
    queryFn: () => appelerApi<MatchDetail>(`/matchs/${id}`),
    enabled: id !== undefined,
  })
}

export function useClassement(type?: TypeMatch, tri: TriClassement = 'Victoires') {
  return useQuery({
    queryKey: cles.classement(type, tri),
    queryFn: () => appelerApi<ClassementEquipe[]>('/stats/classement', { parametres: { type, tri } }),
  })
}

export function useStatsJeux(type?: TypeMatch) {
  return useQuery({
    queryKey: cles.statsJeux(type),
    queryFn: () => appelerApi<StatsJeu[]>('/stats/jeux', { parametres: { type } }),
  })
}

/**
 * Invalide tout ce qui derive des matchs. Une saisie change a la fois l'historique,
 * le classement et les statistiques par jeu.
 */
function useInvaliderStats() {
  const cache = useQueryClient()

  return () => {
    void cache.invalidateQueries({ queryKey: ['matchs'] })
    void cache.invalidateQueries({ queryKey: ['classement'] })
    void cache.invalidateQueries({ queryKey: ['stats-jeux'] })
  }
}

export function useCreerMatch() {
  const invalider = useInvaliderStats()

  return useMutation({
    mutationFn: (match: MatchUpsert) =>
      appelerApi<MatchDetail>('/matchs', { methode: 'POST', corps: match }),
    onSuccess: invalider,
  })
}

export function useModifierMatch(id: number) {
  const invalider = useInvaliderStats()

  return useMutation({
    mutationFn: (match: MatchUpsert) =>
      appelerApi<MatchDetail>(`/matchs/${id}`, { methode: 'PUT', corps: match }),
    onSuccess: invalider,
  })
}

export function useSupprimerMatch() {
  const invalider = useInvaliderStats()

  return useMutation({
    mutationFn: (id: number) => appelerApi<void>(`/matchs/${id}`, { methode: 'DELETE' }),
    onSuccess: invalider,
  })
}

export function useCreerJoueur() {
  const cache = useQueryClient()

  return useMutation({
    mutationFn: (nom: string) =>
      appelerApi<Joueur>('/joueurs', { methode: 'POST', corps: { nom } }),
    onSuccess: () => cache.invalidateQueries({ queryKey: cles.joueurs }),
  })
}

export function useRenommerJoueur() {
  const cache = useQueryClient()

  return useMutation({
    mutationFn: ({ id, nom }: { id: number; nom: string }) =>
      appelerApi<Joueur>(`/joueurs/${id}`, { methode: 'PUT', corps: { nom } }),
    onSuccess: () => {
      void cache.invalidateQueries({ queryKey: cles.joueurs })
      void cache.invalidateQueries({ queryKey: cles.equipes })
    },
  })
}

export function useSupprimerJoueur() {
  const cache = useQueryClient()

  return useMutation({
    mutationFn: (id: number) => appelerApi<void>(`/joueurs/${id}`, { methode: 'DELETE' }),
    onSuccess: () => cache.invalidateQueries({ queryKey: cles.joueurs }),
  })
}

export function useCreerJeu() {
  const cache = useQueryClient()

  return useMutation({
    mutationFn: (nom: string) => appelerApi<Jeu>('/jeux', { methode: 'POST', corps: { nom } }),
    onSuccess: () => {
      void cache.invalidateQueries({ queryKey: cles.jeux })
      void cache.invalidateQueries({ queryKey: ['stats-jeux'] })
    },
  })
}

export function useSupprimerJeu() {
  const cache = useQueryClient()

  return useMutation({
    mutationFn: (id: number) => appelerApi<void>(`/jeux/${id}`, { methode: 'DELETE' }),
    onSuccess: () => {
      void cache.invalidateQueries({ queryKey: cles.jeux })
      void cache.invalidateQueries({ queryKey: ['stats-jeux'] })
    },
  })
}

export interface EquipeSaisie {
  nom: string
  joueurIds: number[]
  /** Capitaine, qui doit figurer dans le roster. */
  capitaineId: number | null
}

export function useCreerEquipe() {
  const cache = useQueryClient()

  return useMutation({
    mutationFn: (equipe: EquipeSaisie) =>
      appelerApi<Equipe>('/equipes', { methode: 'POST', corps: equipe }),
    onSuccess: () => {
      void cache.invalidateQueries({ queryKey: cles.equipes })
      void cache.invalidateQueries({ queryKey: ['classement'] })
    },
  })
}

export function useModifierEquipe() {
  const cache = useQueryClient()

  return useMutation({
    mutationFn: ({ id, ...equipe }: EquipeSaisie & { id: number }) =>
      appelerApi<Equipe>(`/equipes/${id}`, { methode: 'PUT', corps: equipe }),
    onSuccess: () => {
      void cache.invalidateQueries({ queryKey: cles.equipes })
      void cache.invalidateQueries({ queryKey: ['classement'] })
      void cache.invalidateQueries({ queryKey: ['matchs'] })
    },
  })
}

export function useSupprimerEquipe() {
  const cache = useQueryClient()

  return useMutation({
    mutationFn: (id: number) => appelerApi<void>(`/equipes/${id}`, { methode: 'DELETE' }),
    onSuccess: () => {
      void cache.invalidateQueries({ queryKey: cles.equipes })
      void cache.invalidateQueries({ queryKey: ['classement'] })
    },
  })
}

/**
 * Lit un journal Archipelago sans rien enregistrer, pour proposer le rapprochement des
 * pseudonymes avant l'import.
 */
export function useAnalyserLog() {
  return useMutation({
    mutationFn: (contenu: string) =>
      appelerApi<RapportLog>('/logs/analyse', { methode: 'POST', corps: { contenu } }),
  })
}

/** Reporte un journal sur les resultats d'une equipe du match. */
export function useImporterLog(matchId: number, equipeId: number) {
  const cache = useQueryClient()
  const invalider = useInvaliderStats()

  return useMutation({
    mutationFn: (import_: ImportLog) =>
      appelerApi<MatchDetail>(`/matchs/${matchId}/equipes/${equipeId}/log`, {
        methode: 'POST',
        corps: import_,
      }),
    onSuccess: () => {
      invalider()
      void cache.invalidateQueries({ queryKey: cles.progression(matchId) })
    },
  })
}

/** Courbes de progression d'un match, en secondes depuis le depart de la course. */
export function useProgression(matchId: number | undefined) {
  return useQuery({
    queryKey: cles.progression(matchId ?? 0),
    queryFn: () => appelerApi<ProgressionMatch>(`/matchs/${matchId}/progression`),
    enabled: matchId !== undefined,
  })
}
