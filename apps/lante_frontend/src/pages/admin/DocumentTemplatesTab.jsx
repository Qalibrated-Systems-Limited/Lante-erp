import { useState, useEffect, useCallback } from 'react'
import api from '../../api/axios.js'
import { Card, DataTable, Badge, Btn, Alert, Loading, Modal } from '../../components/ui.jsx'
import { T } from '../../theme/tokens.js'

// Scoped deliberately narrow: footer note + terms text only, for the two PDFs that actually
// have a free-text footer area today (Ticket PDF, GRN PDF). ISO-controlled forms (Requisition
// A-1/A-2, Travel Voucher) aren't here — their layout is fixed by design, not admin-editable.
export default function DocumentTemplatesTab() {
  const [templates, setTemplates] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [msg, setMsg] = useState(null)
  const [edit, setEdit] = useState(null) // { docType, footerNote, termsText }
  const [saving, setSaving] = useState(false)

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await api.get('/api/v1/document-templates')
      setTemplates(res.data?.data ?? [])
    } catch {
      setError('Failed to load document templates.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  function openEdit(t) {
    setEdit({ docType: t.docType, footerNote: t.footerNote ?? '', termsText: t.termsText ?? '' })
  }

  async function save() {
    setSaving(true)
    try {
      await api.put(`/api/v1/document-templates/${edit.docType}`, { footerNote: edit.footerNote || null, termsText: edit.termsText || null })
      setMsg({ type: 'success', text: 'Template saved — every new PDF of this type picks it up immediately.' })
      setEdit(null)
      load()
    } catch (err) {
      setMsg({ type: 'error', text: err.response?.data?.message ?? 'Failed to save.' })
    } finally {
      setSaving(false)
    }
  }

  return (
    <>
      <Alert type="info">
        Controls only the footer note and terms text printed on generated PDFs. Layout, titles and form
        codes for controlled quality documents (Requisition Forms, Travel Voucher) aren't customizable here.
      </Alert>
      {error && <Alert type="error">{error}</Alert>}
      {msg && <Alert type={msg.type}>{msg.text}</Alert>}

      {loading ? <Loading /> : (
        <Card style={{ padding: 0, overflow: 'hidden' }}>
          <DataTable
            headers={['Document Type', 'Footer Note', 'Terms Text', 'Status', '']}
            rows={templates.map(t => [
              <strong style={{ fontSize: 12 }}>{t.displayName}</strong>,
              t.footerNote || <span style={{ color: T.mgrey }}>—</span>,
              t.termsText ? <span style={{ fontSize: 11 }}>{t.termsText.slice(0, 60)}{t.termsText.length > 60 ? '…' : ''}</span> : <span style={{ color: T.mgrey }}>—</span>,
              t.isCustomised ? <Badge variant="amber">Customised</Badge> : <Badge variant="default">Default</Badge>,
              <Btn size="sm" onClick={() => openEdit(t)}>Edit</Btn>,
            ])}
          />
        </Card>
      )}

      {edit && (
        <Modal title={`Edit Template — ${edit.docType}`} onClose={() => setEdit(null)} width={560}>
          <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 5 }}>Footer Note</label>
          <input value={edit.footerNote} onChange={e => setEdit(f => ({ ...f, footerNote: e.target.value }))}
            placeholder="Replaces the default 'Confidential — Internal Record' footer line"
            style={{ width: '100%', padding: '9px 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13, marginBottom: 14, boxSizing: 'border-box' }} />

          <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 5 }}>Terms & Conditions</label>
          <textarea value={edit.termsText} onChange={e => setEdit(f => ({ ...f, termsText: e.target.value }))} rows={4}
            placeholder="Optional — printed near the bottom of the document"
            style={{ width: '100%', padding: '9px 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13, marginBottom: 14, fontFamily: 'inherit', resize: 'vertical', boxSizing: 'border-box' }} />

          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
            <Btn variant="ghost" onClick={() => setEdit(null)}>Cancel</Btn>
            <Btn onClick={save} disabled={saving}>{saving ? 'Saving…' : 'Save Template'}</Btn>
          </div>
        </Modal>
      )}
    </>
  )
}
