using System.ComponentModel.DataAnnotations;

namespace Trellis.Api.Modules.Leasing.Configuration;

/// <summary>coding_standards.md's Configuration pattern, bound from <c>Modules__Leasing__*</c> — see ReportingOptions's identical doc comment.</summary>
public class LeasingOptions
{
    /// <summary>BDR-LSE-002/008 — how long an application link stays resolvable, same [Range] pattern as ReportingOptions.ShareLinkExpiryDays.</summary>
    [Range(1, 30)]
    public int ApplicationLinkExpiryDays { get; init; } = 7;

    /// <summary>How many photos a single listing may carry — a sane upper bound, same reasoning as MaxPhotosPerListing precedents elsewhere in this codebase's file-upload limits.</summary>
    [Range(1, 50)]
    public int MaxPhotosPerListing { get; init; } = 12;
}
