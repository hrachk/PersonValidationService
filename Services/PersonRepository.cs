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

    /// <summary>
    /// Fetches the identity fields (FirstName/LastName/BirthDate) we have
    /// on file for a PersonId, to compare against what BPR returns.
    /// </summary>
    public async Task<(string? FirstName, string? LastName, DateTime? BirthDate)?> GetPersonIdentityAsync(
        long personId,
        CancellationToken ct)
    {
        await using var db =
            await _contextFactory.CreateDbContextAsync(ct);

        var person = await db.Persons
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PersonId == personId, ct);

        if (person == null)
            return null;

        return (person.FirstName, person.LastName, person.BirthDate);
    }

    /// <summary>
    /// Finds every PersonId connected to <paramref name="personId"/> via a
    /// shared PassportNum (transitively — A↔B via doc X, B↔C via doc Y means
    /// A, B, C are all the same real human). A shared passport number is the
    /// only deterministic signal available in this schema; intentionally NOT
    /// using fuzzy name/DOB matching here, since a false-positive merge would
    /// attach one person's SSN/SocialCard to a different, unrelated person —
    /// a far worse outcome than leaving two records unmerged.
    /// </summary>
    public async Task<List<long>> GetLinkedPersonIdsAsync(
        long personId,
        CancellationToken ct)
    {
        await using var db =
            await _contextFactory.CreateDbContextAsync(ct);

        var group = new HashSet<long> { personId };
        var frontier = new Queue<long>();
        frontier.Enqueue(personId);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();

            var passportNums = await db.Passports
                .AsNoTracking()
                .Where(p => p.PersonId == current)
                .Where(p => !string.IsNullOrWhiteSpace(p.PassportNum))
                .Select(p => p.PassportNum!)
                .ToListAsync(ct);

            if (passportNums.Count == 0)
                continue;

            var linkedIds = await db.Passports
                .AsNoTracking()
                .Where(p => p.PersonId != null && passportNums.Contains(p.PassportNum!))
                .Select(p => p.PersonId!.Value)
                .Distinct()
                .ToListAsync(ct);

            foreach (var id in linkedIds)
            {
                if (group.Add(id))
                    frontier.Enqueue(id);
            }
        }

        return group.OrderBy(x => x).ToList();
    }

    /// <summary>
    /// Union of distinct passport numbers across an entire linked-PersonId
    /// group, so a person's documents split across several internal
    /// PersonId rows are compared against BPR as one whole, instead of each
    /// PersonId seeing only its own slice and falsely MISMATCH-ing on a
    /// document that actually belongs to the same human's other record.
    /// </summary>
    public async Task<List<string>> GetPassportsForGroupAsync(
        IReadOnlyCollection<long> personIds,
        CancellationToken ct)
    {
        await using var db =
            await _contextFactory.CreateDbContextAsync(ct);

        return await db.Passports
            .AsNoTracking()
            .Where(x => x.PersonId != null && personIds.Contains(x.PersonId.Value))
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

    /// <summary>
    /// Writes the resolved SocialCard to every PersonId in a linked group,
    /// so the same real human doesn't end up with one DB record enriched
    /// and another (same person, different PersonId) left blank.
    /// </summary>
    public async Task UpdateSocialCardForGroupAsync(
        IReadOnlyCollection<long> personIds,
        string socialCard,
        CancellationToken ct)
    {
        await using var db =
            await _contextFactory.CreateDbContextAsync(ct);

        var persons = await db.Persons
            .Where(x => personIds.Contains(x.PersonId))
            .ToListAsync(ct);

        foreach (var person in persons)
            person.SocialCard = socialCard;

        await db.SaveChangesAsync(ct);
    }
}