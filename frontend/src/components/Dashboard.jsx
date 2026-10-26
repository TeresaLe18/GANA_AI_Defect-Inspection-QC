import { useEffect, useState } from 'react'
import { getSummary } from '../api'

export default function Dashboard({ refreshKey }) {
  const [s, setS] = useState(null)
  const [error, setError] = useState('')

  // Polls every 5s so stock numbers stay live while other inspectors confirm reports.
  useEffect(() => {
    let alive = true
    const load = () =>
      getSummary()
        .then((d) => alive && (setS(d), setError('')))
        .catch(() => alive && setError('Cannot load dashboard. Is the API running?'))
    load()
    const t = setInterval(load, 5000)
    return () => { alive = false; clearInterval(t) }
  }, [refreshKey])

  if (error) return <p className="error">{error}</p>
  if (!s) return <p>Loading…</p>
  const maxDefect = Math.max(1, ...s.defectsByCategory.map((d) => d.count))

  return (
    <>
      <section className="cards">
        <Card label="Total stock" value={s.totalStock} />
        <Card label="Passed" value={s.passedStock} tone="ok" />
        <Card label="Failed" value={s.failedStock} tone="bad" />
        <Card label="Not yet inspected" value={s.pendingStock} />
        <Card label="Pass rate" value={s.passRate == null ? '—' : `${s.passRate}%`} />
        <Card label="Drafts awaiting confirmation" value={s.pendingReports} tone={s.pendingReports ? 'warn' : ''} />
      </section>

      <section className="panel">
        <h2>Inventory by product</h2>
        <table>
          <thead><tr><th>Product</th><th>Total</th><th>Passed</th><th>Failed</th><th>Progress</th></tr></thead>
          <tbody>
            {s.products.map((p) => {
              const pct = (n) => (p.totalStock ? (100 * n) / p.totalStock : 0)
              return (
                <tr key={p.productId}>
                  <td>{p.productName}</td><td>{p.totalStock}</td><td>{p.passedStock}</td><td>{p.failedStock}</td>
                  <td>
                    <div className="stack" title={`${p.passedStock} passed, ${p.failedStock} failed of ${p.totalStock}`}>
                      <span className="ok" style={{ width: `${pct(p.passedStock)}%` }} />
                      <span className="bad" style={{ width: `${pct(p.failedStock)}%` }} />
                    </div>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </section>

      <section className="panel">
        <h2>Confirmed inspections by defect type</h2>
        {s.defectsByCategory.length === 0 && <p className="muted">No confirmed inspections yet.</p>}
        {s.defectsByCategory.map((d) => (
          <div className="bar-row" key={d.name}>
            <span>{d.name}</span>
            <div className="bar"><span style={{ width: `${(100 * d.count) / maxDefect}%` }} /></div>
            <b>{d.count}</b>
          </div>
        ))}
      </section>
    </>
  )
}

const Card = ({ label, value, tone = '' }) => (
  <div className={`card ${tone}`}><div className="value">{value}</div><div className="label">{label}</div></div>
)
