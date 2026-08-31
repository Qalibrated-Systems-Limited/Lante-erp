import { useState, useEffect, useCallback } from 'react'
import StoresNav from './StoresNav.jsx'
import api from '../../api/axios.js'
import { KPI_GRID, Kpi, Loading } from '../../components/ui.jsx'
import LowStockCard from './dashboard/LowStockCard.jsx'
import PriceAlertsCard from './dashboard/PriceAlertsCard.jsx'
import RecentGrnsCard from './dashboard/RecentGrnsCard.jsx'
import FlowGuideCard from './dashboard/FlowGuideCard.jsx'
import LocationBreakdownCard from './dashboard/LocationBreakdownCard.jsx'
import { TrendingDown, Package, Search, Flag, FolderOpen, MapPin } from 'lucide-react'

const PAD = 'clamp(16px, 2.4vw, 26px)'

export default function StoresDashboardPage() {
  const [loading, setLoading] = useState(true)
  const [lowStock, setLowStock] = useState([])
  const [pendingGrnCount, setPendingGrnCount] = useState(0)
  const [pendingStockTakeCount, setPendingStockTakeCount] = useState(0)
  const [priceAlerts, setPriceAlerts] = useState([])
  const [recentGrns, setRecentGrns] = useState([])
  const [categoryCount, setCategoryCount] = useState(0)
  const [locationCount, setLocationCount] = useState(0)
  const [acknowledgingId, setAcknowledgingId] = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const [lowStockRes, pendingGrnRes, pendingStockTakeRes, recentGrnRes, categoriesRes, locationsRes, historyRes] = await Promise.allSettled([
        api.get('/api/v1/items/low-stock'),
        api.get('/api/v1/grn', { params: { inspectionStatus: 'Pending', pageSize: 1 } }),
        api.get('/api/v1/stock-take', { params: { status: 'Pending', pageSize: 1 } }),
        api.get('/api/v1/grn', { params: { pageSize: 8 } }),
        api.get('/api/v1/categories', { params: { pageSize: 1 } }),
        api.get('/api/v1/locations', { params: { pageSize: 1 } }),
        api.get('/api/v1/purchase-price-history', { params: { pageSize: 50 } }),
      ])

      setLowStock(lowStockRes.value?.data?.data ?? [])
      setPendingGrnCount(pendingGrnRes.value?.data?.data?.totalCount ?? 0)
      setPendingStockTakeCount(pendingStockTakeRes.value?.data?.data?.totalCount ?? 0)
      setRecentGrns(recentGrnRes.value?.data?.data?.items ?? [])
      setCategoryCount(categoriesRes.value?.data?.data?.totalCount ?? 0)
      setLocationCount(locationsRes.value?.data?.data?.totalCount ?? 0)

      const alerts = (historyRes.value?.data?.data?.items ?? []).filter(h => h.alertLevel !== 'None')
      setPriceAlerts(alerts.slice(0, 6))
    } catch (err) {
      console.error(err)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  async function acknowledge(itemId, e) {
    e.stopPropagation()
    setAcknowledgingId(itemId)
    try {
      await api.post(`/api/v1/items/${itemId}/acknowledge-low-stock`)
      load()
    } catch (err) {
      console.error(err)
    } finally {
      setAcknowledgingId(null)
    }
  }

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        <StoresNav />

        <FlowGuideCard />

        <div style={{ ...KPI_GRID, marginBottom: 22 }}>
          <Kpi label="Low Stock Items" value={lowStock.length} icon={<TrendingDown size={20} />} variant={lowStock.length ? 'red' : 'green'} />
          <Kpi label="Pending GRN Inspections" value={pendingGrnCount} icon={<Package size={20} />} variant="blue" />
          <Kpi label="Open Stock-Take Variances" value={pendingStockTakeCount} icon={<Search size={20} />} variant="amber" />
          <Kpi label="Price Alerts" value={priceAlerts.length} icon={<Flag size={20} />} />
          <Kpi label="Categories" value={categoryCount} icon={<FolderOpen size={20} />} />
          <Kpi label="Locations" value={locationCount} icon={<MapPin size={20} />} />
        </div>

        {loading ? <Loading /> : (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 16 }}>
            <LowStockCard lowStock={lowStock} acknowledgingId={acknowledgingId} onAcknowledge={acknowledge} />
            <PriceAlertsCard priceAlerts={priceAlerts} />
            <LocationBreakdownCard />
            <RecentGrnsCard recentGrns={recentGrns} />
          </div>
        )}
      </div>
    </>
  )
}
