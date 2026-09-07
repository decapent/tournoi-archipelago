import { useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { appelerApi, definirJeton, jetonCourant, surExpirationJeton } from '../api/client'
import type { SessionAdmin } from '../api/types'
import { CLE_USERNAME, ContexteAuth } from './contexte'
import type { Auth } from './contexte'

export function FournisseurAuth({ children }: { children: ReactNode }) {
  const [username, setUsername] = useState<string | null>(() =>
    jetonCourant() === null ? null : sessionStorage.getItem(CLE_USERNAME),
  )

  const deconnecter = useCallback(() => {
    definirJeton(null)
    sessionStorage.removeItem(CLE_USERNAME)
    setUsername(null)
  }, [])

  // Le client HTTP previent quand l'API rejette le jeton, pour retirer l'etat connecte.
  useEffect(() => {
    surExpirationJeton(() => {
      sessionStorage.removeItem(CLE_USERNAME)
      setUsername(null)
    })

    return () => surExpirationJeton(null)
  }, [])

  const connecter = useCallback(async (nom: string, motDePasse: string) => {
    const session = await appelerApi<SessionAdmin>('/auth/login', {
      methode: 'POST',
      corps: { username: nom, password: motDePasse },
    })

    definirJeton(session.token)
    sessionStorage.setItem(CLE_USERNAME, session.username)
    setUsername(session.username)
  }, [])

  const valeur = useMemo<Auth>(
    () => ({ username, estConnecte: username !== null, connecter, deconnecter }),
    [username, connecter, deconnecter],
  )

  return <ContexteAuth.Provider value={valeur}>{children}</ContexteAuth.Provider>
}
