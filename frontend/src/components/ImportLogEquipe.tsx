import { useState, type ChangeEvent } from 'react'
import { useAnalyserLog, useImporterLog } from '../api/hooks'
import type { EquipeResultat, JoueurLog, RapportLog } from '../api/types'
import { Erreur } from './Etats'
import { formaterTemps } from '../lib/format'

/** Choix « ne pas importer ce pseudonyme », valeur du select. */
const IGNORE = ''

/**
 * Televersement d'un journal Archipelago pour une equipe d'un match.
 *
 * Le journal est d'abord analyse sans rien enregistrer : on en tire les pseudonymes, leur jeu
 * et leurs checks, ce qui permet de proposer le rapprochement avec les joueurs deja saisis
 * avant de confirmer.
 */
export function ImportLogEquipe({
  matchId,
  equipe,
  onFerme,
}: {
  matchId: number
  equipe: EquipeResultat
  onFerme: () => void
}) {
  const [contenu, setContenu] = useState<string | null>(null)
  const [nomFichier, setNomFichier] = useState<string | null>(null)
  const [erreurFichier, setErreurFichier] = useState<string | null>(null)
  const [depart, setDepart] = useState('')
  const [correspondances, setCorrespondances] = useState<Record<string, string>>({})

  const analyse = useAnalyserLog()
  const importer = useImporterLog(matchId, equipe.equipeId ?? 0)

  const rapport = analyse.data ?? null

  async function choisirFichier(evenement: ChangeEvent<HTMLInputElement>) {
    const fichier = evenement.target.files?.[0]
    if (fichier === undefined) {
      return
    }

    setErreurFichier(null)
    analyse.reset()
    importer.reset()

    const texte = await fichier.text()
    setContenu(texte)
    setNomFichier(fichier.name)

    const lu = await analyse.mutateAsync(texte).catch(() => null)
    if (lu === null) {
      return
    }

    // Le premier check approxime le depart bien mieux que l'ouverture du serveur.
    const estime = lu.departEstime ?? lu.debut
    setDepart(estime === null ? '' : estime.slice(0, 16))
    setCorrespondances(proposerCorrespondances(lu, equipe))
  }

  const affectes = Object.values(correspondances).filter((valeur) => valeur !== IGNORE)
  const doublon = affectes.length !== new Set(affectes).size

  async function confirmer() {
    if (contenu === null) {
      return
    }

    await importer.mutateAsync({
      contenu,
      // L'API attend une seconde : le champ du navigateur s'arrete a la minute.
      departCourse: `${depart}:00`,
      correspondances: Object.entries(correspondances)
        .filter(([, joueurId]) => joueurId !== IGNORE)
        .map(([alias, joueurId]) => ({ alias, joueurId: Number(joueurId) })),
    })

    onFerme()
  }

  return (
    <article className="panneau mt-3 p-4">
      <header className="mb-3 flex items-start justify-between gap-3">
        <div>
          <h3 className="font-semibold">Journal de {equipe.equipeNom}</h3>
          <p className="text-texte-doux mt-0.5 text-xs">
            Les checks, la taille du monde et les temps remplacent ce qui est saisi. Le jeu et
            le seed de chaque joueur doivent deja etre enregistres.
          </p>
        </div>

        <button type="button" className="bouton-discret" onClick={onFerme}>
          Fermer
        </button>
      </header>

      <label className="block text-sm">
        <span className="etiquette block pb-1">Fichier de journal</span>
        <input
          type="file"
          accept=".txt,.log,text/plain"
          className="champ w-full"
          onChange={(evenement) => {
            void choisirFichier(evenement).catch(() =>
              setErreurFichier('Impossible de lire ce fichier.'),
            )
          }}
        />
      </label>

      {nomFichier !== null && (
        <p className="text-texte-doux mt-1 text-xs">
          {nomFichier}
          {analyse.isPending && ' — analyse en cours...'}
        </p>
      )}

      {erreurFichier !== null && <Erreur erreur={new Error(erreurFichier)} />}
      {analyse.error !== null && <Erreur erreur={analyse.error} />}
      {importer.error !== null && <Erreur erreur={importer.error} />}

      {rapport !== null && (
        <>
          <p className="text-texte-doux mt-3 text-xs">
            {rapport.lignesLues} lignes lues, {rapport.joueurs.length} joueurs reconnus,{' '}
            {rapport.signaux.length} types de signaux.
          </p>

          <label className="mt-3 block text-sm">
            <span className="etiquette block pb-1">Depart de la course</span>
            <input
              type="datetime-local"
              step={60}
              className="champ"
              value={depart}
              onChange={(evenement) => setDepart(evenement.target.value)}
            />
            <span className="text-texte-doux mt-1 block text-xs">
              Commun a toute l'equipe : c'est de lui que se compte le temps de chacun.
            </span>
            <span className="text-texte-doux mt-1 block text-xs">
              {decrireEstimation(rapport)}
            </span>
          </label>

          <table className="tableau mt-3">
            <thead>
              <tr>
                <th>Pseudonyme</th>
                <th>Jeu du journal</th>
                <th className="num">Checks</th>
                <th className="num">Fin</th>
                <th>Joueur</th>
              </tr>
            </thead>
            <tbody>
              {rapport.joueurs.map((joueur) => (
                <tr key={joueur.alias}>
                  <td className="font-medium">{joueur.alias}</td>
                  <td>{joueur.jeu ?? '—'}</td>
                  <td className="num">
                    {joueur.checksTrouves}
                    {joueur.totalChecks !== null && (
                      <span className="text-texte-doux"> / {joueur.totalChecks}</span>
                    )}
                  </td>
                  <td className={`num ${joueur.estAbandon ? 'text-alerte' : ''}`}>
                    {joueur.estAbandon ? 'abandon' : 'termine'}
                    <span className="text-texte-doux block text-xs">
                      {formaterHeure(joueur.objectif ?? joueur.dernierCheck)}
                    </span>
                  </td>
                  <td>
                    <select
                      className="champ w-full"
                      value={correspondances[joueur.alias] ?? IGNORE}
                      onChange={(evenement) =>
                        setCorrespondances((actuelles) => ({
                          ...actuelles,
                          [joueur.alias]: evenement.target.value,
                        }))
                      }
                    >
                      <option value={IGNORE}>— ne pas importer —</option>
                      {equipe.lignes.map((ligne) => (
                        <option key={ligne.joueurId} value={String(ligne.joueurId)}>
                          {ligne.joueurNom} ({ligne.jeuNom})
                        </option>
                      ))}
                    </select>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {equipe.lignes.length === 0 && (
            <p className="text-alerte mt-2 text-xs">
              Aucun resultat n'est saisi pour cette equipe : enregistre d'abord le jeu et le
              seed de chaque joueur.
            </p>
          )}

          {doublon && (
            <p className="text-alerte mt-2 text-xs">
              Un meme joueur est associe a deux pseudonymes.
            </p>
          )}

          <div className="mt-4 flex items-center gap-2">
            <button
              type="button"
              className="bouton"
              disabled={
                importer.isPending || depart === '' || affectes.length === 0 || doublon
              }
              onClick={() => void confirmer()}
            >
              {importer.isPending ? 'Import...' : `Importer ${affectes.length} joueur(s)`}
            </button>
            <span className="text-texte-doux text-xs">
              Reimporter un journal corrige remplace les valeurs, sans les cumuler.
            </span>
          </div>
        </>
      )}
    </article>
  )
}

/**
 * Explique d'ou vient le depart propose, et combien d'attente il retranche.
 *
 * Le vrai depart n'est nulle part dans le journal : les joueurs se connectent puis patientent,
 * parfois quarante minutes, que l'hote lance. Le premier check en tient lieu, a quelques
 * minutes pres — celles qu'il faut au plus rapide pour trouver sa premiere localisation.
 */
function decrireEstimation(rapport: RapportLog): string {
  if (rapport.departEstime === null || rapport.debut === null) {
    return "Aucun check dans le journal : le depart ne peut pas etre approxime."
  }

  const attente = Math.round(
    (Date.parse(rapport.departEstime) - Date.parse(rapport.debut)) / 1_000,
  )

  return (
    `Approxime au premier check, ${formaterTemps(attente)} apres l'ouverture du serveur a ` +
    `${formaterHeure(rapport.debut)}. L'attente avant le lancement de l'hote n'est donc pas ` +
    `comptee. Ajuste si tu connais l'heure exacte.`
  )
}

/**
 * Devine le joueur derriere chaque pseudonyme : d'abord par le nom, sinon par le jeu saisi.
 * Un jeu deja pris par une correspondance plus sure n'est plus propose.
 */
function proposerCorrespondances(rapport: RapportLog, equipe: EquipeResultat): Record<string, string> {
  const libres = [...equipe.lignes]
  const propositions: Record<string, string> = {}

  const retenir = (joueur: JoueurLog, index: number) => {
    propositions[joueur.alias] = String(libres[index].joueurId)
    libres.splice(index, 1)
  }

  for (const passe of ['nom', 'jeu'] as const) {
    for (const joueur of rapport.joueurs) {
      if (joueur.alias in propositions) {
        continue
      }

      const index = libres.findIndex((ligne) =>
        passe === 'nom'
          ? normaliser(ligne.joueurNom) === normaliser(joueur.alias)
          : joueur.jeu !== null && normaliser(ligne.jeuNom) === normaliser(joueur.jeu),
      )

      if (index !== -1) {
        retenir(joueur, index)
      }
    }
  }

  return propositions
}

/** Comparaison tolerante aux accents, a la casse et a la ponctuation. */
function normaliser(valeur: string): string {
  return valeur
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]/g, '')
}

/** Heure d'un horodatage ISO du journal, sans la date. */
function formaterHeure(iso: string | null): string {
  return iso === null ? '—' : iso.slice(11, 19)
}
