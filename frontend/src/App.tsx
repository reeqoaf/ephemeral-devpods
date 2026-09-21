import { Routes, Route } from 'react-router'
import { RequireAuth } from './auth/RequireAuth'
import { Account } from './pages/Account'
import { Dashboard } from './pages/Dashboard'
import { Login } from './pages/Login'
import { NewEnvironment } from './pages/NewEnvironment'

function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route element={<RequireAuth />}>
        <Route path="/" element={<Dashboard />} />
        <Route path="/new" element={<NewEnvironment />} />
        <Route path="/settings" element={<Account />} />
      </Route>
    </Routes>
  )
}

export default App
