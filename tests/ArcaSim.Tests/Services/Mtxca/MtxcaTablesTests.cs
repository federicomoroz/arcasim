using ArcaSim.Application.Services.Mtxca;
using ArcaSim.Application.Wsfe;

namespace ArcaSim.Tests.Services.Mtxca;

/// <summary>wsmtxca's voucher types: the facts its rules turn on are read from WSFEv1's table and are the ones the manual lists.</summary>
public class MtxcaTablesTests
{
    private static readonly MtxcaTables Tables = new(ParameterTables.Load());

    [Fact]
    public void The_fifteen_voucher_types_are_the_ones_validation_100_accepts()
    {
        Assert.Equal([1, 2, 3, 6, 7, 8, 51, 52, 53, 201, 202, 203, 206, 207, 208], Tables.VoucherTypes.Select(t => t.Id));
        Assert.All(Tables.VoucherTypes, t => Assert.False(string.IsNullOrWhiteSpace(t.Description)));
        Assert.Null(Tables.VoucherType(11));
        Assert.Null(Tables.VoucherType(0));
    }

    [Fact]
    public void A_voucher_types_class_kind_and_FCE_flag_are_the_ones_the_manual_assigns()
    {
        int[] classA = [1, 2, 3, 51, 52, 53, 201, 202, 203];
        int[] invoices = [1, 6, 51, 201, 206];
        int[] plainB = [6, 7, 8];
        int[] fce = [201, 202, 203, 206, 207, 208];

        foreach (var type in Tables.VoucherTypes)
        {
            Assert.Equal(classA.Contains(type.Id), type.ClassA);
            Assert.Equal(invoices.Contains(type.Id), type.Invoice);
            Assert.Equal(!invoices.Contains(type.Id), type.Note);
            Assert.Equal(plainB.Contains(type.Id), type.PlainB);
            Assert.Equal(!plainB.Contains(type.Id), type.NeedsCuit);
            Assert.Equal(fce.Contains(type.Id), type.Fce);
        }
    }

    [Fact]
    public void The_receiver_conditions_a_type_allows_follow_its_class()
    {
        var classA = Tables.ReceiverConditions(Tables.VoucherType(1)!).Select(r => r.Code).ToList();
        var classB = Tables.ReceiverConditions(Tables.VoucherType(6)!).Select(r => r.Code).ToList();
        var fceA = Tables.ReceiverConditions(Tables.VoucherType(201)!).Select(r => r.Code).ToList();
        var fceB = Tables.ReceiverConditions(Tables.VoucherType(206)!).Select(r => r.Code).ToList();

        Assert.Equal(classA, fceA);
        Assert.Equal(classB, fceB);
        Assert.Contains(1, classA);
        Assert.DoesNotContain(1, classB);
        Assert.Contains(5, classB);
    }
}
