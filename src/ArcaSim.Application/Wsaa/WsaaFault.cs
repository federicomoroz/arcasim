namespace ArcaSim.Application.Wsaa;

/// <summary>
/// A WSAA error, with the faultstring ARCA really sends. Where the real text was
/// captured it wins over the specification, accents and typos included
/// (docs/arca/wsaa.md §6.2); the rest use the specification's text (§6.1).
/// </summary>
public sealed record WsaaFault(string Code, string Message)
{
    public static readonly WsaaFault BadBase64 = new("cms.bad.base64", "No se puede decodificar el BASE64");
    public static readonly WsaaFault BadCms = new("cms.bad", "El CMS no es valido");
    public static readonly WsaaFault CertificateNotFound = new("cms.cert.notFound", "No se ha encontrado certificado de firmador");
    public static readonly WsaaFault InvalidSignature = new("cms.sign.invalid", "Firma inválida o algoritmo no soportado");
    public static readonly WsaaFault CertificateExpired = new("cms.cert.expired", "Certificado expirado");
    public static readonly WsaaFault CertificateNotYetValid = new("cms.cert.invalid", "Certificado con fecha de generacion posterior a la actual");
    public static readonly WsaaFault CertificateUntrusted = new("cms.cert.untrusted", "Certificado no emitido por AC de confianza");
    public static readonly WsaaFault BadXml = new("xml.bad", "No se ha podido interpretar el XML contra el SCHEMA");
    public static readonly WsaaFault BadSource = new("xml.source.invalid", "El atributo 'source' no se corresponde con el DN del Certificado");
    public static readonly WsaaFault BadDestination = new("xml.destination.invalid", "El atributo 'destination' no se corresponde con el DN del WSAA");
    public static readonly WsaaFault VersionNotSupported = new("xml.version.notSupported", "La versión del documento no es soportada");
    public static readonly WsaaFault BadGenerationTime = new("xml.generationTime.invalid", "generationTime posee formato o dato inválido (ej: en el futuro o más de 24 horas de antigüedad)");
    public static readonly WsaaFault Expired = new("xml.expirationTime.expired", "El tiempo de expiración es inferior a la hora actual");
    public static readonly WsaaFault BadExpirationTime = new("xml.expirationTime.invalid", "El tiempo de expiración del documento es superior a 24 horas");
    public static readonly WsaaFault ServiceNotFound = new("wsn.notFound", "Servicio informado inexistente");
    public static readonly WsaaFault NotAuthorized = new("coe.notAuthorized", "Computador no autorizado a acceder al servicio");
    public static readonly WsaaFault AlreadyAuthenticated = new("coe.alreadyAuthenticated", "El CEE ya posee un TA valido para el acceso al WSN solicitado");
    public static readonly WsaaFault ServiceUnavailable = new("wsn.unavailable", "El servicio al que se desea acceder se encuentra momentáneamente fuera de servicio");
    public static readonly WsaaFault WsaaUnavailable = new("wsaa.unavailable", "El servicio de autenticación/autorización se encuentra momentáneamente fuera de servicio");
    public static readonly WsaaFault InternalError = new("wsaa.internalError", "No se ha podido procesar el requerimiento");

    public static IReadOnlyList<WsaaFault> All { get; } =
    [
        BadBase64, BadCms, CertificateNotFound, InvalidSignature, CertificateExpired, CertificateNotYetValid,
        CertificateUntrusted, BadXml, BadSource, BadDestination, VersionNotSupported, BadGenerationTime, Expired,
        BadExpirationTime, ServiceNotFound, NotAuthorized, AlreadyAuthenticated, ServiceUnavailable, WsaaUnavailable,
        InternalError,
    ];
}

/// <summary>Either the TA as text (what goes in loginCmsReturn) or the fault.</summary>
public sealed record LoginResult(string? TicketXml, WsaaFault? Fault)
{
    public static LoginResult Ok(string ticketXml) => new(ticketXml, null);

    public static LoginResult Fail(WsaaFault fault) => new(null, fault);
}
