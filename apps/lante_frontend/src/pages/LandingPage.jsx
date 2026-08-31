// src/pages/LandingPage.jsx — public Home page ('/'), styled to match the QSL
// (Qalibrated Systems Limited) reference design. Condensed overview — hero +
// teaser of each section — linking out to the full page for detail.
import { Link } from 'react-router-dom';
import { T } from '../theme/tokens.js';
import { GridMotif, NavBar, Footer, StaffLoginStrip, PageEyebrow, ServiceCard, AccreditationBadge, SERVICES, ACCREDITATIONS } from '../components/public/shared.jsx';

export default function LandingPage() {
  return (
    <div style={{ fontFamily: "'Inter', sans-serif", color: T.dgrey, background: T.offwt }}>
      <NavBar active="/" />

      {/* ── HERO ────────────────────────────────────────────────────────── */}
      <div style={{ position: 'relative', background: `linear-gradient(160deg, ${T.navyD} 0%, ${T.navy} 65%, ${T.navyL} 100%)`, overflow: 'hidden' }}>
        <GridMotif />
        <div style={{ position: 'relative', maxWidth: 980, margin: '0 auto', padding: '110px 32px 90px' }}>
          <div style={{
            display: 'inline-block', fontSize: 11, fontWeight: 700, letterSpacing: '0.1em', textTransform: 'uppercase',
            color: T.goldL, border: '1px solid rgba(232,184,77,0.4)', borderRadius: 99, padding: '5px 14px', marginBottom: 22,
          }}>
            ISO/IEC 17025 &amp; ISO/IEC 17020 Accredited — KENAS CL/059
          </div>
          <h1 style={{ fontSize: 'clamp(34px, 5vw, 50px)', fontWeight: 800, color: T.white, lineHeight: 1.1, letterSpacing: '-0.015em', margin: '0 0 22px', maxWidth: 760 }}>
            Calibration and inspection you can trust, traced back to national standards.
          </h1>
          <p style={{ fontSize: 17, color: 'rgba(255,255,255,0.75)', lineHeight: 1.6, maxWidth: 580, margin: '0 0 44px' }}>
            Lante is a multi-tenant ERP platform built for accredited calibration, inspection, and equipment
            maintenance operations across Kenya — keeping every job traceable to KEBS and BIPM national measurement standards.
          </p>
          <div style={{ display: 'flex', gap: 14, marginBottom: 0, flexWrap: 'wrap' }}>
            <Link to="/contact" style={{ fontSize: 14, fontWeight: 700, color: T.navyD, background: T.gold, padding: '13px 26px', borderRadius: 8, textDecoration: 'none' }}>
              Request a Quote →
            </Link>
            <Link to="/services" style={{ fontSize: 14, fontWeight: 600, color: T.white, background: 'rgba(255,255,255,0.08)', border: '1px solid rgba(255,255,255,0.18)', padding: '13px 26px', borderRadius: 8, textDecoration: 'none' }}>
              Our Services
            </Link>
          </div>
        </div>
      </div>

      {/* ── SERVICES TEASER ────────────────────────────────────────────── */}
      <div style={{ maxWidth: 1080, margin: '0 auto', padding: '80px 32px 40px' }}>
        <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 20, flexWrap: 'wrap', marginBottom: 44 }}>
          <div style={{ maxWidth: 560 }}>
            <PageEyebrow>What we do</PageEyebrow>
            <h2 style={{ fontSize: 28, fontWeight: 800, color: T.navy, margin: '0 0 10px', letterSpacing: '-0.01em' }}>Accredited services for industry, utilities, and government.</h2>
          </div>
          <Link to="/services" style={{ fontSize: 13.5, fontWeight: 700, color: T.navy, textDecoration: 'none', whiteSpace: 'nowrap' }}>
            View all services →
          </Link>
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(250px, 1fr))', gap: 18 }}>
          {SERVICES.slice(0, 3).map(s => <ServiceCard key={s.title} {...s} />)}
        </div>
      </div>

      {/* ── ACCREDITATION ───────────────────────────────────────────────── */}
      <div style={{ background: T.navyD, padding: '70px 32px' }}>
        <div style={{ maxWidth: 1080, margin: '0 auto' }}>
          <div style={{ fontSize: 12, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase', color: T.gold, marginBottom: 14, textAlign: 'center' }}>Quality you can verify</div>
          <h2 style={{ fontSize: 26, fontWeight: 700, color: T.white, margin: '0 0 36px', letterSpacing: '-0.01em', textAlign: 'center' }}>Independently accredited, not self-declared.</h2>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            {ACCREDITATIONS.map(a => <AccreditationBadge key={a.code} {...a} />)}
          </div>
        </div>
      </div>

      {/* ── ABOUT TEASER ───────────────────────────────────────────────── */}
      <div style={{ maxWidth: 1080, margin: '0 auto', padding: '80px 32px' }}>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 48, alignItems: 'center' }}>
          <div>
            <PageEyebrow>About Lante</PageEyebrow>
            <h2 style={{ fontSize: 26, fontWeight: 800, color: T.navy, margin: '0 0 16px', letterSpacing: '-0.01em' }}>Built on traceability, not guesswork.</h2>
            <p style={{ fontSize: 14.5, color: T.mgrey, lineHeight: 1.7, marginBottom: 22 }}>
              Based in Nairobi, Kenya, Lante is a multi-tenant ERP platform purpose-built for accredited
              calibration and inspection providers serving industrial, utility, and government clients across
              the region — every job runs through the same quality system, with documented traceability at every step.
            </p>
            <Link to="/about" style={{ fontSize: 13.5, fontWeight: 700, color: T.navy, textDecoration: 'none' }}>
              More about Lante →
            </Link>
          </div>
          <div style={{ background: T.white, border: `1px solid ${T.lgrey}`, borderRadius: 12, padding: '28px 26px' }}>
            <div style={{ fontSize: 13, fontWeight: 700, color: T.navy, marginBottom: 16, textTransform: 'uppercase', letterSpacing: '0.04em' }}>Get in touch</div>
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
            <Link to="/contact" style={{
              display: 'block', textAlign: 'center', marginTop: 8, fontSize: 13.5, fontWeight: 700, color: T.navyD,
              background: T.gold, padding: '12px 20px', borderRadius: 8, textDecoration: 'none',
            }}>
              Request a Quote →
            </Link>
          </div>
        </div>
      </div>

      <StaffLoginStrip />
      <Footer />
    </div>
  );
}
