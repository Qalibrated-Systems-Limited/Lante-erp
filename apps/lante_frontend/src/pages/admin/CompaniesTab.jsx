import { useState, useEffect, useCallback } from 'react'
import api from '../../api/axios.js'
import { Card, DataTable, Badge, Btn, Alert, Loading, SectionHeader, Modal, Input, Select } from '../../components/ui.jsx'

// Real data: Compliance's Inter-Company Management module already tracks sister companies/
// affiliates (RelatedParty) for ICSA and intercompany-transaction disclosure — this tab reuses
// that same endpoint rather than inventing a separate "companies" concept.
const RELATIONSHIPS = ['Subsidiary', 'SisterCompany', 'Affiliate', 'JointVenture', 'Other']
const RELATIONSHIP_LABELS = { Subsidiary: 'Subsidiary', SisterCompany: 'Sister Company', Affiliate: 'Affiliate', JointVenture: 'Joint Venture', Other: 'Other' }

export default function CompaniesTab() {
  const [parties, setParties] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const pageSize = 20

  const [modal, setModal] = useState(null) // null | 'create' | 'edit'
  const [selected, setSelected] = useState(null)
  const [form, setForm] = useState({ companyName: '', regNo: '', relationship: 'SisterCompany', notes: '' })
  const [submitting, setSubmitting] = useState(false)
  const [formError, setFormError] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await api.get('/api/v1/compliance-icm-related-parties', { params: { page, pageSize } })
      const data = res.data?.data
      setParties(data?.items ?? [])
      setTotalCount(data?.totalCount ?? 0)
    } catch {
      setError('Failed to load companies.')
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  function openCreate() {
    setForm({ companyName: '', regNo: '', relationship: 'SisterCompany', notes: '' })
    setFormError('')
    setModal('create')
  }
  function openEdit(p) {
    setSelected(p)
    setForm({ companyName: p.companyName, regNo: p.regNo, relationship: RELATIONSHIPS[p.relationship] ?? 'Other', notes: p.notes ?? '' })
    setFormError('')
    setModal('edit')
  }

  async function handleSave() {
    if (!form.companyName.trim() || !form.regNo.trim()) {
      setFormError('Company name and registration number are required.')
      return
    }
    setSubmitting(true)
    setFormError('')
    try {
      const payload = { companyName: form.companyName.trim(), regNo: form.regNo.trim(), relationship: RELATIONSHIPS.indexOf(form.relationship), notes: form.notes.trim() || null }
      if (modal === 'edit') await api.put(`/api/v1/compliance-icm-related-parties/${selected.id}`, payload)
      else await api.post('/api/v1/compliance-icm-related-parties', payload)
      setModal(null)
      load()
    } catch (err) {
      setFormError(err.response?.data?.message ?? 'Failed to save.')
    } finally {
      setSubmitting(false)
    }
  }

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))

  return (
    <>
      <Alert type="info">
        Sister companies and affiliates registered under Compliance → Inter-Company Management for related-party
        disclosure. The company's own staff and resources always do the work — these entities are the legal party a
        project, client, or invoice may be filed under.
      </Alert>
      {error && <Alert type="error">{error}</Alert>}

      <SectionHeader title="Companies" sub={`${totalCount} registered`} action={<Btn size="sm" onClick={openCreate}>+ Add Company</Btn>} />

      {loading ? <Loading /> : (
        <Card style={{ padding: 0, overflow: 'hidden' }}>
          <DataTable
            headers={['Company Name', 'Reg. No', 'Relationship', 'Notes', 'Actions']}
            empty="No companies registered yet."
            rows={parties.map(p => [
              <strong>{p.companyName}</strong>,
              <span style={{ fontFamily: 'monospace', fontSize: 11 }}>{p.regNo}</span>,
              <Badge variant={p.relationship === 0 ? 'navy' : 'amber'}>{RELATIONSHIP_LABELS[RELATIONSHIPS[p.relationship]] ?? 'Other'}</Badge>,
              p.notes || <span style={{ color: '#94A3B8' }}>—</span>,
              <Btn size="sm" variant="ghost" onClick={() => openEdit(p)}>Edit</Btn>,
            ])}
          />
        </Card>
      )}

      {totalPages > 1 && (
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 14 }}>
          <span style={{ fontSize: 12, color: '#6b7280' }}>Page {page} of {totalPages}</span>
          <div style={{ display: 'flex', gap: 8 }}>
            <Btn size="sm" variant="ghost" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Previous</Btn>
            <Btn size="sm" variant="ghost" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>Next</Btn>
          </div>
        </div>
      )}

      {modal && (
        <Modal title={modal === 'edit' ? 'Edit Company' : 'Add Company'} onClose={() => setModal(null)}>
          {formError && <Alert type="error">{formError}</Alert>}
          <Input label="Company Name" value={form.companyName} onChange={v => setForm(f => ({ ...f, companyName: v }))} required />
          <Input label="Registration Number" value={form.regNo} onChange={v => setForm(f => ({ ...f, regNo: v }))} required />
          <Select label="Relationship" value={form.relationship} onChange={v => setForm(f => ({ ...f, relationship: v }))}
            options={RELATIONSHIPS.map(r => ({ value: r, label: RELATIONSHIP_LABELS[r] }))} />
          <Input label="Notes" value={form.notes} onChange={v => setForm(f => ({ ...f, notes: v }))} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={handleSave} disabled={submitting}>{submitting ? 'Saving…' : 'Save'}</Btn>
          </div>
        </Modal>
      )}
    </>
  )
}
