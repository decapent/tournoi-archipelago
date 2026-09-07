/** Acces HTTP a l'API : injection du jeton, lecture des ProblemDetails. */

const CLE_SESSION = 'tournoi-archipelago.session'

/** Erreur renvoyee par l'API, avec le detail par champ quand il est fourni. */
export class ErreurApi extends Error {
  readonly statut: number

  readonly erreursParChamp: Record<string, string[]>

  constructor(statut: number, message: string, erreursParChamp: Record<string, string[]> = {}) {
    super(message)
    this.name = 'ErreurApi'
    this.statut = statut
    this.erreursParChamp = erreursParChamp
  }

  /** Tous les messages de validation, a plat, pour un affichage simple. */
  get messages(): string[] {
    const details = Object.values(this.erreursParChamp).flat()
    return details.length > 0 ? details : [this.message]
  }
}

let jeton: string | null = lireJetonStocke()

/** Appelee quand l'API rejette le jeton, pour que le contexte d'auth se remette a jour. */
let surExpiration: (() => void) | null = null

export function definirJeton(nouveau: string | null): void {
  jeton = nouveau

  if (nouveau === null) {
    sessionStorage.removeItem(CLE_SESSION)
  } else {
    sessionStorage.setItem(CLE_SESSION, nouveau)
  }
}

export function jetonCourant(): string | null {
  return jeton
}

export function surExpirationJeton(rappel: (() => void) | null): void {
  surExpiration = rappel
}

function lireJetonStocke(): string | null {
  try {
    return sessionStorage.getItem(CLE_SESSION)
  } catch {
    // sessionStorage indisponible (mode prive tres restrictif, tests) : on reste anonyme.
    return null
  }
}

interface OptionsRequete {
  methode?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  corps?: unknown
  parametres?: Record<string, string | number | undefined>
}

export async function appelerApi<T>(chemin: string, options: OptionsRequete = {}): Promise<T> {
  const { methode = 'GET', corps, parametres } = options

  const entetes: Record<string, string> = {}
  if (corps !== undefined) {
    entetes['Content-Type'] = 'application/json'
  }
  if (jeton !== null) {
    entetes.Authorization = `Bearer ${jeton}`
  }

  const reponse = await fetch(`/api${chemin}${construireQuery(parametres)}`, {
    method: methode,
    headers: entetes,
    body: corps === undefined ? undefined : JSON.stringify(corps),
  })

  if (reponse.status === 401 && jeton !== null) {
    // Le jeton a expire ou a ete invalide : on force une reconnexion.
    definirJeton(null)
    surExpiration?.()
  }

  if (!reponse.ok) {
    throw await lireErreur(reponse)
  }

  return reponse.status === 204 ? (undefined as T) : ((await reponse.json()) as T)
}

function construireQuery(parametres?: Record<string, string | number | undefined>): string {
  if (parametres === undefined) {
    return ''
  }

  const query = new URLSearchParams()
  for (const [cle, valeur] of Object.entries(parametres)) {
    if (valeur !== undefined && valeur !== '') {
      query.set(cle, String(valeur))
    }
  }

  const rendu = query.toString()
  return rendu === '' ? '' : `?${rendu}`
}

interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

async function lireErreur(reponse: Response): Promise<ErreurApi> {
  let probleme: ProblemDetails = {}

  try {
    probleme = (await reponse.json()) as ProblemDetails
  } catch {
    // Reponse sans corps JSON : on se rabat sur le code de statut.
  }

  const message =
    probleme.detail ??
    probleme.title ??
    (reponse.status === 401
      ? 'Session expiree, reconnecte-toi.'
      : `La requete a echoue (HTTP ${reponse.status}).`)

  return new ErreurApi(reponse.status, message, probleme.errors ?? {})
}
