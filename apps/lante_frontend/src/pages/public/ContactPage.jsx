// src/pages/public/ContactPage.jsx — Contact page of the public QSL site.
// The form posts through ContactForm to the real /api/v1/portal/submit
// endpoint (same intake used by /portal/submit), not a fabricated API.
import { T } from '../../theme/tokens.js';
import { GridMotif, NavBar, Footer, StaffLoginStrip, PageEyebrow, ContactForm } from '../../components/public/shared.jsx';

export default function ContactPage() {
  return (
    <div style={{ fontFamily: "'Inter', sans-serif", color: T.dgrey, background: T.offwt }}>
      <NavBar active="/contact" />

      <div style={{ position: 'relative', background: `linear-gradient(160deg, ${T.navyD} 0%, ${T.navy} 65%, ${T.navyL} 100%)`, overflow: 'hidden' }}>
        <GridMotif />
        <div style={{ position: 'relative', maxWidth: 980, margin: '0 auto', padding: '80px 32px 64px' }}>
          <PageEyebrow>Request a quote</PageEyebrow>
          <h1 style={{ fontSize: 'clamp(30px, 4.5vw, 42px)', fontWeight: 800, color: T.white, lineHeight: 1.15, letterSpacing: '-0.015em', margin: '0 0 16px', maxWidth: 600 }}>
            Tell us what you need.
          </h1>
          <p style={{ fontSize: 15.5, color: 'rgba(255,255,255,0.75)', lineHeight: 1.65, maxWidth: 560, margin: 0 }}>
            Enquiries go straight to our Commercial team — we typically respond within one business day.
          </p>
        </div>
      </div>

      <div style={{ maxWidth: 980, margin: '0 auto', padding: '64px 32px' }}>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1.2fr', gap: 40, alignItems: 'start' }}>
          <div style={{ background: T.white, border: `1px solid ${T.lgrey}`, borderRadius: 12, padding: '28px 26px' }}>
            <div style={{ fontSize: 13, fontWeight: 700, color: T.navy, marginBottom: 16, textTransform: 'uppercase', letterSpacing: '0.04em' }}>Contact details</div>
            {[
              ['📍', 'Address', 'Birdi Singh Complex, Off Mombasa Road, Nairobi, Kenya'],
              ['📞', 'Phone', '+254 714 999 996 / 756 999 996'],
              ['✉️', 'Email', 'info@qalibrated.co.ke'],
              ['🏢', 'P.O. Box', '34463-00100 GPO Nairobi'],
            ].map(([icon, label, val]) => (
              <div key={label} style={{ display: 'flex', gap: 12, marginBottom: 14 }}>
                <div style={{ fontSize: 18 }}>{icon}</div>
                <div>
                  <div style={{ fontSize: 11, color: T.mgrey, fontWeight: 600, textTransform: 'uppercase' }}>{label}</div>
                  <div style={{ fontSize: 13.5, color: T.dgrey }}>{val}</div>
                </div>
              </div>
            ))}
          </div>

          <ContactForm />
        </div>
      </div>

      <StaffLoginStrip />
      <Footer />
    </div>
  );
}
