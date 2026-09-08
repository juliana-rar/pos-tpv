using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Infrastructure.Ai;
using PosTpv.Infrastructure.Identity;
using PosTpv.Infrastructure.Persistence;
using PosTpv.Infrastructure.Persistence.Repositories;

namespace PosTpv.Infrastructure;

/// <summary>Registers persistence, repositories and identity services.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        // "Database:Provider" defaults to SqlServer; set it to "Sqlite" for environments
        // without a SQL Server instance available (e.g. no container runtime).
        var provider = config["Database:Provider"] ?? "SqlServer";

        services.AddDbContext<PosDbContext>(options =>
        {
            if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
                options.UseSqlite(connectionString);
            else
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IDbSeeder, DbSeeder>();
        services.AddSingleton<IReportExporter, PosTpv.Infrastructure.Reporting.ReportExporter>();

        // Local Ollama vision model, used to read supplier delivery notes (albaranes) from a
        // photo. CPU-only inference can take minutes, hence the generous timeout.
        var ollamaBaseUrl = config["Ollama:BaseUrl"] ?? "http://localhost:11434";
        var ollamaTimeoutSeconds = int.TryParse(config["Ollama:TimeoutSeconds"], out var t) ? t : 300;
        services.AddHttpClient("Ollama", client =>
        {
            client.BaseAddress = new Uri(ollamaBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(ollamaTimeoutSeconds);
        });
        services.AddScoped<IOllamaVisionClient, OllamaVisionClient>();

        return services;
    }
}
