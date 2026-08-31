import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { T } from '../../theme/tokens.js'
import { Tabs } from '../../components/ui.jsx'
import { useAuth } from '../../context/AuthContext.jsx'
import ModuleShell from '../ModuleShell.jsx'
import UsersPage from '../users/UsersPage.jsx'
import RolesPage from '../roles/RolesPage.jsx'
import PermissionsPage from '../permissions/PermissionsPage.jsx'
import DepartmentsPage from '../departments/DepartmentsPage.jsx'
import SettingsPage from '../settings/SettingsPage.jsx'
import AuditLogTab from './AuditLogTab.jsx'
import FieldAuditLogTab from './FieldAuditLogTab.jsx'
import GeneralSettingsTab from './GeneralSettingsTab.jsx'
import EmailSettingsTab from './EmailSettingsTab.jsx'
import CompaniesTab from './CompaniesTab.jsx'
import ModulesTab from './ModulesTab.jsx'
import DocumentTemplatesTab from './DocumentTemplatesTab.jsx'

const PAD = 'clamp(16px, 2.4vw, 26px)'

// Single-screen Administration module (mirrors the QSL reference design's tabbed
// Admin panel). Each tab reuses the existing, already-wired-to-real-endpoints page
// component rather than re-implementing it — this is navigation consolidation, not
// a data-layer change. Tabs the user lacks permission for are hidden, same gating
// each component's standalone route already enforces.
export default function AdminPage() {
  const { hasPermission } = useAuth()
  const [params, setParams] = useSearchParams()

  const canRoles = hasPermission('roles.manage')
  const canPermissions = hasPermission('permissions.manage')

  const TABS = [
    hasPermission('settings.manage') && { id: 'modules', label: 'Modules' },
    hasPermission('users.read') && { id: 'users', label: 'Users' },
    (canRoles || canPermissions) && { id: 'roles', label: 'Roles & Permissions' },
    hasPermission('departments.manage') && { id: 'departments', label: 'Departments & Branches' },
    hasPermission('compliance.read') && { id: 'companies', label: 'Companies' },
    hasPermission('settings.manage') && { id: 'doctemplates', label: 'Document Templates' },
    hasPermission('settings.manage') && { id: 'settings', label: 'System Settings' },
    { id: 'integrations', label: 'Integrations' },
    hasPermission('system.admin') && { id: 'audit', label: 'Audit Log' },
  ].filter(Boolean)

  const active = TABS.some(t => t.id === params.get('tab')) ? params.get('tab') : TABS[0]?.id
  const setActive = (id) => setParams(prev => { const p = new URLSearchParams(prev); p.set('tab', id); return p })

  const rolesSubTabs = [
    canRoles && { id: 'roles', label: 'Roles' },
    canPermissions && { id: 'permissions', label: 'Permissions' },
  ].filter(Boolean)
  const [rolesSubTab, setRolesSubTab] = useState(null)
  const activeRolesSubTab = rolesSubTabs.some(t => t.id === rolesSubTab) ? rolesSubTab : rolesSubTabs[0]?.id

  const settingsSubTabs = [{ id: 'general', label: 'General' }, { id: 'email', label: 'Email (SMTP)' }, { id: 'account', label: 'Account & Security' }]
  const [settingsSubTab, setSettingsSubTab] = useState('general')

  const auditSubTabs = [{ id: 'access', label: 'Access Log' }, { id: 'fields', label: 'Field Changes' }]
  const [auditSubTab, setAuditSubTab] = useState('access')

  return (
    <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
      <div style={{ marginBottom: 4 }}>
        <h1 style={{ fontSize: 20, fontWeight: 800, color: T.navy, margin: 0 }}>Administration</h1>
        <p style={{ fontSize: 12.5, color: T.mgrey, margin: '4px 0 0' }}>
          Users, roles, departments and system configuration.
        </p>
      </div>

      <Tabs tabs={TABS} active={active} setActive={setActive} />

      {active === 'modules' && <ModulesTab />}

      {active === 'users' && <UsersPage />}

      {active === 'roles' && (
        <>
          {rolesSubTabs.length > 1 && (
            <Tabs tabs={rolesSubTabs} active={activeRolesSubTab} setActive={setRolesSubTab} />
          )}
          {activeRolesSubTab === 'roles' && <RolesPage />}
          {activeRolesSubTab === 'permissions' && <PermissionsPage />}
        </>
      )}

      {active === 'departments' && <DepartmentsPage />}

      {active === 'companies' && <CompaniesTab />}

      {active === 'doctemplates' && <DocumentTemplatesTab />}

      {active === 'settings' && (
        <>
          <Tabs tabs={settingsSubTabs} active={settingsSubTab} setActive={setSettingsSubTab} />
          {settingsSubTab === 'general' && <GeneralSettingsTab />}
          {settingsSubTab === 'email' && <EmailSettingsTab />}
          {settingsSubTab === 'account' && <SettingsPage />}
        </>
      )}

      {active === 'integrations' && (
        <ModuleShell title="Integrations" icon="🌐" blurb="Third-party integrations and API connections." />
      )}

      {active === 'audit' && (
        <>
          <Tabs tabs={auditSubTabs} active={auditSubTab} setActive={setAuditSubTab} />
          {auditSubTab === 'access' && <AuditLogTab />}
          {auditSubTab === 'fields' && <FieldAuditLogTab />}
        </>
      )}
    </div>
  )
}
