using Trellis.Api.Core.Results;
using Xunit;

namespace Trellis.Tests;

/// <summary>
/// A real (if minimal) test, not a placeholder assertion — proves the test project actually
/// references and exercises Trellis.Api's Core, ahead of the module-scoped test suites
/// testing_strategy.md describes (multi-tenant isolation, BDR-to-test traceability).
/// </summary>
public class ServiceResultTests
{
    [Fact]
    public void Success_carries_the_data_and_no_error()
    {
        var result = ServiceResult<string>.Success("ok");

        Assert.Equal(ServiceResultOutcome.Success, result.Outcome);
        Assert.Equal("ok", result.Data);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void NotFound_carries_no_data()
    {
        var result = ServiceResult<string>.NotFound();

        Assert.Equal(ServiceResultOutcome.NotFound, result.Outcome);
        Assert.Null(result.Data);
    }

    [Fact]
    public void ConcurrencyConflict_is_distinct_from_plain_conflict()
    {
        // coding_standards.md, "Optimistic Concurrency": a distinct outcome from Conflict()
        // so the frontend can tell "already exists" apart from "changed under you".
        var concurrency = ServiceResult<string>.ConcurrencyConflict();
        var conflict = ServiceResult<string>.Conflict("TRL-E-PLT-0090", "duplicate"); // placeholder — see api_standards.md's registry table

        Assert.NotEqual(concurrency.ErrorCode, conflict.ErrorCode);
        Assert.Equal(ServiceResultOutcome.ConcurrencyConflict, concurrency.Outcome);
        Assert.Equal(ServiceResultOutcome.Conflict, conflict.Outcome);
    }
}
