import { useState, useEffect, useCallback } from 'react'
import api from '../../../api/axios.js'
import { T, fmt } from '../../../theme/tokens.js'
import { Card, Btn, Badge, HelpPanel, SectionHeader, DataTable, Modal, Input, FileInput, Select, Loading, MiniStat, DocumentPreview } from '../../../components/ui.jsx'
import Pagination from '../../../components/Pagination.jsx'
import { EMPTY_TRAINING, daysUntil, dueBadge } from '../constants.js'

const empty = { data: { data: {} } }

const PAGE_SIZE = 20

export default function TrainingTab({ employees, setMsg, refreshDashboard }) {
  const [trainingRecords, setTrainingRecords] = useState([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [uploading, setUploading] = useState(null)
  const [modal, setModal] = useState(null)
  const [trainingForm, setTrainingForm] = useState(EMPTY_TRAINING)
  const [viewing, setViewing] = useState(null)
  const [editing, setEditing] = useState(false)
  const [editForm, setEditForm] = useState(EMPTY_TRAINING)
  const [previewUrl, setPreviewUrl] = useState(null)
  const [deleteTarget, setDeleteTarget] = useState(null)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.get('/api/v1/hse-training-records', { params: { page, pageSize: PAGE_SIZE } }).catch(() => empty)
      const data = res.data?.data ?? {}
      setTrainingRecords(data.items ?? [])
      setTotalCount(data.totalCount ?? 0)
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  function employeeName(id) { return employees.find(e => e.value === id)?.label ?? id }

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

  async function submitTraining() {
    if (!trainingForm.employeeUserId || !trainingForm.course) return
    setSaving(true)
    try {
      await api.post('/api/v1/hse-training-records', {
        employeeUserId: trainingForm.employeeUserId, employeeName: employeeName(trainingForm.employeeUserId),
        course: trainingForm.course,
        completedOn: new Date(trainingForm.completedOn || Date.now()).toISOString(),
        expiresOn: trainingForm.expiresOn ? new Date(trainingForm.expiresOn).toISOString() : null,
        certificateUrl: trainingForm.certificateUrl || null,
      })
      setMsg({ type: 'success', text: 'Training record added.' })
      setTrainingForm(EMPTY_TRAINING); setModal(null); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to add training record.' })
    } finally { setSaving(false) }
  }

  function openView(t, startEditing = false) {
    setViewing(t)
    setEditing(startEditing)
    setEditForm({
      employeeUserId: t.employeeUserId, course: t.course,
      completedOn: t.completedOn ? t.completedOn.slice(0, 10) : '',
      expiresOn: t.expiresOn ? t.expiresOn.slice(0, 10) : '',
      certificateUrl: t.certificateUrl ?? '',
    })
  }

  async function submitEdit() {
    if (!viewing || !editForm.course) return
    setSaving(true)
    try {
      await api.put(`/api/v1/hse-training-records/${viewing.id}`, {
        course: editForm.course,
        completedOn: new Date(editForm.completedOn).toISOString(),
        expiresOn: editForm.expiresOn ? new Date(editForm.expiresOn).toISOString() : null,
        certificateUrl: editForm.certificateUrl || null,
      })
      setMsg({ type: 'success', text: 'Training record updated.' })
      setViewing(null); setEditing(false); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update training record.' })
    } finally { setSaving(false) }
  }

  async function confirmDelete() {
    if (!deleteTarget) return
    try {
      await api.delete(`/api/v1/hse-training-records/${deleteTarget.id}`)
      setMsg({ type: 'success', text: 'Training record deleted.' })
      load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to delete training record.' })
    } finally {
      setDeleteTarget(null)
    }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About the Training Register">
        Track completed HSE training (first aid, fire safety, working at heights, etc.) and
        certificate expiry per employee. Click a row to see the full record and certificate, and to
        edit the course, dates or certificate. The employee can't be changed after the record is
        created — add a new record if it was logged against the wrong person.
      </HelpPanel>

      <SectionHeader title="HSE Training Register" action={<Btn onClick={() => setModal('training')}>+ Add Record</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Employee', 'Course', 'Completed', 'Expires', 'Status', 'Actions']}
          empty="No training records yet."
          rows={trainingRecords.map(t => [
            <span onClick={() => openView(t)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{t.employeeName ?? t.employeeUserId}</span>,
            t.course,
            fmt.date(t.completedOn),
            t.expiresOn ? fmt.date(t.expiresOn) : '—',
            t.expiresOn ? dueBadge(daysUntil(t.expiresOn), 'Valid') : <Badge variant="default">No expiry</Badge>,
            <div style={{ display: 'flex', gap: 4 }}>
              <Btn variant="ghost" size="sm" onClick={() => openView(t)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={() => openView(t, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
              <Btn variant="danger" size="sm" onClick={() => setDeleteTarget(t)} style={{ padding: '3px 10px', fontSize: 11 }}>Delete</Btn>
            </div>,
          ])}
        />
        <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />
      </Card>

      {/* ── Add Training Record modal ── */}
      {modal === 'training' && (
        <Modal title="Add HSE Training Record" onClose={() => setModal(null)}>
          <Select label="Employee" value={trainingForm.employeeUserId} onChange={v => setTrainingForm({ ...trainingForm, employeeUserId: v })} options={[{ value: '', label: '— Select employee —' }, ...employees]} required />
          <Input label="Course" value={trainingForm.course} onChange={v => setTrainingForm({ ...trainingForm, course: v })} required placeholder="e.g. First Aid, Working at Heights" />
          <Input label="Completed On" type="date" value={trainingForm.completedOn} onChange={v => setTrainingForm({ ...trainingForm, completedOn: v })} />
          <Input label="Expires On" type="date" value={trainingForm.expiresOn} onChange={v => setTrainingForm({ ...trainingForm, expiresOn: v })} />
          <FileInput label="Certificate" accept=".pdf,.jpg,.jpeg,.png,.webp"
            uploading={uploading === 'training'} fileUrl={trainingForm.certificateUrl}
            onFileSelected={async file => { const url = await uploadHseFile('training', file); if (url) setTrainingForm(f => ({ ...f, certificateUrl: url })) }} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitTraining} disabled={saving || !!uploading || !trainingForm.employeeUserId || !trainingForm.course}>{saving ? 'Saving…' : 'Add Record'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit Training Record modal ── */}
      {viewing && (
        <Modal title={editing ? 'Edit Training Record' : 'Training Record Details'} onClose={() => { setViewing(null); setEditing(false) }} width={620}>
          {!editing ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewing.employeeName ?? viewing.employeeUserId}</span>
                  {viewing.expiresOn ? dueBadge(daysUntil(viewing.expiresOn), 'Valid') : <Badge variant="default">No expiry</Badge>}
                </div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>{viewing.course}</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Completed On" value={fmt.date(viewing.completedOn)} />
                <MiniStat label="Expires On" value={viewing.expiresOn ? fmt.date(viewing.expiresOn) : '—'} />
              </div>

              <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>Certificate</div>
                {viewing.certificateUrl
                  ? <Btn size="sm" variant="ghost" onClick={() => setPreviewUrl(viewing.certificateUrl)}>👁 View certificate</Btn>
                  : <span style={{ fontSize: 13, color: T.mgrey }}>No certificate uploaded.</span>}
              </div>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewing(null); setEditing(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditing(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Input label="Course" value={editForm.course} onChange={v => setEditForm({ ...editForm, course: v })} required />
              <Input label="Completed On" type="date" value={editForm.completedOn} onChange={v => setEditForm({ ...editForm, completedOn: v })} />
              <Input label="Expires On" type="date" value={editForm.expiresOn} onChange={v => setEditForm({ ...editForm, expiresOn: v })} />
              <FileInput label="Certificate" accept=".pdf,.jpg,.jpeg,.png,.webp"
                uploading={uploading === 'training'} fileUrl={editForm.certificateUrl}
                onFileSelected={async file => { const url = await uploadHseFile('training', file); if (url) setEditForm(f => ({ ...f, certificateUrl: url })) }} />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditing(false)}>Cancel</Btn>
                <Btn onClick={submitEdit} disabled={saving || !!uploading || !editForm.course}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}

      {/* ── Certificate preview modal ── */}
      {previewUrl && (
        <Modal title="Training Certificate" onClose={() => setPreviewUrl(null)} width={800}>
          <DocumentPreview url={previewUrl} />
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 16, marginTop: 12 }}>
            <a href={previewUrl} download style={{ fontSize: 12, color: T.blue }}>⬇ Download</a>
            <a href={previewUrl} target="_blank" rel="noreferrer" style={{ fontSize: 12, color: T.blue }}>Open in new tab ↗</a>
          </div>
        </Modal>
      )}

      {/* ── Delete confirm modal ── */}
      {deleteTarget && (
        <Modal title="Delete Training Record" onClose={() => setDeleteTarget(null)} width={380}>
          <p style={{ fontSize: 13, color: T.dgrey }}>
            Are you sure you want to delete the <strong>"{deleteTarget.course}"</strong> record for "{deleteTarget.employeeName ?? deleteTarget.employeeUserId}"? This action cannot be undone.
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
