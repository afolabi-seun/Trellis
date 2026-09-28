using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Inspection.Application.Dtos;
using Trellis.Api.Modules.Inspection.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Trellis.Api.Modules.Vendor.Application.Dtos;
using Trellis.Api.Modules.Vendor.Application.Interfaces;
using Trellis.Api.Modules.Vendor.Application.Services;
using Trellis.Api.Modules.Vendor.Domain.Entities;
using Trellis.Api.Modules.Workflow.Application.Interfaces;
using Xunit;

namespace Trellis.Tests.Vendor;

public sealed class VendorAssignmentServiceTests
{
    private readonly IVendorAssignmentRepository _repository = Substitute.For<IVendorAssignmentRepository>();
    private readonly ITenantVendorRelationshipRepository _relationshipRepository = Substitute.For<ITenantVendorRelationshipRepository>();
    private readonly IWorkflowInstanceService _workflowInstanceService = Substitute.For<IWorkflowInstanceService>();
    private readonly IInspectionService _inspectionService = Substitute.For<IInspectionService>();
    private readonly ITenantService _tenantService = Substitute.For<ITenantService>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    public VendorAssignmentServiceTests() => TenantContext.Current = new TenantContext(Guid.NewGuid());

    private VendorAssignmentService CreateSut() =>
        new(_repository, _relationshipRepository, _workflowInstanceService, _inspectionService, _tenantService, _auditService);

    [Fact]
    public async Task CreateAsync_WhenNoActiveRelationshipExists_ReturnsBusinessRuleViolation()
    {
        var request = new VendorAssignmentCreateRequest(Guid.NewGuid(), WorkItemTypes.WorkflowInstance, Guid.NewGuid());
        _relationshipRepository.GetByTenantAndVendorAsync(TenantContext.Current!.TenantId, request.VendorId).Returns((TenantVendorRelationship?)null);

        var sut = CreateSut();
        var result = await sut.CreateAsync(request, Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
        await _repository.DidNotReceive().AddAsync(Arg.Any<VendorAssignment>());
    }

    [Fact]
    public async Task CreateAsync_WhenRelationshipIsTerminated_ReturnsBusinessRuleViolation()
    {
        var request = new VendorAssignmentCreateRequest(Guid.NewGuid(), WorkItemTypes.WorkflowInstance, Guid.NewGuid());
        var relationship = TenantVendorRelationship.Create(TenantContext.Current!.TenantId, request.VendorId);
        relationship.Terminate(Guid.NewGuid());
        _relationshipRepository.GetByTenantAndVendorAsync(TenantContext.Current!.TenantId, request.VendorId).Returns(relationship);

        var sut = CreateSut();
        var result = await sut.CreateAsync(request, Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
    }

    [Fact]
    public async Task CompleteAsync_WhenSaveThrowsConcurrencyException_ReturnsConcurrencyConflict()
    {
        var vendorId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();
        var assignment = VendorAssignment.Create(vendorId, Guid.NewGuid(), WorkItemTypes.WorkflowInstance, workItemId);
        _repository.GetByVendorAndWorkItemAsync(vendorId, WorkItemTypes.WorkflowInstance, workItemId).Returns(assignment);
        _repository.SaveChangesAsync().Returns(Task.FromException(new DbUpdateConcurrencyException()));

        var sut = CreateSut();
        var result = await sut.CompleteAsync(vendorId, WorkItemTypes.WorkflowInstance, workItemId);

        result.Outcome.Should().Be(ServiceResultOutcome.ConcurrencyConflict);
    }

    [Fact]
    public async Task StartAsync_WhenNotAssignedToThisVendor_ReturnsNotFound()
    {
        var vendorId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();
        _repository.GetByVendorAndWorkItemAsync(vendorId, WorkItemTypes.WorkflowInstance, workItemId).Returns((VendorAssignment?)null);

        var sut = CreateSut();
        var result = await sut.StartAsync(vendorId, WorkItemTypes.WorkflowInstance, workItemId);

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }

    [Fact]
    public async Task CreateAsync_ForAnInspectionAlreadyAssignedToStaff_ReturnsBusinessRuleViolation()
    {
        var request = new VendorAssignmentCreateRequest(Guid.NewGuid(), WorkItemTypes.Inspection, Guid.NewGuid());
        var relationship = TenantVendorRelationship.Create(TenantContext.Current!.TenantId, request.VendorId);
        _relationshipRepository.GetByTenantAndVendorAsync(TenantContext.Current!.TenantId, request.VendorId).Returns(relationship);

        var inspectionResponse = new InspectionReportResponse(
            request.WorkItemId, null, Guid.NewGuid(), Guid.NewGuid(), "SCHEDULED", DateTimeOffset.UtcNow, null, null,
            null, null, [], [], "AAAAAAAAAAA=", DateTimeOffset.UtcNow);
        _inspectionService.GetByIdAsync(request.WorkItemId).Returns(ServiceResult<InspectionReportResponse>.Success(inspectionResponse));

        var sut = CreateSut();
        var result = await sut.CreateAsync(request, Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
        await _repository.DidNotReceive().AddAsync(Arg.Any<VendorAssignment>());
    }
}
