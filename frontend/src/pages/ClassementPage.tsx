import { useState } from 'react'
import { useClassement } from '../api/hooks'
import type { TriClassement, TypeMatch } from '../api/types'
import { Chargement, Erreur, Vide } from '../components/Etats'
import { FiltreType } from '../components/FiltreType'
import { formaterNombre, formaterPourcent, formaterTemps } from '../lib/format'

export function ClassementPage() {
  const [type, setType] = useState<TypeMatch | undefined>(undefined)
  const [tri, setTri] = useState<TriClassement>('Victoires')

  const { data: classement, isPending, error } = useClassement(type, tri)

  return (
    <section>
      <header className="mb-4 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold">Classement general</h1>
          <p className="text-texte-doux mt-1 text-sm">
            Le score d une equipe est la somme des temps de ses participants, chaque abandon
            comptant son temps majore d une heure. Le plus petit total gagne le match. Les
            matchs dont un temps reste a saisir n y figurent pas encore.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-4">
          <FiltreType valeur={type} onChange={setType} />

          <label className="flex items-center gap-2">
            <span className="etiquette">Trier par</span>
            <select
              className="champ"
              value={tri}
              onChange={(evenement) => setTri(evenement.target.value as TriClassement)}
            >
              <option value="Victoires">Victoires</option>
              <option value="Temps">Temps cumule</option>
            </select>
          </label>
        </div>
      </header>

      {error !== null && <Erreur erreur={error} />}
      {isPending && <Chargement />}

      {classement !== undefined &&
        (classement.length === 0 ? (
          <Vide>Aucune equipe pour l instant. Cree des joueurs et des equipes pour demarrer.</Vide>
        ) : (
          <div className="panneau overflow-x-auto">
            <table className="tableau">
              <thead>
                <tr>
                  <th className="num">#</th>
                  <th>Equipe</th>
                  <th className="num">Matchs</th>
                  <th className="num">Victoires</th>
                  <th className="num">Temps cumule</th>
                  <th className="num">Temps moyen</th>
                  <th className="num">Checks</th>
                  <th className="num">Completion</th>
                  <th className="num">Abandons</th>
                  <th className="num">Penalites</th>
                </tr>
              </thead>
              <tbody>
                {classement.map((ligne) => (
                  <tr key={ligne.equipeId}>
                    <td className="num text-texte-doux">{ligne.position}</td>
                    <td className="font-medium">{ligne.equipeNom}</td>
                    <td className="num">{ligne.matchsJoues}</td>
                    <td className="num font-semibold">{ligne.victoires}</td>
                    <td className="num">
                      {ligne.matchsJoues === 0 ? '—' : formaterTemps(ligne.tempsCumuleSecs)}
                    </td>
                    <td className="num">
                      {ligne.tempsMoyenSecs === null ? '—' : formaterTemps(ligne.tempsMoyenSecs)}
                    </td>
                    <td className="num">{formaterNombre(ligne.checksTrouves)}</td>
                    <td className="num">{formaterPourcent(ligne.pourcentCompleteMoyen)}</td>
                    <td className="num">
                      {ligne.abandons === 0 ? (
                        <span className="text-texte-doux">0</span>
                      ) : (
                        <span className="text-alerte">{ligne.abandons}</span>
                      )}
                    </td>
                    <td className="num">
                      {ligne.penaliteCumuleeSecs === 0 ? (
                        <span className="text-texte-doux">-</span>
                      ) : (
                        <span className="text-alerte">
                          +{formaterTemps(ligne.penaliteCumuleeSecs)}
                        </span>
                      )}
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
