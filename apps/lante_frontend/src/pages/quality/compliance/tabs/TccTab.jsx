import { useState, useEffect, useCallback } from 'react'
import api from '../../../../api/axios.js'
import { T, fmt } from '../../../../theme/tokens.js'
import { Card, Btn, Badge, HelpPanel, SectionHeader, DataTable, Modal, Input, Loading, MiniStat } from '../../../../components/ui.jsx'
import Pagination from '../../../../components/Pagination.jsx'
import { TCC_STATUSES, EMPTY_TCC, EMPTY_TCC_RENEW, ragVariant } from '../constants.js'

const PAGE_SIZE = 20

export default function TccTab({ setMsg }) {
  const [taxCerts, setTaxCerts] = useState([])
  const [loading, setLoading] = useState(true)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [modal, setModal] = useState(null)
  const [saving, setSaving] = useState(false)
  const [tccForm, setTccForm] = useState(EMPTY_TCC)
  const [tccRenewForm, setTccRenewForm] = useState(EMPTY_TCC_RENEW)
  const [activeId, setActiveId] = useState(null)
  const [viewing, setViewing] = useState(null)
  const [editing, setEditing] = useState(false)
  const [editForm, setEditForm] = useState(EMPTY_TCC)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.get('/api/v1/tax-compliance-certs', { params: { page, pageSize: PAGE_SIZE } }).catch(() => ({ data: { data: {} } }))
      const data = res.data?.data || {}
      setTaxCerts(data.items ?? [])
      setTotalCount(data.totalCount ?? 0)
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  async function submitTcc() {
    if (!tccForm.expiryDate) return
    setSaving(true)
    try {
      await api.post('/api/v1/tax-compliance-certs', {
        expiryDate: new Date(tccForm.expiryDate).toISOString(),
        itaxRef: tccForm.itaxRef || null,
        alertDays: Number(tccForm.alertDays) || 60,
      })
      setMsg({ type: 'success', text: 'TCC recorded.' })
      setTccForm(EMPTY_TCC); setModal(null); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to record TCC.' })
    } finally { setSaving(false) }
  }

  function openRenewTcc(t) {
    setActiveId(t.id)
    setTccRenewForm({ newExpiryDate: '', itaxRef: t.itaxRef ?? '' })
    setModal('tccRenew')
  }

  async function submitRenewTcc() {
    if (!tccRenewForm.newExpiryDate) return
    setSaving(true)
    try {
      await api.patch(`/api/v1/tax-compliance-certs/${activeId}/renew`, {
        newExpiryDate: new Date(tccRenewForm.newExpiryDate).toISOString(),
        itaxRef: tccRenewForm.itaxRef || null,
      })
      setMsg({ type: 'success', text: 'TCC renewed.' })
      setModal(null); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to renew TCC.' })
    } finally { setSaving(false) }
  }

  function openView(t, startEditing = false) {
    setViewing(t)
    setEditing(startEditing)
    setEditForm({
      expiryDate: t.expiryDate ? t.expiryDate.slice(0, 10) : '',
      itaxRef: t.itaxRef ?? '',
      alertDays: t.alertDays ?? 60,
    })
  }

  async function submitEdit() {
    if (!viewing || !editForm.expiryDate) return
    setSaving(true)
    try {
      await api.put(`/api/v1/tax-compliance-certs/${viewing.id}`, {
        expiryDate: new Date(editForm.expiryDate).toISOString(),
        itaxRef: editForm.itaxRef || null,
        alertDays: Number(editForm.alertDays) || 60,
      })
      setMsg({ type: 'success', text: 'TCC record updated.' })
      setViewing(null); setEditing(false); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update TCC record.' })
    } finally { setSaving(false) }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About the Tax Compliance Certificate">
        Tracks the current KRA Tax Compliance Certificate expiry and iTax reference, alerting
        Finance ahead of expiry (RAG-coded by the alert lead time). Click any row to see the full
        record and edit its expiry, iTax reference, or alert lead time.
        <br /><br />
        <strong>Renew</strong> records a fresh certificate period once KRA issues a new TCC — it
        doesn't change the current record's editable fields directly, only "Edit" does that.
      </HelpPanel>

      <SectionHeader title="Tax Compliance Certificate" sub="Current TCC status and expiry — 60-day alert auto-reminds Finance to renew via KRA iTax" action={<Btn onClick={() => setModal('tcc')}>+ Record TCC</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Status', 'Expiry', 'iTax Ref', 'RAG', 'Actions']}
          empty="No TCC recorded yet."
          rows={taxCerts.map(t => [
            <Badge variant={t.status === 0 ? 'green' : t.status === 1 ? 'red' : 'amber'}>{TCC_STATUSES[t.status] ?? t.status}</Badge>,
            fmt.date(t.expiryDate),
            t.itaxRef ?? '—',
            <Badge variant={ragVariant(t.rag)}>{t.rag}</Badge>,
            <div style={{ display: 'flex', gap: 4, alignItems: 'center', flexWrap: 'wrap' }}>
              <Btn variant="ghost" size="sm" onClick={() => openView(t)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={() => openView(t, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
              <Btn size="sm" onClick={() => openRenewTcc(t)}>Renew</Btn>
            </div>,
          ])}
        />
      </Card>
      <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />

      {modal === 'tcc' && (
        <Modal title="Record Tax Compliance Certificate" onClose={() => setModal(null)}>
          <Input label="Expiry Date" type="date" value={tccForm.expiryDate} onChange={v => setTccForm({ ...tccForm, expiryDate: v })} required />
          <Input label="iTax Reference" value={tccForm.itaxRef} onChange={v => setTccForm({ ...tccForm, itaxRef: v })} />
          <Input label="Alert Lead Time (days)" type="number" value={tccForm.alertDays} onChange={v => setTccForm({ ...tccForm, alertDays: v })} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitTcc} disabled={saving || !tccForm.expiryDate}>{saving ? 'Saving…' : 'Record TCC'}</Btn>
          </div>
        </Modal>
      )}

      {modal === 'tccRenew' && (
        <Modal title="Renew Tax Compliance Certificate" onClose={() => setModal(null)}>
          <Input label="New Expiry Date" type="date" value={tccRenewForm.newExpiryDate} onChange={v => setTccRenewForm({ ...tccRenewForm, newExpiryDate: v })} required />
          <Input label="iTax Reference" value={tccRenewForm.itaxRef} onChange={v => setTccRenewForm({ ...tccRenewForm, itaxRef: v })} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitRenewTcc} disabled={saving || !tccRenewForm.newExpiryDate}>{saving ? 'Saving…' : 'Renew'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit TCC modal ── */}
      {viewing && (
        <Modal title={editing ? 'Edit Tax Compliance Certificate' : 'Tax Compliance Certificate Details'} onClose={() => { setViewing(null); setEditing(false) }} width={620}>
          {!editing ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>Tax Compliance Certificate</span>
                  <Badge variant={viewing.status === 0 ? 'green' : viewing.status === 1 ? 'red' : 'amber'}>{TCC_STATUSES[viewing.status] ?? viewing.status}</Badge>
                </div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>{viewing.itaxRef ? `iTax Ref: ${viewing.itaxRef}` : 'No iTax reference on file'}</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Expiry Date" value={fmt.date(viewing.expiryDate)} />
                <MiniStat label="Alert Days" value={viewing.alertDays} />
                <MiniStat label="RAG" value={<Badge variant={ragVariant(viewing.rag)}>{viewing.rag}</Badge>} />
              </div>

              <p style={{ fontSize: 11, color: T.mgrey, margin: '4px 0 0' }}>Status changes via Renew, not here.</p>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewing(null); setEditing(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditing(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Input label="Expiry Date" type="date" value={editForm.expiryDate} onChange={v => setEditForm({ ...editForm, expiryDate: v })} required />
              <Input label="iTax Reference" value={editForm.itaxRef} onChange={v => setEditForm({ ...editForm, itaxRef: v })} />
              <Input label="Alert Lead Time (days)" type="number" value={editForm.alertDays} onChange={v => setEditForm({ ...editForm, alertDays: v })} />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditing(false)}>Cancel</Btn>
                <Btn onClick={submitEdit} disabled={saving || !editForm.expiryDate}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}
    </>
  )
}
