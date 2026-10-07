using Arca.Client;

namespace ArcaSim.Tests.Support;

internal static class Vouchers
{
    /// <summary>A class B invoice to an unidentified final consumer: the net plus 21 % VAT, $121 for a net of $100.</summary>
    public static Voucher ConsumerInvoice(decimal net = 100m) => new()
    {
        Concept = 1,
        DocumentType = 99,
        DocumentNumber = 0,
        Total = net * 1.21m,
        Net = net,
        Vat = net * 0.21m,
        ReceiverVatCondition = 5,
        VatLines = [new VatLine(5, net, net * 0.21m)],
    };
}
