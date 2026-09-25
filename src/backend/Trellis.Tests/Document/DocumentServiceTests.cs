using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Trellis.Api.Core.Audit;
using Trellis.Api.Core.Files;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Document.Application.Dtos;
using Trellis.Api.Modules.Document.Application.Interfaces;
using Trellis.Api.Modules.Document.Application.Services;
using Trellis.Api.Modules.Document.Application.Validators;
using Trellis.Api.Modules.Document.Domain.Entities;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.Workflow.Application.Interfaces;
using Xunit;

namespace Trellis.Tests.Document;

public sealed class DocumentServiceTests
{
    private readonly IDocumentRepository _repository = Substitute.For<IDocumentRepository>();
    private readonly IDocumentVersionRepository _versionRepository = Substitute.For<IDocumentVersionRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();
    private readonly IFileScanner _fileScanner = Substitute.For<IFileScanner>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly IPropertyService _propertyService = Substitute.For<IPropertyService>();
    private readonly IUnitAssetService _unitAssetService = Substitute.For<IUnitAssetService>();
    private readonly IOwnerService _ownerService = Substitute.For<IOwnerService>();
    private readonly IWorkflowInstanceService _workflowInstanceService = Substitute.For<IWorkflowInstanceService>();

    public DocumentServiceTests() => TenantContext.Current = new TenantContext(Guid.NewGuid());

    private DocumentService CreateSut() => new(
        _repository, _versionRepository, new DocumentUploadRequestValidator(), _fileStorage, _fileScanner,
        _auditService, _propertyService, _unitAssetService, _ownerService, _workflowInstanceService);

    [Fact]
    public async Task CreateAsync_WhenMoreThanOneAttachmentTargetSet_ReturnsBusinessRuleViolation()
    {
        var request = new DocumentUploadRequest("LEASE", null, Guid.NewGuid(), Guid.NewGuid(), null, null);
        using var content = new MemoryStream([0x25, 0x50, 0x44, 0x46]);

        var sut = CreateSut();
        var result = await sut.CreateAsync(request, content, "lease.pdf", "application/pdf", content.Length, Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
        await _fileStorage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>());
    }

    [Fact]
    public async Task CreateAsync_WhenFileTypeNotAllowed_ReturnsBusinessRuleViolation()
    {
        var request = new DocumentUploadRequest("LEASE", null, null, null, null, null);
        // An .exe renamed to .pdf — magic number sniffing catches what the extension can't.
        using var content = new MemoryStream([0x4D, 0x5A, 0x90, 0x00]);

        var sut = CreateSut();
        var result = await sut.CreateAsync(request, content, "lease.pdf", "application/pdf", content.Length, Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
        await _fileStorage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>());
    }

    [Fact]
    public async Task CreateAsync_WhenDocumentTypeInvalid_ReturnsValidationFailed()
    {
        var request = new DocumentUploadRequest("NOT_A_REAL_TYPE", null, null, null, null, null);
        using var content = new MemoryStream([0x25, 0x50, 0x44, 0x46]);

        var sut = CreateSut();
        var result = await sut.CreateAsync(request, content, "lease.pdf", "application/pdf", content.Length, Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.ValidationFailed);
    }

    [Fact]
    public async Task UpdateAsync_WhenSaveThrowsConcurrencyException_ReturnsConcurrencyConflict()
    {
        var document = DocumentRecord.Create(TenantContext.Current!.TenantId, null, null, null, null, DocumentType.Lease, null);
        _repository.GetByIdAsync(document.DocumentId).Returns(document);
        _repository.SaveChangesAsync().Returns(Task.FromException(new DbUpdateConcurrencyException()));

        var sut = CreateSut();
        var result = await sut.UpdateAsync(document.DocumentId, new DocumentUpdateRequest("INVOICE", null, "stale-version"));

        result.Outcome.Should().Be(ServiceResultOutcome.ConcurrencyConflict);
    }

    [Fact]
    public async Task DownloadAsync_WhenScanNotCleanAndCallerIsNotUploader_ReturnsBusinessRuleViolation()
    {
        var uploaderId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var document = DocumentRecord.Create(TenantContext.Current!.TenantId, null, null, null, null, DocumentType.Lease, null);
        var version = DocumentVersion.Create(
            TenantContext.Current!.TenantId, document.DocumentId, 1, "lease.pdf", "application/pdf", 100, "key", uploaderId);
        document.SetCurrentVersion(version.DocumentVersionId);

        _repository.GetByIdAsync(document.DocumentId).Returns(document);
        _versionRepository.GetByIdAsync(version.DocumentVersionId).Returns(version);

        var sut = CreateSut();
        var result = await sut.DownloadAsync(document.DocumentId, otherUserId);

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
    }

    [Fact]
    public async Task DownloadAsync_WhenScanNotCleanButCallerIsUploader_Succeeds()
    {
        var uploaderId = Guid.NewGuid();
        var document = DocumentRecord.Create(TenantContext.Current!.TenantId, null, null, null, null, DocumentType.Lease, null);
        var version = DocumentVersion.Create(
            TenantContext.Current!.TenantId, document.DocumentId, 1, "lease.pdf", "application/pdf", 100, "key", uploaderId);
        document.SetCurrentVersion(version.DocumentVersionId);

        _repository.GetByIdAsync(document.DocumentId).Returns(document);
        _versionRepository.GetByIdAsync(version.DocumentVersionId).Returns(version);
        _fileStorage.OpenReadAsync("key").Returns(Task.FromResult<Stream>(new MemoryStream()));

        var sut = CreateSut();
        var result = await sut.DownloadAsync(document.DocumentId, uploaderId);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
    }
}
