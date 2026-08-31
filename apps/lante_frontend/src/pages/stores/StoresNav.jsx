import { NavLink } from 'react-router-dom'
import { T } from '../../theme/tokens.js'

// Sub-navigation for the Stores module. Styled to match the QSL Tabs
// component (gold underline, navy active label) but backed by real routes —
// each tab is its own page with its own API calls, not a client-state tab.
const TABS = [
  { to: '/modules/stores',            label: 'Dashboard',  end: true },
  { to: '/modules/stores/items',      label: 'Items' },
  { to: '/modules/stores/categories', label: 'Categories' },
  { to: '/modules/stores/units-of-measure', label: 'Units of Measure' },
  { to: '/modules/stores/locations',  label: 'Locations' },
  { to: '/modules/stores/suppliers',  label: 'Suppliers' },
  { to: '/modules/stores/grn',        label: 'GRN' },
  { to: '/modules/stores/issues',     label: 'Store Issues' },
  { to: '/modules/stores/sold-items', label: 'Sold Items' },
  { to: '/modules/stores/transfers',  label: 'Transfers' },
  { to: '/modules/stores/stock-take', label: 'Stock Take' },
]

export default function StoresNav() {
  return (
    <div style={{ display: 'flex', gap: 0, marginBottom: 22, borderBottom: `1px solid ${T.lgrey}`, overflowX: 'auto', overflowY: 'hidden' }}>
      {TABS.map(t => (
        <NavLink
          key={t.to}
          to={t.to}
          end={t.end}
          style={({ isActive }) => ({
            padding: '9px 18px', background: 'none', border: 'none', cursor: 'pointer',
            fontSize: 13, fontWeight: isActive ? 700 : 400, color: isActive ? T.navy : T.mgrey,
            borderBottom: isActive ? `2px solid ${T.gold}` : '2px solid transparent',
            marginBottom: -1, whiteSpace: 'nowrap', textDecoration: 'none',
          })}
        >
          {t.label}
        </NavLink>
      ))}
    </div>
  )
}
