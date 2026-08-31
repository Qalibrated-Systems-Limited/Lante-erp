import { T, fmt } from '../../../theme/tokens.js'
import { Card, Badge, EmptyState } from '../../../components/ui.jsx'
import { BarChart3 } from 'lucide-react'

const ALERT_BADGE = { Critical: 'red', Warning: 'amber', None: 'default' }

export default function PriceAlertsCard({ priceAlerts }) {
  return (
    <Card style={{ padding: 0, overflow: 'hidden' }}>
      <div style={{ padding: '14px 18px', borderBottom: `1px solid ${T.lgrey}` }}>
        <h2 style={{ fontSize: 15, fontWeight: 700, color: T.navy, margin: 0 }}>Purchase Price Alerts</h2>
      </div>
      {priceAlerts.length === 0 ? (
        <EmptyState icon={<BarChart3 size={40} />} title="No price increases flagged" />
      ) : (
        <div>
          {priceAlerts.map(h => (
            <div key={h.id} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '12px 18px', borderBottom: `1px solid ${T.lgrey}` }}>
              <div style={{ minWidth: 0 }}>
                <p style={{ fontSize: 13, fontWeight: 600, color: T.dgrey, margin: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{h.itemCode}</p>
                <p style={{ fontSize: 11, color: T.mgrey, margin: '2px 0 0' }}>{h.supplierName} · {fmt.kes(h.price)}</p>
              </div>
              <Badge variant={ALERT_BADGE[h.alertLevel] ?? 'default'}>+{h.variancePct?.toFixed(1)}%</Badge>
            </div>
          ))}
        </div>
      )}
    </Card>
  )
}
