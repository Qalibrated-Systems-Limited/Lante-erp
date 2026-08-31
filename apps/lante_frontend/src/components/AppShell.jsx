import { useState, useEffect, useMemo, useContext, createContext } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import {
  LayoutDashboard, User, Ticket, Tag, Zap, GitBranch, BookOpen, Settings,
  Wallet, ClipboardList, Landmark, Building2, Link2, Handshake, FileText, Megaphone,
  Recycle, ScrollText, Bell, ShoppingCart, Package, Store, ClipboardEdit, Microscope,
  Wrench, Scale, History, Search, Car, HardHat, Briefcase, ListChecks, Clock,
  AlertTriangle, Users, Target, ShieldCheck, TrendingUp, Shield, FileBadge2, Siren,
  ChevronLeft, ChevronRight,
} from "lucide-react";
import { useAuth } from "../context/AuthContext.jsx";
import { useNotifications } from "../context/NotificationContext.jsx";
import { useAlerts } from "../context/AlertContext.jsx";
import useCompanyBranding from "../hooks/useCompanyBranding.js";
import { T } from "../theme/tokens.js";

// Lets an individual page override the top bar's title (which otherwise falls back to
// the matched nav item's label, e.g. "Fleet" for every page under /modules/fleet) with
// something more specific, e.g. "Fleet Dashboard". Resets automatically on unmount so a
// page navigated away from doesn't leave a stale override behind.
const PageTitleContext = createContext(() => {});
export function usePageTitle(title) {
  const setPageTitle = useContext(PageTitleContext);
  useEffect(() => {
    setPageTitle(title);
    return () => setPageTitle(null);
  }, [title, setPageTitle]);
}

// ─── NAV STRUCTURE ──────────────────────────────────────────────────────────────
// Mirrors the CEO's DEPLOYED QSL sidebar (qsl-erp.onrender.com): same clusters,
// order, labels and emoji icons. Existing built pages are REUSED via `to`
// (Dashboard, Helpdesk, Projects, Fleet, Technical Dept→Operations,
// Administration → single consolidated tabbed page at /modules/admin, see
// pages/admin/AdminPage.jsx); everything else points to a module shell until
// its UI is ported. `group: null` renders with no header (Overview).
// `permission` mirrors the ProtectedRoute permission the item's `to` resolves to (see
// App.jsx) so the sidebar never advertises a page the user would be bounced out of.
// Items with no `permission` point at auth-only routes (no backend permission model yet
// — mock shells, personal/own-data pages) and stay visible to any authenticated user.
const NAV = [
  { group: null, items: [
    { id: "dashboard", label: "Dashboard",      icon: LayoutDashboard, to: "/dashboard" },
    { id: "me",        label: "My Workspace",   icon: User, to: "/workspace" },
  ]},
  { group: "Helpdesk", items: [
    { id: "hd-dashboard", label: "Dashboard",     icon: LayoutDashboard, to: "/modules/ticketing/dashboard", permission: "tickets.read.own" },
    { id: "helpdesk",  label: "Tickets",          icon: Ticket, to: "/modules/ticketing", permission: "tickets.read.own" },
    { id: "hd-tags",   label: "Tags",             icon: Tag, to: "/modules/ticketing/tags", permission: "settings.manage" },
    { id: "hd-macros", label: "Macros",           icon: Zap, to: "/modules/ticketing/macros", permission: "settings.manage" },
    { id: "hd-workflows", label: "Workflow Rules", icon: GitBranch, to: "/modules/ticketing/workflows", permission: "settings.manage" },
    { id: "hd-kb",     label: "Knowledge Base",   icon: BookOpen, to: "/modules/ticketing/kb", permission: "tickets.read.own" },
    { id: "hd-settings", label: "Helpdesk Settings", icon: Settings, to: "/modules/ticketing/settings", permission: "settings.manage" },
  ]},
  { group: "Finance", items: [
    { id: "finance",   label: "Finance",        icon: Wallet, to: "/modules/finance", permission: "finance.read" },
    { id: "debtors",   label: "Debtors",        icon: ClipboardList, to: "/modules/debtors", permission: "finance.read" },
    { id: "tax",       label: "Tax & KRA",      icon: Landmark, to: "/modules/tax", permission: "finance.read" },
    { id: "assets",    label: "Fixed Assets",   icon: Building2, to: "/modules/assets", permission: "finance.read" },
    { id: "ic",        label: "Inter-Company",  icon: Link2, to: "/modules/ic", permission: "finance.read" },
  ]},
  { group: "Sales & Clients", items: [
    { id: "crm-dashboard", label: "Dashboard",  icon: LayoutDashboard, to: "/modules/crm/dashboard" },
    { id: "crm",       label: "Commercial",     icon: Handshake, to: "/modules/crm" },
    { id: "bids",      label: "Bids & Pre-Sales", icon: FileText, to: "/modules/bids" },
    { id: "crm-marketing", label: "Marketing",  icon: Megaphone, to: "/modules/crm/marketing" },
    { id: "crm-aftersales", label: "After-Sales", icon: Recycle, to: "/modules/crm/after-sales" },
    { id: "crm-certificates", label: "Certificate Recalls", icon: FileBadge2, to: "/modules/operations/certificates", permission: "calibration.certificates.read" },
    { id: "crm-legal", label: "Legal Register", icon: ScrollText, to: "/modules/crm/legal" },
    { id: "crm-payments", label: "Payment Alerts", icon: Bell, to: "/modules/crm/payment-alerts" },
    { id: "shop",      label: "Online Shop",    icon: ShoppingCart, to: "/modules/shop" },
  ]},
  { group: "Supply Chain", items: [
    { id: "procurement", label: "Procurement",  icon: Package, to: "/modules/procurement" },
    { id: "stores",    label: "Stores",         icon: Store, to: "/modules/stores", permission: "stores.read" },
    { id: "requisitions", label: "Requisitions", icon: ClipboardEdit, to: "/modules/requisitions" },
  ]},
  { group: "Technical", items: [
    { id: "technical-dashboard", label: "Dashboard", icon: LayoutDashboard, to: "/modules/operations/technical-dashboard", permission: "operations.read.own" },
    { id: "calibration", label: "Technical Department", icon: Microscope, to: "/modules/operations", permission: "operations.read.own" },
    { id: "service-requests", label: "Service Requests", icon: Wrench, to: "/modules/operations/service-requests", permission: "operations.read.own" },
    { id: "reference-standards", label: "Reference Standards", icon: Scale, to: "/modules/operations/reference-standards", permission: "operations.read.own" },
    { id: "issued-certificates", label: "Issued Certificates", icon: FileBadge2, to: "/modules/operations/certificates", permission: "calibration.certificates.read" },
    { id: "equipment-history", label: "Equipment History", icon: History, to: "/modules/operations/equipment-history", permission: "operations.read.own" },
    { id: "inspection", label: "Inspection (17020)", icon: Search, to: "/modules/inspection" },
  ]},
  { group: "Operations", items: [
    { id: "ops-dashboard", label: "Dashboard",  icon: LayoutDashboard, to: "/modules/operations/projects-dashboard", permission: "projects.read.own" },
    { id: "projects",  label: "Projects",       icon: Landmark, to: "/modules/projects", permission: "projects.read.own" },
    { id: "project-templates", label: "Templates", icon: ListChecks, to: "/modules/operations/project-templates", permission: "projects.read.own" },
    { id: "workload", label: "Workload", icon: Users, to: "/modules/operations/workload", permission: "projects.read.all" },
    { id: "fleet",     label: "Fleet",          icon: Car, to: "/modules/fleet", permission: "fleet.read" },
    { id: "hse",       label: "Health & Safety", icon: HardHat, to: "/modules/hse", permission: "hse.read" },
    { id: "subcontracts", label: "Subcontracts", icon: Briefcase, to: "/modules/subcontracts", permission: "subcontracts.read" },
    { id: "tasks",     label: "Tasks",          icon: ListChecks, to: "/modules/operations/assignments", permission: "operations.read.own" },
    { id: "timesheets", label: "Timesheets",    icon: Clock, to: "/modules/operations/timesheets", permission: "operations.read.own" },
    { id: "negligence", label: "Negligence",    icon: AlertTriangle, to: "/modules/operations/negligence", permission: "operations.read.dept" },
  ]},
  { group: "People", items: [
    { id: "hr",        label: "HR & Payroll",   icon: Users, to: "/modules/hr" },
  ]},
  { group: "Quality & Governance", items: [
    { id: "quality",   label: "Quality (QMS)",  icon: Target, to: "/modules/quality" },
    { id: "compliance", label: "Compliance",    icon: ShieldCheck, to: "/modules/compliance", permission: "compliance.read" },
    { id: "sops",      label: "SOP Library",    icon: BookOpen, to: "/modules/sops" },
  ]},
  { group: "Insights", items: [
    { id: "reports",   label: "Reports",        icon: TrendingUp, to: "/modules/reports", permission: "reports.view" },
  ]},
  { group: "System", items: [
    { id: "admin",     label: "Administration", icon: Shield, to: "/modules/admin", permission: "users.read" },
    { id: "licensing", label: "Licensing",      icon: FileBadge2, to: "/modules/licensing", permission: "licensing.read" },
  ]},
];

const ALL_ITEMS = NAV.flatMap(g => g.items);

// Resolve the active item from the current path (longest matching `to`).
function activeItem(pathname) {
  let best = null;
  for (const it of ALL_ITEMS) {
    if (pathname === it.to || pathname.startsWith(it.to + "/")) {
      if (!best || it.to.length > best.to.length) best = it;
    }
  }
  return best;
}

// ─── AppShell ─────────────────────────────────────────────────────────────────
export default function AppShell({ children, hideSidebar = false, navExtra = null }) {
  const [collapsed, setCollapsed]   = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [isDesktop, setIsDesktop]   = useState(() => window.innerWidth >= 1024);
  const [pageTitle, setPageTitle]   = useState(null);
  const location = useLocation();
  const navigate = useNavigate();

  useEffect(() => {
    const mq = window.matchMedia("(min-width: 1024px)");
    const handler = (e) => { setIsDesktop(e.matches); if (e.matches) setMobileOpen(false); };
    mq.addEventListener("change", handler);
    return () => mq.removeEventListener("change", handler);
  }, []);

  useEffect(() => { setMobileOpen(false); }, [location.pathname]);

  const sideW = collapsed ? 56 : 216;
  const active = activeItem(location.pathname);

  return (
    <div style={{ minHeight: "100vh", display: "flex", background: T.offwt }}>
      {mobileOpen && !isDesktop && (
        <div style={{ position: "fixed", inset: 0, zIndex: 30, background: "rgba(13,34,56,.5)" }} onClick={() => setMobileOpen(false)} />
      )}

      {!hideSidebar && (
        <Sidebar
          collapsed={collapsed}
          setCollapsed={setCollapsed}
          activeId={active?.id}
          onNav={(to) => navigate(to)}
          width={sideW}
          isDesktop={isDesktop}
          mobileOpen={mobileOpen}
        />
      )}

      <div style={{
        flex: 1, minWidth: 0,
        marginLeft: hideSidebar ? 0 : (isDesktop ? sideW : 0),
        transition: "margin-left 0.2s ease",
        display: "flex", flexDirection: "column", minHeight: "100vh",
      }}>
        <TopBar
          title={pageTitle || active?.label || ""}
          isDesktop={isDesktop}
          onMenuToggle={() => setMobileOpen(v => !v)}
          navExtra={navExtra}
          showLogo={hideSidebar}
        />
        <main style={{ flex: 1, padding: 0, overflowY: "auto" }}>
          <PageTitleContext.Provider value={setPageTitle}>{children}</PageTitleContext.Provider>
        </main>
      </div>
    </div>
  );
}

// ─── Sidebar ────────────────────────────────────────────────────────────────────
function Sidebar({ collapsed, setCollapsed, activeId, onNav, width, isDesktop, mobileOpen }) {
  const { user, isModuleEnabled, hasPermission } = useAuth();
  const { logoUrl } = useCompanyBranding();
  const [search, setSearch] = useState("");
  const q = search.trim().toLowerCase();

  // Helpdesk's own sub-nav (hd-*) isn't separately toggleable — it follows the "helpdesk" module.
  const itemEnabled = (it) => it.id.startsWith("hd-") ? isModuleEnabled("helpdesk") : isModuleEnabled(it.id);
  // RBAC: hide items the user's permissions don't cover (system.admin bypasses everything —
  // see hasPermission). Items with no `permission` are auth-only and always visible.
  const itemVisible = (it) => !it.permission || hasPermission(it.permission);

  const groups = useMemo(() => {
    return NAV
      .map(g => ({ ...g, items: g.items.filter(it => itemEnabled(it) && itemVisible(it) && (!q || it.label.toLowerCase().includes(q))) }))
      .filter(g => g.items.length > 0);
  }, [q, isModuleEnabled, hasPermission]);

  // Collapsible groups — the group holding the active route starts open; the
  // rest collapsed (matches the deployed sidebar). Searching forces all open.
  const activeGroup = useMemo(() => NAV.find(g => g.items.some(it => it.id === activeId))?.group || null, [activeId]);
  const [open, setOpen] = useState(() => (activeGroup ? { [activeGroup]: true } : {}));
  useEffect(() => { if (activeGroup) setOpen(o => (o[activeGroup] ? o : { ...o, [activeGroup]: true })); }, [activeGroup]);
  const toggle = (name) => setOpen(o => ({ ...o, [name]: !o[name] }));

  const initial = (user?.firstName?.[0] || "Q").toUpperCase();
  const roles = user?.userRoles?.map(r => r.roleName ?? r) ?? [];
  const role = roles[0] ?? "";

  return (
    <div style={{
      width, flexShrink: 0, background: T.navyD, display: "flex", flexDirection: "column",
      height: "100vh", position: "fixed", top: 0, left: 0, overflow: "hidden",
      transition: "width .2s, transform .3s ease", zIndex: 40,
      transform: isDesktop ? "translateX(0)" : (mobileOpen ? "translateX(0)" : "translateX(-100%)"),
    }}>
      {/* Header — emblem + wordmark */}
      <div style={{
        padding: collapsed ? "14px 0" : "14px 14px",
        borderBottom: "1px solid rgba(255,255,255,.08)",
        display: "flex", alignItems: "center",
        justifyContent: collapsed ? "center" : "space-between",
        minHeight: 60, flexShrink: 0,
      }}>
        <Link to="/dashboard" style={{ display: "flex", alignItems: "center", gap: 9, textDecoration: "none", minWidth: 0 }}>
          {collapsed ? (
            // The icon mark's own art has no background and reads poorly on the
            // dark navy panel — a small light badge behind just the icon (not
            // the whole sidebar) restores its contrast without touching the
            // wordmark version used when expanded, which is already legible.
            <div style={{ width: 34, height: 34, borderRadius: 8, background: T.white, display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 }}>
              <img src={logoUrl || "/brand/qsl-icon.png"} alt="Company logo" style={{ height: 24, width: "auto" }} />
            </div>
          ) : (
            <img
              src={logoUrl || "/qsl-logo.svg"}
              alt="Company logo"
              style={{ height: 30, width: "auto", flexShrink: 0, maxWidth: "100%" }}
            />
          )}
        </Link>
      </div>

      {/* Collapse/expand toggle — its own fixed, non-scrolling row directly under
          the header. Never overlaps the logo (a separate row) and, being a normal
          flex child rather than a floating fixed-position element, can never end
          up underneath scrolling nav content either. Desktop-only: on mobile the
          drawer opens/closes via the TopBar's hamburger instead. */}
      {isDesktop && (
        <div style={{ padding: collapsed ? "6px 0 2px" : "6px 10px 2px", display: "flex", justifyContent: collapsed ? "center" : "flex-end", flexShrink: 0 }}>
          <button
            onClick={() => setCollapsed(v => !v)}
            className="sidebar-btn"
            title={collapsed ? "Expand sidebar" : "Collapse sidebar"}
            style={{
              width: collapsed ? 26 : "auto", height: 26, borderRadius: 8,
              padding: collapsed ? 0 : "0 10px",
              background: "rgba(255,255,255,.06)", border: "1px solid rgba(255,255,255,.12)",
              color: T.gold, cursor: "pointer",
              display: "flex", alignItems: "center", justifyContent: "center", gap: 6,
            }}
            onMouseEnter={e => { e.currentTarget.style.background = "rgba(255,255,255,.12)"; }}
            onMouseLeave={e => { e.currentTarget.style.background = "rgba(255,255,255,.06)"; }}
          >
            {collapsed ? <ChevronRight size={14} /> : <ChevronLeft size={14} />}
            {!collapsed && <span style={{ fontSize: 13, fontWeight: 600 }}>Collapse</span>}
          </button>
        </div>
      )}

      {/* Search */}
      {!collapsed && (
        <div style={{ padding: "12px 12px 6px", flexShrink: 0 }}>
          <div style={{ position: "relative" }}>
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="rgba(255,255,255,.4)" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" style={{ position: "absolute", left: 10, top: "50%", transform: "translateY(-50%)" }}>
              <circle cx="11" cy="11" r="8" /><path d="M21 21l-4.35-4.35" />
            </svg>
            <input
              value={search}
              onChange={e => setSearch(e.target.value)}
              placeholder="Find a module..."
              style={{ width: "100%", padding: "8px 10px 8px 30px", borderRadius: 8, border: "1px solid rgba(255,255,255,.1)", background: "rgba(255,255,255,.05)", fontSize: 12.5, color: "#fff", outline: "none", boxSizing: "border-box" }}
              onFocus={e => e.target.style.borderColor = T.gold}
              onBlur={e => e.target.style.borderColor = "rgba(255,255,255,.1)"}
            />
          </div>
        </div>
      )}

      {/* Nav — minHeight: 0 is required so this flex child actually shrinks and
          scrolls internally, instead of growing past the sidebar's fixed 100vh
          height and having the overflow silently clipped by the parent. */}
      <div className="sidebar-scroll" style={{ flex: 1, minHeight: 0, overflowY: "auto", padding: "4px 0" }}>
        {groups.map((g, gi) => {
          // A group is shown expanded when: it has no header (Overview), the
          // user is searching, it's collapsed-rail mode, or it's toggled open.
          const expanded = !g.group || !!q || collapsed || open[g.group];
          return (
            <div key={g.group || `overview-${gi}`}>
              {!collapsed && g.group && (
                <button
                  onClick={() => toggle(g.group)}
                  className="sidebar-btn"
                  style={{
                    width: "100%", background: "none", border: "none", cursor: "pointer",
                    display: "flex", alignItems: "center", justifyContent: "space-between",
                    padding: "13px 14px 5px",
                    fontSize: 11, fontWeight: 700, color: "rgba(255,255,255,.75)",
                    textTransform: "uppercase", letterSpacing: .8,
                  }}
                >
                  <span>{g.group}</span>
                  <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round"
                    style={{ transition: "transform .18s", transform: open[g.group] ? "rotate(0deg)" : "rotate(-90deg)", flexShrink: 0 }}>
                    <path d="M6 9l6 6 6-6" />
                  </svg>
                </button>
              )}
              {expanded && g.items.map(it => {
                const isActive = it.id === activeId;
                return (
                  <button
                    key={it.id}
                    onClick={() => onNav(it.to)}
                    title={collapsed ? it.label : ""}
                    style={{
                      width: "100%", display: "flex", alignItems: "center", gap: 10,
                      padding: collapsed ? "10px 0" : "10px 14px",
                      background: isActive ? "rgba(200,150,12,.18)" : "none",
                      border: "none", cursor: "pointer",
                      borderLeft: isActive ? `3px solid ${T.gold}` : "3px solid transparent",
                      justifyContent: collapsed ? "center" : "flex-start",
                    }}
                    onMouseEnter={e => { if (!isActive) e.currentTarget.style.background = "rgba(255,255,255,.06)"; }}
                    onMouseLeave={e => { e.currentTarget.style.background = isActive ? "rgba(200,150,12,.18)" : "none"; }}
                  >
                    <span style={{ flexShrink: 0, width: 22, display: "flex", justifyContent: "center", color: T.gold }}>
                      <it.icon size={17} strokeWidth={2} />
                    </span>
                    {!collapsed && <span style={{ fontSize: 15, fontWeight: isActive ? 700 : 500, color: isActive ? T.gold : "#FFFFFF", minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{it.label}</span>}
                  </button>
                );
              })}
            </div>
          );
        })}
        {!collapsed && groups.length === 0 && (
          <div style={{ padding: "20px 14px", fontSize: 12, color: "rgba(255,255,255,.4)" }}>No modules match “{search}”.</div>
        )}
      </div>

      {/* Footer — user (clickable through to My Workspace) */}
      <button
        onClick={() => onNav("/workspace")}
        className="sidebar-btn"
        title="My Workspace"
        style={{
          padding: collapsed ? "10px 0" : "10px 12px", borderTop: "1px solid rgba(255,255,255,.08)",
          display: "flex", alignItems: "center", gap: 8, justifyContent: collapsed ? "center" : "flex-start",
          flexShrink: 0, width: "100%", background: "none", cursor: "pointer", textAlign: "left",
        }}
        onMouseEnter={e => { e.currentTarget.style.background = "rgba(255,255,255,.06)"; }}
        onMouseLeave={e => { e.currentTarget.style.background = "none"; }}
      >
        <div style={{ width: 28, height: 28, borderRadius: "50%", background: T.gold, display: "flex", alignItems: "center", justifyContent: "center", fontSize: 11, fontWeight: 700, color: T.navy, flexShrink: 0 }}>{initial}</div>
        {!collapsed && (
          <div style={{ overflow: "hidden" }}>
            <div style={{ fontSize: 12, fontWeight: 600, color: T.white, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{user ? `${user.firstName} ${user.lastName}` : ""}</div>
            <div style={{ fontSize: 11, color: "rgba(255,255,255,.65)" }}>{role}</div>
          </div>
        )}
      </button>
    </div>
  );
}

// ─── TopBar ─────────────────────────────────────────────────────────────────────
function TopBar({ title, isDesktop, onMenuToggle, navExtra, showLogo }) {
  const { logout } = useAuth();
  const { unreadCount } = useNotifications();
  const { openCount } = useAlerts();
  const navigate = useNavigate();
  const today = new Date().toLocaleDateString("en-KE", { weekday: "short", day: "2-digit", month: "short", year: "numeric" });

  return (
    <div style={{ background: T.white, borderBottom: `1px solid ${T.lgrey}`, padding: "11px 22px", display: "flex", alignItems: "center", justifyContent: "space-between", flexShrink: 0, position: "sticky", top: 0, zIndex: 20 }}>
      <div style={{ display: "flex", alignItems: "center", gap: 12, minWidth: 0 }}>
        {!isDesktop && !showLogo && (
          <button onClick={onMenuToggle} style={{ background: "none", border: "none", cursor: "pointer", color: T.dgrey, padding: 4 }} aria-label="Open menu">
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M3 12h18M3 6h18M3 18h18" /></svg>
          </button>
        )}
        {showLogo ? (
          <Link to="/dashboard" style={{ display: "flex", alignItems: "center", gap: 8, textDecoration: "none" }}>
            <img src="/brand/qsl-icon.png" alt="Lante" style={{ height: 26 }} />
            <span style={{ fontSize: 15, fontWeight: 800, color: T.navy }}>Lante ERP</span>
          </Link>
        ) : (
          <h1 style={{ fontSize: 16, fontWeight: 700, color: T.brandVar, margin: 0 }}>{title}</h1>
        )}
      </div>

      <div style={{ display: "flex", alignItems: "center", gap: 14 }}>
        {navExtra}
        <span className="hidden sm:inline" style={{ fontSize: 11, color: T.mgrey }}>{today}</span>
        <button onClick={() => navigate("/notifications")} style={{ position: "relative", background: "none", border: "none", cursor: "pointer", lineHeight: 1, padding: 0, color: T.dgrey, display: "flex" }} aria-label="Notifications">
          <Bell size={19} strokeWidth={2} />
          {unreadCount > 0 && <span style={{ position: "absolute", top: -4, right: -4, background: T.red, color: T.white, fontSize: 9, fontWeight: 700, padding: "1px 4px", borderRadius: 99 }}>{unreadCount > 99 ? "99+" : unreadCount}</span>}
        </button>
        <button onClick={() => navigate("/alerts")} style={{ position: "relative", background: "none", border: "none", cursor: "pointer", lineHeight: 1, padding: 0, color: T.dgrey, display: "flex" }} aria-label="Alerts">
          <Siren size={19} strokeWidth={2} />
          {openCount > 0 && <span style={{ position: "absolute", top: -4, right: -4, background: T.red, color: T.white, fontSize: 9, fontWeight: 700, padding: "1px 4px", borderRadius: 99 }}>{openCount > 99 ? "99+" : openCount}</span>}
        </button>
        <div className="hidden sm:block" style={{ background: T.greenL, color: T.green, padding: "4px 10px", borderRadius: 6, fontSize: 11, fontWeight: 700 }}>● Live</div>
        <button onClick={() => { logout(); navigate("/login"); }} style={{ background: "none", border: `1px solid ${T.lgrey}`, borderRadius: 6, padding: "5px 12px", fontSize: 12, cursor: "pointer", color: T.mgrey }}>Sign out</button>
      </div>
    </div>
  );
}
