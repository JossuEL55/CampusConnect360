using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SharedKernel.Observability;

// Centraliza la configuración de Serilog para los componentes del ecosistema.
public static class SerilogExtensions
{
    public static WebApplicationBuilder AddCampusSerilog(
        this WebApplicationBuilder builder,
        string serviceName)
    {
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            var seqUrl =
                context.Configuration["Seq:ServerUrl"]
                ?? "http://localhost:5341";

            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithProperty("ServiceName", serviceName)
                .WriteTo.Console()
                .WriteTo.Seq(seqUrl);
        });

        return builder;
    }

    public static WebApplication UseCampusRequestLogging(
        this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} " +
        "in {Elapsed:0.0000} ms";

    options.EnrichDiagnosticContext = (
        diagnosticContext,
        httpContext) =>
    {
        var correlationId =
            httpContext.Items[
                CorrelationConstants.LogPropertyName
            ]?.ToString();

        diagnosticContext.Set(
            CorrelationConstants.LogPropertyName,
            correlationId ?? "unknown");
    };
    });

        return app;
    }
}