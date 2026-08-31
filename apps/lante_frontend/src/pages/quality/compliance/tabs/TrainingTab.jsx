import { useState, useEffect, useCallback } from 'react'
import api from '../../../../api/axios.js'
import { T, fmt } from '../../../../theme/tokens.js'
import { Card, Btn, HelpPanel, SectionHeader, DataTable, Modal, Input, FileInput, Select, Loading, DocumentPreview, MiniStat } from '../../../../components/ui.jsx'
import Pagination from '../../../../components/Pagination.jsx'
import { EMPTY_TRAINING, daysUntil, dueBadge } from '../constants.js'

const PAGE_SIZE = 20

export default function TrainingTab({ employees, employeeName, setMsg, refreshDashboard }) {
  const [trainings, setTrainings] = useState([])
  const [loading, setLoading] = useState(true)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [modal, setModal] = useState(null)
  const [saving, setSaving] = useState(false)
  const [trainingForm, setTrainingForm] = useState(EMPTY_TRAINING)
  const [uploading, setUploading] = useState(null)
  const [viewing, setViewing] = useState(null)
  const [editing, setEditing] = useState(false)
  const [editForm, setEditForm] = useState(EMPTY_TRAINING)
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
      const res = await api.get('/api/v1/compliance-abc-training', { params: { page, pageSize: PAGE_SIZE } }).catch(() => ({ data: { data: {} } }))
      const data = res.data?.data || {}
      setTrainings(data.items ?? [])
      setTotalCount(data.totalCount ?? 0)
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  async function submitTraining() {
    if (!trainingForm.employeeUserId) return
    setSaving(true)
    try {
      await api.post('/api/v1/compliance-abc-training', {
        employeeUserId: trainingForm.employeeUserId, employeeName: employeeName(trainingForm.employeeUserId),
        completedOn: new Date(trainingForm.completedOn || Date.now()).toISOString(),
        certificateUrl: trainingForm.certificateUrl || null,
      })
      setMsg({ type: 'success', text: 'Anti-bribery training record added — renewal due in 2 years.' })
      setTrainingForm(EMPTY_TRAINING); setModal(null); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to add training record.' })
    } finally { setSaving(false) }
  }

  function openView(t, startEditing = false) {
    setViewing(t)
    setEditing(startEditing)
    setEditForm({
      completedOn: t.completedOn ? t.completedOn.slice(0, 10) : '',
      certificateUrl: t.certificateUrl ?? '',
    })
  }

  async function submitEdit() {
    if (!viewing || !editForm.completedOn) return
    setSaving(true)
    try {
      await api.put(`/api/v1/compliance-abc-training/${viewing.id}`, {
        completedOn: new Date(editForm.completedOn).toISOString(),
        certificateUrl: editForm.certificateUrl || null,
      })
      setMsg({ type: 'success', text: 'Training record updated.' })
      setViewing(null); setEditing(false); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update training record.' })
    } finally { setSaving(false) }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About the Anti-Bribery &amp; Corruption Training Tracker">
        Tracks completion of anti-bribery and corruption training on a 2-year renewal cycle — the
        "Renewal Due" date is computed automatically from the completion date, so it doesn't need
        to be set manually. Click any row to see the full record and edit its details.
      </HelpPanel>

      <SectionHeader title="Anti-Bribery & Corruption Training Tracker" sub="2-year renewal cycle" action={<Btn onClick={() => setModal('training')}>+ Add Record</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Employee', 'Completed', 'Renewal Due', 'Status', 'Actions']}
          empty="No anti-bribery training records yet."
          rows={trainings.map(t => [
            <span onClick={() => openView(t)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{t.employeeName ?? t.employeeUserId}</span>,
            fmt.date(t.completedOn),
            fmt.date(t.nextDueOn),
            dueBadge(daysUntil(t.nextDueOn), 'Current'),
            <div style={{ display: 'flex', gap: 4 }}>
              <Btn variant="ghost" size="sm" onClick={() => openView(t)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={() => openView(t, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
            </div>,
          ])}
        />
      </Card>
      <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />

      {modal === 'training' && (
        <Modal title="Add Anti-Bribery Training Record" onClose={() => setModal(null)}>
          <Select label="Employee" value={trainingForm.employeeUserId} onChange={v => setTrainingForm({ ...trainingForm, employeeUserId: v })} options={[{ value: '', label: '— Select employee —' }, ...employees]} required />
          <Input label="Completed On" type="date" value={trainingForm.completedOn} onChange={v => setTrainingForm({ ...trainingForm, completedOn: v })} />
          <FileInput label="Certificate" accept=".pdf,.jpg,.jpeg,.png,.webp"
            uploading={uploading === 'training'} fileUrl={trainingForm.certificateUrl}
            onFileSelected={async file => { const url = await uploadComplianceFile('training', file); if (url) setTrainingForm(f => ({ ...f, certificateUrl: url })) }} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitTraining} disabled={saving || !!uploading || !trainingForm.employeeUserId}>{saving ? 'Saving…' : 'Add Record'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit Training modal ── */}
      {viewing && (
        <Modal title={editing ? 'Edit Training Record' : 'Training Record Details'} onClose={() => { setViewing(null); setEditing(false) }} width={620}>
          {!editing ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewing.employeeName ?? viewing.employeeUserId}</span>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>Anti-Bribery & Corruption Training</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Completed On" value={fmt.date(viewing.completedOn)} />
                <MiniStat label="Next Due On" value={fmt.date(viewing.nextDueOn)} />
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
              <div style={{ marginBottom: 12 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 3 }}>Employee</div>
                <div style={{ fontSize: 13, color: T.dgrey }}>{viewing.employeeName ?? viewing.employeeUserId}</div>
              </div>
              <Input label="Completed On" type="date" value={editForm.completedOn} onChange={v => setEditForm({ ...editForm, completedOn: v })} required />
              <FileInput label="Certificate" accept=".pdf,.jpg,.jpeg,.png,.webp"
                uploading={uploading === 'training'} fileUrl={editForm.certificateUrl}
                onFileSelected={async file => { const url = await uploadComplianceFile('training', file); if (url) setEditForm(f => ({ ...f, certificateUrl: url })) }} />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditing(false)}>Cancel</Btn>
                <Btn onClick={submitEdit} disabled={saving || !!uploading || !editForm.completedOn}>{saving ? 'Saving…' : 'Save'}</Btn>
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
    </>
  )
}
