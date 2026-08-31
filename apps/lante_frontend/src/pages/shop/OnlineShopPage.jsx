import { useState, useEffect } from 'react'
import { T } from '../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Alert, Tabs, DataTable, EmptyState, Loading } from '../../components/ui.jsx'

// ─────────────────────────────────────────────────────────────────────────────
// Online Shop — Orders matches the deployed QSL module. UI-only shell: MOCK
// order list (empty). Shop Listings tab not yet specced — placeholder.
// ─────────────────────────────────────────────────────────────────────────────

const PAD = 'clamp(16px, 2.4vw, 26px)'

export default function OnlineShopPage() {
  const [tab, setTab] = useState('orders')
  const [orders] = useState([])
  const [tabLoading, setTabLoading] = useState(false)

  useEffect(() => {
    setTabLoading(true)
    const t = setTimeout(() => setTabLoading(false), 400)
    return () => clearTimeout(t)
  }, [tab])

  const awaitingPayment = orders.filter(o => o.status === 'pending_payment').length

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        <Alert type="info">Orders placed on the public website (qalibrated.co.ke/shop) land here automatically — stock is deducted and a draft invoice is created the moment an order is placed, ready for payment to be collected and the order fulfilled.</Alert>

        <div style={{ ...KPI_GRID, marginBottom: 22 }}>
          <Kpi label="Total Orders" value={orders.length} icon="🛒" />
          <Kpi label="Awaiting Payment" value={awaitingPayment} icon="⏳" variant={awaitingPayment ? 'red' : 'green'} />
          <Kpi label="Listed Products" value={0} icon="🏷️" />
          <Kpi label="Total Catalog Items" value={0} icon="📦" />
        </div>

        <Tabs tabs={[
          { id: 'orders', label: `Orders (${orders.length})` },
          { id: 'listings', label: 'Shop Listings' },
        ]} active={tab} setActive={setTab} />

        {tabLoading ? <Loading /> : (
          <>
            {tab === 'orders' && (
              <Card style={{ padding: 0, overflow: 'hidden' }}>
                <DataTable
                  headers={['Order No', 'Customer', 'Items', 'Total (Kshs)', 'Status', 'Placed', 'Action']}
                  rows={[]}
                />
              </Card>
            )}

            {tab === 'listings' && <EmptyState icon="🏷️" title="Shop Listings" sub="Coming soon." />}
          </>
        )}
      </div>
    </>
  )
}
