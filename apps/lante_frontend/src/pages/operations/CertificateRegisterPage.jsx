import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { Card, DataTable, Btn, Badge, Loading, Alert, Modal, Input, Select, Kpi, KPI_GRID, SectionHeader }
  from '../../components/ui.jsx'
import { useCertificateRegister, validityBadge, recallTrail } from '../../hooks/operations/useCertificateRegister.js'

// O6.1 — register of issued calibration certificates. Three jobs: see what has been issued, see
// what is falling due (and whether the client was chased), and answer "how many, by whom, when"
// for an ISO 17025 audit. Read-only over an immutable record — the only write is withdrawal.

const SHEET_LABEL = {
  Mass:            'Mass',
  NawiBalance:     'NAWI · Balance',
  NawiWeighbridge: 'NAWI · Weighbridge',
}

function fmt(iso) {
  if (!iso) return '—'
  return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}

export default function CertificateRegisterPage() {
  const navigate = useNavigate()
  const {
    items, summary, total, loading, error,
    page, setPage, totalPages,
    filters, updateFilter, resetFilters,
    canWithdraw, withdraw, loadPdfUrl,
  } = useCertificateRegister()

  const [preview, setPreview]         = useState(null)   // { cert, url, loading, error }
  const [withdrawing, setWithdrawing] = useState(null)   // certificate row being withdrawn
  const [reason, setReason]           = useState('')
  const [busy, setBusy]               = useState(false)
  const [actionError, setActionError] = useState('')

  const openPreview = async (cert) => {
    setPreview({ cert, url: null, loading: true, error: '' })
    try {
      setPreview({ cert, url: await loadPdfUrl(cert.id), loading: false, error: '' })
    } catch {
      setPreview({ cert, url: null, loading: false, error: 'Could not render this certificate.' })
    }
  }

  const closePreview = () => {
    // Release the blob; without this each preview holds a copy of the PDF for the life of the tab.
    if (preview?.url) URL.revokeObjectURL(preview.url)
    setPreview(null)
  }

  // Same cleanup when the page unmounts with a preview still open.
  useEffect(() => () => { if (preview?.url) URL.revokeObjectURL(preview.url) }, [preview?.url])

  const submitWithdraw = async () => {
    if (!reason.trim()) { setActionError('A reason is required.'); return }
    setBusy(true); setActionError('')
    try {
      await withdraw(withdrawing.id, reason.trim())
      setWithdrawing(null); setReason('')
    } catch (e) {
      setActionError(e?.response?.data?.message ?? 'Failed to withdraw the certificate.')
    } finally { setBusy(false) }
  }

  const headers = ['Certificate', 'Client', 'Item', 'Issued', 'Next due', 'Status', 'Recalls sent', 'Signatory', '']
  const rows = items.map(c => {
    const vb    = validityBadge(c)
    const trail = recallTrail(c)
    return [
      <span style={{ fontWeight: 600 }}>{c.number}</span>,
      <div>
        <div>{c.clientName || '—'}</div>
        {/* Without a CRM anchor a recall can only fall back to the address captured on the
            original request, so flag it — it's the difference between chasing the client and
            hoping the old address still works. */}
        {!c.crmCustomerId && (
          <div style={{ fontSize: 11, color: '#b8901f' }} title="No CRM customer linked — recalls fall back to the address on the original service request">
            not linked to CRM
          </div>
        )}
      </div>,
      <div>
        <div>{c.equipment || SHEET_LABEL[c.sheetType] || c.sheetType || '—'}</div>
        {c.serialNo && <div style={{ fontSize: 11, color: '#9aa7b4' }}>S/N {c.serialNo}</div>}
      </div>,
      fmt(c.issuedAt),
      fmt(c.nextCalibrationDue),
      <Badge variant={vb.variant}>{vb.label}</Badge>,
      trail.length
        ? <span style={{ fontSize: 12 }}>{trail.join(' · ')}</span>
        : <span style={{ fontSize: 12, color: '#9aa7b4' }}>none</span>,
      c.signatoryName || '—',
      <div style={{ display: 'flex', gap: 6, justifyContent: 'flex-end' }}>
        {c.assignmentId && (
          <Btn size="sm" variant="outline"
               onClick={e => { e.stopPropagation(); navigate(`/modules/operations/assignments/${c.assignmentId}/certificate`) }}>
            Open
          </Btn>
        )}
        {canWithdraw && !c.withdrawnAt && (
          <Btn size="sm" variant="danger"
               onClick={e => { e.stopPropagation(); setWithdrawing(c); setReason(''); setActionError('') }}>
            Withdraw
          </Btn>
        )}
      </div>,
    ]
  })

  return (
    <>
      <main className="flex-1 max-w-7xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">
        <div className="mb-6">
          <h1 className="text-2xl font-extrabold text-zinc-950">Issued Certificates</h1>
          <p className="text-sm text-zinc-500 mt-1">
            Register of issued calibration certificates — recall tracking and audit counts.
          </p>
        </div>

        {error && <Alert type="error">{error}</Alert>}

        {/* Audit counts. These describe every certificate matching the current filter, not just
            this page — a count that only covered one page would be misleading in an audit. */}
        <div style={{ ...KPI_GRID, marginBottom: 18 }}>
          <Kpi label="Certificates" value={summary?.total ?? 0} sub="matching filter" loading={loading} />
          <Kpi label="In force"     value={summary?.valid ?? 0} variant="green" loading={loading} />
          <Kpi label="Falling due"  value={summary?.expiring ?? 0} variant="amber"
               sub="within 60 days" loading={loading} />
          <Kpi label="Expired"      value={summary?.expired ?? 0} variant="red" loading={loading} />
          <Kpi label="Withdrawn"    value={summary?.withdrawn ?? 0} loading={loading} />
        </div>

        <Card style={{ padding: 0, marginBottom: 18 }}>
          <div style={{ padding: '12px 16px', display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'flex-end' }}>
            <div style={{ minWidth: 220, flex: 1 }}>
              <Input label="Search" value={filters.q} placeholder="Certificate no., client or signatory"
                     onChange={v => updateFilter('q', v)} />
            </div>
            <div style={{ minWidth: 150 }}>
              <Select label="Status" value={filters.validity} onChange={v => updateFilter('validity', v)}
                      options={[
                        { value: '',          label: 'All' },
                        { value: 'valid',     label: 'In force' },
                        { value: 'expiring',  label: 'Falling due' },
                        { value: 'expired',   label: 'Expired' },
                        { value: 'withdrawn', label: 'Withdrawn' },
                        { value: 'unknown',   label: 'No due date' },
                      ]} />
            </div>
            <div style={{ minWidth: 170 }}>
              <Select label="Type" value={filters.sheetType} onChange={v => updateFilter('sheetType', v)}
                      options={[
                        { value: '',                label: 'All' },
                        { value: 'Mass',            label: 'Mass' },
                        { value: 'NawiBalance',     label: 'NAWI · Balance' },
                        { value: 'NawiWeighbridge', label: 'NAWI · Weighbridge' },
                      ]} />
            </div>
            <div style={{ minWidth: 150 }}>
              <Input label="Issued from" type="date" value={filters.issuedFrom}
                     onChange={v => updateFilter('issuedFrom', v)} />
            </div>
            <div style={{ minWidth: 150 }}>
              <Input label="Issued to" type="date" value={filters.issuedTo}
                     onChange={v => updateFilter('issuedTo', v)} />
            </div>
            <Btn variant="outline" onClick={resetFilters}>Reset</Btn>
          </div>
        </Card>

        <Card style={{ padding: 0 }}>
          <div style={{ padding: '12px 16px', borderBottom: '1px solid #eef1f5', display: 'flex', alignItems: 'center' }}>
            <span style={{ fontSize: 12, color: '#9aa7b4' }}>
              {total} certificate{total === 1 ? '' : 's'}
              {totalPages > 1 && ` · page ${page} of ${totalPages}`}
            </span>
            {totalPages > 1 && (
              <div style={{ display: 'flex', gap: 6, marginLeft: 'auto' }}>
                <Btn size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</Btn>
                <Btn size="sm" variant="outline" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>Next</Btn>
              </div>
            )}
          </div>
          {loading
            ? <Loading />
            : <DataTable headers={headers} rows={rows}
                         onRowClick={(_row, i) => openPreview(items[i])}
                         empty="No issued certificates match this filter." />}
        </Card>

        {/* Audit breakdowns — the questions an assessor actually asks. */}
        {summary && summary.total > 0 && (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 14, marginTop: 18 }}>
            <Card>
              <SectionHeader title="Issued by month" sub="Certificates issued per calendar month" />
              <Breakdown data={summary.issuedByMonth} />
            </Card>
            <Card>
              <SectionHeader title="Issued by type" />
              <Breakdown data={summary.issuedBySheetType} labelMap={SHEET_LABEL} />
            </Card>
            <Card>
              <SectionHeader title="Issued by signatory" sub="Authorised signatory of record" />
              <Breakdown data={summary.issuedBySignatory} />
            </Card>
          </div>
        )}
      </main>

      {preview && (
        <Modal title={`Certificate ${preview.cert.number}`} onClose={closePreview} width={900}>
          <p style={{ fontSize: 12, color: '#5b6b7c', marginBottom: 10 }}>
            {preview.cert.clientName || '—'} · issued {fmt(preview.cert.issuedAt)} · next due{' '}
            {fmt(preview.cert.nextCalibrationDue)}
            {preview.cert.withdrawnAt && (
              <span style={{ color: '#b91c1c', fontWeight: 600 }}>
                {' '}· WITHDRAWN {fmt(preview.cert.withdrawnAt)}
                {preview.cert.withdrawnReason ? ` — ${preview.cert.withdrawnReason}` : ''}
              </span>
            )}
          </p>

          {preview.loading && <Loading />}
          {preview.error && <Alert type="error">{preview.error}</Alert>}

          {preview.url && (
            <>
              {/* Rendered from the certificate's stored snapshot, so this is the document that was
                  signed — not a re-derivation from the current data sheet. */}
              <iframe
                title={`Certificate ${preview.cert.number}`}
                src={preview.url}
                style={{ width: '100%', height: '70vh', border: '1px solid #C9D2DE', borderRadius: 4, background: '#fff' }}
              />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
                <Btn variant="outline" onClick={closePreview}>Close</Btn>
                <a href={preview.url} download={`${preview.cert.number}.pdf`} style={{ textDecoration: 'none' }}>
                  <Btn variant="primary">Download PDF</Btn>
                </a>
              </div>
            </>
          )}
        </Modal>
      )}

      {withdrawing && (
        <Modal title={`Withdraw ${withdrawing.number}`} onClose={() => !busy && setWithdrawing(null)}>
          <Alert type="warning">
            The certificate stays in the register — an audit trail must not lose records — but it
            stops generating client recalls and leaves the in-force counts.
          </Alert>
          {actionError && <Alert type="error">{actionError}</Alert>}
          <Input label="Reason" value={reason} onChange={setReason} required
                 placeholder="e.g. superseded by CAL-2026-0042; issued in error" />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 14 }}>
            <Btn variant="outline" onClick={() => setWithdrawing(null)} disabled={busy}>Cancel</Btn>
            <Btn variant="danger" onClick={submitWithdraw} disabled={busy}>
              {busy ? 'Withdrawing…' : 'Withdraw certificate'}
            </Btn>
          </div>
        </Modal>
      )}
    </>
  )
}

// Simple count breakdown with a proportional bar, so the shape is readable without a chart library.
function Breakdown({ data, labelMap }) {
  const entries = Object.entries(data ?? {})
  if (entries.length === 0) return <p style={{ fontSize: 12, color: '#9aa7b4' }}>No data.</p>
  const max = Math.max(...entries.map(([, v]) => v))

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: 4 }}>
      {entries.map(([key, value]) => (
        <div key={key}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, marginBottom: 3 }}>
            <span style={{ color: '#5b6b7c' }}>{labelMap?.[key] ?? key}</span>
            <span style={{ fontWeight: 600 }}>{value}</span>
          </div>
          <div style={{ height: 5, background: '#eef1f5', borderRadius: 3, overflow: 'hidden' }}>
            <div style={{ width: `${(value / max) * 100}%`, height: '100%', background: '#B8901F' }} />
          </div>
        </div>
      ))}
    </div>
  )
}
