import { useState, useEffect, useCallback } from 'react'
import api from '../../../api/axios.js'
import { T, fmt } from '../../../theme/tokens.js'
import { Card, Btn, Badge, HelpPanel, SectionHeader, DataTable, Modal, Input, FileInput, Select, Loading, DocumentPreview, MiniStat } from '../../../components/ui.jsx'
import Pagination from '../../../components/Pagination.jsx'
import { RAMS_STATUSES, EMPTY_RAMS } from '../constants.js'

const empty = { data: { data: {} } }

const PAGE_SIZE = 20

const ramsStatusVariant = s => s === 'Approved' ? 'green' : s === 'Rejected' ? 'red' : s === 'Draft' ? 'default' : 'amber'

const EMPTY_QUICK_SUB = { name: '', tradeCategory: '' }

export default function RamsTab({ employees, setMsg, subcontractors, setSubcontractors, refreshDashboard }) {
  const [rams, setRams] = useState([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [uploading, setUploading] = useState(null)
  const [modal, setModal] = useState(null)
  const [ramsForm, setRamsForm] = useState(EMPTY_RAMS)
  const [viewing, setViewing] = useState(null)
  const [editing, setEditing] = useState(false)
  const [editForm, setEditForm] = useState(EMPTY_RAMS)
  const [previewUrl, setPreviewUrl] = useState(null)
  const [deleteTarget, setDeleteTarget] = useState(null)

  // 'create' | 'edit' | null — tracks which form's Subcontractor dropdown to auto-select
  // the newly added subcontractor into, since the same quick-add modal serves both.
  const [addingSubFor, setAddingSubFor] = useState(null)
  const [quickSubForm, setQuickSubForm] = useState(EMPTY_QUICK_SUB)
  const [savingSub, setSavingSub] = useState(false)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.get('/api/v1/hse-rams', { params: { page, pageSize: PAGE_SIZE } }).catch(() => empty)
      const data = res.data?.data ?? {}
      setRams(data.items ?? [])
      setTotalCount(data.totalCount ?? 0)
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  // subcontractors is lifted to the shell and populated by SubcontractorsTab's
  // own lazy fetch — it may be empty here until that tab has been visited.
  function subcontractorName(id) { return subcontractors.find(s => s.id === id)?.name ?? id }

  async function uploadHseFile(folder, file) {
    if (!file) return null
    setUploading(folder)
    try {
      const form = new FormData()
      form.append('folder', folder)
      form.append('file', file)
      const res = await api.post('/api/v1/hse-uploads', form, { headers: { 'Content-Type': 'multipart/form-data' } })
      return res.data?.data?.url ?? null
    } catch {
      setMsg({ type: 'error', text: 'File upload failed.' })
      return null
    } finally {
      setUploading(null)
    }
  }

  async function submitRams() {
    if (!ramsForm.site || !ramsForm.title) return
    setSaving(true)
    try {
      await api.post('/api/v1/hse-rams', {
        siteId: ramsForm.site, siteName: ramsForm.site,
        subcontractorId: ramsForm.subcontractorId || null,
        subcontractorName: ramsForm.subcontractorId ? subcontractorName(ramsForm.subcontractorId) : null,
        title: ramsForm.title,
        fileUrl: ramsForm.fileUrl || null,
        issueNotes: ramsForm.issueNotes || null,
      })
      setMsg({ type: 'success', text: 'RAMS uploaded — pending review.' })
      setRamsForm(EMPTY_RAMS); setModal(null); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to upload RAMS.' })
    } finally { setSaving(false) }
  }

  async function setRamsStatus(id, statusLabel) {
    try {
      await api.patch(`/api/v1/hse-rams/${id}/status`, { status: RAMS_STATUSES.indexOf(statusLabel), issueNotes: null })
      load(); refreshDashboard()
    } catch {
      setMsg({ type: 'error', text: 'Failed to update RAMS status.' })
    }
  }

  async function submitQuickSub() {
    if (!quickSubForm.name || !quickSubForm.tradeCategory) return
    setSavingSub(true)
    try {
      const res = await api.post('/api/v1/subcontractors', {
        name: quickSubForm.name, tradeCategory: quickSubForm.tradeCategory, notes: null,
      })
      const id = res.data?.data?.id
      if (id) {
        await api.put(`/api/v1/subcontractors/${id}`, {
          name: quickSubForm.name, tradeCategory: quickSubForm.tradeCategory,
          safetyScore: 0, ramsSubmitted: false, notes: null,
        })
        const created = { id, name: quickSubForm.name, tradeCategory: quickSubForm.tradeCategory, safetyScore: 0, ramsSubmitted: false, prequalified: false }
        setSubcontractors(list => [...list, created])
        if (addingSubFor === 'create') setRamsForm(f => ({ ...f, subcontractorId: id }))
        if (addingSubFor === 'edit') setEditForm(f => ({ ...f, subcontractorId: id }))
      }
      setMsg({ type: 'success', text: 'Subcontractor added.' })
      setQuickSubForm(EMPTY_QUICK_SUB); setAddingSubFor(null)
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to add subcontractor.' })
    } finally { setSavingSub(false) }
  }

  function openView(r, startEditing = false) {
    setViewing(r)
    setEditing(startEditing)
    setEditForm({
      site: r.siteId, title: r.title,
      subcontractorId: r.subcontractorId ?? '',
      fileUrl: r.fileUrl ?? '', issueNotes: r.issueNotes ?? '',
    })
  }

  async function submitEdit() {
    if (!viewing || !editForm.site || !editForm.title) return
    setSaving(true)
    try {
      await api.put(`/api/v1/hse-rams/${viewing.id}`, {
        siteId: editForm.site, siteName: editForm.site,
        subcontractorId: editForm.subcontractorId || null,
        subcontractorName: editForm.subcontractorId ? subcontractorName(editForm.subcontractorId) : null,
        title: editForm.title,
        fileUrl: editForm.fileUrl || null,
        issueNotes: editForm.issueNotes || null,
      })
      setMsg({ type: 'success', text: 'RAMS record updated.' })
      setViewing(null); setEditing(false); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update RAMS record.' })
    } finally { setSaving(false) }
  }

  async function confirmDelete() {
    if (!deleteTarget) return
    try {
      await api.delete(`/api/v1/hse-rams/${deleteTarget.id}`)
      setMsg({ type: 'success', text: 'RAMS record deleted.' })
      load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to delete RAMS record.' })
    } finally {
      setDeleteTarget(null)
    }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About the RAMS Library">
        RAMS (Risk Assessment &amp; Method Statement) documents are versioned per site — uploading
        again under the same site and title bumps the version automatically. Click any row to see
        the full record, including the uploaded document, and to edit its details. Approve or reject
        moves a submission through review; approving/rejecting doesn't change the document itself.
        <br /><br />
        <strong>HSE-002:</strong> RAMS upload is mandatory before site mobilisation — the ERP blocks
        a project's start without an approved RAMS.
      </HelpPanel>

      <SectionHeader title="RAMS Library" sub="Risk Assessment & Method Statements — versioned per site" action={<Btn onClick={() => setModal('rams')}>+ Upload RAMS</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          onRowClick={(_, i) => openView(rams[i])}
          headers={['Site', 'Title', 'Version', 'Subcontractor', 'Status', 'Reviewed', 'Actions']}
          empty="No RAMS uploaded yet."
          rows={rams.map(r => [
            <strong style={{ fontSize: 12, color: T.dgrey }}>{r.siteName ?? r.siteId}</strong>,
            r.title,
            <Badge variant="blue">v{r.version}</Badge>,
            r.subcontractorName ?? '—',
            <Badge variant={ramsStatusVariant(RAMS_STATUSES[r.status] ?? r.status)}>{RAMS_STATUSES[r.status] ?? r.status}</Badge>,
            r.reviewedAt ? fmt.date(r.reviewedAt) : '—',
            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
              <Btn size="sm" variant="ghost" onClick={e => { e.stopPropagation(); openView(r) }}>View</Btn>
              <Btn size="sm" onClick={e => { e.stopPropagation(); openView(r, true) }}>Edit</Btn>
              {(r.status === 1 || r.status === 2) && (
                <>
                  <Btn size="sm" variant="green" onClick={e => { e.stopPropagation(); setRamsStatus(r.id, 'Approved') }}>Approve</Btn>
                  <Btn size="sm" variant="danger" onClick={e => { e.stopPropagation(); setRamsStatus(r.id, 'Rejected') }}>Reject</Btn>
                </>
              )}
              <Btn size="sm" variant="danger" onClick={e => { e.stopPropagation(); setDeleteTarget(r) }}>Delete</Btn>
            </div>,
          ])}
        />
        <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />
      </Card>

      {/* ── Upload RAMS modal ── */}
      {modal === 'rams' && (
        <Modal title="Upload RAMS" onClose={() => setModal(null)}>
          <Input label="Site / Location" value={ramsForm.site} onChange={v => setRamsForm({ ...ramsForm, site: v })} required />
          <Input label="Title" value={ramsForm.title} onChange={v => setRamsForm({ ...ramsForm, title: v })} required placeholder="e.g. Working at Heights RAMS" />
          <div style={{ display: 'flex', gap: 8, alignItems: 'flex-end' }}>
            <div style={{ flex: 1 }}>
              <Select label="Subcontractor (optional)" value={ramsForm.subcontractorId} onChange={v => setRamsForm({ ...ramsForm, subcontractorId: v })}
                options={[{ value: '', label: '— Not subcontractor-specific —' }, ...subcontractors.map(s => ({ value: s.id, label: s.name }))]} />
            </div>
            <Btn variant="ghost" size="sm" onClick={() => { setAddingSubFor('create'); setQuickSubForm(EMPTY_QUICK_SUB) }} style={{ marginBottom: 14 }}>+ Add New</Btn>
          </div>
          <FileInput label="RAMS Document" accept=".pdf,.doc,.docx,.jpg,.jpeg,.png,.webp"
            uploading={uploading === 'rams'} fileUrl={ramsForm.fileUrl}
            onFileSelected={async file => { const url = await uploadHseFile('rams', file); if (url) setRamsForm(f => ({ ...f, fileUrl: url })) }} />
          <Input label="Notes" value={ramsForm.issueNotes} onChange={v => setRamsForm({ ...ramsForm, issueNotes: v })} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitRams} disabled={saving || !!uploading || !ramsForm.site || !ramsForm.title}>{saving ? 'Uploading…' : 'Upload'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit RAMS modal ── */}
      {viewing && (
        <Modal title={editing ? 'Edit RAMS' : 'RAMS Details'} onClose={() => { setViewing(null); setEditing(false) }}>
          {!editing ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewing.title}</div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>
                  {viewing.siteName ?? viewing.siteId}{viewing.subcontractorName ? ` · ${viewing.subcontractorName}` : ''}
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Version" value={`v${viewing.version}`} />
                <MiniStat label="Status" value={<Badge variant={ramsStatusVariant(RAMS_STATUSES[viewing.status] ?? viewing.status)}>{RAMS_STATUSES[viewing.status] ?? viewing.status}</Badge>} />
                <MiniStat label="Reviewed" value={viewing.reviewedAt ? fmt.date(viewing.reviewedAt) : '—'} />
                <MiniStat label="Uploaded" value={fmt.date(viewing.createdAt)} />
              </div>

              <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>Document</div>
                {viewing.fileUrl
                  ? <Btn size="sm" variant="ghost" onClick={() => setPreviewUrl(viewing.fileUrl)}>👁 View document</Btn>
                  : <span style={{ fontSize: 13, color: T.mgrey }}>No document uploaded.</span>}
              </div>

              {viewing.issueNotes && (
                <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                  <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 6 }}>Notes</div>
                  <div style={{ fontSize: 13, color: T.dgrey }}>{viewing.issueNotes}</div>
                </div>
              )}

              <p style={{ fontSize: 11, color: T.mgrey, margin: '4px 0 0' }}>Status changes via Approve/Reject in the RAMS Library, not here.</p>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewing(null); setEditing(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditing(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Input label="Site / Location" value={editForm.site} onChange={v => setEditForm({ ...editForm, site: v })} required />
              <Input label="Title" value={editForm.title} onChange={v => setEditForm({ ...editForm, title: v })} required />
              <div style={{ display: 'flex', gap: 8, alignItems: 'flex-end' }}>
                <div style={{ flex: 1 }}>
                  <Select label="Subcontractor (optional)" value={editForm.subcontractorId} onChange={v => setEditForm({ ...editForm, subcontractorId: v })}
                    options={[{ value: '', label: '— Not subcontractor-specific —' }, ...subcontractors.map(s => ({ value: s.id, label: s.name }))]} />
                </div>
                <Btn variant="ghost" size="sm" onClick={() => { setAddingSubFor('edit'); setQuickSubForm(EMPTY_QUICK_SUB) }} style={{ marginBottom: 14 }}>+ Add New</Btn>
              </div>
              <FileInput label="RAMS Document" accept=".pdf,.doc,.docx,.jpg,.jpeg,.png,.webp"
                uploading={uploading === 'rams'} fileUrl={editForm.fileUrl}
                onFileSelected={async file => { const url = await uploadHseFile('rams', file); if (url) setEditForm(f => ({ ...f, fileUrl: url })) }} />
              <Input label="Notes" value={editForm.issueNotes} onChange={v => setEditForm({ ...editForm, issueNotes: v })} />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditing(false)}>Cancel</Btn>
                <Btn onClick={submitEdit} disabled={saving || !!uploading || !editForm.site || !editForm.title}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}

      {/* ── Document preview modal ── */}
      {previewUrl && (
        <Modal title="RAMS Document" onClose={() => setPreviewUrl(null)} width={800}>
          <DocumentPreview url={previewUrl} />
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 16, marginTop: 12 }}>
            <a href={previewUrl} download style={{ fontSize: 12, color: T.blue }}>⬇ Download</a>
            <a href={previewUrl} target="_blank" rel="noreferrer" style={{ fontSize: 12, color: T.blue }}>Open in new tab ↗</a>
          </div>
        </Modal>
      )}

      {/* ── Quick-add Subcontractor modal ── */}
      {addingSubFor && (
        <Modal title="Add Subcontractor" onClose={() => setAddingSubFor(null)} width={420}>
          <Input label="Name" value={quickSubForm.name} onChange={v => setQuickSubForm({ ...quickSubForm, name: v })} required />
          <Input label="Trade Category" value={quickSubForm.tradeCategory} onChange={v => setQuickSubForm({ ...quickSubForm, tradeCategory: v })} required placeholder="e.g. Electrical, Plumbing, Civil Works" />
          <p style={{ fontSize: 11, color: T.mgrey, margin: '0 0 14px' }}>Adds to the Approved Subcontractor Register with default safety score — full details can be filled in later from the Subcontractor Prequal tab.</p>
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setAddingSubFor(null)}>Cancel</Btn>
            <Btn onClick={submitQuickSub} disabled={savingSub || !quickSubForm.name || !quickSubForm.tradeCategory}>{savingSub ? 'Saving…' : 'Add Subcontractor'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── Delete confirm modal ── */}
      {deleteTarget && (
        <Modal title="Delete RAMS Record" onClose={() => setDeleteTarget(null)} width={380}>
          <p style={{ fontSize: 13, color: T.dgrey }}>
            Are you sure you want to delete <strong>"{deleteTarget.title}"</strong> (v{deleteTarget.version}) at "{deleteTarget.siteName ?? deleteTarget.siteId}"? This action cannot be undone.
          </p>
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setDeleteTarget(null)}>Cancel</Btn>
            <Btn variant="danger" onClick={confirmDelete}>Yes, Delete</Btn>
          </div>
        </Modal>
      )}
    </>
  )
}
