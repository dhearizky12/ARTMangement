import { Route, Routes } from 'react-router-dom'
import { AppLayout } from './routes/AppLayout'
import { NotFound } from './routes/NotFound'
import { agent } from './agent/current'

export function App() {
  return (
    <Routes>
      <Route element={<AppLayout withNav />}>
        <Route index element={<h1 className="p-20 text-24 font-bold text-pri">{agent.appName}</h1>} />
      </Route>

      <Route path="*" element={<NotFound />} />
    </Routes>
  )
}
