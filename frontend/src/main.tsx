import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router-dom'
import { App } from './App'
import { FournisseurAuth } from './auth/AuthContext'
import './index.css'

const cache = new QueryClient({
  defaultOptions: {
    queries: {
      // Les statistiques d un tournoi amical n evoluent qu a la saisie d un match.
      staleTime: 30_000,
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={cache}>
      <BrowserRouter>
        <FournisseurAuth>
          <App />
        </FournisseurAuth>
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
)
