import { useState, useEffect, useCallback } from 'react'
import api from '../../../../api/axios.js'
import { T, fmt } from '../../../../theme/tokens.js'
import { Card, Btn, Badge, HelpPanel, SectionHeader, DataTable, Modal, Input, Select, Loading, MiniStat } from '../../../../components/ui.jsx'
import Pagination from '../../../../components/Pagination.jsx'
import { COI_STATUSES, EMPTY_COI } from '../constants.js'

const coiStatusVariant = s => s === 'Reviewed' ? 'green' : s === 'Submitted' ? 'amber' : 'default'
const PAGE_SIZE = 20

export default function CoiTab({ employees, employeeName, setMsg, refreshDashboard }) {
  const [coiDeclarations, setCoiDeclarations] = useState([])
  const [loading, setLoading] = useState(true)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [modal, setModal] = useState(null)
  const [saving, setSaving] = useState(false)
  const [coiForm, setCoiForm] = useState(EMPTY_COI)
  const [viewing, setViewing] = useState(null)
  const [editing, setEditing] = useState(false)
  const [editForm, setEditForm] = useState(EMPTY_COI)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.get('/api/v1/compliance-coi', { params: { page, pageSize: PAGE_SIZE } }).catch(() => ({ data: { data: {} } }))
      const data = res.data?.data || {}
      setCoiDeclarations(data.items ?? [])
      setTotalCount(data.totalCount ?? 0)
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  async function submitCoi() {
    if (!coiForm.employeeUserId) return
    setSaving(true)
    try {
      await api.post('/api/v1/compliance-coi', {
        employeeUserId: coiForm.employeeUserId, employeeName: employeeName(coiForm.employeeUserId),
        year: Number(coiForm.year), hasConflict: coiForm.hasConflict, details: coiForm.details || null,
      })
      setMsg({ type: 'success', text: 'COI declaration submitted.' })
      setCoiForm(EMPTY_COI); setModal(null); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to submit declaration.' })
    } finally { setSaving(false) }
  }

  async function reviewCoi(id, statusLabel) {
    try {
      await api.patch(`/api/v1/compliance-coi/${id}/review`, { status: COI_STATUSES.indexOf(statusLabel) })
      load(); refreshDashboard()
    } catch {
      setMsg({ type: 'error', text: 'Failed to update declaration.' })
    }
  }

  function openView(c, startEditing = false) {
    setViewing(c)
    setEditing(startEditing)
    setEditForm({
      employeeUserId: c.employeeUserId,
      year: c.year,
      hasConflict: !!c.hasConflict,
      details: c.details ?? '',
    })
  }

  async function submitEdit() {
    if (!viewing || !editForm.employeeUserId) return
    setSaving(true)
    try {
      await api.put(`/api/v1/compliance-coi/${viewing.id}`, {
        employeeUserId: editForm.employeeUserId, employeeName: employeeName(editForm.employeeUserId),
        year: Number(editForm.year), hasConflict: editForm.hasConflict, details: editForm.details || null,
      })
      setMsg({ type: 'success', text: 'COI declaration updated.' })
      setViewing(null); setEditing(false); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update declaration.' })
    } finally { setSaving(false) }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About the Conflict of Interest Register">
        This is the annual Conflict of Interest (COI) declaration register for Department Heads and
        above. Every declaration is reviewed each January. Click any row to see the full record and
        edit its details.
        <br /><br />
        Status moves through <strong>Pending → Submitted → Reviewed</strong> via the "Mark Reviewed"
        action already in this list — it isn't editable from the record view.
      </HelpPanel>

      <SectionHeader title="Conflict of Interest Register" sub="Annual declarations by Department Heads and above" action={<Btn onClick={() => setModal('coi')}>+ New Declaration</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Employee', 'Year', 'Has Conflict', 'Status', 'Actions']}
          empty="No COI declarations yet."
          rows={coiDeclarations.map(c => [
            <span onClick={() => openView(c)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{c.employeeName ?? c.employeeUserId}</span>,
            c.year,
            c.hasConflict ? <Badge variant="red">Yes</Badge> : <Badge variant="green">No</Badge>,
            <Badge variant={coiStatusVariant(COI_STATUSES[c.status] ?? c.status)}>{COI_STATUSES[c.status] ?? c.status}</Badge>,
            <div style={{ display: 'flex', gap: 4, alignItems: 'center', flexWrap: 'wrap' }}>
              <Btn variant="ghost" size="sm" onClick={() => openView(c)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={() => openView(c, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
              {c.status !== 2 && <Btn size="sm" onClick={() => reviewCoi(c.id, 'Reviewed')}>Mark Reviewed</Btn>}
            </div>,
          ])}
        />
      </Card>
      <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />

      {modal === 'coi' && (
        <Modal title="New Conflict of Interest Declaration" onClose={() => setModal(null)}>
          <Select label="Employee" value={coiForm.employeeUserId} onChange={v => setCoiForm({ ...coiForm, employeeUserId: v })} options={[{ value: '', label: '— Select employee —' }, ...employees]} required />
          <Input label="Year" type="number" value={coiForm.year} onChange={v => setCoiForm({ ...coiForm, year: v })} />
          <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 14 }}>
            <input type="checkbox" checked={coiForm.hasConflict} onChange={e => setCoiForm({ ...coiForm, hasConflict: e.target.checked })} />
            I have a conflict of interest to declare
          </label>
          <Input label="Details" value={coiForm.details} onChange={v => setCoiForm({ ...coiForm, details: v })} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitCoi} disabled={saving || !coiForm.employeeUserId}>{saving ? 'Submitting…' : 'Submit Declaration'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit COI modal ── */}
      {viewing && (
        <Modal title={editing ? 'Edit COI Declaration' : 'COI Declaration Details'} onClose={() => { setViewing(null); setEditing(false) }} width={620}>
          {!editing ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewing.employeeName ?? viewing.employeeUserId}</span>
                  <Badge variant={coiStatusVariant(COI_STATUSES[viewing.status] ?? viewing.status)}>{COI_STATUSES[viewing.status] ?? viewing.status}</Badge>
                </div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>Year {viewing.year}</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Has Conflict" value={viewing.hasConflict ? <Badge variant="red">Yes</Badge> : <Badge variant="green">No</Badge>} />
                <MiniStat label="Declared On" value={viewing.declaredOn ? fmt.date(viewing.declaredOn) : '—'} />
              </div>

              <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>Details</div>
                <div style={{ fontSize: 13, color: T.dgrey }}>{viewing.details || 'No details.'}</div>
              </div>

              <p style={{ fontSize: 11, color: T.mgrey, margin: '4px 0 0' }}>Status changes via Mark Reviewed, not here.</p>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewing(null); setEditing(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditing(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Select label="Employee" value={editForm.employeeUserId} onChange={v => setEditForm({ ...editForm, employeeUserId: v })} options={[{ value: '', label: '— Select employee —' }, ...employees]} required />
              <Input label="Year" type="number" value={editForm.year} onChange={v => setEditForm({ ...editForm, year: v })} />
              <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 14 }}>
                <input type="checkbox" checked={editForm.hasConflict} onChange={e => setEditForm({ ...editForm, hasConflict: e.target.checked })} />
                I have a conflict of interest to declare
              </label>
              <Input label="Details" value={editForm.details} onChange={v => setEditForm({ ...editForm, details: v })} />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditing(false)}>Cancel</Btn>
                <Btn onClick={submitEdit} disabled={saving || !editForm.employeeUserId}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}
    </>
  )
}
