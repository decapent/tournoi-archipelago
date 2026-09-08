import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMatch, useSupprimerMatch } from '../api/hooks'
import type { EquipeResultat } from '../api/types'
import { useAuth } from '../auth/contexte'
import { Chargement, Erreur } from '../components/Etats'
import { formaterDate, formaterPourcent, formaterTemps } from '../lib/format'

export function MatchDetailPage() {
  const { id } = useParams<{ id: string }>()
  const matchId = id === undefined ? undefined : Number(id)
  const { estConnecte } = useAuth()
  const naviguer = useNavigate()

  const { data: match, isPending, error } = useMatch(matchId)
  const supprimer = useSupprimerMatch()

  async function confirmerSuppression() {
    if (matchId === undefined || !window.confirm('Supprimer ce match et ses quatre resultats ?')) {
      return
    }

    await supprimer.mutateAsync(matchId)
    void naviguer('/matchs')
  }

  if (error !== null) {
    return <Erreur erreur={error} />
  }

  if (isPending || match === undefined) {
    return <Chargement />
  }

  return (
    <section>
      <header className="mb-4 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold">Match du {formaterDate(match.date)}</h1>
          <p className="text-texte-doux mt-1 text-sm">{match.type}</p>
        </div>

        <div className="flex items-center gap-2">
          <Link to="/matchs" className="bouton-discret">
            Retour
          </Link>
          {estConnecte && (
            <>
              <Link to={`/admin/matchs/${match.id}`} className="bouton-discret">
                Corriger
              </Link>
              <button
                type="button"
                className="bouton-discret hover:border-alerte hover:text-alerte"
                onClick={() => void confirmerSuppression()}
                disabled={supprimer.isPending}
              >
                Supprimer
              </button>
            </>
          )}
        </div>
      </header>

      {supprimer.error !== null && <Erreur erreur={supprimer.error} />}

      <div className="grid gap-4 lg:grid-cols-2">
        {match.equipes.map((equipe) => (
          <CarteEquipe key={equipe.equipeId ?? equipe.equipeNom} equipe={equipe} />
        ))}
      </div>
    </section>
  )
}

function CarteEquipe({ equipe }: { equipe: EquipeResultat }) {
  return (
    <article className={`panneau p-4 ${equipe.estGagnante ? 'border-accent' : ''}`}>
      <header className="mb-3 flex items-start justify-between gap-3">
        <div>
          <h2 className="font-semibold">
            {equipe.equipeNom}
            {equipe.estGagnante && <span className="text-accent ml-2 text-xs">GAGNANTE</span>}
          </h2>
          <p className="text-texte-doux mt-0.5 text-xs">Position {equipe.position}</p>
        </div>

        <div className="text-right">
          <p className="text-lg font-semibold tabular-nums">
            {formaterTemps(equipe.tempsTotalSecs)}
          </p>
          {equipe.nbAbandons > 0 ? (
            <p className="text-alerte text-xs">
              {formaterTemps(equipe.tempsBrutSecs)} + {formaterTemps(equipe.penaliteSecs)} de
              penalite
            </p>
          ) : (
            <p className="text-texte-doux text-xs">temps total</p>
          )}
        </div>
      </header>

      <table className="tableau">
        <thead>
          <tr>
            <th>Joueur</th>
            <th>Jeu</th>
            <th className="num">Checks</th>
            <th className="num">Completion</th>
            <th className="num">Temps</th>
            <th className="num">Retenu</th>
          </tr>
        </thead>
        <tbody>
          {equipe.lignes.map((ligne) => (
            <tr key={ligne.joueurId}>
              <td className="font-medium">{ligne.joueurNom}</td>
              <td>
                {ligne.jeuNom}
                {ligne.seed !== null && (
                  <span className="text-texte-doux block text-xs">seed {ligne.seed}</span>
                )}
              </td>
              <td className="num">
                {ligne.nbChecks ?? '—'}
                {ligne.totalChecks !== null && (
                  <span className="text-texte-doux"> / {ligne.totalChecks}</span>
                )}
              </td>
              <td className="num">{formaterPourcent(ligne.pourcentComplete)}</td>
              <td className="num">{formaterTemps(ligne.tempsFinalSecs)}</td>
              <td className={`num whitespace-nowrap ${ligne.estAbandon ? 'text-alerte' : ''}`}>
                {formaterTemps(ligne.tempsEffectifSecs)}
                {ligne.estAbandon && (
                  <span className="block text-xs">abandon, +1 h</span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr>
            <td colSpan={2} className="etiquette">
              Total equipe
            </td>
            <td className="num font-semibold">
              {equipe.checksTrouves}
              {equipe.totalChecks !== null && (
                <span className="text-texte-doux"> / {equipe.totalChecks}</span>
              )}
            </td>
            <td className="num font-semibold">{formaterPourcent(equipe.pourcentComplete)}</td>
            <td className="num font-semibold">{formaterTemps(equipe.tempsBrutSecs)}</td>
            <td className="num font-semibold">{formaterTemps(equipe.tempsTotalSecs)}</td>
          </tr>
        </tfoot>
      </table>
    </article>
  )
}
