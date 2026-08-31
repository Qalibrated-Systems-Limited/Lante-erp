# QSL Frontend Porting Guide

How we revamp the QaliCore frontend to match the CEO's **QSL** design, module by module.
Read this before starting a module so everyone's output is consistent.

---

## 1. What we're doing

- **Rebrand + port** the existing Vite/React (JSX) app at `apps/lante_frontend` to the QSL
  **navy + gold** design. We are **not** switching to Next.js and **not** replacing the .NET backend.
- **Source of truth = the DEPLOYED app** at `qsl-erp.onrender.com` (what the CEO designed).
  The Next.js code at `~/Downloads/qsl-erp-updated/` is a useful reference for layout/logic but its
  **sidebar grouping is stale** — always match the deployed app, and use screenshots when unsure.
- New modules with no .NET endpoint yet are **UI-only shells with MOCK data** (wire to real APIs later).

## 2. The design system (use these — don't hand-roll styles)

| Piece | Where | Use for |
|---|---|---|
| **Design tokens** | `src/theme/tokens.js` → `T` | All colors. `T.navy`, `T.gold`, `T.offwt`, `T.lgrey`, `T.mgrey`, `T.dgrey`, `T.red/green/amber/blue/purple` (+ `L` light variants). |
| **Formatters** | `src/theme/tokens.js` → `fmt` | `fmt.kes(n)`, `fmt.pct(n)`, `fmt.date(d)`, `fmt.num(n)`. |
| **UI kit** | `src/components/ui.jsx` | `Card, Stat, Btn, Badge, Alert, Progress, Input, Select, Modal, DataTable, SectionHeader, Loading, Tabs, EmptyState`. |
| **Shell** | `src/components/AppShell.jsx` | Wrap every module page in `<AppShell>…</AppShell>`. Sidebar/topbar are handled for you. |
| **Charts** | `react-apexcharts` (installed) | `import Chart from 'react-apexcharts'`. Colors from `T`. (The reference Next.js app uses recharts — translate to apexcharts.) |

**Brand rules:** navy `#1B3A5C` (primary), gold `#C8960C` (accent), off-white `#F0F4F8` (page bg),
Inter font, JetBrains Mono for codes/numbers. White-label: prefer `T.brandVar`/`T.accentVar`
(`var(--brand)`/`var(--accent)`) for primary buttons/table headers so tenants can re-theme.
**No amber/black** (old QaliCore brand) in new work.

## 3. Reference module

**`src/pages/DashboardPage.jsx`** is the canonical example. Copy its structure:
- `<AppShell>` wrapper + `<div className="page-content">`
- `Tabs` for sub-sections (the QSL pattern: one sidebar entry per module, tabs inside)
- `Stat` tiles in a `repeat(auto-fit,minmax(170px,1fr))` grid
- `Card` + `SectionHeader` + `DataTable`
- **`MOCK` constant at the top**, shaped like the eventual API response, with a comment pointing to
  the real endpoint. This makes later wiring a find-and-replace.

## 4. How to port a module (checklist)

1. **Screenshot the deployed module** (all tabs). That's the spec.
2. Find its view function in the Next.js source: `~/Downloads/qsl-erp-updated/qsl-erp/src/app/dashboard/page.js`
   (search for the module's function, e.g. `function FinanceModule`). Reuse its layout/logic.
3. **Reuse existing pages** where we already have them (see tracker). Modules we already built
   (Projects, Fleet, Helpdesk, Operations, Users/Roles, Settings, Licensing) are wired to the real
   .NET backend with real features (filters, export, pagination, detail tabs). **EDIT/restyle those
   pages in place** — do NOT create a parallel mock page. Rebrand amber→navy/gold, go full-width, and
   keep the real API + functionality. Leave *semantic* amber (medium-risk / pending-status badges) as
   amber. Only build brand-new pages (with MOCK data) for modules that don't exist yet.
4. Create/replace the page under `src/pages/<module>/…`, wrapped in `<AppShell>`.
5. Use **only** `ui.jsx` primitives + `T` tokens. Translate recharts → react-apexcharts.
6. Add a `MOCK` constant (shape = future API response) unless a real endpoint already exists.
7. Wire the route in `src/App.jsx` (replace the `ModuleShell` placeholder for that path).
8. `npm run build` must pass. Check it in the running app (`npm run dev`, http://localhost:3000).
9. Update the tracker below + move the row to Done.

## 4b. File structure (adopted 2026-07-10, Finance is the reference)

Pages had grown huge (data-fetch + layout + sub-components + modals in one file). New convention — split so no file is enormous:
- **`src/services/<domain>.js`** — the ONLY place API endpoint strings live. One function per call, returns the unwrapped `data` (e.g. `services/finance.js` → `getChartOfAccounts()`, `listJournals()`, `createJournal(dto)`). Pages/components import these, never call `api.get('/api/v1/...')` inline.
- **`src/components/<module>/*.jsx`** — module-specific presentational/self-fetching pieces (tabs, modals, tables). e.g. `components/finance/{CoaTab,JournalsTab,JournalModal}.jsx`.
- **`src/pages/<module>/*.jsx`** — THIN: compose tabs + pass `notify`; no raw fetching.
- `layout/` move (AppShell → layout/) deferred — it touches every page's import; do it as a coordinated pass, not now.
Roll out **incrementally, per module as we touch it** — do NOT big-bang refactor (teammates work in parallel → merge conflicts). Finance done; others when their turn comes.

## 5. Conventions

- **Inline styles** keyed off `T` (matches the source design). Tailwind classes are fine for layout
  but colors come from `T` / the brand vars.
- Page wrapper: `<div className="page-content">` (responsive padding, max-width). Use `.grid-2col` /
  `.grid-3col` helpers for responsive grids.
- Money = `fmt.kes()`. Dates = `fmt.date()`. Empty states = `EmptyState` or `DataTable`'s `empty`.
- One sidebar entry per module; sub-navigation via `Tabs` inside the page (not nested sidebar items).
- Don't touch `AppShell`'s `NAV` unless changing the module list (coordinate — it's shared).

## 6. Module tracker

Legend: ✅ done · 🟡 in progress · ⬜ shell (placeholder) · ♻️ reuse existing page (needs restyle)

| Module | Sidebar group | Route | Status | Owner | Notes |
|---|---|---|---|---|---|
| Dashboard | Overview | `/dashboard` | ✅ | — | Reference module. MD Executive Summary + Analytics (mock data). |
| My Workspace | Overview | `/workspace` | ✅ | — | Overview/Leave/Payslips/My Tasks/Attendance/Account tabs, profile, apply-leave modal, clock in/out (mock/local state). |
| Helpdesk | Helpdesk | `/modules/ticketing` | 🟡 | — | ♻️ TicketingPage RESTYLED: navy/gold + ticket cards (Projects style). Added **Customer Feedback** tab (avg rating, % good/excellent, distribution bars, feedback cards w/ stars+comment) reading satisfactionRating off the ticket list. PDF now has a Rating column. Backend: TicketRepository now Includes SatisfactionRating (list+detail); ticketing service rebuilt. Seeded 12 demo tickets (Apr/May/Jun 2026) w/ history+comments+ratings (90% good/excellent) into tenant_qsl. Real /api/v1/tickets + filters/search/export/pagination kept. TODO: restyle TicketDetail/CreateTicket/HelpdeskDashboard/Tags/Macros/WorkflowRules (still amber). |
| Finance | Finance | `/modules/finance` | ✅ | — | All 13 tabs ported (Imprest, Documents×5, Payroll, Chart of Accounts, Journals, Month-End & P&L, Trial Balance/Balance Sheet, Bank Reconciliation, Cash Flow, Budgets, Payments (AP), Treasury & Statutory, Payment Authority) + all modals. Mock data on the data-heavy tabs; wire to /api/finance later. |
| Debtors | Finance | `/modules/debtors` | ✅ | — | Daily Follow-up (KPIs, Record Status modal, EOD submit) + All Debtors. Mock data. |
| Tax & KRA | Finance | `/modules/tax` | ✅ | — | Tax Dashboard (KPIs + obligations), Tax Invoices (eTIMS + modal), VAT Returns (compute flow + categories), PAYE bands + rates, Statutory Calendar. Mock data. |
| Fixed Assets | Finance | `/modules/assets` | ✅ | — | Asset Register (KPIs, Add Asset modal, Run Depreciation) + Depreciation Schedule. Mock data. |
| Inter-Company | Finance | `/modules/ic` | ✅ | — | IC Transactions (KPIs + New IC Transaction modal w/ ICSA gate) + Consolidation. Mock data. |
| Commercial | Sales & Clients | `/modules/crm` | ✅ | — | All 5 tabs ported + reconciled against the real QSL Next.js source (CRMModule): Client Register (KPIs, client detail, Transfer Account Owner as the real 3-step CFO→MD digital-signature wizard, Edit, Deactivate, document links), Leads & Pipeline (KPIs, New Lead w/ real Service/Source option lists), Quotes (New Quotation w/ line items, PDF + Send-to-Client actions), Support Tickets (SLA banner, New Ticket), Payment Alerts (Send M-PESA, static). Mock data. |
| Bids & Pre-Sales | Sales & Clients | `/modules/bids` | ✅ | — | Bid Pipeline (KPIs, PSB-004 gate banner, New Bid + auto-checklist notice) + full Stage 2B Compliance Matrix ported from the real QSL source (12 real CSE-001..CSE-012 requirements, MEETS/WILL MEET/DOES NOT MEET/PENDING toggles, permanent auto-STOP on any mandatory DOES NOT MEET). Mock data. |
| Online Shop | Sales & Clients | `/modules/shop` | 🟡 | — | Orders tab done (KPIs, auto-landed-order banner, orders table). Mock data (empty). Shop Listings still a placeholder tab. |
| Procurement | Supply Chain | `/modules/procurement` | 🟡 | — | Ported from the QSL Next.js source + deployed-app screenshots (the local reference file predates the LPO-creation UI, per the guide's staleness warning): Requisitions (PROC-003 quote-tier banner, New PR w/ tier note + >500K hard block), LPOs (Create LPO w/ supplier select, line items, live VAT-inclusive subtotal — VAT math matches the real `create_lpo` API: 16% of total), GRN (Stage 1/Photo/Stage 2 gate, read-only), Supplier Register (real deployed seed data — BOC/Emerson/SafeWork/Siemens/TechCal, New Supplier, Edit, Suspend); only approved suppliers feed the LPO picker, matching the tab's own subtitle. GRN creation still not specced. |
| Stores | Supply Chain | `/modules/stores` | 🟡 | — | Ported from the QSL Next.js source + one deployed screenshot: 6 tabs (Items, Stock Balances w/ Stock Take Sheet buttons, Transfers, Adjustments, Stock Take [placeholder — no screenshot of its content yet], Low Stock). Transfers/Adjustments mirror the real pending→approve logic (blocked on insufficient stock; variance = corrected qty − before). Locations seeded with the real 4 (Nairobi HQ/Kisumu/Calibration Lab/Site Stock-Mobile) confirmed via screenshot; the real 9 categories aren't visible in any screenshot yet so stay user-creatable. Items/transfers/adjustments still empty (matches the real deployed state). |
| Requisitions | Supply Chain | `/modules/requisitions` | 🟡 | — | Ported from the QSL Next.js source (no screenshots yet for this module): internal store-requisition workflow — All Requisitions/Pending My Approval tabs, New Requisition, real 2-level supervisor→store_manager approval chain, issue-against-stock per line (blocked on insufficient stock), Close once fully issued, full approval-history audit trail. Distinct from Procurement's Purchase Requisitions (buying from suppliers) — this is issuing from stock already on hand. Self-contained mock item/location/stock catalog (not yet wired to the Stores page's own state). Needs visual verification once screenshots exist. |
| Technical Department | Technical | `/modules/operations` | ♻️ | | Existing operations (service requests/assignments) — restyle + align to calibration. |
| Inspection (17020) | Technical | `/modules/inspection` | ⬜ | | Inspection jobs, checklists, NCRs. |
| Projects | Operations | `/modules/projects` | ✅ | — | ♻️ RESTYLED existing real pages: list→portfolio cards, detail header→budget bar + gross profit. Tabs mirror deployed: Overview/Milestones/Expenses/History = real API; Timesheets/Subcontractors/Handover = FE-only stubs (backend TODO, see memory). Filters/export/pagination + real /api/v1/projects kept. |
| Fleet | Operations | `/modules/fleet` | ✅ | — | ♻️ RESTYLED existing real pages (Dashboard/Trips/Trucks/Drivers/Trip Types/Materials/Trip Detail/Create Trip): amber→navy/gold brand, chart colors navy/gold, zinc stepper→navy. Kept semantic amber (In Progress / Pending / expiry-warning badges) + real /api/v1/trips & fleet API. Our fleet is richer than deployed (deployed = just Vehicles + Trip Log tabs); kept ours per "compare what we have". |
| HSE | Operations | `/modules/hse` | ✅ | — | NEW (src/pages/hse/HsePage.jsx). 3 tabs: Incident Register (empty state + Report Incident modal w/ Type/Site/Related Project/Severity/Description + 24h/48h banner, prepends to local state), RAMS Status (per-project table, all "Missing"/NOT UPLOADED/Upload Now), PPE Tracker (Stores-fed empty state). 4 KPIs (Incidents YTD/Lost Time Injuries/Open CAPAs/Near Misses) + HSE-002 banner. Mock/local — no HSE backend (see memory). |
| Tasks | Operations | → `/modules/operations/assignments` | ✅ | — | Tasks = Operations Assignments. Sidebar "Tasks" now routes straight to the existing real assignments page (no separate page). Old `/modules/tasks` route redirects there. |
| HR & Payroll | People | `/modules/hr` | ✅ | — | Ported from the QSL Next.js source + deployed screenshots: 9 tabs — Employees (real seeded roster incl. James Otieno, QSL-XXX numbering, search box, Edit/Exit), Attendance (+monthly report, GPS trail), Leave (approve/reject), KPI Scorecards (+search), CPD (+search, real 12 platforms — Alison/LinkedIn Learning/Coursera/edX/Udemy/Saylor/Google Digital Garage/FutureLearn/Khan Academy/NEBOSH-IOSH/KASNEB/EBK — log, per-employee log detail), Appraisals (manager review → HR review → real 2/3-consecutive-low-score escalation), Payroll Inputs (HELB, Overtime — real basicSalary/26/8 ×1.5/×2 formula), Increments & Discipline (HR-013/HR-026/HR-020 real logic), Recruitment (Vacancies register + Open Vacancy modal). |
| Quality (QMS) | Quality & Governance | `/modules/quality` | ✅ | — | NEW (src/pages/quality/QualityPage.jsx), built from deployed screenshots. 5 tabs: Overview (KPIs+ISO note), Nonconformities & CAPA (register + Raise NC modal), Internal Audits (programme + Plan Audit modal), Management Review (register + Schedule Review modal), Competency Matrix (authorization matrix + Grant Authorization modal w/ expiry→amber/red). Mock/local state — no QMS backend (see memory). |
| Compliance | Quality & Governance | `/modules/compliance` | ✅ | — | NEW (src/pages/quality/CompliancePage.jsx). 3 tabs: Certificates (7 mock certs, Days-Left colored red/amber/green, Renew(gold)/View action), Statutory Calendar (10 mock KRA/NHIF/NSSF… obligations + search), Tasks (empty + New Compliance Task modal). 4 KPIs (Certificates/Current/Expiring ≤60/Open Tasks). Mock data. |
| SOP Library | Quality & Governance | `/modules/sops` | ✅ | — | NEW (src/pages/quality/SopLibraryPage.jsx). Department filter pills (All/Technical/Commercial/Finance/HR/Procurement/Stores/Projects/HSE/Administration) + table (Code/Title/Department/Category/Version/Last Reviewed/Next Review/Status). New SOP modal (Code/Department/Title/Category/Next Review/Document file) → Rev 1 local row. Status derives from next-review date (green/amber/red). Mock/local. |
| Reports | Insights | `/modules/reports` | ⬜ | | Cross-module reports/exports. |
| Administration | System | `/modules/users` | ♻️ | | Existing users/roles/permissions/departments — consolidate under Admin, restyle. |
| Integrations | System | `/modules/integrations` | ⬜ | | Third-party integrations. |
| Settings | System | `/modules/settings` | ♻️ | | Existing settings — restyle. |
| Licensing | (not in CEO sidebar) | `/modules/licensing` | ♻️ | | Existing; route works but hidden from sidebar. Decide placement with CEO. |
