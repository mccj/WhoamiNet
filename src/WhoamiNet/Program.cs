using System.Net;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

//https://github.com/traefik/whoami/blob/30767b10c576c03c208deedb3c6d56621599cb8e/app.go#L127

app.MapGet("/data", async context =>
{
    //data?size=1&unit=kb
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.ContentType = "text/plain";
    var size = 1L;
    if (long.TryParse(context.Request.Query["size"], out var outSize))
        size = outSize;
    if (size < 0)
        size = 0;

    var attachment = false;
    if (bool.TryParse(context.Request.Query["attachment"], out var outAttachment))
        attachment = outAttachment;


    var unit = context.Request.Query["unit"].ToString();

    switch (unit.ToLower())
    {
        case "kb":
            size *= 1;//KB
            break;
        case "mb":
            size *= 1 * 1024;//MB
            break;

        case "gb":
            size *= 1 * 1024 * 1024;//GB
            break;

        case "tb":
            size *= 1 * 1024 * 1024 * 1024;//TB
            break;
        default:
            break;
    }

    if (attachment)
    {
        context.Response.Headers.ContentDisposition = "Attachment";
    }
    await context.Response.WriteAsync("|");
    for (int i = 0; i < size * 1024 - 1; i++)
    {
        await context.Response.WriteAsync(char.ConvertFromUtf32(65 + i % 26));
    }
    await context.Response.WriteAsync("|");
});
app.MapGet("/echo",async context => {
    if(context.Request.HttpContext.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await context.Request.HttpContext.WebSockets.AcceptWebSocketAsync();

        var buffer = new byte[1024 * 4];
        var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        while (!result.CloseStatus.HasValue)
        {
            var serverMsg = Encoding.UTF8.GetBytes($"Server: Hello. You said: {Encoding.UTF8.GetString(buffer)}");
            await webSocket.SendAsync(new ArraySegment<byte>(serverMsg, 0, serverMsg.Length), result.MessageType, result.EndOfMessage, CancellationToken.None);

            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }
        await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
    }
    else
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
    }
});//Î´Íê
app.MapGet("/bench", async context =>
{
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.Connection = "keep-alive";
    context.Response.Headers.ContentType = "text/plain";
    await context.Response.WriteAsync("1");
});
app.MapGet("/api", async context =>
{
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.ContentType = "application/json";
    var data = new
    {
        Hostname = System.Net.Dns.GetHostName(),
        Method = context.Request.Method,
        Url = $"{context.Request.Path}{context.Request.QueryString}",
        Scheme = context.Request.Scheme,
        IP = Dns.GetHostEntry(Dns.GetHostName()).AddressList.Select(f => f.ToString()).ToArray(),
        Headers = context.Request.Headers,
        RemoteIp = $"{context.Connection.RemoteIpAddress?.MapToIPv4().ToString()}:{context.Connection.RemotePort}",
        LocalIp = $"{context.Connection.LocalIpAddress?.MapToIPv4().ToString()}:{context.Connection.LocalPort}",
        Host = context.Request.Host.ToString(),
        OSArchitecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
        OSDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        RuntimeIdentifier = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
        Framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        ProcessArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
        ProcessorCount = System.Environment.ProcessorCount,
        SystemVersion = System.Runtime.InteropServices.RuntimeEnvironment.GetSystemVersion(),
        Name = app.Configuration.GetValue<string>("WHOAMI_NAME")
    };
    await context.Response.WriteAsJsonAsync(data);
});
app.MapGet("/health", async context => {
    if (context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
    }
    else
    {
        context.Response.StatusCode = (int)HttpStatusCode.OK;
    }
    await Task.CompletedTask;
});//Î´Íê
app.MapGet("{*url}", async context =>
{
    //?wait=100ms
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.ContentType = "text/plain";

    var wait = context.Request.Query["wait"];
    if (!string.IsNullOrWhiteSpace(wait) && int.TryParse(wait, out var duration))
        System.Threading.Thread.Sleep(duration);
    var name = app.Configuration.GetValue<string>("WHOAMI_NAME");
    if (!string.IsNullOrWhiteSpace(name))
        await context.Response.WriteAsync($"Name: {name}{Environment.NewLine}");

    //Write connection, request and system information
    await context.Response.WriteAsync($"Hostname: {System.Net.Dns.GetHostName()}{Environment.NewLine}");
    foreach (var ip in (await Dns.GetHostEntryAsync(Dns.GetHostName())).AddressList.Select(f => f.ToString()))
    {
        await context.Response.WriteAsync($"IP: {ip}{Environment.NewLine}");
    }
    await context.Response.WriteAsync($"RemoteAddr: {context.Connection.RemoteIpAddress?.MapToIPv4().ToString()}:{context.Connection.RemotePort.ToString()}{Environment.NewLine}");
    await context.Response.WriteAsync($"LocalAddr: {context.Connection.LocalIpAddress?.MapToIPv4().ToString()}:{context.Connection.LocalPort.ToString()}{Environment.NewLine}");
    await context.Response.WriteAsync($"Scheme: {context.Request.Scheme}{Environment.NewLine}");
    await context.Response.WriteAsync($"OS Architecture: {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString()}{Environment.NewLine}");
    await context.Response.WriteAsync($"OS Description: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}{Environment.NewLine}");
    await context.Response.WriteAsync($"Runtime identifier: {System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}{Environment.NewLine}");
    await context.Response.WriteAsync($"Framework: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}{Environment.NewLine}");
    await context.Response.WriteAsync($"Process Architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString()}{Environment.NewLine}");
    await context.Response.WriteAsync($"Processor count: {System.Environment.ProcessorCount}{Environment.NewLine}");
    await context.Response.WriteAsync($"System Version: {System.Runtime.InteropServices.RuntimeEnvironment.GetSystemVersion()}{Environment.NewLine}");
    await context.Response.WriteAsync($"{context.Request.Method} {context.Request.Path}{context.Request.QueryString} {context.Request.Protocol}{Environment.NewLine}");
    //Write HTTP headers
    foreach (var header in context.Request.Headers)
    {
        await context.Response.WriteAsync($"{header.Key}: {header.Value}{Environment.NewLine}");
    }
});
app.Run();


public struct data
{
    public string Hostname { get; set; }
}