using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Trellis.Api.Core.Files;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Application.Dtos;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Application.Services;
using Trellis.Api.Modules.Leasing.Configuration;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Xunit;

namespace Trellis.Tests.Leasing;

public sealed class VacancyListingServiceTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _unitId = Guid.NewGuid();
    private readonly IVacancyListingRepository _repository = Substitute.For<IVacancyListingRepository>();
    private readonly IVacancyListingPhotoRepository _photoRepository = Substitute.For<IVacancyListingPhotoRepository>();
    private readonly IUnitAssetService _unitAssetService = Substitute.For<IUnitAssetService>();
    private readonly IModuleEntitlementService _entitlements = Substitute.For<IModuleEntitlementService>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();
    private readonly IFileScanner _fileScanner = Substitute.For<IFileScanner>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    public VacancyListingServiceTests()
    {
        TenantContext.Current = new TenantContext(_tenantId);
        _repository.GetRowVersion(Arg.Any<VacancyListing>()).Returns("AAAAAAAAAAA=");
        _photoRepository.ListByListingAsync(Arg.Any<Guid>()).Returns([]);
        _unitAssetService.GetByIdAsync(_unitId).Returns(ServiceResult<UnitAssetResponse>.Success(
            new UnitAssetResponse(_unitId, Guid.NewGuid(), "Unit 4B", "ACTIVE", "VACANT", null, null, null, "AAAAAAAAAAA=", DateTimeOffset.UtcNow)));
    }

    public void Dispose() => TenantContext.Current = null;

    private VacancyListingService CreateSut() => new(
        _repository, _photoRepository, _unitAssetService, _entitlements, _fileStorage, _fileScanner, _auditService,
        Options.Create(new LeasingOptions { MaxPhotosPerListing = 2 }));

    [Fact]
    public async Task CreateAsync_ForAValidUnit_CreatesAListedListing_AndAudits()
    {
        var sut = CreateSut();

        var result = await sut.CreateAsync(new VacancyListingCreateRequest(_unitId, 1500m, "USD", new DateOnly(2026, 11, 1), "Nice"), Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.Status.Should().Be("LISTED");
        await _auditService.Received(1).RecordAsync(_tenantId, Arg.Any<Guid>(), Arg.Any<Trellis.Api.Modules.Reporting.Domain.Entities.AuditActorType>(), "VACANCY_LISTING_CREATED", Arg.Any<string>());
    }

    [Fact]
    public async Task CreateAsync_WhenTheUnitAlreadyHasAnActiveListing_ReturnsTheFriendlyBusinessRuleViolation()
    {
        _repository.HasActiveListingForUnitAsync(_unitId).Returns(true);
        var sut = CreateSut();

        var result = await sut.CreateAsync(new VacancyListingCreateRequest(_unitId, 1500m, "USD", new DateOnly(2026, 11, 1), null), Guid.NewGuid());

        result.ErrorCode.Should().Be("TRL-E-LSE-11001");
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task CreateAsync_ForAnUnknownUnit_ReturnsNotFound()
    {
        _unitAssetService.GetByIdAsync(Arg.Any<Guid>()).Returns(ServiceResult<UnitAssetResponse>.NotFound());
        var sut = CreateSut();

        var result = await sut.CreateAsync(new VacancyListingCreateRequest(Guid.NewGuid(), 1500m, "USD", new DateOnly(2026, 11, 1), null), Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    [Fact]
    public async Task CreateAsync_ForAnOccupiedUnit_IsRejected_AUnitWithALiveLeaseCannotBeRelisted()
    {
        _unitAssetService.GetByIdAsync(_unitId).Returns(ServiceResult<UnitAssetResponse>.Success(
            new UnitAssetResponse(_unitId, Guid.NewGuid(), "Unit 4B", "ACTIVE", "OCCUPIED", null, null, null, "AAAAAAAAAAA=", DateTimeOffset.UtcNow)));
        var sut = CreateSut();

        var result = await sut.CreateAsync(new VacancyListingCreateRequest(_unitId, 1500m, "USD", new DateOnly(2026, 11, 1), null), Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
        result.ErrorCode.Should().Be("TRL-E-LSE-11006");
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task BrowsePublicAsync_WhenTheTenantIsNotEntitledToLeasing_ReturnsNotFound_EvenIfListingsExist()
    {
        _entitlements.IsEntitledAsync(_tenantId, "LEASING").Returns(false);
        var sut = CreateSut();

        var result = await sut.BrowsePublicAsync(_tenantId, null, 25);

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
        await _repository.DidNotReceiveWithAnyArgs().ListPublicForTenantAsync(default, default, default);
    }

    [Fact]
    public async Task BrowsePublicAsync_WhenEntitled_ReturnsTheListingWithItsUnitLabel()
    {
        _entitlements.IsEntitledAsync(_tenantId, "LEASING").Returns(true);
        var listing = VacancyListing.Create(_tenantId, _unitId, 1500m, "USD", new DateOnly(2026, 11, 1), "Nice", Guid.NewGuid());
        _repository.ListPublicForTenantAsync(_tenantId, null, 25).Returns(((IReadOnlyList<VacancyListing>)[listing], false, 1));
        var sut = CreateSut();

        var result = await sut.BrowsePublicAsync(_tenantId, null, 25);

        result.Data!.Items.Should().ContainSingle().Which.UnitLabel.Should().Be("Unit 4B");
    }

    /// <summary>
    /// Regression for a bug the live smoke test found: TenantContext was set inside a nested async
    /// helper, and an AsyncLocal write inside an awaited callee doesn't flow back to its caller — so
    /// the entitlement check saw the tenant while the repository query that followed saw none, and
    /// RLS silently returned zero rows. Mocks alone can't notice that, so this asserts the ambient
    /// context AT THE MOMENT each downstream call is made.
    /// </summary>
    [Fact]
    public async Task PublicMethods_HaveTheRouteTenantSetAtTheMomentTheRepositoryIsQueried_NotJustDuringTheEntitlementCheck()
    {
        TenantContext.Current = null;
        _entitlements.IsEntitledAsync(_tenantId, "LEASING").Returns(true);
        Guid? seenByBrowse = null, seenByDetail = null, seenByPhoto = null;
        var listing = VacancyListing.Create(_tenantId, _unitId, 1500m, "USD", new DateOnly(2026, 11, 1), null, Guid.NewGuid());
        _repository.ListPublicForTenantAsync(_tenantId, null, 25).Returns(_ =>
        {
            seenByBrowse = TenantContext.Current?.TenantId;
            return ((IReadOnlyList<VacancyListing>)[listing], false, 1);
        });
        _repository.GetByIdForTenantAsync(_tenantId, listing.VacancyListingId).Returns(_ =>
        {
            seenByDetail = TenantContext.Current?.TenantId;
            return listing;
        });
        _photoRepository.GetPublicByIdAsync(_tenantId, Arg.Any<Guid>()).Returns(_ =>
        {
            seenByPhoto = TenantContext.Current?.TenantId;
            return (VacancyListingPhoto?)null;
        });
        var sut = CreateSut();

        await sut.BrowsePublicAsync(_tenantId, null, 25);
        await sut.GetPublicByIdAsync(_tenantId, listing.VacancyListingId);
        await sut.GetPublicPhotoAsync(_tenantId, Guid.NewGuid());

        seenByBrowse.Should().Be(_tenantId);
        seenByDetail.Should().Be(_tenantId);
        seenByPhoto.Should().Be(_tenantId);
    }

    [Fact]
    public async Task GetPublicByIdAsync_OnlyExposesCleanPhotoIds_AndNoInternalFields()
    {
        _entitlements.IsEntitledAsync(_tenantId, "LEASING").Returns(true);
        var listing = VacancyListing.Create(_tenantId, _unitId, 1500m, "USD", new DateOnly(2026, 11, 1), null, Guid.NewGuid());
        _repository.GetByIdForTenantAsync(_tenantId, listing.VacancyListingId).Returns(listing);
        var clean = VacancyListingPhoto.Create(_tenantId, listing.VacancyListingId, "a.jpg", "image/jpeg", 10, "k", 0, Guid.NewGuid());
        _photoRepository.ListPublicByListingAsync(_tenantId, listing.VacancyListingId).Returns([clean]);
        var sut = CreateSut();

        var result = await sut.GetPublicByIdAsync(_tenantId, listing.VacancyListingId);

        result.Data!.PhotoIds.Should().Equal(clean.VacancyListingPhotoId);
        typeof(PublicVacancyListingDetailResponse).GetProperties().Select(p => p.Name)
            .Should().NotContain(["TenantId", "UnitAssetId", "CreatedBy", "Status", "RowVersion"]);
    }

    [Fact]
    public async Task GetPublicByIdAsync_ForAWithdrawnListing_ReturnsNotFound()
    {
        _entitlements.IsEntitledAsync(_tenantId, "LEASING").Returns(true);
        var listing = VacancyListing.Create(_tenantId, _unitId, 1500m, "USD", new DateOnly(2026, 11, 1), null, Guid.NewGuid());
        listing.Withdraw();
        _repository.GetByIdForTenantAsync(_tenantId, listing.VacancyListingId).Returns(listing);
        var sut = CreateSut();

        var result = await sut.GetPublicByIdAsync(_tenantId, listing.VacancyListingId);

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    [Fact]
    public async Task UploadPhotoAsync_WhenTheListingIsAtTheCap_IsRejected()
    {
        var listing = VacancyListing.Create(_tenantId, _unitId, 1500m, "USD", new DateOnly(2026, 11, 1), null, Guid.NewGuid());
        _repository.GetByIdAsync(listing.VacancyListingId).Returns(listing);
        _photoRepository.CountByListingAsync(listing.VacancyListingId).Returns(2);
        var sut = CreateSut();

        var result = await sut.UploadPhotoAsync(listing.VacancyListingId, new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0]), "a.jpg", "image/jpeg", 4, Guid.NewGuid());

        result.ErrorCode.Should().Be("TRL-E-LSE-11004");
    }

    [Fact]
    public async Task UploadPhotoAsync_WithNonImageBytes_IsRejected_RegardlessOfTheDeclaredContentType()
    {
        var listing = VacancyListing.Create(_tenantId, _unitId, 1500m, "USD", new DateOnly(2026, 11, 1), null, Guid.NewGuid());
        _repository.GetByIdAsync(listing.VacancyListingId).Returns(listing);
        var sut = CreateSut();

        var result = await sut.UploadPhotoAsync(listing.VacancyListingId, new MemoryStream("%PDF-1.4"u8.ToArray()), "fake.jpg", "image/jpeg", 8, Guid.NewGuid());

        result.ErrorCode.Should().Be("TRL-E-LSE-11004");
        await _fileStorage.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!);
    }

    [Fact]
    public async Task UploadPhotoAsync_WithAJpeg_StoresScansAndMarksItClean()
    {
        var listing = VacancyListing.Create(_tenantId, _unitId, 1500m, "USD", new DateOnly(2026, 11, 1), null, Guid.NewGuid());
        _repository.GetByIdAsync(listing.VacancyListingId).Returns(listing);
        _fileStorage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>()).Returns("key-1");
        _fileStorage.OpenReadAsync("key-1").Returns(_ => Task.FromResult<Stream>(new MemoryStream([1])));
        _fileScanner.ScanAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>()).Returns(new ScanResult(ScanOutcome.Clean, null));
        var sut = CreateSut();

        var result = await sut.UploadPhotoAsync(listing.VacancyListingId, new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0]), "a.jpg", "image/jpeg", 4, Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.ScanStatus.Should().Be("CLEAN");
    }
}
