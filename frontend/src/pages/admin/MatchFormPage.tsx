import { useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import type { UseMutationResult } from '@tanstack/react-query'
import { useCreerMatch, useEquipes, useJeux, useMatch, useModifierMatch } from '../../api/hooks'
import type { Equipe, Jeu, MatchDetail, MatchUpsert, TypeMatch } from '../../api/types'
import { TYPES_MATCH } from '../../api/types'
import { Chargement, Erreur, Vide } from '../../components/Etats'
import { dateDuJourIso, formaterPourcent, formaterTemps } from '../../lib/format'
import {
  MAX_PARTICIPANTS,
  MIN_PARTICIPANTS,
  PENALITE_ABANDON_SECS,
  calculerApercu,
  tempsEffectif,
  validerLignes,
  versResultats,
} from './apercuMatch'
import type { Apercu, LigneSaisie } from './apercuMatch'

/** Valeurs de depart du formulaire : celles d un match existant, ou un brouillon vide. */
interface ValeursInitiales {
  date: string
  type: TypeMatch
  equipeAId: number | null
  equipeBId: number | null
  lignes: LigneSaisie[]
}

type Enregistrement = UseMutationResult<MatchDetail, Error, MatchUpsert>

export function MatchFormPage() {
  const { id } = useParams<{ id: string }>()
  const matchId = id === undefined ? undefined : Number(id)
  const enEdition = matchId !== undefined

  const { data: equipes, isPending: equipesEnCours, error: erreurEquipes } = useEquipes()
  const { data: jeux, isPending: jeuxEnCours } = useJeux()
  const { data: matchExistant, isPending: matchEnCours, error: erreurMatch } = useMatch(matchId)

  const creer = useCreerMatch()
  const modifier = useModifierMatch(matchId ?? 0)

  const erreur = erreurEquipes ?? erreurMatch
  if (erreur !== null) {
    return <Erreur erreur={erreur} />
  }

  if (equipesEnCours || jeuxEnCours || (enEdition && matchEnCours)) {
    return <Chargement />
  }

  if (equipes === undefined || equipes.length < 2) {
    return (
      <Vide>
        Il faut au moins deux equipes pour saisir un match. Cree-les depuis l onglet
        « Joueurs & equipes ».
      </Vide>
    )
  }

  const initiales =
    matchExistant === undefined ? brouillonVide() : depuisMatch(matchExistant, equipes)

  return (
    <FormulaireMatch
      // Remonter le formulaire quand on passe d un match a un autre evite de synchroniser
      // l etat de saisie avec les donnees chargees.
      key={matchExistant?.id ?? 'nouveau'}
      matchId={matchId}
      equipes={equipes}
      jeux={jeux ?? []}
      initiales={initiales}
      enregistrement={enEdition ? modifier : creer}
    />
  )
}

function FormulaireMatch({
  matchId,
  equipes,
  jeux,
  initiales,
  enregistrement,
}: {
  matchId: number | undefined
  equipes: Equipe[]
  jeux: Jeu[]
  initiales: ValeursInitiales
  enregistrement: Enregistrement
}) {
  const naviguer = useNavigate()
  const enEdition = matchId !== undefined

  const [date, setDate] = useState(initiales.date)
  const [type, setType] = useState(initiales.type)
  const [equipeAId, setEquipeAId] = useState(initiales.equipeAId)
  const [equipeBId, setEquipeBId] = useState(initiales.equipeBId)
  const [lignes, setLignes] = useState(initiales.lignes)

  const apercu = useMemo(() => calculerApercu(lignes), [lignes])
  const problemes = useMemo(() => validerLignes(lignes), [lignes])

  // Un match peut etre enregistre des que les deux equipes sont choisies : les resultats
  // se completent ensuite. Seules les erreurs de saisie bloquent.
  const pret = problemes.length === 0 && equipeAId !== null && equipeBId !== null

  /** Changer d equipe regenere les lignes depuis les rosters des deux equipes. */
  function choisirEquipe(cote: 'A' | 'B', nouvelId: number | null) {
    const idA = cote === 'A' ? nouvelId : equipeAId
    const idB = cote === 'B' ? nouvelId : equipeBId

    setEquipeAId(idA)
    setEquipeBId(idB)
    setLignes(construireLignes(equipes, idA, idB, lignes))
  }

  function majLigne(joueurId: number, champs: Partial<LigneSaisie>) {
    setLignes((precedentes) =>
      precedentes.map((ligne) => (ligne.joueurId === joueurId ? { ...ligne, ...champs } : ligne)),
    )
  }

  async function soumettre(evenement: React.FormEvent) {
    evenement.preventDefault()

    const resultats = versResultats(lignes)
    if (resultats === null || equipeAId === null || equipeBId === null) {
      return
    }

    const enregistre = await enregistrement.mutateAsync({
      date,
      type,
      equipeAId,
      equipeBId,
      resultats,
    })

    void naviguer(`/matchs/${enregistre.id}`)
  }

  // Les lignes sont presentees equipe par equipe, dans l ordre des deux selecteurs.
  const parEquipe = [equipeAId, equipeBId]
    .filter((id): id is number => id !== null)
    .map((equipeId) => ({
      equipeId,
      nom: equipes.find((equipe) => equipe.id === equipeId)?.nom ?? '',
      lignes: lignes.filter((ligne) => ligne.equipeId === equipeId),
    }))
    .filter((groupe) => groupe.lignes.length > 0)

  return (
    <form onSubmit={(evenement) => void soumettre(evenement)}>
      <header className="mb-4">
        <h1 className="text-xl font-semibold">
          {enEdition ? `Corriger le match ${matchId}` : 'Saisir un match'}
        </h1>
        <p className="text-texte-doux mt-1 text-sm">
          Choisis les deux equipes et enregistre : les jeux, seeds et temps peuvent etre
          completes plus tard. Coche les participants ({MIN_PARTICIPANTS} par equipe en
          qualification, 3 en demi-finale, {MAX_PARTICIPANTS} en finale). Pour un abandon,
          saisis le temps atteint et coche la case : une heure de penalite sera ajoutee.
        </p>
      </header>

      <div className="panneau mb-4 flex flex-wrap items-end gap-4 p-4">
        <label>
          <span className="etiquette mb-1 block">Date</span>
          <input
            type="date"
            className="champ"
            value={date}
            onChange={(evenement) => setDate(evenement.target.value)}
            required
          />
        </label>

        <label>
          <span className="etiquette mb-1 block">Type</span>
          <select
            className="champ"
            value={type}
            onChange={(evenement) => setType(evenement.target.value as TypeMatch)}
          >
            {TYPES_MATCH.map((valeur) => (
              <option key={valeur} value={valeur}>
                {valeur}
              </option>
            ))}
          </select>
        </label>

        <ChoixEquipe
          libelle="Equipe A"
          equipes={equipes}
          valeur={equipeAId}
          exclure={equipeBId}
          onChange={(nouvelId) => choisirEquipe('A', nouvelId)}
        />

        <ChoixEquipe
          libelle="Equipe B"
          equipes={equipes}
          valeur={equipeBId}
          exclure={equipeAId}
          onChange={(nouvelId) => choisirEquipe('B', nouvelId)}
        />
      </div>

      {parEquipe.length === 0 ? (
        <Vide>
          Choisis les deux equipes. Tu peux enregistrer le match tout de suite et saisir les
          resultats plus tard.
        </Vide>
      ) : (
        <>
          <div className="mb-4 space-y-4">
            {parEquipe.map((groupe) => (
              <div key={groupe.equipeId} className="panneau overflow-x-auto">
                <div className="border-bordure flex items-baseline justify-between border-b px-3 py-2">
                  <h2 className="font-medium">{groupe.nom}</h2>
                  <span className="text-texte-doux text-xs">
                    {groupe.lignes.filter((ligne) => ligne.participe).length} participant(s)
                  </span>
                </div>

                <table className="tableau">
                  <thead>
                    <tr>
                      <th className="w-12">Joue</th>
                      <th>Joueur</th>
                      <th>Jeu</th>
                      <th>Seed</th>
                      <th className="num">Checks trouves</th>
                      <th className="num">Total du jeu</th>
                      <th>Temps</th>
                      <th className="w-16">Abandon</th>
                      <th className="num">Retenu</th>
                    </tr>
                  </thead>
                  <tbody>
                    {groupe.lignes.map((ligne) => (
                      <tr key={ligne.joueurId} className={ligne.participe ? undefined : 'opacity-40'}>
                        <td>
                          <input
                            type="checkbox"
                            checked={ligne.participe}
                            aria-label={`${ligne.joueurNom} participe`}
                            onChange={(evenement) =>
                              majLigne(ligne.joueurId, { participe: evenement.target.checked })
                            }
                          />
                        </td>
                        <td className="font-medium whitespace-nowrap">{ligne.joueurNom}</td>
                        <td>
                          <select
                            className="champ w-44"
                            value={ligne.jeuId ?? ''}
                            disabled={!ligne.participe}
                            onChange={(evenement) =>
                              majLigne(ligne.joueurId, {
                                jeuId:
                                  evenement.target.value === ''
                                    ? null
                                    : Number(evenement.target.value),
                              })
                            }
                          >
                            <option value="">Choisir...</option>
                            {jeux.map((jeu) => (
                              <option key={jeu.id} value={jeu.id}>
                                {jeu.nom}
                              </option>
                            ))}
                          </select>
                        </td>
                        <td>
                          <input
                            className="champ w-28"
                            value={ligne.seed}
                            placeholder="optionnel"
                            disabled={!ligne.participe}
                            onChange={(evenement) =>
                              majLigne(ligne.joueurId, { seed: evenement.target.value })
                            }
                          />
                        </td>
                        <td className="num">
                          <input
                            className="champ w-20 text-right"
                            inputMode="numeric"
                            value={ligne.nbChecks}
                            disabled={!ligne.participe}
                            onChange={(evenement) =>
                              majLigne(ligne.joueurId, { nbChecks: evenement.target.value })
                            }
                          />
                        </td>
                        <td className="num">
                          <input
                            className="champ w-20 text-right"
                            inputMode="numeric"
                            value={ligne.totalChecks}
                            disabled={!ligne.participe}
                            onChange={(evenement) =>
                              majLigne(ligne.joueurId, { totalChecks: evenement.target.value })
                            }
                          />
                        </td>
                        <td>
                          <input
                            className="champ w-24"
                            placeholder="a venir"
                            value={ligne.temps}
                            disabled={!ligne.participe}
                            onChange={(evenement) =>
                              majLigne(ligne.joueurId, { temps: evenement.target.value })
                            }
                          />
                        </td>
                        <td>
                          <input
                            type="checkbox"
                            checked={ligne.estAbandon}
                            disabled={!ligne.participe}
                            aria-label={`${ligne.joueurNom} a abandonne`}
                            onChange={(evenement) =>
                              majLigne(ligne.joueurId, { estAbandon: evenement.target.checked })
                            }
                          />
                        </td>
                        <td className="num whitespace-nowrap">
                          {ligne.participe && (
                            <>
                              <span className={ligne.estAbandon ? 'text-alerte' : undefined}>
                                {formaterTemps(tempsEffectif(ligne))}
                              </span>
                              {ligne.estAbandon && (
                                <span className="text-texte-doux block text-xs">
                                  +{formaterTemps(PENALITE_ABANDON_SECS)}
                                </span>
                              )}
                            </>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ))}
          </div>

          <ApercuClassement apercu={apercu} />
        </>
      )}

      {problemes.length > 0 && (
        <div className="border-alerte text-alerte my-3 rounded border px-3 py-2 text-sm">
          <ul className="list-inside list-disc space-y-1">
            {problemes.map((probleme) => (
              <li key={probleme}>{probleme}</li>
            ))}
          </ul>
        </div>
      )}

      {enregistrement.error !== null && <Erreur erreur={enregistrement.error} />}

      <div className="mt-4 flex items-center gap-3">
        <button type="submit" className="bouton" disabled={!pret || enregistrement.isPending}>
          {enregistrement.isPending
            ? 'Enregistrement...'
            : enEdition
              ? 'Enregistrer'
              : 'Creer le match'}
        </button>
        <button type="button" className="bouton-discret" onClick={() => void naviguer(-1)}>
          Annuler
        </button>
      </div>
    </form>
  )
}

function ChoixEquipe({
  libelle,
  equipes,
  valeur,
  exclure,
  onChange,
}: {
  libelle: string
  equipes: Equipe[]
  valeur: number | null
  exclure: number | null
  onChange: (id: number | null) => void
}) {
  return (
    <label>
      <span className="etiquette mb-1 block">{libelle}</span>
      <select
        className="champ w-48"
        value={valeur ?? ''}
        onChange={(evenement) =>
          onChange(evenement.target.value === '' ? null : Number(evenement.target.value))
        }
        required
      >
        <option value="">Choisir...</option>
        {equipes
          .filter((equipe) => equipe.id !== exclure)
          .map((equipe) => (
            <option key={equipe.id} value={equipe.id}>
              {equipe.nom}
            </option>
          ))}
      </select>
    </label>
  )
}

function ApercuClassement({ apercu }: { apercu: Apercu }) {
  if (apercu.equipes.length === 0) {
    return null
  }

  return (
    <div className="panneau p-4">
      <div className="mb-3 flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="etiquette">Apercu du classement</h2>
        {apercu.estComplet ? (
          <span className="text-accent text-xs">match complet</span>
        ) : (
          <span className="text-texte-doux text-xs">
            match en cours, hors classement general
          </span>
        )}
      </div>

      {apercu.manquants.length > 0 && (
        <ul className="text-texte-doux mb-3 list-inside list-disc space-y-1 text-xs">
          {apercu.manquants.map((manquant) => (
            <li key={manquant}>{manquant}</li>
          ))}
        </ul>
      )}

      <div className="grid gap-3 sm:grid-cols-2">
        {apercu.equipes.map((equipe) => (
          <div
            key={equipe.equipeId}
            className={`rounded border px-3 py-2 ${
              equipe.estGagnante ? 'border-accent' : 'border-bordure'
            }`}
          >
            <div className="flex items-baseline justify-between gap-2">
              <span className="font-medium">{equipe.equipeNom}</span>
              <span className="text-lg font-semibold tabular-nums">
                {formaterTemps(equipe.totalSecs)}
              </span>
            </div>
            <p className="text-texte-doux mt-1 text-xs">
              {equipe.estGagnante
                ? 'gagnante'
                : equipe.nbResultatsEnAttente > 0
                  ? `${equipe.nbResultatsEnAttente} temps a saisir`
                  : `position ${equipe.position}`}
              {' · '}
              {equipe.checksTrouves} checks
              {equipe.totalChecks !== null && ` (${formaterPourcent(equipe.pourcentComplete)})`}
            </p>
            {equipe.nbAbandons > 0 && (
              <p className="text-alerte mt-1 text-xs">
                {formaterTemps(equipe.totalBrutSecs)} + {formaterTemps(equipe.penaliteSecs)} de
                penalite ({equipe.nbAbandons} abandon{equipe.nbAbandons > 1 ? 's' : ''})
              </p>
            )}
          </div>
        ))}
      </div>
    </div>
  )
}

function brouillonVide(): ValeursInitiales {
  return {
    date: dateDuJourIso(),
    // La phase de qualification represente la grande majorite des matchs saisis.
    type: 'QUALIFICATION',
    equipeAId: null,
    equipeBId: null,
    lignes: [],
  }
}

function depuisMatch(match: MatchDetail, equipes: Equipe[]): ValeursInitiales {
  const ids = match.equipes.map((equipe) => equipe.equipeId).filter((id) => id !== null)

  // Le roster complet est affiche, y compris les membres qui n ont pas joue ce match : cocher
  // un remplacant doit rester possible en correction.
  const lignes = ids.flatMap((equipeId) => {
    const equipe = equipes.find((e) => e.id === equipeId)
    const resultat = match.equipes.find((e) => e.equipeId === equipeId)
    const roster = equipe?.membres ?? resultat?.lignes.map((l) => ({ id: l.joueurId, nom: l.joueurNom })) ?? []

    return roster.map((joueur) => {
      const jouee = resultat?.lignes.find((ligne) => ligne.joueurId === joueur.id)

      return {
        joueurId: joueur.id,
        joueurNom: joueur.nom,
        equipeId,
        equipeNom: equipe?.nom ?? resultat?.equipeNom ?? '',
        participe: jouee !== undefined,
        estAbandon: jouee?.estAbandon ?? false,
        jeuId: jouee?.jeuId ?? null,
        seed: jouee?.seed ?? '',
        totalChecks: jouee?.totalChecks == null ? '' : String(jouee.totalChecks),
        nbChecks: jouee?.nbChecks == null ? '' : String(jouee.nbChecks),
        temps: jouee === undefined ? '' : formaterTemps(jouee.tempsFinalSecs),
      }
    })
  })

  return {
    date: match.date.split('T')[0],
    type: match.type,
    equipeAId: ids[0] ?? null,
    equipeBId: ids[1] ?? null,
    lignes,
  }
}

/**
 * Une ligne par membre des deux rosters, en conservant la saisie deja faite pour les joueurs
 * qui restent dans le match. Les deux premiers de chaque roster sont coches par defaut, le
 * format qualification etant le plus courant.
 */
function construireLignes(
  equipes: Equipe[],
  equipeAId: number | null,
  equipeBId: number | null,
  dejaSaisies: readonly LigneSaisie[],
): LigneSaisie[] {
  const equipeA = equipes.find((equipe) => equipe.id === equipeAId)
  const equipeB = equipes.find((equipe) => equipe.id === equipeBId)

  if (equipeA === undefined || equipeB === undefined) {
    return []
  }

  return [equipeA, equipeB]
    .flatMap((equipe) =>
      equipe.membres.map((joueur, rang) => ({
        joueurId: joueur.id,
        joueurNom: joueur.nom,
        equipeId: equipe.id,
        equipeNom: equipe.nom,
        participe: rang < MIN_PARTICIPANTS,
        estAbandon: false,
        jeuId: null,
        seed: '',
        totalChecks: '',
        nbChecks: '',
        temps: '',
      })),
    )
    .map((attendue) => {
      const precedente = dejaSaisies.find((ligne) => ligne.joueurId === attendue.joueurId)
      return precedente === undefined ? attendue : { ...precedente, equipeNom: attendue.equipeNom }
    })
}
