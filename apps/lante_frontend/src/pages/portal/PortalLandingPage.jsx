import { useNavigate } from 'react-router-dom'
import usePortalTenant from '../../hooks/usePortalTenant'

const ACTIONS = [
  {
    icon: '📣',
    title: 'Submit a Complaint',
    description: 'Tell us about a service issue or experience that did not meet your expectations.',
    type: 'Complaint',
    color: 'border-red-200 hover:border-red-400 hover:bg-red-50',
    badge: 'bg-red-100 text-red-700',
  },
  {
    icon: '💬',
    title: 'Share Feedback',
    description: 'We value your opinion. Let us know what we are doing well or where we can improve.',
    type: 'Feedback',
    color: 'border-blue-200 hover:border-blue-400 hover:bg-blue-50',
    badge: 'bg-blue-100 text-blue-700',
  },
  {
    icon: '🏗️',
    title: 'Project Inquiry',
    description: 'Interested in construction, technical services or calibration? Tell us about your project.',
    type: 'ProjectInquiry',
    color: 'border-amber-200 hover:border-amber-400 hover:bg-amber-50',
    badge: 'bg-amber-100 text-amber-700',
  },
  {
    icon: '✉️',
    title: 'General Message',
    description: 'Any other query, request or message you would like to send our team.',
    type: 'General',
    color: 'border-gray-200 hover:border-gray-400 hover:bg-gray-50',
    badge: 'bg-gray-100 text-gray-700',
  },
]

export default function PortalLandingPage() {
  const navigate = useNavigate()
  const { name: brandName, logoUrl } = usePortalTenant()
  const brand = brandName || 'Lante'

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      {/* Header */}
      <header className="bg-white border-b border-gray-200">
        <div className="max-w-5xl mx-auto px-4 sm:px-6 py-4 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <img src={logoUrl || "/qc-logo.png"} alt={brand} className="w-9 h-9 object-contain" />
            <div>
              <p className="font-extrabold text-zinc-950 text-sm leading-none">{brand}</p>
              <p className="text-xs text-gray-400">Client Portal</p>
            </div>
          </div>
          <button
            onClick={() => navigate('/portal/track')}
            className="text-sm font-medium text-amber-600 hover:text-amber-800 transition-colors"
          >
            Track a submission →
          </button>
        </div>
      </header>

      <main className="flex-1 max-w-5xl mx-auto w-full px-4 sm:px-6 py-12">
        {/* Hero */}
        <div className="text-center mb-12">
          <span className="inline-flex items-center gap-1.5 bg-amber-50 text-amber-700 text-xs font-semibold px-3 py-1.5 rounded-full border border-amber-200 mb-4">
            <span className="w-1.5 h-1.5 bg-amber-500 rounded-full inline-block" />
            Client Portal
          </span>
          <h1 className="text-3xl sm:text-4xl font-extrabold text-zinc-950 mb-4 leading-tight">
            How can we help you?
          </h1>
          <p className="text-gray-500 text-base max-w-xl mx-auto leading-relaxed">
            Whether you have a complaint, feedback, or a new project in mind — we are here to listen.
            Select an option below to get started.
          </p>
        </div>

        {/* General enquiries */}
        <p className="text-xs font-bold text-gray-400 uppercase tracking-widest mb-3">General Enquiries</p>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-6">
          {ACTIONS.map(action => (
            <button
              key={action.type}
              onClick={() => navigate('/portal/submit', { state: { type: action.type } })}
              className={`text-left bg-white rounded-2xl border-2 p-5 transition-all duration-150 group ${action.color}`}
            >
              <div className="flex items-start gap-4">
                <div className="text-2xl shrink-0">{action.icon}</div>
                <div className="flex-1">
                  <div className="flex items-center gap-2 mb-1">
                    <h3 className="font-bold text-gray-900 text-sm">{action.title}</h3>
                    <span className={`text-xs font-semibold px-2 py-0.5 rounded-full ${action.badge}`}>
                      {action.type === 'ProjectInquiry' ? 'Inquiry' : action.type}
                    </span>
                  </div>
                  <p className="text-xs text-gray-500 leading-relaxed">{action.description}</p>
                </div>
                <svg className="w-4 h-4 text-gray-300 group-hover:text-gray-500 transition-colors shrink-0 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                </svg>
              </div>
            </button>
          ))}
        </div>

        {/* Service & Calibration requests */}
        <p className="text-xs font-bold text-gray-400 uppercase tracking-widest mb-3">Service &amp; Calibration Requests</p>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-12">
          {[
            { icon: '🔧', title: 'Service Request', short: 'SRF', type: 'SRF', color: 'border-blue-200 hover:border-blue-400 hover:bg-blue-50', badge: 'bg-blue-100 text-blue-700', desc: 'Technical service, maintenance, repair or inspection.' },
            { icon: '⚖️', title: 'Calibration — NAWI', short: 'CRF-NAWI', type: 'CRF_NAWI', color: 'border-amber-200 hover:border-amber-400 hover:bg-amber-50', badge: 'bg-amber-100 text-amber-700', desc: 'Balances, scales and weighing instruments.' },
            { icon: '📦', title: 'Calibration — Mass', short: 'CRF-MASS', type: 'CRF_MASS', color: 'border-green-200 hover:border-green-400 hover:bg-green-50', badge: 'bg-green-100 text-green-700', desc: 'Mass standards, reference weights and weight sets.' },
          ].map(card => (
            <button
              key={card.type}
              onClick={() => navigate(`/portal/service-request?type=${card.type}`)}
              className={`text-left bg-white rounded-2xl border-2 p-5 transition-all duration-150 group ${card.color}`}
            >
              <div className="text-2xl mb-2">{card.icon}</div>
              <div className="flex items-center gap-1.5 mb-1">
                <h3 className="font-bold text-gray-900 text-sm">{card.title}</h3>
                <span className={`text-xs font-semibold px-1.5 py-0.5 rounded-full ${card.badge}`}>{card.short}</span>
              </div>
              <p className="text-xs text-gray-500 leading-relaxed">{card.desc}</p>
              <p className="text-xs font-semibold text-gray-400 mt-3 group-hover:text-gray-600 transition-colors">
                Submit request →
              </p>
            </button>
          ))}
        </div>

        {/* Track banner */}
        <div className="bg-zinc-950 rounded-2xl p-6 flex flex-col sm:flex-row items-center justify-between gap-4">
          <div>
            <h3 className="font-bold text-white text-base">Already submitted something?</h3>
            <p className="text-sm text-zinc-300 mt-0.5">Use your reference number to check the status of your submission.</p>
          </div>
          <button
            onClick={() => navigate('/portal/track')}
            className="shrink-0 px-5 py-2.5 bg-amber-500 hover:bg-amber-400 text-white text-sm font-bold rounded-xl transition-colors"
          >
            Track Submission →
          </button>
        </div>

        {/* Service highlights */}
        <div className="mt-14 grid grid-cols-2 sm:grid-cols-4 gap-6 text-center">
          {[
            { icon: '🏗️', label: 'Construction' },
            { icon: '⚡', label: 'Technical Service' },
            { icon: '📏', label: 'Calibration' },
            { icon: '🛡️', label: 'Safety & HSE' },
          ].map(s => (
            <div key={s.label} className="flex flex-col items-center gap-2">
              <div className="text-3xl">{s.icon}</div>
              <p className="text-sm font-semibold text-gray-600">{s.label}</p>
            </div>
          ))}
        </div>
      </main>

      <footer className="border-t border-gray-200 bg-white py-5 px-6 text-center">
        <p className="text-xs text-gray-400">
          {brand} &copy; {new Date().getFullYear()} &mdash; All rights reserved.
          &nbsp;|&nbsp;
          <a href="mailto:info@qalibrated.co.ke" className="hover:text-amber-600 transition-colors">info@qalibrated.co.ke</a>
        </p>
      </footer>
    </div>
  )
}
