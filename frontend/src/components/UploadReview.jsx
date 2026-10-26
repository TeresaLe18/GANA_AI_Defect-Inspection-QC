import { useEffect, useState } from 'react'
import {
  analyze, confirmReport, discardInspection, errorMessage, getCategories, getProducts, imageSrc,
} from '../api'

const MAX_MB = 10

// Editable form values for a draft report (from /analyze or reopened from History).
const toForm = (r) => ({
  defectCategoryId: r.defectCategoryId,
  qualityStatus: r.qualityStatus,
  defectDescription: r.defectDescription ?? '',
  quantity: 1,
})

export default function UploadReview({ notify, onConfirmed, resume, onResumed, inspector }) {
  const [products, setProducts] = useState([])
  const [categories, setCategories] = useState([])
  const [productId, setProductId] = useState(resume ? String(resume.productId) : '')
  const inspectorId = inspector.inspectorId
  const [file, setFile] = useState(null)
  const [preview, setPreview] = useState(null)
  const [busy, setBusy] = useState(false)
  const [unclear, setUnclear] = useState('')
  const [report, setReport] = useState(resume ?? null)   // AI draft returned by /analyze, or reopened from History
  const [form, setForm] = useState(resume ? toForm(resume) : null) // editable copy reviewed by QC

  useEffect(() => { if (resume) { onResumed(); notify('info', `Reviewing draft #${resume.reportId}.`) } }, []) // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    Promise.all([getProducts(), getCategories()])
      .then(([p, c]) => {
        setProducts(p); setCategories(c)
        setProductId((cur) => cur || String(p[0]?.productId ?? ''))
      })
      .catch((e) => notify('error', errorMessage(e)))
  }, [notify])

  useEffect(() => {
    if (!file) { setPreview(null); return }
    const url = URL.createObjectURL(file)
    setPreview(url)
    return () => URL.revokeObjectURL(url)
  }, [file])

  const pickFile = (f) => {
    setUnclear('')
    if (!f) return setFile(null)
    if (!/^image\/(jpeg|png|webp)$/.test(f.type)) return notify('error', 'Please choose a JPG, PNG or WebP image.')
    if (f.size > MAX_MB * 1024 * 1024) return notify('error', `Image must be smaller than ${MAX_MB} MB.`)
    setFile(f)
  }

  const reset = () => { setFile(null); setReport(null); setForm(null); setUnclear('') }

  async function runAnalysis() {
    setBusy(true); setUnclear('')
    try {
      const res = await analyze(file, productId, inspectorId)
      setReport(res.report)
      setForm(toForm(res.report))
      notify('success', 'AI analysis complete — please review.')
    } catch (e) {
      if (e.response?.data?.code === 'IMAGE_UNCLEAR') {
        setUnclear(e.response.data.detail)
        notify('warn', 'Image is blurry/unclear. Please retake the photo.')
      } else notify('error', errorMessage(e))
    } finally { setBusy(false) }
  }

  async function confirm() {
    setBusy(true)
    try {
      await confirmReport({ reportId: report.reportId, ...form, defectCategoryId: Number(form.defectCategoryId), quantity: Number(form.quantity) })
      notify('success', `Report #${report.reportId} confirmed. Inventory updated (${form.qualityStatus}).`)
      onConfirmed(); reset()
    } catch (e) { notify('error', errorMessage(e)) } finally { setBusy(false) }
  }

  async function discard() {
    try { await discardInspection(report.reportId); notify('info', 'Draft discarded.'); onConfirmed(); reset() }
    catch (e) { notify('error', errorMessage(e)) }
  }

  const selectedCat = categories.find((c) => c.defectCategoryId === Number(form?.defectCategoryId))
  const product = products.find((p) => p.productId === Number(productId))

  return (
    <div className="grid2">
      <section className="panel">
        <h2>1. Upload product image</h2>
        <label>Product
          <select value={productId} onChange={(e) => setProductId(e.target.value)} disabled={!!report}>
            {products.map((p) => <option key={p.productId} value={p.productId}>{p.productName}</option>)}
          </select>
        </label>
        <p className="muted">Inspector: <b>{report?.inspectorName ?? inspector.inspectorName}</b> (signed in)</p>
        <div
          className="drop"
          onDragOver={(e) => e.preventDefault()}
          onDrop={(e) => { e.preventDefault(); if (!report) pickFile(e.dataTransfer.files[0]) }}
        >
          {preview || report ? <img src={report ? imageSrc(report.imageUrl) : preview} alt="preview" /> : <p>Drag &amp; drop an image here, or choose a file</p>}
          <input type="file" accept="image/jpeg,image/png,image/webp" disabled={!!report || busy}
            onChange={(e) => pickFile(e.target.files[0])} />
        </div>
        {unclear && <p className="error">⚠ {unclear}</p>}
        {!report && (
          <button className="primary" disabled={!file || !productId || !inspectorId || busy} onClick={runAnalysis}>
            {busy ? 'Analyzing with Gemini…' : 'Analyze image'}
          </button>
        )}
      </section>

      <section className="panel">
        <h2>2. Review &amp; confirm</h2>
        {!report && <p className="muted">Upload an image and run the analysis to see the AI result here.</p>}
        {report && form && (
          <>
            <p>
              AI confidence: <b>{Math.round(report.confidenceScore * 100)}%</b>{' '}
              {report.confidenceScore < 0.6 && <span className="badge warn">Low – review carefully</span>}
            </p>
            <label>Defect type
              <select value={form.defectCategoryId} onChange={(e) => setForm({ ...form, defectCategoryId: e.target.value })}>
                {categories.map((c) => <option key={c.defectCategoryId} value={c.defectCategoryId}>{c.categoryName} — {c.severityLevel}</option>)}
              </select>
            </label>
            <label>Verdict
              <div className="seg">
                {['Pass', 'Fail'].map((v) => (
                  <button key={v} type="button" className={`${form.qualityStatus === v ? 'on' : ''} ${v}`}
                    onClick={() => setForm({ ...form, qualityStatus: v })}>{v}</button>
                ))}
              </div>
            </label>
            {selectedCat && ['Major', 'Critical'].includes(selectedCat.severityLevel) && form.qualityStatus === 'Pass' &&
              <p className="error">A {selectedCat.severityLevel} defect must be marked Fail.</p>}
            <label>Description (AI-generated, editable)
              <textarea rows="4" value={form.defectDescription} onChange={(e) => setForm({ ...form, defectDescription: e.target.value })} />
            </label>
            <label>Units represented by this inspection
              <input type="number" min="1" max="100000" value={form.quantity} onChange={(e) => setForm({ ...form, quantity: e.target.value })} />
            </label>
            {product && <p className="muted">Stock before: {product.passedStock} passed / {product.failedStock} failed of {product.totalStock}</p>}
            <div className="row">
              <button className="primary" disabled={busy} onClick={confirm}>Confirm &amp; update inventory</button>
              <button disabled={busy} onClick={discard}>Discard</button>
            </div>
          </>
        )}
      </section>
    </div>
  )
}
