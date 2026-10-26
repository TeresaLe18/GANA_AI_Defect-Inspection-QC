import { useEffect, useState } from 'react'
import { errorMessage, getInspectors } from '../api'

export default function Login({ onLogin, notify }) {
  const [inspectors, setInspectors] = useState([])
  const [inspectorId, setInspectorId] = useState('')

  useEffect(() => {
    getInspectors()
      .then((list) => { setInspectors(list); setInspectorId(String(list[0]?.inspectorId ?? '')) })
      .catch((e) => notify('error', errorMessage(e)))
  }, [notify])

  const submit = (e) => {
    e.preventDefault()
    const inspector = inspectors.find((i) => i.inspectorId === Number(inspectorId))
    if (inspector) onLogin(inspector)
  }

  return (
    <form className="panel login" onSubmit={submit}>
      <h2>Sign in</h2>
      <p className="muted">Choose your inspector account to start a QC session.</p>
      <label>Inspector
        <select value={inspectorId} onChange={(e) => setInspectorId(e.target.value)}>
          {inspectors.map((i) => <option key={i.inspectorId} value={i.inspectorId}>{i.inspectorName} ({i.department})</option>)}
        </select>
      </label>
      <button className="primary" type="submit" disabled={!inspectorId}>Sign in</button>
    </form>
  )
}
