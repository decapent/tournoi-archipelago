import { useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../../auth/contexte'
import { Erreur } from '../../components/Etats'

export function LoginPage() {
  const { estConnecte, connecter } = useAuth()
  const naviguer = useNavigate()
  const emplacement = useLocation()

  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [erreur, setErreur] = useState<unknown>(null)
  const [enCours, setEnCours] = useState(false)

  const retour = (emplacement.state as { retour?: string } | null)?.retour ?? '/admin/matchs/nouveau'

  if (estConnecte) {
    return <Navigate to={retour} replace />
  }

  async function soumettre(evenement: React.FormEvent) {
    evenement.preventDefault()
    setErreur(null)
    setEnCours(true)

    try {
      await connecter(username, password)
      void naviguer(retour, { replace: true })
    } catch (probleme) {
      setErreur(probleme)
    } finally {
      setEnCours(false)
    }
  }

  return (
    <section className="mx-auto max-w-sm">
      <h1 className="mb-1 text-xl font-semibold">Panneau d admin</h1>
      <p className="text-texte-doux mb-4 text-sm">
        La consultation des statistiques est libre. La connexion sert uniquement a saisir et
        corriger les matchs.
      </p>

      {erreur !== null && <Erreur erreur={erreur} />}

      <form className="panneau space-y-3 p-4" onSubmit={(evenement) => void soumettre(evenement)}>
        <label className="block">
          <span className="etiquette mb-1 block">Nom d utilisateur</span>
          <input
            className="champ w-full"
            autoComplete="username"
            value={username}
            onChange={(evenement) => setUsername(evenement.target.value)}
            required
          />
        </label>

        <label className="block">
          <span className="etiquette mb-1 block">Mot de passe</span>
          <input
            type="password"
            className="champ w-full"
            autoComplete="current-password"
            value={password}
            onChange={(evenement) => setPassword(evenement.target.value)}
            required
          />
        </label>

        <button type="submit" className="bouton w-full" disabled={enCours}>
          {enCours ? 'Connexion...' : 'Se connecter'}
        </button>
      </form>
    </section>
  )
}
