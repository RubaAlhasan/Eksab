using System;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Eksabli.HealthChecks;

public static class HealthChecksBuilderExtensions
{
    public static void AddEksabliHealthChecks(this IServiceCollection services)
    {
        // Add your health checks here
        var healthChecksBuilder = services.AddHealthChecks();
        healthChecksBuilder.AddCheck<EksabliDatabaseCheck>("Eksabli DbContext Check", tags: new string[] { "database" });

        services.ConfigureHealthCheckEndpoint("/health-status");

        // The HealthChecks *UI* from the ABP template (AddHealthChecksUI + AddInMemoryStorage +
        // /health-ui and /health-api) is deliberately not registered. It was:
        //
        //   * broken -- it was given the relative path "/health-status", which its background
        //     collector resolves against Kestrel's bind address. In a container that is
        //     http://+:8080, i.e. [::]:8080, and an unspecified address is not a valid target:
        //       "IPv4 address 0.0.0.0 and IPv6 address ::0 ... cannot be used as a target address"
        //     It therefore reported the app Unhealthy while /health-status itself returned 200.
        //
        //   * noisy -- that exception was logged with a full stack trace every few seconds,
        //     swamping Logs/logs.txt and making `docker compose logs` hard to read.
        //
        //   * exposed -- /health-ui and /health-api answered 200 to anonymous callers on the
        //     public internet, publishing check names and internal addresses.
        //
        // Giving it an absolute URL would have fixed the first two and made the third worse, by
        // turning a broken public dashboard into a working one. Nothing consumes it, so it is
        // gone. /health-status remains (uptime monitors, deploy/verify-deploy.sh); the
        // HealthChecks.UI.Client package stays for UIResponseWriter below, which only formats
        // that endpoint's JSON.
    }

    private static IServiceCollection ConfigureHealthCheckEndpoint(this IServiceCollection services, string path)
    {
        services.Configure<AbpEndpointRouterOptions>(options =>
        {
            options.EndpointConfigureActions.Add(endpointContext =>
            {
                endpointContext.Endpoints.MapHealthChecks(
                    new PathString(path.EnsureStartsWith('/')),
                    new HealthCheckOptions
                    {
                        Predicate = _ => true,
                        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
                        AllowCachingResponses = false,
                    });
            });
        });

        return services;
    }
}
