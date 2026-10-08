using System.Text.Json;
using ArcaSim.Application.Services.Mtxca;
using ArcaSim.Application.Services.TurismoBonos;

namespace ArcaSim.Tests.Services.TurismoBonos;

/// <summary>
/// The vouchers these services keep are read back by every later run, so a
/// document written when they still carried processedAt (which nothing ever
/// read) must still read, with the stores' own JSON settings.
/// </summary>
public class StoredVouchersTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void A_BookedVoucher_written_with_processedAt_still_reads()
    {
        const string stored = """
            {"service":"wsbfev1","cuit":20111111112,"pointOfSale":5,"voucherType":1,"number":1,"requestId":7,"date":"2026-10-01","sentDate":"20261001",
             "cae":"70000000000001","caeDue":"2026-10-11","result":"A","notes":[{"code":22,"text":"x"}],"detail":"<Cmp/>","processedAt":"2026-10-01T12:00:00-03:00"}
            """;

        var voucher = JsonSerializer.Deserialize<BookedVoucher>(stored, Json)!;

        Assert.Equal(7, voucher.RequestId);
        Assert.Equal("70000000000001", voucher.Cae);
        Assert.Equal(22, Assert.Single(voucher.Notes).Code);
        Assert.Equal("<Cmp/>", voucher.Detail);
    }

    [Fact]
    public void A_MtxcaVoucher_written_with_processedAt_still_reads()
    {
        const string stored = """
            {"cuit":20111111112,"pointOfSale":1,"voucherType":1,"number":1,"date":"2026-10-01","authorizationType":"E","authorizationCode":70000000000001,
             "authorizationDue":"2026-10-11","processedAt":"2026-10-01T15:00:00+00:00","voucher":"<comprobante/>","observations":[]}
            """;

        var voucher = JsonSerializer.Deserialize<MtxcaVoucher>(stored, Json)!;

        Assert.Equal(70000000000001, voucher.AuthorizationCode);
        Assert.Equal(new DateOnly(2026, 10, 11), voucher.AuthorizationDue);
        Assert.Equal("<comprobante/>", voucher.Voucher);
        Assert.Empty(voucher.Observations);
    }
}
