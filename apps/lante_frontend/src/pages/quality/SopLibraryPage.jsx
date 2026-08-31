import { useState, useEffect, useCallback } from 'react'
import api from '../../api/axios.js'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Btn, Badge, Alert, DataTable, Modal, Input, Select, FileInput, DocumentPreview, Loading } from '../../components/ui.jsx'
import useCompanyBranding from '../../hooks/useCompanyBranding.js'

// ─────────────────────────────────────────────────────────────────────────────
// SOP Library — departmental procedures with current revision & review dates,
// filtered by department. Backed by compliance-service's /api/v1/sop-library
// (SopLibraryController). SOP Code is always assigned server-side on create —
// never trust/generate it on the client.
// ─────────────────────────────────────────────────────────────────────────────

const PAD = 'clamp(16px, 2.4vw, 26px)'
const DEPARTMENTS = ['Technical', 'Commercial', 'Finance', 'HR', 'Procurement', 'Stores', 'Projects', 'HSE', 'Administration']
const FILTERS = ['All', ...DEPARTMENTS]
const EMPTY_FORM = { department: 'Technical', title: '', category: '', nextReview: '', fileUrl: '' }

const daysUntil = d => { if (!d) return null; const x = new Date(d); return isNaN(x) ? null : Math.ceil((x - new Date(new Date().toDateString())) / 86400000) }

export default function SopLibraryPage() {
  const branding = useCompanyBranding()
  const [dept, setDept] = useState('All')
  const [modal, setModal] = useState(false)
  const [msg, setMsg] = useState(null)
  const [sops, setSops] = useState([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [form, setForm] = useState(EMPTY_FORM)
  const [previewUrl, setPreviewUrl] = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.get('/api/v1/sop-library', { params: { page: 1, pageSize: 100 } })
      setSops(res.data?.data?.items ?? [])
    } catch {
      setMsg({ type: 'error', text: 'Failed to load the SOP Library.' })
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  async function uploadSopFile(file) {
    if (!file) return null
    setUploading(true)
    try {
      const body = new FormData()
      body.append('folder', 'sops')
      body.append('file', file)
      const res = await api.post('/api/v1/compliance-uploads', body, { headers: { 'Content-Type': 'multipart/form-data' } })
      return res.data?.data?.url ?? null
    } catch {
      setMsg({ type: 'error', text: 'File upload failed.' })
      return null
    } finally {
      setUploading(false)
    }
  }

  async function createSop() {
    if (!form.title) return
    setSaving(true)
    try {
      await api.post('/api/v1/sop-library', {
        title: form.title,
        department: form.department,
        category: form.category || null,
        nextReview: form.nextReview || null,
        fileUrl: form.fileUrl || null,
      })
      setForm(EMPTY_FORM); setModal(false)
      setMsg({ type: 'success', text: 'SOP added to the library at Rev 1.' })
      load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to add SOP.' })
    } finally {
      setSaving(false)
    }
  }

  const visible = sops.filter(s => dept === 'All' || s.department === dept)

  const statusBadge = s => {
    const d = daysUntil(s.nextReview)
    if (d !== null && d < 0) return <Badge variant="red">Due for Review</Badge>
    if (d !== null && d <= 60) return <Badge variant="amber">Review Soon</Badge>
    return <Badge variant="green">Current</Badge>
  }

  if (loading) return <Loading />

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        {msg && <Alert type={msg.type}>{msg.text}</Alert>}

        {/* Header */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12, flexWrap: 'wrap', marginBottom: 16 }}>
          <div>
            <h1 style={{ fontSize: 22, fontWeight: 800, color: T.navy, margin: 0 }}>SOP Library</h1>
            <p style={{ fontSize: 13, color: T.mgrey, marginTop: 4 }}>Departmental procedures, current revision &amp; review history</p>
          </div>
          <Btn onClick={() => setModal(true)}>+ New SOP</Btn>
        </div>

        {/* Department filter pills */}
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginBottom: 16 }}>
          {FILTERS.map(f => (
            <button key={f} onClick={() => setDept(f)} style={{
              padding: '6px 16px', borderRadius: 8, fontSize: 13, fontWeight: 600, cursor: 'pointer',
              border: `1px solid ${dept === f ? T.navy : T.lgrey}`,
              background: dept === f ? T.navy : T.white,
              color: dept === f ? T.white : T.dgrey,
            }}>{f}</button>
          ))}
        </div>

        {/* Table */}
        <Card style={{ padding: 0, overflow: 'hidden' }}>
          <DataTable
            headers={['Code', 'Title', 'Department', 'Category', 'Version', 'Last Reviewed', 'Next Review', 'Status', 'Document']}
            empty="No SOPs yet for this department."
            rows={visible.map(s => {
              const d = daysUntil(s.nextReview)
              const nextColor = d === null ? T.mgrey : d < 0 ? T.red : d <= 60 ? T.amber : T.dgrey
              return [
                <strong style={{ fontSize: 12, fontFamily: 'monospace' }}>{s.code}</strong>,
                <span style={{ fontWeight: 600, fontSize: 12.5, color: T.dgrey }}>{s.title}</span>,
                <Badge variant="navy">{s.department}</Badge>,
                s.category ?? '—',
                <Badge variant="blue">{s.version}</Badge>,
                fmt.date(s.lastReviewed),
                <span style={{ color: nextColor, fontWeight: d !== null && d <= 60 ? 700 : 400 }}>{s.nextReview ? fmt.date(s.nextReview) : '—'}</span>,
                statusBadge(s),
                s.fileUrl
                  ? <Btn size="sm" variant="ghost" onClick={() => setPreviewUrl(s.fileUrl)}>👁 View</Btn>
                  : <span style={{ fontSize: 12, color: T.mgrey }}>No document</span>,
              ]
            })}
          />
        </Card>

        {/* New SOP modal */}
        {modal && (
          <Modal title="New SOP" onClose={() => setModal(false)} width={620}>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
              <Input label="SOP Code" value="(assigned automatically on save)" readOnly note={`Server-generated — ${branding.docPrefix}/QP/n`} />
              <Select label="Department" value={form.department} onChange={v => setForm({ ...form, department: v })} options={DEPARTMENTS.map(d => ({ value: d, label: d }))} />
            </div>
            <Input label="Title" value={form.title} onChange={v => setForm({ ...form, title: v })} required />
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
              <Input label="Category" value={form.category} onChange={v => setForm({ ...form, category: v })} placeholder="e.g. Calibration, Safety" />
              <Input label="Next Review Date" type="date" value={form.nextReview} onChange={v => setForm({ ...form, nextReview: v })} />
            </div>
            <FileInput label="Document File (PDF/Word)" accept=".pdf,.doc,.docx"
              uploading={uploading} fileUrl={form.fileUrl}
              onFileSelected={async file => { const url = await uploadSopFile(file); if (url) setForm(f => ({ ...f, fileUrl: url })) }} />
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
              <Btn variant="ghost" onClick={() => setModal(false)}>Cancel</Btn>
              <Btn onClick={createSop} disabled={saving || uploading || !form.title}>{saving ? 'Saving…' : 'Create SOP'}</Btn>
            </div>
          </Modal>
        )}

        {/* Document preview modal */}
        {previewUrl && (
          <Modal title="SOP Document" onClose={() => setPreviewUrl(null)} width={800}>
            <DocumentPreview url={previewUrl} />
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 16, marginTop: 12 }}>
              <a href={previewUrl} download style={{ fontSize: 12, color: T.blue }}>⬇ Download</a>
              <a href={previewUrl} target="_blank" rel="noreferrer" style={{ fontSize: 12, color: T.blue }}>Open in new tab ↗</a>
            </div>
          </Modal>
        )}
      </div>
    </>
  )
}
