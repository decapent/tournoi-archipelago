import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/contexte'

const LIENS_PUBLICS = [
  { to: '/', libelle: 'Classement' },
  { to: '/jeux', libelle: 'Stats par jeu' },
  { to: '/matchs', libelle: 'Matchs' },
]

const LIENS_ADMIN = [
  { to: '/admin/matchs/nouveau', libelle: 'Saisir un match' },
  { to: '/admin/referentiel', libelle: 'Joueurs & equipes' },
]

export function Layout() {
  const { estConnecte, username, deconnecter } = useAuth()

  return (
    <div className="min-h-screen">
      <header className="border-bordure border-b">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-x-6 gap-y-3 px-4 py-3">
          <span className="text-accent font-semibold tracking-tight">Tournoi Archipelago</span>

          <nav className="flex flex-wrap gap-1">
            {LIENS_PUBLICS.map((lien) => (
              <Lien key={lien.to} {...lien} />
            ))}
            {estConnecte && LIENS_ADMIN.map((lien) => <Lien key={lien.to} {...lien} />)}
          </nav>

          <div className="ml-auto flex items-center gap-3 text-sm">
            {estConnecte ? (
              <>
                <span className="text-texte-doux">{username}</span>
                <button type="button" className="bouton-discret" onClick={deconnecter}>
                  Deconnexion
                </button>
              </>
            ) : (
              <NavLink to="/admin/connexion" className="bouton-discret">
                Admin
              </NavLink>
            )}
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-4 py-6">
        <Outlet />
      </main>
    </div>
  )
}

function Lien({ to, libelle }: { to: string; libelle: string }) {
  return (
    <NavLink
      to={to}
      end={to === '/'}
      className={({ isActive }) =>
        `rounded px-2 py-1 text-sm transition-colors ${
          isActive ? 'text-accent bg-panneau' : 'text-texte-doux hover:text-texte'
        }`
      }
    >
      {libelle}
    </NavLink>
  )
}
