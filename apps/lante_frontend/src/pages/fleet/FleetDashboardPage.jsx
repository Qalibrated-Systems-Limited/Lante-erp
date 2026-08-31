import FleetNav from './FleetNav.jsx'
import GeneralDashboardTab from './dashboard/GeneralDashboardTab.jsx'
import TrucksDashboardTab from './dashboard/TrucksDashboardTab.jsx'
import FieldVehiclesDashboardTab from './dashboard/FieldVehiclesDashboardTab.jsx'
import MaterialsDashboardTab from './dashboard/MaterialsDashboardTab.jsx'
import { usePageTitle } from '../../components/AppShell.jsx'
import { useAuth } from '../../context/AuthContext.jsx'

export default function FleetDashboardPage() {
  usePageTitle('Fleet Dashboard')
  const { hasPermission } = useAuth()

  return (
    <>
      <main className="w-full px-4 sm:px-6 py-6 space-y-8">
        <FleetNav />

        <GeneralDashboardTab />

        <section className="space-y-6">
          <h2 className="text-lg font-extrabold text-navy mb-3">Trucks</h2>
          <TrucksDashboardTab />
          {/* Materials ride along with trucks (they're what a truck's loaded trips haul).
              Fleet Staff only need Dashboard + Trips (see FleetNav); this analytics block
              is Fleet Manager/Admin-only, same gate as the Drivers/Trucks tabs. */}
          {hasPermission('fleet.delete') && <MaterialsDashboardTab />}
        </section>

        <section>
          <h2 className="text-lg font-extrabold text-navy mb-3">Field Vehicles</h2>
          <FieldVehiclesDashboardTab />
        </section>
      </main>
    </>
  )
}
