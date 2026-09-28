using System.ComponentModel.DataAnnotations;

namespace Trellis.Api.Modules.RentLedger.Configuration;

/// <summary>coding_standards.md's Configuration pattern, bound from <c>Modules__RentLedger__*</c> — see ReportingOptions's identical doc comment.</summary>
public class RentLedgerOptions
{
    /// <summary>BDR-RENT-003 — the grace period applied to a new schedule that doesn't specify one (rent_collection_design.md's default of 5 days); each schedule can then be edited individually.</summary>
    [Range(0, 60)]
    public int DefaultGracePeriodDays { get; init; } = 5;
}
