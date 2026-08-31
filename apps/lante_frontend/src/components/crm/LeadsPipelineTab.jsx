import { useState } from 'react'
import LeadsTab from './LeadsTab.jsx'
import PipelineTab from './PipelineTab.jsx'

// The "Leads & Pipeline" Commercial tab: a sub-toggle between the Lead register (C2) and the
// opportunity Pipeline board (C3). Qualified leads convert into the pipeline.
export default function LeadsPipelineTab() {
  const [view, setView] = useState('leads')
  return (
    <div>
      <div className="inline-flex rounded-lg border border-gray-200 bg-white p-0.5 mb-5">
        {[['leads', 'Leads'], ['pipeline', 'Pipeline']].map(([id, label]) => (
          <button key={id} onClick={() => setView(id)}
            className={`px-4 py-1.5 text-sm font-semibold rounded-md transition-colors ${view === id ? 'bg-navy text-white' : 'text-gray-500 hover:text-navy'}`}>
            {label}
          </button>
        ))}
      </div>
      {view === 'leads' ? <LeadsTab /> : <PipelineTab />}
    </div>
  )
}
