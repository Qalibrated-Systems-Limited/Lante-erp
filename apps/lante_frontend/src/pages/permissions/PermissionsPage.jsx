import { useState } from 'react'
import { Link } from 'react-router-dom'
import { exportToPdf, exportToExcel, PERMISSION_COLUMNS } from '../../utils/export.js'
import { T } from '../../theme/tokens.js'
import { Card, Badge, Btn, Alert, Modal, Input, SectionHeader, Loading, EmptyState, DataTable } from '../../components/ui.jsx'
import { usePermissionsPage } from './usePermissionsPage.js'

const PERMISSION_META = {
  'system.admin':           { label: 'System Administrator',      desc: 'Full system access including settings and configuration' },
  'departments.manage':     { label: 'Manage Departments',        desc: 'Create, edit and delete departments' },
  'users.read':             { label: 'View Users',                desc: 'View user profiles and lists' },
  'users.write':            { label: 'Manage Users',              desc: 'Create, edit, assign roles, activate/deactivate users' },
  'users.delete':           { label: 'Delete Users',              desc: 'Soft-delete and restore user accounts' },
  'roles.manage':           { label: 'Manage Roles',              desc: 'Create, edit and delete roles, assign permissions' },
  'permissions.manage':     { label: 'Manage Permissions',        desc: 'Create and delete permission records' },
  'settings.manage':        { label: 'Manage Settings',           desc: 'Update system-wide configuration and settings' },
  'tickets.read.all':       { label: 'View All Tickets',          desc: 'See tickets across all departments and users' },
  'tickets.read.dept':      { label: 'View Dept Tickets',         desc: 'See tickets within your department' },
  'tickets.read.own':       { label: 'View Own Tickets',          desc: 'See only tickets assigned to or created by this user' },
  'tickets.write':          { label: 'Create & Edit Tickets',     desc: 'Open new tickets and update ticket details' },
  'tickets.assign':         { label: 'Assign Tickets',            desc: 'Assign tickets to staff members' },
  'tickets.resolve':        { label: 'Resolve Tickets',           desc: 'Mark tickets as resolved or closed' },
  'tickets.delete':         { label: 'Delete Tickets',            desc: 'Permanently delete ticket records' },
  'projects.read.all':      { label: 'View All Projects',         desc: 'See all projects across the organisation' },
  'projects.read.dept':     { label: 'View Dept Projects',        desc: 'See projects within your department' },
  'projects.read.own':      { label: 'View Own Projects',         desc: 'See only projects you are assigned to' },
  'projects.write':         { label: 'Create & Edit Projects',    desc: 'Create projects and update their details' },
  'projects.approve':       { label: 'Approve Projects',          desc: 'Give sign-off on project milestones and deliverables' },
  'projects.delete':        { label: 'Delete Projects',           desc: 'Remove project records from the system' },
  'finance.read':           { label: 'View Finance',              desc: 'View financial records, invoices and budgets' },
  'finance.write':          { label: 'Create Finance Records',    desc: 'Add and edit financial entries and invoices' },
  'finance.approve':        { label: 'Approve Financials',        desc: 'Approve payments, invoices and budget requests' },
  'finance.reports':        { label: 'Finance Reports',           desc: 'Access financial analytics and export reports' },
  'reports.view':           { label: 'View Reports',              desc: 'Access analytics and summary dashboards' },
  'reports.export':         { label: 'Export Reports',            desc: 'Download and export report data' },
  'portal.manage':          { label: 'Manage Portal',             desc: 'Manage the client portal and submissions' },
  'fleet.read':             { label: 'View Fleet',                desc: 'View vehicle and fleet records' },
  'fleet.write':            { label: 'Manage Fleet',              desc: 'Create and edit fleet records and assignments' },
  'fleet.delete':           { label: 'Delete Fleet Records',      desc: 'Remove fleet records from the system' },
  'fleet.expenses':         { label: 'Fleet Expenses',            desc: 'View and manage fleet expense entries' },
  'fleet.viewMileage':      { label: 'View Trip Mileage',         desc: 'See Start Mileage readings on trips (restricted from Drivers)' },
  'fleet.approve':          { label: 'Approve Fleet Requests',    desc: 'Approve fleet/vehicle dispatch requests' },
  'fleet.dispatch.request': { label: 'Request Vehicle Dispatch',  desc: 'Request a field vehicle dispatch for an assignment' },
  'stores.read':            { label: 'View Stores',               desc: 'View suppliers, items, GRNs and stock records' },
  'stores.write':           { label: 'Manage Stores',             desc: 'Create and update suppliers, items, GRNs and stock movements' },
  'stores.delete':          { label: 'Delete Stores Records',     desc: 'Delete/void stores records' },
  'stores.approve':         { label: 'Approve Stock Reconciliations', desc: 'Approve stock-take variance reconciliations' },
  'technician.read':        { label: 'View Technicians',          desc: 'View technician profiles and job records' },
  'technician.write':       { label: 'Manage Technicians',        desc: 'Create and edit technician records and jobs' },
  'technician.delete':      { label: 'Delete Technician Records', desc: 'Remove technician records from the system' },
  'technician.approve':     { label: 'Approve Technician Forms',  desc: 'Approve job forms and technician submissions' },
  'licensing.read':         { label: 'View Licenses',             desc: 'View license records and expiry dates' },
  'licensing.write':        { label: 'Manage Licenses',           desc: 'Create and update license entries' },
  'licensing.delete':       { label: 'Delete Licenses',           desc: 'Remove license records from the system' },
  'operations.read.own':    { label: 'View Own Operations',       desc: 'View operations and assignments you are part of' },
  'operations.write':       { label: 'Manage Operations',         desc: 'Create and edit operations records' },
  'operations.delete':      { label: 'Delete Operations',         desc: 'Remove operations records from the system' },
  'operations.approve':     { label: 'Approve Operations',        desc: 'Approve operations and work orders' },
}

const MODULE_GROUPS = [
  { key: 'system',      label: 'System & Administration', prefixes: ['users.', 'roles.', 'system.', 'departments.', 'settings.', 'permissions.'], accent: T.navy },
  { key: 'tickets',     label: 'Ticketing',               prefixes: ['tickets.'],     accent: T.amber },
  { key: 'projects',    label: 'Projects',                prefixes: ['projects.'],    accent: T.blue },
  { key: 'operations',  label: 'Operations',              prefixes: ['operations.'],  accent: T.purple },
  { key: 'finance',     label: 'Finance',                 prefixes: ['finance.'],     accent: T.green },
  { key: 'reports',     label: 'Reports',                 prefixes: ['reports.'],     accent: T.gold },
  { key: 'portal',      label: 'Client Portal',           prefixes: ['portal.'],      accent: T.navyL },
  { key: 'fleet',       label: 'Fleet',                   prefixes: ['fleet.'],       accent: T.red },
  { key: 'stores',      label: 'Stores',                  prefixes: ['stores.'],      accent: T.green },
  { key: 'technician',  label: 'Technicians',             prefixes: ['technician.'],  accent: T.amber },
  { key: 'licensing',   label: 'Licensing',               prefixes: ['licensing.'],   accent: T.navy },
]

function groupPermissions(permissions) {
  const grouped = Object.fromEntries(MODULE_GROUPS.map(g => [g.key, []]))
  const other = []
  for (const perm of permissions) {
    const match = MODULE_GROUPS.find(g => g.prefixes.some(p => perm.name.startsWith(p)))
    if (match) grouped[match.key].push(perm)
    else other.push(perm)
  }
  return { grouped, other }
}

const PAD = 'clamp(16px, 2.4vw, 26px)'

export default function PermissionsPage() {
  const {
    permissions,
    loading,
    error,
    search, setSearch,

    modal, setModal,
    selected, setSelected,
    form, setForm,
    submitting,
    formError, setFormError,
    toast,

    handleCreate,
    handleUpdate,
    handleDelete,
  } = usePermissionsPage()

  const q = search.toLowerCase()
  const filtered = permissions.filter(p =>
    !q ||
    p.name.toLowerCase().includes(q) ||
    (PERMISSION_META[p.name]?.label ?? '').toLowerCase().includes(q) ||
    (PERMISSION_META[p.name]?.desc ?? '').toLowerCase().includes(q)
  )
  const { grouped, other } = groupPermissions(filtered)

  return (
    <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
      <SectionHeader
        title="Permissions"
        sub={loading ? 'Loading…' : `${permissions.length} total permissions`}
        action={
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            <Btn variant="ghost" onClick={() => exportToPdf({ title: 'Permissions Report', columns: PERMISSION_COLUMNS, rows: permissions, filename: 'lante-permissions' })}>PDF</Btn>
            <Btn variant="ghost" onClick={() => exportToExcel({ title: 'Permissions', columns: PERMISSION_COLUMNS, rows: permissions, filename: 'lante-permissions', sheetName: 'Permissions' })}>Excel</Btn>
            <Btn onClick={() => { setForm({ name: '', description: '' }); setFormError(''); setModal('create') }}>+ New Permission</Btn>
          </div>
        }
      />

      {toast && <Alert type="success">{toast}</Alert>}

      <Alert type="info">
        Permissions are assigned to <strong>roles</strong>, and roles are assigned to users.
        Manage role permissions from the{' '}
        <Link to="/modules/admin?tab=roles" style={{ fontWeight: 700, color: T.blue, textDecoration: 'underline' }}>Roles tab</Link>.
      </Alert>

      <div style={{ maxWidth: 360 }}>
        <Input value={search} onChange={setSearch} placeholder="Search permissions…" />
      </div>

      {error && <Alert type="error">{error}</Alert>}

      {loading ? (
        <Loading />
      ) : filtered.length === 0 ? (
        <EmptyState icon="🔍" title={search ? `No permissions match "${search}"` : 'No permissions found'} />
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          {MODULE_GROUPS.map(group => {
            const perms = grouped[group.key]
            if (!perms || perms.length === 0) return null
            return (
              <PermissionGroup
                key={group.key}
                group={group}
                perms={perms}
                onEdit={(perm) => { setSelected(perm); setForm({ name: perm.name, description: perm.description ?? '' }); setFormError(''); setModal('edit') }}
                onDelete={(perm) => { setSelected(perm); setFormError(''); setModal('delete') }}
              />
            )
          })}
          {other.length > 0 && (
            <PermissionGroup
              group={{ key: 'other', label: 'Other', accent: T.mgrey }}
              perms={other}
              onEdit={(perm) => { setSelected(perm); setForm({ name: perm.name, description: perm.description ?? '' }); setFormError(''); setModal('edit') }}
              onDelete={(perm) => { setSelected(perm); setFormError(''); setModal('delete') }}
            />
          )}
        </div>
      )}

      {/* Create Modal */}
      {modal === 'create' && (
        <Modal title="New Permission" onClose={() => setModal(null)}>
          {formError && <Alert type="error">{formError}</Alert>}
          <Input
            label="Permission Key" required value={form.name}
            onChange={v => setForm(f => ({ ...f, name: v }))}
            placeholder="e.g. tickets.manage"
            note="Use dot notation: module.action"
          />
          <Input
            label="Description" value={form.description}
            onChange={v => setForm(f => ({ ...f, description: v }))}
            placeholder="What does this permission allow?"
          />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn disabled={submitting} onClick={handleCreate}>{submitting ? 'Creating…' : 'Create'}</Btn>
          </div>
        </Modal>
      )}

      {/* Edit Modal */}
      {modal === 'edit' && (
        <Modal title="Edit Permission" onClose={() => setModal(null)}>
          {formError && <Alert type="error">{formError}</Alert>}
          <Input
            label="Permission Key" required value={form.name}
            onChange={v => setForm(f => ({ ...f, name: v }))}
            placeholder="e.g. tickets.manage"
          />
          <Input
            label="Description" value={form.description}
            onChange={v => setForm(f => ({ ...f, description: v }))}
            placeholder="What does this permission allow?"
          />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn disabled={submitting} onClick={handleUpdate}>{submitting ? 'Saving…' : 'Save'}</Btn>
          </div>
        </Modal>
      )}

      {/* Delete Modal */}
      {modal === 'delete' && (
        <Modal title="Delete Permission" onClose={() => setModal(null)}>
          {formError && <Alert type="error">{formError}</Alert>}
          <p style={{ fontSize: 13, color: T.dgrey }}>
            Delete <strong>{PERMISSION_META[selected?.name]?.label ?? selected?.name}</strong>?
          </p>
          <p style={{ fontSize: 12, color: T.mgrey, marginTop: 4 }}>It will be removed from all roles immediately. This cannot be undone.</p>
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 12 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn variant="danger" disabled={submitting} onClick={handleDelete}>{submitting ? 'Deleting…' : 'Delete'}</Btn>
          </div>
        </Modal>
      )}
    </div>
  )
}

function PermissionGroup({ group, perms, onEdit, onDelete }) {
  const [collapsed, setCollapsed] = useState(false)

  return (
    <Card style={{ padding: 0, overflow: 'hidden' }}>
      <button
        onClick={() => setCollapsed(v => !v)}
        style={{
          width: '100%', display: 'flex', alignItems: 'center', gap: 10,
          padding: '12px 18px', background: 'none', border: 'none',
          borderBottom: collapsed ? 'none' : `1px solid ${T.lgrey}`,
          cursor: 'pointer', textAlign: 'left',
        }}
      >
        <span style={{ width: 10, height: 10, borderRadius: '50%', background: group.accent, flexShrink: 0 }} />
        <span style={{ fontSize: 13, fontWeight: 700, color: T.navy, flex: 1 }}>{group.label}</span>
        <Badge variant="default">{perms.length} permission{perms.length !== 1 ? 's' : ''}</Badge>
        <span style={{ fontSize: 11, color: T.mgrey, display: 'inline-block', transform: collapsed ? 'rotate(-90deg)' : 'none', transition: 'transform .15s' }}>▾</span>
      </button>

      {!collapsed && (
        <DataTable
          headers={['Permission', 'Code', 'Actions']}
          rows={perms.map(perm => {
            const meta = PERMISSION_META[perm.name]
            return [
              <div key="perm">
                <p style={{ fontSize: 13, fontWeight: 600, color: T.dgrey, margin: 0 }}>{meta?.label ?? perm.name}</p>
                {(meta?.desc || perm.description) && (
                  <p style={{ fontSize: 11, color: T.mgrey, marginTop: 3 }}>{meta?.desc ?? perm.description}</p>
                )}
              </div>,
              <span key="code" style={{ fontFamily: T.mono, fontSize: 11, color: T.mgrey }}>{perm.name}</span>,
              <div key="actions" style={{ display: 'flex', gap: 4 }}>
                <Btn size="sm" variant="ghost" onClick={() => onEdit(perm)}>Edit</Btn>
                <Btn size="sm" variant="danger" onClick={() => onDelete(perm)}>Delete</Btn>
              </div>,
            ]
          })}
        />
      )}
    </Card>
  )
}
