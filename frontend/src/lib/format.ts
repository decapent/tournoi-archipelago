/** Conversions entre les valeurs de l'API et leur affichage. */

const SECONDES_PAR_MINUTE = 60
const SECONDES_PAR_HEURE = 3600

/** Marqueur affiche a la place d'un temps absent (abandon). */
export const ABANDON = 'DNF'

/**
 * Formate un nombre de secondes en `h:mm:ss`, ou `mm:ss` sous une heure.
 * Renvoie {@link ABANDON} pour une valeur absente.
 */
export function formaterTemps(secondes: number | null | undefined): string {
  if (secondes === null || secondes === undefined) {
    return ABANDON
  }

  const total = Math.max(0, Math.round(secondes))
  const heures = Math.floor(total / SECONDES_PAR_HEURE)
  const minutes = Math.floor((total % SECONDES_PAR_HEURE) / SECONDES_PAR_MINUTE)
  const restantes = total % SECONDES_PAR_MINUTE

  const mm = String(minutes).padStart(2, '0')
  const ss = String(restantes).padStart(2, '0')

  return heures > 0 ? `${heures}:${mm}:${ss}` : `${mm}:${ss}`
}

/**
 * Lit une duree saisie a la main. Accepte `ss`, `mm:ss` et `hh:mm:ss`.
 * Renvoie `null` pour une saisie vide (abandon) et `undefined` si la saisie est invalide,
 * ce qui permet de distinguer les deux cas dans un formulaire.
 */
export function analyserTemps(saisie: string): number | null | undefined {
  const propre = saisie.trim()
  if (propre === '') {
    return null
  }

  const morceaux = propre.split(':')
  if (morceaux.length > 3) {
    return undefined
  }

  const nombres: number[] = []
  for (const morceau of morceaux) {
    if (!/^\d{1,3}$/.test(morceau.trim())) {
      return undefined
    }
    nombres.push(Number(morceau.trim()))
  }

  // Les segments hors du premier representent des minutes ou des secondes : ils restent sous 60.
  if (nombres.length > 1 && nombres.slice(1).some((valeur) => valeur > 59)) {
    return undefined
  }

  const total = nombres.reduce((cumul, valeur) => cumul * SECONDES_PAR_MINUTE + valeur, 0)
  return total > 0 ? total : undefined
}

/** Formate une part (0 a 1) en pourcentage, ou `—` si elle est absente. */
export function formaterPourcent(part: number | null | undefined, decimales = 1): string {
  if (part === null || part === undefined) {
    return '—'
  }

  return `${formaterNombre(part * 100, decimales)} %`
}

/** Formate un nombre, ou `—` s'il est absent. */
export function formaterNombre(valeur: number | null | undefined, decimales = 0): string {
  if (valeur === null || valeur === undefined) {
    return '—'
  }

  return valeur.toLocaleString('fr-CA', {
    minimumFractionDigits: decimales,
    maximumFractionDigits: decimales,
  })
}

/** Formate une date ISO (AAAA-MM-JJ) pour l'affichage, sans decalage de fuseau. */
export function formaterDate(iso: string): string {
  const [annee, mois, jour] = iso.split('T')[0].split('-')
  return `${jour}-${mois}-${annee}`
}

/** Date du jour au format attendu par l'API et par `<input type="date">`. */
export function dateDuJourIso(): string {
  const maintenant = new Date()
  const mois = String(maintenant.getMonth() + 1).padStart(2, '0')
  const jour = String(maintenant.getDate()).padStart(2, '0')
  return `${maintenant.getFullYear()}-${mois}-${jour}`
}

/**
 * Total des temps d'une equipe, tel que le calcule le backend : la somme des temps saisis,
 * et un abandon des qu'un membre n'a pas de temps.
 */
export function totaliserEquipe(temps: readonly (number | null)[]): {
  totalSecs: number | null
  estAbandon: boolean
} {
  const renseignes = temps.filter((valeur): valeur is number => valeur !== null)

  return {
    totalSecs: renseignes.length > 0 ? renseignes.reduce((a, b) => a + b, 0) : null,
    estAbandon: renseignes.length !== temps.length,
  }
}
