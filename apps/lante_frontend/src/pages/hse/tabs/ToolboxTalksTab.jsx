import { useState, useEffect, useCallback } from 'react'
import api from '../../../api/axios.js'
import { T, fmt } from '../../../theme/tokens.js'
import { Card, Btn, Badge, HelpPanel, SectionHeader, DataTable, Modal, Input, Select, Loading, MiniStat } from '../../../components/ui.jsx'
import Pagination from '../../../components/Pagination.jsx'
import { EMPTY_TALK } from '../constants.js'

const empty = { data: { data: {} } }

const PAGE_SIZE = 20

export default function ToolboxTalksTab({ employees, setMsg, refreshDashboard }) {
  const [toolboxTalks, setToolboxTalks] = useState([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [modal, setModal] = useState(null)
  const [talkForm, setTalkForm] = useState(EMPTY_TALK)
  const [viewing, setViewing] = useState(null)
  const [editing, setEditing] = useState(false)
  const [editForm, setEditForm] = useState(EMPTY_TALK)
  const [deleteTarget, setDeleteTarget] = useState(null)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.get('/api/v1/hse-toolbox-talks', { params: { page, pageSize: PAGE_SIZE } }).catch(() => empty)
      const data = res.data?.data ?? {}
      setToolboxTalks(data.items ?? [])
      setTotalCount(data.totalCount ?? 0)
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  function employeeName(id) { return employees.find(e => e.value === id)?.label ?? id }

  function toggleAttendee(id) {
    setTalkForm(f => ({
      ...f,
      attendeeIds: f.attendeeIds.includes(id) ? f.attendeeIds.filter(x => x !== id) : [...f.attendeeIds, id],
    }))
  }

  async function submitTalk() {
    if (!talkForm.site || !talkForm.topic) return
    setSaving(true)
    try {
      await api.post('/api/v1/hse-toolbox-talks', {
        siteId: talkForm.site, siteName: talkForm.site,
        supervisorUserId: talkForm.supervisorUserId || null,
        supervisorName: talkForm.supervisorUserId ? employeeName(talkForm.supervisorUserId) : null,
        topic: talkForm.topic,
        heldOn: new Date(talkForm.heldOn || Date.now()).toISOString(),
        attendees: talkForm.attendeeIds.map(id => ({ employeeUserId: id, employeeName: employeeName(id) })),
      })
      setMsg({ type: 'success', text: 'Toolbox talk recorded.' })
      setTalkForm(EMPTY_TALK); setModal(null); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to record toolbox talk.' })
    } finally { setSaving(false) }
  }

  function openView(t, startEditing = false) {
    setViewing(t)
    setEditing(startEditing)
    setEditForm({
      site: t.siteId, supervisorUserId: t.supervisorUserId ?? '',
      topic: t.topic, heldOn: t.heldOn ? t.heldOn.slice(0, 10) : '',
      attendeeIds: t.attendees?.map(a => a.employeeUserId) ?? [],
    })
  }

  async function submitEdit() {
    if (!viewing || !editForm.site || !editForm.topic) return
    setSaving(true)
    try {
      await api.put(`/api/v1/hse-toolbox-talks/${viewing.id}`, {
        siteId: editForm.site, siteName: editForm.site,
        supervisorUserId: editForm.supervisorUserId || null,
        supervisorName: editForm.supervisorUserId ? employeeName(editForm.supervisorUserId) : null,
        topic: editForm.topic,
        heldOn: new Date(editForm.heldOn).toISOString(),
      })
      setMsg({ type: 'success', text: 'Toolbox talk updated.' })
      setViewing(null); setEditing(false); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update toolbox talk.' })
    } finally { setSaving(false) }
  }

  async function confirmDelete() {
    if (!deleteTarget) return
    try {
      await api.delete(`/api/v1/hse-toolbox-talks/${deleteTarget.id}`)
      setMsg({ type: 'success', text: 'Toolbox talk deleted.' })
      load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to delete toolbox talk.' })
    } finally {
      setDeleteTarget(null)
    }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About the Toolbox Talk Register">
        Record short, site-level safety briefings and who attended (sign-off). Click a row to see
        the full record, including the attendee list, and to edit the site, supervisor, topic or
        date. Attendees are fixed once recorded — sign-off can't be edited retroactively.
      </HelpPanel>

      <SectionHeader title="Toolbox Talk Register" action={<Btn onClick={() => setModal('talk')}>+ Record Talk</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Date', 'Site', 'Topic', 'Supervisor', 'Attendees', 'Actions']}
          empty="No toolbox talks recorded yet."
          rows={toolboxTalks.map(t => [
            fmt.date(t.heldOn),
            t.siteName ?? t.siteId,
            <span onClick={() => openView(t)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{t.topic}</span>,
            t.supervisorName ?? '—',
            <Badge variant="navy">{t.attendees?.length ?? 0} signed</Badge>,
            <div style={{ display: 'flex', gap: 4 }}>
              <Btn variant="ghost" size="sm" onClick={() => openView(t)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={() => openView(t, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
              <Btn variant="danger" size="sm" onClick={() => setDeleteTarget(t)} style={{ padding: '3px 10px', fontSize: 11 }}>Delete</Btn>
            </div>,
          ])}
        />
        <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />
      </Card>

      {/* ── Record Toolbox Talk modal ── */}
      {modal === 'talk' && (
        <Modal title="Record Toolbox Talk" onClose={() => setModal(null)} width={620}>
          <Input label="Site / Location" value={talkForm.site} onChange={v => setTalkForm({ ...talkForm, site: v })} required />
          <Select label="Supervisor" value={talkForm.supervisorUserId} onChange={v => setTalkForm({ ...talkForm, supervisorUserId: v })} options={[{ value: '', label: '— Select supervisor —' }, ...employees]} />
          <Input label="Topic" value={talkForm.topic} onChange={v => setTalkForm({ ...talkForm, topic: v })} required />
          <Input label="Date Held" type="date" value={talkForm.heldOn} onChange={v => setTalkForm({ ...talkForm, heldOn: v })} />
          <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 5 }}>Attendees (sign-off)</label>
          <div style={{ maxHeight: 160, overflowY: 'auto', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, padding: 10, marginBottom: 14 }}>
            {employees.length === 0 ? <span style={{ fontSize: 12, color: T.mgrey }}>No employees found.</span> : employees.map(e => (
              <label key={e.value} style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12.5, color: T.dgrey, padding: '4px 0' }}>
                <input type="checkbox" checked={talkForm.attendeeIds.includes(e.value)} onChange={() => toggleAttendee(e.value)} />
                {e.label}
              </label>
            ))}
          </div>
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitTalk} disabled={saving || !talkForm.site || !talkForm.topic}>{saving ? 'Saving…' : 'Record Talk'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit Toolbox Talk modal ── */}
      {viewing && (
        <Modal title={editing ? 'Edit Toolbox Talk' : 'Toolbox Talk Details'} onClose={() => { setViewing(null); setEditing(false) }} width={620}>
          {!editing ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewing.topic}</div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>{viewing.siteName ?? viewing.siteId} · {fmt.date(viewing.heldOn)}</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Date Held" value={fmt.date(viewing.heldOn)} />
                <MiniStat label="Supervisor" value={viewing.supervisorName ?? '—'} />
                <MiniStat label="Attendees" value={viewing.attendees?.length ?? 0} />
              </div>

              <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>Attendees</div>
                <div style={{ fontSize: 13, color: T.dgrey }}>
                  {viewing.attendees?.length > 0 ? viewing.attendees.map(a => a.employeeName ?? a.employeeUserId).join(', ') : 'None recorded.'}
                </div>
              </div>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewing(null); setEditing(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditing(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Input label="Site / Location" value={editForm.site} onChange={v => setEditForm({ ...editForm, site: v })} required />
              <Select label="Supervisor" value={editForm.supervisorUserId} onChange={v => setEditForm({ ...editForm, supervisorUserId: v })} options={[{ value: '', label: '— Select supervisor —' }, ...employees]} />
              <Input label="Topic" value={editForm.topic} onChange={v => setEditForm({ ...editForm, topic: v })} required />
              <Input label="Date Held" type="date" value={editForm.heldOn} onChange={v => setEditForm({ ...editForm, heldOn: v })} />
              <p style={{ fontSize: 11, color: T.mgrey, margin: '0 0 14px' }}>Attendee sign-off can't be edited here — record a new talk if attendance was wrong.</p>
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditing(false)}>Cancel</Btn>
                <Btn onClick={submitEdit} disabled={saving || !editForm.site || !editForm.topic}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}

      {/* ── Delete confirm modal ── */}
      {deleteTarget && (
        <Modal title="Delete Toolbox Talk" onClose={() => setDeleteTarget(null)} width={380}>
          <p style={{ fontSize: 13, color: T.dgrey }}>
            Are you sure you want to delete <strong>"{deleteTarget.topic}"</strong> ({fmt.date(deleteTarget.heldOn)})? This action cannot be undone.
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
