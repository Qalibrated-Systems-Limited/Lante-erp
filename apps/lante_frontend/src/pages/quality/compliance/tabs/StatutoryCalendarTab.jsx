import { useState, useEffect, useCallback } from 'react'
import api from '../../../../api/axios.js'
import { T, fmt } from '../../../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Btn, Badge, HelpPanel, SectionHeader, DataTable, Modal, Input, Select, Loading, MiniStat, worstVariant } from '../../../../components/ui.jsx'
import Pagination from '../../../../components/Pagination.jsx'
import { OBLIGATION_FREQUENCIES, EMPTY_OBLIGATION, ragVariant } from '../constants.js'

const PAGE_SIZE = 20

export default function StatutoryCalendarTab({ employees, employeeName, setMsg, statutoryDashboard }) {
  const [statutoryObligations, setStatutoryObligations] = useState([])
  const [loading, setLoading] = useState(true)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [modal, setModal] = useState(null)
  const [saving, setSaving] = useState(false)
  const [obligationForm, setObligationForm] = useState(EMPTY_OBLIGATION)
  const [viewing, setViewing] = useState(null)
  const [editing, setEditing] = useState(false)
  const [editForm, setEditForm] = useState(EMPTY_OBLIGATION)

  // Note: the "upcoming deadlines" table below is driven entirely by the statutoryDashboard prop
  // (from /api/v1/statutory-dashboard, fetched in CompliancePage.jsx) — that dashboard endpoint is
  // out of scope here, so only the "Recurring Obligations" register below is paginated.
  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.get('/api/v1/statutory-obligations', { params: { page, pageSize: PAGE_SIZE } }).catch(() => ({ data: { data: {} } }))
      const data = res.data?.data || {}
      setStatutoryObligations(data.items ?? [])
      setTotalCount(data.totalCount ?? 0)
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  async function submitObligation() {
    if (!obligationForm.name || !obligationForm.authority) return
    setSaving(true)
    try {
      await api.post('/api/v1/statutory-obligations', {
        name: obligationForm.name, authority: obligationForm.authority,
        frequency: OBLIGATION_FREQUENCIES.indexOf(obligationForm.frequency),
        statutoryDay: Number(obligationForm.statutoryDay) || 1,
        ownerUserId: obligationForm.ownerUserId || null,
        ownerName: obligationForm.ownerUserId ? employeeName(obligationForm.ownerUserId) : null,
      })
      setMsg({ type: 'success', text: 'Statutory obligation added — deadlines will be generated automatically.' })
      setObligationForm(EMPTY_OBLIGATION); setModal(null); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to add obligation.' })
    } finally { setSaving(false) }
  }

  function openView(o, startEditing = false) {
    setViewing(o)
    setEditing(startEditing)
    setEditForm({
      name: o.name ?? '', authority: o.authority ?? '',
      frequency: OBLIGATION_FREQUENCIES[o.frequency] ?? o.frequency,
      statutoryDay: o.statutoryDay ?? 1,
      ownerUserId: o.ownerUserId ?? '',
    })
  }

  async function submitEdit() {
    if (!viewing || !editForm.name || !editForm.authority) return
    setSaving(true)
    try {
      await api.put(`/api/v1/statutory-obligations/${viewing.id}`, {
        name: editForm.name, authority: editForm.authority,
        frequency: OBLIGATION_FREQUENCIES.indexOf(editForm.frequency),
        statutoryDay: Number(editForm.statutoryDay) || 1,
        ownerUserId: editForm.ownerUserId || null,
        ownerName: editForm.ownerUserId ? employeeName(editForm.ownerUserId) : null,
      })
      setMsg({ type: 'success', text: 'Statutory obligation updated.' })
      setViewing(null); setEditing(false); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update obligation.' })
    } finally { setSaving(false) }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About the Statutory Compliance Calendar" variant={worstVariant(
        (statutoryDashboard?.redCount ?? 0) > 0 ? 'red' : 'green',
        (statutoryDashboard?.amberCount ?? 0) > 0 ? 'amber' : 'green',
      )}>
        The upcoming deadlines table above is computed automatically from the recurring
        obligations register below — it isn't editable directly. Click a row in the
        obligations register to view its details and edit the recurring rule; the deadlines
        table will regenerate from the updated schedule.
      </HelpPanel>

      <SectionHeader
        title="Statutory Compliance Calendar"
        sub="One-screen view of all upcoming statutory deadlines — GREEN >60d, AMBER 30-60d, RED <30d or overdue"
        action={<Btn onClick={() => setModal('obligation')}>+ Add Recurring Obligation</Btn>}
      />
      <div style={{ ...KPI_GRID, marginBottom: 18 }}>
        <Kpi label="Green" value={statutoryDashboard?.greenCount ?? 0} icon="🟢" variant="green" />
        <Kpi label="Amber" value={statutoryDashboard?.amberCount ?? 0} icon="🟡" variant="amber" />
        <Kpi label="Red" value={statutoryDashboard?.redCount ?? 0} icon="🔴" variant="red" />
      </div>
      <Card style={{ padding: 0, overflow: 'hidden', marginBottom: 18 }}>
        <DataTable
          headers={['Deadline', 'Source', 'Due Date', 'Owner', 'RAG']}
          empty="No upcoming statutory deadlines."
          rows={(statutoryDashboard?.upcomingDeadlines ?? []).map(i => [
            <strong style={{ fontSize: 12.5, color: T.dgrey }}>{i.title}</strong>,
            <Badge variant="navy">{i.sourceType}</Badge>,
            fmt.date(i.dueDate),
            i.ownerName ?? '—',
            <Badge variant={ragVariant(i.rag)}>{i.rag}{i.isOverdue ? ' · Overdue' : ''}</Badge>,
          ])}
        />
      </Card>
      <SectionHeader title="Recurring Obligations" sub="PAYE, VAT, NSSF/SHA, WHT — deadlines generated automatically each period" />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Name', 'Authority', 'Frequency', 'Statutory Day', 'Owner', 'Actions']}
          empty="No recurring obligations configured yet."
          rows={statutoryObligations.map(o => [
            <span onClick={() => openView(o)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{o.name}</span>,
            <Badge variant="navy">{o.authority}</Badge>,
            OBLIGATION_FREQUENCIES[o.frequency] ?? o.frequency,
            o.statutoryDay,
            o.ownerName ?? '—',
            <div style={{ display: 'flex', gap: 4 }}>
              <Btn variant="ghost" size="sm" onClick={() => openView(o)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={() => openView(o, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
            </div>,
          ])}
        />
      </Card>
      <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />

      {modal === 'obligation' && (
        <Modal title="Add Recurring Statutory Obligation" onClose={() => setModal(null)}>
          <Input label="Name" value={obligationForm.name} onChange={v => setObligationForm({ ...obligationForm, name: v })} required placeholder="e.g. PAYE, VAT, NSSF/SHA, WHT" />
          <Input label="Authority" value={obligationForm.authority} onChange={v => setObligationForm({ ...obligationForm, authority: v })} required placeholder="e.g. KRA, NSSF, SHA" />
          <Select label="Frequency" value={obligationForm.frequency} onChange={v => setObligationForm({ ...obligationForm, frequency: v })} options={OBLIGATION_FREQUENCIES.map(f => ({ value: f, label: f }))} />
          <Input label="Statutory Day (day of month)" type="number" value={obligationForm.statutoryDay} onChange={v => setObligationForm({ ...obligationForm, statutoryDay: v })} required />
          <Select label="Owner (optional)" value={obligationForm.ownerUserId} onChange={v => setObligationForm({ ...obligationForm, ownerUserId: v })} options={[{ value: '', label: '— Unassigned —' }, ...employees]} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitObligation} disabled={saving || !obligationForm.name || !obligationForm.authority}>{saving ? 'Saving…' : 'Add Obligation'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit Recurring Obligation modal ── */}
      {viewing && (
        <Modal title={editing ? 'Edit Recurring Obligation' : 'Recurring Obligation Details'} onClose={() => { setViewing(null); setEditing(false) }} width={620}>
          {!editing ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewing.name}</span>
                  <Badge variant="navy">{viewing.authority}</Badge>
                </div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>{OBLIGATION_FREQUENCIES[viewing.frequency] ?? viewing.frequency}</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Statutory Day" value={viewing.statutoryDay} />
                <MiniStat label="Owner" value={viewing.ownerName ?? '—'} />
              </div>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewing(null); setEditing(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditing(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Input label="Name" value={editForm.name} onChange={v => setEditForm({ ...editForm, name: v })} required placeholder="e.g. PAYE, VAT, NSSF/SHA, WHT" />
              <Input label="Authority" value={editForm.authority} onChange={v => setEditForm({ ...editForm, authority: v })} required placeholder="e.g. KRA, NSSF, SHA" />
              <Select label="Frequency" value={editForm.frequency} onChange={v => setEditForm({ ...editForm, frequency: v })} options={OBLIGATION_FREQUENCIES.map(f => ({ value: f, label: f }))} />
              <Input label="Statutory Day (day of month)" type="number" value={editForm.statutoryDay} onChange={v => setEditForm({ ...editForm, statutoryDay: v })} required />
              <Select label="Owner (optional)" value={editForm.ownerUserId} onChange={v => setEditForm({ ...editForm, ownerUserId: v })} options={[{ value: '', label: '— Unassigned —' }, ...employees]} />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditing(false)}>Cancel</Btn>
                <Btn onClick={submitEdit} disabled={saving || !editForm.name || !editForm.authority}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}
    </>
  )
}
