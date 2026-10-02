namespace ArcaSim.Api.Admin;

// What ArcaSim adds on top of ARCA, under /arcasim/api, one controller per
// resource. ARCA's own endpoints are not here: they answer through the SOAP
// layer, which writes ARCA's exact bytes (Soap/).

public static class AdminRoutes
{
    public const string Prefix = "arcasim/api";
}
