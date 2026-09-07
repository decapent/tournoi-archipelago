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

export function ReferentielPage() {
  return (
    <section className="space-y-8">
      <header>
        <h1 className="text-xl font-semibold">Joueurs, equipes et jeux</h1>
        <p className="text-texte-doux mt-1 text-sm">
          Un joueur ne peut appartenir qu a une seule equipe : c est ce qui permet de regrouper
          automatiquement les resultats d un match par duo.
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

  const [joueur1Id, setJoueur1Id] = useState<number | null>(null)
  const [joueur2Id, setJoueur2Id] = useState<number | null>(null)

  // Un joueur deja engage dans une equipe ne peut pas en rejoindre une seconde.
  const disponibles = useMemo(() => {
    if (joueurs === undefined) {
      return []
    }

    const engages = new Set((equipes ?? []).flatMap((equipe) => [equipe.joueur1.id, equipe.joueur2.id]))
    return joueurs.filter((joueur) => !engages.has(joueur.id))
  }, [joueurs, equipes])

  async function ajouter(evenement: React.FormEvent) {
    evenement.preventDefault()
    if (joueur1Id === null || joueur2Id === null) {
      return
    }

    await creer.mutateAsync({ joueur1Id, joueur2Id })
    setJoueur1Id(null)
    setJoueur2Id(null)
  }

  return (
    <article>
      <h2 className="mb-2 font-semibold">Equipes</h2>

      <form className="mb-3 flex flex-wrap items-end gap-2" onSubmit={(evenement) => void ajouter(evenement)}>
        <ChoixJoueur
          libelle="Joueur 1"
          joueurs={disponibles}
          valeur={joueur1Id}
          exclure={joueur2Id}
          onChange={setJoueur1Id}
        />
        <ChoixJoueur
          libelle="Joueur 2"
          joueurs={disponibles}
          valeur={joueur2Id}
          exclure={joueur1Id}
          onChange={setJoueur2Id}
        />
        <button
          type="submit"
          className="bouton"
          disabled={creer.isPending || joueur1Id === null || joueur2Id === null}
        >
          Creer l equipe
        </button>
      </form>

      {disponibles.length < 2 && (
        <p className="text-texte-doux mb-3 text-xs">
          Tous les joueurs sont deja en equipe. Ajoute des joueurs pour former un nouveau duo.
        </p>
      )}

      {error !== null && <Erreur erreur={error} />}
      {creer.error !== null && <Erreur erreur={creer.error} />}
      {supprimer.error !== null && <Erreur erreur={supprimer.error} />}
      {isPending && <Chargement />}

      {equipes !== undefined &&
        (equipes.length === 0 ? (
          <Vide>Aucune equipe.</Vide>
        ) : (
          <ul className="flex flex-wrap gap-2">
            {equipes.map((equipe) => (
              <li key={equipe.id} className="panneau flex items-center gap-2 px-3 py-1.5 text-sm">
                {equipe.nom}
                <button
                  type="button"
                  className="text-texte-doux hover:text-alerte text-xs"
                  title="Supprimer"
                  onClick={() => void supprimer.mutateAsync(equipe.id)}
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

function ChoixJoueur({
  libelle,
  joueurs,
  valeur,
  exclure,
  onChange,
}: {
  libelle: string
  joueurs: { id: number; nom: string }[]
  valeur: number | null
  exclure: number | null
  onChange: (id: number | null) => void
}) {
  return (
    <label>
      <span className="etiquette mb-1 block">{libelle}</span>
      <select
        className="champ w-44"
        value={valeur ?? ''}
        onChange={(evenement) =>
          onChange(evenement.target.value === '' ? null : Number(evenement.target.value))
        }
        required
      >
        <option value="">Choisir...</option>
        {joueurs
          .filter((joueur) => joueur.id !== exclure)
          .map((joueur) => (
            <option key={joueur.id} value={joueur.id}>
              {joueur.nom}
            </option>
          ))}
      </select>
    </label>
  )
}
