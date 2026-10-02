using ArcaSim.Domain;

namespace ArcaSim.Application.Wsfe;

/// <summary>
/// The detail validations of FECAESolicitar and FECAEARegInformativo
/// (docs/arca/wsfev1.md §4): numbers, dates, amounts, VAT, other taxes,
/// currency, receiver and receiver VAT condition, associated vouchers. Each
/// failed rule becomes a finding with the method's code for it (RuleCodes);
/// whether it rejects or only observes comes from the catalog. Numbering
/// against earlier vouchers is not here: it needs the sequence, and ARCA
/// reports it apart, as an error of the whole request.
/// </summary>
public sealed class VoucherValidator(
    ParameterTables tables,
    ValidationCatalog catalog,
    SimulationSettings settings,
    ITaxpayerRepository taxpayers,
    IExchangeRates rates)
{
    /// <summary>"No Categorizado": the CUIT that stands for a receiver without a VAT category.</summary>
    public const long NotCategorized = 23000000000;

    private static readonly int[] IdentifiedDocuments = [80, 86, 87];

    /// <summary>Which voucher types each type may reference in CbtesAsoc (code 10040, wsfev1.md §4.4).</summary>
    private static readonly Dictionary<int, int[]> AssociableTypes = new()
    {
        [1] = [88, 991], [6] = [88, 991], [51] = [88, 991],
        [2] = [1, 2, 3, 4, 5, 34, 39, 60, 63, 88, 991], [3] = [1, 2, 3, 4, 5, 34, 39, 60, 63, 88, 991],
        [7] = [6, 7, 8, 9, 10, 35, 40, 61, 64, 88, 991], [8] = [6, 7, 8, 9, 10, 35, 40, 61, 64, 88, 991],
        [12] = [11, 12, 13, 15], [13] = [11, 12, 13, 15],
        [52] = [51, 52, 53, 54, 88, 991], [53] = [51, 52, 53, 54, 88, 991],
        [201] = [91, 990, 991, 993, 994, 995], [206] = [91, 990, 991, 993, 994, 995], [211] = [91, 990, 991, 993, 994, 995],
        [202] = [201, 202, 203], [203] = [201, 202, 203],
        [207] = [206, 207, 208], [208] = [206, 207, 208],
        [212] = [211, 212, 213], [213] = [211, 212, 213],
    };

    /// <summary>The voucher's date: the one sent, or today when it came empty, as ARCA assigns it.</summary>
    public static DateOnly DateOf(FEDetRequest detail, DateOnly today) =>
        Fev1Dates.TryParse(Fev1Dates.Blank(detail.CbteFch), out var date) ? date : today;

    /// <param name="method">RuleCodes.Cae or RuleCodes.Caea: whose codes the findings carry.</param>
    public async Task<IReadOnlyList<Finding>> ValidateAsync(
        string method, FEDetRequest detail, VoucherTypeInfo type, long issuerCuit, DateOnly today, CancellationToken ct = default)
    {
        var findings = new List<Finding>();
        void Add(Rule rule)
        {
            if (RuleCodes.CodeOf(rule, method) is { } code) findings.Add(catalog.For(method, code));
        }

        CheckNumbers(detail, type, Add);
        await CheckReceiverAsync(detail, type, issuerCuit, Add, ct);
        if (method == RuleCodes.Cae) CheckDateRange(detail, type, today, findings, Add);
        CheckServiceDates(detail, today, Add);
        CheckAmounts(detail, type, Add);
        if (type.Class == VoucherClass.C)
        {
            if (detail.Iva is not null) Add(Rule.VatOnClassC);
        }
        else
        {
            CheckVat(detail, type, Add);
        }
        CheckOtherTaxes(detail, type, Add);
        if (!tables.HasConcept(detail.Concepto)) Add(Rule.Concept);
        if (type.Class == VoucherClass.UsedGoods && detail.Concepto != 1) Add(Rule.UsedGoodsConcept);
        await CheckCurrencyAsync(detail, today, Add, ct);
        CheckReceiverVatCondition(detail, type, today, Add);
        CheckAssociated(detail, type, Add);

        return findings.DistinctBy(f => f.Code).ToList();
    }

    private static void CheckNumbers(FEDetRequest d, VoucherTypeInfo type, Action<Rule> add)
    {
        if (d.CbteDesde is < 1 or > 99_999_999) add(Rule.FromRange);
        if (d.CbteHasta is < 1 or > 99_999_999) add(Rule.ToRange);
        if (type.Class == VoucherClass.B && !type.Fce && d.CbteHasta < d.CbteDesde) add(Rule.BatchOrder);
        if ((type.Class != VoucherClass.B || type.Fce) && d.CbteHasta != d.CbteDesde)
            add(type.Class == VoucherClass.C ? Rule.SingleNumberClassC : Rule.SingleNumber);
    }

    /// <summary>
    /// ArcaSim's padrón only knows the taxpayers loaded into it. A CUIT it does
    /// not know but whose check digit is right is taken as an active taxpayer,
    /// so that applications can invoice their real customers while developing.
    /// </summary>
    private async Task CheckReceiverAsync(FEDetRequest d, VoucherTypeInfo type, long issuerCuit, Action<Rule> add, CancellationToken ct)
    {
        var receiver = d.DocTipo == 80 ? await taxpayers.FindAsync(d.DocNro, ct) : null;
        var identified = IdentifiedDocuments.Contains(d.DocTipo);
        var unknownNumber = identified && d.DocNro != NotCategorized && (!Cuits.IsValid(d.DocNro) || receiver is { Active: false });

        switch (type.Class)
        {
            case VoucherClass.A or VoucherClass.ALey:
                if (d.DocTipo != 80) add(Rule.ClassADocumentType);
                else if (unknownNumber) add(Rule.ReceiverInactiveClassA);
                else if (receiver is not null && !IsActiveInVatOrMonotributo(receiver)) add(Rule.ReceiverNotInVatClassA);
                if (receiver is not null && IsMonotributo(receiver.VatCondition)) add(Rule.MonotributoReceiver);
                break;

            case VoucherClass.B:
                var count = d.CbteHasta - d.CbteDesde + 1;
                var amount = (decimal)d.ImpTotal * (d.Exchange is { } exchange ? (decimal)exchange : 1m);
                if (count > 1)
                {
                    if (amount / count >= settings.FinalConsumerIdentificationThreshold) add(Rule.BatchOverThreshold);
                    if (d.DocTipo != 99 || d.DocNro != 0) add(Rule.BatchToConsumer);
                }
                else if (!tables.HasDocumentType(d.DocTipo)) add(Rule.UnknownDocumentType);
                else if (amount >= settings.FinalConsumerIdentificationThreshold && (d.DocTipo == 99 || d.DocNro <= 0)) add(Rule.IdentifyOverThreshold);
                else if (d.DocTipo == 99 && d.DocNro != 0) add(Rule.ConsumerNumberZero);
                else if (d.DocTipo != 99 && d.DocNro <= 0) add(Rule.DocumentNumberRequired);
                else if (unknownNumber) add(Rule.ReceiverNotRegistered);

                if (d.DocTipo == 80 && d.DocNro == NotCategorized && d.ImpNeto + d.ImpIVA > 0 && type.Id is 6 or 7 or 8
                    && d.Tributos?.Any(t => t.Id == 13 && t.Importe > 0) != true)
                {
                    add(d.ImpTrib > 0 ? Rule.NotCategorizedPerceptionObserved : Rule.NotCategorizedPerceptionMissing);
                }
                break;

            case VoucherClass.UsedGoods:
                if (!tables.HasDocumentType(d.DocTipo) || d.DocTipo == 99 || d.DocNro <= 0 || unknownNumber) add(Rule.UsedGoodsReceiver);
                break;
        }
        if (type.Fce && d.DocTipo != 80) add(Rule.FceDocumentType);
        if (d.DocNro == issuerCuit) add(Rule.SameAsIssuer);
    }

    private static bool IsMonotributo(VatCondition condition) =>
        condition is VatCondition.Monotributo or VatCondition.MonotributistaSocial or VatCondition.MonotributoTrabajadorIndependientePromovido;

    private static bool IsActiveInVatOrMonotributo(Taxpayer receiver) =>
        receiver.Active && (receiver.VatCondition == VatCondition.ResponsableInscripto || IsMonotributo(receiver.VatCondition));

    /// <summary>CbteFch around the day of the request (wsfev1.md §4.2). CAEA vouchers are checked against the CAEA's validity instead.</summary>
    private void CheckDateRange(FEDetRequest d, VoucherTypeInfo type, DateOnly today, List<Finding> findings, Action<Rule> add)
    {
        var sent = Fev1Dates.Blank(d.CbteFch);
        if (sent is null) return;

        var (before, after) = type.Fce
            ? (5, type.IsNote ? 0 : 1)
            : d.Concepto is 2 or 3 ? (10, 10) : (5, 5);
        if (!Fev1Dates.TryParse(sent, out var date) || date < today.AddDays(-before) || date > today.AddDays(after))
            findings.Add(DateRangeFinding(d.Concepto, type, before, after));
        else if ((d.Concepto == 1 || type.Fce) && date > today && (date.Month != today.Month || date.Year != today.Year))
            add(Rule.FutureMonth);
    }

    /// <summary>
    /// 10016 for a date out of range. Two of its texts were captured (products,
    /// and FCE notes); the others follow the same wording with their own range.
    /// </summary>
    private Finding DateRangeFinding(int concept, VoucherTypeInfo type, int before, int after)
    {
        if (type.Fce && type.IsNote) return catalog.For(RuleCodes.Cae, 10016, "Obs:FCE-ND-NC");
        if (!type.Fce && concept is not (2 or 3)) return catalog.For(RuleCodes.Cae, 10016, "Obs:1");
        var subject = type.Fce
            ? "Facturas de Credito"
            : concept == 2 ? "2 - Servicios" : "3 - Productos y Servicios";
        return catalog.WithMessage(RuleCodes.Cae, 10016,
            $"Campo CbteFch Debe estar comprendido  en el  rango  N-{before} y N+{after} siendo N la fecha de envio del pedido  de autorizacion para {subject}");
    }

    private static void CheckServiceDates(FEDetRequest d, DateOnly today, Action<Rule> add)
    {
        var from = Fev1Dates.Blank(d.FchServDesde);
        var to = Fev1Dates.Blank(d.FchServHasta);
        var due = Fev1Dates.Blank(d.FchVtoPago);
        if (d.Concepto is 2 or 3)
        {
            if (from is null || to is null || due is null) add(Rule.ServiceDatesRequired);
        }
        else if (from is not null || to is not null || due is not null)
        {
            if (from is null) add(Rule.ServiceFromMissing);
            if (to is null) add(Rule.ServiceToMissing);
            if (due is null) add(Rule.PaymentDueMissing);
        }

        var fromOk = Fev1Dates.TryParse(from, out var fromDate);
        var toOk = Fev1Dates.TryParse(to, out var toDate);
        var dueOk = Fev1Dates.TryParse(due, out var dueDate);
        if ((from is not null && !fromOk) || (to is not null && !toOk) || (due is not null && !dueOk)) add(Rule.DateFormat);
        if (fromOk && toOk && fromDate > toDate) add(Rule.ServiceOrder);
        if (dueOk && dueDate < DateOf(d, today)) add(Rule.PaymentDueBeforeDate);
    }

    private static void CheckAmounts(FEDetRequest d, VoucherTypeInfo type, Action<Rule> add)
    {
        var classC = type.Class == VoucherClass.C;
        if (d.ImpTotConc < 0) add(Rule.NotTaxedNegative);
        else if (classC && d.ImpTotConc != 0) add(Rule.NotTaxedClassC);
        if (d.ImpOpEx < 0) add(Rule.ExemptNegative);
        else if (classC && d.ImpOpEx != 0) add(Rule.ExemptClassC);
        if (d.ImpNeto < 0) add(Rule.NetNegative);
        if (d.ImpTrib < 0) add(Rule.OtherTaxesNegative);
        if (d.ImpIVA < 0) add(Rule.VatNegative);
        else if (classC && d.ImpIVA != 0) add(Rule.VatClassC);
        if (d.ImpTotal < 0) add(Rule.TotalNegative);

        var expected = classC
            ? Money(d.ImpNeto) + Money(d.ImpTrib)
            : Money(d.ImpTotConc) + Money(d.ImpNeto) + Money(d.ImpOpEx) + Money(d.ImpTrib) + Money(d.ImpIVA);
        if (!Amounts.WithinMargin(expected, Money(d.ImpTotal), 1))
            add(classC ? Rule.TotalMismatchClassC : Rule.TotalMismatch);

        double[] totals = [d.ImpTotal, d.ImpTotConc, d.ImpNeto, d.ImpOpEx, d.ImpTrib, d.ImpIVA];
        var precise = totals.All(v => Amounts.HasPrecision(v, 13, 2))
                      && (d.Iva ?? []).All(a => Amounts.HasPrecision(a.BaseImp, 13, 2) && Amounts.HasPrecision(a.Importe, 13, 2))
                      && (d.Tributos ?? []).All(t => Amounts.HasPrecision(t.BaseImp, 13, 2) && Amounts.HasPrecision(t.Importe, 13, 2) && Amounts.HasPrecision(t.Alic, 3, 2))
                      && (!d.MonCotizSpecified || Amounts.HasPrecision(d.MonCotiz, 4, 6));
        if (!precise) add(Rule.Precision);
    }

    private void CheckVat(FEDetRequest d, VoucherTypeInfo type, Action<Rule> add)
    {
        var iva = d.Iva;
        if (iva is null)
        {
            if (d.ImpIVA > 0) add(Rule.VatMissing);
            if (d.ImpNeto > 0) add(Rule.NetWithoutVat);
            return;
        }
        if (iva.Length == 0 || (d.ImpIVA == 0 && iva.Any(a => a.Id != 3)))
        {
            add(Rule.VatMissing);
            return;
        }

        foreach (var line in iva)
        {
            var rate = tables.Vat(line.Id);
            if ((line.Id == 0 && !type.IsLooseVatNote) || (line.Id != 0 && rate is null)) add(Rule.VatId);
            if (line.BaseImp < 0 || (line.BaseImp == 0 && !type.IsLooseVatNote)) add(Rule.VatBase);
            if (line.Importe < 0) add(Rule.VatAmount);
            if (rate is not null && !type.IsLooseVatNote
                && !Amounts.WithinMargin(Math.Round(Money(line.BaseImp) * rate.Rate, 2, MidpointRounding.ToEven), Money(line.Importe), 1))
            {
                add(Rule.VatRateAmount);
            }
        }
        if (iva.GroupBy(a => a.Id).Any(g => g.Count() > 1)) add(Rule.VatDuplicate);
        if (!Amounts.WithinMargin(iva.Sum(a => Money(a.Importe)), Money(d.ImpIVA), iva.Length)) add(Rule.VatSum);
        if (!type.IsLooseVatNote && !Amounts.WithinMargin(iva.Sum(a => Money(a.BaseImp)), Money(d.ImpNeto), iva.Length)) add(Rule.NetSum);
    }

    private void CheckOtherTaxes(FEDetRequest d, VoucherTypeInfo type, Action<Rule> add)
    {
        var taxes = d.Tributos;
        if ((d.ImpTrib > 0 && (taxes is null || taxes.Length == 0)) || (d.ImpTrib == 0 && taxes is not null))
        {
            add(Rule.OtherTaxesPresence);
            return;
        }
        if (taxes is null) return;

        foreach (var tax in taxes)
        {
            if (!tables.HasTax(tax.Id)) add(Rule.OtherTaxId);
            if (tax.BaseImp < 0) add(Rule.OtherTaxBase);
            if (tax.Alic < 0) add(Rule.OtherTaxRate);
            if (tax.Importe < 0) add(Rule.OtherTaxAmount);
            if ((tax.Id == 99 || type.Fce) && string.IsNullOrWhiteSpace(tax.Desc)) add(Rule.OtherTaxDescription);
        }
        if (!Amounts.WithinMargin(taxes.Sum(t => Money(t.Importe)), Money(d.ImpTrib), taxes.Length)) add(Rule.OtherTaxesSum);
    }

    private async Task CheckCurrencyAsync(FEDetRequest d, DateOnly today, Action<Rule> add, CancellationToken ct)
    {
        var currency = d.MonId;
        var sameCurrency = d.CanMisMonExt;
        if (sameCurrency is not null && sameCurrency is not ("S" or "N")) add(Rule.SameCurrencyFlag);
        if (string.IsNullOrEmpty(currency) || !tables.HasCurrency(currency))
        {
            add(Rule.Currency);
            return;
        }

        if (currency == "PES")
        {
            if (!d.MonCotizSpecified || d.MonCotiz != 1) add(Rule.PesosRate);
            if (sameCurrency == "S") add(Rule.PesosSameCurrency);
            return;
        }

        if ((d.MonCotizSpecified && d.MonCotiz <= 0) || (!d.MonCotizSpecified && sameCurrency != "S"))
        {
            add(Rule.Rate);
            return;
        }
        if (!d.MonCotizSpecified) return;

        var date = DateOf(d, today);
        var official = await rates.RateAsync(currency, Fev1Dates.PreviousBusinessDay(date < today ? date : today), ct);
        if (sameCurrency == "S" && official is not null && Money(d.MonCotiz) != official.Value.Rate) add(Rule.Rate);

        var orientative = await rates.RateAsync(currency, today, ct);
        if (orientative is { Rate: var reference }
            && (Money(d.MonCotiz) < reference * 0.02m || Money(d.MonCotiz) > reference * 4m))
        {
            add(Rule.RateRange);
        }
    }

    private void CheckReceiverVatCondition(FEDetRequest d, VoucherTypeInfo type, DateOnly today, Action<Rule> add)
    {
        if (d.ReceiverVatCondition is not { } id)
        {
            add(settings.ManualVersionOn(today) == ManualVersion.V4_8
                ? Rule.ReceiverConditionMissingRequired
                : Rule.ReceiverConditionMissingObserved);
            return;
        }
        var condition = tables.ReceiverVatCondition(id);
        if (condition is null) add(Rule.ReceiverConditionUnknown);
        else if (!ParameterTables.Allows(condition, type.Class)) add(Rule.ReceiverConditionClass);
    }

    private static void CheckAssociated(FEDetRequest d, VoucherTypeInfo type, Action<Rule> add)
    {
        var associated = d.CbtesAsoc ?? [];
        if (type.IsNote && associated.Length == 0 && d.PeriodoAsoc is null) add(Rule.NoteWithoutAssociated);
        if (type.Kind == VoucherKind.Invoice && d.PeriodoAsoc is not null) add(Rule.InvoiceWithPeriod);
        if (associated.Length == 0) return;

        if (!AssociableTypes.TryGetValue(type.Id, out var allowed) || associated.Any(a => !allowed.Contains(a.Tipo))) add(Rule.AssociatedType);
        if (associated.Any(a => a.PtoVta is <= 0 or >= 99_999)) add(Rule.AssociatedPointOfSale);
        if (associated.Any(a => a.Nro is <= 0 or >= 99_999_999)) add(Rule.AssociatedNumber);
    }

    private static decimal Money(double value) => (decimal)value;
}

/// <summary>The arithmetic rules of the manual's "Margen de error" (wsfev1.md §4.3).</summary>
public static class Amounts
{
    /// <summary>Equal within an absolute error of 0.01 per added line, or a relative error of 0.01 %.</summary>
    public static bool WithinMargin(decimal expected, decimal actual, int lines)
    {
        var absolute = Math.Abs(expected - actual);
        if (absolute <= 0.01m * Math.Max(lines, 1)) return true;
        return actual != 0 && absolute / Math.Abs(actual) <= 0.0001m;
    }

    /// <summary>At most the given integer digits and decimals, as in "Double (13+2)".</summary>
    public static bool HasPrecision(double value, int integers, int decimals)
    {
        var amount = Math.Abs((decimal)value);
        return Math.Round(amount, decimals) == amount && Math.Truncate(amount).ToString().Length <= integers;
    }
}
