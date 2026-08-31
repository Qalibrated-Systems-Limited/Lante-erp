import { useState, useEffect, useCallback } from 'react'
import api from '../../../../api/axios.js'
import { T, fmt } from '../../../../theme/tokens.js'
import { Card, Btn, Badge, HelpPanel, SectionHeader, DataTable, Modal, Input, Select, Loading, MiniStat } from '../../../../components/ui.jsx'
import Pagination from '../../../../components/Pagination.jsx'
import { COSEC_TASK_STATUSES, COSEC_TASK_LABELS, EMPTY_COSEC_TASK } from '../constants.js'

const PAGE_SIZE = 20

export default function CosecTasksTab({ employees, employeeName, setMsg }) {
  const [cosecTasks, setCosecTasks] = useState([])
  const [statutoryObligations, setStatutoryObligations] = useState([])
  const [loading, setLoading] = useState(true)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [modal, setModal] = useState(null)
  const [saving, setSaving] = useState(false)
  const [cosecTaskForm, setCosecTaskForm] = useState(EMPTY_COSEC_TASK)
  const [viewing, setViewing] = useState(null)
  const [editing, setEditing] = useState(false)
  const [editForm, setEditForm] = useState(EMPTY_COSEC_TASK)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const [cosecRes, obligationsRes] = await Promise.all([
        api.get('/api/v1/cosec-tasks', { params: { page, pageSize: PAGE_SIZE } }).catch(() => ({ data: { data: {} } })),
        // Lookup only (populates the "Related Obligation" select) — fetched at a large page size
        // since it's not the paginated register in scope here (that's StatutoryCalendarTab's).
        api.get('/api/v1/statutory-obligations', { params: { page: 1, pageSize: 100 } }).catch(() => ({ data: { data: {} } })),
      ])
      const cosecData = cosecRes.data?.data || {}
      setCosecTasks(cosecData.items ?? [])
      setTotalCount(cosecData.totalCount ?? 0)
      setStatutoryObligations(obligationsRes.data?.data?.items ?? [])
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  async function submitCosecTask() {
    if (!cosecTaskForm.title) return
    setSaving(true)
    try {
      await api.post('/api/v1/cosec-tasks', {
        obligationId: cosecTaskForm.obligationId || null,
        title: cosecTaskForm.title,
        responsiblePersonUserId: cosecTaskForm.responsiblePersonUserId || null,
        responsiblePersonName: cosecTaskForm.responsiblePersonUserId ? employeeName(cosecTaskForm.responsiblePersonUserId) : null,
        dueDate: new Date(cosecTaskForm.dueDate || Date.now()).toISOString(),
      })
      setMsg({ type: 'success', text: 'Company Secretary task added.' })
      setCosecTaskForm(EMPTY_COSEC_TASK); setModal(null); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to add task.' })
    } finally { setSaving(false) }
  }

  async function setCosecTaskStatus(id, statusLabel) {
    try {
      await api.patch(`/api/v1/cosec-tasks/${id}/status`, { status: COSEC_TASK_STATUSES.indexOf(statusLabel) })
      load()
    } catch {
      setMsg({ type: 'error', text: 'Failed to update task.' })
    }
  }

  function obligationName(id) { return statutoryObligations.find(o => o.id === id)?.name ?? '—' }

  function openView(t, startEditing = false) {
    setViewing(t)
    setEditing(startEditing)
    setEditForm({
      obligationId: t.obligationId ?? '',
      title: t.title ?? '',
      responsiblePersonUserId: t.responsiblePersonUserId ?? '',
      dueDate: t.dueDate ? t.dueDate.slice(0, 10) : '',
    })
  }

  async function submitEdit() {
    if (!viewing || !editForm.title) return
    setSaving(true)
    try {
      await api.put(`/api/v1/cosec-tasks/${viewing.id}`, {
        obligationId: editForm.obligationId || null,
        title: editForm.title,
        responsiblePersonUserId: editForm.responsiblePersonUserId || null,
        responsiblePersonName: editForm.responsiblePersonUserId ? employeeName(editForm.responsiblePersonUserId) : null,
        dueDate: new Date(editForm.dueDate || Date.now()).toISOString(),
      })
      setMsg({ type: 'success', text: 'Company Secretary task updated.' })
      setViewing(null); setEditing(false); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update task.' })
    } finally { setSaving(false) }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About Company Secretary Tasks">
        Tracks Company Secretary tasks — AGM preparation, annual return filing preparation,
        statutory register updates — tied to the statutory obligations they support. Click any
        row to see the full record and edit its details.
        <br /><br />
        <strong>Mark Done</strong> (and the underlying status action) records progress on the
        task itself — status isn't editable from the "Edit" form.
      </HelpPanel>

      <SectionHeader title="Company Secretary Tasks" sub="AGM scheduling, annual-return preparation, statutory-register updates" action={<Btn onClick={() => setModal('cosecTask')}>+ Add Task</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Task', 'Responsible', 'Due Date', 'Status', 'Actions']}
          empty="No Company Secretary tasks yet."
          rows={cosecTasks.map(t => [
            <span onClick={() => openView(t)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{t.title}</span>,
            t.responsiblePersonName ?? '—',
            fmt.date(t.dueDate),
            <Badge variant={t.status === 2 ? 'green' : t.status === 3 ? 'red' : 'amber'}>{COSEC_TASK_LABELS[COSEC_TASK_STATUSES[t.status]] ?? t.status}</Badge>,
            <div style={{ display: 'flex', gap: 4, alignItems: 'center', flexWrap: 'wrap' }}>
              <Btn variant="ghost" size="sm" onClick={() => openView(t)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={() => openView(t, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
              {t.status !== 2 && <Btn size="sm" onClick={() => setCosecTaskStatus(t.id, 'Done')}>Mark Done</Btn>}
            </div>,
          ])}
        />
      </Card>
      <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />

      {modal === 'cosecTask' && (
        <Modal title="Add Company Secretary Task" onClose={() => setModal(null)}>
          <Input label="Title" value={cosecTaskForm.title} onChange={v => setCosecTaskForm({ ...cosecTaskForm, title: v })} required placeholder="e.g. Schedule AGM, prepare annual return" />
          <Select label="Related Obligation (optional)" value={cosecTaskForm.obligationId} onChange={v => setCosecTaskForm({ ...cosecTaskForm, obligationId: v })}
            options={[{ value: '', label: '— None —' }, ...statutoryObligations.map(o => ({ value: o.id, label: o.name }))]} />
          <Select label="Responsible Person" value={cosecTaskForm.responsiblePersonUserId} onChange={v => setCosecTaskForm({ ...cosecTaskForm, responsiblePersonUserId: v })}
            options={[{ value: '', label: '— Unassigned —' }, ...employees]} />
          <Input label="Due Date" type="date" value={cosecTaskForm.dueDate} onChange={v => setCosecTaskForm({ ...cosecTaskForm, dueDate: v })} required />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitCosecTask} disabled={saving || !cosecTaskForm.title}>{saving ? 'Saving…' : 'Add Task'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit Company Secretary Task modal ── */}
      {viewing && (
        <Modal title={editing ? 'Edit Company Secretary Task' : 'Company Secretary Task Details'} onClose={() => { setViewing(null); setEditing(false) }} width={620}>
          {!editing ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewing.title}</span>
                  <Badge variant={viewing.status === 2 ? 'green' : viewing.status === 3 ? 'red' : 'amber'}>{COSEC_TASK_LABELS[COSEC_TASK_STATUSES[viewing.status]] ?? viewing.status}</Badge>
                </div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>{viewing.responsiblePersonName ?? 'Unassigned'}</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Due Date" value={fmt.date(viewing.dueDate)} />
                <MiniStat label="Obligation" value={viewing.obligationId ? obligationName(viewing.obligationId) : '—'} />
              </div>

              <p style={{ fontSize: 11, color: T.mgrey, margin: '4px 0 0' }}>Status changes via Mark Done, not here.</p>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewing(null); setEditing(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditing(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Input label="Title" value={editForm.title} onChange={v => setEditForm({ ...editForm, title: v })} required placeholder="e.g. Schedule AGM, prepare annual return" />
              <Select label="Related Obligation (optional)" value={editForm.obligationId} onChange={v => setEditForm({ ...editForm, obligationId: v })}
                options={[{ value: '', label: '— None —' }, ...statutoryObligations.map(o => ({ value: o.id, label: o.name }))]} />
              <Select label="Responsible Person" value={editForm.responsiblePersonUserId} onChange={v => setEditForm({ ...editForm, responsiblePersonUserId: v })}
                options={[{ value: '', label: '— Unassigned —' }, ...employees]} />
              <Input label="Due Date" type="date" value={editForm.dueDate} onChange={v => setEditForm({ ...editForm, dueDate: v })} required />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditing(false)}>Cancel</Btn>
                <Btn onClick={submitEdit} disabled={saving || !editForm.title}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}
    </>
  )
}
