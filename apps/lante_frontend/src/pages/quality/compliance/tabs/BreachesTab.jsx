import { useState, useEffect, useCallback } from 'react'
import api from '../../../../api/axios.js'
import { T, fmt } from '../../../../theme/tokens.js'
import { Card, Btn, Badge, HelpPanel, SectionHeader, DataTable, Modal, Input, Select, Loading, MiniStat } from '../../../../components/ui.jsx'
import Pagination from '../../../../components/Pagination.jsx'
import { BREACH_STATUSES, EMPTY_BREACH, EMPTY_BREACH_UPDATE, dueBadge } from '../constants.js'

const breachStatusVariant = s => s === 'Closed' ? 'green' : s === 'Notified' ? 'blue' : s === 'Contained' ? 'amber' : 'red'
const PAGE_SIZE = 20

export default function BreachesTab({ setMsg, refreshDashboard }) {
  const [breaches, setBreaches] = useState([])
  const [loading, setLoading] = useState(true)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [modal, setModal] = useState(null)
  const [saving, setSaving] = useState(false)
  const [breachForm, setBreachForm] = useState(EMPTY_BREACH)
  const [breachUpdateForm, setBreachUpdateForm] = useState(EMPTY_BREACH_UPDATE)
  const [activeId, setActiveId] = useState(null)
  const [viewing, setViewing] = useState(null)
  const [editing, setEditing] = useState(false)
  const [editForm, setEditForm] = useState(EMPTY_BREACH)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.get('/api/v1/compliance-data-breaches', { params: { page, pageSize: PAGE_SIZE } }).catch(() => ({ data: { data: {} } }))
      const data = res.data?.data || {}
      setBreaches(data.items ?? [])
      setTotalCount(data.totalCount ?? 0)
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  async function submitBreach() {
    if (!breachForm.description) return
    setSaving(true)
    try {
      await api.post('/api/v1/compliance-data-breaches', {
        occurredAt: new Date(breachForm.occurredAt || Date.now()).toISOString(),
        discoveredAt: new Date(breachForm.discoveredAt || Date.now()).toISOString(),
        description: breachForm.description,
      })
      setMsg({ type: 'success', text: 'Breach logged — ODPC notification due within 72 hours of discovery.' })
      setBreachForm(EMPTY_BREACH); setModal(null); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to log breach.' })
    } finally { setSaving(false) }
  }

  function openBreachUpdate(b) {
    setActiveId(b.id)
    setBreachUpdateForm({
      status: BREACH_STATUSES[b.status] ?? 'Open',
      odpcNotifiedAt: b.odpcNotifiedAt ? b.odpcNotifiedAt.slice(0, 10) : '',
      remediationNotes: b.remediationNotes ?? '',
    })
    setModal('breachUpdate')
  }

  async function submitBreachUpdate() {
    setSaving(true)
    try {
      await api.patch(`/api/v1/compliance-data-breaches/${activeId}`, {
        status: BREACH_STATUSES.indexOf(breachUpdateForm.status),
        odpcNotifiedAt: breachUpdateForm.odpcNotifiedAt ? new Date(breachUpdateForm.odpcNotifiedAt).toISOString() : null,
        remediationNotes: breachUpdateForm.remediationNotes || null,
      })
      setMsg({ type: 'success', text: 'Breach record updated.' })
      setModal(null); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update breach record.' })
    } finally { setSaving(false) }
  }

  function openView(b, startEditing = false) {
    setViewing(b)
    setEditing(startEditing)
    setEditForm({
      occurredAt: b.occurredAt ? b.occurredAt.slice(0, 10) : '',
      discoveredAt: b.discoveredAt ? b.discoveredAt.slice(0, 10) : '',
      description: b.description ?? '',
    })
  }

  async function submitEdit() {
    if (!viewing || !editForm.description) return
    setSaving(true)
    try {
      await api.put(`/api/v1/compliance-data-breaches/${viewing.id}`, {
        occurredAt: new Date(editForm.occurredAt || Date.now()).toISOString(),
        discoveredAt: new Date(editForm.discoveredAt || Date.now()).toISOString(),
        description: editForm.description,
      })
      setMsg({ type: 'success', text: 'Breach details updated.' })
      setViewing(null); setEditing(false); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update breach details.' })
    } finally { setSaving(false) }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About the Data Breach Log">
        Logs data breaches and tracks the 72-hour ODPC (Office of the Data Protection
        Commissioner) notification deadline, computed automatically from when the breach was
        discovered. Click any row to see the full record and edit its details (occurred/discovered
        dates, description) — status, ODPC notification, and remediation notes move forward via
        the existing <strong>Update</strong> action, not here.
      </HelpPanel>

      <SectionHeader title="Data Breach Log" sub="72-hour ODPC notification timer from discovery" action={<Btn onClick={() => setModal('breach')}>+ Log Breach</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Discovered', 'Description', 'ODPC Due', 'Notified', 'Status', 'Actions']}
          empty="No data breaches logged."
          rows={breaches.map(b => [
            fmt.date(b.discoveredAt),
            <div style={{ maxWidth: 260 }}>
              <span onClick={() => openView(b)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{b.description}</span>
            </div>,
            dueBadge(Math.floor((new Date(b.odpcNotificationDueAt) - new Date()) / 3600000 / 24), 'On track'),
            b.odpcNotifiedAt ? fmt.date(b.odpcNotifiedAt) : <Badge variant="red">Not notified</Badge>,
            <Badge variant={breachStatusVariant(BREACH_STATUSES[b.status] ?? b.status)}>{BREACH_STATUSES[b.status] ?? b.status}</Badge>,
            <div style={{ display: 'flex', gap: 4, alignItems: 'center', flexWrap: 'wrap' }}>
              <Btn variant="ghost" size="sm" onClick={() => openView(b)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={() => openView(b, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
              <Btn size="sm" onClick={() => openBreachUpdate(b)}>Update</Btn>
            </div>,
          ])}
        />
      </Card>
      <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />

      {modal === 'breach' && (
        <Modal title="Log Data Breach" onClose={() => setModal(null)}>
          <Input label="Occurred At" type="date" value={breachForm.occurredAt} onChange={v => setBreachForm({ ...breachForm, occurredAt: v })} />
          <Input label="Discovered At" type="date" value={breachForm.discoveredAt} onChange={v => setBreachForm({ ...breachForm, discoveredAt: v })} />
          <Input label="Description" value={breachForm.description} onChange={v => setBreachForm({ ...breachForm, description: v })} required />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitBreach} disabled={saving || !breachForm.description}>{saving ? 'Saving…' : 'Log Breach'}</Btn>
          </div>
        </Modal>
      )}

      {modal === 'breachUpdate' && (
        <Modal title="Update Data Breach" onClose={() => setModal(null)}>
          <Select label="Status" value={breachUpdateForm.status} onChange={v => setBreachUpdateForm({ ...breachUpdateForm, status: v })} options={BREACH_STATUSES.map(s => ({ value: s, label: s }))} />
          <Input label="ODPC Notified On" type="date" value={breachUpdateForm.odpcNotifiedAt} onChange={v => setBreachUpdateForm({ ...breachUpdateForm, odpcNotifiedAt: v })} />
          <Input label="Remediation Notes" value={breachUpdateForm.remediationNotes} onChange={v => setBreachUpdateForm({ ...breachUpdateForm, remediationNotes: v })} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitBreachUpdate} disabled={saving}>{saving ? 'Saving…' : 'Save'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit Breach modal ── */}
      {viewing && (
        <Modal title={editing ? 'Edit Data Breach' : 'Data Breach Details'} onClose={() => { setViewing(null); setEditing(false) }} width={620}>
          {!editing ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewing.description}</span>
                  <Badge variant={breachStatusVariant(BREACH_STATUSES[viewing.status] ?? viewing.status)}>{BREACH_STATUSES[viewing.status] ?? viewing.status}</Badge>
                </div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>Discovered {fmt.date(viewing.discoveredAt)}</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Occurred At" value={fmt.date(viewing.occurredAt)} />
                <MiniStat label="ODPC Notification Due" value={fmt.date(viewing.odpcNotificationDueAt)} />
                <MiniStat label="ODPC Notified At" value={viewing.odpcNotifiedAt ? fmt.date(viewing.odpcNotifiedAt) : 'Not notified'} />
              </div>

              <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>Remediation Notes</div>
                <div style={{ fontSize: 13, color: T.dgrey }}>{viewing.remediationNotes || 'No notes.'}</div>
              </div>

              <p style={{ fontSize: 11, color: T.mgrey, margin: '4px 0 0' }}>Status changes via Update, not here.</p>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewing(null); setEditing(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditing(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Input label="Occurred At" type="date" value={editForm.occurredAt} onChange={v => setEditForm({ ...editForm, occurredAt: v })} />
              <Input label="Discovered At" type="date" value={editForm.discoveredAt} onChange={v => setEditForm({ ...editForm, discoveredAt: v })} />
              <Input label="Description" value={editForm.description} onChange={v => setEditForm({ ...editForm, description: v })} required />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditing(false)}>Cancel</Btn>
                <Btn onClick={submitEdit} disabled={saving || !editForm.description}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}
    </>
  )
}
