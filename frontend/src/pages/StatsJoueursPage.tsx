import { useMemo, useState } from 'react'
import { useStatsJoueurs } from '../api/hooks'
import type { StatsJoueur, TypeMatch } from '../api/types'
import { Chargement, Erreur, Vide } from '../components/Etats'
import { FiltreType } from '../components/FiltreType'
import { formaterNombre, formaterPourcent, formaterTemps } from '../lib/format'

type Colonne = keyof Pick<
  StatsJoueur,
  | 'joueurNom'
  | 'equipeNom'
  | 'seedsJouees'
  | 'victoires'
  | 'tempsMoyenSecs'
  | 'tempsMedianSecs'
  | 'meilleurTempsSecs'
  | 'checksTrouves'
  | 'pourcentCompleteMoyen'
  | 'checksParHeure'
  | 'seedsDeterminantes'
  | 'nbAbandons'
>

const COLONNES: { cle: Colonne; libelle: string; numerique: boolean; infobulle?: string }[] = [
  { cle: 'joueurNom', libelle: 'Joueur', numerique: false },
  { cle: 'equipeNom', libelle: 'Equipe', numerique: false },
  { cle: 'seedsJouees', libelle: 'Seeds', numerique: true },
  { cle: 'victoires', libelle: 'Victoires', numerique: true },
  { cle: 'tempsMoyenSecs', libelle: 'Temps moyen', numerique: true },
  { cle: 'tempsMedianSecs', libelle: 'Temps median', numerique: true },
  { cle: 'meilleurTempsSecs', libelle: 'Meilleur temps', numerique: true },
  { cle: 'checksTrouves', libelle: 'Checks', numerique: true },
  { cle: 'pourcentCompleteMoyen', libelle: 'Completion', numerique: true },
  {
    cle: 'checksParHeure',
    libelle: 'Checks / h',
    numerique: true,
    infobulle: 'Rythme sur les seeds terminees. Les abandons sont exclus.',
  },
  {
    cle: 'seedsDeterminantes',
    libelle: 'Seeds decisives',
    numerique: true,
    infobulle:
      "Nombre de fois ou sa seed a fixe le temps de son equipe, en etant la plus longue. L'equipe a fini quand son dernier joueur a fini.",
  },
  { cle: 'nbAbandons', libelle: 'Abandons', numerique: true },
]

export function StatsJoueursPage() {
  const [type, setType] = useState<TypeMatch | undefined>(undefined)
  const [masquerSansMatch, setMasquerSansMatch] = useState(true)
  const [triColonne, setTriColonne] = useState<Colonne>('seedsJouees')
  const [triDescendant, setTriDescendant] = useState(true)

  const { data: stats, isPending, error } = useStatsJoueurs(type)

  const lignes = useMemo(() => {
    if (stats === undefined) {
      return undefined
    }

    const filtrees = masquerSansMatch
      ? stats.filter((joueur) => joueur.seedsJouees > 0)
      : [...stats]

    return filtrees.sort((a, b) => {
      const comparaison = comparer(a[triColonne], b[triColonne])
      return triDescendant ? -comparaison : comparaison
    })
  }, [stats, masquerSansMatch, triColonne, triDescendant])

  function basculerTri(colonne: Colonne) {
    if (colonne === triColonne) {
      setTriDescendant((precedent) => !precedent)
    } else {
      setTriColonne(colonne)
      // Un nom se lit de A a Z, un nombre du plus grand au plus petit.
      setTriDescendant(colonne !== 'joueurNom' && colonne !== 'equipeNom')
    }
  }

  return (
    <section>
      <header className="mb-4 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold">Statistiques par joueur</h1>
          <p className="text-texte-doux mt-1 text-sm">
            Chaque ligne agrege les seeds courues par un joueur. Comme pour le classement, un
            match dont un resultat reste a saisir est ignore.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-4">
          <FiltreType valeur={type} onChange={setType} />

          <label className="text-texte-doux flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={masquerSansMatch}
              onChange={(evenement) => setMasquerSansMatch(evenement.target.checked)}
            />
            Masquer les joueurs sans seed
          </label>
        </div>
      </header>

      {error !== null && <Erreur erreur={error} />}
      {isPending && <Chargement />}

      {lignes !== undefined &&
        (lignes.length === 0 ? (
          <Vide>Aucune seed enregistree pour ce filtre.</Vide>
        ) : (
          <div className="panneau overflow-x-auto">
            <table className="tableau">
              <thead>
                <tr>
                  {COLONNES.map((colonne) => (
                    <th key={colonne.cle} className={colonne.numerique ? 'num' : undefined}>
                      <button
                        type="button"
                        className="hover:text-accent transition-colors"
                        title={colonne.infobulle}
                        onClick={() => basculerTri(colonne.cle)}
                      >
                        {colonne.libelle}
                        {triColonne === colonne.cle && (triDescendant ? ' ↓' : ' ↑')}
                      </button>
                    </th>
                  ))}
                  <th>Meilleur jeu</th>
                </tr>
              </thead>
              <tbody>
                {lignes.map((joueur) => (
                  <tr key={joueur.joueurId}>
                    <td className="font-medium">{joueur.joueurNom}</td>
                    <td className="text-texte-doux">{joueur.equipeNom}</td>
                    <td className="num">{joueur.seedsJouees}</td>
                    <td className="num">{joueur.victoires}</td>
                    <td className="num">{formaterTemps(joueur.tempsMoyenSecs)}</td>
                    <td className="num">{formaterTemps(joueur.tempsMedianSecs)}</td>
                    <td className="num">{formaterTemps(joueur.meilleurTempsSecs)}</td>
                    <td className="num">{joueur.checksTrouves}</td>
                    <td className="num">{formaterPourcent(joueur.pourcentCompleteMoyen)}</td>
                    <td className="num">{formaterNombre(joueur.checksParHeure, 1)}</td>
                    <td className="num">{joueur.seedsDeterminantes}</td>
                    <td className="num">
                      {joueur.nbAbandons === 0 ? (
                        <span className="text-texte-doux">0</span>
                      ) : (
                        <span className="text-alerte">{joueur.nbAbandons}</span>
                      )}
                    </td>
                    <td className="text-texte-doux">{joueur.meilleurJeuNom ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ))}
    </section>
  )
}

/** Les valeurs absentes passent toujours en fin de tri, quel que soit le sens. */
function comparer(a: string | number | null, b: string | number | null): number {
  if (a === b) {
    return 0
  }
  if (a === null) {
    return -1
  }
  if (b === null) {
    return 1
  }

  return typeof a === 'string' && typeof b === 'string'
    ? b.localeCompare(a, 'fr')
    : Number(a) - Number(b)
}
