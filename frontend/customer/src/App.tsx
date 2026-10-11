import { agent } from './agent/current'

export default function App() {
  return <h1 className="p-20 text-24 font-bold text-pri">{agent.appName}</h1>
}
