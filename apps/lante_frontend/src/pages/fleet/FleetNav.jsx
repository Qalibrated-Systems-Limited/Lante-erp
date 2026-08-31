import { NavLink } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext.jsx'

// Sub-navigation for the Fleet module. The QSL sidebar has a single "Fleet"
// entry (→ dashboard); these tabs move between the module's pages.
// Tabs with no `permission`/`hideIfPermission` are visible to everyone who
// can reach the Fleet module at all (Dashboard/Trips); the master-data/
// management views are gated on `fleet.delete`, which only Fleet Manager and
// Admin hold — Fleet Staff has fleet.read/fleet.write but not fleet.delete
// (see permissions.js). "My Profile" is the inverse: it's Fleet Staff's own
// self-service page, so it's hidden once a user already has fleet.delete
// (Fleet Manager/Admin manage drivers via the Drivers tab instead).
const TABS = [
  { to: '/modules/fleet',            label: 'Dashboard',  end: true },
  { to: '/modules/fleet/trips',      label: 'Trips' },
  { to: '/modules/fleet/requests',   label: 'Requests & Approvals' },
  { to: '/modules/fleet/my-profile', label: 'My Profile',       hideIfPermission: 'fleet.delete' },
  { to: '/modules/fleet/trucks',     label: 'Trucks',           permission: 'fleet.delete' },
  { to: '/modules/fleet/field-vehicles', label: 'Field Vehicles', permission: 'fleet.delete' },
  { to: '/modules/fleet/drivers',    label: 'Drivers',          permission: 'fleet.delete' },
  { to: '/modules/fleet/trip-types', label: 'Trip Types',       permission: 'fleet.delete' },
  { to: '/modules/fleet/materials',  label: 'Materials',        permission: 'fleet.delete' },
  { to: '/modules/fleet/vehicle-classes', label: 'Vehicle Classes', permission: 'fleet.delete' },
]

export default function FleetNav() {
  const { hasPermission } = useAuth()
  const visibleTabs = TABS.filter(t =>
    (!t.permission || hasPermission(t.permission)) &&
    (!t.hideIfPermission || !hasPermission(t.hideIfPermission))
  )

  return (
    <div className="flex items-center gap-1 overflow-x-auto border-b border-gray-200 mb-6 -mx-4 sm:-mx-6 px-4 sm:px-6">
      {visibleTabs.map(t => (
        <NavLink
          key={t.to}
          to={t.to}
          end={t.end}
          className={({ isActive }) =>
            `px-4 py-3 text-sm font-bold whitespace-nowrap border-b-2 transition-colors ${
              isActive
                ? 'border-gold text-navy'
                : 'border-transparent text-gray-500 hover:text-navy'
            }`
          }
        >
          {t.label}
        </NavLink>
      ))}
    </div>
  )
}
