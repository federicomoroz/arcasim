namespace ArcaSim.Application.Contracts;

/// <summary>The pages of a list that a query answers one at a time (FCE, CEC, the remitos), whatever its page size.</summary>
public static class Paging
{
    /// <summary>
    /// The items of the 1-based page, and whether more follow it. A page before the first or past the last
    /// is empty and has no more. The page is a long and the offset is worked out only for a page that
    /// exists, so no page number, however large, can wrap into the middle of the list.
    /// </summary>
    public static (List<T> Items, bool More) Page<T>(IReadOnlyList<T> all, long page, int size)
    {
        if (page < 1 || page > Count(all.Count, size)) return ([], false);
        var skip = (int)((page - 1) * size);
        return (all.Skip(skip).Take(size).ToList(), all.Count > skip + size);
    }

    /// <summary>How many pages <paramref name="total"/> items fill.</summary>
    public static int Count(int total, int size) => (total + size - 1) / size;
}
