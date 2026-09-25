using FluentValidation;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Modules.Document.Application.Dtos;
using Trellis.Api.Modules.Document.Domain.Entities;

namespace Trellis.Api.Modules.Document.Application.Validators;

/// <summary>
/// DocumentUploadRequest reaches ValidationActionFilter only on the flat POST /api/v1/documents
/// route — nested routes (POST .../properties/{id}/documents etc.) construct it inside the
/// Controller from route + form fields, so it's never a bound action argument there.
/// DocumentService.CreateAsync calls this validator explicitly itself so both paths get the same
/// check, not just the flat one.
/// </summary>
public sealed class DocumentUploadRequestValidator : AbstractValidator<DocumentUploadRequest>
{
    public DocumentUploadRequestValidator()
    {
        RuleFor(x => x.DocumentType)
            .Must(v => EnumStringConverter.TryParseUpperSnakeCase<DocumentType>(v, out _))
            .WithMessage("documentType must be one of LEASE, INSURANCE_CERT, LICENCE, INVOICE, OTHER.");
    }
}

public sealed class DocumentUpdateRequestValidator : AbstractValidator<DocumentUpdateRequest>
{
    public DocumentUpdateRequestValidator()
    {
        RuleFor(x => x.DocumentType)
            .Must(v => EnumStringConverter.TryParseUpperSnakeCase<DocumentType>(v, out _))
            .WithMessage("documentType must be one of LEASE, INSURANCE_CERT, LICENCE, INVOICE, OTHER.");
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}
