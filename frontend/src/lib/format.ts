/** Conversions entre les valeurs de l'API et leur affichage. */

const SECONDES_PAR_MINUTE = 60
const SECONDES_PAR_HEURE = 3600

/** Marqueur affiche a la place d'une valeur absente. */
export const ABSENT = '—'

/**
 * Formate un nombre de secondes en `h:mm:ss`, ou `mm:ss` sous une heure.
 *
 * Un abandon n'est pas un temps absent : il porte l'instant ou le joueur a arrete, et se
 * signale par un libelle distinct dans l'interface.
 */
export function formaterTemps(secondes: number | null | undefined): string {
  if (secondes === null || secondes === undefined) {
    return ABSENT
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
 *
 * Renvoie `undefined` pour une saisie vide ou invalide : un temps est toujours requis, y
 * compris pour un abandon.
 */
export function analyserTemps(saisie: string): number | undefined {
  const propre = saisie.trim()
  if (propre === '') {
    return undefined
  }

  const morceaux = propre.split(':')
  if (morceaux.length > 3) {
    return undefined
  }

  const nombres: number[] = []
  for (const morceau of morceaux) {
    // Le premier segment n'est pas borne a deux chiffres : il porte les heures, ou un nombre
    // de secondes brut quand la saisie n'a pas de separateur.
    if (!/^\d{1,6}$/.test(morceau.trim())) {
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

/** Formate une part (0 a 1) en pourcentage, ou le marqueur d'absence. */
export function formaterPourcent(part: number | null | undefined, decimales = 1): string {
  if (part === null || part === undefined) {
    return ABSENT
  }

  return `${formaterNombre(part * 100, decimales)} %`
}

/** Formate un nombre, ou le marqueur d'absence. */
export function formaterNombre(valeur: number | null | undefined, decimales = 0): string {
  if (valeur === null || valeur === undefined) {
    return ABSENT
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

/** Date du jour au format attendu par l'API et par un champ `input type="date"`. */
export function dateDuJourIso(): string {
  const maintenant = new Date()
  const mois = String(maintenant.getMonth() + 1).padStart(2, '0')
  const jour = String(maintenant.getDate()).padStart(2, '0')
  return `${maintenant.getFullYear()}-${mois}-${jour}`
}
