using System.Net;
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing.Constraints;

public static class WhoamiEx
{
#if NET6_0_OR_GREATER
    public static IEndpointConventionBuilder MapPath(this IEndpointRouteBuilder endpoints, string pattern, RequestDelegate requestDelegate)
    {
        return endpoints.MapGet(pattern, requestDelegate);
    }
    public static void NoCache(this IHeaderDictionary headers) => headers.CacheControl = "no-cache";

#elif !NET6_0_OR_GREATER
    public static IApplicationBuilder MapPath(this IApplicationBuilder app, string pattern, RequestDelegate requestDelegate)
    {
        if (pattern == "{*url}")
        {
            return app.Use(async (context, next) =>
            {
                await requestDelegate.Invoke(context);
            });
        }
        else
        {
            return app.Map(pattern, _app =>
            {
                _app.Use(async (context, next) =>
                {
                    await requestDelegate.Invoke(context);
                });
            });
        }
    }
    public static void NoCache(this IHeaderDictionary headers) => headers.Add("Cache-Control", "no-cache");
#endif
#if NET6_0_OR_GREATER
    public static void MapApps(this IEndpointRouteBuilder app)
    {
        var configuration = app.ServiceProvider.GetRequiredService<IConfiguration>();
#elif !NET6_0_OR_GREATER
    public static void MapApps(this IApplicationBuilder app)
    {
        var configuration = app.ApplicationServices.GetService<IConfiguration>();
#endif
        app.MapPath("/data", async context =>
        {
            //data?size=1&unit=kb
            context.Response.Headers.NoCache();
            context.Response.ContentType = "text/plain;charset=utf-8";
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
#if NET6_0_OR_GREATER
                context.Response.Headers.ContentDisposition = "Attachment";
#else
                context.Response.Headers.Add("Content-Disposition", "Attachment");
#endif
            }
            await context.Response.WriteAsync("|");
            for (int i = 0; i < size * 1024 - 1; i++)
            {
                await context.Response.WriteAsync(char.ConvertFromUtf32(65 + i % 26));
            }
            await context.Response.WriteAsync("|");
        });
        app.MapPath("/echo", async context =>
        {
            if (context.Request.HttpContext.WebSockets.IsWebSocketRequest)
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
        app.MapPath("/bench", async context =>
        {
            context.Response.Headers.NoCache();
#if NET6_0_OR_GREATER
            context.Response.Headers.Connection = "keep-alive";
#else
            context.Response.Headers.Add("Connetction", "keep-alive");
#endif
            context.Response.ContentType = "text/plain;charset=utf-8";
            await context.Response.WriteAsync("1");
        });
        app.MapPath("/api", async context =>
        {
            context.Response.Headers.NoCache();
            context.Response.ContentType = "application/json;charset=utf-8";

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
#if NET5_0_OR_GREATER
                RuntimeIdentifier = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
#endif
                Framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                ProcessArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                ProcessorCount = System.Environment.ProcessorCount,
                SystemVersion = System.Runtime.InteropServices.RuntimeEnvironment.GetSystemVersion(),
                Name = configuration.GetValue<string>("WHOAMI_NAME")
            };
#if NET5_0_OR_GREATER
            await context.Response.WriteAsJsonAsync(data);
#elif NETCOREAPP3_0_OR_GREATER
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(data));
#else
            await context.Response.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(data));

#endif

        });
        app.MapPath("/health", async context =>
        {
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
        app.MapPath("{*url}", async context =>
        {
            //?wait=100ms
            context.Response.Headers.NoCache();
            context.Response.ContentType = "text/plain;charset=utf-8";

            var wait = context.Request.Query["wait"];
            if (!string.IsNullOrWhiteSpace(wait) && int.TryParse(wait, out var duration))
                System.Threading.Thread.Sleep(duration);
            var name = configuration.GetValue<string>("WHOAMI_NAME");
            if (!string.IsNullOrWhiteSpace(name))
                await context.Response.WriteAsync($"Name: {name}{Environment.NewLine}");

            string hostName = System.Net.Dns.GetHostName();
            var ipList = await System.Net.Dns.GetHostAddressesAsync(hostName);

            //Write connection, request and system information
            await context.Response.WriteAsync($"Hostname: {hostName}{Environment.NewLine}");
            foreach (var ip in ipList)
            {
                await context.Response.WriteAsync($"IP: {ip}{Environment.NewLine}");
            }
            await context.Response.WriteAsync($"RemoteAddr: {context.Connection.RemoteIpAddress?.MapToIPv4().ToString()}:{context.Connection.RemotePort.ToString()}{Environment.NewLine}");
            await context.Response.WriteAsync($"LocalAddr: {context.Connection.LocalIpAddress?.MapToIPv4().ToString()}:{context.Connection.LocalPort.ToString()}{Environment.NewLine}");
            await context.Response.WriteAsync($"Scheme: {context.Request.Scheme}{Environment.NewLine}");
            await context.Response.WriteAsync($"Processor architecture: {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}{Environment.NewLine}");
            await context.Response.WriteAsync($"Operating system: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}{Environment.NewLine}");
#if NET5_0_OR_GREATER
            await context.Response.WriteAsync($"Runtime identifier: {System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}{Environment.NewLine}");
#endif
            await context.Response.WriteAsync($".NET version: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}{Environment.NewLine}");
            await context.Response.WriteAsync($"Process Architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}{Environment.NewLine}");
            await context.Response.WriteAsync($"CPU cores: {System.Environment.ProcessorCount}{Environment.NewLine}");
            await context.Response.WriteAsync($"System Version: {System.Runtime.InteropServices.RuntimeEnvironment.GetSystemVersion()}{Environment.NewLine}");
            await context.Response.WriteAsync($"Containerized: {(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") is null ? "false" : "true")}{Environment.NewLine}");
            await context.Response.WriteAsync($"User Name: {Environment.UserName}{Environment.NewLine}");
            await context.Response.WriteAsync($"Current Time: {System.DateTimeOffset.Now}{TimeZoneInfo.Local.DisplayName}{Environment.NewLine}");
            await context.Response.WriteAsync($"Local Time: {System.DateTimeOffset.Now.ToLocalTime()}{Environment.NewLine}");
            await context.Response.WriteAsync($"UTC Time: {System.DateTimeOffset.Now.ToUniversalTime()}{Environment.NewLine}");
            await context.Response.WriteAsync($"Current Culture: {System.Globalization.CultureInfo.CurrentCulture}{Environment.NewLine}");
            await context.Response.WriteAsync($"Current UI Culture: {System.Globalization.CultureInfo.CurrentUICulture}{Environment.NewLine}");
            var gcInfo = GC.GetGCMemoryInfo();
            await context.Response.WriteAsync($"Memory, total available GC memory: {gcInfo.TotalAvailableMemoryBytes}({GetInBestUnit(gcInfo.TotalAvailableMemoryBytes)}){Environment.NewLine}");





            await context.Response.WriteAsync($"{Environment.NewLine}{context.Request.Method} {context.Request.Path}{context.Request.QueryString} {context.Request.Protocol}{Environment.NewLine}");
            //Write HTTP headers
            foreach (var header in context.Request.Headers)
            {
                await context.Response.WriteAsync($"{header.Key}: {header.Value}{Environment.NewLine}");
            }

            await context.Response.WriteAsync($"{Environment.NewLine}Command Args: {string.Join(" ", System.Environment.GetCommandLineArgs())}{Environment.NewLine}");

            await context.Response.WriteAsync($"{Environment.NewLine}Environment Variables{Environment.NewLine}");
            foreach (System.Collections.DictionaryEntry? header in System.Environment.GetEnvironmentVariables())
            {
                await context.Response.WriteAsync($"{header?.Key}: {header?.Value}{Environment.NewLine}");
            }
        });

        const double Mebi = 1024 * 1024;
        const double Gibi = Mebi * 1024;
        string GetInBestUnit(long size)
        {
            if (size < Mebi)
            {
                return $"{size} bytes";
            }
            else if (size < Gibi)
            {
                double mebibytes = size / Mebi;
                return $"{mebibytes:N2} MiB";
            }
            else
            {
                double gibibytes = size / Gibi;
                return $"{gibibytes:N2} GiB";
            }
        }
    }
}
