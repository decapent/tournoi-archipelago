import { useMemo, useState } from 'react'
import { useStatsJeux } from '../api/hooks'
import type { StatsJeu, TypeMatch } from '../api/types'
import { Chargement, Erreur, Vide } from '../components/Etats'
import { FiltreType } from '../components/FiltreType'
import { formaterNombre, formaterPourcent, formaterTemps } from '../lib/format'

type Colonne = keyof Pick<
  StatsJeu,
  'jeuNom' | 'nbParties' | 'tempsMoyenSecs' | 'tempsMedianSecs' | 'meilleurTempsSecs' | 'nbChecksMoyen' | 'pourcentCompleteMoyen' | 'nbAbandons'
>

const COLONNES: { cle: Colonne; libelle: string; numerique: boolean }[] = [
  { cle: 'jeuNom', libelle: 'Jeu', numerique: false },
  { cle: 'nbParties', libelle: 'Parties', numerique: true },
  { cle: 'tempsMoyenSecs', libelle: 'Temps moyen', numerique: true },
  { cle: 'tempsMedianSecs', libelle: 'Temps median', numerique: true },
  { cle: 'meilleurTempsSecs', libelle: 'Meilleur temps', numerique: true },
  { cle: 'nbChecksMoyen', libelle: 'Checks moyens', numerique: true },
  { cle: 'pourcentCompleteMoyen', libelle: 'Completion', numerique: true },
  { cle: 'nbAbandons', libelle: 'Abandons', numerique: true },
]

export function StatsJeuxPage() {
  const [type, setType] = useState<TypeMatch | undefined>(undefined)
  const [masquerNonJoues, setMasquerNonJoues] = useState(true)
  const [triColonne, setTriColonne] = useState<Colonne>('nbParties')
  const [triDescendant, setTriDescendant] = useState(true)

  const { data: stats, isPending, error } = useStatsJeux(type)

  const lignes = useMemo(() => {
    if (stats === undefined) {
      return undefined
    }

    const filtrees = masquerNonJoues ? stats.filter((jeu) => jeu.nbParties > 0) : [...stats]

    return filtrees.sort((a, b) => {
      const comparaison = comparer(a[triColonne], b[triColonne])
      return triDescendant ? -comparaison : comparaison
    })
  }, [stats, masquerNonJoues, triColonne, triDescendant])

  function basculerTri(colonne: Colonne) {
    if (colonne === triColonne) {
      setTriDescendant((precedent) => !precedent)
    } else {
      setTriColonne(colonne)
      setTriDescendant(colonne !== 'jeuNom')
    }
  }

  return (
    <section>
      <header className="mb-4 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold">Statistiques par jeu</h1>
          <p className="text-texte-doux mt-1 text-sm">
            Chaque ligne agrege toutes les parties jouees sur un jeu, tous joueurs confondus.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-4">
          <FiltreType valeur={type} onChange={setType} />

          <label className="text-texte-doux flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={masquerNonJoues}
              onChange={(evenement) => setMasquerNonJoues(evenement.target.checked)}
            />
            Masquer les jeux jamais joues
          </label>
        </div>
      </header>

      {error !== null && <Erreur erreur={error} />}
      {isPending && <Chargement />}

      {lignes !== undefined &&
        (lignes.length === 0 ? (
          <Vide>Aucune partie enregistree pour ce filtre.</Vide>
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
                        onClick={() => basculerTri(colonne.cle)}
                      >
                        {colonne.libelle}
                        {triColonne === colonne.cle && (triDescendant ? ' ↓' : ' ↑')}
                      </button>
                    </th>
                  ))}
                  <th>Meilleur joueur</th>
                </tr>
              </thead>
              <tbody>
                {lignes.map((jeu) => (
                  <tr key={jeu.jeuId}>
                    <td className="font-medium">{jeu.jeuNom}</td>
                    <td className="num">{jeu.nbParties}</td>
                    <td className="num">
                      {jeu.tempsMoyenSecs === null ? '—' : formaterTemps(jeu.tempsMoyenSecs)}
                    </td>
                    <td className="num">
                      {jeu.tempsMedianSecs === null ? '—' : formaterTemps(jeu.tempsMedianSecs)}
                    </td>
                    <td className="num">
                      {jeu.meilleurTempsSecs === null ? '—' : formaterTemps(jeu.meilleurTempsSecs)}
                    </td>
                    <td className="num">{formaterNombre(jeu.nbChecksMoyen, 1)}</td>
                    <td className="num">{formaterPourcent(jeu.pourcentCompleteMoyen)}</td>
                    <td className="num">
                      {jeu.nbAbandons === 0 ? (
                        <span className="text-texte-doux">0</span>
                      ) : (
                        <span className="text-alerte">{jeu.nbAbandons}</span>
                      )}
                    </td>
                    <td className="text-texte-doux">{jeu.meilleurJoueurNom ?? '—'}</td>
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

  return typeof a === 'string' && typeof b === 'string' ? b.localeCompare(a, 'fr') : Number(a) - Number(b)
}
