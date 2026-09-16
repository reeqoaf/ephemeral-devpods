import { Routes, Route } from 'react-router'
import { Dashboard } from './pages/Dashboard'
import { NewEnvironment } from './pages/NewEnvironment'

function App() {
  return (
    <Routes>
      <Route path="/" element={<Dashboard />} />
      <Route path="/new" element={<NewEnvironment />} />
    </Routes>
  )
}

export default App
