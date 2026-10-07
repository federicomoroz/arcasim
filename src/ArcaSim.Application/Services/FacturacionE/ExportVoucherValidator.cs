using System.Text.RegularExpressions;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;

namespace ArcaSim.Application.Services.FacturacionE;

/// <summary>
/// FEXAuthorize's validations (wsfexv1.md, "Validaciones y errores"). "Ante
/// cualquier anomalía se retorna un código de error cancelando la ejecución":
/// the first one that fails is the answer. The order they run in is ArcaSim's
/// (the manual's table order, with the voucher's identity first); ARCA's is
/// not documented. Not simulated, for lack of data: the customs and exporter
/// registries (1668, 2020-2029, 2059-2061), delivery notes (1818-1823), the
/// services' exchange-rate rules (2040-2055 but 2047) and 1604, whose
/// "superar en 1" has no clear reading.
/// </summary>
internal sealed partial class ExportVoucherValidator(ParameterTables tables, IExchangeRates rates, ExportStore store)
{
    public async Task<int> ValidateAsync(long cuit, ExportVoucher v, DateOnly today, bool pointOfSaleEnabled, CancellationToken ct)
    {
        if (v.VoucherType is not (19 or 20 or 21)) return 1530;
        if (!pointOfSaleEnabled) return 1510;
        if (v.Number is < 1 or > VoucherLimits.MaxNumber) return 1520;

        DateOnly date = today;
        if (!string.IsNullOrEmpty(v.Date))
        {
            if (!Fev1Dates.TryParse(v.Date, out date) || date < today.AddDays(-5) || date > today.AddDays(5)) return 1500;
            if (v.ExportType == 2 && (date.Year, date.Month).CompareTo((today.Year, today.Month)) > 0) return 1500;
        }

        var last = await store.LastAsync(cuit, v.PointOfSale, v.VoucherType, ct);
        if (v.Number != (last?.Voucher.Number ?? 0) + 1) return 1535;
        if (last is not null && Fev1Dates.TryParse(last.Date, out var lastDate) && date < lastDate) return 1535;

        if (v.ExportType is not (1 or 2 or 4)) return 1540;
        if (PermitProblem(v) is { } permit) return permit;

        if (!tables.Countries.Any(c => c.Id == v.Destination)) return 1560;
        if (v.ClientCountryCuit != 0 && !Cuits.IsValid(v.ClientCountryCuit)
            && !Wsfexv1Tables.CountryCuits.Any(c => c.Cuit == v.ClientCountryCuit)) return 1570;
        if (v.ClientCountryCuit == 0 && string.IsNullOrWhiteSpace(v.ClientTaxId)) return 1580;

        if (await CurrencyProblemAsync(v, date, ct) is { } currency) return currency;

        if (v.Items is not { Count: >= 1 and <= 9_999 }) return 1666;
        foreach (var item in v.Items)
            if (ItemProblem(item) is { } problem) return problem;
        var sum = v.Items.Sum(i => i.Total);
        if (v.Total < 0 || !Close(v.Total, sum, 0.01m * v.Items.Count)) return 1610;

        if (v.VoucherType == 19 && string.IsNullOrWhiteSpace(v.PaymentTerms)) return 1620;
        if (v.Language is not (1 or 2 or 3)) return 1630;
        if (string.IsNullOrWhiteSpace(v.Incoterms))
        {
            if (v.VoucherType == 19 && v.ExportType == 1) return 1640;
            if (!string.IsNullOrEmpty(v.IncotermsText)) return 1641;
        }
        else if (!Wsfexv1Tables.Incoterms.Any(i => i.Id == v.Incoterms)) return 1640;
        if (v.IncotermsText is { Length: > 20 }) return 1642;

        if (string.IsNullOrWhiteSpace(v.Client)) return 1650;
        if (v.Client.Length > 200) return 1651;
        if (string.IsNullOrWhiteSpace(v.ClientAddress)) return 1660;
        if (v.ClientAddress.Length > 300) return 1661;
        if (v.Notes is { Length: > 1000 } || v.CommercialNotes is { Length: > 4000 }) return 1665;

        if (PaymentDateProblem(v, date) is { } payment) return payment;
        if (await AssociatedProblemAsync(cuit, v, ct) is { } associated) return associated;
        return OptionalProblem(v) ?? 0;
    }

    /// <summary>The shipping permits' matrix (pág. 21) and the permits themselves (1720 to 1750).</summary>
    private int? PermitProblem(ExportVoucher v)
    {
        var exists = string.IsNullOrEmpty(v.PermitExists) ? null : v.PermitExists;
        if (exists is not (null or "S" or "N")) return 1550;
        var goodsInvoice = v.VoucherType == 19 && v.ExportType == 1;
        if (goodsInvoice ? exists is null : exists is not null) return 1550;

        if (v.Permits is null)
            return exists == "S" ? 1720 : null;
        if (v.ExportType is 2 or 4) return 1736;
        if (v.Permits.Count == 0 || exists == "N") return 1720;

        var seen = new HashSet<(string, int)>();
        foreach (var permit in v.Permits)
        {
            if (string.IsNullOrEmpty(permit.Id) != (permit.Destination == 0)) return 1730;
            if (permit.Id is null || !PermitFormat().IsMatch(permit.Id) || !seen.Add((permit.Id, permit.Destination))) return 1740;
            if (!tables.Countries.Any(c => c.Id == permit.Destination)) return 1750;
        }
        return null;
    }

    /// <summary>
    /// Currency and rate (1590 to 1605, 1667). A rate is checked against the
    /// official one only when ArcaSim has it (IExchangeRates), as WSFEv1 does.
    /// </summary>
    private async Task<int?> CurrencyProblemAsync(ExportVoucher v, DateOnly date, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(v.Currency) || !tables.HasCurrency(v.Currency)) return 1590;
        if (v.SameCurrency is not null && v.SameCurrency is not ("S" or "N")) return 1603;
        if (v.SameCurrency is not null && (v.VoucherType != 19 || v.Currency == "PES")) return 1605;
        if (v.Currency == "PES" && v.Rate is { } pesos && pesos != 1) return 1601;
        if (v.Rate is null or <= 0)
            return v.SameCurrency == "S" ? null : 1602;
        if (!Fits(v.Rate.Value, 4, 6)) return 1600;
        if (v.Currency != "PES" && await rates.RateAsync(v.Currency, Fev1Dates.PreviousBusinessDay(date), ct) is { } official
            && (v.Rate.Value > official.Rate * 4 || v.Rate.Value < official.Rate * 0.02m))
            return 1667;
        return null;
    }

    private static int? ItemProblem(ExportItem item)
    {
        if (item.Code is { Length: > 50 }) return 1760;
        if (string.IsNullOrWhiteSpace(item.Description) || item.Description.Length > 4_000) return 1770;
        if (!Wsfexv1Tables.Units.Any(u => u.Id == item.Unit)) return 1790;

        if (Wsfexv1Tables.IsSpecialUnit(item.Unit))
        {
            if (item.Quantity != 0 || item.UnitPrice != 0 || item.Discount != 0) return 1775;
        }
        else
        {
            if (item.Quantity <= 0) return 1780;
            if (!Fits(item.Quantity, 12, 6)) return 1813;
            if (item.UnitPrice < 0) return 1800;
            if (!Fits(item.UnitPrice, 12, 6)) return 1814;
            if (item.Discount < 0) return 1811;
            if (!Fits(item.Discount, 12, 6)) return 1817;
            if (item.Discount > 0 && item.Discount > item.UnitPrice * item.Quantity) return 1812;
        }

        if (item.Unit == 99 ? item.Total >= 0 : item.Unit != 97 && item.Total < 0) return 1810;
        if (!Fits(item.Total, 13, 2)) return 1816;
        if (!Wsfexv1Tables.IsSpecialUnit(item.Unit) && !Close(item.Total, item.UnitPrice * item.Quantity - item.Discount, 0.01m)) return 1815;
        return null;
    }

    private static int? PaymentDateProblem(ExportVoucher v, DateOnly date)
    {
        var services = v.VoucherType == 19 && v.ExportType is 2 or 4;
        if (string.IsNullOrEmpty(v.PaymentDate))
            return services ? 1672 : null;
        if (v.VoucherType != 19) return 1673;
        if (!Fev1Dates.TryParse(v.PaymentDate, out var payment)) return 1671;
        return services && payment < date ? 1674 : null;
    }

    /// <summary>
    /// Associated vouchers (pág. 19-21). An associated E voucher must be one
    /// ArcaSim authorized: every export point of sale is electronic. Delivery
    /// notes (88, 89, 91, 993, 994) are not looked up.
    /// </summary>
    private async Task<int?> AssociatedProblemAsync(long cuit, ExportVoucher v, CancellationToken ct)
    {
        if (v.Associated is null)
            return v.VoucherType is 20 or 21 && v.ExportType == 2 ? 2047 : null;
        if (v.Associated.Count == 0) return 1820;

        foreach (var associated in v.Associated)
        {
            if (!Wsfexv1Tables.AssociableTypes.Contains(associated.Type)) return 1680;
            if (associated.PointOfSale is < 1 or > VoucherLimits.MaxPointOfSale) return 1690;
            if (associated.Number is < 1 or > 999_999_999) return 1700;
        }
        var vouchers = v.Associated.Where(a => !Wsfexv1Tables.DeliveryNoteTypes.Contains(a.Type)).ToList();
        if (v.VoucherType == 19 && vouchers.Count > 0) return 1755;
        if (v.Associated.Count(a => a.Type is not (88 or 89)) > 1) return 1754;
        foreach (var associated in vouchers)
        {
            if (associated.Cuit != 0 && associated.Cuit != cuit) return 2031;
            if (await store.FindAsync(cuit, associated.PointOfSale, associated.Type, associated.Number, ct) is null) return 1749;
        }
        return null;
    }

    /// <summary>The simplified export regime's optional data, as far as it can be checked without the customs registry.</summary>
    private static int? OptionalProblem(ExportVoucher v)
    {
        if (v.Optionals is null) return null;
        if (v.Optionals.Count == 0) return 2001;
        var ids = new HashSet<string>();
        foreach (var optional in v.Optionals)
        {
            if (string.IsNullOrEmpty(optional.Id) && string.IsNullOrEmpty(optional.Value)) return 2002;
            if (optional.Id is not ("2401" or "2402")) return 2003;
            if (!ids.Add(optional.Id)) return 2004;
            if (optional.Id == "2401")
            {
                if (string.IsNullOrEmpty(optional.Value)) return 2005;
                if (!SimplifiedDocument().IsMatch(optional.Value)) return 2006;
            }
            else
            {
                if (string.IsNullOrEmpty(optional.Value)) return 2007;
                if (!FobAmount().IsMatch(optional.Value)) return 2008;
            }
        }
        if (v.ExportType != 1) return 2011;
        if (v.Currency != "DOL") return 2016;
        if (v.Permits is { Count: > 0 }) return 2056;
        if (v.VoucherType == 19) return ids.Count == 2 ? null : 2010;
        if (ids.Contains("2401")) return 2057;
        return ids.Contains("2402") ? null : 2058;
    }

    /// <summary>The manual's tolerance: relative error up to 0.01 %, or absolute error up to the given one.</summary>
    private static bool Close(decimal sent, decimal expected, decimal absolute)
    {
        var error = Math.Abs(sent - expected);
        return error <= absolute || expected != 0 && error / Math.Abs(expected) <= 0.0001m;
    }

    private static bool Fits(decimal value, int integers, int decimals) =>
        Math.Abs(value) < (decimal)Math.Pow(10, integers) && value == Math.Round(value, decimals);

    /// <summary>99999AAXX999999A: five digits, two letters, two letters or digits, six digits, a letter.</summary>
    [GeneratedRegex("^[0-9]{5}[A-Z]{2}[A-Z0-9]{2}[0-9]{6}[A-Z]$")]
    private static partial Regex PermitFormat();

    [GeneratedRegex("^[A-Za-z0-9]{11}$")]
    private static partial Regex SimplifiedDocument();

    [GeneratedRegex("^[0-9]{1,13}(\\.[0-9]{1,2})?$")]
    private static partial Regex FobAmount();
}
