// src/pages/public/VerifyResultPage.jsx — the QR-code landing page.
// No public certificate-lookup endpoint exists yet (certificates are only
// reachable via the authenticated assignment/lab-work-order routes) — this
// honestly renders the reference design's "not found" state rather than
// faking a match.
import { Link, useParams } from 'react-router-dom';
import { T } from '../../theme/tokens.js';
import { NavBar, Footer, PageEyebrow } from '../../components/public/shared.jsx';

export default function VerifyResultPage() {
  const { certNo: rawCertNo } = useParams();
  const certNo = decodeURIComponent(rawCertNo);

  return (
    <div style={{ fontFamily: "'Inter', sans-serif", color: T.dgrey, background: T.offwt, minHeight: '100vh' }}>
      <NavBar active="/verify" />
      <div style={{ maxWidth: 560, margin: '0 auto', padding: '60px 32px 90px' }}>
        <PageEyebrow>Certificate Verification</PageEyebrow>

        <div style={{ background: '#FEF2F2', border: '1px solid #FCA5A5', borderRadius: 12, padding: '32px 28px', textAlign: 'center' }}>
          <div style={{ fontSize: 32, marginBottom: 10 }}>⚠️</div>
          <div style={{ fontSize: 16, fontWeight: 700, color: '#DC2626', marginBottom: 8 }}>Certificate not found</div>
          <div style={{ fontSize: 13.5, color: T.dgrey, lineHeight: 1.6 }}>
            "{certNo}" doesn't match any certificate on record. If you scanned a QR code and see this, please{' '}
            <Link to="/contact" style={{ color: T.navy }}>contact us</Link> so we can check it directly.
          </div>
        </div>

        <Link to="/verify" style={{ display: 'block', textAlign: 'center', marginTop: 24, fontSize: 13, color: T.mgrey, textDecoration: 'none' }}>
          ← Verify a different certificate
        </Link>
      </div>
      <Footer />
    </div>
  );
}
