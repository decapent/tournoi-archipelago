import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMatchs } from '../api/hooks'
import type { TypeMatch } from '../api/types'
import { Chargement, Erreur, Vide } from '../components/Etats'
import { FiltreType } from '../components/FiltreType'
import { formaterDate } from '../lib/format'

export function MatchsPage() {
  const [type, setType] = useState<TypeMatch | undefined>(undefined)
  const [du, setDu] = useState('')
  const [au, setAu] = useState('')

  const { data: matchs, isPending, error } = useMatchs({ type, du: du || undefined, au: au || undefined })

  return (
    <section>
      <header className="mb-4 flex flex-wrap items-end justify-between gap-4">
        <h1 className="text-xl font-semibold">Historique des matchs</h1>

        <div className="flex flex-wrap items-center gap-4">
          <FiltreType valeur={type} onChange={setType} />

          <label className="flex items-center gap-2">
            <span className="etiquette">Du</span>
            <input
              type="date"
              className="champ"
              value={du}
              onChange={(evenement) => setDu(evenement.target.value)}
            />
          </label>

          <label className="flex items-center gap-2">
            <span className="etiquette">Au</span>
            <input
              type="date"
              className="champ"
              value={au}
              onChange={(evenement) => setAu(evenement.target.value)}
            />
          </label>
        </div>
      </header>

      {error !== null && <Erreur erreur={error} />}
      {isPending && <Chargement />}

      {matchs !== undefined &&
        (matchs.length === 0 ? (
          <Vide>Aucun match pour ce filtre.</Vide>
        ) : (
          <div className="panneau overflow-x-auto">
            <table className="tableau">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Type</th>
                  <th>Equipes</th>
                  <th>Gagnante</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {matchs.map((match) => (
                  <tr key={match.id}>
                    <td className="tabular-nums">{formaterDate(match.date)}</td>
                    <td className="text-texte-doux text-xs">{match.type}</td>
                    <td>{match.equipeNoms.join('  vs  ')}</td>
                    <td className="text-accent font-medium">{match.equipeGagnanteNom ?? '—'}</td>
                    <td className="text-right">
                      <Link to={`/matchs/${match.id}`} className="bouton-discret">
                        Detail
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ))}
    </section>
  )
}
