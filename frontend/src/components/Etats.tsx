import type { ReactNode } from 'react'
import { ErreurApi } from '../api/client'

/** Bandeau d'attente affiche pendant un chargement. */
export function Chargement({ libelle = 'Chargement...' }: { libelle?: string }) {
  return <p className="text-texte-doux py-6 text-sm">{libelle}</p>
}

/** Bandeau d'erreur. Detaille les messages de validation quand l'API en fournit. */
export function Erreur({ erreur }: { erreur: unknown }) {
  const messages = erreur instanceof ErreurApi ? erreur.messages : [decrire(erreur)]

  return (
    <div className="border-alerte text-alerte my-3 rounded border px-3 py-2 text-sm" role="alert">
      {messages.length === 1 ? (
        messages[0]
      ) : (
        <ul className="list-inside list-disc space-y-1">
          {messages.map((message) => (
            <li key={message}>{message}</li>
          ))}
        </ul>
      )}
    </div>
  )
}

/** Message affiche quand une liste est vide. */
export function Vide({ children }: { children: ReactNode }) {
  return <p className="text-texte-doux py-6 text-sm">{children}</p>
}

function decrire(erreur: unknown): string {
  return erreur instanceof Error ? erreur.message : 'Une erreur inattendue est survenue.'
}
