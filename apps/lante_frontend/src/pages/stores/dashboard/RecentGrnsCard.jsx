import { useNavigate } from 'react-router-dom'
import { T, fmt } from '../../../theme/tokens.js'
import { Card, Badge, EmptyState } from '../../../components/ui.jsx'
import { Package } from 'lucide-react'

export default function RecentGrnsCard({ recentGrns }) {
  const navigate = useNavigate()

  return (
    <Card style={{ padding: 0, overflow: 'hidden', gridColumn: '1 / -1' }}>
      <div style={{ padding: '14px 18px', borderBottom: `1px solid ${T.lgrey}` }}>
        <h2 style={{ fontSize: 15, fontWeight: 700, color: T.navy, margin: 0 }}>Recent Goods Received Notes</h2>
      </div>
      {recentGrns.length === 0 ? (
        <EmptyState icon={<Package size={40} />} title="No GRNs recorded yet" />
      ) : (
        <div>
          {recentGrns.map(grn => (
            <div key={grn.id}
              onClick={() => navigate(`/modules/stores/grn/${grn.id}`)}
              style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '12px 18px', borderBottom: `1px solid ${T.lgrey}`, cursor: 'pointer' }}>
              <div style={{ minWidth: 0 }}>
                <p style={{ fontSize: 13, fontWeight: 600, color: T.dgrey, margin: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{grn.itemCode} — {grn.supplierName}</p>
                <p style={{ fontSize: 11, color: T.mgrey, margin: '2px 0 0' }}>{grn.qtyReceived} units · {fmt.kes(grn.landedCost)}</p>
              </div>
              <Badge variant={grn.inspectionStatus === 'Passed' ? 'green' : grn.inspectionStatus === 'Failed' ? 'red' : 'blue'}>{grn.inspectionStatus}</Badge>
            </div>
          ))}
        </div>
      )}
    </Card>
  )
}
