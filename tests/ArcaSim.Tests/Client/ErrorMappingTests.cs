using System.Net;
using Arca.Client;

namespace ArcaSim.Tests.Client;

/// <summary>
/// What a failure is to the caller: ARCA down or busy (try again), a request ARCA cannot read (do not),
/// or an answer the client cannot read (do not either). The answers come from a stub, because ArcaSim
/// only fails the way a test asks it to.
/// </summary>
public class ErrorMappingTests
{
    // ---- WSFEv1 ----------------------------------------------------------------------------

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "")]
    [InlineData(HttpStatusCode.Unauthorized, "")]
    [InlineData(HttpStatusCode.Forbidden, "<html><body>Access denied by the proxy</body></html>")]
    [InlineData(HttpStatusCode.NotFound, "<html><head><title>404</title></head><body>Not found<br></body></html>")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "{\"error\":\"nope\"}")]
    public async Task A_client_error_is_a_request_ARCA_refused_and_is_not_retryable(HttpStatusCode status, string body)
    {
        using var rig = new ClientRig();
        rig.Arca.OnWsfe = _ => Task.FromResult(StubArca.Text(status, body, "text/html"));

        var failure = await Assert.ThrowsAsync<ArcaBadRequestException>(() => rig.Wsfe.DummyAsync());

        Assert.False(failure.Retryable);
        Assert.Equal((int)status, failure.HttpStatus);
        Assert.Contains($"HTTP {(int)status}", failure.Message);
    }

    [Theory]
    [InlineData("System.Web.Services.Protocols.SoapException: Server was unable to read request. ---&gt; System.InvalidOperationException: There is an error in XML document (1, 2).")]
    [InlineData("System.Web.Services.Protocols.SoapException: Server did not recognize the value of HTTP Header SOAPAction: http://ar.gov.afip.dif.FEV1/FEXXX.")]
    [InlineData("Unable to handle request without a valid action parameter. Please supply a valid soap action.")]
    public async Task A_fault_that_says_the_request_could_not_be_read_is_not_retryable(string faultString)
    {
        using var rig = new ClientRig();
        rig.Arca.OnWsfe = _ => Task.FromResult(StubArca.Fault(faultString));

        var failure = await Assert.ThrowsAsync<ArcaBadRequestException>(() => rig.Wsfe.DummyAsync());

        Assert.False(failure.Retryable);
        Assert.Equal(500, failure.HttpStatus);
        Assert.Contains(WebUtility.HtmlDecode(faultString), failure.Message);
    }

    [Fact]
    public async Task The_same_fault_travelling_with_HTTP_200_is_read_the_same_way()
    {
        using var rig = new ClientRig();
        rig.Arca.OnWsfe = _ => Task.FromResult(StubArca.Soap(
            "<soap:Fault><faultcode>soap:Client</faultcode><faultstring>Server was unable to read request.</faultstring></soap:Fault>"));

        var failure = await Assert.ThrowsAsync<ArcaBadRequestException>(() => rig.Wsfe.DummyAsync());

        Assert.False(failure.Retryable);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task A_server_error_or_a_request_to_try_later_is_retryable(HttpStatusCode status)
    {
        using var rig = new ClientRig();
        rig.Arca.OnWsfe = _ => Task.FromResult(StubArca.Text(status, "<html><body>Try later</body></html>", "text/html"));

        var failure = await Assert.ThrowsAsync<ArcaUnavailableException>(() => rig.Wsfe.DummyAsync());

        Assert.True(failure.Retryable);
        Assert.Contains($"HTTP {(int)status}", failure.Message);
    }

    [Fact]
    public async Task A_fault_about_anything_else_is_retryable_and_says_what_it_was()
    {
        using var rig = new ClientRig();
        rig.Arca.OnWsfe = _ => Task.FromResult(StubArca.Fault("System.Web.Services.Protocols.SoapException: Server Error"));

        var failure = await Assert.ThrowsAsync<ArcaUnavailableException>(() => rig.Wsfe.DummyAsync());

        Assert.True(failure.Retryable);
        Assert.Contains("HTTP 500: System.Web.Services.Protocols.SoapException: Server Error", failure.Message);
    }

    [Fact]
    public async Task A_timeout_is_retryable()
    {
        using var rig = new ClientRig();
        rig.Arca.OnWsfe = _ => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.", new TimeoutException());

        var failure = await Assert.ThrowsAsync<ArcaUnavailableException>(() => rig.Wsfe.DummyAsync());

        Assert.True(failure.Retryable);
        Assert.IsType<TaskCanceledException>(failure.InnerException);
    }

    [Fact]
    public async Task A_connection_that_fails_is_retryable()
    {
        using var rig = new ClientRig();
        rig.Arca.OnWsfe = _ => throw new HttpRequestException("No connection could be made because the target machine actively refused it.");

        var failure = await Assert.ThrowsAsync<ArcaUnavailableException>(() => rig.Wsfe.DummyAsync());

        Assert.True(failure.Retryable);
        Assert.IsType<HttpRequestException>(failure.InnerException);
    }

    [Fact]
    public async Task A_caller_that_cancels_gets_the_cancellation_not_a_failure_of_ARCA()
    {
        using var rig = new ClientRig();
        using var cancel = new CancellationTokenSource();
        rig.Arca.OnWsfe = async _ =>
        {
            await cancel.CancelAsync();
            await Task.Delay(Timeout.Infinite, cancel.Token);
            return StubArca.Soap("");
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rig.Wsfe.DummyAsync(cancel.Token));
    }

    [Fact]
    public async Task An_answer_that_is_not_XML_is_retryable()
    {
        using var rig = new ClientRig();
        rig.Arca.OnWsfe = _ => Task.FromResult(StubArca.Text(HttpStatusCode.OK, "<html>maintenance", "text/html"));

        var failure = await Assert.ThrowsAsync<ArcaUnavailableException>(() => rig.Wsfe.DummyAsync());

        Assert.Contains("not XML", failure.Message);
    }

    [Theory]
    [InlineData("<CbteNro>forty-two</CbteNro>")]
    [InlineData("")]
    [InlineData("<CbteNro>1</CbteNro><Errors><Err><Code>x</Code><Msg>y</Msg></Err></Errors>")]
    public async Task A_number_that_is_not_one_is_an_answer_the_client_cannot_read(string inner)
    {
        using var rig = new ClientRig();
        rig.Arca.OnWsfe = _ => Task.FromResult(StubArca.Soap(StubArca.Result("FECompUltimoAutorizado", inner)));

        var failure = await Assert.ThrowsAsync<ArcaBadResponseException>(() => rig.Wsfe.LastAuthorizedAsync(1, 6));

        Assert.False(failure.Retryable);
        Assert.Contains("cannot be read", failure.Message);
    }

    [Fact]
    public async Task An_amount_or_a_date_that_does_not_parse_is_an_answer_the_client_cannot_read()
    {
        using var rig = new ClientRig();
        rig.Arca.OnWsfe = _ => Task.FromResult(StubArca.Soap(StubArca.Result("FECompConsultar", StubArca.Authorized(5, 121m).Replace("<ImpTotal>121</ImpTotal>", "<ImpTotal>a lot</ImpTotal>"))));

        var failure = await Assert.ThrowsAsync<ArcaBadResponseException>(() => rig.Wsfe.QueryAsync(1, 6, 5));

        Assert.Contains("ImpTotal", failure.Message);
    }

    [Fact]
    public async Task When_the_answer_to_an_authorization_cannot_be_read_the_voucher_is_looked_up_before_giving_up()
    {
        using var rig = new ClientRig();
        rig.Arca.LastVoucher = 9;
        rig.Arca.OnWsfe = call => Task.FromResult(call.Operation switch
        {
            "FECompUltimoAutorizado" => StubArca.Soap(StubArca.Result(call.Operation, "<PtoVta>1</PtoVta><CbteTipo>6</CbteTipo><CbteNro>9</CbteNro>")),
            "FECAESolicitar" => StubArca.Soap(StubArca.Result(call.Operation, StubArca.Approved(call.Request).Replace("<CbteFch>20261001</CbteFch>", "<CbteFch>yesterday</CbteFch>"))),
            "FECompConsultar" => StubArca.Soap(StubArca.Result(call.Operation, StubArca.Authorized(10, 121m))),
            _ => throw new InvalidOperationException(call.Operation),
        });

        var result = await rig.Wsfe.AuthorizeNextAsync(1, 6, new Voucher { Concept = 1, DocumentType = 99, DocumentNumber = 0, Total = 121m });

        Assert.True(result.Approved);
        Assert.True(result.Recovered);
        Assert.Equal(10, result.Number);
        Assert.Equal("71263951827464", result.Cae);
    }

    [Fact]
    public async Task When_the_answer_cannot_be_read_and_the_voucher_is_not_there_the_failure_is_the_original()
    {
        using var rig = new ClientRig();
        rig.Arca.OnWsfe = call => Task.FromResult(call.Operation switch
        {
            "FECompUltimoAutorizado" => StubArca.Soap(StubArca.Result(call.Operation, "<PtoVta>1</PtoVta><CbteTipo>6</CbteTipo><CbteNro>0</CbteNro>")),
            "FECAESolicitar" => StubArca.Soap(StubArca.Result(call.Operation, "<FeDetResp><FECAEDetResponse><Resultado>A</Resultado><CbteFch>garbage</CbteFch></FECAEDetResponse></FeDetResp>")),
            "FECompConsultar" => StubArca.Soap(StubArca.Result(call.Operation, StubArca.Error(602, "No existen datos en nuestros registros para los parametros ingresados."))),
            _ => throw new InvalidOperationException(call.Operation),
        });

        var failure = await Assert.ThrowsAsync<ArcaBadResponseException>(() => rig.Wsfe.AuthorizeNextAsync(1, 6, new Voucher { Concept = 1, DocumentType = 99, DocumentNumber = 0, Total = 121m }));

        Assert.Contains("CbteFch", failure.Message);
    }

    [Fact]
    public async Task A_refused_request_is_not_looked_up_as_if_the_answer_had_been_lost()
    {
        using var rig = new ClientRig();
        rig.Arca.OnWsfe = call => Task.FromResult(call.Operation == "FECAESolicitar"
            ? StubArca.Fault("Server was unable to read request.")
            : StubArca.Soap(StubArca.Result(call.Operation, "<PtoVta>1</PtoVta><CbteTipo>6</CbteTipo><CbteNro>0</CbteNro>")));

        await Assert.ThrowsAsync<ArcaBadRequestException>(() => rig.Wsfe.AuthorizeNextAsync(1, 6, new Voucher { Concept = 1, DocumentType = 99, DocumentNumber = 0, Total = 121m }));

        Assert.DoesNotContain(rig.Arca.WsfeRequests, request => request.StartsWith("FECompConsultar", StringComparison.Ordinal));
    }

    // ---- WSAA ------------------------------------------------------------------------------

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "")]
    [InlineData(HttpStatusCode.Forbidden, "<html>forbidden</html>")]
    [InlineData(HttpStatusCode.NotFound, "<html>not found</html>")]
    public async Task A_client_error_from_WSAA_is_not_retryable(HttpStatusCode status, string body)
    {
        using var rig = new ClientRig();
        rig.Arca.OnLogin = _ => Task.FromResult(StubArca.Text(status, body, "text/html"));

        var failure = await Assert.ThrowsAsync<ArcaBadRequestException>(() => rig.Wsaa.GetTicketAsync("wsfe"));

        Assert.False(failure.Retryable);
        Assert.Equal((int)status, failure.HttpStatus);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task A_server_error_from_WSAA_is_retryable(HttpStatusCode status)
    {
        using var rig = new ClientRig();
        rig.Arca.OnLogin = _ => Task.FromResult(StubArca.Text(status, "<html>try later</html>", "text/html"));

        var failure = await Assert.ThrowsAsync<ArcaUnavailableException>(() => rig.Wsaa.GetTicketAsync("wsfe"));

        Assert.True(failure.Retryable);
        Assert.Contains($"HTTP {(int)status}", failure.Message);
    }

    [Theory]
    [InlineData("ns1:coe.alreadyAuthenticated", "El CEE ya posee un TA valido para el acceso al WSN solicitado", false)]
    [InlineData("ns1:cms.cert.untrusted", "Certificado no emitido por AC de confianza", false)]
    [InlineData("ns1:wsn.unavailable", "Servicio no disponible", true)]
    public async Task A_fault_from_WSAA_keeps_its_code_and_says_whether_to_try_again(string code, string text, bool retryable)
    {
        using var rig = new ClientRig();
        rig.Arca.OnLogin = _ => Task.FromResult(StubArca.Fault(text, code));

        var failure = await Assert.ThrowsAsync<WsaaFaultException>(() => rig.Wsaa.GetTicketAsync("wsfe"));

        Assert.Equal(code[4..], failure.Code);
        Assert.Equal(text, failure.FaultMessage);
        Assert.Equal(retryable, failure.Retryable);
    }

    [Fact]
    public async Task An_answer_from_WSAA_with_no_ticket_in_it_is_retryable()
    {
        using var rig = new ClientRig();
        rig.Arca.OnLogin = _ => Task.FromResult(StubArca.Soap("<loginCmsResponse/>"));

        var failure = await Assert.ThrowsAsync<ArcaUnavailableException>(() => rig.Wsaa.GetTicketAsync("wsfe"));

        Assert.Contains("loginCmsReturn", failure.Message);
    }

    [Theory]
    [InlineData("this is not XML at all")]
    [InlineData("<loginTicketResponse")]
    [InlineData("<loginTicketResponse version=\"1\"/>")]
    [InlineData("<loginTicketResponse><header><generationTime>2026-10-01T12:00:00.000-03:00</generationTime><expirationTime>2026-10-02T00:00:00.000-03:00</expirationTime></header></loginTicketResponse>")]
    [InlineData("<loginTicketResponse><header><generationTime>yesterday</generationTime><expirationTime>2026-10-02T00:00:00.000-03:00</expirationTime></header><credentials><token>t</token><sign>s</sign></credentials></loginTicketResponse>")]
    [InlineData("<loginTicketResponse><header><generationTime>2026-10-01T12:00:00.000-03:00</generationTime></header><credentials><token>t</token><sign>s</sign></credentials></loginTicketResponse>")]
    [InlineData("<loginTicketResponse><header><generationTime>2026-10-01T12:00:00.000-03:00</generationTime><expirationTime>2026-10-02T00:00:00.000-03:00</expirationTime></header><credentials><token></token><sign>s</sign></credentials></loginTicketResponse>")]
    public async Task A_ticket_that_does_not_parse_is_an_answer_the_client_cannot_read(string ticket)
    {
        using var rig = new ClientRig();
        rig.Arca.OnLogin = _ => Task.FromResult(StubArca.LoginAnswer(ticket));

        var failure = await Assert.ThrowsAsync<ArcaBadResponseException>(() => rig.Wsaa.GetTicketAsync("wsfe"));

        Assert.False(failure.Retryable);
        Assert.StartsWith("WSAA's ticket could not be read", failure.Message);
        Assert.NotNull(failure.InnerException);
    }

    [Fact]
    public async Task A_ticket_that_does_not_parse_is_not_kept_and_the_next_call_asks_again()
    {
        using var rig = new ClientRig();
        rig.Arca.OnLogin = login => Task.FromResult(login.Number == 1 ? StubArca.LoginAnswer("garbage") : rig.Arca.LoginAnswer(login.Service, login.Number));
        await Assert.ThrowsAsync<ArcaBadResponseException>(() => rig.Wsaa.GetTicketAsync("wsfe"));

        var ticket = await rig.Wsaa.GetTicketAsync("wsfe");

        Assert.Equal("token-wsfe-2", ticket.Token);
    }
}
