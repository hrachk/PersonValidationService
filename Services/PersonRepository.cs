using Microsoft.EntityFrameworkCore;
using PersonValidationService.Data;

namespace PersonValidationService.Services;

public sealed class PersonRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public PersonRepository(
        IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<string>> GetPassportsAsync(
        long personId,
        CancellationToken ct)
    {
        await using var db =
            await _contextFactory.CreateDbContextAsync(ct);

        return await db.Passports
            .AsNoTracking()
            .Where(x => x.PersonId == personId)
            .Where(x => !string.IsNullOrWhiteSpace(x.PassportNum))
            .Select(x => x.PassportNum!)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task UpdateSocialCardAsync(
     long personId,
     string socialCard,
     CancellationToken ct)
    {
        await using var db =
            await _contextFactory.CreateDbContextAsync(ct);

        var person = await db.Persons
            .FirstOrDefaultAsync(
                x => x.PersonId == personId,
                ct);

        if (person == null)
            return;

        person.SocialCard = socialCard;

        await db.SaveChangesAsync(ct);
    }
}