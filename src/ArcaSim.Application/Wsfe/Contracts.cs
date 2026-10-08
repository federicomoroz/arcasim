using System.Xml.Serialization;

// WSFEv1's wire contract, type by type from docs/arca/wsdl/wsfev1-homologacion.wsdl.
// Class and member names are ARCA's element names, so they stay in Spanish: they
// are the protocol, not ArcaSim's vocabulary. Members are declared in schema
// order because XmlSerializer writes them in declaration order, base class first,
// which is what ARCA's ASMX host does too.
namespace ArcaSim.Application.Wsfe;

public static class Fev1
{
    public const string Namespace = "http://ar.gov.afip.dif.FEV1/";
}

[XmlType(Namespace = Fev1.Namespace)]
public class FEAuthRequest
{
    public string? Token { get; set; }
    public string? Sign { get; set; }
    public long Cuit { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class Err
{
    public int Code { get; set; }
    public string? Msg { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class Evt
{
    public int Code { get; set; }
    public string? Msg { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class Obs
{
    public int Code { get; set; }
    public string? Msg { get; set; }
}

// ---- Requests --------------------------------------------------------------

[XmlType(Namespace = Fev1.Namespace)]
public class FECabRequest
{
    public int CantReg { get; set; }
    public int PtoVta { get; set; }
    public int CbteTipo { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FECAECabRequest : FECabRequest;

[XmlType(Namespace = Fev1.Namespace)]
public class FECAEACabRequest : FECabRequest;

[XmlType(Namespace = Fev1.Namespace)]
public class CbteAsoc
{
    public int Tipo { get; set; }
    public int PtoVta { get; set; }
    public long Nro { get; set; }
    public string? Cuit { get; set; }
    public string? CbteFch { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class Tributo
{
    public short Id { get; set; }
    public string? Desc { get; set; }
    public double BaseImp { get; set; }
    public double Alic { get; set; }
    public double Importe { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class AlicIva
{
    public int Id { get; set; }
    public double BaseImp { get; set; }
    public double Importe { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class Opcional
{
    public string? Id { get; set; }
    public string? Valor { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class Comprador
{
    public int DocTipo { get; set; }
    public long DocNro { get; set; }
    public double Porcentaje { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class Periodo
{
    public string? FchDesde { get; set; }
    public string? FchHasta { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class Actividad
{
    public long Id { get; set; }
}

/// <summary>
/// One voucher. MonCotiz and CondicionIVAReceptorId are minOccurs="0" value
/// types, which ASMX models with a "Specified" flag: absent is not the same as 0.
/// </summary>
[XmlType(Namespace = Fev1.Namespace)]
public class FEDetRequest
{
    public int Concepto { get; set; }
    public int DocTipo { get; set; }
    public long DocNro { get; set; }
    public long CbteDesde { get; set; }
    public long CbteHasta { get; set; }
    public string? CbteFch { get; set; }
    public double ImpTotal { get; set; }
    public double ImpTotConc { get; set; }
    public double ImpNeto { get; set; }
    public double ImpOpEx { get; set; }
    public double ImpTrib { get; set; }
    public double ImpIVA { get; set; }
    public string? FchServDesde { get; set; }
    public string? FchServHasta { get; set; }
    public string? FchVtoPago { get; set; }
    public string? MonId { get; set; }
    public double MonCotiz { get; set; }
    [XmlIgnore] public bool MonCotizSpecified { get; set; }
    public string? CanMisMonExt { get; set; }
    public int CondicionIVAReceptorId { get; set; }
    [XmlIgnore] public bool CondicionIVAReceptorIdSpecified { get; set; }
    [XmlArrayItem("CbteAsoc")] public CbteAsoc[]? CbtesAsoc { get; set; }
    [XmlArrayItem("Tributo")] public Tributo[]? Tributos { get; set; }
    [XmlArrayItem("AlicIva")] public AlicIva[]? Iva { get; set; }
    [XmlArrayItem("Opcional")] public Opcional[]? Opcionales { get; set; }
    [XmlArrayItem("Comprador")] public Comprador[]? Compradores { get; set; }
    public Periodo? PeriodoAsoc { get; set; }
    [XmlArrayItem("Actividad")] public Actividad[]? Actividades { get; set; }

    [XmlIgnore]
    public double? Exchange => MonCotizSpecified ? MonCotiz : null;

    [XmlIgnore]
    public int? ReceiverVatCondition => CondicionIVAReceptorIdSpecified ? CondicionIVAReceptorId : null;

    /// <summary>Copies every field of the request into another instance, for FECompConsultar's echo.</summary>
    public void CopyTo(FEDetRequest target)
    {
        target.Concepto = Concepto;
        target.DocTipo = DocTipo;
        target.DocNro = DocNro;
        target.CbteDesde = CbteDesde;
        target.CbteHasta = CbteHasta;
        target.CbteFch = CbteFch;
        target.ImpTotal = ImpTotal;
        target.ImpTotConc = ImpTotConc;
        target.ImpNeto = ImpNeto;
        target.ImpOpEx = ImpOpEx;
        target.ImpTrib = ImpTrib;
        target.ImpIVA = ImpIVA;
        target.FchServDesde = FchServDesde;
        target.FchServHasta = FchServHasta;
        target.FchVtoPago = FchVtoPago;
        target.MonId = MonId;
        target.MonCotiz = MonCotiz;
        target.MonCotizSpecified = MonCotizSpecified;
        target.CanMisMonExt = CanMisMonExt;
        target.CondicionIVAReceptorId = CondicionIVAReceptorId;
        target.CondicionIVAReceptorIdSpecified = CondicionIVAReceptorIdSpecified;
        target.CbtesAsoc = CbtesAsoc;
        target.Tributos = Tributos;
        target.Iva = Iva;
        target.Opcionales = Opcionales;
        target.Compradores = Compradores;
        target.PeriodoAsoc = PeriodoAsoc;
        target.Actividades = Actividades;
    }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FECAEDetRequest : FEDetRequest;

[XmlType(Namespace = Fev1.Namespace)]
public class FECAEADetRequest : FEDetRequest
{
    public string? CAEA { get; set; }
    public string? CbteFchHsGen { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FECAERequest
{
    public FECAECabRequest? FeCabReq { get; set; }
    [XmlArrayItem("FECAEDetRequest")] public FECAEDetRequest[]? FeDetReq { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FECAEARequest
{
    public FECAEACabRequest? FeCabReq { get; set; }
    [XmlArrayItem("FECAEADetRequest")] public FECAEADetRequest[]? FeDetReq { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FECompConsultaReq
{
    public int CbteTipo { get; set; }
    public long CbteNro { get; set; }
    public int PtoVta { get; set; }
}

/// <summary>The body of an operation: its element holds Auth and the operation's own parameters.</summary>
public abstract class OperationRequest
{
    public FEAuthRequest? Auth { get; set; }
}

[XmlRoot("FEDummy", Namespace = Fev1.Namespace)]
public class FEDummyRequest;

/// <summary>The eleven operations that take nothing but Auth share this shape under their own element name.</summary>
[XmlType(Namespace = Fev1.Namespace)]
public class AuthOnlyRequest : OperationRequest;

[XmlRoot("FECAESolicitar", Namespace = Fev1.Namespace)]
public class FECAESolicitarRequest : OperationRequest
{
    public FECAERequest? FeCAEReq { get; set; }
}

[XmlRoot("FECompUltimoAutorizado", Namespace = Fev1.Namespace)]
public class FECompUltimoAutorizadoRequest : OperationRequest
{
    public int PtoVta { get; set; }
    public int CbteTipo { get; set; }
}

[XmlRoot("FECompConsultar", Namespace = Fev1.Namespace)]
public class FECompConsultarRequest : OperationRequest
{
    public FECompConsultaReq? FeCompConsReq { get; set; }
}

[XmlRoot("FECAEARegInformativo", Namespace = Fev1.Namespace)]
public class FECAEARegInformativoRequest : OperationRequest
{
    public FECAEARequest? FeCAEARegInfReq { get; set; }
}

/// <summary>FECAEASolicitar and FECAEAConsultar take the same parameters.</summary>
[XmlType(Namespace = Fev1.Namespace)]
public class CaeaPeriodRequest : OperationRequest
{
    public int Periodo { get; set; }
    public short Orden { get; set; }
}

[XmlRoot("FECAEASinMovimientoConsultar", Namespace = Fev1.Namespace)]
public class FECAEASinMovimientoConsultarRequest : OperationRequest
{
    public string? CAEA { get; set; }
    public int PtoVta { get; set; }
}

[XmlRoot("FECAEASinMovimientoInformar", Namespace = Fev1.Namespace)]
public class FECAEASinMovimientoInformarRequest : OperationRequest
{
    public int PtoVta { get; set; }
    public string? CAEA { get; set; }
}

[XmlRoot("FEParamGetCotizacion", Namespace = Fev1.Namespace)]
public class FEParamGetCotizacionRequest : OperationRequest
{
    public string? MonId { get; set; }
    public string? FchCotiz { get; set; }
}

[XmlRoot("FEParamGetCondicionIvaReceptor", Namespace = Fev1.Namespace)]
public class FEParamGetCondicionIvaReceptorRequest : OperationRequest
{
    public string? ClaseCmp { get; set; }
}

// ---- Responses -------------------------------------------------------------

/// <summary>A result that can carry Errors. Each result declares where they go, since the order differs between types.</summary>
public interface IHasErrors
{
    Err[]? Errors { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class DummyResponse
{
    public string? AppServer { get; set; }
    public string? DbServer { get; set; }
    public string? AuthServer { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FERecuperaLastCbteResponse : IHasErrors
{
    public int PtoVta { get; set; }
    public int CbteTipo { get; set; }
    public int CbteNro { get; set; }
    [XmlArrayItem("Err")] public Err[]? Errors { get; set; }
    [XmlArrayItem("Evt")] public Evt[]? Events { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FERegXReqResponse : IHasErrors
{
    public int RegXReq { get; set; }
    [XmlArrayItem("Err")] public Err[]? Errors { get; set; }
    [XmlArrayItem("Evt")] public Evt[]? Events { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FECabResponse
{
    public long Cuit { get; set; }
    public int PtoVta { get; set; }
    public int CbteTipo { get; set; }
    public string? FchProceso { get; set; }
    public int CantReg { get; set; }
    public string? Resultado { get; set; }
    public string? Reproceso { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FECAECabResponse : FECabResponse;

[XmlType(Namespace = Fev1.Namespace)]
public class FECAEACabResponse : FECabResponse;

[XmlType(Namespace = Fev1.Namespace)]
public class FEDetResponse
{
    public int Concepto { get; set; }
    public int DocTipo { get; set; }
    public long DocNro { get; set; }
    public long CbteDesde { get; set; }
    public long CbteHasta { get; set; }
    public string? CbteFch { get; set; }
    public string? Resultado { get; set; }
    [XmlArrayItem("Obs")] public Obs[]? Observaciones { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FECAEDetResponse : FEDetResponse
{
    public string? CAE { get; set; }
    public string? CAEFchVto { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FECAEADetResponse : FEDetResponse
{
    public string? CAEA { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FECAEResponse : IHasErrors
{
    public FECAECabResponse? FeCabResp { get; set; }
    [XmlArrayItem("FECAEDetResponse")] public FECAEDetResponse[]? FeDetResp { get; set; }
    [XmlArrayItem("Evt")] public Evt[]? Events { get; set; }
    [XmlArrayItem("Err")] public Err[]? Errors { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FECAEAResponse : IHasErrors
{
    public FECAEACabResponse? FeCabResp { get; set; }
    [XmlArrayItem("FECAEADetResponse")] public FECAEADetResponse[]? FeDetResp { get; set; }
    [XmlArrayItem("Evt")] public Evt[]? Events { get; set; }
    [XmlArrayItem("Err")] public Err[]? Errors { get; set; }
}

/// <summary>A stored voucher as FECompConsultar returns it: the request's fields, then how it was authorized.</summary>
[XmlType(Namespace = Fev1.Namespace)]
public class FECompConsResponse : FECAEDetRequest
{
    public string? Resultado { get; set; }
    public string? CodAutorizacion { get; set; }
    public string? EmisionTipo { get; set; }
    public string? FchVto { get; set; }
    public string? FchProceso { get; set; }
    [XmlArrayItem("Obs")] public Obs[]? Observaciones { get; set; }
    public int PtoVta { get; set; }
    public int CbteTipo { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FECompConsultaResponse : IHasErrors
{
    public FECompConsResponse? ResultGet { get; set; }
    [XmlArrayItem("Err")] public Err[]? Errors { get; set; }
    [XmlArrayItem("Evt")] public Evt[]? Events { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FECAEAGet
{
    public string? CAEA { get; set; }
    public int Periodo { get; set; }
    public short Orden { get; set; }
    public string? FchVigDesde { get; set; }
    public string? FchVigHasta { get; set; }
    public string? FchTopeInf { get; set; }
    public string? FchProceso { get; set; }
    [XmlArrayItem("Obs")] public Obs[]? Observaciones { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FECAEAGetResponse : IHasErrors
{
    public FECAEAGet? ResultGet { get; set; }
    [XmlArrayItem("Err")] public Err[]? Errors { get; set; }
    [XmlArrayItem("Evt")] public Evt[]? Events { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FECAEASinMov
{
    public string? CAEA { get; set; }
    public string? FchProceso { get; set; }
    public int PtoVta { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FECAEASinMovResponse : FECAEASinMov, IHasErrors
{
    public string? Resultado { get; set; }
    [XmlArrayItem("Err")] public Err[]? Errors { get; set; }
    [XmlArrayItem("Evt")] public Evt[]? Events { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FECAEASinMovConsResponse : IHasErrors
{
    [XmlArrayItem("FECAEASinMov")] public FECAEASinMov[]? ResultGet { get; set; }
    [XmlArrayItem("Err")] public Err[]? Errors { get; set; }
    [XmlArrayItem("Evt")] public Evt[]? Events { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class Cotizacion
{
    public string? MonId { get; set; }
    public double MonCotiz { get; set; }
    public string? FchCotiz { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FECotizacionResponse : IHasErrors
{
    public Cotizacion? ResultGet { get; set; }
    [XmlArrayItem("Err")] public Err[]? Errors { get; set; }
    [XmlArrayItem("Evt")] public Evt[]? Events { get; set; }
}

/// <summary>The parameter tables: a ResultGet list, then Errors and Events. Each type declares them itself to keep that order.</summary>
public interface IParameterResponse<TItem> : IHasErrors
{
    TItem[]? ResultGet { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class CbteTipo
{
    public int Id { get; set; }
    public string? Desc { get; set; }
    public string? FchDesde { get; set; }
    public string? FchHasta { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class ConceptoTipo
{
    public int Id { get; set; }
    public string? Desc { get; set; }
    public string? FchDesde { get; set; }
    public string? FchHasta { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class DocTipo
{
    public int Id { get; set; }
    public string? Desc { get; set; }
    public string? FchDesde { get; set; }
    public string? FchHasta { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class IvaTipo
{
    public string? Id { get; set; }
    public string? Desc { get; set; }
    public string? FchDesde { get; set; }
    public string? FchHasta { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class Moneda
{
    public string? Id { get; set; }
    public string? Desc { get; set; }
    public string? FchDesde { get; set; }
    public string? FchHasta { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class OpcionalTipo
{
    public string? Id { get; set; }
    public string? Desc { get; set; }
    public string? FchDesde { get; set; }
    public string? FchHasta { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class TributoTipo
{
    public short Id { get; set; }
    public string? Desc { get; set; }
    public string? FchDesde { get; set; }
    public string? FchHasta { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class PaisTipo
{
    public short Id { get; set; }
    public string? Desc { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class PtoVenta
{
    public int Nro { get; set; }
    public string? EmisionTipo { get; set; }
    public string? Bloqueado { get; set; }
    public string? FchBaja { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class ActividadesTipo
{
    public long Id { get; set; }
    public short Orden { get; set; }
    public string? Desc { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class CondicionIvaReceptor
{
    public int Id { get; set; }
    public string? Desc { get; set; }
    public string? Cmp_Clase { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class CbteTipoResponse : IParameterResponse<CbteTipo>
{
    [XmlArrayItem("CbteTipo")] public CbteTipo[]? ResultGet { get; set; }
    [XmlArrayItem("Err")] public Err[]? Errors { get; set; }
    [XmlArrayItem("Evt")] public Evt[]? Events { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class ConceptoTipoResponse : IParameterResponse<ConceptoTipo>
{
    [XmlArrayItem("ConceptoTipo")] public ConceptoTipo[]? ResultGet { get; set; }
    [XmlArrayItem("Err")] public Err[]? Errors { get; set; }
    [XmlArrayItem("Evt")] public Evt[]? Events { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class DocTipoResponse : IParameterResponse<DocTipo>
{
    [XmlArrayItem("DocTipo")] public DocTipo[]? ResultGet { get; set; }
    [XmlArrayItem("Err")] public Err[]? Errors { get; set; }
    [XmlArrayItem("Evt")] public Evt[]? Events { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class IvaTipoResponse : IParameterResponse<IvaTipo>
{
    [XmlArrayItem("IvaTipo")] public IvaTipo[]? ResultGet { get; set; }
    [XmlArrayItem("Err")] public Err[]? Errors { get; set; }
    [XmlArrayItem("Evt")] public Evt[]? Events { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class MonedaResponse : IParameterResponse<Moneda>
{
    [XmlArrayItem("Moneda")] public Moneda[]? ResultGet { get; set; }
    [XmlArrayItem("Err")] public Err[]? Errors { get; set; }
    [XmlArrayItem("Evt")] public Evt[]? Events { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class OpcionalTipoResponse : IParameterResponse<OpcionalTipo>
{
    [XmlArrayItem("OpcionalTipo")] public OpcionalTipo[]? ResultGet { get; set; }
    [XmlArrayItem("Err")] public Err[]? Errors { get; set; }
    [XmlArrayItem("Evt")] public Evt[]? Events { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FETributoResponse : IParameterResponse<TributoTipo>
{
    [XmlArrayItem("TributoTipo")] public TributoTipo[]? ResultGet { get; set; }
    [XmlArrayItem("Err")] public Err[]? Errors { get; set; }
    [XmlArrayItem("Evt")] public Evt[]? Events { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FEPaisResponse : IParameterResponse<PaisTipo>
{
    [XmlArrayItem("PaisTipo")] public PaisTipo[]? ResultGet { get; set; }
    [XmlArrayItem("Err")] public Err[]? Errors { get; set; }
    [XmlArrayItem("Evt")] public Evt[]? Events { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FEPtoVentaResponse : IParameterResponse<PtoVenta>
{
    [XmlArrayItem("PtoVenta")] public PtoVenta[]? ResultGet { get; set; }
    [XmlArrayItem("Err")] public Err[]? Errors { get; set; }
    [XmlArrayItem("Evt")] public Evt[]? Events { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class FEActividadesResponse : IParameterResponse<ActividadesTipo>
{
    [XmlArrayItem("ActividadesTipo")] public ActividadesTipo[]? ResultGet { get; set; }
    [XmlArrayItem("Err")] public Err[]? Errors { get; set; }
    [XmlArrayItem("Evt")] public Evt[]? Events { get; set; }
}

[XmlType(Namespace = Fev1.Namespace)]
public class CondicionIvaReceptorResponse : IParameterResponse<CondicionIvaReceptor>
{
    [XmlArrayItem("CondicionIvaReceptor")] public CondicionIvaReceptor[]? ResultGet { get; set; }
    [XmlArrayItem("Err")] public Err[]? Errors { get; set; }
    [XmlArrayItem("Evt")] public Evt[]? Events { get; set; }
}
