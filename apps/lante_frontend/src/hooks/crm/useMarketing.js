import { useState, useEffect, useCallback } from 'react'
import * as crm from '../../services/crm.js'
import { useAuth } from '../../context/AuthContext.jsx'

export const CAMPAIGN_TYPES = ['Exhibition', 'TradeShow', 'Email', 'SiteVisit', 'Digital', 'Other']
export const CAMPAIGN_STATUSES = ['Planned', 'Active', 'Completed', 'Cancelled']
export const CAMPAIGN_STATUS_VARIANT = {
  Planned: 'default', Active: 'blue', Completed: 'green', Cancelled: 'red',
}
export const BRAND_ASSET_TYPES = ['Logo', 'Template', 'Brochure', 'Other']
export const cTypeLabel = (s) => (s || '').replace('TradeShow', 'Trade Show').replace('SiteVisit', 'Site Visit')

// Marketing hub: dashboard KPIs + campaign list + brand-asset library, with mutations.
export function useMarketing() {
  const { hasPermission } = useAuth()
  const canWrite = hasPermission?.('crm.write')

  const [dashboard, setDashboard] = useState(null)
  const [campaigns, setCampaigns] = useState([])
  const [assets, setAssets] = useState([])
  const [statusF, setStatusF] = useState('')
  const [assetTypeF, setAssetTypeF] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const [d, c, a] = await Promise.allSettled([
        crm.getMarketingDashboard(),
        crm.listCampaigns({ status: statusF || undefined }),
        crm.listBrandAssets({ type: assetTypeF || undefined }),
      ])
      if (d.status === 'fulfilled') setDashboard(d.value)
      if (c.status === 'fulfilled') setCampaigns(c.value ?? [])
      if (a.status === 'fulfilled') setAssets(a.value ?? [])
      if ([d, c, a].every(x => x.status === 'rejected')) setError('Failed to load marketing data.')
    } finally {
      setLoading(false)
    }
  }, [statusF, assetTypeF])

  useEffect(() => { load() }, [load])

  return {
    dashboard, campaigns, assets, loading, error, canWrite,
    statusF, setStatusF, assetTypeF, setAssetTypeF, reload: load,
  }
}
