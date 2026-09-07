import { useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import type { UseMutationResult } from '@tanstack/react-query'
import { useCreerMatch, useEquipes, useJeux, useMatch, useModifierMatch } from '../../api/hooks'
import type { Equipe, Jeu, MatchDetail, MatchUpsert, TypeMatch } from '../../api/types'
import { TYPES_MATCH } from '../../api/types'
import { Chargement, Erreur, Vide } from '../../components/Etats'
import { dateDuJourIso, formaterPourcent, formaterTemps } from '../../lib/format'
import { calculerApercu, validerLignes, versResultats } from './apercuMatch'
import type { ApercuEquipe, LigneSaisie } from './apercuMatch'

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
  const pret =
    lignes.length === 4 && problemes.length === 0 && equipeAId !== null && equipeBId !== null

  /** Changer d equipe regenere les quatre lignes depuis les membres des deux duos. */
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

  return (
    <form onSubmit={(evenement) => void soumettre(evenement)}>
      <header className="mb-4">
        <h1 className="text-xl font-semibold">
          {enEdition ? `Corriger le match ${matchId}` : 'Saisir un match'}
        </h1>
        <p className="text-texte-doux mt-1 text-sm">
          Un match oppose deux equipes, soit quatre resultats. Laisser le temps vide pour un
          abandon.
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

      {lignes.length === 0 ? (
        <Vide>Choisis les deux equipes pour saisir leurs resultats.</Vide>
      ) : (
        <>
          <div className="panneau mb-4 overflow-x-auto">
            <table className="tableau">
              <thead>
                <tr>
                  <th>Joueur</th>
                  <th>Equipe</th>
                  <th>Jeu</th>
                  <th>Seed</th>
                  <th className="num">Checks trouves</th>
                  <th className="num">Total du jeu</th>
                  <th>Temps</th>
                </tr>
              </thead>
              <tbody>
                {lignes.map((ligne) => (
                  <tr key={ligne.joueurId}>
                    <td className="font-medium whitespace-nowrap">{ligne.joueurNom}</td>
                    <td className="text-texte-doux text-xs whitespace-nowrap">{ligne.equipeNom}</td>
                    <td>
                      <select
                        className="champ w-44"
                        value={ligne.jeuId ?? ''}
                        onChange={(evenement) =>
                          majLigne(ligne.joueurId, {
                            jeuId:
                              evenement.target.value === '' ? null : Number(evenement.target.value),
                          })
                        }
                        required
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
                        onChange={(evenement) =>
                          majLigne(ligne.joueurId, { totalChecks: evenement.target.value })
                        }
                      />
                    </td>
                    <td>
                      <input
                        className="champ w-24"
                        placeholder="hh:mm:ss"
                        value={ligne.temps}
                        onChange={(evenement) =>
                          majLigne(ligne.joueurId, { temps: evenement.target.value })
                        }
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <Apercu apercu={apercu} />
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

function Apercu({ apercu }: { apercu: ApercuEquipe[] }) {
  if (apercu.length === 0) {
    return null
  }

  return (
    <div className="panneau p-4">
      <h2 className="etiquette mb-3">Apercu du classement</h2>

      <div className="grid gap-3 sm:grid-cols-2">
        {apercu.map((equipe) => (
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
              {equipe.estGagnante ? 'gagnante' : `position ${equipe.position}`}
              {equipe.estAbandon && ' · abandon'}
              {' · '}
              {equipe.checksTrouves} checks
              {equipe.totalChecks !== null && ` (${formaterPourcent(equipe.pourcentComplete)})`}
            </p>
          </div>
        ))}
      </div>
    </div>
  )
}

function brouillonVide(): ValeursInitiales {
  return {
    date: dateDuJourIso(),
    type: 'TOURNOI',
    equipeAId: null,
    equipeBId: null,
    lignes: [],
  }
}

function depuisMatch(match: MatchDetail, equipes: Equipe[]): ValeursInitiales {
  const ids = match.equipes.map((equipe) => equipe.equipeId).filter((id) => id !== null)

  return {
    date: match.date.split('T')[0],
    type: match.type,
    equipeAId: ids[0] ?? null,
    equipeBId: ids[1] ?? null,
    lignes: match.equipes.flatMap((resultat) =>
      resultat.lignes.map((ligne) => ({
        joueurId: ligne.joueurId,
        joueurNom: ligne.joueurNom,
        equipeId: resultat.equipeId ?? 0,
        equipeNom:
          equipes.find((equipe) => equipe.id === resultat.equipeId)?.nom ?? resultat.equipeNom,
        jeuId: ligne.jeuId,
        seed: ligne.seed ?? '',
        totalChecks: ligne.totalChecks === null ? '' : String(ligne.totalChecks),
        nbChecks: ligne.nbChecks === null ? '' : String(ligne.nbChecks),
        temps: ligne.tempsFinalSecs === null ? '' : formaterTemps(ligne.tempsFinalSecs),
      })),
    ),
  }
}

/**
 * Quatre lignes vides pour les membres des deux equipes, en conservant la saisie deja faite
 * pour les joueurs qui restent dans le match.
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
      [equipe.joueur1, equipe.joueur2].map((joueur) => ({
        joueurId: joueur.id,
        joueurNom: joueur.nom,
        equipeId: equipe.id,
        equipeNom: equipe.nom,
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
