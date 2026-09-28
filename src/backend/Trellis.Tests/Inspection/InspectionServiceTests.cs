using FluentAssertions;
using NSubstitute;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Files;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Inspection.Application.Dtos;
using Trellis.Api.Modules.Inspection.Application.Interfaces;
using Trellis.Api.Modules.Inspection.Application.Services;
using Trellis.Api.Modules.Inspection.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Xunit;

namespace Trellis.Tests.Inspection;

public sealed class InspectionServiceTests
{
    private readonly IInspectionRepository _repository = Substitute.For<IInspectionRepository>();
    private readonly IInspectionItemRepository _itemRepository = Substitute.For<IInspectionItemRepository>();
    private readonly IInspectionPhotoRepository _photoRepository = Substitute.For<IInspectionPhotoRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();
    private readonly IFileScanner _fileScanner = Substitute.For<IFileScanner>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly IPropertyService _propertyService = Substitute.For<IPropertyService>();
    private readonly IUnitAssetService _unitAssetService = Substitute.For<IUnitAssetService>();
    private readonly IVendorAssignmentReader _vendorAssignmentReader = Substitute.For<IVendorAssignmentReader>();

    public InspectionServiceTests() => TenantContext.Current = new TenantContext(Guid.NewGuid());

    private InspectionService CreateSut() => new(
        _repository, _itemRepository, _photoRepository, _fileStorage, _fileScanner, _auditService,
        _propertyService, _unitAssetService, _vendorAssignmentReader);

    [Fact]
    public async Task ScheduleAsync_WhenPropertyDoesNotExist_ReturnsNotFound()
    {
        var propertyId = Guid.NewGuid();
        _propertyService.GetByIdAsync(propertyId).Returns(ServiceResult<PropertyResponse>.NotFound());
        var request = new InspectionCreateRequest(propertyId, null, null, DateTimeOffset.UtcNow);

        var sut = CreateSut();
        var result = await sut.ScheduleAsync(request, Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    [Fact]
    public async Task ScheduleAsync_PopulatesFiveBaselineChecklistItems()
    {
        var request = new InspectionCreateRequest(Guid.NewGuid(), null, null, DateTimeOffset.UtcNow);
        _propertyService.GetByIdAsync(request.PropertyId!.Value)
            .Returns(ServiceResult<PropertyResponse>.Success(new PropertyResponse(
                request.PropertyId.Value, Guid.NewGuid(), "123 Main St", "SINGLE_FAMILY", "ACTIVE",
                null, null, null, null, "AAAAAAAAAAA=", DateTimeOffset.UtcNow)));

        var sut = CreateSut();
        var result = await sut.ScheduleAsync(request, Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        await _itemRepository.Received(5).AddAsync(Arg.Any<InspectionItem>());
    }

    [Fact]
    public async Task GetForVendorAsync_WhenNotAssignedToThisVendor_ReturnsNotFoundWithoutQuerying()
    {
        var vendorId = Guid.NewGuid();
        var inspectionId = Guid.NewGuid();
        _vendorAssignmentReader.IsAssignedAsync(vendorId, WorkItemTypes.Inspection, inspectionId).Returns(false);

        var sut = CreateSut();
        var result = await sut.GetForVendorAsync(vendorId, inspectionId);

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
        await _repository.DidNotReceive().GetForVendorAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task CompleteFromVendorAsync_WhenNotAssignedToThisVendor_ReturnsNotFoundWithoutTouchingData()
    {
        var vendorId = Guid.NewGuid();
        var inspectionId = Guid.NewGuid();
        _vendorAssignmentReader.IsAssignedAsync(vendorId, WorkItemTypes.Inspection, inspectionId).Returns(false);
        var request = new InspectionCompleteRequest([], null, []);

        var sut = CreateSut();
        var result = await sut.CompleteFromVendorAsync(vendorId, inspectionId, request, Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
        await _itemRepository.DidNotReceive().SaveChangesAsync();
        await _photoRepository.DidNotReceive().AddAsync(Arg.Any<InspectionPhoto>());
    }

    [Fact]
    public async Task DownloadPhotoAsync_WhenNotCleanAndCallerIsNotUploader_ReturnsBusinessRuleViolation()
    {
        var uploaderId = Guid.NewGuid();
        var photo = InspectionPhoto.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "a.jpg", "image/jpeg", 10, "key", null, null, DateTimeOffset.UtcNow, uploaderId);
        _photoRepository.GetByIdAsync(photo.InspectionPhotoId).Returns(photo);

        var sut = CreateSut();
        var result = await sut.DownloadPhotoAsync(photo.InspectionPhotoId, Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
    }

    [Fact]
    public async Task DownloadPhotoAsync_WhenNotCleanButCallerIsUploader_Succeeds()
    {
        var uploaderId = Guid.NewGuid();
        var photo = InspectionPhoto.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "a.jpg", "image/jpeg", 10, "key", null, null, DateTimeOffset.UtcNow, uploaderId);
        _photoRepository.GetByIdAsync(photo.InspectionPhotoId).Returns(photo);
        _fileStorage.OpenReadAsync("key").Returns(Stream.Null);

        var sut = CreateSut();
        var result = await sut.DownloadPhotoAsync(photo.InspectionPhotoId, uploaderId);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
    }
}
