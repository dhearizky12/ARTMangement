import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { App } from './App'
import { initAgentTheme } from './agent/current'
import { basePath } from './lib/basePath'
import './styles/index.css'

// The colour ramp must be on :root before the first paint.
initAgentTheme()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter basename={basePath}>
      <App />
    </BrowserRouter>
  </StrictMode>,
)
