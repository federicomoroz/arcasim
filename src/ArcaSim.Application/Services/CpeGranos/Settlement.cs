using System.Xml.Linq;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Application.Services.CpeGranos;

/// <summary>
/// The amounts of a wslpg liquidación or of one side of an adjustment,
/// computed the way the manual's examples come out (§2.4.2.4, ejemplos 1 and
/// 2): IVA over the subtotal, each retención as base x alícuota, deducciones
/// as their base plus its IVA, and the net, IVA RG 4310 and pago según
/// condición from those. Round half even, as §4.2 asks.
/// </summary>
internal static class Settlement
{
    /// <summary>One deducción, retención or importe: what it came from, its base (or rate, for importes), its amount and its IVA.</summary>
    public sealed record Line(XElement Source, decimal Base, decimal Amount, decimal Vat);

    public sealed record Totals(
        decimal Price,
        long Weight,
        decimal SubTotal,
        decimal Vat,
        IReadOnlyList<Line> Importes,
        IReadOnlyList<Line> Deductions,
        IReadOnlyList<Line> Retentions,
        IReadOnlyList<XElement> Perceptions)
    {
        public decimal WithVat => SubTotal + Vat;
        public decimal Deducted => Deductions.Sum(d => d.Amount + d.Vat);
        public decimal Retained => Retentions.Sum(r => r.Amount);
        public decimal Net => WithVat - Retained - Deducted;
        public decimal VatRg4310 => Vat - RetainedAs("RI");
        public decimal PaidByCondition => Net - VatRg4310;

        public decimal RetainedAs(string code) =>
            Retentions.Where(r => string.Equals(r.Source.Text("codigoConcepto"), code, StringComparison.OrdinalIgnoreCase)).Sum(r => r.Amount);

        public decimal VatAt(decimal rate) => Importes.Where(i => i.Base == rate).Sum(i => i.Vat);
    }

    private static readonly Totals Zero = new(0, 0, 0, 0, [], [], [], []);

    /// <summary>
    /// A primary liquidación: price per ton = precioRefTn x factorEnt / 100 +
    /// precioFleteTn, precioOperacion is per kilo with three decimals, weight
    /// is the certificates' pesoNeto or pesoNetoSinCertificado.
    /// </summary>
    public static Totals ForPrimary(XElement request, XElement liquidation)
    {
        var certificates = liquidation.Child("certificados")?.Elements("certificado").ToList() ?? [];
        var weight = certificates.Count > 0 ? certificates.Sum(c => c.Decimal("pesoNeto")) : liquidation.Decimal("pesoNetoSinCertificado");
        var factor = liquidation.Child("factorEnt") is null ? 100 : liquidation.Decimal("factorEnt");
        var price = GrainsFormat.Round(liquidation.Decimal("precioRefTn") * factor / 100 + liquidation.Decimal("precioFleteTn"), 3) / 1000;
        price = GrainsFormat.Round(price, 3);
        var subTotal = GrainsFormat.Round(price * weight);
        var vat = GrainsFormat.Round(subTotal * liquidation.Decimal("alicIvaOperacion") / 100);
        return new Totals(price, (long)weight, subTotal, vat, [],
            Deductions(request.Child("deducciones"), subTotal, weight),
            Retentions(request.Child("retenciones")),
            request.Child("percepciones")?.Elements("percepcion").ToList() ?? []);
    }

    /// <summary>One side of a unified adjustment: its amounts are the importes it declares at 0, 10.5 and 21%.</summary>
    public static Totals ForAdjustment(XElement side)
    {
        var importes = new List<Line>();
        foreach (var (amount, concept, rate) in new[] { ("importeAjustarIva0", "conceptoImporteIva0", 0m), ("importeAjustarIva105", "conceptoImporteIva105", 10.5m), ("importeAjustarIva21", "conceptoImporteIva21", 21m) })
            if (side.Child(amount) is not null)
            {
                var importe = GrainsFormat.Round(side.Decimal(amount));
                importes.Add(new Line(side.Child(concept) ?? new XElement(concept), rate, importe, GrainsFormat.Round(importe * rate / 100)));
            }
        var subTotal = importes.Sum(i => i.Amount);
        return Zero with
        {
            Price = GrainsFormat.Round(side.Decimal("diferenciaPrecioOperacion"), 3),
            Weight = (long)side.Decimal("diferenciaPesoNeto"),
            SubTotal = subTotal,
            Vat = importes.Sum(i => i.Vat),
            Importes = importes,
            Deductions = Deductions(side.Child("deducciones"), subTotal, side.Decimal("diferenciaPesoNeto")),
            Retentions = Retentions(side.Child("retenciones")),
        };
    }

    /// <summary>A deducción's amount: its base; with no base, the commission over the subtotal or the storage days x price per kilo.</summary>
    private static List<Line> Deductions(XElement? list, decimal subTotal, decimal weight) =>
        (list?.Elements("deduccion") ?? []).Select(d =>
        {
            var amount = d.Decimal("baseCalculo");
            if (amount == 0 && d.Decimal("comisionGastosAdm") > 0) amount = subTotal * d.Decimal("comisionGastosAdm") / 100;
            if (amount == 0 && d.Decimal("diasAlmacenaje") > 0) amount = d.Decimal("diasAlmacenaje") * d.Decimal("precioPKGdiario") * weight;
            amount = GrainsFormat.Round(amount);
            return new Line(d, amount, amount, GrainsFormat.Round(amount * d.Decimal("alicuotaIva") / 100));
        }).ToList();

    private static List<Line> Retentions(XElement? list) =>
        (list?.Elements("retencion") ?? []).Select(r =>
            new Line(r, r.Decimal("baseCalculo"), GrainsFormat.Round(r.Decimal("baseCalculo") * r.Decimal("alicuota") / 100), 0)).ToList();

    /// <summary>Writes the amounts into an autorización (or an adjustment side), list by list, in the schema's places.</summary>
    public static void Write(AnswerFill fill, XElement target, Totals totals)
    {
        WslpgRules.Put(fill, target, "subTotal", GrainsFormat.Amount(totals.SubTotal));
        WslpgRules.Put(fill, target, "importeIva", GrainsFormat.Amount(totals.Vat));
        WslpgRules.Put(fill, target, "operacionConIva", GrainsFormat.Amount(totals.WithVat));
        Rows(target.Child("percepciones"), "percepcion", totals.Perceptions, (row, p) => fill.Merge(row, p));
        WslpgRules.Put(fill, target, "totalPercepcion", GrainsFormat.Amount(totals.Perceptions.Sum(p => p.Decimal("importeFinal"))));
        Rows(target.Child("deducciones"), "deduccionReturn", totals.Deductions, (row, d) =>
        {
            fill.Merge(row.Child("deduccion"), d.Source);
            WslpgRules.Put(fill, row, "importeIva", GrainsFormat.Amount(d.Vat));
            WslpgRules.Put(fill, row, "importeDeduccion", GrainsFormat.Amount(d.Amount));
        });
        WslpgRules.Put(fill, target, "totalDeduccion", GrainsFormat.Amount(totals.Deducted));
        Rows(target.Child("retenciones"), "retencionReturn", totals.Retentions, (row, r) =>
        {
            fill.Merge(row.Child("retencion"), r.Source);
            WslpgRules.Put(fill, row, "importeRetencion", GrainsFormat.Amount(r.Amount));
        });
        WslpgRules.Put(fill, target, "totalRetencion", GrainsFormat.Amount(totals.Retained));
        WslpgRules.Put(fill, target, "totalRetencionAfip", GrainsFormat.Amount(totals.Retained));
        WslpgRules.Put(fill, target, "totalOtrasRetenciones", GrainsFormat.Amount(0));
        WslpgRules.Put(fill, target, "totalNetoAPagar", GrainsFormat.Amount(totals.Net));
        WslpgRules.Put(fill, target, "totalIvaRg4310_18", GrainsFormat.Amount(totals.VatRg4310));
        WslpgRules.Put(fill, target, "totalPagoSegunCondicion", GrainsFormat.Amount(totals.PaidByCondition));
    }

    /// <summary>totalesUnificados: debit minus credit, item by item (ArcaSim's reading; the manual gives no formula).</summary>
    public static void WriteUnified(AnswerFill fill, XElement target, Totals? debit, Totals? credit)
    {
        var d = debit ?? Zero;
        var c = credit ?? Zero;
        var deductionBase = d.Deductions.Sum(x => x.Amount) - c.Deductions.Sum(x => x.Amount);
        var values = new Dictionary<string, decimal>
        {
            ["subTotalDebCred"] = d.SubTotal - c.SubTotal,
            ["totalBaseDeducciones"] = deductionBase,
            ["subTotalGeneral"] = d.SubTotal - c.SubTotal - deductionBase,
            ["ivaDeducciones"] = d.Deductions.Sum(x => x.Vat) - c.Deductions.Sum(x => x.Vat),
            ["iva105"] = d.VatAt(10.5m) - c.VatAt(10.5m),
            ["iva21"] = d.VatAt(21m) - c.VatAt(21m),
            ["retencionesGanancias"] = d.RetainedAs("RG") - c.RetainedAs("RG"),
            ["retencionesIVA"] = d.RetainedAs("RI") - c.RetainedAs("RI"),
            ["importeOtrasRetenciones"] = d.Retained - d.RetainedAs("RG") - d.RetainedAs("RI") - (c.Retained - c.RetainedAs("RG") - c.RetainedAs("RI")),
            ["importeNeto"] = d.Net - c.Net,
            ["ivaRG4310_18"] = d.VatRg4310 - c.VatRg4310,
            ["pagoSCondicion"] = d.PaidByCondition - c.PaidByCondition,
        };
        foreach (var (name, value) in values) WslpgRules.Put(fill, target, name, GrainsFormat.Amount(value));
    }

    /// <summary>One copy of the list's item per line; with no lines, the list goes away.</summary>
    public static void Rows<T>(XElement? list, string item, IReadOnlyList<T> lines, Action<XElement, T> each)
    {
        if (list is null) return;
        if (lines.Count == 0)
        {
            list.Remove();
            return;
        }
        list.Repeat(item, lines, each);
    }
}
