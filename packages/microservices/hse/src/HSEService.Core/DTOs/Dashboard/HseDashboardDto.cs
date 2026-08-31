namespace HSEService.Core.DTOs.Dashboard;

// HSE-008: TRIR, LTIF, near-miss frequency, open corrective actions — updated in real time
// (computed on demand from current data rather than a materialized/cached snapshot).
public class HseDashboardDto
{
    /// <summary>Total Recordable Incident Rate = (recordable incidents × 200,000) / hours worked.</summary>
    public decimal Trir { get; set; }
    /// <summary>Lost Time Injury Frequency = (lost-time injuries × 1,000,000) / hours worked.</summary>
    public decimal Ltif { get; set; }
    public int NearMissCount { get; set; }
    public int TotalIncidentsYtd { get; set; }
    public int OpenCorrectiveActions { get; set; }
    public int OverdueCorrectiveActions { get; set; }
    public int RamsPendingApproval { get; set; }
    public int TrainingCertificatesExpiringSoon { get; set; }
    public int StatutoryInspectionsDueSoon { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
