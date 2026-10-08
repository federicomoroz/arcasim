using ArcaSim.Application.Contracts;

namespace ArcaSim.Tests.Contract;

/// <summary>The page arithmetic FCE, CEC and the remitos answer their queries with.</summary>
public class PagingTests
{
    private static readonly List<int> Items = Enumerable.Range(1, 250).ToList();

    [Theory]
    [InlineData(1, 1, 100, true)]
    [InlineData(2, 101, 100, true)]
    [InlineData(3, 201, 50, false)]
    public void A_page_that_exists_holds_its_items_and_says_whether_more_follow(long page, int first, int count, bool more)
    {
        var (items, hasMore) = Paging.Page(Items, page, 100);

        Assert.Equal(Enumerable.Range(first, count), items);
        Assert.Equal(more, hasMore);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(4)]
    [InlineData(21_474_837)]
    [InlineData(long.MaxValue)]
    public void A_page_before_the_first_or_past_the_last_is_empty_with_no_more(long page)
    {
        var (items, more) = Paging.Page(Items, page, 100);

        Assert.Empty(items);
        Assert.False(more);
    }

    [Fact]
    public void A_full_last_page_has_no_more_and_an_empty_list_has_no_pages()
    {
        Assert.False(Paging.Page(Enumerable.Range(1, 200).ToList(), 2, 100).More);
        Assert.Empty(Paging.Page(new List<int>(), 1, 100).Items);
        Assert.Equal([0, 1, 1, 2], new[] { 0, 1, 100, 101 }.Select(total => Paging.Count(total, 100)));
    }
}
