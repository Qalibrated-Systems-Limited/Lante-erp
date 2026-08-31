import { useState, useEffect, useCallback } from 'react'
import api from '../../../../api/axios.js'
import { T, fmt } from '../../../../theme/tokens.js'
import { Card, Btn, Badge, HelpPanel, SectionHeader, DataTable, Modal, Input, Select, Loading, MiniStat } from '../../../../components/ui.jsx'
import Pagination from '../../../../components/Pagination.jsx'
import { DSR_TYPES, DSR_STATUSES, EMPTY_DSR, EMPTY_DSR_UPDATE, daysUntil, dueBadge } from '../constants.js'

const dsrStatusVariant = s => s === 'Completed' ? 'green' : s === 'Overdue' ? 'red' : 'amber'
const PAGE_SIZE = 20

export default function DsrTab({ setMsg, refreshDashboard }) {
  const [dsrs, setDsrs] = useState([])
  const [loading, setLoading] = useState(true)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [modal, setModal] = useState(null)
  const [saving, setSaving] = useState(false)
  const [dsrForm, setDsrForm] = useState(EMPTY_DSR)
  const [dsrUpdateForm, setDsrUpdateForm] = useState(EMPTY_DSR_UPDATE)
  const [activeId, setActiveId] = useState(null)
  const [viewing, setViewing] = useState(null)
  const [editing, setEditing] = useState(false)
  const [editForm, setEditForm] = useState(EMPTY_DSR)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.get('/api/v1/compliance-dsr', { params: { page, pageSize: PAGE_SIZE } }).catch(() => ({ data: { data: {} } }))
      const data = res.data?.data || {}
      setDsrs(data.items ?? [])
      setTotalCount(data.totalCount ?? 0)
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  async function submitDsr() {
    if (!dsrForm.requestorName) return
    setSaving(true)
    try {
      await api.post('/api/v1/compliance-dsr', {
        type: DSR_TYPES.indexOf(dsrForm.type), requestorName: dsrForm.requestorName,
        requestorContact: dsrForm.requestorContact || null,
        receivedOn: new Date(dsrForm.receivedOn || Date.now()).toISOString(),
      })
      setMsg({ type: 'success', text: 'Data subject request logged — due within 30 days.' })
      setDsrForm(EMPTY_DSR); setModal(null); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to log request.' })
    } finally { setSaving(false) }
  }

  function openDsrUpdate(d) {
    setActiveId(d.id)
    setDsrUpdateForm({ status: DSR_STATUSES[d.status] ?? 'Open', notes: d.notes ?? '' })
    setModal('dsrUpdate')
  }

  async function submitDsrUpdate() {
    setSaving(true)
    try {
      await api.patch(`/api/v1/compliance-dsr/${activeId}`, {
        status: DSR_STATUSES.indexOf(dsrUpdateForm.status), notes: dsrUpdateForm.notes || null,
      })
      setMsg({ type: 'success', text: 'Request updated.' })
      setModal(null); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update request.' })
    } finally { setSaving(false) }
  }

  function openView(d, startEditing = false) {
    setViewing(d)
    setEditing(startEditing)
    setEditForm({
      type: DSR_TYPES[d.type] ?? d.type,
      requestorName: d.requestorName,
      requestorContact: d.requestorContact ?? '',
      receivedOn: d.receivedOn ? d.receivedOn.slice(0, 10) : '',
    })
  }

  async function submitEdit() {
    if (!viewing || !editForm.requestorName) return
    setSaving(true)
    try {
      await api.put(`/api/v1/compliance-dsr/${viewing.id}`, {
        type: DSR_TYPES.indexOf(editForm.type),
        requestorName: editForm.requestorName,
        requestorContact: editForm.requestorContact || null,
        receivedOn: new Date(editForm.receivedOn || Date.now()).toISOString(),
      })
      setMsg({ type: 'success', text: 'Request details updated.' })
      setViewing(null); setEditing(false); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update request details.' })
    } finally { setSaving(false) }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About the Data Subject Request Register">
        Tracks access, erasure, and correction requests under Kenya's Data Protection Act, 2019.
        Each request gets a 30-day due date computed automatically from when it was received.
        Click any row to see the full record and edit its details (type, requestor, received
        date) — status, notes, and completion move forward via the existing <strong>Update</strong> action,
        not here.
      </HelpPanel>

      <SectionHeader title="Data Subject Request Register" sub="Access / erasure / correction — respond within 30 days per DPA 2019" action={<Btn onClick={() => setModal('dsr')}>+ Log Request</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Requestor', 'Type', 'Received', 'Due By', 'Status', 'Actions']}
          empty="No data subject requests yet."
          rows={dsrs.map(d => [
            <span onClick={() => openView(d)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{d.requestorName}</span>,
            <Badge variant="navy">{DSR_TYPES[d.type] ?? d.type}</Badge>,
            fmt.date(d.receivedOn),
            dueBadge(daysUntil(d.dueBy)),
            <Badge variant={dsrStatusVariant(DSR_STATUSES[d.status] ?? d.status)}>{DSR_STATUSES[d.status] ?? d.status}</Badge>,
            <div style={{ display: 'flex', gap: 4, alignItems: 'center', flexWrap: 'wrap' }}>
              <Btn variant="ghost" size="sm" onClick={() => openView(d)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={() => openView(d, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
              <Btn size="sm" onClick={() => openDsrUpdate(d)}>Update</Btn>
            </div>,
          ])}
        />
      </Card>
      <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />

      {modal === 'dsr' && (
        <Modal title="Log Data Subject Request" onClose={() => setModal(null)}>
          <Select label="Type" value={dsrForm.type} onChange={v => setDsrForm({ ...dsrForm, type: v })} options={DSR_TYPES.map(t => ({ value: t, label: t }))} />
          <Input label="Requestor Name" value={dsrForm.requestorName} onChange={v => setDsrForm({ ...dsrForm, requestorName: v })} required />
          <Input label="Requestor Contact" value={dsrForm.requestorContact} onChange={v => setDsrForm({ ...dsrForm, requestorContact: v })} />
          <Input label="Received On" type="date" value={dsrForm.receivedOn} onChange={v => setDsrForm({ ...dsrForm, receivedOn: v })} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitDsr} disabled={saving || !dsrForm.requestorName}>{saving ? 'Saving…' : 'Log Request'}</Btn>
          </div>
        </Modal>
      )}

      {modal === 'dsrUpdate' && (
        <Modal title="Update Data Subject Request" onClose={() => setModal(null)}>
          <Select label="Status" value={dsrUpdateForm.status} onChange={v => setDsrUpdateForm({ ...dsrUpdateForm, status: v })} options={DSR_STATUSES.map(s => ({ value: s, label: s }))} />
          <Input label="Notes" value={dsrUpdateForm.notes} onChange={v => setDsrUpdateForm({ ...dsrUpdateForm, notes: v })} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitDsrUpdate} disabled={saving}>{saving ? 'Saving…' : 'Save'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit DSR modal ── */}
      {viewing && (
        <Modal title={editing ? 'Edit Data Subject Request' : 'Data Subject Request Details'} onClose={() => { setViewing(null); setEditing(false) }} width={620}>
          {!editing ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewing.requestorName}</span>
                  <Badge variant="navy">{DSR_TYPES[viewing.type] ?? viewing.type}</Badge>
                </div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>
                  Received {fmt.date(viewing.receivedOn)}{viewing.requestorContact ? ` · ${viewing.requestorContact}` : ''}
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Due By" value={dueBadge(daysUntil(viewing.dueBy))} />
                <MiniStat label="Status" value={<Badge variant={dsrStatusVariant(DSR_STATUSES[viewing.status] ?? viewing.status)}>{DSR_STATUSES[viewing.status] ?? viewing.status}</Badge>} />
                <MiniStat label="Completed On" value={viewing.completedOn ? fmt.date(viewing.completedOn) : '—'} />
              </div>

              <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>Notes</div>
                <div style={{ fontSize: 13, color: T.dgrey }}>{viewing.notes || 'No notes.'}</div>
              </div>

              <p style={{ fontSize: 11, color: T.mgrey, margin: '4px 0 0' }}>Status changes via Update, not here.</p>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewing(null); setEditing(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditing(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Select label="Type" value={editForm.type} onChange={v => setEditForm({ ...editForm, type: v })} options={DSR_TYPES.map(t => ({ value: t, label: t }))} />
              <Input label="Requestor Name" value={editForm.requestorName} onChange={v => setEditForm({ ...editForm, requestorName: v })} required />
              <Input label="Requestor Contact" value={editForm.requestorContact} onChange={v => setEditForm({ ...editForm, requestorContact: v })} />
              <Input label="Received On" type="date" value={editForm.receivedOn} onChange={v => setEditForm({ ...editForm, receivedOn: v })} />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditing(false)}>Cancel</Btn>
                <Btn onClick={submitEdit} disabled={saving || !editForm.requestorName}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}
    </>
  )
}
