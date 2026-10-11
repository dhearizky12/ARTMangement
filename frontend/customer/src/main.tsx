import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import { initAgentTheme } from './agent/current'
import './styles/index.css'

// The colour ramp must be on :root before the first paint.
initAgentTheme()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
