import { analyserTemps } from '../../lib/format'
import type { ResultatUpsert } from '../../api/types'

/** Bornes imposees par l'API (ScoringService.NbJoueursMin/MaxParEquipe). */
export const MIN_PARTICIPANTS = 2
export const MAX_PARTICIPANTS = 4

/** Majoration appliquee au temps d'un abandon (ScoringService.PenaliteAbandonSecs). */
export const PENALITE_ABANDON_SECS = 3600

/** Une ligne du formulaire de saisie : les champs restent des chaines tant qu on edite. */
export interface LigneSaisie {
  joueurId: number
  joueurNom: string
  /** Equipe a laquelle le joueur appartient, pour le regroupement de l apercu. */
  equipeId: number
  equipeNom: string
  /**
   * Faux pour un membre du roster qui ne joue pas ce match. Le format decoule de ce choix :
   * deux participants par equipe en qualification, trois en demi-finale, quatre en finale.
   */
  participe: boolean
  /** Vrai quand le joueur n a pas termine : son temps est alors majore d une heure. */
  estAbandon: boolean
  jeuId: number | null
  seed: string
  totalChecks: string
  nbChecks: string
  /**
   * Temps saisi en `hh:mm:ss`, `mm:ss` ou secondes. Peut rester vide : le resultat sera
   * complete plus tard. Pour un abandon, c est l instant ou le joueur a arrete.
   */
  temps: string
}

export interface ApercuEquipe {
  equipeId: number
  equipeNom: string
  position: number
  estGagnante: boolean
  /** Score de l equipe. Null tant qu un temps de l equipe reste a saisir. */
  totalSecs: number | null
  /** Temps brut de la seed determinante, avant penalite. */
  totalBrutSecs: number | null
  /** Penalite portee par la seed determinante : une heure si c est un abandon, sinon zero. */
  penaliteSecs: number
  nbAbandons: number
  nbResultatsEnAttente: number
  checksTrouves: number
  totalChecks: number | null
  pourcentComplete: number | null
}

export interface Apercu {
  /** Vrai quand le match satisfait les regles de completion du backend. */
  estComplet: boolean
  equipes: ApercuEquipe[]
  /** Ce qu il reste a faire pour que le match compte au classement. */
  manquants: string[]
}

/**
 * Etat d un temps saisi. On distingue le champ vide (resultat a venir) d une saisie mal
 * formee (erreur a corriger) : seul le second bloque l enregistrement.
 */
type TempsSaisi =
  | { etat: 'vide' }
  | { etat: 'valide'; secondes: number }
  | { etat: 'invalide' }

export function lireTemps(saisie: string): TempsSaisi {
  if (saisie.trim() === '') {
    return { etat: 'vide' }
  }

  const secondes = analyserTemps(saisie)
  return secondes === undefined ? { etat: 'invalide' } : { etat: 'valide', secondes }
}

/** Temps retenu au classement pour une ligne, ou `null` s il reste a saisir. */
export function tempsEffectif(ligne: LigneSaisie): number | null {
  const temps = lireTemps(ligne.temps)

  return temps.etat === 'valide'
    ? temps.secondes + (ligne.estAbandon ? PENALITE_ABANDON_SECS : 0)
    : null
}

/**
 * Reproduit le classement du backend (ScoringService) pour donner un apercu avant l envoi :
 * un abandon compte son temps majore d une heure, le score d une equipe est le plus long des
 * temps ainsi obtenus, et le plus petit score gagne. Tant que le match n est pas complet,
 * aucun vainqueur n est designe.
 */
export function calculerApercu(lignes: readonly LigneSaisie[]): Apercu {
  const groupes = new Map<number, LigneSaisie[]>()

  for (const ligne of lignes.filter((l) => l.participe)) {
    const existantes = groupes.get(ligne.equipeId)
    if (existantes === undefined) {
      groupes.set(ligne.equipeId, [ligne])
    } else {
      existantes.push(ligne)
    }
  }

  const agreges = [...groupes.entries()].map(([equipeId, sesLignes]) => {
    const nbResultatsEnAttente = sesLignes.filter(
      (ligne) => tempsEffectif(ligne) === null,
    ).length

    const nbAbandons = sesLignes.filter((ligne) => ligne.estAbandon).length

    // Le score d une equipe est le temps de sa seed la plus longue : elle a fini quand son
    // dernier joueur a fini. Il n a de sens qu une fois tous ses temps saisis.
    const determinante =
      sesLignes.length > 0 && nbResultatsEnAttente === 0
        ? sesLignes.reduce((pire, ligne) =>
            (tempsEffectif(ligne) ?? 0) > (tempsEffectif(pire) ?? 0) ? ligne : pire,
          )
        : null

    const totalSecs = determinante === null ? null : tempsEffectif(determinante)

    // La penalite exposee est celle de la ligne determinante, pas la somme de toutes : seule
    // celle-ci entre dans le score, ce qui garde l egalite brut + penalite = total.
    const penaliteSecs =
      determinante !== null && determinante.estAbandon ? PENALITE_ABANDON_SECS : 0

    const checksTrouves = sesLignes.reduce(
      (cumul, ligne) => cumul + (entier(ligne.nbChecks) ?? 0),
      0,
    )
    const totaux = sesLignes
      .map((ligne) => entier(ligne.totalChecks))
      .filter((valeur): valeur is number => valeur !== null)
    const totalChecks = totaux.length > 0 ? totaux.reduce((a, b) => a + b, 0) : null

    return {
      equipeId,
      equipeNom: sesLignes[0].equipeNom,
      totalSecs,
      totalBrutSecs: totalSecs === null ? null : totalSecs - penaliteSecs,
      penaliteSecs,
      nbAbandons,
      nbResultatsEnAttente,
      effectif: sesLignes.length,
      checksTrouves,
      totalChecks,
      pourcentComplete:
        totalChecks !== null && totalChecks > 0 ? checksTrouves / totalChecks : null,
    }
  })

  const manquants = decrireManquants(agreges)
  const estComplet = manquants.length === 0

  // Les equipes dont le total n est pas encore calculable passent en fin d apercu.
  const ordonnes = agreges.sort(
    (a, b) =>
      (a.totalSecs ?? Number.MAX_SAFE_INTEGER) - (b.totalSecs ?? Number.MAX_SAFE_INTEGER) ||
      a.equipeNom.localeCompare(b.equipeNom, 'fr'),
  )

  let position = 0
  let totalPrecedent: number | null | undefined

  const equipes = ordonnes.map((groupe, index) => {
    if (totalPrecedent === undefined || totalPrecedent !== groupe.totalSecs) {
      position = index + 1
      totalPrecedent = groupe.totalSecs
    }

    const { effectif: _effectif, ...reste } = groupe
    return { ...reste, position, estGagnante: estComplet && position === 1 }
  })

  return { estComplet, equipes, manquants }
}

/** Mirroir de ScoringService.EstComplet, exprime en langage naturel. */
function decrireManquants(
  groupes: readonly { equipeNom: string; effectif: number; nbResultatsEnAttente: number }[],
): string[] {
  if (groupes.length < 2) {
    return ['Ajouter des participants dans les deux equipes.']
  }

  const manquants: string[] = []

  const effectifs = groupes.map((g) => g.effectif)
  if (new Set(effectifs).size > 1) {
    const detail = groupes.map((g) => `${g.effectif} pour ${g.equipeNom}`).join(' contre ')
    manquants.push(`Les deux equipes doivent aligner le meme nombre de joueurs : ${detail}.`)
  } else if (effectifs[0] < MIN_PARTICIPANTS || effectifs[0] > MAX_PARTICIPANTS) {
    manquants.push(
      `Chaque equipe doit aligner entre ${MIN_PARTICIPANTS} et ${MAX_PARTICIPANTS} joueurs, ${effectifs[0]} selectionne(s).`,
    )
  }

  const enAttente = groupes.reduce((cumul, g) => cumul + g.nbResultatsEnAttente, 0)
  if (enAttente > 0) {
    manquants.push(`${enAttente} temps reste(nt) a saisir.`)
  }

  return manquants
}

/**
 * Traduit les lignes du formulaire en corps de requete. Un temps vide devient `null` : le
 * resultat sera complete plus tard. Renvoie `null` si une ligne est inexploitable, ce qui
 * bloque l envoi.
 */
export function versResultats(lignes: readonly LigneSaisie[]): ResultatUpsert[] | null {
  const resultats: ResultatUpsert[] = []

  for (const ligne of lignes.filter((l) => l.participe)) {
    if (ligne.jeuId === null) {
      return null
    }

    const temps = lireTemps(ligne.temps)
    if (temps.etat === 'invalide') {
      return null
    }

    resultats.push({
      joueurId: ligne.joueurId,
      jeuId: ligne.jeuId,
      seed: ligne.seed.trim() === '' ? null : ligne.seed.trim(),
      totalChecks: entier(ligne.totalChecks),
      nbChecks: entier(ligne.nbChecks),
      tempsFinalSecs: temps.etat === 'valide' ? temps.secondes : null,
      estAbandon: ligne.estAbandon,
    })
  }

  return resultats
}

/**
 * Erreurs qui empechent l enregistrement. Un temps encore inconnu n en fait pas partie :
 * il rend seulement le match incomplet, ce que signale {@link Apercu.manquants}.
 */
export function validerLignes(lignes: readonly LigneSaisie[]): string[] {
  const problemes: string[] = []

  for (const ligne of lignes.filter((l) => l.participe)) {
    if (ligne.jeuId === null) {
      problemes.push(`${ligne.joueurNom} : choisir un jeu.`)
    }

    if (lireTemps(ligne.temps).etat === 'invalide') {
      problemes.push(`${ligne.joueurNom} : temps invalide (attendu hh:mm:ss).`)
    }

    const total = entier(ligne.totalChecks)
    const trouves = entier(ligne.nbChecks)
    if (total !== null && trouves !== null && trouves > total) {
      problemes.push(`${ligne.joueurNom} : ${trouves} checks trouves pour un total de ${total}.`)
    }
  }

  return problemes
}

function entier(saisie: string): number | null {
  const propre = saisie.trim()
  if (propre === '' || !/^\d+$/.test(propre)) {
    return null
  }

  return Number(propre)
}
