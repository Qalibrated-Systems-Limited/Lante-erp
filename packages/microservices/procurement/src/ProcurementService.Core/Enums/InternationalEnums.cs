namespace ProcurementService.Core.Enums;

/// <summary>P7 (PROC-003) — lifecycle of an international order, from proforma through customs clearance to
/// a finalised landed cost. Advances independently of the LPO's own approval status: the LPO is approved and
/// issued first, then the T/T advance, shipment and clearance play out against it.</summary>
public enum IntlPoStatus
{
    Draft,              // FX terms captured
    ProformaReceived,   // supplier proforma invoice attached
    TtApproved,         // MD has approved the telegraphic-transfer advance
    TtSent,             // funds transferred to the foreign supplier
    Shipped,            // bill of lading / airway bill issued
    Cleared,            // customs declaration lodged
    Costed,             // landed cost finalised (per-unit computed)
}

/// <summary>P7 — the cost buckets that make up the landed cost, per the PROC-003 formula:
/// (purchase price + freight + import duty + clearing agent fees + port and handling charges) ÷ qty received.
/// Insurance and Other are carried too, since real shipments incur them.</summary>
public enum LandedCostComponentType
{
    Freight,
    ImportDuty,
    Clearing,       // clearing agent fees
    PortCharges,    // port and handling
    Insurance,
    Other,
}
