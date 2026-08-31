import { useNavigate } from 'react-router-dom'
import { T } from '../../../theme/tokens.js'
import { Btn } from '../../../components/ui.jsx'
import Collapsible from '../../../components/Collapsible.jsx'

const STEPS = [
  { step: '1. Supplier', text: 'Register who you buy stock from.' },
  { step: '2. GRN', text: 'Record a Goods Received Note when stock physically arrives.' },
  { step: '3. Inspection', text: 'Inspect the GRN — Pass creates stock units, Fail rejects it (no stock added).' },
  { step: '4. Stock Unit', text: 'A passed GRN produces a stock unit/lot you can track by location.' },
  { step: '5. Issue / Sale', text: 'Issue stock internally or as a sale — works with or without picking a specific lot.' },
]

export default function FlowGuideCard() {
  const navigate = useNavigate()

  return (
    <Collapsible title="How the Stores flow works" dismissKey="stores.flowGuide.dismissed">
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(170px, 1fr))', gap: 14, marginBottom: 18 }}>
        {STEPS.map(s => (
          <div key={s.step}>
            <p style={{ fontSize: 11, fontWeight: 700, color: T.navy, margin: '0 0 3px' }}>{s.step}</p>
            <p style={{ fontSize: 12, color: T.mgrey, margin: 0, lineHeight: 1.4 }}>{s.text}</p>
          </div>
        ))}
      </div>
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
        <Btn variant="outline" size="sm" onClick={() => navigate('/modules/stores/locations')}>+ Add Location</Btn>
        <Btn variant="outline" size="sm" onClick={() => navigate('/modules/stores/categories')}>+ Add Category</Btn>
        <Btn variant="outline" size="sm" onClick={() => navigate('/modules/stores/suppliers')}>+ Add Supplier</Btn>
      </div>
    </Collapsible>
  )
}
