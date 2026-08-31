import { useState, useEffect, useCallback } from 'react'
import api from '../../../../api/axios.js'
import { T, fmt } from '../../../../theme/tokens.js'
import { Card, Btn, Badge, HelpPanel, SectionHeader, DataTable, Modal, Input, Select, Loading, MiniStat } from '../../../../components/ui.jsx'
import Pagination from '../../../../components/Pagination.jsx'
import { LICENCE_TYPES, LICENCE_TYPE_LABELS, EMPTY_LICENCE, EMPTY_RENEWAL, today, daysUntil, dueBadge } from '../constants.js'

const PAGE_SIZE = 20

export default function LicencesTab({ setMsg, refreshDashboard }) {
  const [licences, setLicences] = useState([])
  const [loading, setLoading] = useState(true)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [modal, setModal] = useState(null)
  const [saving, setSaving] = useState(false)
  const [licenceForm, setLicenceForm] = useState(EMPTY_LICENCE)
  const [renewalForm, setRenewalForm] = useState(EMPTY_RENEWAL)
  const [activeId, setActiveId] = useState(null)
  const [viewing, setViewing] = useState(null)
  const [editing, setEditing] = useState(false)
  const [editForm, setEditForm] = useState(EMPTY_LICENCE)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.get('/api/v1/compliance-licences', { params: { page, pageSize: PAGE_SIZE } }).catch(() => ({ data: { data: {} } }))
      const data = res.data?.data || {}
      setLicences(data.items ?? [])
      setTotalCount(data.totalCount ?? 0)
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  async function submitLicence() {
    if (!licenceForm.authority || !licenceForm.expiryDate) return
    setSaving(true)
    try {
      await api.post('/api/v1/compliance-licences', {
        type: LICENCE_TYPES.indexOf(licenceForm.type),
        authority: licenceForm.authority,
        licenceNumber: licenceForm.licenceNumber || null,
        issuedOn: licenceForm.issuedOn ? new Date(licenceForm.issuedOn).toISOString() : null,
        expiryDate: new Date(licenceForm.expiryDate).toISOString(),
        alertDays: Number(licenceForm.alertDays) || 90,
        renewalRequirements: licenceForm.renewalRequirements || null,
      })
      setMsg({ type: 'success', text: 'Licence recorded.' })
      setLicenceForm(EMPTY_LICENCE); setModal(null); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to record licence.' })
    } finally { setSaving(false) }
  }

  function openRenewal(l) {
    setActiveId(l.id)
    setRenewalForm({ licenceNumber: l.licenceNumber ?? '', issuedOn: today(), newExpiryDate: '' })
    setModal('renew')
  }

  async function submitRenewal() {
    if (!renewalForm.newExpiryDate) return
    setSaving(true)
    try {
      await api.patch(`/api/v1/compliance-licences/${activeId}/renew`, {
        licenceNumber: renewalForm.licenceNumber || null,
        issuedOn: new Date(renewalForm.issuedOn || Date.now()).toISOString(),
        newExpiryDate: new Date(renewalForm.newExpiryDate).toISOString(),
      })
      setMsg({ type: 'success', text: 'Licence renewed.' })
      setModal(null); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to renew licence.' })
    } finally { setSaving(false) }
  }

  function openView(l, startEditing = false) {
    setViewing(l)
    setEditing(startEditing)
    setEditForm({
      type: LICENCE_TYPES[l.type] ?? l.type,
      authority: l.authority ?? '',
      licenceNumber: l.licenceNumber ?? '',
      issuedOn: l.issuedOn ? l.issuedOn.slice(0, 10) : '',
      expiryDate: l.expiryDate ? l.expiryDate.slice(0, 10) : '',
      alertDays: l.alertDays ?? 90,
      renewalRequirements: l.renewalRequirements ?? '',
    })
  }

  async function submitEdit() {
    if (!viewing || !editForm.authority || !editForm.expiryDate) return
    setSaving(true)
    try {
      await api.put(`/api/v1/compliance-licences/${viewing.id}`, {
        type: LICENCE_TYPES.indexOf(editForm.type),
        authority: editForm.authority,
        licenceNumber: editForm.licenceNumber || null,
        issuedOn: editForm.issuedOn ? new Date(editForm.issuedOn).toISOString() : null,
        expiryDate: new Date(editForm.expiryDate).toISOString(),
        alertDays: Number(editForm.alertDays) || 90,
        renewalRequirements: editForm.renewalRequirements || null,
      })
      setMsg({ type: 'success', text: 'Licence record updated.' })
      setViewing(null); setEditing(false); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update licence record.' })
    } finally { setSaving(false) }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About the Regulatory Licence Tracker">
        Tracks regulatory licences and permits (NCA, KRA PIN, NEMA, KEBS/NMK, DOSHS) along with
        their expiry dates, with automatic alerts as the expiry date approaches. Click any row to
        see the full record and edit its details.
        <br /><br />
        <strong>Renew</strong> starts a new licence period — it creates a new record and retires the
        current one, keeping a full renewal history. <strong>Edit</strong> only corrects a mistake on
        the current record (e.g. a typo in the licence number) without starting a new cycle.
      </HelpPanel>

      <SectionHeader title="Regulatory Licence Tracker" sub="NCA, KRA PIN, NEMA, KEBS/NMK, DOSHS — expiry alerts (STAT-003/004/005/007)" action={<Btn onClick={() => setModal('licence')}>+ Add Licence</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Type', 'Authority', 'Licence No.', 'Issued', 'Expiry', 'Status', 'Actions']}
          empty="No regulatory licences recorded yet."
          rows={licences.map(l => [
            <Badge variant="navy">{LICENCE_TYPE_LABELS[LICENCE_TYPES[l.type]] ?? l.type}</Badge>,
            <span onClick={() => openView(l)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{l.authority}</span>,
            l.licenceNumber ?? '—',
            l.issuedOn ? fmt.date(l.issuedOn) : '—',
            fmt.date(l.expiryDate),
            dueBadge(daysUntil(l.expiryDate), 'Current'),
            <div style={{ display: 'flex', gap: 4, alignItems: 'center', flexWrap: 'wrap' }}>
              <Btn variant="ghost" size="sm" onClick={() => openView(l)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={() => openView(l, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
              <Btn size="sm" onClick={() => openRenewal(l)}>Renew</Btn>
            </div>,
          ])}
        />
      </Card>
      <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />

      {modal === 'licence' && (
        <Modal title="Add Regulatory Licence" onClose={() => setModal(null)}>
          <Select label="Type" value={licenceForm.type} onChange={v => setLicenceForm({ ...licenceForm, type: v })}
            options={LICENCE_TYPES.map(t => ({ value: t, label: LICENCE_TYPE_LABELS[t] }))} />
          <Input label="Authority" value={licenceForm.authority} onChange={v => setLicenceForm({ ...licenceForm, authority: v })} required placeholder="e.g. NCA, KRA, NEMA, NMK" />
          <Input label="Licence Number" value={licenceForm.licenceNumber} onChange={v => setLicenceForm({ ...licenceForm, licenceNumber: v })} />
          <Input label="Issued On" type="date" value={licenceForm.issuedOn} onChange={v => setLicenceForm({ ...licenceForm, issuedOn: v })} />
          <Input label="Expiry Date" type="date" value={licenceForm.expiryDate} onChange={v => setLicenceForm({ ...licenceForm, expiryDate: v })} required />
          <Input label="Alert Lead Time (days)" type="number" value={licenceForm.alertDays} onChange={v => setLicenceForm({ ...licenceForm, alertDays: v })}
            note="KEBS/NMK licences always additionally get a fixed second reminder at 30 days." />
          <Input label="Renewal Requirements" value={licenceForm.renewalRequirements} onChange={v => setLicenceForm({ ...licenceForm, renewalRequirements: v })} placeholder="e.g. audited accounts, staff, equipment evidence" />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitLicence} disabled={saving || !licenceForm.authority || !licenceForm.expiryDate}>{saving ? 'Saving…' : 'Add Licence'}</Btn>
          </div>
        </Modal>
      )}

      {modal === 'renew' && (
        <Modal title="Renew Licence" onClose={() => setModal(null)}>
          <Input label="Licence Number" value={renewalForm.licenceNumber} onChange={v => setRenewalForm({ ...renewalForm, licenceNumber: v })} />
          <Input label="Issued On" type="date" value={renewalForm.issuedOn} onChange={v => setRenewalForm({ ...renewalForm, issuedOn: v })} />
          <Input label="New Expiry Date" type="date" value={renewalForm.newExpiryDate} onChange={v => setRenewalForm({ ...renewalForm, newExpiryDate: v })} required />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitRenewal} disabled={saving || !renewalForm.newExpiryDate}>{saving ? 'Saving…' : 'Renew'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit Licence modal ── */}
      {viewing && (
        <Modal title={editing ? 'Edit Licence' : 'Licence Details'} onClose={() => { setViewing(null); setEditing(false) }} width={620}>
          {!editing ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewing.authority}</span>
                  <Badge variant="navy">{LICENCE_TYPE_LABELS[LICENCE_TYPES[viewing.type]] ?? viewing.type}</Badge>
                </div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>{viewing.licenceNumber ? `Licence No. ${viewing.licenceNumber}` : 'No licence number on file'}</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Issued On" value={viewing.issuedOn ? fmt.date(viewing.issuedOn) : '—'} />
                <MiniStat label="Expiry Date" value={fmt.date(viewing.expiryDate)} />
                <MiniStat label="Alert Days" value={viewing.alertDays ?? 90} />
              </div>

              <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>Renewal Requirements</div>
                <div style={{ fontSize: 13, color: T.dgrey }}>{viewing.renewalRequirements || 'None specified.'}</div>
              </div>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewing(null); setEditing(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditing(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Select label="Type" value={editForm.type} onChange={v => setEditForm({ ...editForm, type: v })}
                options={LICENCE_TYPES.map(t => ({ value: t, label: LICENCE_TYPE_LABELS[t] }))} />
              <Input label="Authority" value={editForm.authority} onChange={v => setEditForm({ ...editForm, authority: v })} required />
              <Input label="Licence Number" value={editForm.licenceNumber} onChange={v => setEditForm({ ...editForm, licenceNumber: v })} />
              <Input label="Issued On" type="date" value={editForm.issuedOn} onChange={v => setEditForm({ ...editForm, issuedOn: v })} />
              <Input label="Expiry Date" type="date" value={editForm.expiryDate} onChange={v => setEditForm({ ...editForm, expiryDate: v })} required />
              <Input label="Alert Lead Time (days)" type="number" value={editForm.alertDays} onChange={v => setEditForm({ ...editForm, alertDays: v })}
                note="KEBS/NMK licences always additionally get a fixed second reminder at 30 days." />
              <Input label="Renewal Requirements" value={editForm.renewalRequirements} onChange={v => setEditForm({ ...editForm, renewalRequirements: v })} />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditing(false)}>Cancel</Btn>
                <Btn onClick={submitEdit} disabled={saving || !editForm.authority || !editForm.expiryDate}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}
    </>
  )
}
