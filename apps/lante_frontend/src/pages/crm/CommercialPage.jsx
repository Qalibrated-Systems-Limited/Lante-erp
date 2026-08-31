import { useSearchParams } from 'react-router-dom'
import { Tabs } from '../../components/ui.jsx'
import CustomersTab from '../../components/crm/CustomersTab.jsx'
import LeadsPipelineTab from '../../components/crm/LeadsPipelineTab.jsx'
import QuotesTab from '../../components/crm/QuotesTab.jsx'

// Commercial (Sales & Clients) — the CRM hub. Mirrors the deployed QSL 5-tab layout;
// each tab gets its real implementation per phase:
//   Client Register (C1 ✓) · Leads & Pipeline (C2/C3) · Quotes (C4) · Support Tickets · Payment Alerts (C13)
// Tab is URL-driven (?tab=) so it's shareable and survives refresh.
const TABS = [
  { id: 'register', label: 'Client Register' },
  { id: 'leads', label: 'Leads & Pipeline' },
  { id: 'quotes', label: 'Quotes' },
  { id: 'tickets', label: 'Support Tickets' },
  { id: 'alerts', label: 'Payment Alerts' },
]
const TAB_IDS = TABS.map(t => t.id)

export default function CommercialPage() {
  const [params, setParams] = useSearchParams()
  const tab = TAB_IDS.includes(params.get('tab')) ? params.get('tab') : 'register'
  const setTab = (id) => setParams(id === 'register' ? {} : { tab: id }, { replace: true })

  return (
    <>
      <main className="flex-1 w-full px-4 sm:px-6 lg:px-8 py-8">
        <div className="mb-5">
          <h1 className="text-2xl font-extrabold text-navy">Commercial</h1>
          <p className="text-sm text-gray-500 mt-0.5">Sales &amp; client relationship management</p>
        </div>

        <Tabs tabs={TABS} active={tab} setActive={setTab} />

        {tab === 'register' && <CustomersTab />}
        {tab === 'leads' && <LeadsPipelineTab />}
        {tab === 'quotes' && <QuotesTab />}
        {tab === 'tickets' && <ComingSoon title="Support Tickets" note="Client support tickets — surfaced from the Helpdesk module." />}
        {tab === 'alerts' && <ComingSoon title="Payment Alerts" note="Debtor & payment-due alerts from Finance (C13)." />}
      </main>
    </>
  )
}

function ComingSoon({ title, note }) {
  return (
    <div className="flex flex-col items-center justify-center bg-white rounded-2xl border border-dashed border-gray-300 py-20 text-center">
      <div className="text-4xl mb-3">🚧</div>
      <h3 className="font-semibold text-gray-700">{title}</h3>
      <p className="text-sm text-gray-400 mt-1">{note}</p>
    </div>
  )
}
