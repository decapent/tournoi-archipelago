import { createContext, useContext } from 'react'

export interface Auth {
  /** Nom de l'admin connecte, ou `null` en mode consultation. */
  username: string | null
  estConnecte: boolean
  connecter: (username: string, password: string) => Promise<void>
  deconnecter: () => void
}

export const ContexteAuth = createContext<Auth | null>(null)

export const CLE_USERNAME = 'tournoi-archipelago.username'

export function useAuth(): Auth {
  const contexte = useContext(ContexteAuth)

  if (contexte === null) {
    throw new Error('useAuth doit etre utilise a l interieur de FournisseurAuth.')
  }

  return contexte
}
