using FluentAssertions;
using Trellis.Api.Modules.Leasing.Application.Dtos;
using Trellis.Api.Modules.Leasing.Application.Validators;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Validators;
using Trellis.Api.Modules.RentLedger.Application.Dtos;
using Trellis.Api.Modules.RentLedger.Application.Validators;
using Xunit;

namespace Trellis.Tests.PropertyAsset;

/// <summary>BDR-LSE-009/011 at the API edge — and that the three requests that end a lease all apply the same rules.</summary>
public sealed class LeaseRequestValidatorTests
{
    private static readonly DateOnly Yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

    public static TheoryData<string, string, string?, bool> EndCases => new()
    {
        { "NON_RENEWAL", "LESSEE", null, true },
        { "OTHER", "PM_COMPANY", "Owner moving in", true },
        { "OTHER", "PM_COMPANY", null, false },
        { "NOT_RECORDED", "LESSEE", null, false },
        { "TENANT_LEFT", "LESSEE", null, false },
        { "NON_RENEWAL", "LANDLORD", null, false },
    };

    [Theory]
    [MemberData(nameof(EndCases))]
    public void EveryEndRequest_AppliesTheSameReasonPartyAndNoteRules(string reason, string endedBy, string? note, bool valid)
    {
        new LeaseEndRequestValidator().Validate(new LeaseEndRequest(Yesterday, reason, endedBy, note)).IsValid.Should().Be(valid);
        new EndLeaseRequestValidator().Validate(new EndLeaseRequest(Yesterday, reason, endedBy, note)).IsValid.Should().Be(valid);
        new RentScheduleEndRequestValidator().Validate(new RentScheduleEndRequest(Yesterday, reason, endedBy, note)).IsValid.Should().Be(valid);
    }

    [Fact]
    public void AnEndRequest_CantBeDatedInTheFuture()
    {
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        new LeaseEndRequestValidator().Validate(new LeaseEndRequest(tomorrow, "NON_RENEWAL", "LESSEE", null)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("2027-10-31", false, true)]
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    [InlineData("2027-10-31", true, false)]
    [InlineData("2026-11-01", false, false)]
    public void SendForSignature_NeedsAFixedEndAfterTheStart_OrMonthToMonth(string? end, bool monthToMonth, bool valid)
    {
        var request = new SendForSignatureRequest(
            Guid.NewGuid(), new DateOnly(2026, 11, 1),
            end is null ? null : DateOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture), monthToMonth);

        new SendForSignatureRequestValidator().Validate(request).IsValid.Should().Be(valid);
    }

    [Theory]
    [InlineData("FIXED", "2026-12-31", true)]
    [InlineData("FIXED", null, false)]
    [InlineData("MONTH_TO_MONTH", null, true)]
    [InlineData("MONTH_TO_MONTH", "2026-12-31", false)]
    [InlineData("NOT_RECORDED", null, false)]
    public void RecordingATerm_AcceptsOnlyARealTerm(string termType, string? end, bool valid)
    {
        var request = new LeaseTermRequest(
            termType, end is null ? null : DateOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture), "AAAAAAAAAAA=");

        new LeaseTermRequestValidator().Validate(request).IsValid.Should().Be(valid);
    }
}
