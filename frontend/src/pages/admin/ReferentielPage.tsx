import { useMemo, useState } from 'react'
import {
  useCreerEquipe,
  useCreerJeu,
  useCreerJoueur,
  useEquipes,
  useJeux,
  useJoueurs,
  useSupprimerEquipe,
  useSupprimerJeu,
  useSupprimerJoueur,
} from '../../api/hooks'
import { Chargement, Erreur, Vide } from '../../components/Etats'

/** Taille du roster imposee par l'API (EquipeService.TailleRoster). */
const TAILLE_ROSTER = 4

export function ReferentielPage() {
  return (
    <section className="space-y-8">
      <header>
        <h1 className="text-xl font-semibold">Joueurs, equipes et jeux</h1>
        <p className="text-texte-doux mt-1 text-sm">
          Chaque equipe compte quatre joueurs. Un joueur ne peut appartenir qu a une seule
          equipe : c est ce qui permet de regrouper automatiquement les resultats d un match.
        </p>
      </header>

      <SectionJoueurs />
      <SectionEquipes />
      <SectionJeux />
    </section>
  )
}

function SectionJoueurs() {
  const { data: joueurs, isPending, error } = useJoueurs()
  const creer = useCreerJoueur()
  const supprimer = useSupprimerJoueur()
  const [nom, setNom] = useState('')

  async function ajouter(evenement: React.FormEvent) {
    evenement.preventDefault()
    await creer.mutateAsync(nom.trim())
    setNom('')
  }

  return (
    <article>
      <h2 className="mb-2 font-semibold">Joueurs</h2>

      <form className="mb-3 flex gap-2" onSubmit={(evenement) => void ajouter(evenement)}>
        <input
          className="champ w-64"
          placeholder="Nom du joueur"
          value={nom}
          onChange={(evenement) => setNom(evenement.target.value)}
          required
        />
        <button type="submit" className="bouton" disabled={creer.isPending}>
          Ajouter
        </button>
      </form>

      {error !== null && <Erreur erreur={error} />}
      {creer.error !== null && <Erreur erreur={creer.error} />}
      {supprimer.error !== null && <Erreur erreur={supprimer.error} />}
      {isPending && <Chargement />}

      {joueurs !== undefined &&
        (joueurs.length === 0 ? (
          <Vide>Aucun joueur.</Vide>
        ) : (
          <ul className="flex flex-wrap gap-2">
            {joueurs.map((joueur) => (
              <li key={joueur.id} className="panneau flex items-center gap-2 px-3 py-1.5 text-sm">
                {joueur.nom}
                <button
                  type="button"
                  className="text-texte-doux hover:text-alerte text-xs"
                  title="Supprimer"
                  onClick={() => void supprimer.mutateAsync(joueur.id)}
                >
                  ✕
                </button>
              </li>
            ))}
          </ul>
        ))}
    </article>
  )
}

function SectionEquipes() {
  const { data: equipes, isPending, error } = useEquipes()
  const { data: joueurs } = useJoueurs()
  const creer = useCreerEquipe()
  const supprimer = useSupprimerEquipe()

  const [nom, setNom] = useState('')
  const [selection, setSelection] = useState<number[]>([])

  // Un joueur deja engage dans une equipe ne peut pas en rejoindre une seconde.
  const disponibles = useMemo(() => {
    if (joueurs === undefined) {
      return []
    }

    const engages = new Set((equipes ?? []).flatMap((equipe) => equipe.membres.map((m) => m.id)))
    return joueurs.filter((joueur) => !engages.has(joueur.id))
  }, [joueurs, equipes])

  const complet = selection.length === TAILLE_ROSTER

  function basculer(joueurId: number) {
    setSelection((precedente) =>
      precedente.includes(joueurId)
        ? precedente.filter((id) => id !== joueurId)
        : // On ne laisse pas depasser la taille du roster : plus clair qu un message d erreur.
          precedente.length < TAILLE_ROSTER
          ? [...precedente, joueurId]
          : precedente,
    )
  }

  async function ajouter(evenement: React.FormEvent) {
    evenement.preventDefault()
    await creer.mutateAsync({ nom: nom.trim(), joueurIds: selection })
    setNom('')
    setSelection([])
  }

  return (
    <article>
      <h2 className="mb-2 font-semibold">Equipes</h2>

      <form className="panneau mb-3 space-y-3 p-4" onSubmit={(evenement) => void ajouter(evenement)}>
        <label className="block">
          <span className="etiquette mb-1 block">Nom de l equipe</span>
          <input
            className="champ w-72"
            placeholder="Les Nous_"
            value={nom}
            onChange={(evenement) => setNom(evenement.target.value)}
            required
          />
        </label>

        <div>
          <span className="etiquette mb-1 block">
            Roster ({selection.length} / {TAILLE_ROSTER})
          </span>

          {disponibles.length === 0 ? (
            <p className="text-texte-doux text-xs">
              Tous les joueurs sont deja en equipe. Ajoute des joueurs pour former un nouveau
              roster.
            </p>
          ) : (
            <ul className="flex flex-wrap gap-2">
              {disponibles.map((joueur) => {
                const choisi = selection.includes(joueur.id)
                return (
                  <li key={joueur.id}>
                    <button
                      type="button"
                      aria-pressed={choisi}
                      className={`rounded border px-2.5 py-1 text-sm transition-colors ${
                        choisi
                          ? 'border-accent text-accent'
                          : 'border-bordure text-texte-doux hover:text-texte'
                      }`}
                      onClick={() => basculer(joueur.id)}
                    >
                      {joueur.nom}
                    </button>
                  </li>
                )
              })}
            </ul>
          )}
        </div>

        <button type="submit" className="bouton" disabled={creer.isPending || !complet}>
          Creer l equipe
        </button>
      </form>

      {error !== null && <Erreur erreur={error} />}
      {creer.error !== null && <Erreur erreur={creer.error} />}
      {supprimer.error !== null && <Erreur erreur={supprimer.error} />}
      {isPending && <Chargement />}

      {equipes !== undefined &&
        (equipes.length === 0 ? (
          <Vide>Aucune equipe.</Vide>
        ) : (
          <ul className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
            {equipes.map((equipe) => (
              <li key={equipe.id} className="panneau p-3">
                <div className="mb-1 flex items-start justify-between gap-2">
                  <span className="font-medium">{equipe.nom}</span>
                  <button
                    type="button"
                    className="text-texte-doux hover:text-alerte text-xs"
                    title="Supprimer"
                    onClick={() => void supprimer.mutateAsync(equipe.id)}
                  >
                    &#10005;
                  </button>
                </div>
                <ul className="text-texte-doux space-y-0.5 text-sm">
                  {equipe.membres.map((membre) => (
                    <li key={membre.id}>{membre.nom}</li>
                  ))}
                </ul>
              </li>
            ))}
          </ul>
        ))}
    </article>
  )
}

function SectionJeux() {
  const { data: jeux, isPending, error } = useJeux()
  const creer = useCreerJeu()
  const supprimer = useSupprimerJeu()
  const [nom, setNom] = useState('')

  async function ajouter(evenement: React.FormEvent) {
    evenement.preventDefault()
    await creer.mutateAsync(nom.trim())
    setNom('')
  }

  return (
    <article>
      <h2 className="mb-2 font-semibold">Jeux</h2>

      <form className="mb-3 flex gap-2" onSubmit={(evenement) => void ajouter(evenement)}>
        <input
          className="champ w-64"
          placeholder="Nom du jeu"
          value={nom}
          onChange={(evenement) => setNom(evenement.target.value)}
          required
        />
        <button type="submit" className="bouton" disabled={creer.isPending}>
          Ajouter
        </button>
      </form>

      {error !== null && <Erreur erreur={error} />}
      {creer.error !== null && <Erreur erreur={creer.error} />}
      {supprimer.error !== null && <Erreur erreur={supprimer.error} />}
      {isPending && <Chargement />}

      {jeux !== undefined && (
        <ul className="flex flex-wrap gap-2">
          {jeux.map((jeu) => (
            <li key={jeu.id} className="panneau flex items-center gap-2 px-3 py-1.5 text-sm">
              {jeu.nom}
              <button
                type="button"
                className="text-texte-doux hover:text-alerte text-xs"
                title="Supprimer"
                onClick={() => void supprimer.mutateAsync(jeu.id)}
              >
                ✕
              </button>
            </li>
          ))}
        </ul>
      )}
    </article>
  )
}
