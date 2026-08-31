import { useState } from 'react'
import { CategoriesSection, SLASection, EscalationSection } from '../settings/SettingsPage.jsx'

// Helpdesk configuration — categories, SLA policies and escalation rules — surfaced inside the
// Helpdesk cluster (was buried in the global Settings page). Reuses the section components.
const TABS = [
  { id: 'categories', label: 'Ticket Categories', icon: '🏷️' },
  { id: 'sla',        label: 'SLA Policies',       icon: '⏱️' },
  { id: 'escalation', label: 'Escalation Rules',   icon: '⚠️' },
]

export default function HelpdeskSettingsPage() {
  const [active, setActive] = useState('categories')

  return (
    <>
      <main className="flex-1 max-w-6xl mx-auto w-full px-4 sm:px-6 py-8">
        <div className="mb-6">
          <h1 className="text-2xl font-extrabold text-zinc-950">Helpdesk Settings</h1>
          <p className="text-sm text-gray-500 mt-0.5">Categories, SLA targets and auto-escalation rules for tickets</p>
        </div>

        <div className="flex flex-col lg:flex-row gap-6">
          <aside className="lg:w-56 shrink-0">
            <nav className="bg-white rounded-xl border border-gray-200 overflow-hidden">
              {TABS.map((item, i) => (
                <button
                  key={item.id}
                  onClick={() => setActive(item.id)}
                  className={`w-full flex items-center gap-3 px-4 py-3 text-sm font-medium transition-colors text-left
                    ${i > 0 ? 'border-t border-gray-100' : ''}
                    ${active === item.id
                      ? 'bg-amber-50 text-amber-700 border-l-2 border-l-amber-500'
                      : 'text-gray-600 hover:bg-gray-50 hover:text-gray-900'}`}
                >
                  <span className="w-4 text-center shrink-0">{item.icon}</span>
                  {item.label}
                </button>
              ))}
            </nav>
          </aside>

          <div className="flex-1 min-w-0">
            {active === 'categories' && <CategoriesSection />}
            {active === 'sla'        && <SLASection />}
            {active === 'escalation' && <EscalationSection />}
          </div>
        </div>
      </main>
    </>
  )
}
