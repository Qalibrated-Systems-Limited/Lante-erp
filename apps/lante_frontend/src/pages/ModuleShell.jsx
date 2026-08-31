import { T } from "../theme/tokens.js";

// Placeholder shell for QSL modules not yet ported. Renders inside the rebranded
// AppShell (so the sidebar/topbar are correct) with an on-brand "coming soon"
// panel. Each of these will be replaced by the module's ported UI.
export default function ModuleShell({ title, icon = "🧩", blurb }) {
  return (
    <>
      <div className="page-content">
        <div style={{
          background: T.white, border: `1px solid ${T.lgrey}`, borderRadius: 12,
          padding: "56px 32px", textAlign: "center", boxShadow: "0 1px 3px rgba(27,58,92,.08)",
        }}>
          <div style={{ fontSize: 44, marginBottom: 14 }}>{icon}</div>
          <h2 style={{ fontSize: 20, fontWeight: 700, color: T.navy, margin: "0 0 8px" }}>{title}</h2>
          <p style={{ fontSize: 13, color: T.mgrey, maxWidth: 460, margin: "0 auto 20px", lineHeight: 1.6 }}>
            {blurb || "This module is being built to match the QSL design. The interface will appear here shortly."}
          </p>
          <span style={{ display: "inline-block", background: T.amberL, color: T.amber, padding: "5px 14px", borderRadius: 20, fontSize: 12, fontWeight: 700 }}>
            In progress
          </span>
        </div>
      </div>
    </>
  );
}
