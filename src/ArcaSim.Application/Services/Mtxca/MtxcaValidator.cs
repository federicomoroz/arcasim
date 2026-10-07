using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;

namespace ArcaSim.Application.Services.Mtxca;

/// <summary>A check on a ComprobanteType, independent of the code it carries in each method.</summary>
public enum MtxcaRule
{
    AuthorizationTypeSent, AuthorizationCodeSent, AuthorizationDueSent, DateWindow,
    DocumentPair, DocumentRequired, ClassADocumentType, DocumentType, SameAsIssuer, NotCategorizedReceiver,
    ReceiverInvalid, ReceiverNotInVat, MonotributoReceiver, NotCategorizedPerception,
    ReceiverConditionMissing, ReceiverConditionClass,
    Currency, PesosRate, SameCurrencyFlag, SameCurrencyNotInvoice, PesosSameCurrency, RateRequired, RateNegative, RateRange,
    Concept, ServiceFrom, ServiceTo, PaymentDue, PaymentDueBeforeDate, ServiceOrder, FcePaymentDueMissing, FceNotePaymentDue,
    GenerationTimeOnlyCaea, GenerationTimeRequired,
    NoteWithoutAssociated, NoteWithBoth, InvoiceWithPeriod, FceNoteWithPeriod, AssociatedType, AssociatedTypeForVoucher,
    PeriodOrder, PeriodAfterDate,
    OtherTaxCode, OtherTaxDescription, OtherTaxesSum,
    ItemUnitsRequired, ItemUnitsMin, ItemUnitsLength, ItemGtinRequired, ItemUnitsWithGtin, ItemCodeLength, ItemDescriptionLength,
    ItemQuantity, ItemUnit, ItemPrice, ItemDiscountNotAllowed, ItemDiscountTooHigh, ItemVatCondition, ItemBonusAlone,
    ItemVatPresence, ItemVatAmount, ItemBonusVat, ItemVatSign, ItemAmountSign, ItemAmount, ItemVatZero,
    SubtotalsRequired, SubtotalCode, SubtotalAmount, SubtotalRepeated, SubtotalPresence, SubtotalNegative,
    NetSum, NotTaxedSum, ExemptSum, SubtotalSum, TotalSum, TotalItems,
    ExtraDataType, ExtraDataSingle,
    BuyersNotAllowed, BuyersSingle, BuyerDocType, BuyerPercentPositive, BuyerPercentBelow100, BuyersSum, BuyersConcept, BuyersFce,
    ActivityRepeated, ActivityNotCurrent,
}

/// <summary>
/// The checks autorizarComprobante and informarComprobanteCAEA share
/// (docs/arca/servicios/wsmtxca.md, "Validaciones y errores"): the same rule
/// carries a different code in each method, and CAEA often observes where CAE
/// rejects, because the voucher was already issued.
///
/// ArcaSim's choices where the manual is silent: every finding is returned,
/// not only the first; a valid CUIT ArcaSim's padrón does not know counts as
/// an active taxpayer, as in WSFEv1; FCE A and B follow class A's and class
/// B's item rules; class B has to identify the receiver from WSFEv1's
/// threshold (RG 5700/2025), the amount RG 4444's rule points to today.
/// </summary>
public sealed class MtxcaValidator(MtxcaTables tables, ITaxpayerRepository taxpayers, IExchangeRates rates, SimulationSettings settings)
{
    private const long NotCategorized = 23000000000;

    private static (int, bool)? R(int code) => (code, true);
    private static (int, bool)? O(int code) => (code, false);

    private static readonly Dictionary<MtxcaRule, ((int Code, bool Rejects)? Cae, (int Code, bool Rejects)? Caea)> Codes = new()
    {
        [MtxcaRule.AuthorizationTypeSent] = (R(105), R(731)),
        [MtxcaRule.AuthorizationCodeSent] = (R(106), null),
        [MtxcaRule.AuthorizationDueSent] = (R(107), null),
        [MtxcaRule.DateWindow] = (R(103), null),
        [MtxcaRule.DocumentPair] = (R(108), R(707)),
        [MtxcaRule.DocumentRequired] = (R(128), R(718)),
        [MtxcaRule.ClassADocumentType] = (R(129), R(733)),
        [MtxcaRule.DocumentType] = (R(132), R(736)),
        [MtxcaRule.SameAsIssuer] = (R(131), O(735)),
        [MtxcaRule.NotCategorizedReceiver] = (R(342), R(889)),
        [MtxcaRule.ReceiverInvalid] = (O(109), O(708)),
        [MtxcaRule.ReceiverNotInVat] = (O(130), O(734)),
        [MtxcaRule.MonotributoReceiver] = (O(164), O(782)),
        [MtxcaRule.NotCategorizedPerception] = (R(145), O(749)),
        [MtxcaRule.ReceiverConditionMissing] = (O(190), O(390)),
        [MtxcaRule.ReceiverConditionClass] = (O(191), O(391)),
        [MtxcaRule.Currency] = (R(117), R(710)),
        [MtxcaRule.PesosRate] = (R(120), O(726)),
        [MtxcaRule.SameCurrencyFlag] = (R(164), O(174)),
        [MtxcaRule.SameCurrencyNotInvoice] = (R(118), O(122)),
        [MtxcaRule.PesosSameCurrency] = (R(169), O(175)),
        [MtxcaRule.RateRequired] = (R(194), R(194)),
        [MtxcaRule.RateNegative] = (R(195), R(195)),
        [MtxcaRule.RateRange] = (R(119), O(182)),
        [MtxcaRule.Concept] = (R(121), R(713)),
        [MtxcaRule.ServiceFrom] = (R(122), O(727)),
        [MtxcaRule.ServiceTo] = (R(123), O(728)),
        [MtxcaRule.PaymentDue] = (R(124), O(729)),
        [MtxcaRule.PaymentDueBeforeDate] = (R(125), O(730)),
        [MtxcaRule.ServiceOrder] = (R(133), O(737)),
        [MtxcaRule.FcePaymentDueMissing] = (R(148), R(764)),
        [MtxcaRule.FceNotePaymentDue] = (R(149), R(765)),
        [MtxcaRule.GenerationTimeOnlyCaea] = (R(146), null),
        [MtxcaRule.GenerationTimeRequired] = (null, R(754)),
        [MtxcaRule.NoteWithoutAssociated] = (R(160), R(778)),
        [MtxcaRule.NoteWithBoth] = (R(161), R(779)),
        [MtxcaRule.InvoiceWithPeriod] = (R(162), R(780)),
        [MtxcaRule.FceNoteWithPeriod] = (R(159), R(777)),
        [MtxcaRule.AssociatedType] = (R(203), R(803)),
        [MtxcaRule.AssociatedTypeForVoucher] = (R(200), null),
        [MtxcaRule.PeriodOrder] = (R(2200), R(2800)),
        [MtxcaRule.PeriodAfterDate] = (R(2201), R(2801)),
        [MtxcaRule.OtherTaxCode] = (R(300), R(900)),
        [MtxcaRule.OtherTaxDescription] = (R(301), R(901)),
        [MtxcaRule.OtherTaxesSum] = (R(114), O(723)),
        [MtxcaRule.ItemUnitsRequired] = (R(500), R(1100)),
        [MtxcaRule.ItemUnitsMin] = (R(501), R(1101)),
        [MtxcaRule.ItemUnitsLength] = (R(502), R(1102)),
        [MtxcaRule.ItemGtinRequired] = (R(503), R(1103)),
        [MtxcaRule.ItemUnitsWithGtin] = (R(520), R(1121)),
        [MtxcaRule.ItemCodeLength] = (R(505), R(1105)),
        [MtxcaRule.ItemDescriptionLength] = (R(506), R(1106)),
        [MtxcaRule.ItemQuantity] = (R(507), R(1107)),
        [MtxcaRule.ItemUnit] = (R(508), R(1108)),
        [MtxcaRule.ItemPrice] = (R(509), R(1109)),
        [MtxcaRule.ItemDiscountNotAllowed] = (R(510), R(1110)),
        [MtxcaRule.ItemDiscountTooHigh] = (R(511), O(1114)),
        [MtxcaRule.ItemVatCondition] = (R(512), R(1111)),
        [MtxcaRule.ItemBonusAlone] = (R(513), O(1115)),
        [MtxcaRule.ItemVatPresence] = (R(514), R(1112)),
        [MtxcaRule.ItemVatAmount] = (R(515), O(1116)),
        [MtxcaRule.ItemBonusVat] = (R(516), O(1117)),
        [MtxcaRule.ItemVatSign] = (R(517), O(1118)),
        [MtxcaRule.ItemAmountSign] = (R(518), O(1119)),
        [MtxcaRule.ItemAmount] = (R(519), O(1120)),
        [MtxcaRule.ItemVatZero] = (R(521), O(1122)),
        [MtxcaRule.SubtotalsRequired] = (R(127), R(715)),
        [MtxcaRule.SubtotalCode] = (R(400), R(1000)),
        [MtxcaRule.SubtotalAmount] = (R(401), O(1001)),
        [MtxcaRule.SubtotalRepeated] = (R(402), R(1002)),
        [MtxcaRule.SubtotalPresence] = (R(403), R(1003)),
        [MtxcaRule.SubtotalNegative] = (R(405), O(1005)),
        [MtxcaRule.NetSum] = (R(110), O(719)),
        [MtxcaRule.NotTaxedSum] = (R(111), O(720)),
        [MtxcaRule.ExemptSum] = (R(112), O(721)),
        [MtxcaRule.SubtotalSum] = (R(113), O(722)),
        [MtxcaRule.TotalSum] = (R(115), O(724)),
        [MtxcaRule.TotalItems] = (R(116), O(725)),
        [MtxcaRule.ExtraDataType] = (R(320), R(920)),
        [MtxcaRule.ExtraDataSingle] = (R(322), null),
        [MtxcaRule.BuyersNotAllowed] = (null, R(753)),
        [MtxcaRule.BuyersSingle] = (R(420), null),
        [MtxcaRule.BuyerDocType] = (R(422), null),
        [MtxcaRule.BuyerPercentPositive] = (R(424), null),
        [MtxcaRule.BuyerPercentBelow100] = (R(425), null),
        [MtxcaRule.BuyersSum] = (R(427), null),
        [MtxcaRule.BuyersConcept] = (R(432), null),
        [MtxcaRule.BuyersFce] = (R(433), null),
        [MtxcaRule.ActivityRepeated] = (R(166), O(366)),
        [MtxcaRule.ActivityNotCurrent] = (R(167), O(367)),
    };

    /// <summary>The code a rule carries in a method, for tests and for the manual's cross-references.</summary>
    public static (int Code, bool Rejects)? CodeOf(MtxcaRule rule, bool caea) => caea ? Codes[rule].Caea : Codes[rule].Cae;

    /// <param name="caea">informarComprobanteCAEA's codes instead of autorizarComprobante's.</param>
    /// <param name="date">The voucher's date: the one sent, or the day it is processed.</param>
    /// <param name="activities">The issuer's current activities, or null when ArcaSim does not know them (open access).</param>
    public async Task<List<MtxcaFinding>> ValidateAsync(
        bool caea, MtxcaVoucherInput v, MtxcaVoucherType type, long issuer, DateOnly date, DateOnly today,
        IReadOnlyCollection<long>? activities, CancellationToken ct)
    {
        var findings = new List<MtxcaFinding>();
        var table = caea ? MtxcaTable.Caea : MtxcaTable.Cae;
        void Add(MtxcaRule rule)
        {
            if (CodeOf(rule, caea) is not { } code) return;
            var finding = code.Rejects ? MtxcaCodes.Error(table, code.Code) : MtxcaCodes.Observation(table, code.Code);
            if (!findings.Contains(finding)) findings.Add(finding);
        }

        if (!caea)
        {
            if (v.AuthorizationType is not null) Add(MtxcaRule.AuthorizationTypeSent);
            if (v.AuthorizationCode is not null) Add(MtxcaRule.AuthorizationCodeSent);
            if (v.AuthorizationDue is not null) Add(MtxcaRule.AuthorizationDueSent);
            if (v.GenerationTime is not null) Add(MtxcaRule.GenerationTimeOnlyCaea);
            if (v.Date is { } sent && !InWindow(sent, today, v.Concept)) Add(MtxcaRule.DateWindow);
        }
        else
        {
            if (v.AuthorizationType is not null and not "A") Add(MtxcaRule.AuthorizationTypeSent);
            if (v.GenerationTime is null) Add(MtxcaRule.GenerationTimeRequired);
        }

        if (v.Concept is not (1 or 2 or 3)) Add(MtxcaRule.Concept);
        await CheckReceiverAsync(v, type, issuer, Add, ct);
        await CheckCurrencyAsync(v, type, today, Add, ct);
        CheckDates(v, type, date, Add);
        CheckAssociated(v, type, date, Add);
        CheckOtherTaxes(v, Add);
        CheckItems(v, type, Add);
        CheckSubtotals(v, type, Add);
        CheckTotals(v, type, Add);

        if (v.ExtraData.Any(t => !MtxcaTables.HasExtraDataType(t))) Add(MtxcaRule.ExtraDataType);
        if (v.ExtraData.Count > 1) Add(MtxcaRule.ExtraDataSingle);
        CheckBuyers(v, type, caea, Add);
        if (v.Activities.Distinct().Count() != v.Activities.Count) Add(MtxcaRule.ActivityRepeated);
        if (activities is not null && v.Activities.Any(a => !activities.Contains(a))) Add(MtxcaRule.ActivityNotCurrent);
        return findings;
    }

    /// <summary>Within 5 days of today for products, without reaching into next month; within 10 for services (103).</summary>
    public static bool InWindow(DateOnly date, DateOnly today, int concept)
    {
        var limit = concept == 1 ? 5 : 10;
        var days = date.DayNumber - today.DayNumber;
        if (days < -limit || days > limit) return false;
        return concept != 1 || date <= today || (date.Year == today.Year && date.Month == today.Month);
    }

    /// <summary>Error relativo ≤ 0,01 % o absoluto ≤ 0,01 × cantidad de elementos sumados, as every sum check of the manual says.</summary>
    public static bool Close(decimal expected, decimal actual, int count)
    {
        var difference = Math.Abs(expected - actual);
        return difference <= 0.01m * Math.Max(count, 1) || (expected != 0 && difference / Math.Abs(expected) <= 0.0001m);
    }

    private async Task CheckReceiverAsync(MtxcaVoucherInput v, MtxcaVoucherType type, long issuer, Action<MtxcaRule> add, CancellationToken ct)
    {
        if ((v.DocType is null) != (v.DocNumber is null)) add(MtxcaRule.DocumentPair);
        if (v.DocType is { } docType && !tables.HasDocumentType(docType)) add(MtxcaRule.DocumentType);

        var amount = v.Total * (v.Rate is { } rate && rate > 0 ? rate : 1m);
        var identified = v.DocType is not null and not 99 && v.DocNumber is > 0;
        if (v.DocType is null && type.NeedsCuit) add(MtxcaRule.DocumentRequired);
        else if (!type.NeedsCuit && !identified && amount >= settings.FinalConsumerIdentificationThreshold) add(MtxcaRule.DocumentRequired);
        if (type.NeedsCuit && v.DocType is { } sent && sent != 80) add(MtxcaRule.ClassADocumentType);
        if (v.DocNumber == issuer) add(MtxcaRule.SameAsIssuer);

        var classB = type.Id is 6 or 7 or 8;
        if (v.DocNumber == NotCategorized && (!classB || v.ReceiverCondition is not (null or 7))) add(MtxcaRule.NotCategorizedReceiver);

        var receiver = v.DocType == 80 && v.DocNumber is { } cuit ? await taxpayers.FindAsync(cuit, ct) : null;
        if (v.DocType is 80 or 86 or 87 && v.DocNumber is { } number && !(classB && v.DocType == 80 && number == NotCategorized)
            && (!Cuits.IsValid(number) || receiver is { Active: false }))
        {
            add(MtxcaRule.ReceiverInvalid);
        }
        if (type.Id is 1 or 2 or 3 or 51 or 52 or 53 && receiver is { Active: true })
        {
            if (receiver.VatCondition is not (VatCondition.ResponsableInscripto or VatCondition.Exento) && !receiver.VatCondition.IsMonotributo())
                add(MtxcaRule.ReceiverNotInVat);
            if (receiver.VatCondition.IsMonotributo()) add(MtxcaRule.MonotributoReceiver);
        }

        if (classB && v.DocType == 80 && v.DocNumber == NotCategorized && (v.Net ?? 0) + v.Subtotals.Sum(s => s.Amount) > 0
            && !v.OtherTaxes.Any(t => t.Code == 13 && t.Amount > 0))
        {
            add(MtxcaRule.NotCategorizedPerception);
        }

        if (v.ReceiverCondition is not { } condition || !tables.IsReceiverCondition(condition))
            add(MtxcaRule.ReceiverConditionMissing);
        else if (tables.ReceiverConditions(type).All(c => c.Code != condition))
            add(MtxcaRule.ReceiverConditionClass);
    }

    private async Task CheckCurrencyAsync(MtxcaVoucherInput v, MtxcaVoucherType type, DateOnly today, Action<MtxcaRule> add, CancellationToken ct)
    {
        if (!tables.HasCurrency(v.Currency))
        {
            add(MtxcaRule.Currency);
            return;
        }
        var pesos = v.Currency == "PES";
        if (v.SameCurrency is not null and not ("S" or "N")) add(MtxcaRule.SameCurrencyFlag);
        if (v.SameCurrency == "S")
        {
            if (!type.Invoice) add(MtxcaRule.SameCurrencyNotInvoice);
            if (pesos) add(MtxcaRule.PesosSameCurrency);
        }
        if (v.Rate is not { } rate)
        {
            if (!(v.SameCurrency == "S" && MtxcaTables.BnaCurrencies.Contains(v.Currency))) add(MtxcaRule.RateRequired);
            return;
        }
        if (rate < 0) add(MtxcaRule.RateNegative);
        if (pesos)
        {
            if (rate != 1) add(MtxcaRule.PesosRate);
            return;
        }
        if (await rates.RateAsync(v.Currency, today, ct) is { Rate: var reference } && (rate < reference * 0.02m || rate > reference * 4m))
            add(MtxcaRule.RateRange);
    }

    private static void CheckDates(MtxcaVoucherInput v, MtxcaVoucherType type, DateOnly date, Action<MtxcaRule> add)
    {
        var services = v.Concept is 2 or 3;
        if (services != (v.ServiceFrom is not null)) add(MtxcaRule.ServiceFrom);
        if (services != (v.ServiceTo is not null)) add(MtxcaRule.ServiceTo);
        if (type.Fce)
        {
            if (type.Invoice && v.PaymentDue is null) add(MtxcaRule.FcePaymentDueMissing);
            if (type.Note && v.PaymentDue is not null) add(MtxcaRule.FceNotePaymentDue);
        }
        else if (services != (v.PaymentDue is not null)) add(MtxcaRule.PaymentDue);
        if (v.PaymentDue < date) add(MtxcaRule.PaymentDueBeforeDate);
        if (v.ServiceFrom > v.ServiceTo) add(MtxcaRule.ServiceOrder);
    }

    private static void CheckAssociated(MtxcaVoucherInput v, MtxcaVoucherType type, DateOnly date, Action<MtxcaRule> add)
    {
        var plainNote = type.Id is 2 or 3 or 7 or 8 or 52 or 53;
        if (plainNote && v.Associated.Count == 0 && v.Period is null) add(MtxcaRule.NoteWithoutAssociated);
        if (plainNote && v.Associated.Count > 0 && v.Period is not null) add(MtxcaRule.NoteWithBoth);
        if (type.Id is 1 or 2 or 51 or 201 or 206 && v.Period is not null) add(MtxcaRule.InvoiceWithPeriod);
        if (type.Id is 202 or 203 or 207 or 208 && v.Period is not null) add(MtxcaRule.FceNoteWithPeriod);

        foreach (var associated in v.Associated)
        {
            if (!MtxcaTables.AssociableTypes.Contains(associated.Type)) add(MtxcaRule.AssociatedType);
            else if (!AssociableFor(type.Id).Contains(associated.Type)) add(MtxcaRule.AssociatedTypeForVoucher);
        }
        if (v.Period is { } period)
        {
            if (period.To < period.From) add(MtxcaRule.PeriodOrder);
            if (period.To > date) add(MtxcaRule.PeriodAfterDate);
        }
    }

    /// <summary>The associated types validation 200 allows for each voucher type.</summary>
    private static int[] AssociableFor(int type) => type switch
    {
        1 or 6 or 51 => [88, 990],
        2 or 3 => [1, 2, 3, 88, 990],
        7 or 8 => [6, 7, 8, 88, 990],
        52 or 53 => [51, 52, 53, 88, 990],
        201 or 206 => [88, 91, 990, 995],
        202 or 203 => [201, 202, 203, 88, 91, 990, 995],
        207 or 208 => [206, 207, 208, 88, 91, 990, 995],
        _ => [],
    };

    private void CheckOtherTaxes(MtxcaVoucherInput v, Action<MtxcaRule> add)
    {
        foreach (var tax in v.OtherTaxes)
        {
            if (!tables.HasTax(tax.Code)) add(MtxcaRule.OtherTaxCode);
            if (tax.Code == 99 && string.IsNullOrWhiteSpace(tax.Description)) add(MtxcaRule.OtherTaxDescription);
        }
        if (!Close(v.OtherTaxes.Sum(t => t.Amount), v.OtherTaxesTotal ?? 0, v.OtherTaxes.Count)) add(MtxcaRule.OtherTaxesSum);
    }

    private static decimal RateOf(MtxcaItem item) => MtxcaTables.ItemVatRates.GetValueOrDefault(item.VatCondition);

    private static void CheckItems(MtxcaVoucherInput v, MtxcaVoucherType type, Action<MtxcaRule> add)
    {
        foreach (var item in v.Items)
        {
            var special = item.Unit is 97 or 99;
            if (item.Units is null && !special) add(MtxcaRule.ItemUnitsRequired);
            if (item.Units < 1) add(MtxcaRule.ItemUnitsMin);
            if (item.Units > 999_999) add(MtxcaRule.ItemUnitsLength);
            if (item.Gtin is null && !special) add(MtxcaRule.ItemGtinRequired);
            if ((item.Units is null) != (item.Gtin is null)) add(MtxcaRule.ItemUnitsWithGtin);
            if (item.Code?.Length > 50) add(MtxcaRule.ItemCodeLength);
            if (item.Description.Length > 4000) add(MtxcaRule.ItemDescriptionLength);
            if (special == (item.Quantity is not null)) add(MtxcaRule.ItemQuantity);
            if (!MtxcaTables.HasUnit(item.Unit)) add(MtxcaRule.ItemUnit);
            if (special == (item.Price is not null)) add(MtxcaRule.ItemPrice);
            if (special && item.Discount is not null) add(MtxcaRule.ItemDiscountNotAllowed);
            if (item.Discount is { } discount && item.Price is { } p && item.Quantity is { } q && discount > p * q) add(MtxcaRule.ItemDiscountTooHigh);
            if (!MtxcaTables.ItemVatRates.ContainsKey(item.VatCondition)) add(MtxcaRule.ItemVatCondition);
            if (item.Unit == 99 && !v.Items.Any(other => other.VatCondition == item.VatCondition && other.Unit != 99)) add(MtxcaRule.ItemBonusAlone);
            if (type.ClassA != (item.Vat is not null)) add(MtxcaRule.ItemVatPresence);

            var rate = RateOf(item);
            var sign = item.Unit == 95 ? -1m : 1m;
            var net = (item.Price ?? 0) * (item.Quantity ?? 0) - (item.Discount ?? 0);
            if (item.Unit is 99 or 95 ? item.Amount >= 0 : item.Unit != 97 && item.Amount < 0) add(MtxcaRule.ItemAmountSign);

            if (type.ClassA)
            {
                if (item.Vat is { } vat)
                {
                    if (item.VatCondition is 1 or 2 or 3 && vat != 0) add(MtxcaRule.ItemVatZero);
                    var expected = special ? item.Amount - item.Amount / (1 + rate) : sign * net * rate;
                    if (item.VatCondition is 4 or 5 or 6 && !Close(expected, vat, 1)) add(MtxcaRule.ItemVatAmount);
                    if (item.Unit is 99 or 95 ? vat > 0 : item.Unit != 97 && vat < 0) add(MtxcaRule.ItemVatSign);
                }
                if (!special && !Close(sign * net * (1 + rate), item.Amount, 1)) add(MtxcaRule.ItemAmount);
            }
            else if (!special && !Close(sign * net, item.Amount, 1)) add(MtxcaRule.ItemAmount);
        }

        if (type.ClassA)
            foreach (var group in v.Items.GroupBy(i => i.VatCondition))
            {
                var bonus = Math.Abs(group.Where(i => i.Unit == 99).Sum(i => i.Vat ?? 0));
                var rest = group.Where(i => i.Unit != 99).Sum(i => i.Vat ?? 0);
                if (bonus > 0 && bonus > rest && !Close(rest, bonus, 1)) add(MtxcaRule.ItemBonusVat);
            }
    }

    /// <summary>The VAT of an item: the one sent in class A, the one inside the price in class B.</summary>
    private static decimal VatOf(MtxcaItem item, MtxcaVoucherType type) =>
        type.ClassA ? item.Vat ?? 0 : item.Amount - item.Amount / (1 + RateOf(item));

    private static void CheckSubtotals(MtxcaVoucherInput v, MtxcaVoucherType type, Action<MtxcaRule> add)
    {
        var taxedRates = v.Items.Where(i => i.VatCondition is 4 or 5 or 6).Select(i => i.VatCondition).ToHashSet();
        if (taxedRates.Count > 0 != v.HasSubtotals) add(MtxcaRule.SubtotalsRequired);
        foreach (var (code, amount) in v.Subtotals)
        {
            if (code is not (4 or 5 or 6)) add(MtxcaRule.SubtotalCode);
            var items = v.Items.Where(i => i.VatCondition == code).ToList();
            if (items.Count > 0 && !Close(items.Sum(i => VatOf(i, type)), amount, items.Count)) add(MtxcaRule.SubtotalAmount);
        }
        if (v.Subtotals.Select(s => s.Code).Distinct().Count() != v.Subtotals.Count) add(MtxcaRule.SubtotalRepeated);
        if (v.HasSubtotals && !taxedRates.SetEquals(v.Subtotals.Select(s => s.Code))) add(MtxcaRule.SubtotalPresence);
        if (v.Subtotals.Sum(s => s.Amount) < 0) add(MtxcaRule.SubtotalNegative);
    }

    private static void CheckTotals(MtxcaVoucherInput v, MtxcaVoucherType type, Action<MtxcaRule> add)
    {
        var taxed = v.Items.Where(i => i.VatCondition is 3 or 4 or 5 or 6).ToList();
        var notTaxed = v.Items.Where(i => i.VatCondition == 1).ToList();
        var exempt = v.Items.Where(i => i.VatCondition == 2).ToList();
        if (!Close(taxed.Sum(i => i.Amount - VatOf(i, type)), v.Net ?? 0, taxed.Count)) add(MtxcaRule.NetSum);
        if (!Close(notTaxed.Sum(i => i.Amount), v.NotTaxed ?? 0, notTaxed.Count)) add(MtxcaRule.NotTaxedSum);
        if (!Close(exempt.Sum(i => i.Amount), v.Exempt ?? 0, exempt.Count)) add(MtxcaRule.ExemptSum);
        if (!Close((v.NotTaxed ?? 0) + (v.Net ?? 0) + (v.Exempt ?? 0), v.Subtotal, 3)) add(MtxcaRule.SubtotalSum);
        var otherTaxes = v.OtherTaxesTotal ?? 0;
        if (!Close(v.Subtotal + otherTaxes + v.Subtotals.Sum(s => s.Amount), v.Total, 2 + v.Subtotals.Count)) add(MtxcaRule.TotalSum);
        if (!Close(otherTaxes + v.Items.Sum(i => i.Amount), v.Total, 1 + v.Items.Count)) add(MtxcaRule.TotalItems);
    }

    private static void CheckBuyers(MtxcaVoucherInput v, MtxcaVoucherType type, bool caea, Action<MtxcaRule> add)
    {
        if (v.Buyers.Count == 0) return;
        if (caea)
        {
            add(MtxcaRule.BuyersNotAllowed);
            return;
        }
        if (v.Buyers.Count == 1) add(MtxcaRule.BuyersSingle);
        if (v.Buyers.Any(b => b.DocType is not (80 or 86 or 87))) add(MtxcaRule.BuyerDocType);
        if (v.Buyers.Any(b => b.Percentage <= 0)) add(MtxcaRule.BuyerPercentPositive);
        if (v.Buyers.Any(b => b.Percentage >= 100)) add(MtxcaRule.BuyerPercentBelow100);
        if (v.Buyers.Sum(b => b.Percentage) != 100) add(MtxcaRule.BuyersSum);
        if (v.Concept != 1) add(MtxcaRule.BuyersConcept);
        if (type.Fce) add(MtxcaRule.BuyersFce);
    }
}
