import { useState, useEffect, useCallback } from 'react'
import api from '../../../api/axios.js'
import { T, fmt } from '../../../theme/tokens.js'
import { Card, Btn, Badge, HelpPanel, SectionHeader, DataTable, Modal, Input, FileInput, Select, Loading, MiniStat, DocumentPreview } from '../../../components/ui.jsx'
import Pagination from '../../../components/Pagination.jsx'
import { INSPECTION_STATUSES, EMPTY_INSPECTION, EMPTY_RESULT, daysUntil } from '../constants.js'

const empty = { data: { data: {} } }

const PAGE_SIZE = 20

const inspectionStatusVariant = (status, dueDate) => {
  if (status === 'Failed') return 'red'
  if (status === 'Passed') return daysUntil(dueDate) !== null && daysUntil(dueDate) < 0 ? 'amber' : 'green'
  return daysUntil(dueDate) !== null && daysUntil(dueDate) < 0 ? 'red' : 'amber'
}

export default function InspectionsTab({ setMsg, refreshDashboard }) {
  const [inspections, setInspections] = useState([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [uploading, setUploading] = useState(null)
  const [modal, setModal] = useState(null)
  const [inspectionForm, setInspectionForm] = useState(EMPTY_INSPECTION)
  const [resultForm, setResultForm] = useState(EMPTY_RESULT)
  const [activeInspectionId, setActiveInspectionId] = useState(null)
  const [viewing, setViewing] = useState(null)
  const [editing, setEditing] = useState(false)
  const [editForm, setEditForm] = useState(EMPTY_INSPECTION)
  const [previewUrl, setPreviewUrl] = useState(null)
  const [deleteTarget, setDeleteTarget] = useState(null)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.get('/api/v1/hse-statutory-inspections', { params: { page, pageSize: PAGE_SIZE } }).catch(() => empty)
      const data = res.data?.data ?? {}
      setInspections(data.items ?? [])
      setTotalCount(data.totalCount ?? 0)
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

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

  async function submitInspection() {
    if (!inspectionForm.site || !inspectionForm.equipment || !inspectionForm.dueDate) return
    setSaving(true)
    try {
      await api.post('/api/v1/hse-statutory-inspections', {
        siteId: inspectionForm.site, siteName: inspectionForm.site,
        equipment: inspectionForm.equipment,
        dueDate: new Date(inspectionForm.dueDate).toISOString(),
      })
      setMsg({ type: 'success', text: 'Statutory inspection scheduled.' })
      setInspectionForm(EMPTY_INSPECTION); setModal(null); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to schedule inspection.' })
    } finally { setSaving(false) }
  }

  function openRecordResult(inspection) {
    setActiveInspectionId(inspection.id)
    setResultForm(EMPTY_RESULT)
    setModal('inspResult')
  }

  async function submitResult() {
    if (!resultForm.nextDueDate) return
    setSaving(true)
    try {
      await api.patch(`/api/v1/hse-statutory-inspections/${activeInspectionId}/result`, {
        status: INSPECTION_STATUSES.indexOf(resultForm.status),
        inspectorName: resultForm.inspectorName || null,
        certificateUrl: resultForm.certificateUrl || null,
        nextDueDate: new Date(resultForm.nextDueDate).toISOString(),
      })
      setMsg({ type: 'success', text: 'Inspection result recorded.' })
      setModal(null); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to record inspection result.' })
    } finally { setSaving(false) }
  }

  function openView(i, startEditing = false) {
    setViewing(i)
    setEditing(startEditing)
    setEditForm({ site: i.siteId, equipment: i.equipment, dueDate: i.dueDate ? i.dueDate.slice(0, 10) : '' })
  }

  async function submitEdit() {
    if (!viewing || !editForm.site || !editForm.equipment || !editForm.dueDate) return
    setSaving(true)
    try {
      await api.put(`/api/v1/hse-statutory-inspections/${viewing.id}`, {
        siteId: editForm.site, siteName: editForm.site,
        equipment: editForm.equipment,
        dueDate: new Date(editForm.dueDate).toISOString(),
      })
      setMsg({ type: 'success', text: 'Inspection updated.' })
      setViewing(null); setEditing(false); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update inspection.' })
    } finally { setSaving(false) }
  }

  async function confirmDelete() {
    if (!deleteTarget) return
    try {
      await api.delete(`/api/v1/hse-statutory-inspections/${deleteTarget.id}`)
      setMsg({ type: 'success', text: 'Inspection deleted.' })
      load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to delete inspection.' })
    } finally {
      setDeleteTarget(null)
    }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About Statutory Inspections">
        Track legally required inspection due dates for scaffolding, lifting equipment, pressure
        vessels and similar. Click a row to see the full record and certificate, and to edit the
        site, equipment or due date. Recording a result closes this cycle and schedules the next one
        as a new row — that's done via "Record Result", not this edit.
      </HelpPanel>

      <SectionHeader title="Statutory Inspection Register" sub="Scaffolding, lifting equipment, pressure vessels — legal inspection due dates" action={<Btn onClick={() => setModal('inspection')}>+ Schedule Inspection</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Site', 'Equipment', 'Last Inspected', 'Due Date', 'Status', 'Actions']}
          empty="No statutory inspections scheduled yet."
          rows={inspections.map(i => [
            i.siteName ?? i.siteId,
            <span onClick={() => openView(i)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{i.equipment}</span>,
            i.lastInspectedAt ? fmt.date(i.lastInspectedAt) : '—',
            fmt.date(i.dueDate),
            <Badge variant={inspectionStatusVariant(INSPECTION_STATUSES[i.status] ?? i.status, i.dueDate)}>{INSPECTION_STATUSES[i.status] ?? i.status}</Badge>,
            <div style={{ display: 'flex', gap: 4, alignItems: 'center', flexWrap: 'wrap' }}>
              <Btn variant="ghost" size="sm" onClick={() => openView(i)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={() => openView(i, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
              {(i.status === 0 || i.status === 3)
                ? <Btn size="sm" onClick={() => openRecordResult(i)}>Record Result</Btn>
                : <span style={{ color: T.mgrey, fontSize: 12 }}>Closed</span>}
              <Btn variant="danger" size="sm" onClick={() => setDeleteTarget(i)} style={{ padding: '3px 10px', fontSize: 11 }}>Delete</Btn>
            </div>,
          ])}
        />
        <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />
      </Card>

      {/* ── Schedule Inspection modal ── */}
      {modal === 'inspection' && (
        <Modal title="Schedule Statutory Inspection" onClose={() => setModal(null)}>
          <Input label="Site / Location" value={inspectionForm.site} onChange={v => setInspectionForm({ ...inspectionForm, site: v })} required />
          <Input label="Equipment" value={inspectionForm.equipment} onChange={v => setInspectionForm({ ...inspectionForm, equipment: v })} required placeholder="e.g. Scaffolding, Crane, Pressure vessel" />
          <Input label="Due Date" type="date" value={inspectionForm.dueDate} onChange={v => setInspectionForm({ ...inspectionForm, dueDate: v })} required />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitInspection} disabled={saving || !inspectionForm.site || !inspectionForm.equipment || !inspectionForm.dueDate}>{saving ? 'Saving…' : 'Schedule'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── Record Inspection Result modal ── */}
      {modal === 'inspResult' && (
        <Modal title="Record Inspection Result" onClose={() => setModal(null)}>
          <Select label="Result" value={resultForm.status} onChange={v => setResultForm({ ...resultForm, status: v })} options={['Passed', 'Failed'].map(s => ({ value: s, label: s }))} />
          <Input label="Inspector Name" value={resultForm.inspectorName} onChange={v => setResultForm({ ...resultForm, inspectorName: v })} />
          <FileInput label="Certificate" accept=".pdf,.jpg,.jpeg,.png,.webp"
            uploading={uploading === 'inspections'} fileUrl={resultForm.certificateUrl}
            onFileSelected={async file => { const url = await uploadHseFile('inspections', file); if (url) setResultForm(f => ({ ...f, certificateUrl: url })) }} />
          <Input label="Next Due Date" type="date" value={resultForm.nextDueDate} onChange={v => setResultForm({ ...resultForm, nextDueDate: v })} required />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitResult} disabled={saving || !!uploading || !resultForm.nextDueDate}>{saving ? 'Saving…' : 'Save Result'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit Inspection modal ── */}
      {viewing && (
        <Modal title={editing ? 'Edit Inspection' : 'Inspection Details'} onClose={() => { setViewing(null); setEditing(false) }} width={620}>
          {!editing ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewing.equipment}</span>
                  <Badge variant={inspectionStatusVariant(INSPECTION_STATUSES[viewing.status] ?? viewing.status, viewing.dueDate)}>{INSPECTION_STATUSES[viewing.status] ?? viewing.status}</Badge>
                </div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>{viewing.siteName ?? viewing.siteId}</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Due Date" value={fmt.date(viewing.dueDate)} />
                <MiniStat label="Last Inspected" value={viewing.lastInspectedAt ? fmt.date(viewing.lastInspectedAt) : '—'} />
                <MiniStat label="Inspector" value={viewing.inspectorName ?? '—'} />
              </div>

              <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>Certificate</div>
                {viewing.certificateUrl
                  ? <Btn size="sm" variant="ghost" onClick={() => setPreviewUrl(viewing.certificateUrl)}>👁 View certificate</Btn>
                  : <span style={{ fontSize: 13, color: T.mgrey }}>No certificate uploaded.</span>}
              </div>

              <p style={{ fontSize: 11, color: T.mgrey, margin: '4px 0 0' }}>Status changes via Record Result, not here.</p>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewing(null); setEditing(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditing(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Input label="Site / Location" value={editForm.site} onChange={v => setEditForm({ ...editForm, site: v })} required />
              <Input label="Equipment" value={editForm.equipment} onChange={v => setEditForm({ ...editForm, equipment: v })} required />
              <Input label="Due Date" type="date" value={editForm.dueDate} onChange={v => setEditForm({ ...editForm, dueDate: v })} required />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditing(false)}>Cancel</Btn>
                <Btn onClick={submitEdit} disabled={saving || !editForm.site || !editForm.equipment || !editForm.dueDate}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}

      {/* ── Certificate preview modal ── */}
      {previewUrl && (
        <Modal title="Inspection Certificate" onClose={() => setPreviewUrl(null)} width={800}>
          <DocumentPreview url={previewUrl} />
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 16, marginTop: 12 }}>
            <a href={previewUrl} download style={{ fontSize: 12, color: T.blue }}>⬇ Download</a>
            <a href={previewUrl} target="_blank" rel="noreferrer" style={{ fontSize: 12, color: T.blue }}>Open in new tab ↗</a>
          </div>
        </Modal>
      )}

      {/* ── Delete confirm modal ── */}
      {deleteTarget && (
        <Modal title="Delete Inspection" onClose={() => setDeleteTarget(null)} width={380}>
          <p style={{ fontSize: 13, color: T.dgrey }}>
            Are you sure you want to delete the <strong>"{deleteTarget.equipment}"</strong> inspection at "{deleteTarget.siteName ?? deleteTarget.siteId}"? This action cannot be undone.
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
