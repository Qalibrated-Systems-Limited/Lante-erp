import { lazy, Suspense, useEffect } from 'react'
import { BrowserRouter, Routes, Route, Navigate, Outlet, useNavigate } from 'react-router-dom'
import { AuthProvider } from './context/AuthContext.jsx'
import { NotificationProvider } from './context/NotificationContext.jsx'
import { AlertProvider } from './context/AlertContext.jsx'
import ProtectedRoute from './components/ProtectedRoute.jsx'
import PlatformRoute from './components/PlatformRoute.jsx'
import GuestRoute from './components/GuestRoute.jsx'
import AppShell from './components/AppShell.jsx'
import { Loading } from './components/ui.jsx'
import { setNavigator } from './navigation.js'

// Registers react-router's navigate() for use outside the component tree (see
// src/navigation.js) — lets the axios 401 interceptor redirect to /login without
// forcing a full browser reload.
function NavigationListener() {
  const navigate = useNavigate()
  useEffect(() => { setNavigator(navigate) }, [navigate])
  return null
}

// Shared layout for every authenticated module page: AppShell (sidebar/topbar) mounts
// once and stays mounted across navigations — only the Outlet content re-suspends
// while a page's lazy chunk loads, instead of the whole shell unmounting/remounting.
function AppLayout() {
  return (
    <AppShell>
      <Suspense fallback={<Loading />}>
        <Outlet />
      </Suspense>
    </AppShell>
  )
}
const PlatformLoginPage = lazy(() => import('./pages/platform/PlatformLoginPage.jsx'))
const PlatformDashboardPage = lazy(() => import('./pages/platform/PlatformDashboardPage.jsx'))
const PlatformCompaniesPage = lazy(() => import('./pages/platform/PlatformCompaniesPage.jsx'))
const PlatformCompanyDetailPage = lazy(() => import('./pages/platform/PlatformCompanyDetailPage.jsx'))
const PlatformCompanyCreatePage = lazy(() => import('./pages/platform/PlatformCompanyCreatePage.jsx'))
const PlatformPlansPage = lazy(() => import('./pages/platform/PlatformPlansPage.jsx'))
const PlatformBroadcastPage = lazy(() => import('./pages/platform/PlatformBroadcastPage.jsx'))
const PlatformBackupsPage = lazy(() => import('./pages/platform/PlatformBackupsPage.jsx'))
import { CartProvider } from './components/public/CartContext.jsx'
const LandingPage = lazy(() => import('./pages/LandingPage.jsx'))
const ServicesPage = lazy(() => import('./pages/public/ServicesPage.jsx'))
const AboutPage = lazy(() => import('./pages/public/AboutPage.jsx'))
const ContactPage = lazy(() => import('./pages/public/ContactPage.jsx'))
const ShopPage = lazy(() => import('./pages/public/ShopPage.jsx'))
const ProductPage = lazy(() => import('./pages/public/ProductPage.jsx'))
const CartPage = lazy(() => import('./pages/public/CartPage.jsx'))
const CheckoutPage = lazy(() => import('./pages/public/CheckoutPage.jsx'))
const VerifyPage = lazy(() => import('./pages/public/VerifyPage.jsx'))
const VerifyResultPage = lazy(() => import('./pages/public/VerifyResultPage.jsx'))
const LoginPage = lazy(() => import('./pages/LoginPage.jsx'))
const AcceptInvitePage = lazy(() => import('./pages/AcceptInvitePage.jsx'))
const ModuleShell = lazy(() => import('./pages/ModuleShell.jsx'))
const MyWorkspacePage = lazy(() => import('./pages/workspace/MyWorkspacePage.jsx'))
const FinancePage = lazy(() => import('./pages/finance/FinancePage.jsx'))
const DebtorsPage = lazy(() => import('./pages/finance/DebtorsPage.jsx'))
const TaxPage = lazy(() => import('./pages/finance/TaxPage.jsx'))
const FixedAssetsPage = lazy(() => import('./pages/finance/FixedAssetsPage.jsx'))
const InterCompanyPage = lazy(() => import('./pages/finance/InterCompanyPage.jsx'))
const HsePage = lazy(() => import('./pages/hse/HsePage.jsx'))
const SubcontractsPage = lazy(() => import('./pages/subcontracts/SubcontractsPage.jsx'))
const QualityPage = lazy(() => import('./pages/quality/QualityPage.jsx'))
const CompliancePage = lazy(() => import('./pages/quality/compliance/CompliancePage.jsx'))
const SopLibraryPage = lazy(() => import('./pages/quality/SopLibraryPage.jsx'))
const ReportsPage = lazy(() => import('./pages/reports/ReportsPage.jsx'))
const CommercialPage = lazy(() => import('./pages/crm/CommercialPage.jsx'))
const CreateCustomerPage = lazy(() => import('./pages/crm/CreateCustomerPage.jsx'))
const CustomerDetailPage = lazy(() => import('./pages/crm/CustomerDetailPage.jsx'))
const SalesDashboardPage = lazy(() => import('./pages/crm/SalesDashboardPage.jsx'))
const MarketingPage = lazy(() => import('./pages/crm/MarketingPage.jsx'))
const AfterSalesPage = lazy(() => import('./pages/crm/AfterSalesPage.jsx'))
const LegalPage = lazy(() => import('./pages/crm/LegalPage.jsx'))
const PaymentAlertsPage = lazy(() => import('./pages/crm/PaymentAlertsPage.jsx'))
const BidsPage = lazy(() => import('./pages/crm/TendersPage.jsx'))
const OnlineShopPage = lazy(() => import('./pages/shop/OnlineShopPage.jsx'))
const ProcurementPage = lazy(() => import('./pages/procurement/ProcurementPage.jsx'))
const StoresDashboardPage = lazy(() => import('./pages/stores/StoresDashboardPage.jsx'))
const ItemMasterPage = lazy(() => import('./pages/stores/ItemMasterPage.jsx'))
const ItemDetailPage = lazy(() => import('./pages/stores/ItemDetailPage.jsx'))
const CategoriesPage = lazy(() => import('./pages/stores/CategoriesPage.jsx'))
const UnitsOfMeasurePage = lazy(() => import('./pages/stores/UnitsOfMeasurePage.jsx'))
const LocationsPage = lazy(() => import('./pages/stores/LocationsPage.jsx'))
const SuppliersPage = lazy(() => import('./pages/stores/SuppliersPage.jsx'))
const GrnPage = lazy(() => import('./pages/stores/GrnPage.jsx'))
const GrnDetailPage = lazy(() => import('./pages/stores/GrnDetailPage.jsx'))
const StoreIssuesPage = lazy(() => import('./pages/stores/StoreIssuesPage.jsx'))
const SoldItemsPage = lazy(() => import('./pages/stores/SoldItemsPage.jsx'))
const TransfersPage = lazy(() => import('./pages/stores/TransfersPage.jsx'))
const StockTakePage = lazy(() => import('./pages/stores/StockTakePage.jsx'))
const RequisitionsPage = lazy(() => import('./pages/requisitions/RequisitionsPage.jsx'))
const HrPage = lazy(() => import('./pages/hr/HrPage.jsx'))
const DashboardPage = lazy(() => import('./pages/DashboardPage.jsx'))
const TicketingPage = lazy(() => import('./pages/ticketing/TicketingPage.jsx'))
const HelpdeskDashboardPage = lazy(() => import('./pages/ticketing/HelpdeskDashboardPage.jsx'))
const CreateTicketPage = lazy(() => import('./pages/ticketing/CreateTicketPage.jsx'))
const TicketDetailPage = lazy(() => import('./pages/ticketing/TicketDetailPage.jsx'))
const TagsPage = lazy(() => import('./pages/ticketing/TagsPage.jsx'))
const MacrosPage = lazy(() => import('./pages/ticketing/MacrosPage.jsx'))
const WorkflowRulesPage = lazy(() => import('./pages/ticketing/WorkflowRulesPage.jsx'))
const DepartmentsPage = lazy(() => import('./pages/departments/DepartmentsPage.jsx'))
const AdminPage = lazy(() => import('./pages/admin/AdminPage.jsx'))
const UsersPage = lazy(() => import('./pages/users/UsersPage.jsx'))
const UserDetailPage = lazy(() => import('./pages/users/UserDetailPage.jsx'))
const RolesPage = lazy(() => import('./pages/roles/RolesPage.jsx'))
const PermissionsPage = lazy(() => import('./pages/permissions/PermissionsPage.jsx'))
const ProjectsPage = lazy(() => import('./pages/projects/ProjectsPage.jsx'))
const CreateProjectPage = lazy(() => import('./pages/projects/CreateProjectPage.jsx'))
const ProjectDetailPage = lazy(() => import('./pages/projects/ProjectDetailPage.jsx'))
const SettingsPage = lazy(() => import('./pages/settings/SettingsPage.jsx'))
const HelpdeskSettingsPage = lazy(() => import('./pages/ticketing/HelpdeskSettingsPage.jsx'))
const KnowledgeBasePage = lazy(() => import('./pages/ticketing/KnowledgeBasePage.jsx'))
const PortalLandingPage = lazy(() => import('./pages/portal/PortalLandingPage.jsx'))
const PortalSubmitPage = lazy(() => import('./pages/portal/PortalSubmitPage.jsx'))
const PortalTrackPage = lazy(() => import('./pages/portal/PortalTrackPage.jsx'))
const StaffPortalPage = lazy(() => import('./pages/portal/StaffPortalPage.jsx'))
const ServiceRequestFormPage = lazy(() => import('./pages/portal/ServiceRequestFormPage.jsx'))
const CustomerSurveyPage = lazy(() => import('./pages/portal/CustomerSurveyPage.jsx'))
const ServiceRequestQueuePage = lazy(() => import('./pages/operations/ServiceRequestQueuePage.jsx'))
const CreateServiceRequestPage = lazy(() => import('./pages/operations/CreateServiceRequestPage.jsx'))
const ServiceRequestDetailPage = lazy(() => import('./pages/operations/ServiceRequestDetailPage.jsx'))
const LicensingPage = lazy(() => import('./pages/licensing/LicensingPage.jsx'))
const NotificationsPage = lazy(() => import('./pages/notifications/NotificationsPage.jsx'))
const AlertsPage = lazy(() => import('./pages/alerts/AlertsPage.jsx'))
const FleetDashboardPage = lazy(() => import('./pages/fleet/FleetDashboardPage.jsx'))
const FleetTripsPage = lazy(() => import('./pages/fleet/FleetTripsPage.jsx'))
const TripDetailPage = lazy(() => import('./pages/fleet/TripDetailPage.jsx'))
const CreateTripPage = lazy(() => import('./pages/fleet/CreateTripPage.jsx'))
const FleetTrucksPage = lazy(() => import('./pages/fleet/FleetTrucksPage.jsx'))
const DriverProfilesPage = lazy(() => import('./pages/fleet/DriverProfilesPage.jsx'))
const MyDriverProfilePage = lazy(() => import('./pages/fleet/MyDriverProfilePage.jsx'))
const TripTypesPage = lazy(() => import('./pages/fleet/TripTypesPage.jsx'))
const MaterialsPage = lazy(() => import('./pages/fleet/MaterialsPage.jsx'))
const VehicleClassesPage = lazy(() => import('./pages/fleet/VehicleClassesPage.jsx'))
const CreateServiceReportPage = lazy(() => import('./pages/technicians/CreateServiceReportPage.jsx'))
const EditServiceReportPage = lazy(() => import('./pages/technicians/EditServiceReportPage.jsx'))
const OperationsOverviewPage = lazy(() => import('./pages/operations/OperationsOverviewPage.jsx'))
const OperationsProjectsPage = lazy(() => import('./pages/operations/OperationsProjectsPage.jsx'))
const ProjectTemplatesPage = lazy(() => import('./pages/operations/ProjectTemplatesPage.jsx'))
const WorkloadPage = lazy(() => import('./pages/operations/WorkloadPage.jsx'))
const OperationsAssignmentsPage = lazy(() => import('./pages/operations/OperationsAssignmentsPage.jsx'))
const OperationsProjectDetailPage = lazy(() => import('./pages/operations/ProjectDetailPage.jsx'))
const OperationsAssignmentDetailPage = lazy(() => import('./pages/operations/AssignmentDetailPage.jsx'))
const CalibrationCertificatePage = lazy(() => import('./pages/operations/CalibrationCertificatePage.jsx'))
const QuotationPrintPage = lazy(() => import('./pages/operations/QuotationPrintPage.jsx'))
const FieldVehiclesPage = lazy(() => import('./pages/operations/FieldVehiclesPage.jsx'))
const FieldVehicleDetailPage = lazy(() => import('./pages/operations/FieldVehicleDetailPage.jsx'))
const ReferenceStandardsPage = lazy(() => import('./pages/operations/ReferenceStandardsPage.jsx'))
const CertificateRegisterPage = lazy(() => import('./pages/operations/CertificateRegisterPage.jsx'))
const TimesheetsPage = lazy(() => import('./pages/operations/TimesheetsPage.jsx'))
const TechnicalDashboardPage = lazy(() => import('./pages/operations/TechnicalDashboardPage.jsx'))
const ProjectsDashboardPage = lazy(() => import('./pages/operations/ProjectsDashboardPage.jsx'))
const TimesheetDetailPage = lazy(() => import('./pages/operations/TimesheetDetailPage.jsx'))
const NegligencePage = lazy(() => import('./pages/operations/NegligencePage.jsx'))
const EquipmentHistoryPage = lazy(() => import('./pages/operations/EquipmentHistoryPage.jsx'))
const ServiceReportReviewPage = lazy(() => import('./pages/operations/ServiceReportReviewPage.jsx'))
const FleetFieldVehiclesPage = lazy(() => import('./pages/fleet/FieldVehiclesPage.jsx'))
const FleetDispatchRequestsPage = lazy(() => import('./pages/fleet/FleetDispatchRequestsPage.jsx'))
const FleetFieldVehicleDetailPage = lazy(() => import('./pages/fleet/FieldVehicleDetailPage.jsx'))

export default function App() {
  // Single-login model (2026-07): subdomains are retired — everyone uses lante.africa.
  // Tenants sign in at `/login`, platform admins at `/platform/login` (login is NOT unified). After
  // login the app routes by JWT role. No host-based swap.
  return (
    <AuthProvider>
      <NotificationProvider>
        <AlertProvider>
        <CartProvider>
        <BrowserRouter>
        {/* Floating KMK watermark — appears on every page */}
        {/* <img
          src="/kmkkk.png"
          alt="KMK Pure Souls"
          className="kmk-watermark fixed bottom-2 right-4 z-50 w-44 h-44 object-contain rounded-2xl select-none pointer-events-none opacity-90"
        /> */}
        <NavigationListener />
        <Suspense fallback={<Loading />}>
        <Routes>
          {/* ── Platform admin routes ── */}
          <Route path="/platform/login" element={<GuestRoute><PlatformLoginPage /></GuestRoute>} />
          <Route path="/platform/dashboard" element={<PlatformRoute><PlatformDashboardPage /></PlatformRoute>} />
          <Route path="/platform/companies" element={<PlatformRoute><PlatformCompaniesPage /></PlatformRoute>} />
          <Route path="/platform/companies/new" element={<PlatformRoute><PlatformCompanyCreatePage /></PlatformRoute>} />
          <Route path="/platform/companies/:id" element={<PlatformRoute><PlatformCompanyDetailPage /></PlatformRoute>} />
          <Route path="/platform/plans" element={<PlatformRoute><PlatformPlansPage /></PlatformRoute>} />
          <Route path="/platform/broadcast" element={<PlatformRoute><PlatformBroadcastPage /></PlatformRoute>} />
          <Route path="/platform/backups" element={<PlatformRoute><PlatformBackupsPage /></PlatformRoute>} />
          <Route path="/platform" element={<Navigate to="/platform/dashboard" replace />} />
          {/* ── Tenant routes ── */}
          <Route path="/" element={<LandingPage />} />
          <Route path="/services" element={<ServicesPage />} />
          <Route path="/about" element={<AboutPage />} />
          <Route path="/contact" element={<ContactPage />} />
          <Route path="/shop" element={<ShopPage />} />
          <Route path="/shop/:id" element={<ProductPage />} />
          <Route path="/cart" element={<CartPage />} />
          <Route path="/checkout" element={<CheckoutPage />} />
          <Route path="/verify" element={<VerifyPage />} />
          <Route path="/verify/:certNo" element={<VerifyResultPage />} />
          <Route path="/login" element={<GuestRoute><LoginPage /></GuestRoute>} />
          <Route path="/accept-invite" element={<AcceptInvitePage />} />
          {/* Public portal — no auth required.
              Each page is also mounted under an optional /:slug company segment so a link like
              /portal/acme/track renders the same page branded with that company's name (resolved
              anonymously via usePortalTenant). The paramless paths stay mounted unchanged so every
              already-bookmarked/emailed portal link keeps working and falls back to the platform brand. */}
          <Route path="/portal" element={<PortalLandingPage />} />
          <Route path="/portal/submit" element={<PortalSubmitPage />} />
          <Route path="/portal/track" element={<PortalTrackPage />} />
          <Route path="/portal/service-request" element={<ServiceRequestFormPage />} />
          <Route path="/portal/survey" element={<CustomerSurveyPage />} />
          <Route path="/portal/:slug" element={<PortalLandingPage />} />
          <Route path="/portal/:slug/submit" element={<PortalSubmitPage />} />
          <Route path="/portal/:slug/track" element={<PortalTrackPage />} />
          <Route path="/portal/:slug/service-request" element={<ServiceRequestFormPage />} />
          <Route path="/portal/:slug/survey" element={<CustomerSurveyPage />} />
          {/* Internal staff portal — no auth required */}
          <Route path="/staff" element={<StaffPortalPage />} />
          <Route path="/staff/:slug" element={<StaffPortalPage />} />
          {/* Print views — standalone, no app chrome, so they stay outside AppLayout */}
          <Route path="/modules/operations/assignments/:id/certificate" element={<ProtectedRoute permission="operations.read.own"><CalibrationCertificatePage /></ProtectedRoute>} />
          <Route path="/modules/operations/service-requests/:id/quotation/print" element={<ProtectedRoute permission="operations.read.own"><QuotationPrintPage /></ProtectedRoute>} />

          {/* ── Every authenticated module page shares this layout: AppShell mounts once,
              only its Outlet content re-suspends across navigations ── */}
          {/* Gate the layout route itself, not just each page inside it — otherwise AppShell's
              sidebar/topbar chrome paints immediately on every navigation, before authChecked
              resolves, producing a dashboard-shell flash ahead of the Loading/redirect below. */}
          <Route element={<ProtectedRoute><AppLayout /></ProtectedRoute>}>
          <Route
            path="/dashboard"
            element={
              <ProtectedRoute>
                <DashboardPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/modules/ticketing"
            element={
              <ProtectedRoute permission="tickets.read.own">
                <TicketingPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/modules/ticketing/dashboard"
            element={
              <ProtectedRoute permission="tickets.read.own">
                <HelpdeskDashboardPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/modules/ticketing/new"
            element={
              <ProtectedRoute permission="tickets.write">
                <CreateTicketPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/modules/ticketing/:id"
            element={
              <ProtectedRoute permission="tickets.read.own">
                <TicketDetailPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/modules/departments"
            element={
              <ProtectedRoute permission="departments.manage">
                <DepartmentsPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/modules/admin"
            element={
              <ProtectedRoute permission="users.read">
                <AdminPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/modules/users"
            element={
              <ProtectedRoute permission="users.read">
                <UsersPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/modules/users/:id"
            element={
              <ProtectedRoute permission="users.read">
                <UserDetailPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/modules/roles"
            element={
              <ProtectedRoute permission="roles.manage">
                <RolesPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/modules/permissions"
            element={
              <ProtectedRoute permission="permissions.manage">
                <PermissionsPage />
              </ProtectedRoute>
            }
          />
          <Route path="/modules/projects" element={<ProtectedRoute permission="projects.read.own"><ProjectsPage /></ProtectedRoute>} />
          <Route path="/modules/projects/new" element={<ProtectedRoute permission="projects.write"><CreateProjectPage /></ProtectedRoute>} />
          <Route path="/modules/projects/:id" element={<ProtectedRoute permission="projects.read.own"><ProjectDetailPage /></ProtectedRoute>} />
          <Route path="/modules/ticketing/tags" element={<ProtectedRoute permission="settings.manage"><TagsPage /></ProtectedRoute>} />
          <Route path="/modules/ticketing/macros" element={<ProtectedRoute permission="settings.manage"><MacrosPage /></ProtectedRoute>} />
          <Route path="/modules/ticketing/workflows" element={<ProtectedRoute permission="settings.manage"><WorkflowRulesPage /></ProtectedRoute>} />
          <Route path="/modules/ticketing/settings" element={<ProtectedRoute permission="settings.manage"><HelpdeskSettingsPage /></ProtectedRoute>} />
          <Route path="/modules/ticketing/kb" element={<ProtectedRoute permission="tickets.read.own"><KnowledgeBasePage /></ProtectedRoute>} />
          <Route path="/modules/ticketing/kb/:id" element={<ProtectedRoute permission="tickets.read.own"><KnowledgeBasePage /></ProtectedRoute>} />
          <Route path="/modules/settings" element={<ProtectedRoute permission="settings.manage"><SettingsPage /></ProtectedRoute>} />
          <Route path="/modules/licensing" element={<ProtectedRoute permission="licensing.read"><LicensingPage /></ProtectedRoute>} />
          <Route path="/notifications" element={<ProtectedRoute><NotificationsPage /></ProtectedRoute>} />
          <Route path="/alerts" element={<ProtectedRoute><AlertsPage /></ProtectedRoute>} />
          {/* Fleet */}
          <Route path="/modules/fleet" element={<ProtectedRoute permission="fleet.read"><FleetDashboardPage /></ProtectedRoute>} />
          <Route path="/modules/fleet/trips" element={<ProtectedRoute permission="fleet.read"><FleetTripsPage /></ProtectedRoute>} />
          <Route path="/modules/fleet/trips/new" element={<ProtectedRoute permission="fleet.write"><CreateTripPage /></ProtectedRoute>} />
          <Route path="/modules/fleet/trips/:id" element={<ProtectedRoute permission="fleet.read"><TripDetailPage /></ProtectedRoute>} />
          <Route path="/modules/fleet/my-profile" element={<ProtectedRoute permission="fleet.read"><MyDriverProfilePage /></ProtectedRoute>} />
          <Route path="/modules/fleet/trucks" element={<ProtectedRoute permission="fleet.read"><FleetTrucksPage /></ProtectedRoute>} />
          <Route path="/modules/fleet/drivers" element={<ProtectedRoute permission="fleet.read"><DriverProfilesPage /></ProtectedRoute>} />
          <Route path="/modules/fleet/trip-types" element={<ProtectedRoute permission="fleet.write"><TripTypesPage /></ProtectedRoute>} />
          <Route path="/modules/fleet/materials" element={<ProtectedRoute permission="fleet.write"><MaterialsPage /></ProtectedRoute>} />
<Route path="/modules/fleet/vehicle-classes" element={<ProtectedRoute permission="fleet.write"><VehicleClassesPage /></ProtectedRoute>} />
          <Route path="/modules/fleet/field-vehicles" element={<ProtectedRoute permission="fleet.read"><FleetFieldVehiclesPage /></ProtectedRoute>} />
          <Route path="/modules/fleet/field-vehicles/:id" element={<ProtectedRoute permission="fleet.read"><FleetFieldVehicleDetailPage /></ProtectedRoute>} />
          <Route path="/modules/fleet/requests" element={<ProtectedRoute permission="fleet.read"><FleetDispatchRequestsPage /></ProtectedRoute>} />
          {/* Service report — accessed from operations assignment detail */}
          <Route path="/modules/operations/assignments/:id/report/new" element={<ProtectedRoute permission="operations.read.own"><CreateServiceReportPage /></ProtectedRoute>} />
          <Route path="/modules/operations/assignments/:id/report/:reportId/edit" element={<ProtectedRoute permission="operations.read.own"><EditServiceReportPage /></ProtectedRoute>} />
          {/* Operations */}
          <Route path="/modules/operations" element={<ProtectedRoute permission="operations.read.own"><OperationsOverviewPage /></ProtectedRoute>} />
          <Route path="/modules/operations/technical-dashboard" element={<ProtectedRoute permission="operations.read.own"><TechnicalDashboardPage /></ProtectedRoute>} />
          <Route path="/modules/operations/projects-dashboard" element={<ProtectedRoute permission="projects.read.own"><ProjectsDashboardPage /></ProtectedRoute>} />
          <Route path="/modules/operations/projects" element={<ProtectedRoute permission="projects.read.own"><OperationsProjectsPage /></ProtectedRoute>} />
          {/* PR4b — template library + recurring schedules. Read-gated; authoring is checked in-page. */}
          <Route path="/modules/operations/project-templates" element={<ProtectedRoute permission="projects.read.own"><ProjectTemplatesPage /></ProtectedRoute>} />
          {/* PR4c — cross-project workload; the endpoint itself requires projects.read.all */}
          <Route path="/modules/operations/workload" element={<ProtectedRoute permission="projects.read.all"><WorkloadPage /></ProtectedRoute>} />
          <Route path="/modules/operations/projects/:id" element={<ProtectedRoute permission="projects.read.own"><OperationsProjectDetailPage /></ProtectedRoute>} />
          <Route path="/modules/operations/assignments" element={<ProtectedRoute permission="operations.read.own"><OperationsAssignmentsPage /></ProtectedRoute>} />
          <Route path="/modules/operations/assignments/:id" element={<ProtectedRoute permission="operations.read.own"><OperationsAssignmentDetailPage /></ProtectedRoute>} />
          <Route path="/modules/operations/service-requests" element={<ProtectedRoute permission="operations.read.own"><ServiceRequestQueuePage /></ProtectedRoute>} />
          <Route path="/modules/operations/service-requests/new" element={<ProtectedRoute permission="operations.write"><CreateServiceRequestPage /></ProtectedRoute>} />
          <Route path="/modules/operations/service-requests/:id" element={<ProtectedRoute permission="operations.read.own"><ServiceRequestDetailPage /></ProtectedRoute>} />
          <Route path="/modules/operations/field-vehicles" element={<ProtectedRoute permission="operations.read.own"><FieldVehiclesPage /></ProtectedRoute>} />
          <Route path="/modules/operations/field-vehicles/:id" element={<ProtectedRoute permission="operations.read.own"><FieldVehicleDetailPage /></ProtectedRoute>} />
          <Route path="/modules/operations/reference-standards" element={<ProtectedRoute permission="operations.read.own"><ReferenceStandardsPage /></ProtectedRoute>} />
          <Route path="/modules/operations/certificates" element={<ProtectedRoute permission="calibration.certificates.read"><CertificateRegisterPage /></ProtectedRoute>} />
          <Route path="/modules/operations/timesheets" element={<ProtectedRoute permission="operations.read.own"><TimesheetsPage /></ProtectedRoute>} />
          <Route path="/modules/operations/timesheets/:id" element={<ProtectedRoute permission="operations.read.own"><TimesheetDetailPage /></ProtectedRoute>} />
          <Route path="/modules/operations/negligence" element={<ProtectedRoute permission="operations.read.dept"><NegligencePage /></ProtectedRoute>} />
          <Route path="/modules/operations/equipment-history" element={<ProtectedRoute permission="operations.read.own"><EquipmentHistoryPage /></ProtectedRoute>} />
          <Route path="/modules/operations/service-reports/:id" element={<ProtectedRoute permission="operations.read.own"><ServiceReportReviewPage /></ProtectedRoute>} />
          {/* ── QSL module shells (UI-only, being ported to match the CEO design) ── */}
          <Route path="/workspace"            element={<ProtectedRoute><MyWorkspacePage /></ProtectedRoute>} />
          <Route path="/modules/finance"      element={<ProtectedRoute><FinancePage /></ProtectedRoute>} />
          <Route path="/modules/debtors"      element={<ProtectedRoute><DebtorsPage /></ProtectedRoute>} />
          <Route path="/modules/tax"          element={<ProtectedRoute><TaxPage /></ProtectedRoute>} />
          <Route path="/modules/ic"           element={<ProtectedRoute><InterCompanyPage /></ProtectedRoute>} />
          <Route path="/modules/crm"          element={<ProtectedRoute permission="crm.read.own"><CommercialPage /></ProtectedRoute>} />
          <Route path="/modules/crm/dashboard" element={<ProtectedRoute permission="crm.read.own"><SalesDashboardPage /></ProtectedRoute>} />
          <Route path="/modules/crm/marketing" element={<ProtectedRoute permission="crm.read.own"><MarketingPage /></ProtectedRoute>} />
          <Route path="/modules/crm/after-sales" element={<ProtectedRoute permission="crm.read.own"><AfterSalesPage /></ProtectedRoute>} />
          <Route path="/modules/crm/legal" element={<ProtectedRoute permission="crm.read.own"><LegalPage /></ProtectedRoute>} />
          <Route path="/modules/crm/payment-alerts" element={<ProtectedRoute permission="crm.read.own"><PaymentAlertsPage /></ProtectedRoute>} />
          <Route path="/modules/crm/customers/new" element={<ProtectedRoute permission="crm.write"><CreateCustomerPage /></ProtectedRoute>} />
          <Route path="/modules/crm/customers/:id" element={<ProtectedRoute permission="crm.read.own"><CustomerDetailPage /></ProtectedRoute>} />
          <Route path="/modules/finance"      element={<ProtectedRoute permission="finance.read"><FinancePage /></ProtectedRoute>} />
          <Route path="/modules/debtors"      element={<ProtectedRoute permission="finance.read"><DebtorsPage /></ProtectedRoute>} />
          <Route path="/modules/tax"          element={<ProtectedRoute permission="finance.read"><TaxPage /></ProtectedRoute>} />
          <Route path="/modules/assets"       element={<ProtectedRoute permission="finance.read"><FixedAssetsPage /></ProtectedRoute>} />
          <Route path="/modules/ic"           element={<ProtectedRoute permission="finance.read"><InterCompanyPage /></ProtectedRoute>} />
          <Route path="/modules/crm"          element={<ProtectedRoute><CommercialPage /></ProtectedRoute>} />
          <Route path="/modules/bids"         element={<ProtectedRoute><BidsPage /></ProtectedRoute>} />
          <Route path="/modules/shop"         element={<ProtectedRoute><OnlineShopPage /></ProtectedRoute>} />
          <Route path="/modules/procurement"  element={<ProtectedRoute><ProcurementPage /></ProtectedRoute>} />
          <Route path="/modules/stores"              element={<ProtectedRoute permission="stores.read"><StoresDashboardPage /></ProtectedRoute>} />
          <Route path="/modules/stores/items"         element={<ProtectedRoute permission="stores.read"><ItemMasterPage /></ProtectedRoute>} />
          <Route path="/modules/stores/items/:id"     element={<ProtectedRoute permission="stores.read"><ItemDetailPage /></ProtectedRoute>} />
          <Route path="/modules/stores/categories"    element={<ProtectedRoute permission="stores.read"><CategoriesPage /></ProtectedRoute>} />
          <Route path="/modules/stores/units-of-measure" element={<ProtectedRoute permission="stores.read"><UnitsOfMeasurePage /></ProtectedRoute>} />
          <Route path="/modules/stores/locations"     element={<ProtectedRoute permission="stores.read"><LocationsPage /></ProtectedRoute>} />
          <Route path="/modules/stores/suppliers"     element={<ProtectedRoute permission="stores.read"><SuppliersPage /></ProtectedRoute>} />
          <Route path="/modules/stores/grn"           element={<ProtectedRoute permission="stores.read"><GrnPage /></ProtectedRoute>} />
          <Route path="/modules/stores/grn/:id"       element={<ProtectedRoute permission="stores.read"><GrnDetailPage /></ProtectedRoute>} />
          <Route path="/modules/stores/issues"        element={<ProtectedRoute permission="stores.read"><StoreIssuesPage /></ProtectedRoute>} />
          <Route path="/modules/stores/sold-items"    element={<ProtectedRoute permission="stores.read"><SoldItemsPage /></ProtectedRoute>} />
          <Route path="/modules/stores/transfers"     element={<ProtectedRoute permission="stores.read"><TransfersPage /></ProtectedRoute>} />
          <Route path="/modules/stores/stock-take"    element={<ProtectedRoute permission="stores.read"><StockTakePage /></ProtectedRoute>} />
          <Route path="/modules/requisitions" element={<ProtectedRoute><RequisitionsPage /></ProtectedRoute>} />
          <Route path="/modules/inspection"   element={<ProtectedRoute><ModuleShell title="Inspection (17020)"  icon="🔍" blurb="Inspection jobs, checklists and non-conformance reports." /></ProtectedRoute>} />
          <Route path="/modules/hse"          element={<ProtectedRoute permission="hse.read"><HsePage /></ProtectedRoute>} />
          <Route path="/modules/subcontracts" element={<ProtectedRoute permission="subcontracts.read"><SubcontractsPage /></ProtectedRoute>} />
          <Route path="/modules/tasks"        element={<Navigate to="/modules/operations/assignments" replace />} />
          <Route path="/modules/hr"           element={<ProtectedRoute><HrPage /></ProtectedRoute>} />
          <Route path="/modules/quality"      element={<ProtectedRoute><QualityPage /></ProtectedRoute>} />
          <Route path="/modules/compliance"   element={<ProtectedRoute permission="compliance.read"><CompliancePage /></ProtectedRoute>} />
          <Route path="/modules/sops"         element={<ProtectedRoute><SopLibraryPage /></ProtectedRoute>} />
          <Route path="/modules/reports"      element={<ProtectedRoute permission="reports.view"><ReportsPage /></ProtectedRoute>} />
          <Route path="/modules/integrations" element={<ProtectedRoute><ModuleShell title="Integrations"        icon="🌐" blurb="Third-party integrations and API connections." /></ProtectedRoute>} />
          </Route>

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
        </Suspense>
        </BrowserRouter>
        </CartProvider>
        </AlertProvider>
      </NotificationProvider>
    </AuthProvider>
  )
}
