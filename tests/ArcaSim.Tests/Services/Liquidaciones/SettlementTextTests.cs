using ArcaSim.Application.Services.Liquidaciones;
using ArcaSim.Domain;

namespace ArcaSim.Tests.Services.Liquidaciones;

/// <summary>The words the liquidations print for a VAT condition: ArcaSim's, since the manuals show no list.</summary>
public class SettlementTextTests
{
    [Theory]
    [InlineData(VatCondition.ResponsableInscripto, "IVA Responsable Inscripto")]
    [InlineData(VatCondition.Exento, "IVA Sujeto Exento")]
    [InlineData(VatCondition.Monotributo, "Responsable Monotributo")]
    [InlineData(VatCondition.MonotributistaSocial, "Responsable Monotributo")]
    [InlineData(VatCondition.MonotributoTrabajadorIndependientePromovido, "Responsable Monotributo")]
    [InlineData(VatCondition.ConsumidorFinal, "IVA No Alcanzado")]
    [InlineData(VatCondition.NoCategorizado, "IVA No Alcanzado")]
    [InlineData(VatCondition.NoAlcanzado, "IVA No Alcanzado")]
    public void A_VAT_condition_prints_in_the_liquidations_wording(VatCondition condition, string text) =>
        Assert.Equal(text, SettlementLedger.VatText(condition));
}
