using ArcaSim.Application.Services.Fce;

namespace ArcaSim.Tests.Services.Fce;

public class FceTypesTests
{
    [Fact]
    public void The_list_of_FCE_types_is_every_type_IsFce_takes()
    {
        Assert.Equal(Enumerable.Range(0, 1000).Where(FceTypes.IsFce), FceTypes.All);
    }
}
