import { useEffect, useState } from 'react'
import { getInspections, imageSrc } from '../api'

export default function History({ refreshKey, notify, onReview }) {
  const [rows, setRows] = useState([])
  const [status, setStatus] = useState('')
  const [confirmed, setConfirmed] = useState('')
  const [selected, setSelected] = useState(null)

  useEffect(() => {
    const params = {}
    if (status) params.status = status
    if (confirmed) params.confirmed = confirmed === 'yes'
    getInspections(params).then(setRows).catch(() => notify('error', 'Cannot load history.'))
  }, [status, confirmed, refreshKey, notify])

  return (
    <section className="panel">
      <h2>Inspection history</h2>
      <div className="row">
        <select value={status} onChange={(e) => setStatus(e.target.value)}>
          <option value="">All verdicts</option><option>Pass</option><option>Fail</option>
        </select>
        <select value={confirmed} onChange={(e) => setConfirmed(e.target.value)}>
          <option value="">Confirmed + drafts</option><option value="yes">Confirmed only</option><option value="no">Drafts only</option>
        </select>
      </div>
      <table>
        <thead><tr><th>#</th><th>Image</th><th>Product</th><th>Defect</th><th>Verdict</th><th>Conf.</th><th>Inspector</th><th>Date</th><th>State</th></tr></thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r.reportId} onClick={() => setSelected(r)} className="click">
              <td>{r.reportId}</td>
              <td><img className="thumb" src={imageSrc(r.imageUrl)} alt="" /></td>
              <td>{r.productName}</td>
              <td>{r.defectCategoryName}<div className="muted">{r.severity}</div></td>
              <td><span className={`badge ${r.qualityStatus}`}>{r.qualityStatus}</span></td>
              <td>{Math.round(r.confidenceScore * 100)}%</td>
              <td>{r.inspectorName}</td>
              <td>{new Date(r.createdAt + 'Z').toLocaleString()}</td>
              <td>
                {r.isUserConfirmed ? '✔ Confirmed' : (
                  <>
                    Draft{' '}
                    <a href="#review" onClick={(e) => { e.preventDefault(); e.stopPropagation(); onReview(r) }}>Review &amp; edit →</a>
                  </>
                )}
              </td>
            </tr>
          ))}
          {rows.length === 0 && <tr><td colSpan="9" className="muted">No reports found.</td></tr>}
        </tbody>
      </table>

      {selected && (
        <div className="modal" onClick={() => setSelected(null)}>
          <div className="modal-body" onClick={(e) => e.stopPropagation()}>
            <img src={imageSrc(selected.imageUrl)} alt="inspected product" />
            <h3>Report #{selected.reportId} — {selected.productName}</h3>
            <p><b>{selected.defectCategoryName}</b> ({selected.severity}) · {selected.qualityStatus}</p>
            <p>{selected.defectDescription}</p>
            <button onClick={() => setSelected(null)}>Close</button>
          </div>
        </div>
      )}
    </section>
  )
}
