using System.Net.WebSockets;
using System.Text.Json;
using AdbWireGuardRelay;

var builder = WebApplication.CreateBuilder(args);

var brokerOptions = BrokerOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(brokerOptions);
builder.Services.AddSingleton<SessionBroker>();
builder.Services.AddHostedService<SessionCleanupService>();

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.MapGet("/healthz", () => Results.Ok(new
{
    ok = true,
    service = "adb-wireguard-relay",
    hostTokensConfigured = brokerOptions.HostTokens.Count
}));

app.MapPost("/api/v1/relay/sessions", (HttpContext context, CreateSessionRequest request, SessionBroker store) =>
{
    var hostToken = ReadBearerToken(context);
    var validationError = ValidateHostToken(hostToken, brokerOptions);
    if (validationError is not null)
    {
        return validationError;
    }

    try
    {
        var session = store.CreateSession(hostToken!, request.DeviceName, request.RequestedTtlMinutes);
        return Results.Ok(session);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new ErrorResponse(ex.Message));
    }
});

app.MapPost("/api/v1/relay/claim", (ClaimSessionRequest request, SessionBroker store) =>
{
    try
    {
        var claim = store.ClaimSession(request.PairCode, request.ClientName);
        return Results.Ok(claim);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new ErrorResponse(ex.Message));
    }
});

app.MapPost("/api/v1/relay/sessions/{sessionId:guid}/heartbeat", (Guid sessionId, HeartbeatRequest request, SessionBroker store) =>
{
    try
    {
        return Results.Ok(store.RecordHeartbeat(sessionId, request.Role, request.ResumeToken));
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new ErrorResponse(ex.Message));
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new ErrorResponse(ex.Message));
    }
});

app.MapGet("/api/v1/relay/sessions/{sessionId:guid}", (Guid sessionId, HttpContext context, SessionBroker store) =>
{
    var hostToken = ReadBearerToken(context);
    var validationError = ValidateHostToken(hostToken, brokerOptions);
    if (validationError is not null)
    {
        return validationError;
    }

    try
    {
        return Results.Ok(store.GetSessionStatus(sessionId, hostToken!));
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new ErrorResponse(ex.Message));
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
});

app.MapDelete("/api/v1/relay/sessions/{sessionId:guid}", (Guid sessionId, HttpContext context, SessionBroker store) =>
{
    var hostToken = ReadBearerToken(context);
    var validationError = ValidateHostToken(hostToken, brokerOptions);
    if (validationError is not null)
    {
        return validationError;
    }

    try
    {
        store.CloseSession(sessionId, hostToken!);
        return Results.Ok(new { ok = true });
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new ErrorResponse(ex.Message));
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
});

app.Map("/ws/host/{sessionId:guid}", async (HttpContext context, Guid sessionId, SessionBroker store) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new ErrorResponse("To endpoint wymaga WebSocket."));
        return;
    }

    var token = context.Request.Query["token"].ToString();
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var attached = false;
    try
    {
        store.AttachHost(sessionId, token, socket);
        attached = true;
        await WaitUntilSocketClosesAsync(socket, context.RequestAborted);
    }
    catch (KeyNotFoundException ex)
    {
        await CloseWithErrorAsync(socket, ex.Message);
    }
    catch (UnauthorizedAccessException ex)
    {
        await CloseWithErrorAsync(socket, ex.Message);
    }
    catch (InvalidOperationException ex)
    {
        await CloseWithErrorAsync(socket, ex.Message);
    }
    finally
    {
        if (attached)
        {
            store.MarkHostDisconnected(sessionId, socket);
        }
    }
});

app.Map("/ws/client/{sessionId:guid}", async (HttpContext context, Guid sessionId, SessionBroker store) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new ErrorResponse("To endpoint wymaga WebSocket."));
        return;
    }

    var token = context.Request.Query["token"].ToString();
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var attached = false;
    try
    {
        store.AttachClient(sessionId, token, socket);
        attached = true;
        await WaitUntilSocketClosesAsync(socket, context.RequestAborted);
    }
    catch (KeyNotFoundException ex)
    {
        await CloseWithErrorAsync(socket, ex.Message);
    }
    catch (UnauthorizedAccessException ex)
    {
        await CloseWithErrorAsync(socket, ex.Message);
    }
    catch (InvalidOperationException ex)
    {
        await CloseWithErrorAsync(socket, ex.Message);
    }
    finally
    {
        if (attached)
        {
            store.MarkClientDisconnected(sessionId, socket);
        }
    }
});

app.Run();

static string? ReadBearerToken(HttpContext context)
{
    var header = context.Request.Headers.Authorization.ToString();
    const string prefix = "Bearer ";
    return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        ? header[prefix.Length..].Trim()
        : null;
}

static IResult? ValidateHostToken(string? hostToken, BrokerOptions options)
{
    if (string.IsNullOrWhiteSpace(hostToken))
    {
        return Results.Unauthorized();
    }

    if (options.HostTokens.Count == 0)
    {
        return Results.Problem(
            title: "Relay host tokens are not configured",
            detail: "Ustaw co najmniej jeden token hosta w Broker:HostTokens albo w ADBWG_BROKER_HOST_TOKENS. Stary klucz Relay:HostTokens i ADBWG_RELAY_HOST_TOKENS nadal są obsługiwane.",
            statusCode: StatusCodes.Status500InternalServerError);
    }

    if (!options.HostTokens.Contains(hostToken, StringComparer.Ordinal))
    {
        return Results.Unauthorized();
    }

    return null;
}

static async Task WaitUntilSocketClosesAsync(WebSocket socket, CancellationToken cancellationToken)
{
    while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
    {
        await Task.Delay(250, cancellationToken);
    }
}

static async Task CloseWithErrorAsync(WebSocket socket, string message)
{
    if (socket.State == WebSocketState.Open)
    {
        var payload = JsonSerializer.Serialize(new ErrorResponse(message));
        await socket.SendAsync(
            System.Text.Encoding.UTF8.GetBytes(payload),
            WebSocketMessageType.Text,
            endOfMessage: true,
            CancellationToken.None);
        await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, message, CancellationToken.None);
    }
}
