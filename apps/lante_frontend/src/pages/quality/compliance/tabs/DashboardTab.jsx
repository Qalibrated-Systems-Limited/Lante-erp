import { T, fmt } from '../../../../theme/tokens.js'
import { Kpi, KPI_GRID, SectionHeader, HelpPanel, worstVariant } from '../../../../components/ui.jsx'

export default function DashboardTab({ dashboard, canSeeWhistleblower }) {
  const giftsVariant = (dashboard?.giftsFlaggedPendingReview ?? 0) > 0 ? 'amber' : 'green'
  const coiVariant = (dashboard?.coiDeclarationsOutstandingThisYear ?? 0) > 0 ? 'amber' : 'green'
  const whistleblowerVariant = (dashboard?.whistleblowerCasesOpen ?? 0) > 0 ? 'red' : 'green'
  const dsrDueSoonVariant = (dashboard?.dsrDueSoon ?? 0) > 0 ? 'amber' : 'green'
  const dsrOverdueVariant = (dashboard?.dsrOverdue ?? 0) > 0 ? 'red' : 'green'
  const breachesVariant = (dashboard?.dataBreachesPendingOdpcNotification ?? 0) > 0 ? 'red' : 'green'
  const licencesExpiringVariant = (dashboard?.licencesExpiringSoon ?? 0) > 0 ? 'amber' : 'green'
  const licencesExpiredVariant = (dashboard?.licencesExpired ?? 0) > 0 ? 'red' : 'green'
  const abcTrainingVariant = (dashboard?.abcTrainingDueSoon ?? 0) > 0 ? 'amber' : 'green'
  const relatedPartyVariant = (dashboard?.relatedPartyTransactionsUnreported ?? 0) > 0 ? 'amber' : 'green'

  return (
    <>
      <SectionHeader title="Compliance & Governance Dashboard" sub="Live counts across every compliance register" />
      <HelpPanel variant={worstVariant(
        giftsVariant, coiVariant, ...(canSeeWhistleblower ? [whistleblowerVariant] : []),
        dsrDueSoonVariant, dsrOverdueVariant, breachesVariant, licencesExpiringVariant,
        licencesExpiredVariant, abcTrainingVariant, relatedPartyVariant,
      )}>
        This dashboard rolls up compliance health across every register in the module: gifts flagged for review,
        overdue Data Subject Requests, data breaches pending ODPC notification, and licences expiring soon
        {canSeeWhistleblower ? ', along with whistleblower case counts' : ''}. The numbers update live as records
        change on any other tab.
      </HelpPanel>
      <div style={{ ...KPI_GRID }}>
        <Kpi label="Gifts Flagged Pending Review" value={dashboard?.giftsFlaggedPendingReview ?? 0} icon="🎁" variant={giftsVariant} />
        <Kpi label="COI Outstanding This Year" value={dashboard?.coiDeclarationsOutstandingThisYear ?? 0} icon="📋" variant={coiVariant} />
        {canSeeWhistleblower && <Kpi label="Whistleblower Cases Open" value={dashboard?.whistleblowerCasesOpen ?? 0} icon="🔒" variant={whistleblowerVariant} />}
        <Kpi label="DSR Due Soon" value={dashboard?.dsrDueSoon ?? 0} icon="📨" variant={dsrDueSoonVariant} />
        <Kpi label="DSR Overdue" value={dashboard?.dsrOverdue ?? 0} icon="⏰" variant={dsrOverdueVariant} />
        <Kpi label="Breaches Pending ODPC Notification" value={dashboard?.dataBreachesPendingOdpcNotification ?? 0} icon="🚨" variant={breachesVariant} />
        <Kpi label="Licences Expiring Soon" value={dashboard?.licencesExpiringSoon ?? 0} icon="📄" variant={licencesExpiringVariant} />
        <Kpi label="Licences Expired" value={dashboard?.licencesExpired ?? 0} icon="❌" variant={licencesExpiredVariant} />
        <Kpi label="ABC Training Due Soon" value={dashboard?.abcTrainingDueSoon ?? 0} icon="🎓" variant={abcTrainingVariant} />
        <Kpi label="Related Party Txns Unreported" value={dashboard?.relatedPartyTransactionsUnreported ?? 0} icon="🔗" variant={relatedPartyVariant} />
      </div>
      {dashboard?.generatedAt && <p style={{ fontSize: 11, color: T.mgrey, marginTop: 12 }}>Last computed {fmt.date(dashboard.generatedAt)}</p>}
    </>
  )
}
