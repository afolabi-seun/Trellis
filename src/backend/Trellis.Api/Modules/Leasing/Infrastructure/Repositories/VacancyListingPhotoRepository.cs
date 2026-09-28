using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Domain.Entities;
using Trellis.Api.Modules.Leasing.Infrastructure.Data;

namespace Trellis.Api.Modules.Leasing.Infrastructure.Repositories;

public sealed class VacancyListingPhotoRepository(LeasingDbContext dbContext) : TenantAwareRepository<VacancyListingPhoto>, IVacancyListingPhotoRepository
{
    public async Task AddAsync(VacancyListingPhoto photo) => await dbContext.VacancyListingPhotos.AddAsync(photo);

    public Task<VacancyListingPhoto?> GetByIdAsync(Guid vacancyListingPhotoId) =>
        Scoped(dbContext.VacancyListingPhotos).FirstOrDefaultAsync(p => p.VacancyListingPhotoId == vacancyListingPhotoId);

    public async Task<IReadOnlyList<VacancyListingPhoto>> ListByListingAsync(Guid vacancyListingId) =>
        await Scoped(dbContext.VacancyListingPhotos)
            .Where(p => p.VacancyListingId == vacancyListingId)
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.CreatedAt)
            .ToListAsync();

    public async Task<IReadOnlyList<VacancyListingPhoto>> ListPublicByListingAsync(Guid tenantId, Guid vacancyListingId) =>
        await dbContext.VacancyListingPhotos
            .Where(p => p.TenantId == tenantId && p.VacancyListingId == vacancyListingId
                && p.ScanStatus == VacancyListingPhotoScanStatus.Clean)
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.CreatedAt)
            .ToListAsync();

    public Task<VacancyListingPhoto?> GetPublicByIdAsync(Guid tenantId, Guid vacancyListingPhotoId) =>
        dbContext.VacancyListingPhotos.FirstOrDefaultAsync(p =>
            p.TenantId == tenantId && p.VacancyListingPhotoId == vacancyListingPhotoId
            && p.ScanStatus == VacancyListingPhotoScanStatus.Clean);

    public Task<int> CountByListingAsync(Guid vacancyListingId) =>
        Scoped(dbContext.VacancyListingPhotos).CountAsync(p => p.VacancyListingId == vacancyListingId);

    public Task SaveChangesAsync() => dbContext.SaveChangesAsync();
}
