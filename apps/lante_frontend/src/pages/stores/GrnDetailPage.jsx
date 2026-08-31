import { useState, useEffect, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import api from '../../api/axios.js'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Btn, Badge, Alert, SectionHeader, DataTable, Loading } from '../../components/ui.jsx'
import { exportGrnPdf } from '../../utils/export.js'
import { ArrowLeft, Search, Download } from 'lucide-react'

const PAD = 'clamp(16px, 2.4vw, 26px)'
const STATUS_BADGE = { Passed: 'green', Failed: 'red', Pending: 'blue' }
const STOCK_STATUS_BADGE = { InStock: 'green', Issued: 'blue', Sold: 'default', Reserved: 'amber', WrittenOff: 'red' }

export default function GrnDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()

  const [grn, setGrn] = useState(null)
  const [stockUnits, setStockUnits] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const [inspecting, setInspecting] = useState(false)
  const [inspectNotes, setInspectNotes] = useState('')
  const [working, setWorking] = useState('')
  const [exportingPdf, setExportingPdf] = useState(false)

  const loadAll = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const grnRes = await api.get(`/api/v1/grn/${id}`)
      setGrn(grnRes.data?.data)
      const unitsRes = await api.get('/api/v1/stock-units', { params: { grnId: id, pageSize: 100 } })
      setStockUnits(unitsRes.data?.data?.items ?? [])
    } catch {
      setError('GRN not found.')
    } finally {
      setLoading(false)
    }
  }, [id])

  useEffect(() => { loadAll() }, [loadAll])

  async function inspect(passed) {
    setWorking(passed ? 'pass' : 'fail')
    setError('')
    try {
      await api.post(`/api/v1/grn/${id}/inspect`, { passed, notes: inspectNotes.trim() || null })
      setInspectNotes('')
      setInspecting(false)
      await loadAll()
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to record inspection.')
    } finally {
      setWorking('')
    }
  }

  async function handleViewPdf() {
    setExportingPdf(true)
    try {
      await exportGrnPdf({ grn, stockUnits })
    } catch {
      setError('Failed to generate PDF.')
    } finally {
      setExportingPdf(false)
    }
  }

  if (loading) {
    return <><div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}><Loading /></div></>
  }

  if (error && !grn) {
    return <><div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}><Alert type="error">{error}</Alert></div></>
  }

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        <button onClick={() => navigate('/modules/stores/grn')}
          style={{ background: 'none', border: 'none', color: T.mgrey, fontSize: 13, cursor: 'pointer', marginBottom: 14, padding: 0, display: 'inline-flex', alignItems: 'center', gap: 6 }}>
          <ArrowLeft size={14} /> Back to GRNs
        </button>

        {error && <Alert type="error">{error}</Alert>}

        <Card style={{ marginBottom: 16 }}>
          <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', flexWrap: 'wrap', gap: 12 }}>
            <div>
              <h1 style={{ fontSize: 20, fontWeight: 700, color: T.navy, margin: 0 }}>{grn.itemCode} — {grn.itemName}</h1>
              <p style={{ fontSize: 13, color: T.mgrey, margin: '4px 0 0' }}>{grn.supplierName}</p>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
              <Badge variant={STATUS_BADGE[grn.inspectionStatus] ?? 'blue'}>{grn.inspectionStatus}</Badge>
              <Btn variant="ghost" size="sm" onClick={() => navigate(`/modules/stores/items/${grn.itemId}`)}>View Item</Btn>
              <Btn variant="ghost" size="sm" disabled={exportingPdf} onClick={handleViewPdf}>
                {exportingPdf ? 'Generating…' : <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><Download size={14} /> View as PDF</span>}
              </Btn>
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: 14, marginTop: 18 }}>
            <div>
              <p style={{ fontSize: 10, color: T.mgrey, fontWeight: 600, textTransform: 'uppercase', margin: 0 }}>Receiving Location</p>
              <p style={{ fontSize: 13, fontWeight: 600, color: T.dgrey, margin: '3px 0 0' }}>{grn.locationName}</p>
            </div>
            <div>
              <p style={{ fontSize: 10, color: T.mgrey, fontWeight: 600, textTransform: 'uppercase', margin: 0 }}>Qty Received</p>
              <p style={{ fontSize: 13, fontWeight: 600, color: T.dgrey, margin: '3px 0 0' }}>{grn.qtyReceived}</p>
            </div>
            <div>
              <p style={{ fontSize: 10, color: T.mgrey, fontWeight: 600, textTransform: 'uppercase', margin: 0 }}>Landed Cost (total)</p>
              <p style={{ fontSize: 13, fontWeight: 600, color: T.dgrey, margin: '3px 0 0' }}>{fmt.kes(grn.landedCost)}</p>
            </div>
            <div>
              <p style={{ fontSize: 10, color: T.mgrey, fontWeight: 600, textTransform: 'uppercase', margin: 0 }}>Unit Cost</p>
              <p style={{ fontSize: 13, fontWeight: 600, color: T.dgrey, margin: '3px 0 0' }}>{fmt.kes(grn.unitCost)}</p>
            </div>
            {grn.alertLevel && grn.alertLevel !== 'None' && (
              <div>
                <p style={{ fontSize: 10, color: T.mgrey, fontWeight: 600, textTransform: 'uppercase', margin: 0 }}>Price Variance</p>
                <p style={{ fontSize: 13, fontWeight: 700, color: T.amber, margin: '3px 0 0' }}>+{grn.variancePct?.toFixed(1)}%</p>
              </div>
            )}
            <div>
              <p style={{ fontSize: 10, color: T.mgrey, fontWeight: 600, textTransform: 'uppercase', margin: 0 }}>Inspected By</p>
              <p style={{ fontSize: 13, fontWeight: 600, color: T.dgrey, margin: '3px 0 0' }}>{grn.inspectedBy || '—'}</p>
            </div>
            <div>
              <p style={{ fontSize: 10, color: T.mgrey, fontWeight: 600, textTransform: 'uppercase', margin: 0 }}>Inspected At</p>
              <p style={{ fontSize: 13, fontWeight: 600, color: T.dgrey, margin: '3px 0 0' }}>{grn.inspectedAt ? fmt.date(grn.inspectedAt) : '—'}</p>
            </div>
          </div>

          {grn.notes && (
            <p style={{ marginTop: 16, fontSize: 13, color: T.dgrey, background: T.offwt, border: `1px solid ${T.lgrey}`, borderRadius: 8, padding: '10px 14px', whiteSpace: 'pre-wrap' }}>
              {grn.notes}
            </p>
          )}
        </Card>

        {grn.inspectionStatus === 'Pending' && !inspecting && (
          <Card style={{ marginBottom: 16 }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 10 }}>
              <p style={{ fontSize: 13, color: T.dgrey, margin: 0 }}>This GRN is awaiting inspection before stock units are created.</p>
              <Btn onClick={() => setInspecting(true)}><span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><Search size={14} /> Inspect this GRN</span></Btn>
            </div>
          </Card>
        )}

        {grn.inspectionStatus === 'Pending' && inspecting && (
          <Card style={{ marginBottom: 16 }}>
            <SectionHeader title="Inspect this GRN" />
            <textarea rows={3} value={inspectNotes} onChange={e => setInspectNotes(e.target.value)}
              placeholder="Inspection notes (optional)…"
              style={{ width: '100%', padding: '9px 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13, color: T.dgrey, marginBottom: 14, boxSizing: 'border-box', resize: 'none', fontFamily: 'inherit' }} />
            <div style={{ display: 'flex', gap: 10 }}>
              <Btn variant="green" disabled={!!working} onClick={() => inspect(true)} style={{ flex: 1 }}>
                {working === 'pass' ? 'Processing…' : 'Pass Inspection'}
              </Btn>
              <Btn variant="danger" disabled={!!working} onClick={() => inspect(false)} style={{ flex: 1 }}>
                {working === 'fail' ? 'Processing…' : 'Fail Inspection'}
              </Btn>
              <Btn variant="ghost" disabled={!!working} onClick={() => setInspecting(false)}>Cancel</Btn>
            </div>
          </Card>
        )}

        <SectionHeader title="Stock Units Generated"
          action={<Btn variant="outline" size="sm" onClick={() => navigate('/modules/stores/grn', { state: { prefillItemId: grn.itemId, prefillLocationId: grn.locationId } })}>+ Add Stock Unit (via GRN)</Btn>} />
        <Card style={{ padding: 0, overflow: 'hidden' }}>
          <DataTable
            headers={['Unit', 'Location', 'Qty Remaining', 'Status']}
            empty={grn.inspectionStatus === 'Pending' ? 'Stock units will be created once this GRN passes inspection.' : 'No stock units generated.'}
            rows={stockUnits.map(u => [
              u.serialNo || 'Bulk lot',
              u.locationName || 'No location set',
              u.qty,
              <Badge variant={STOCK_STATUS_BADGE[u.status] ?? 'green'}>{u.status}</Badge>,
            ])}
          />
        </Card>
      </div>
    </>
  )
}
