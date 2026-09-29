using Trellis.Api.Core.Privacy;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;

namespace Trellis.Api.Modules.PropertyAsset.Application.Services;

/// <summary>An owner subject — matched by Email. The Owner row is never deleted (Property.OwnerId must keep resolving), only tombstoned in place, same as Archive()'s own "never a hard delete" posture.</summary>
public sealed class PropertyAssetPersonalDataHandler(IOwnerRepository ownerRepository) : IPersonalDataHandler
{
    public string ModuleName => "PropertyAsset";

    public async Task<object?> ExportAsync(string subjectEmail)
    {
        var owner = await ownerRepository.GetByEmailAsync(subjectEmail);
        return owner is null ? null : new { owner.OwnerId, owner.Name, owner.Email, owner.Phone, Status = owner.Status.ToString() };
    }

    public async Task<ErasureSummary> EraseAsync(string subjectEmail)
    {
        var owner = await ownerRepository.GetByEmailAsync(subjectEmail);
        if (owner is null)
        {
            return new ErasureSummary(ModuleName, 0);
        }

        owner.RedactPii();
        await ownerRepository.SaveChangesAsync();

        return new ErasureSummary(ModuleName, 1);
    }
}
