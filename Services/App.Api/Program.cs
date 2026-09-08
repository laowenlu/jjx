/**
 * Program.cs - Main application bootstrap for App.Api (.NET 8 Web API)
 *
 * Orchestrates the entire setup and start sequence for the API, including configuration loading,
 * service registration, endpoint mapping, and infrastructural middleware (CORS, Swagger, Serilog, etc.).
 */
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using App.Api.Accessors;
using App.Api.Engines;
using App.Api.Managers;
using App.Api.Utilities;
using App.ServiceInvoker.Auth;
using App.ServiceInvoker.Endpoints;
using App.ServiceInvoker.Interfaces;
using Microsoft.AspNetCore.HttpOverrides;
using Marten;
using Marten.NodaTimePlugin;
using Serilog;

namespace App.Api
{
    /// <summary>
    /// Entrypoint class for the API application. Contains Main() and all DI configuration logic.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Main entrypoint: creates/configures the web host, registers services,
        /// configures middleware, and starts the API server. Handles fatal startup errors.
        /// </summary>
        public static void Main(string[] args)
        {
            // Bootstrap the ASP.NET Core builder
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            // Load application configuration (from appsettings etc.)
            AppConfiguration? appConfiguration = new AppConfiguration();
            builder.Configuration.GetSection("AppConfiguration").Bind(appConfiguration);

            // Register configuration singleton
            builder.Services.AddSingleton(appConfiguration);

            // Configure global JSON serialization conventions
            var jsonSerializerOptions = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() },
            };
            builder.Services.AddSingleton(jsonSerializerOptions);

            // Register controllers and configure JSON serializers for API contract stability
            builder.Services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new NullableEnumConverter<LogLevel>());
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            // Register custom application/business/application services in DI
            ConfigureServices(builder.Services, appConfiguration, builder.Environment);

            // Trust forwarded headers only from the local reverse proxy (nginx on the same machine).
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownProxies.Add(IPAddress.Parse("127.0.0.1"));
                options.KnownProxies.Add(IPAddress.IPv6Loopback);
            });

            // Build the app and set up environment-specific middleware
            WebApplication app = builder.Build();

            // Add foundational middleware
            app.UseForwardedHeaders();
           
            if (app.Environment.IsDevelopment())
            {
                app.UseCors("DevCors");
            }
            else
            {
                app.UseHttpsRedirection();
                // Let Azure App Service CORS do the work to avoid duplicate/conflicting headers
                app.UseCors("ProdCors");
            }
            app.UseAuthorization();

            // Register all controller routes
            app.MapControllers();

            // Register main ServiceInvoker endpoint for all deterministically-reflectable manager APIs
            app.MapServiceInvokerEndpoint("/api/invoke");

            // NDJSON streaming RPC endpoint (manager methods returning IAsyncEnumerable<T>)
            app.MapStreamingInvokerEndpoint("/api/stream");

            app.MapGet("/api/health", () => Results.Ok(new { Status = "ok" })).AllowAnonymous();

            // Start the API, robust error logging via Serilog
            try
            {
                Log.Information("Starting up");
                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application start-up failed");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        /// Registers all custom services, accessors, and engines for dependency injection.
        /// Configures CORS, cache, Marten DB, and more. Called from Main.
        /// </summary>
        private static void ConfigureServices(IServiceCollection services, AppConfiguration appConfiguration, IWebHostEnvironment env)
        {
            GetScopedServices(services, appConfiguration, env);
            GetTransientServices(services);
            GetSingletonServices(services);

            // Set up CORS (allow local dev, desktop, etc.)
            services.AddCors(options =>
            {
                // Dev policy: allow localhost origins you actually use, with credentials
                options.AddPolicy("DevCors", builder =>
                {
                    builder
                        .SetIsOriginAllowed(origin =>
                        {
                            if (origin is null) return false;
                            var host = new Uri(origin).Host;
                            return host == "localhost" || host == "127.0.0.1";
                        })
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });

                // No-op policy for prod (Azure handles CORS). We�ll still reference a policy so code paths are uniform.
                options.AddPolicy("ProdCors", builder =>
                {
                    // Intentionally empty: don�t emit CORS headers from the app in prod.
                    // If you ever want the app to add headers in prod, replace this with .WithOrigins(...).AllowCredentials() etc.
                });
            });

            // Caching and request context setup
            services.AddMemoryCache();
            services.AddHttpContextAccessor();

            // Marten DB configuration + schema auto creation and NodaTime
            services.AddMarten(options =>
            {
                options.Connection(ConnectionStringUtility.GetDatabaseConnectionString(appConfiguration));

                // Use a non-"public" schema by default to reduce accidental exposure in hosted Postgres setups.
                options.DatabaseSchemaName = "app";
                options.Events.DatabaseSchemaName = "app";

                options.AutoCreateSchemaObjects = Weasel.Core.AutoCreate.All;
                options.UseNodaTime();
            });
        }

        /// <summary>
        /// Register per-request lifetime services (transient)
        /// </summary>
        private static void GetTransientServices(IServiceCollection services)
        {
            services.AddHttpClient();
        }

        /// <summary>
        /// Register scoped services per request (business logic, data, context).
        /// Conditionally registers IDatabaseAccessor to use either the Marten/Postgres or Local implementation.
        /// Also conditionally registers IBlobStorageAccessor and IAuthAccessor.
        /// </summary>
        private static void GetScopedServices(IServiceCollection services, AppConfiguration appConfiguration, IWebHostEnvironment env)
        {
            // All orchestrating manager and engine registration
            services.AddScoped<AuthManager>();
            services.AddScoped<AuthenticatorEngine>();
            services.AddScoped<UserContextService>();

            // ServiceInvoker AuthZ (shared)
            services.AddScoped<IMethodAuthorizer, AttributeMethodAuthorizer>();
            services.AddScoped<IAuthTokenManager>(sp => sp.GetRequiredService<AuthManager>());

            // Conditional registration for main accessors
            if (env.IsDevelopment())
            {
                services.AddScoped<IDatabaseAccessor, LocalDatabaseAccessor>();
                services.AddScoped<IBlobStorageAccessor, LocalBlobStorageAccessor>();
                services.AddScoped<IAuthAccessor, LocalAuthAccessor>();
            }
            else
            {
                services.AddScoped<IDatabaseAccessor, DatabaseAccessor>();
                services.AddScoped<IBlobStorageAccessor, BlobStorageAccessor>();
                services.AddScoped<IAuthAccessor, AuthAccessor>();
            }
        }

        /// <summary>
        /// Register true singleton services (rare; most use config or cached objects)
        /// </summary>
        private static void GetSingletonServices(IServiceCollection services)
        {
        }
    }
}
