// src/pages/public/ShopPage.jsx — Shop catalog page.
// UI-only shell with MOCK data (Online Shop module has no .NET endpoint yet —
// see PORTING_GUIDE.md tracker). Wire to a real products endpoint later.
import { Link } from 'react-router-dom';
import { T } from '../../theme/tokens.js';
import { GridMotif, NavBar, Footer, PageEyebrow, ProductCard, PRODUCTS } from '../../components/public/shared.jsx';

export default function ShopPage() {
  const products = PRODUCTS;

  return (
    <div style={{ fontFamily: "'Inter', sans-serif", color: T.dgrey, background: T.offwt, minHeight: '100vh' }}>
      <NavBar active="/shop" />

      <div style={{ position: 'relative', background: `linear-gradient(160deg, ${T.navyD} 0%, ${T.navy} 65%, ${T.navyL} 100%)`, overflow: 'hidden' }}>
        <GridMotif />
        <div style={{ position: 'relative', maxWidth: 980, margin: '0 auto', padding: '70px 32px 56px' }}>
          <PageEyebrow>Shop</PageEyebrow>
          <h1 style={{ fontSize: 'clamp(28px, 4.5vw, 40px)', fontWeight: 800, color: T.white, lineHeight: 1.15, letterSpacing: '-0.015em', margin: '0 0 14px', maxWidth: 700 }}>
            Spare parts, tools, and equipment — straight from our stores.
          </h1>
          <p style={{ fontSize: 14.5, color: 'rgba(255,255,255,0.75)', lineHeight: 1.6, maxWidth: 560, margin: 0 }}>
            Order directly from Lante's inventory. Place your order online, we'll confirm by email, and
            payment is settled on delivery or invoice.
          </p>
        </div>
      </div>

      <div style={{ maxWidth: 1080, margin: '0 auto', padding: '48px 32px 80px' }}>
        {products.length === 0 ? (
          <div style={{ textAlign: 'center', padding: '60px 0', color: T.mgrey, fontSize: 14 }}>
            Nothing listed in the shop right now — <Link to="/contact" style={{ color: T.navy }}>get in touch</Link> and we'll help directly.
          </div>
        ) : (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(230px, 1fr))', gap: 18 }}>
            {products.map(p => <ProductCard key={p.id} product={p} />)}
          </div>
        )}
      </div>

      <Footer />
    </div>
  );
}
