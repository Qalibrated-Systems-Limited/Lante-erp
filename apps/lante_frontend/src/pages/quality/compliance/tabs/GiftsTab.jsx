import { useState, useEffect, useCallback } from 'react'
import api from '../../../../api/axios.js'
import { T, fmt } from '../../../../theme/tokens.js'
import { Card, Btn, Badge, HelpPanel, SectionHeader, DataTable, Modal, Input, Select, Loading, MiniStat } from '../../../../components/ui.jsx'
import Pagination from '../../../../components/Pagination.jsx'
import { GIFT_DIRECTIONS, EMPTY_GIFT, today } from '../constants.js'

const PAGE_SIZE = 20

export default function GiftsTab({ employees, employeeName, setMsg, refreshDashboard }) {
  const [gifts, setGifts] = useState([])
  const [loading, setLoading] = useState(true)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [modal, setModal] = useState(null)
  const [saving, setSaving] = useState(false)
  const [giftForm, setGiftForm] = useState(EMPTY_GIFT)
  const [viewing, setViewing] = useState(null)
  const [editing, setEditing] = useState(false)
  const [editForm, setEditForm] = useState(EMPTY_GIFT)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.get('/api/v1/compliance-gifts', { params: { page, pageSize: PAGE_SIZE } }).catch(() => ({ data: { data: {} } }))
      const data = res.data?.data || {}
      setGifts(data.items ?? [])
      setTotalCount(data.totalCount ?? 0)
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  async function submitGift() {
    if (!giftForm.employeeUserId || !giftForm.counterpartyName || !giftForm.value) return
    setSaving(true)
    try {
      await api.post('/api/v1/compliance-gifts', {
        employeeUserId: giftForm.employeeUserId, employeeName: employeeName(giftForm.employeeUserId),
        direction: GIFT_DIRECTIONS.indexOf(giftForm.direction),
        counterpartyName: giftForm.counterpartyName,
        isGovernmentOfficial: giftForm.isGovernmentOfficial,
        description: giftForm.description || null,
        value: Number(giftForm.value) || 0,
        date: new Date(giftForm.date || Date.now()).toISOString(),
      })
      setMsg({ type: 'success', text: 'Gift/hospitality recorded.' })
      setGiftForm(EMPTY_GIFT); setModal(null); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to record gift.' })
    } finally { setSaving(false) }
  }

  function openView(g, startEditing = false) {
    setViewing(g)
    setEditing(startEditing)
    setEditForm({
      employeeUserId: g.employeeUserId,
      direction: GIFT_DIRECTIONS[g.direction] ?? g.direction,
      counterpartyName: g.counterpartyName ?? '',
      isGovernmentOfficial: !!g.isGovernmentOfficial,
      description: g.description ?? '',
      value: g.value ?? '',
      date: g.date ? g.date.slice(0, 10) : today(),
    })
  }

  async function submitEdit() {
    if (!viewing || !editForm.employeeUserId || !editForm.counterpartyName || !editForm.value) return
    setSaving(true)
    try {
      await api.put(`/api/v1/compliance-gifts/${viewing.id}`, {
        employeeUserId: editForm.employeeUserId, employeeName: employeeName(editForm.employeeUserId),
        direction: GIFT_DIRECTIONS.indexOf(editForm.direction),
        counterpartyName: editForm.counterpartyName,
        isGovernmentOfficial: editForm.isGovernmentOfficial,
        description: editForm.description || null,
        value: Number(editForm.value) || 0,
        date: new Date(editForm.date || Date.now()).toISOString(),
      })
      setMsg({ type: 'success', text: 'Gift/hospitality record updated.' })
      setViewing(null); setEditing(false); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update gift record.' })
    } finally { setSaving(false) }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About the Gifts &amp; Hospitality Register">
        This register logs gifts and hospitality given to, or received from, external parties.
        Click any row to see the full record and edit its details.
        <br /><br />
        Entries are automatically flagged for review when the value exceeds <strong>Kshs 5,000</strong>
        (or <strong>Kshs 2,000</strong> when the counterparty is a government official) — the "Flagged"
        threshold is applied server-side and isn't editable here.
      </HelpPanel>

      <SectionHeader title="Gifts & Hospitality Register" sub="Flagged if value exceeds Kshs 5,000 (Kshs 2,000 for government officials)" action={<Btn onClick={() => setModal('gift')}>+ Log Gift</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Employee', 'Direction', 'Counterparty', 'Value', 'Date', 'Flagged', 'Actions']}
          empty="No gifts/hospitality logged yet."
          rows={gifts.map(g => [
            <span onClick={() => openView(g)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{g.employeeName ?? g.employeeUserId}</span>,
            <Badge variant="navy">{GIFT_DIRECTIONS[g.direction] ?? g.direction}</Badge>,
            `${g.counterpartyName}${g.isGovernmentOfficial ? ' (Govt)' : ''}`,
            fmt.kes(g.value),
            fmt.date(g.date),
            g.flagged ? <Badge variant="red">Flagged</Badge> : <Badge variant="green">OK</Badge>,
            <div style={{ display: 'flex', gap: 4, alignItems: 'center', flexWrap: 'wrap' }}>
              <Btn variant="ghost" size="sm" onClick={() => openView(g)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={() => openView(g, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
            </div>,
          ])}
        />
      </Card>
      <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />

      {modal === 'gift' && (
        <Modal title="Log Gift / Hospitality" onClose={() => setModal(null)}>
          <Select label="Employee" value={giftForm.employeeUserId} onChange={v => setGiftForm({ ...giftForm, employeeUserId: v })} options={[{ value: '', label: '— Select employee —' }, ...employees]} required />
          <Select label="Direction" value={giftForm.direction} onChange={v => setGiftForm({ ...giftForm, direction: v })} options={GIFT_DIRECTIONS.map(d => ({ value: d, label: d }))} />
          <Input label="Counterparty" value={giftForm.counterpartyName} onChange={v => setGiftForm({ ...giftForm, counterpartyName: v })} required />
          <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 14 }}>
            <input type="checkbox" checked={giftForm.isGovernmentOfficial} onChange={e => setGiftForm({ ...giftForm, isGovernmentOfficial: e.target.checked })} />
            Counterparty is a government official (Kshs 2,000 threshold applies)
          </label>
          <Input label="Value (Kshs)" type="number" value={giftForm.value} onChange={v => setGiftForm({ ...giftForm, value: v })} required />
          <Input label="Date" type="date" value={giftForm.date} onChange={v => setGiftForm({ ...giftForm, date: v })} />
          <Input label="Description" value={giftForm.description} onChange={v => setGiftForm({ ...giftForm, description: v })} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitGift} disabled={saving || !giftForm.employeeUserId || !giftForm.counterpartyName || !giftForm.value}>{saving ? 'Saving…' : 'Log Gift'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit Gift modal ── */}
      {viewing && (
        <Modal title={editing ? 'Edit Gift / Hospitality' : 'Gift / Hospitality Details'} onClose={() => { setViewing(null); setEditing(false) }} width={620}>
          {!editing ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewing.employeeName ?? viewing.employeeUserId}</span>
                  <Badge variant="navy">{GIFT_DIRECTIONS[viewing.direction] ?? viewing.direction}</Badge>
                </div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>{viewing.counterpartyName}{viewing.isGovernmentOfficial ? ' (Govt official)' : ''}</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Value" value={fmt.kes(viewing.value)} />
                <MiniStat label="Date" value={fmt.date(viewing.date)} />
                <MiniStat label="Flagged" value={viewing.flagged ? <Badge variant="red">Flagged</Badge> : <Badge variant="green">OK</Badge>} />
              </div>

              <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>Description</div>
                <div style={{ fontSize: 13, color: T.dgrey }}>{viewing.description || 'No description.'}</div>
              </div>

              <p style={{ fontSize: 11, color: T.mgrey, margin: '4px 0 0' }}>Flagged is server-computed based on value thresholds, not editable.</p>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewing(null); setEditing(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditing(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Select label="Employee" value={editForm.employeeUserId} onChange={v => setEditForm({ ...editForm, employeeUserId: v })} options={[{ value: '', label: '— Select employee —' }, ...employees]} required />
              <Select label="Direction" value={editForm.direction} onChange={v => setEditForm({ ...editForm, direction: v })} options={GIFT_DIRECTIONS.map(d => ({ value: d, label: d }))} />
              <Input label="Counterparty" value={editForm.counterpartyName} onChange={v => setEditForm({ ...editForm, counterpartyName: v })} required />
              <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 14 }}>
                <input type="checkbox" checked={editForm.isGovernmentOfficial} onChange={e => setEditForm({ ...editForm, isGovernmentOfficial: e.target.checked })} />
                Counterparty is a government official (Kshs 2,000 threshold applies)
              </label>
              <Input label="Value (Kshs)" type="number" value={editForm.value} onChange={v => setEditForm({ ...editForm, value: v })} required />
              <Input label="Date" type="date" value={editForm.date} onChange={v => setEditForm({ ...editForm, date: v })} />
              <Input label="Description" value={editForm.description} onChange={v => setEditForm({ ...editForm, description: v })} />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditing(false)}>Cancel</Btn>
                <Btn onClick={submitEdit} disabled={saving || !editForm.employeeUserId || !editForm.counterpartyName || !editForm.value}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}
    </>
  )
}
