import { useNavigate } from 'react-router-dom'
import Collapsible from '../../../components/Collapsible.jsx'

const STEPS = [
  { step: '1. Vehicle Class', text: 'Set up the classes trucks are registered under (e.g. Flatbed, Tipper).' },
  { step: '2. Register Vehicle', text: 'Add a truck or field vehicle to the fleet.' },
  { step: '3. Materials', text: 'List what a loaded trip can carry.' },
  { step: '4. Create Trip', text: 'Start a trip — truck, driver, route and start mileage.' },
  { step: '5. Complete Trip', text: 'Log end mileage and close it out; revenue/expenses roll up here.' },
]

export default function FleetFlowGuideCard() {
  const navigate = useNavigate()

  return (
    <Collapsible title="How the Fleet flow works" dismissKey="fleet.flowGuide.dismissed">
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-3 mb-4">
        {STEPS.map(s => (
          <div key={s.step}>
            <p className="text-xs font-bold text-navy mb-0.5">{s.step}</p>
            <p className="text-xs text-gray-500 leading-snug">{s.text}</p>
          </div>
        ))}
      </div>
      <div className="flex gap-2 flex-wrap">
        <button onClick={() => navigate('/modules/fleet/vehicle-classes')} className="text-xs px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600 font-semibold">+ Add Vehicle Class</button>
        <button onClick={() => navigate('/modules/fleet/materials')} className="text-xs px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600 font-semibold">+ Add Material</button>
        <button onClick={() => navigate('/modules/fleet/trips/new')} className="text-xs px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600 font-semibold">+ Create Trip</button>
      </div>
    </Collapsible>
  )
}
