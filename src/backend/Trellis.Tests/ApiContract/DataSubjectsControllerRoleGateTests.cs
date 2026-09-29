using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Domain.Entities;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Xunit;

namespace Trellis.Tests.ApiContract;

/// <summary>security_guide.md's POPIA data-subject export/erasure — TENANT_ADMIN only.</summary>
public sealed class DataSubjectsControllerRoleGateTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DataSubjectsControllerRoleGateTests(CustomWebApplicationFactory factory) => _factory = factory;

    private HttpClient CreateClient(UserRole role, out IAsyncDisposable handle, IDataSubjectService? dataSubjectService = null)
    {
        var service = dataSubjectService ?? Substitute.For<IDataSubjectService>();
        if (dataSubjectService is null)
        {
            service.ExportAsync(Arg.Any<DataSubjectExportRequest>(), Arg.Any<Guid>()).Returns(
                ServiceResult<DataSubjectExportResponse>.Success(new DataSubjectExportResponse(new Dictionary<string, object>())));
            service.EraseAsync(Arg.Any<EraseDataSubjectRequest>(), Arg.Any<Guid>()).Returns(
                ServiceResult<DataSubjectErasureResponse>.Success(new DataSubjectErasureResponse([])));
        }

        var scoped = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped(_ => service)));
        handle = scoped;
        var client = scoped.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.IssueAccessTokenFor(Guid.NewGuid(), role));
        return client;
    }

    [Fact]
    public async Task Export_IsAllowedForATenantAdmin_WithANoStoreCacheHeader()
    {
        var client = CreateClient(UserRole.TenantAdmin, out var handle);
        await using var _ = handle;

        var response = await client.PostAsJsonAsync("/api/v1/data-subjects/export", new DataSubjectExportRequest("subject@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.DepartmentStaff)]
    [InlineData(UserRole.VendorUser)]
    [InlineData(UserRole.SuperAdmin)]
    public async Task Export_IsForbiddenToEveryOtherRole(UserRole role)
    {
        var client = CreateClient(role, out var handle);
        await using var _ = handle;

        var response = await client.PostAsJsonAsync("/api/v1/data-subjects/export", new DataSubjectExportRequest("subject@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Erase_IsAllowedForATenantAdmin()
    {
        var client = CreateClient(UserRole.TenantAdmin, out var handle);
        await using var _ = handle;

        var response = await client.PostAsJsonAsync(
            "/api/v1/data-subjects/erase",
            new EraseDataSubjectRequest("subject@example.com", "subject@example.com", "correct-password", "GDPR request"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Erase_IsForbiddenToDepartmentStaff()
    {
        var service = Substitute.For<IDataSubjectService>();
        var client = CreateClient(UserRole.DepartmentStaff, out var handle, service);
        await using var _ = handle;

        var response = await client.PostAsJsonAsync(
            "/api/v1/data-subjects/erase",
            new EraseDataSubjectRequest("subject@example.com", "subject@example.com", "correct-password", null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await service.DidNotReceiveWithAnyArgs().EraseAsync(default!, default);
    }

    [Fact]
    public async Task Erase_WithAMismatchedConfirmationEmail_Returns400_AndNeverCallsTheService()
    {
        var service = Substitute.For<IDataSubjectService>();
        var client = CreateClient(UserRole.TenantAdmin, out var handle, service);
        await using var _ = handle;

        var response = await client.PostAsJsonAsync(
            "/api/v1/data-subjects/erase",
            new EraseDataSubjectRequest("subject@example.com", "typo@example.com", "correct-password", null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await service.DidNotReceiveWithAnyArgs().EraseAsync(default!, default);
    }
}
