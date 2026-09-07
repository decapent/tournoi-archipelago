import { analyserTemps } from '../../lib/format'
import type { ResultatUpsert } from '../../api/types'

/** Une ligne du formulaire de saisie : les champs restent des chaines tant qu on edite. */
export interface LigneSaisie {
  joueurId: number
  joueurNom: string
  /** Equipe a laquelle le joueur appartient, pour le regroupement de l apercu. */
  equipeId: number
  equipeNom: string
  jeuId: number | null
  seed: string
  totalChecks: string
  nbChecks: string
  /** Temps saisi en `hh:mm:ss`, `mm:ss` ou secondes. Vide signifie un abandon. */
  temps: string
}

export interface ApercuEquipe {
  equipeId: number
  equipeNom: string
  position: number
  estGagnante: boolean
  totalSecs: number | null
  estAbandon: boolean
  checksTrouves: number
  totalChecks: number | null
  pourcentComplete: number | null
}

/**
 * Reproduit le classement du backend (ScoringService) pour donner un apercu avant l envoi :
 * somme des temps du duo, le plus petit total gagne, et un abandon passe apres toutes les
 * equipes completes, departage par checks trouves.
 */
export function calculerApercu(lignes: readonly LigneSaisie[]): ApercuEquipe[] {
  const groupes = new Map<number, LigneSaisie[]>()

  for (const ligne of lignes) {
    const existantes = groupes.get(ligne.equipeId)
    if (existantes === undefined) {
      groupes.set(ligne.equipeId, [ligne])
    } else {
      existantes.push(ligne)
    }
  }

  const agreges = [...groupes.entries()].map(([equipeId, sesLignes]) => {
    const temps = sesLignes.map((ligne) => analyserTemps(ligne.temps))
    const valides = temps.filter((valeur): valeur is number => typeof valeur === 'number')

    // Une saisie invalide est traitee comme absente : l apercu reste lisible pendant la frappe.
    const estAbandon = valides.length !== sesLignes.length
    const totalSecs = valides.length > 0 ? valides.reduce((a, b) => a + b, 0) : null

    const checksTrouves = sesLignes.reduce((cumul, ligne) => cumul + (entier(ligne.nbChecks) ?? 0), 0)
    const totaux = sesLignes
      .map((ligne) => entier(ligne.totalChecks))
      .filter((valeur): valeur is number => valeur !== null)
    const totalChecks = totaux.length > 0 ? totaux.reduce((a, b) => a + b, 0) : null

    return {
      equipeId,
      equipeNom: sesLignes[0].equipeNom,
      totalSecs,
      estAbandon,
      checksTrouves,
      totalChecks,
      pourcentComplete:
        totalChecks !== null && totalChecks > 0 ? checksTrouves / totalChecks : null,
      cle: [
        estAbandon ? 1 : 0,
        estAbandon ? -checksTrouves : (totalSecs ?? Number.MAX_SAFE_INTEGER),
        totalSecs ?? Number.MAX_SAFE_INTEGER,
      ] as const,
    }
  })

  const ordonnes = agreges.sort(
    (a, b) =>
      a.cle[0] - b.cle[0] ||
      a.cle[1] - b.cle[1] ||
      a.cle[2] - b.cle[2] ||
      a.equipeNom.localeCompare(b.equipeNom, 'fr'),
  )

  let position = 0
  let clePrecedente: readonly number[] | null = null

  return ordonnes.map((groupe, index) => {
    if (clePrecedente === null || !memeCle(clePrecedente, groupe.cle)) {
      position = index + 1
      clePrecedente = groupe.cle
    }

    const { cle: _cle, ...reste } = groupe
    return { ...reste, position, estGagnante: position === 1 }
  })
}

/**
 * Traduit les lignes du formulaire en corps de requete. Renvoie `null` si une ligne est
 * incomplete ou si un temps est mal forme, ce qui bloque l envoi.
 */
export function versResultats(lignes: readonly LigneSaisie[]): ResultatUpsert[] | null {
  const resultats: ResultatUpsert[] = []

  for (const ligne of lignes) {
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
    })
  }

  return resultats
}

/** Messages de validation locaux, pour eviter un aller-retour serveur evitable. */
export function validerLignes(lignes: readonly LigneSaisie[]): string[] {
  const problemes: string[] = []

  for (const ligne of lignes) {
    if (ligne.jeuId === null) {
      problemes.push(`${ligne.joueurNom} : choisir un jeu.`)
    }

    if (analyserTemps(ligne.temps) === undefined) {
      problemes.push(`${ligne.joueurNom} : temps invalide (attendu hh:mm:ss, ou vide pour un abandon).`)
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

function memeCle(a: readonly number[], b: readonly number[]): boolean {
  return a.length === b.length && a.every((valeur, index) => valeur === b[index])
}
