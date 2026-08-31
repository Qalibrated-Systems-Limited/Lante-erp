import { useNavigate } from 'react-router-dom'
import { T } from '../../../theme/tokens.js'
import { Card, Btn, EmptyState } from '../../../components/ui.jsx'
import { CheckCircle2 } from 'lucide-react'

export default function LowStockCard({ lowStock, acknowledgingId, onAcknowledge }) {
  const navigate = useNavigate()

  return (
    <Card style={{ padding: 0, overflow: 'hidden' }}>
      <div style={{ padding: '14px 18px', borderBottom: `1px solid ${T.lgrey}`, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ fontSize: 15, fontWeight: 700, color: T.navy, margin: 0 }}>Low Stock Items</h2>
        <span style={{ fontSize: 11, color: T.mgrey }}>Qty on hand ≤ min stock level</span>
      </div>
      {lowStock.length === 0 ? (
        <EmptyState icon={<CheckCircle2 size={40} />} title="No low-stock items" />
      ) : (
        <div>
          {lowStock.slice(0, 8).map(item => (
            <div key={item.id}
              onClick={() => navigate(`/modules/stores/items/${item.id}`)}
              style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '12px 18px', borderBottom: `1px solid ${T.lgrey}`, cursor: 'pointer' }}>
              <div style={{ minWidth: 0 }}>
                <p style={{ fontSize: 13, fontWeight: 600, color: T.dgrey, margin: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{item.itemCode} — {item.description}</p>
                <p style={{ fontSize: 11, color: T.mgrey, margin: '2px 0 0' }}>{item.categoryName}</p>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexShrink: 0, marginLeft: 12 }}>
                <p style={{ fontSize: 12, fontWeight: 700, color: T.red, margin: 0 }}>{item.qtyOnHand} / {item.minStockLevel} {item.uom}</p>
                <Btn size="sm" variant="ghost" disabled={acknowledgingId === item.id} onClick={e => onAcknowledge(item.id, e)}>
                  {acknowledgingId === item.id ? '…' : 'Acknowledge'}
                </Btn>
              </div>
            </div>
          ))}
        </div>
      )}
    </Card>
  )
}
