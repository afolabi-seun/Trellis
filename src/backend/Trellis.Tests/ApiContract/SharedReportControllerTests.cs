using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Xunit;

namespace Trellis.Tests.ApiContract;

/// <summary>
/// BDR-SEC-003's public share-link route — [AllowAnonymous], reachable with no Authorization
/// header at all. Every failure mode (bad/expired/revoked token) maps to 404, never 403 — no
/// existence disclosure.
/// </summary>
public sealed class SharedReportControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SharedReportControllerTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetShared_WithNoAuthorizationHeader_IsReachable_AndReturns404ForAnUnknownToken()
    {
        var shareLinkService = Substitute.For<IReportShareLinkService>();
        shareLinkService.ResolveAsync(Arg.Any<string>()).Returns(ServiceResult<(byte[], DateOnly)>.NotFound());

        await using var scopedFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped(_ => shareLinkService)));
        var client = scopedFactory.CreateClient();

        var response = await client.GetAsync("/reports/shared/bogus-token");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetShared_WhenResolveSucceeds_ReturnsPdfBytesWithCorrectContentType()
    {
        byte[] pdfBytes = [0x25, 0x50, 0x44, 0x46];
        var shareLinkService = Substitute.For<IReportShareLinkService>();
        shareLinkService.ResolveAsync(Arg.Any<string>()).Returns(
            ServiceResult<(byte[] PdfContent, DateOnly PeriodStart)>.Success((pdfBytes, new DateOnly(2026, 1, 31))));

        await using var scopedFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped(_ => shareLinkService)));
        var client = scopedFactory.CreateClient();

        var response = await client.GetAsync("/reports/shared/valid-token");
        var body = await response.Content.ReadAsByteArrayAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        body.Should().Equal(pdfBytes);
    }
}
