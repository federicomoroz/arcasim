using ArcaSim.Domain;

namespace ArcaSim.Application.Wsfe;

/// <summary>The issuer of a voucher, for every invoicing service: one rule for what open access creates.</summary>
public static class Issuers
{
    /// <summary>
    /// The issuer, as ArcaSim knows it. With open access, a CUIT it has not seen
    /// is created on the spot (Monotributo if its first voucher is class C,
    /// Responsable Inscripto otherwise), and so is the point of sale it uses,
    /// so an application needs nothing but ARCA's endpoints.
    /// </summary>
    public static async Task<Taxpayer?> FindOrOpenAsync(
        this ITaxpayerRepository taxpayers, SimulationSettings settings, long cuit, int? pointOfSale, PointOfSaleKind kind,
        VoucherClass? firstClass, CancellationToken ct)
    {
        var issuer = await taxpayers.FindAsync(cuit, ct);
        if (!settings.OpenAccess) return issuer;

        var changed = false;
        if (issuer is null)
        {
            if (!Cuits.IsValid(cuit)) return null;
            var condition = firstClass == VoucherClass.C ? VatCondition.Monotributo : VatCondition.ResponsableInscripto;
            issuer = new Taxpayer(cuit, $"Contribuyente {cuit}", condition);
            changed = true;
        }
        if (pointOfSale is >= 1 and <= VoucherLimits.MaxPointOfSale && issuer.FindPointOfSale(pointOfSale.Value) is null)
        {
            issuer.AddPointOfSale(new PointOfSale(pointOfSale.Value, kind));
            changed = true;
        }
        if (changed) await taxpayers.SaveAsync(issuer, ct);
        return issuer;
    }
}
