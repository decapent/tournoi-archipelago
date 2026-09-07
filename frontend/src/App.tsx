import { Route, Routes } from 'react-router-dom'
import { Layout } from './components/Layout'
import { ProtectedRoute } from './auth/ProtectedRoute'
import { ClassementPage } from './pages/ClassementPage'
import { StatsJeuxPage } from './pages/StatsJeuxPage'
import { MatchsPage } from './pages/MatchsPage'
import { MatchDetailPage } from './pages/MatchDetailPage'
import { LoginPage } from './pages/admin/LoginPage'
import { MatchFormPage } from './pages/admin/MatchFormPage'
import { ReferentielPage } from './pages/admin/ReferentielPage'

export function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<ClassementPage />} />
        <Route path="jeux" element={<StatsJeuxPage />} />
        <Route path="matchs" element={<MatchsPage />} />
        <Route path="matchs/:id" element={<MatchDetailPage />} />

        <Route path="admin/connexion" element={<LoginPage />} />

        <Route path="admin" element={<ProtectedRoute />}>
          <Route path="matchs/nouveau" element={<MatchFormPage />} />
          <Route path="matchs/:id" element={<MatchFormPage />} />
          <Route path="referentiel" element={<ReferentielPage />} />
        </Route>

        <Route path="*" element={<p className="text-texte-doux py-6">Page introuvable.</p>} />
      </Route>
    </Routes>
  )
}
