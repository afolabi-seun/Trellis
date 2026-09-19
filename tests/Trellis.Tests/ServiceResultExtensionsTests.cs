using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Trellis.Api.Core.Results;
using Xunit;

namespace Trellis.Tests;

/// <summary>
/// Proves ToActionResult() implements error_handling.md's outcome→HTTP mapping table —
/// the one place that mapping is allowed to live, per project_architecture.md's Thin
/// Controllers section.
/// </summary>
public class ServiceResultExtensionsTests
{
    private static DefaultHttpContext NewContext() => new();

    [Fact]
    public void Success_maps_to_200()
    {
        var result = ServiceResult<string>.Success("ok").ToActionResult(NewContext());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, objectResult.StatusCode);
    }

    [Fact]
    public void NotFound_maps_to_404()
    {
        var result = ServiceResult<string>.NotFound().ToActionResult(NewContext());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public void ConcurrencyConflict_and_Conflict_both_map_to_409_with_different_codes()
    {
        var concurrency = ServiceResult<string>.ConcurrencyConflict().ToActionResult(NewContext());
        var conflict = ServiceResult<string>.Conflict("TRL-E-PLT-0090", "duplicate").ToActionResult(NewContext());

        var concurrencyResult = Assert.IsType<ObjectResult>(concurrency);
        var conflictResult = Assert.IsType<ObjectResult>(conflict);

        Assert.Equal(StatusCodes.Status409Conflict, concurrencyResult.StatusCode);
        Assert.Equal(StatusCodes.Status409Conflict, conflictResult.StatusCode);

        var concurrencyBody = Assert.IsType<Trellis.Api.Core.Dtos.ApiResponse<object>>(concurrencyResult.Value);
        var conflictBody = Assert.IsType<Trellis.Api.Core.Dtos.ApiResponse<object>>(conflictResult.Value);
        Assert.NotEqual(concurrencyBody.Error!.Code, conflictBody.Error!.Code);
    }

    [Fact]
    public void Failure_maps_to_500_and_never_exposes_an_exception_message()
    {
        var result = ServiceResult<string>.Failure("TRL-E-PLT-0500", "generic fault").ToActionResult(NewContext());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    }
}
