import { analyserTemps } from '../../lib/format'
import type { ResultatUpsert } from '../../api/types'

/** Bornes imposees par l'API (MatchService.NbJoueursMin/MaxParEquipe). */
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
   * Temps saisi en `hh:mm:ss`, `mm:ss` ou secondes. Toujours requis : pour un abandon,
   * c est l instant ou le joueur a arrete.
   */
  temps: string
}

export interface ApercuEquipe {
  equipeId: number
  equipeNom: string
  position: number
  estGagnante: boolean
  /** Score de l equipe : somme des temps effectifs de ses participants. */
  totalSecs: number | null
  /** Somme des temps saisis, avant penalite. */
  totalBrutSecs: number | null
  /** Total des penalites d abandon incluses dans le score. */
  penaliteSecs: number
  nbAbandons: number
  checksTrouves: number
  totalChecks: number | null
  pourcentComplete: number | null
}

/** Temps retenu au classement pour une ligne, ou `undefined` si la saisie est inexploitable. */
export function tempsEffectif(ligne: LigneSaisie): number | undefined {
  const brut = analyserTemps(ligne.temps)
  return brut === undefined ? undefined : brut + (ligne.estAbandon ? PENALITE_ABANDON_SECS : 0)
}

/**
 * Reproduit le classement du backend (ScoringService) pour donner un apercu avant l envoi :
 * un abandon compte son temps majore d une heure, on somme les temps ainsi obtenus, et le
 * plus petit total gagne.
 */
export function calculerApercu(lignes: readonly LigneSaisie[]): ApercuEquipe[] {
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
    // Une saisie invalide est ignoree : l apercu reste lisible pendant la frappe.
    const bruts = sesLignes
      .map((ligne) => analyserTemps(ligne.temps))
      .filter((valeur): valeur is number => valeur !== undefined)

    const nbAbandons = sesLignes.filter((ligne) => ligne.estAbandon).length
    const penaliteSecs = nbAbandons * PENALITE_ABANDON_SECS

    const totalBrutSecs = bruts.length > 0 ? bruts.reduce((a, b) => a + b, 0) : null
    const totalSecs = totalBrutSecs === null ? null : totalBrutSecs + penaliteSecs

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
      totalBrutSecs,
      penaliteSecs,
      nbAbandons,
      checksTrouves,
      totalChecks,
      pourcentComplete:
        totalChecks !== null && totalChecks > 0 ? checksTrouves / totalChecks : null,
    }
  })

  // Les equipes dont le total n est pas encore calculable passent en fin d apercu.
  const ordonnes = agreges.sort(
    (a, b) =>
      (a.totalSecs ?? Number.MAX_SAFE_INTEGER) - (b.totalSecs ?? Number.MAX_SAFE_INTEGER) ||
      a.equipeNom.localeCompare(b.equipeNom, 'fr'),
  )

  let position = 0
  let totalPrecedent: number | null | undefined

  return ordonnes.map((groupe, index) => {
    if (totalPrecedent === undefined || totalPrecedent !== groupe.totalSecs) {
      position = index + 1
      totalPrecedent = groupe.totalSecs
    }

    return { ...groupe, position, estGagnante: position === 1 }
  })
}

/**
 * Traduit les lignes du formulaire en corps de requete. Renvoie `null` si une ligne est
 * incomplete ou si un temps est mal forme, ce qui bloque l envoi.
 */
export function versResultats(lignes: readonly LigneSaisie[]): ResultatUpsert[] | null {
  const resultats: ResultatUpsert[] = []

  for (const ligne of lignes.filter((l) => l.participe)) {
    if (ligne.jeuId === null) {
      return null
    }

    const temps = analyserTemps(ligne.temps)
    if (temps === undefined) {
      return null
    }

    resultats.push({
      joueurId: ligne.joueurId,
      jeuId: ligne.jeuId,
      seed: ligne.seed.trim() === '' ? null : ligne.seed.trim(),
      totalChecks: entier(ligne.totalChecks),
      nbChecks: entier(ligne.nbChecks),
      tempsFinalSecs: temps,
      estAbandon: ligne.estAbandon,
    })
  }

  return resultats
}

/** Messages de validation locaux, pour eviter un aller-retour serveur evitable. */
export function validerLignes(lignes: readonly LigneSaisie[]): string[] {
  const problemes: string[] = []
  const participants = lignes.filter((l) => l.participe)

  const effectifs = new Map<string, number>()
  for (const ligne of participants) {
    effectifs.set(ligne.equipeNom, (effectifs.get(ligne.equipeNom) ?? 0) + 1)
  }

  // Meme regle que le backend : les deux equipes alignent autant de joueurs l une que l autre,
  // entre deux et quatre.
  const nombres = [...effectifs.values()]
  if (new Set(nombres).size > 1) {
    const detail = [...effectifs.entries()].map(([nom, n]) => `${n} pour ${nom}`).join(' contre ')
    problemes.push(`Les deux equipes doivent aligner le meme nombre de joueurs : ${detail}.`)
  } else if (
    nombres.length > 0 &&
    (nombres[0] < MIN_PARTICIPANTS || nombres[0] > MAX_PARTICIPANTS)
  ) {
    problemes.push(
      `Chaque equipe doit aligner entre ${MIN_PARTICIPANTS} et ${MAX_PARTICIPANTS} joueurs, ${nombres[0]} selectionne(s).`,
    )
  }

  for (const ligne of participants) {
    if (ligne.jeuId === null) {
      problemes.push(`${ligne.joueurNom} : choisir un jeu.`)
    }

    if (analyserTemps(ligne.temps) === undefined) {
      problemes.push(
        ligne.estAbandon
          ? `${ligne.joueurNom} : saisir le temps atteint au moment de l abandon.`
          : `${ligne.joueurNom} : temps invalide (attendu hh:mm:ss).`,
      )
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
