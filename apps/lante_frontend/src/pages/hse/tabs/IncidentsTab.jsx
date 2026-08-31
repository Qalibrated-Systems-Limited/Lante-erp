import { useState, useEffect, useCallback } from 'react'
import api from '../../../api/axios.js'
import { T, fmt } from '../../../theme/tokens.js'
import { Card, Btn, Badge, Alert, HelpPanel, SectionHeader, DataTable, Modal, Input, Select, Loading, MiniStat } from '../../../components/ui.jsx'
import Pagination from '../../../components/Pagination.jsx'
import { INCIDENT_TYPES, SEVERITIES, INCIDENT_STATUSES, CORRECTIVE_STATUSES, EMPTY_INCIDENT, EMPTY_CAPA } from '../constants.js'

const empty = { data: { data: {} } }

const PAGE_SIZE = 20

const typeVariant = t => t === 'Near Miss' ? 'amber' : t === 'First Aid' ? 'blue' : t === 'Lost Time Injury' ? 'red' : t === 'Medical Treatment' ? 'red' : 'green'
const capaVariant = s => s === 'Completed' ? 'green' : s === 'Overdue' ? 'red' : 'amber'

function Section({ label, children }) {
  return (
    <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
      <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>{label}</div>
      {children}
    </div>
  )
}

export default function IncidentsTab({ employees, setMsg, incidents, setIncidents, page, setPage, totalCount, setTotalCount, refreshDashboard }) {
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [modal, setModal] = useState(null)
  const [form, setForm] = useState(EMPTY_INCIDENT)
  const [viewing, setViewing] = useState(null)
  const [editing, setEditing] = useState(false)
  const [editForm, setEditForm] = useState(EMPTY_INCIDENT)
  const [statusDraft, setStatusDraft] = useState('Open')
  const [capaForm, setCapaForm] = useState(EMPTY_CAPA)
  const [addingCapa, setAddingCapa] = useState(false)
  const [deleteTarget, setDeleteTarget] = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.get('/api/v1/hse-incidents', { params: { page, pageSize: PAGE_SIZE } }).catch(() => empty)
      const data = res.data?.data ?? {}
      setIncidents(data.items ?? [])
      setTotalCount(data.totalCount ?? 0)
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  function employeeName(id) { return employees.find(e => e.value === id)?.label ?? id }

  async function submitIncident() {
    if (!form.site || !form.description) return
    setSaving(true)
    try {
      await api.post('/api/v1/hse-incidents', {
        siteId: form.site, siteName: form.site,
        type: INCIDENT_TYPES.indexOf(form.type),
        severity: SEVERITIES.indexOf(form.severity),
        occurredAt: new Date(form.occurredAt || Date.now()).toISOString(),
        description: form.description,
        isEnvironmental: form.isEnvironmental,
        nemaRef: form.isEnvironmental ? (form.nemaRef || null) : null,
        correctiveActionDescription: form.capaDescription || null,
        correctiveActionOwnerUserId: form.capaOwnerUserId || null,
        correctiveActionOwnerName: form.capaOwnerUserId ? employeeName(form.capaOwnerUserId) : null,
        correctiveActionDueDate: form.capaDueDate ? new Date(form.capaDueDate).toISOString() : null,
      })
      setMsg({ type: 'success', text: 'Incident reported. CAPA required within 48 hours.' })
      setForm(EMPTY_INCIDENT); setModal(null); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to report incident.' })
    } finally { setSaving(false) }
  }

  function openView(h, startEditing = false) {
    setViewing(h)
    setEditing(startEditing)
    setAddingCapa(false)
    setStatusDraft(INCIDENT_STATUSES[h.status] ?? INCIDENT_STATUSES[0])
    setCapaForm(EMPTY_CAPA)
    setEditForm({
      site: h.siteId, type: INCIDENT_TYPES[h.type] ?? INCIDENT_TYPES[0],
      severity: SEVERITIES[h.severity] ?? SEVERITIES[0],
      occurredAt: h.occurredAt ? h.occurredAt.slice(0, 10) : '',
      description: h.description,
    })
  }

  // Refetches the full incident (with CAPA/env details) and syncs it back into `viewing` +
  // the list, so the still-open modal reflects the change without a full reload flicker.
  async function refreshViewing(id) {
    const res = await api.get(`/api/v1/hse-incidents/${id}`).catch(() => null)
    const fresh = res?.data?.data
    if (fresh) {
      setViewing(fresh)
      setStatusDraft(INCIDENT_STATUSES[fresh.status] ?? INCIDENT_STATUSES[0])
    }
    load(); refreshDashboard()
  }

  async function submitEdit() {
    if (!viewing || !editForm.site || !editForm.description) return
    setSaving(true)
    try {
      await api.put(`/api/v1/hse-incidents/${viewing.id}`, {
        siteId: editForm.site, siteName: editForm.site,
        type: INCIDENT_TYPES.indexOf(editForm.type),
        severity: SEVERITIES.indexOf(editForm.severity),
        occurredAt: new Date(editForm.occurredAt).toISOString(),
        description: editForm.description,
      })
      setMsg({ type: 'success', text: 'Incident updated.' })
      setEditing(false); refreshViewing(viewing.id)
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update incident.' })
    } finally { setSaving(false) }
  }

  async function submitStatus() {
    if (!viewing) return
    if (statusDraft === 'Closed') {
      const openCapas = (viewing.correctiveActions ?? []).filter(c => c.status !== 2)
      if (openCapas.length > 0) {
        setMsg({ type: 'error', text: `Can't close — ${openCapas.length} corrective action${openCapas.length > 1 ? 's are' : ' is'} still not Completed.` })
        return
      }
    }
    setSaving(true)
    try {
      await api.patch(`/api/v1/hse-incidents/${viewing.id}/status`, { status: INCIDENT_STATUSES.indexOf(statusDraft) })
      setMsg({ type: 'success', text: 'Incident status updated.' })
      refreshViewing(viewing.id)
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update incident status.' })
    } finally { setSaving(false) }
  }

  async function completeCapa(capaId) {
    setSaving(true)
    try {
      const capa = viewing.correctiveActions.find(c => c.id === capaId)
      await api.patch(`/api/v1/hse-incidents/corrective-actions/${capaId}`, {
        description: capa.description, ownerUserId: capa.ownerUserId, ownerName: capa.ownerName,
        status: CORRECTIVE_STATUSES.indexOf('Completed'), dueDate: capa.dueDate,
      })
      setMsg({ type: 'success', text: 'Corrective action marked complete.' })
      refreshViewing(viewing.id)
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update corrective action.' })
    } finally { setSaving(false) }
  }

  async function submitCapa() {
    if (!viewing || !capaForm.description || !capaForm.dueDate) return
    setSaving(true)
    try {
      await api.post(`/api/v1/hse-incidents/${viewing.id}/corrective-actions`, {
        description: capaForm.description,
        ownerUserId: capaForm.ownerUserId || null,
        ownerName: capaForm.ownerUserId ? employeeName(capaForm.ownerUserId) : null,
        dueDate: new Date(capaForm.dueDate).toISOString(),
      })
      setMsg({ type: 'success', text: 'Corrective action added.' })
      setCapaForm(EMPTY_CAPA); setAddingCapa(false); refreshViewing(viewing.id)
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to add corrective action.' })
    } finally { setSaving(false) }
  }

  async function confirmDelete() {
    if (!deleteTarget) return
    try {
      await api.delete(`/api/v1/hse-incidents/${deleteTarget.id}`)
      setMsg({ type: 'success', text: 'Incident deleted.' })
      load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to delete incident.' })
    } finally {
      setDeleteTarget(null)
    }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About the Incident Register">
        Log every near miss, first aid case, medical treatment and lost-time injury here. Click any
        incident to see the full record — including CAPA (corrective action) history and
        environmental/NEMA details. From there you can move the incident through its status
        (Open → Under Investigation → CAPA Pending → Closed — any status can be set directly, it
        isn't a strict sequence), add further corrective actions, and mark existing ones complete.
        <br /><br />
        <strong>Report within 24 hours.</strong> CAPA (corrective action) is required within 48 hours.
      </HelpPanel>

      <SectionHeader title="Incident Register" action={<Btn onClick={() => setModal('inc')}>+ Report Incident</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Date', 'Site', 'Type', 'Description', 'Status', 'CAPA', 'Actions']}
          empty="No incidents reported."
          rows={incidents.map(h => {
            const typeLabel = INCIDENT_TYPES[h.type] ?? h.type
            const capas = h.correctiveActions ?? []
            const completedCount = capas.filter(c => c.status === 2).length
            return [
              fmt.date(h.occurredAt),
              <span onClick={() => openView(h)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{h.siteName ?? h.siteId}</span>,
              <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
                <Badge variant={typeVariant(typeLabel)}>{typeLabel}</Badge>
                {h.envIncident && <Badge variant="purple">Env</Badge>}
              </div>,
              <div style={{ maxWidth: 260 }}>{h.description}</div>,
              <Badge variant={h.status === 3 ? 'green' : 'amber'}>{INCIDENT_STATUSES[h.status] ?? h.status}</Badge>,
              capas.length === 0 ? '—' : <Badge variant={completedCount === capas.length ? 'green' : 'amber'}>{completedCount}/{capas.length} complete</Badge>,
              <div style={{ display: 'flex', gap: 4 }}>
                <Btn variant="ghost" size="sm" onClick={() => openView(h)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
                <Btn variant="ghost" size="sm" onClick={() => openView(h, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
                <Btn variant="danger" size="sm" onClick={() => setDeleteTarget(h)} style={{ padding: '3px 10px', fontSize: 11 }}>Delete</Btn>
              </div>,
            ]
          })}
        />
        <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />
      </Card>

      {/* ── Report Incident modal ── */}
      {modal === 'inc' && (
        <Modal title="Report Incident" onClose={() => setModal(null)}>
          <Alert type="warning">Report within 24 hours. CAPA required within 48 hours.</Alert>
          <Select label="Type" value={form.type} onChange={v => setForm({ ...form, type: v })} options={INCIDENT_TYPES.map(t => ({ value: t, label: t }))} />
          <Input label="Site / Location" value={form.site} onChange={v => setForm({ ...form, site: v })} required />
          <Select label="Severity" value={form.severity} onChange={v => setForm({ ...form, severity: v })} options={SEVERITIES.map(s => ({ value: s, label: s }))} />
          <Input label="Date Occurred" type="date" value={form.occurredAt} onChange={v => setForm({ ...form, occurredAt: v })} />
          <Input label="Description" value={form.description} onChange={v => setForm({ ...form, description: v })} required placeholder="What happened exactly?" />
          <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 14 }}>
            <input type="checkbox" checked={form.isEnvironmental} onChange={e => setForm({ ...form, isEnvironmental: e.target.checked })} />
            Environmental incident (spill, pollution event — NEMA notification tracking)
          </label>
          {form.isEnvironmental && <Input label="NEMA Reference" value={form.nemaRef} onChange={v => setForm({ ...form, nemaRef: v })} />}
          <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 10, marginTop: 4 }}>
            <p style={{ fontSize: 12, fontWeight: 700, color: T.navy, marginBottom: 8 }}>First Corrective Action (optional)</p>
            <Input label="Description" value={form.capaDescription} onChange={v => setForm({ ...form, capaDescription: v })} />
            <Select label="Owner" value={form.capaOwnerUserId} onChange={v => setForm({ ...form, capaOwnerUserId: v })} options={[{ value: '', label: '— Unassigned —' }, ...employees]} />
            <Input label="Due Date" type="date" value={form.capaDueDate} onChange={v => setForm({ ...form, capaDueDate: v })} />
          </div>
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitIncident} disabled={saving || !form.site || !form.description}>{saving ? 'Submitting…' : 'Submit Report'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit Incident modal ── */}
      {viewing && (
        <Modal title={editing ? 'Edit Incident' : 'Incident Details'} onClose={() => { setViewing(null); setEditing(false) }} width={620}>
          {!editing ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewing.siteName ?? viewing.siteId}</span>
                  <Badge variant={typeVariant(INCIDENT_TYPES[viewing.type] ?? viewing.type)}>{INCIDENT_TYPES[viewing.type] ?? viewing.type}</Badge>
                  {viewing.envIncident && <Badge variant="purple">Environmental</Badge>}
                </div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>Reported by {viewing.reportedByName ?? viewing.reportedByUserId}</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Severity" value={SEVERITIES[viewing.severity] ?? viewing.severity} />
                <MiniStat label="CAPAs" value={(viewing.correctiveActions ?? []).length === 0 ? 'None' : `${(viewing.correctiveActions ?? []).filter(c => c.status === 2).length}/${viewing.correctiveActions.length} complete`} />
                <MiniStat label="Date Occurred" value={fmt.date(viewing.occurredAt)} />
              </div>

              <Section label="Description">
                <div style={{ fontSize: 13, color: T.dgrey }}>{viewing.description}</div>
              </Section>

              {viewing.envIncident && (
                <Section label="Environmental">
                  <div style={{ fontSize: 13, color: T.dgrey }}>
                    NEMA ref: {viewing.envIncident.nemaRef ?? '—'} · Notification required: {viewing.envIncident.nemaNotificationRequired ? 'Yes' : 'No'}
                    {viewing.envIncident.notifiedAt ? ` · Notified ${fmt.date(viewing.envIncident.notifiedAt)}` : ''}
                  </div>
                </Section>
              )}

              <Section label="Status">
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <Badge variant={viewing.status === 3 ? 'green' : 'amber'}>{INCIDENT_STATUSES[viewing.status] ?? viewing.status}</Badge>
                  <Select value={statusDraft} onChange={setStatusDraft} options={INCIDENT_STATUSES.map(s => ({ value: s, label: s }))} style={{ marginBottom: 0 }} />
                  <Btn size="sm" onClick={submitStatus} disabled={saving || statusDraft === (INCIDENT_STATUSES[viewing.status] ?? viewing.status)}>Update Status</Btn>
                </div>
              </Section>

              <Section label="Corrective Actions">
                {viewing.correctiveActions?.length > 0 ? (
                  <div>
                    {viewing.correctiveActions.map(c => (
                      <div key={c.id} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8, padding: '6px 0', borderBottom: `1px solid ${T.lgrey}` }}>
                        <div>
                          <div style={{ fontSize: 13, color: T.dgrey }}>{c.description}</div>
                          <div style={{ fontSize: 11, color: T.mgrey }}>{c.ownerName ?? 'Unassigned'} · due {fmt.date(c.dueDate)}</div>
                        </div>
                        <div style={{ display: 'flex', gap: 6, alignItems: 'center', flexShrink: 0 }}>
                          <Badge variant={capaVariant(CORRECTIVE_STATUSES[c.status] ?? c.status)}>{CORRECTIVE_STATUSES[c.status] ?? c.status}</Badge>
                          {c.status !== 2 && <Btn size="sm" variant="green" onClick={() => completeCapa(c.id)} disabled={saving}>Mark Complete</Btn>}
                        </div>
                      </div>
                    ))}
                  </div>
                ) : <div style={{ fontSize: 13, color: T.mgrey }}>None recorded.</div>}

                {!addingCapa ? (
                  <Btn size="sm" variant="ghost" onClick={() => setAddingCapa(true)} style={{ marginTop: 10 }}>+ Add Corrective Action</Btn>
                ) : (
                  <div style={{ marginTop: 10, borderTop: `1px solid ${T.lgrey}`, paddingTop: 10 }}>
                    <Input label="Description" value={capaForm.description} onChange={v => setCapaForm({ ...capaForm, description: v })} required />
                    <Select label="Owner" value={capaForm.ownerUserId} onChange={v => setCapaForm({ ...capaForm, ownerUserId: v })} options={[{ value: '', label: '— Unassigned —' }, ...employees]} />
                    <Input label="Due Date" type="date" value={capaForm.dueDate} onChange={v => setCapaForm({ ...capaForm, dueDate: v })} required />
                    <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
                      <Btn variant="ghost" size="sm" onClick={() => setAddingCapa(false)}>Cancel</Btn>
                      <Btn size="sm" onClick={submitCapa} disabled={saving || !capaForm.description || !capaForm.dueDate}>Add</Btn>
                    </div>
                  </div>
                )}
              </Section>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewing(null); setEditing(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditing(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Select label="Type" value={editForm.type} onChange={v => setEditForm({ ...editForm, type: v })} options={INCIDENT_TYPES.map(t => ({ value: t, label: t }))} />
              <Input label="Site / Location" value={editForm.site} onChange={v => setEditForm({ ...editForm, site: v })} required />
              <Select label="Severity" value={editForm.severity} onChange={v => setEditForm({ ...editForm, severity: v })} options={SEVERITIES.map(s => ({ value: s, label: s }))} />
              <Input label="Date Occurred" type="date" value={editForm.occurredAt} onChange={v => setEditForm({ ...editForm, occurredAt: v })} />
              <Input label="Description" value={editForm.description} onChange={v => setEditForm({ ...editForm, description: v })} required />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditing(false)}>Cancel</Btn>
                <Btn onClick={submitEdit} disabled={saving || !editForm.site || !editForm.description}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}

      {/* ── Delete confirm modal ── */}
      {deleteTarget && (
        <Modal title="Delete Incident" onClose={() => setDeleteTarget(null)} width={380}>
          <p style={{ fontSize: 13, color: T.dgrey }}>
            Are you sure you want to delete the incident at <strong>"{deleteTarget.siteName ?? deleteTarget.siteId}"</strong> on {fmt.date(deleteTarget.occurredAt)}? This action cannot be undone.
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
