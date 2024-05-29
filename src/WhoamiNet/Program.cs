#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

//https://github.com/traefik/whoami/blob/30767b10c576c03c208deedb3c6d56621599cb8e/app.go#L127

app.MapApps();
app.Run();
#endif