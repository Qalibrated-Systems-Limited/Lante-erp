import { useState, useEffect, useCallback } from 'react'
import api from '../../../api/axios.js'
import { T, fmt } from '../../../theme/tokens.js'
import { Card, Btn, Badge, HelpPanel, SectionHeader, DataTable, Modal, Input, Select, Loading, MiniStat } from '../../../components/ui.jsx'
import Pagination from '../../../components/Pagination.jsx'
import { PPE_CONDITIONS, EMPTY_PPE, EMPTY_PPE_UPDATE, today, daysUntil, dueBadge } from '../constants.js'

const empty = { data: { data: {} } }

const PAGE_SIZE = 20

export default function PpeTab({ employees, setMsg, refreshDashboard }) {
  const [ppeIssues, setPpeIssues] = useState([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [modal, setModal] = useState(null)
  const [ppeForm, setPpeForm] = useState(EMPTY_PPE)
  const [viewing, setViewing] = useState(null)
  const [editing, setEditing] = useState(false)
  const [ppeUpdateForm, setPpeUpdateForm] = useState(EMPTY_PPE_UPDATE)
  const [deleteTarget, setDeleteTarget] = useState(null)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.get('/api/v1/hse-ppe-issues', { params: { page, pageSize: PAGE_SIZE } }).catch(() => empty)
      const data = res.data?.data ?? {}
      setPpeIssues(data.items ?? [])
      setTotalCount(data.totalCount ?? 0)
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  function employeeName(id) { return employees.find(e => e.value === id)?.label ?? id }

  async function submitPpe() {
    if (!ppeForm.employeeUserId || !ppeForm.item) return
    setSaving(true)
    try {
      await api.post('/api/v1/hse-ppe-issues', {
        employeeUserId: ppeForm.employeeUserId, employeeName: employeeName(ppeForm.employeeUserId),
        item: ppeForm.item, condition: PPE_CONDITIONS.indexOf(ppeForm.condition),
        replacementDueAt: ppeForm.replacementDueAt ? new Date(ppeForm.replacementDueAt).toISOString() : null,
      })
      setMsg({ type: 'success', text: 'PPE issue recorded.' })
      setPpeForm(EMPTY_PPE); setModal(null); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to record PPE issue.' })
    } finally { setSaving(false) }
  }

  function openView(p, startEditing = false) {
    setViewing(p)
    setEditing(startEditing)
    setPpeUpdateForm({
      condition: PPE_CONDITIONS[p.condition] ?? PPE_CONDITIONS[0],
      returned: !!p.returnedAt,
      returnedAt: p.returnedAt ? p.returnedAt.slice(0, 10) : today(),
      replacementDueAt: p.replacementDueAt ? p.replacementDueAt.slice(0, 10) : '',
    })
  }

  async function submitPpeUpdate() {
    if (!viewing) return
    setSaving(true)
    try {
      await api.put(`/api/v1/hse-ppe-issues/${viewing.id}`, {
        condition: PPE_CONDITIONS.indexOf(ppeUpdateForm.condition),
        returnedAt: ppeUpdateForm.returned ? new Date(ppeUpdateForm.returnedAt).toISOString() : null,
        replacementDueAt: ppeUpdateForm.replacementDueAt ? new Date(ppeUpdateForm.replacementDueAt).toISOString() : null,
      })
      setMsg({ type: 'success', text: 'PPE issue updated.' })
      setViewing(null); setEditing(false); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update PPE issue.' })
    } finally { setSaving(false) }
  }

  async function confirmDelete() {
    if (!deleteTarget) return
    try {
      await api.delete(`/api/v1/hse-ppe-issues/${deleteTarget.id}`)
      setMsg({ type: 'success', text: 'PPE issue deleted.' })
      load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to delete PPE issue.' })
    } finally {
      setDeleteTarget(null)
    }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About the PPE Issue Register">
        Track what protective equipment has been issued to whom, its condition, and when it's due
        for replacement. Click a row to see the full record and to update condition, return status
        or replacement due date. The employee and item can't be changed after issue — record a new
        issue if either was logged wrong.
      </HelpPanel>

      <SectionHeader title="PPE Issue Register" action={<Btn onClick={() => setModal('ppe')}>+ Record Issue</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          onRowClick={(_, i) => openView(ppeIssues[i])}
          headers={['Employee', 'Item', 'Condition', 'Issued', 'Returned', 'Replacement Due', 'Actions']}
          empty="No PPE issues recorded yet."
          rows={ppeIssues.map(p => [
            <span onClick={() => openView(p)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{p.employeeName ?? p.employeeUserId}</span>,
            p.item,
            <Badge variant={p.condition >= 2 ? 'amber' : 'green'}>{PPE_CONDITIONS[p.condition] ?? p.condition}</Badge>,
            fmt.date(p.issuedAt),
            p.returnedAt ? fmt.date(p.returnedAt) : '—',
            p.replacementDueAt ? dueBadge(daysUntil(p.replacementDueAt)) : '—',
            <div style={{ display: 'flex', gap: 4 }}>
              <Btn variant="ghost" size="sm" onClick={e => { e.stopPropagation(); openView(p) }} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={e => { e.stopPropagation(); openView(p, true) }} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
              <Btn variant="danger" size="sm" onClick={e => { e.stopPropagation(); setDeleteTarget(p) }} style={{ padding: '3px 10px', fontSize: 11 }}>Delete</Btn>
            </div>,
          ])}
        />
        <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />
      </Card>

      {/* ── Record PPE Issue modal ── */}
      {modal === 'ppe' && (
        <Modal title="Record PPE Issue" onClose={() => setModal(null)}>
          <Select label="Employee" value={ppeForm.employeeUserId} onChange={v => setPpeForm({ ...ppeForm, employeeUserId: v })} options={[{ value: '', label: '— Select employee —' }, ...employees]} required />
          <Input label="Item" value={ppeForm.item} onChange={v => setPpeForm({ ...ppeForm, item: v })} required placeholder="e.g. Safety helmet" />
          <Select label="Condition" value={ppeForm.condition} onChange={v => setPpeForm({ ...ppeForm, condition: v })} options={PPE_CONDITIONS.map(c => ({ value: c, label: c }))} />
          <Input label="Replacement Due" type="date" value={ppeForm.replacementDueAt} onChange={v => setPpeForm({ ...ppeForm, replacementDueAt: v })} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitPpe} disabled={saving || !ppeForm.employeeUserId || !ppeForm.item}>{saving ? 'Saving…' : 'Record Issue'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit PPE Issue modal ── */}
      {viewing && (
        <Modal title={editing ? 'Update PPE Issue' : 'PPE Issue Details'} onClose={() => { setViewing(null); setEditing(false) }}>
          {!editing ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewing.employeeName ?? viewing.employeeUserId}</span>
                  <Badge variant={viewing.condition >= 2 ? 'amber' : 'green'}>{PPE_CONDITIONS[viewing.condition] ?? viewing.condition}</Badge>
                </div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>{viewing.item}</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Issued" value={fmt.date(viewing.issuedAt)} />
                <MiniStat label="Returned" value={viewing.returnedAt ? fmt.date(viewing.returnedAt) : '—'} />
                <MiniStat label="Replacement Due" value={viewing.replacementDueAt ? fmt.date(viewing.replacementDueAt) : '—'} />
              </div>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewing(null); setEditing(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditing(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Select label="Condition" value={ppeUpdateForm.condition} onChange={v => setPpeUpdateForm({ ...ppeUpdateForm, condition: v })} options={PPE_CONDITIONS.map(c => ({ value: c, label: c }))} />
              <div style={{ marginBottom: 14 }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, color: T.dgrey }}>
                  <input type="checkbox" checked={ppeUpdateForm.returned} onChange={e => setPpeUpdateForm({ ...ppeUpdateForm, returned: e.target.checked })} />
                  Returned
                </label>
              </div>
              {ppeUpdateForm.returned && (
                <Input label="Returned On" type="date" value={ppeUpdateForm.returnedAt} onChange={v => setPpeUpdateForm({ ...ppeUpdateForm, returnedAt: v })} />
              )}
              <Input label="Replacement Due" type="date" value={ppeUpdateForm.replacementDueAt} onChange={v => setPpeUpdateForm({ ...ppeUpdateForm, replacementDueAt: v })} />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditing(false)}>Cancel</Btn>
                <Btn onClick={submitPpeUpdate} disabled={saving}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}

      {/* ── Delete confirm modal ── */}
      {deleteTarget && (
        <Modal title="Delete PPE Issue" onClose={() => setDeleteTarget(null)} width={380}>
          <p style={{ fontSize: 13, color: T.dgrey }}>
            Are you sure you want to delete the <strong>"{deleteTarget.item}"</strong> issued to "{deleteTarget.employeeName ?? deleteTarget.employeeUserId}"? This action cannot be undone.
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
