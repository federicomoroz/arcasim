using ArcaSim.Domain;

namespace ArcaSim.Tests;

/// <summary>The CUIT a DNI gets when nobody loaded the person: a man's, the way ARCA assigns it.</summary>
public class CuitsTests
{
    [Theory]
    [InlineData(22222222, 20222222223)]
    [InlineData(1, 23000000019)]
    public void A_DNI_gets_prefix_20_or_23_when_20_needs_check_digit_10(long document, long cuit)
    {
        Assert.Equal(cuit, Cuits.ForDocument(document));
        Assert.True(Cuits.IsValid(cuit));
    }
}
