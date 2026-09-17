import { useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useStatsJoueurs } from '../api/hooks'
import type { StatsJoueur, TypeMatch } from '../api/types'
import { Chargement, Erreur, Vide } from '../components/Etats'
import { FiltreType } from '../components/FiltreType'
import { couleurEquipe, styleEquipe } from '../lib/couleursEquipes'
import { ABSENT, formaterNombre, formaterPourcent, formaterTemps } from '../lib/format'

/**
 * Bilan des joueurs, une carte par joueur, l'equipe se choisissant en tete.
 *
 * Un tableau de treize colonnes obligeait a defiler vers la droite pour atteindre la fin
 * d'une ligne — et donc a perdre de vue le nom du joueur. Les equipes comptant quatre
 * joueurs, les quatre cartes de l'une d'elles tiennent dans l'ecran sans defilement.
 */
export function StatsJoueursPage() {
  const [type, setType] = useState<TypeMatch | undefined>(undefined)
  const [parametres, setParametres] = useSearchParams()

  const { data: stats, isPending, error } = useStatsJoueurs(type)

  const equipes = useMemo(() => regrouperParEquipe(stats), [stats])

  // Aucune equipe choisie : on ouvre sur la premiere, plutot que sur un ecran vide.
  const equipe = equipes.find((e) => e.nom === parametres.get('equipe')) ?? equipes[0]

  return (
    <section>
      <header className="mb-4 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold">Statistiques par joueur</h1>
          <p className="text-texte-doux mt-1 text-sm">
            Le bilan de chaque joueur sur le tournoi. Comme pour le classement, un match dont
            un resultat reste a saisir est ignore.
          </p>
        </div>

        <FiltreType valeur={type} onChange={setType} />
      </header>

      {error !== null && <Erreur erreur={error} />}
      {isPending && <Chargement />}

      {stats !== undefined &&
        (equipes.length === 0 ? (
          <Vide>Aucune equipe pour l instant. Cree des joueurs et des equipes pour demarrer.</Vide>
        ) : (
          <>
            <nav className="mb-4 flex flex-wrap gap-2" aria-label="Choix de l equipe">
              {equipes.map((candidate) => {
                const estChoisie = candidate.nom === equipe?.nom

                return (
                  <button
                    key={candidate.nom}
                    type="button"
                    aria-current={estChoisie}
                    className={`rounded border px-3 py-1.5 text-sm font-medium transition-colors ${
                      estChoisie
                        ? 'bg-panneau border-accent'
                        : 'border-bordure hover:border-accent'
                    }`}
                    style={styleEquipe(candidate.nom)}
                    onClick={() => setParametres({ equipe: candidate.nom })}
                  >
                    {candidate.nom}
                  </button>
                )
              })}
            </nav>

            {equipe !== undefined && (
              <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                {equipe.joueurs.map((joueur) => (
                  <CarteJoueur key={joueur.joueurId} joueur={joueur} />
                ))}
              </div>
            )}
          </>
        ))}
    </section>
  )
}

function CarteJoueur({ joueur }: { joueur: StatsJoueur }) {
  const couleur = couleurEquipe(joueur.equipeNom)

  return (
    <article className="panneau flex flex-col overflow-hidden">
      {/* Un filet de la couleur de l'equipe : le nom reste en encre de texte, plus lisible. */}
      <div className="h-0.5" style={{ backgroundColor: couleur ?? 'transparent' }} />

      <header className="border-bordure border-b px-4 py-3">
        <h2 className="truncate font-semibold" title={joueur.joueurNom}>
          {joueur.joueurNom}
        </h2>
        <p className="mt-0.5 truncate text-xs" style={styleEquipe(joueur.equipeNom)}>
          {joueur.equipeNom}
        </p>
      </header>

      <div className="border-bordure grid grid-cols-2 border-b">
        <Vedette libelle="Seeds" valeur={joueur.seedsJouees} />
        <Vedette libelle="Victoires" valeur={joueur.victoires} bordee />
      </div>

      <dl className="flex-1 px-4 py-3 text-sm">
        <Ligne libelle="Temps total" valeur={formaterTemps(joueur.tempsTotalSecs)} />
        <Ligne libelle="Temps moyen" valeur={formaterTemps(joueur.tempsMoyenSecs)} />
        <Ligne
          libelle="Meilleur temps"
          valeur={formaterTemps(joueur.meilleurTempsSecs)}
          detail={joueur.meilleurJeuNom ?? undefined}
        />
        <Ligne libelle="Completion" valeur={formaterPourcent(joueur.pourcentCompleteMoyen)} />

        <Paire
          gauche={{ libelle: 'Checks', valeur: formaterNombre(joueur.checksTrouves) }}
          droite={{
            libelle: 'Checks / h',
            valeur: formaterNombre(joueur.checksParHeure, 1),
            infobulle: 'Rythme sur les seeds terminees. Les abandons sont exclus.',
          }}
        />

        <Ligne
          libelle="Indices demandes"
          valeur={formaterNombre(joueur.indicesDemandes)}
          infobulle="Demandes d indice, abouties ou non : un nom mal orthographie ou un refus faute de points compte quand meme."
        />
        <Ligne
          libelle="Indices obtenus"
          valeur={formaterNombre(joueur.indicesObtenus)}
          infobulle="Emplacements differents reveles. Redemander un indice connu le reaffiche sans compter double."
        />

        <Ligne
          libelle="Seeds decisives"
          valeur={formaterNombre(joueur.seedsDeterminantes)}
          infobulle="Nombre de fois ou sa seed a fixe le temps de son equipe, en etant la plus longue."
        />
        <Ligne
          libelle="Abandons"
          valeur={formaterNombre(joueur.nbAbandons)}
          alerte={joueur.nbAbandons > 0}
        />
      </dl>
    </article>
  )
}

/** Les deux chiffres qui resument un joueur, plus gros que le reste. */
function Vedette({
  libelle,
  valeur,
  bordee = false,
}: {
  libelle: string
  valeur: number
  bordee?: boolean
}) {
  return (
    <div className={`px-4 py-3 ${bordee ? 'border-bordure border-l' : ''}`}>
      {/* Chiffres proportionnels : le tabulaire est pour les colonnes a aligner. */}
      <p className="text-2xl font-semibold leading-none">{valeur}</p>
      <p className="etiquette mt-1 block">{libelle}</p>
    </div>
  )
}

function Ligne({
  libelle,
  valeur,
  detail,
  infobulle,
  alerte = false,
}: {
  libelle: string
  valeur: string
  detail?: string
  infobulle?: string
  alerte?: boolean
}) {
  return (
    <div className="flex items-baseline justify-between gap-3 py-1">
      <dt className="text-texte-doux shrink-0" title={infobulle}>
        {libelle}
      </dt>
      <dd className={`truncate text-right font-medium ${alerte ? 'text-alerte' : ''}`}>
        {valeur}
        {detail !== undefined && valeur !== ABSENT && (
          <span className="text-texte-doux block truncate text-xs font-normal" title={detail}>
            {detail}
          </span>
        )}
      </dd>
    </div>
  )
}

interface Mesure {
  libelle: string
  valeur: string
  infobulle?: string
}

/** Deux mesures qui se lisent ensemble, cote a cote plutot qu'une sous l'autre. */
function Paire({ gauche, droite }: { gauche: Mesure; droite: Mesure }) {
  return (
    <div className="border-bordure my-2 grid grid-cols-2 gap-3 border-y py-2">
      {[gauche, droite].map((mesure) => (
        <div key={mesure.libelle}>
          <dt className="etiquette block" title={mesure.infobulle}>
            {mesure.libelle}
          </dt>
          <dd className="mt-0.5 font-medium">{mesure.valeur}</dd>
        </div>
      ))}
    </div>
  )
}

interface EquipeGroupee {
  nom: string
  joueurs: StatsJoueur[]
}

/**
 * Regroupe les joueurs par equipe. L'ordre des equipes suit celui de leur joueur le plus
 * actif, celui que l'API remonte en premier.
 */
function regrouperParEquipe(stats: StatsJoueur[] | undefined): EquipeGroupee[] {
  if (stats === undefined) {
    return []
  }

  const parNom = new Map<string, StatsJoueur[]>()

  for (const joueur of stats) {
    const existants = parNom.get(joueur.equipeNom)
    if (existants === undefined) {
      parNom.set(joueur.equipeNom, [joueur])
    } else {
      existants.push(joueur)
    }
  }

  return [...parNom]
    .map(([nom, joueurs]) => ({ nom, joueurs }))
    .sort((a, b) => a.nom.localeCompare(b.nom, 'fr'))
}
