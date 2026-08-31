import { useState, useEffect, useCallback } from 'react'
import api from '../../../../api/axios.js'
import { T, fmt } from '../../../../theme/tokens.js'
import { Card, Btn, HelpPanel, SectionHeader, DataTable, Modal, Input, FileInput, Loading, DocumentPreview, MiniStat } from '../../../../components/ui.jsx'
import Pagination from '../../../../components/Pagination.jsx'
import { EMPTY_RESOLUTION } from '../constants.js'

const PAGE_SIZE = 20

export default function ResolutionsTab({ setMsg }) {
  const [resolutions, setResolutions] = useState([])
  const [loading, setLoading] = useState(true)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [modal, setModal] = useState(null)
  const [saving, setSaving] = useState(false)
  const [resolutionForm, setResolutionForm] = useState(EMPTY_RESOLUTION)
  const [uploading, setUploading] = useState(null)
  const [viewing, setViewing] = useState(null)
  const [editing, setEditing] = useState(false)
  const [editForm, setEditForm] = useState(EMPTY_RESOLUTION)
  const [previewUrl, setPreviewUrl] = useState(null)

  async function uploadComplianceFile(folder, file) {
    if (!file) return null
    setUploading(folder)
    try {
      const form = new FormData()
      form.append('folder', folder)
      form.append('file', file)
      const res = await api.post('/api/v1/compliance-uploads', form, { headers: { 'Content-Type': 'multipart/form-data' } })
      return res.data?.data?.url ?? null
    } catch {
      setMsg({ type: 'error', text: 'File upload failed.' })
      return null
    } finally {
      setUploading(null)
    }
  }

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.get('/api/v1/compliance-board-resolutions', { params: { page, pageSize: PAGE_SIZE } }).catch(() => ({ data: { data: {} } }))
      const data = res.data?.data || {}
      setResolutions(data.items ?? [])
      setTotalCount(data.totalCount ?? 0)
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  async function submitResolution() {
    if (!resolutionForm.referenceNo || !resolutionForm.title) return
    setSaving(true)
    try {
      await api.post('/api/v1/compliance-board-resolutions', {
        ...resolutionForm,
        resolutionDate: new Date(resolutionForm.resolutionDate || Date.now()).toISOString(),
      })
      setMsg({ type: 'success', text: 'Board resolution logged.' })
      setResolutionForm(EMPTY_RESOLUTION); setModal(null); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to log resolution.' })
    } finally { setSaving(false) }
  }

  function openView(r, startEditing = false) {
    setViewing(r)
    setEditing(startEditing)
    setEditForm({
      referenceNo: r.referenceNo, title: r.title,
      resolutionDate: (r.resolutionDate ?? '').slice(0, 10),
      summary: r.summary ?? '', scannedCopyUrl: r.scannedCopyUrl ?? '',
    })
  }

  async function submitEdit() {
    if (!viewing || !editForm.referenceNo || !editForm.title) return
    setSaving(true)
    try {
      await api.put(`/api/v1/compliance-board-resolutions/${viewing.id}`, {
        referenceNo: editForm.referenceNo,
        title: editForm.title,
        resolutionDate: new Date(editForm.resolutionDate || Date.now()).toISOString(),
        summary: editForm.summary || null,
        scannedCopyUrl: editForm.scannedCopyUrl || null,
      })
      setMsg({ type: 'success', text: 'Board resolution updated.' })
      setViewing(null); setEditing(false); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update resolution.' })
    } finally { setSaving(false) }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About the Board Resolution Register">
        Every board resolution is logged with a reference number, date, and scanned copy for the
        statutory register. Click any row to see the full record, including the scanned copy, and
        to edit its details.
      </HelpPanel>

      <SectionHeader title="Board Resolution Register" action={<Btn onClick={() => setModal('resolution')}>+ Log Resolution</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Reference No', 'Title', 'Date', 'Scanned Copy', 'Actions']}
          empty="No board resolutions logged yet."
          rows={resolutions.map(r => [
            <strong style={{ fontFamily: 'monospace', fontSize: 12 }}>{r.referenceNo}</strong>,
            <span onClick={() => openView(r)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{r.title}</span>,
            fmt.date(r.resolutionDate),
            r.scannedCopyUrl ? <Btn size="sm" variant="ghost" onClick={() => setPreviewUrl(r.scannedCopyUrl)}>👁 View document</Btn> : '—',
            <div style={{ display: 'flex', gap: 4 }}>
              <Btn variant="ghost" size="sm" onClick={() => openView(r)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={() => openView(r, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
            </div>,
          ])}
        />
      </Card>
      <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />

      {modal === 'resolution' && (
        <Modal title="Log Board Resolution" onClose={() => setModal(null)}>
          <Input label="Reference No" value={resolutionForm.referenceNo} onChange={v => setResolutionForm({ ...resolutionForm, referenceNo: v })} required />
          <Input label="Title" value={resolutionForm.title} onChange={v => setResolutionForm({ ...resolutionForm, title: v })} required />
          <Input label="Resolution Date" type="date" value={resolutionForm.resolutionDate} onChange={v => setResolutionForm({ ...resolutionForm, resolutionDate: v })} />
          <Input label="Summary" value={resolutionForm.summary} onChange={v => setResolutionForm({ ...resolutionForm, summary: v })} />
          <FileInput label="Scanned Copy" accept=".pdf,.jpg,.jpeg,.png,.webp"
            uploading={uploading === 'resolutions'} fileUrl={resolutionForm.scannedCopyUrl}
            onFileSelected={async file => { const url = await uploadComplianceFile('resolutions', file); if (url) setResolutionForm(f => ({ ...f, scannedCopyUrl: url })) }} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitResolution} disabled={saving || !!uploading || !resolutionForm.referenceNo || !resolutionForm.title}>{saving ? 'Saving…' : 'Log Resolution'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit Resolution modal ── */}
      {viewing && (
        <Modal title={editing ? 'Edit Board Resolution' : 'Board Resolution Details'} onClose={() => { setViewing(null); setEditing(false) }} width={620}>
          {!editing ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewing.title}</div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>
                  <span style={{ fontFamily: 'monospace' }}>{viewing.referenceNo}</span> · {fmt.date(viewing.resolutionDate)}
                </div>
              </div>

              <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>Summary</div>
                <div style={{ fontSize: 13, color: T.dgrey }}>{viewing.summary || 'No summary.'}</div>
              </div>

              <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>Scanned Copy</div>
                {viewing.scannedCopyUrl
                  ? <Btn size="sm" variant="ghost" onClick={() => setPreviewUrl(viewing.scannedCopyUrl)}>👁 View document</Btn>
                  : <span style={{ fontSize: 13, color: T.mgrey }}>No document uploaded.</span>}
              </div>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewing(null); setEditing(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditing(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Input label="Reference No" value={editForm.referenceNo} onChange={v => setEditForm({ ...editForm, referenceNo: v })} required />
              <Input label="Title" value={editForm.title} onChange={v => setEditForm({ ...editForm, title: v })} required />
              <Input label="Resolution Date" type="date" value={editForm.resolutionDate} onChange={v => setEditForm({ ...editForm, resolutionDate: v })} />
              <Input label="Summary" value={editForm.summary} onChange={v => setEditForm({ ...editForm, summary: v })} />
              <FileInput label="Scanned Copy" accept=".pdf,.jpg,.jpeg,.png,.webp"
                uploading={uploading === 'resolutions'} fileUrl={editForm.scannedCopyUrl}
                onFileSelected={async file => { const url = await uploadComplianceFile('resolutions', file); if (url) setEditForm(f => ({ ...f, scannedCopyUrl: url })) }} />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditing(false)}>Cancel</Btn>
                <Btn onClick={submitEdit} disabled={saving || !!uploading || !editForm.referenceNo || !editForm.title}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}

      {/* ── Document preview modal ── */}
      {previewUrl && (
        <Modal title="Scanned Copy" onClose={() => setPreviewUrl(null)} width={800}>
          <DocumentPreview url={previewUrl} />
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 16, marginTop: 12 }}>
            <a href={previewUrl} download style={{ fontSize: 12, color: T.blue }}>⬇ Download</a>
            <a href={previewUrl} target="_blank" rel="noreferrer" style={{ fontSize: 12, color: T.blue }}>Open in new tab ↗</a>
          </div>
        </Modal>
      )}
    </>
  )
}
