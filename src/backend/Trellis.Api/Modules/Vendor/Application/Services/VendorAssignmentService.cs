using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Inspection.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Trellis.Api.Modules.Vendor.Application.Dtos;
using Trellis.Api.Modules.Vendor.Application.Interfaces;
using Trellis.Api.Modules.Vendor.Domain.Entities;
using Trellis.Api.Modules.Vendor.Domain.Exceptions;
using Trellis.Api.Modules.Workflow.Application.Dtos;
using Trellis.Api.Modules.Workflow.Application.Interfaces;

namespace Trellis.Api.Modules.Vendor.Application.Services;

public sealed class VendorAssignmentService(
    IVendorAssignmentRepository repository,
    ITenantVendorRelationshipRepository relationshipRepository,
    IWorkflowInstanceService workflowInstanceService,
    IInspectionService inspectionService,
    ITenantService tenantService,
    IAuditService auditService)
    : IVendorAssignmentService
{
    public async Task<ServiceResult<VendorAssignmentResponse>> CreateAsync(VendorAssignmentCreateRequest request, Guid createdBy)
    {
        var tenantId = TenantContext.Current!.TenantId;

        var relationship = await relationshipRepository.GetByTenantAndVendorAsync(tenantId, request.VendorId);
        if (relationship is null || relationship.Status != TenantVendorRelationshipStatus.Active)
        {
            return ServiceResult<VendorAssignmentResponse>.BusinessRuleViolation(ErrorCodeRegistry.Vendor.NoActiveVendorRelationship);
        }

        if (request.WorkItemType == WorkItemTypes.WorkflowInstance)
        {
            var workItem = await workflowInstanceService.GetByIdAsync(request.WorkItemId);
            if (workItem.Outcome != ServiceResultOutcome.Success)
            {
                return ServiceResult<VendorAssignmentResponse>.NotFound();
            }
        }
        else if (request.WorkItemType == WorkItemTypes.Inspection)
        {
            var workItem = await inspectionService.GetByIdAsync(request.WorkItemId);
            if (workItem.Outcome != ServiceResultOutcome.Success)
            {
                return ServiceResult<VendorAssignmentResponse>.NotFound();
            }

            // BDR-INSP-001's assignment is mutually exclusive: an inspection already assigned to
            // an internal staff member at schedule time never also gets a Vendor assignment.
            if (workItem.Data!.AssignedToUserId is not null)
            {
                return ServiceResult<VendorAssignmentResponse>.BusinessRuleViolation(ErrorCodeRegistry.Inspection.AlreadyStaffAssigned);
            }
        }

        var assignment = VendorAssignment.Create(request.VendorId, tenantId, request.WorkItemType, request.WorkItemId);
        await repository.AddAsync(assignment);
        await repository.SaveChangesAsync();

        await auditService.RecordAsync(
            tenantId, createdBy, AuditActorType.HumanUser, AuditTypes.Vendor.VendorAssigned,
            $"{{\"assignmentId\":\"{assignment.AssignmentId}\",\"vendorId\":\"{assignment.VendorId}\",\"workItemType\":\"{assignment.WorkItemType}\",\"workItemId\":\"{assignment.WorkItemId}\"}}");

        return ServiceResult<VendorAssignmentResponse>.Success(ToResponse(assignment));
    }

    public async Task<ServiceResult<VendorAssignmentResponse>> GetByIdAsync(Guid assignmentId)
    {
        var assignment = await repository.GetByIdAsync(assignmentId);
        return assignment is null
            ? ServiceResult<VendorAssignmentResponse>.NotFound()
            : ServiceResult<VendorAssignmentResponse>.Success(ToResponse(assignment));
    }

    public async Task<ServiceResult<PagedResponse<VendorAssignmentResponse>>> ListForTenantAsync(string? cursor, int limit)
    {
        var decoded = CursorCodec.Decode(cursor);
        var (items, hasMore, totalCount) = await repository.ListForTenantAsync(decoded, limit);

        var nextCursor = hasMore && items.Count > 0
            ? CursorCodec.Encode(items[^1].AssignedAt, items[^1].AssignmentId)
            : null;

        return ServiceResult<PagedResponse<VendorAssignmentResponse>>.Success(
            new PagedResponse<VendorAssignmentResponse>(items.Select(ToResponse).ToList(), nextCursor, hasMore, totalCount));
    }

    public async Task<ServiceResult<PagedResponse<VendorAssignmentVendorView>>> ListForVendorAsync(Guid vendorId, string? cursor, int limit)
    {
        var decoded = CursorCodec.Decode(cursor);
        var (items, hasMore, totalCount) = await repository.ListForVendorAsync(vendorId, decoded, limit);

        var nextCursor = hasMore && items.Count > 0
            ? CursorCodec.Encode(items[^1].AssignedAt, items[^1].AssignmentId)
            : null;

        // Sequential, not Task.WhenAll — ToVendorViewAsync calls into ITenantService, which
        // shares this request's single scoped DbContext instance; running those concurrently
        // throws ("A second operation was started on this context instance before a previous
        // operation completed"), found live once a vendor had more than one assignment to list.
        var views = new List<VendorAssignmentVendorView>();
        foreach (var item in items)
        {
            views.Add(await ToVendorViewAsync(item));
        }

        return ServiceResult<PagedResponse<VendorAssignmentVendorView>>.Success(
            new PagedResponse<VendorAssignmentVendorView>(views, nextCursor, hasMore, totalCount));
    }

    public async Task<ServiceResult<VendorAssignmentDetailView>> GetForVendorAsync(Guid vendorId, string workItemType, Guid workItemId)
    {
        var assignment = await repository.GetByVendorAndWorkItemAsync(vendorId, workItemType, workItemId);
        if (assignment is null)
        {
            return ServiceResult<VendorAssignmentDetailView>.NotFound();
        }

        var tenant = await tenantService.GetByIdAsync(assignment.TenantId);
        var tenantName = tenant.Outcome == ServiceResultOutcome.Success ? tenant.Data!.TenantName : "Unknown tenant";

        VendorWorkflowInstanceView? workItem = null;
        if (workItemType == WorkItemTypes.WorkflowInstance)
        {
            var workItemResult = await workflowInstanceService.GetForVendorAsync(vendorId, workItemId);
            workItem = workItemResult.Outcome == ServiceResultOutcome.Success ? workItemResult.Data : null;
        }

        return ServiceResult<VendorAssignmentDetailView>.Success(new VendorAssignmentDetailView(ToResponse(assignment), tenantName, workItem));
    }

    public async Task<ServiceResult<VendorAssignmentResponse>> StartAsync(Guid vendorId, string workItemType, Guid workItemId)
    {
        var assignment = await repository.GetByVendorAndWorkItemAsync(vendorId, workItemType, workItemId);
        if (assignment is null)
        {
            return ServiceResult<VendorAssignmentResponse>.NotFound();
        }

        try
        {
            assignment.MarkInProgress();
            await repository.SaveChangesAsync();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<VendorAssignmentResponse>.BusinessRuleViolation(ErrorCodeRegistry.Vendor.InvalidStatusTransition);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<VendorAssignmentResponse>.ConcurrencyConflict();
        }

        await auditService.RecordAsync(
            assignment.TenantId, vendorId, AuditActorType.VendorUser, AuditTypes.Vendor.VendorStatusUpdated,
            $"{{\"assignmentId\":\"{assignment.AssignmentId}\",\"status\":\"IN_PROGRESS\"}}");

        return ServiceResult<VendorAssignmentResponse>.Success(ToResponse(assignment));
    }

    public async Task<ServiceResult<VendorAssignmentResponse>> CompleteAsync(Guid vendorId, string workItemType, Guid workItemId)
    {
        var assignment = await repository.GetByVendorAndWorkItemAsync(vendorId, workItemType, workItemId);
        if (assignment is null)
        {
            return ServiceResult<VendorAssignmentResponse>.NotFound();
        }

        try
        {
            assignment.Complete();
            await repository.SaveChangesAsync();
        }
        catch (InvalidStatusTransitionException)
        {
            return ServiceResult<VendorAssignmentResponse>.BusinessRuleViolation(ErrorCodeRegistry.Vendor.InvalidStatusTransition);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<VendorAssignmentResponse>.ConcurrencyConflict();
        }

        await auditService.RecordAsync(
            assignment.TenantId, vendorId, AuditActorType.VendorUser, AuditTypes.Vendor.VendorStatusUpdated,
            $"{{\"assignmentId\":\"{assignment.AssignmentId}\",\"status\":\"CLOSED\"}}");

        return ServiceResult<VendorAssignmentResponse>.Success(ToResponse(assignment));
    }

    private async Task<VendorAssignmentVendorView> ToVendorViewAsync(VendorAssignment assignment)
    {
        var tenant = await tenantService.GetByIdAsync(assignment.TenantId);
        var tenantName = tenant.Outcome == ServiceResultOutcome.Success ? tenant.Data!.TenantName : "Unknown tenant";
        return new VendorAssignmentVendorView(ToResponse(assignment), tenantName);
    }

    private static VendorAssignmentResponse ToResponse(VendorAssignment assignment) => new(
        assignment.AssignmentId,
        assignment.VendorId,
        assignment.TenantId,
        assignment.WorkItemType,
        assignment.WorkItemId,
        assignment.Status.ToUpperSnakeCase(),
        assignment.AssignedAt,
        assignment.ClosedAt);
}
