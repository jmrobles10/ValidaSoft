using JasperFx;
using Npgsql;
using Scalar.AspNetCore;
using Validasoft.Aplicacion.Comun;
using Validasoft.Infraestructura;
using Validasoft.Infraestructura.Http;
using Wolverine;
using Wolverine.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AgregarValidasoft(builder.Configuration);
builder.Services.AddOpenApi("v1");
builder.Services.AddWolverineHttp();
builder.Host.UseWolverine(opts => opts.Discovery.IncludeAssembly(typeof(Permisos).Assembly));

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseMiddleware<MiddlewareDeContexto>();
app.UseAuthorization();
app.UseMiddleware<MiddlewareDeIdempotencia>();

app.MapGet("/health/live", () => Results.Ok(new { estado = "vivo" })).AllowAnonymous();
app.MapGet("/health/ready", async (IConfiguration configuracion, CancellationToken ct) =>
{
    try
    {
        await using var conexion = new NpgsqlConnection(configuracion.GetConnectionString("Validasoft"));
        await conexion.OpenAsync(ct);
        return Results.Ok(new { estado = "listo" });
    }
    catch (Exception)
    {
        return Results.Json(new { estado = "sin-base-de-datos" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();

app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
if (app.Environment.IsDevelopment())
    app.MapScalarApiReference("/docs").AllowAnonymous();

app.MapWolverineEndpoints();

// `dotnet run -- db-apply` aplica el esquema en entornos sin creación automática (docs/04 §11).
return await app.RunJasperFxCommands(args);

public partial class Program;
