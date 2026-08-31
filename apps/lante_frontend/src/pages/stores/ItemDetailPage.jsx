import { useState, useEffect, useCallback, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { QRCodeSVG } from 'qrcode.react'
import Barcode from 'react-barcode'
import api from '../../api/axios.js'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Kpi, Btn, Badge, Alert, SectionHeader, DataTable, Loading } from '../../components/ui.jsx'
import ImagePicker from '../../components/ImagePicker.jsx'
import { ArrowLeft, Printer } from 'lucide-react'

const PAD = 'clamp(16px, 2.4vw, 26px)'

async function uploadImages(files, itemId) {
  for (const file of files) {
    const fd = new FormData()
    fd.append('image', file)
    await api.post(`/api/v1/item-photos/item/${itemId}`, fd).catch(() => {})
  }
}

const ALERT_BADGE = { Critical: 'red', Warning: 'amber', None: 'default' }
const STOCK_STATUS_BADGE = { InStock: 'green', Issued: 'blue', Sold: 'default', Reserved: 'amber', WrittenOff: 'red' }

export default function ItemDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()

  const [item, setItem] = useState(null)
  const [priceHistory, setPriceHistory] = useState([])
  const [stockUnits, setStockUnits] = useState([])
  const [grns, setGrns] = useState([])
  const [balances, setBalances] = useState([])
  const [movements, setMovements] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [acknowledging, setAcknowledging] = useState(false)

  const [images, setImages] = useState([])
  const [imagesLoading, setImagesLoading] = useState(true)
  const [pendingImages, setPendingImages] = useState([])
  const [uploadingImages, setUploadingImages] = useState(false)
  const printRef = useRef(null)

  const loadAll = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const [itemRes, historyRes, unitsRes, grnRes, balancesRes, movementsRes] = await Promise.all([
        api.get(`/api/v1/items/${id}`),
        api.get(`/api/v1/items/${id}/price-history`),
        api.get('/api/v1/stock-units', { params: { itemId: id, pageSize: 100 } }),
        api.get('/api/v1/grn', { params: { itemId: id, pageSize: 20 } }),
        api.get(`/api/v1/stock-movements/balances/${id}`),
        api.get('/api/v1/stock-movements', { params: { itemId: id, pageSize: 20 } }),
      ])
      setItem(itemRes.data?.data)
      setPriceHistory(historyRes.data?.data ?? [])
      setStockUnits(unitsRes.data?.data?.items ?? [])
      setGrns(grnRes.data?.data?.items ?? [])
      setBalances(balancesRes.data?.data?.balances ?? [])
      setMovements(movementsRes.data?.data?.items ?? [])
    } catch {
      setError('Item not found.')
    } finally {
      setLoading(false)
    }
  }, [id])

  const loadImages = useCallback(async () => {
    setImagesLoading(true)
    try {
      const res = await api.get(`/api/v1/item-photos/item/${id}`)
      setImages(res.data?.data ?? [])
    } catch {
      setImages([])
    } finally {
      setImagesLoading(false)
    }
  }, [id])

  useEffect(() => { loadAll() }, [loadAll])
  useEffect(() => { loadImages() }, [loadImages])

  async function handleDeleteImage(imageId) {
    try {
      await api.delete(`/api/v1/item-photos/${imageId}`)
      setImages(imgs => imgs.filter(i => i.id !== imageId))
    } catch {}
  }

  async function handleUploadImages() {
    if (!pendingImages.length) return
    setUploadingImages(true)
    try {
      await uploadImages(pendingImages, id)
      setPendingImages([])
      loadImages()
    } finally {
      setUploadingImages(false)
    }
  }

  function handlePrintLabel() {
    const printWindow = window.open('', '_blank', 'width=420,height=520')
    if (!printWindow || !printRef.current) return
    printWindow.document.write(`<!doctype html><html><head><title>${item.itemCode} — Label</title></head><body>${printRef.current.innerHTML}</body></html>`)
    printWindow.document.close()
    printWindow.focus()
    printWindow.print()
    printWindow.close()
  }

  async function handleAcknowledge() {
    setAcknowledging(true)
    try {
      await api.post(`/api/v1/items/${id}/acknowledge-low-stock`)
      loadAll()
    } catch {
      setError('Failed to acknowledge low-stock alert.')
    } finally {
      setAcknowledging(false)
    }
  }

  if (loading) {
    return (
      <>
        <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}><Loading /></div>
      </>
    )
  }

  if (error || !item) {
    return (
      <>
        <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
          <Alert type="error">{error || 'Item not found.'}</Alert>
        </div>
      </>
    )
  }

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        <button onClick={() => navigate('/modules/stores/items')}
          style={{ background: 'none', border: 'none', color: T.mgrey, fontSize: 13, cursor: 'pointer', marginBottom: 14, padding: 0, display: 'inline-flex', alignItems: 'center', gap: 6 }}>
          <ArrowLeft size={14} /> Back to Items
        </button>

        <Card style={{ marginBottom: 16 }}>
          <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', flexWrap: 'wrap', gap: 12 }}>
            <div>
              <h1 style={{ fontSize: 20, fontWeight: 700, color: T.navy, margin: 0 }}>{item.itemCode}</h1>
              <p style={{ fontSize: 13, color: T.mgrey, margin: '4px 0 0' }}>{item.description}</p>
            </div>
            {item.isLowStock && (
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <Badge variant="red">Low Stock</Badge>
                {item.isLowStockAcknowledged ? (
                  <span style={{ fontSize: 11, color: T.mgrey }}>Acknowledged</span>
                ) : (
                  <Btn size="sm" variant="ghost" disabled={acknowledging} onClick={handleAcknowledge}>
                    {acknowledging ? 'Acknowledging…' : 'Acknowledge'}
                  </Btn>
                )}
              </div>
            )}
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: 14, marginTop: 18 }}>
            <div>
              <p style={{ fontSize: 10, color: T.mgrey, fontWeight: 600, textTransform: 'uppercase', margin: 0 }}>Supplier</p>
              <p style={{ fontSize: 13, fontWeight: 600, color: T.dgrey, margin: '3px 0 0' }}>{item.supplierName}</p>
            </div>
            <div>
              <p style={{ fontSize: 10, color: T.mgrey, fontWeight: 600, textTransform: 'uppercase', margin: 0 }}>Category</p>
              <p style={{ fontSize: 13, fontWeight: 600, color: T.dgrey, margin: '3px 0 0' }}>{item.categoryName}</p>
            </div>
            <div>
              <p style={{ fontSize: 10, color: T.mgrey, fontWeight: 600, textTransform: 'uppercase', margin: 0 }}>Qty on Hand</p>
              <p style={{ fontSize: 13, fontWeight: 700, color: item.isLowStock ? T.red : T.dgrey, margin: '3px 0 0' }}>{item.qtyOnHand} {item.uom}</p>
            </div>
            <div>
              <p style={{ fontSize: 10, color: T.mgrey, fontWeight: 600, textTransform: 'uppercase', margin: 0 }}>Avg Weighted Cost</p>
              <p style={{ fontSize: 13, fontWeight: 600, color: T.dgrey, margin: '3px 0 0' }}>{fmt.kes(item.avgWeightedCost)}</p>
            </div>
            <div>
              <p style={{ fontSize: 10, color: T.mgrey, fontWeight: 600, textTransform: 'uppercase', margin: 0 }}>Min Selling Price</p>
              <p style={{ fontSize: 13, fontWeight: 600, color: T.dgrey, margin: '3px 0 0' }}>{fmt.kes(item.minSellingPrice)}</p>
            </div>
            <div>
              <p style={{ fontSize: 10, color: T.mgrey, fontWeight: 600, textTransform: 'uppercase', margin: 0 }}>Min / Max Stock</p>
              <p style={{ fontSize: 13, fontWeight: 600, color: T.dgrey, margin: '3px 0 0' }}>{item.minStockLevel} / {item.maxStockLevel}</p>
            </div>
            <div>
              <p style={{ fontSize: 10, color: T.mgrey, fontWeight: 600, textTransform: 'uppercase', margin: 0 }}>Reorder Qty</p>
              <p style={{ fontSize: 13, fontWeight: 600, color: T.dgrey, margin: '3px 0 0' }}>{item.reorderQty}</p>
            </div>
            <div>
              <p style={{ fontSize: 10, color: T.mgrey, fontWeight: 600, textTransform: 'uppercase', margin: 0 }}>Status</p>
              <p style={{ fontSize: 13, fontWeight: 600, color: T.dgrey, margin: '3px 0 0' }}>{item.isActive ? 'Active' : 'Inactive'}</p>
            </div>
          </div>
        </Card>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 16, marginBottom: 16 }}>
          <div>
            <SectionHeader title="Barcode &amp; QR Code"
              action={<Btn variant="outline" size="sm" onClick={handlePrintLabel}><span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><Printer size={14} /> Print Label</span></Btn>} />
            <Card>
              <div ref={printRef} style={{ display: 'flex', alignItems: 'center', gap: 24, flexWrap: 'wrap' }}>
                <div style={{ textAlign: 'center' }}>
                  <p style={{ fontSize: 11, color: T.mgrey, fontWeight: 600, marginBottom: 6 }}>{item.itemCode}</p>
                  {item.barcode ? (
                    <Barcode value={item.barcode} width={1.4} height={50} fontSize={12} />
                  ) : (
                    <p style={{ fontSize: 12, color: T.mgrey }}>No barcode set — add one via Edit.</p>
                  )}
                </div>
                <div style={{ textAlign: 'center' }}>
                  <QRCodeSVG value={`${window.location.origin}/modules/stores/items/${id}`} size={90} />
                  <p style={{ fontSize: 10, color: T.mgrey, marginTop: 6 }}>Scan to open item</p>
                </div>
              </div>
            </Card>
          </div>

          <div>
            <SectionHeader title="Photos" />
            <Card>
              {imagesLoading ? (
                <p style={{ fontSize: 12, color: T.mgrey }}>Loading photos…</p>
              ) : (
                <>
                  {images.length > 0 ? (
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, marginBottom: 12 }}>
                      {images.map(img => (
                        <div key={img.id} style={{ position: 'relative' }}>
                          <a href={img.photoUrl} target="_blank" rel="noreferrer">
                            <img src={img.photoUrl} alt={img.caption ?? 'Item photo'}
                              style={{ width: 80, height: 80, objectFit: 'cover', borderRadius: 8, border: `1px solid ${T.lgrey}`, display: 'block' }}
                              onError={e => { e.target.style.display = 'none' }} />
                          </a>
                          <button type="button" onClick={() => handleDeleteImage(img.id)}
                            style={{ position: 'absolute', top: -6, right: -6, width: 18, height: 18, borderRadius: '50%', background: T.red, color: T.white, border: 'none', fontSize: 11, lineHeight: 1, cursor: 'pointer' }}>×</button>
                        </div>
                      ))}
                    </div>
                  ) : (
                    <p style={{ fontSize: 12, color: T.mgrey, marginBottom: 12 }}>No photos uploaded yet.</p>
                  )}
                  <ImagePicker files={pendingImages} onChange={setPendingImages} />
                  {pendingImages.length > 0 && (
                    <Btn size="sm" style={{ marginTop: 8 }} disabled={uploadingImages} onClick={handleUploadImages}>
                      {uploadingImages ? 'Uploading…' : `Upload ${pendingImages.length} photo${pendingImages.length !== 1 ? 's' : ''}`}
                    </Btn>
                  )}
                </>
              )}
            </Card>
          </div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 16, marginBottom: 16 }}>
          <div>
            <SectionHeader title="Purchase Price History" />
            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable
                headers={['Price', 'Supplier / Date', 'Variance']}
                empty="No purchase history yet."
                rows={priceHistory.map(h => [
                  <strong>{fmt.kes(h.price)}</strong>,
                  <span style={{ fontSize: 11, color: T.mgrey }}>{h.supplierName} · {fmt.date(h.purchasedOn)}</span>,
                  h.alertLevel !== 'None' ? <Badge variant={ALERT_BADGE[h.alertLevel] ?? 'default'}>+{h.variancePct?.toFixed(1)}%</Badge> : '—',
                ])}
              />
            </Card>
          </div>

          <div>
            <SectionHeader title="Stock Units"
              action={<Btn variant="outline" size="sm" onClick={() => navigate('/modules/stores/grn', { state: { prefillItemId: item.id } })}>+ Add Stock Unit (via GRN)</Btn>} />
            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable
                headers={['Unit', 'Location', 'Qty Remaining', 'Status']}
                empty="No stock units yet."
                rows={stockUnits.map(u => [
                  u.serialNo || 'Bulk lot',
                  u.locationName || 'No location set',
                  u.qty,
                  <Badge variant={STOCK_STATUS_BADGE[u.status] ?? 'green'}>{u.status}</Badge>,
                ])}
              />
            </Card>
          </div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 16, marginBottom: 16 }}>
          <div>
            <SectionHeader title="Balances by Location" />
            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable
                headers={['Location', 'Quantity']}
                empty="No stock at any location."
                rows={balances.map(b => [b.locationName, <strong>{b.quantity} {item.uom}</strong>])}
              />
            </Card>
          </div>

          <div>
            <SectionHeader title="Recent Movements" />
            <Card style={{ padding: 0, overflow: 'hidden' }}>
              <DataTable
                headers={['Type / Location', 'Date', 'Qty']}
                empty="No movements recorded yet."
                rows={movements.map(m => [
                  <span>{m.type} · {m.locationName}</span>,
                  <span style={{ fontSize: 11, color: T.mgrey }}>{new Date(m.occurredAt).toLocaleString('en-KE')}</span>,
                  <strong style={{ color: m.quantity > 0 ? T.green : T.red }}>{m.quantity > 0 ? '+' : ''}{m.quantity}</strong>,
                ])}
              />
            </Card>
          </div>
        </div>

        <SectionHeader title="Goods Received Notes" />
        <Card style={{ padding: 0, overflow: 'hidden' }}>
          <DataTable
            headers={['Item', 'Supplier', 'Status']}
            empty="No GRNs for this item yet."
            rows={grns.map(grn => [
              <a onClick={() => navigate(`/modules/stores/grn/${grn.id}`)} style={{ cursor: 'pointer', color: T.navy, fontWeight: 700 }}>{grn.qtyReceived} units · {fmt.kes(grn.landedCost)}</a>,
              grn.supplierName,
              <Badge variant={grn.inspectionStatus === 'Passed' ? 'green' : grn.inspectionStatus === 'Failed' ? 'red' : 'blue'}>{grn.inspectionStatus}</Badge>,
            ])}
          />
        </Card>
      </div>
    </>
  )
}
