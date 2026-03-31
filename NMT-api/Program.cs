
using Library_Authentication;
using Library_Authentication.Formatters;
using Library_Authentication.Middlewares;
using Library_Authentication.Objects;
using Library_Common;
using Library_Common.SharedConnectors;
using Library_Logger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.OpenApi;
using NMT_api.Services.Srt;
using NMT_api.Services.Translation.Configuration;
using NMT_api.Services.Translation.PythonBridge;
using NMT_api.Services.Translation;
using Scalar.AspNetCore;
using Serilog;
using System.Net;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace NMT_api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                // Init Logs - Text
                Logger.InitLogPath(Config.Application.Name, Config.Application.Instance, Config.Application.Logs);
                Logger.LogApplicationInfo_Start(SharedGetter.GetApplicationInfoLogs(Config.Application));

                // Create builder
                WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions()
                {
                    Args = args,
                    ContentRootPath = WindowsServiceHelpers.IsWindowsService() || SystemdHelpers.IsSystemdService() ? AppContext.BaseDirectory : default
                });

                // Hide "server: Kestrel" header
                _ = builder.WebHost.ConfigureKestrel(options =>
                {
                    options.AddServerHeader = false;
                });

                // Specify the URLs the hosted application will listen on (No args -> From appsettings.json/launchSettings.json)
                bool enableSecurity = Config.Application.IsEndpointSecurized.Value;
                if (FlowEnvironments.IsDevelopment(SharedConfig.Environment))
                {
                    enableSecurity = false;
                    _ = builder.WebHost.UseUrls();
                }
                else
                {
                    _ = builder.WebHost.UseUrls(Config.Application.Endpoint);
                }

                // Set Windows Service ready for SCM (Windows Service Control Manager)
                if (WindowsServiceHelpers.IsWindowsService())
                {
                    _ = builder.Host.UseWindowsService();
                }
                // Set Daemon (Disk And Execution MONitor) ready for systemd
                else if (SystemdHelpers.IsSystemdService())
                {
                    _ = builder.Host.UseSystemd();
                }

                // Use Serilog
                _ = builder.Host.UseSerilog(); // Tells the Dependency Injection (DI) container to replace the default Microsoft logging engine

                // Add various services to the container
                _ = builder.Services.AddEndpointsApiExplorer();
                _ = builder.Services.AddControllers(options =>
                {
                    options.AllowEmptyInputInBodyModelBinding = true;
                    options.InputFormatters.Insert(0, new PlainTextInputFormatter());
                }).AddJsonOptions(a =>
                {
                    a.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                });
                PythonTranslationBackendOptions backendOptions = builder.Configuration
                    .GetSection(PythonTranslationBackendOptions.SectionName)
                    .Get<PythonTranslationBackendOptions>() ?? new PythonTranslationBackendOptions();
                _ = builder.Services.Configure<PythonTranslationBackendOptions>(builder.Configuration.GetSection(PythonTranslationBackendOptions.SectionName));
                _ = builder.Services.AddHttpClient<IPythonTranslationBackendClient, PythonTranslationBackendClient>(client =>
                {
                    client.BaseAddress = new Uri(backendOptions.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(backendOptions.TimeoutSeconds);
                });
                _ = builder.Services.AddSingleton<INmtTranslationService, NllbTranslationService>();
                _ = builder.Services.AddScoped<ISrtTranslationService, SrtTranslationService>();

                // Tell .NET to scan the controllers, routes, and DTOs to build the internal "map" of the API
                //  - Provide the metadata for Scalar
                //  - Not required for Swagger who made lot of guessing through Swashbuckle
                _ = builder.Services.AddOpenApi("v1", options =>
                {
                    // Provide JWT auth info to OpenAPI documentation
                    _ = options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();

                    // Set OpenAPI info (Title, Versio, etc.) and order Controllers and Paths alphabetically
                    _ = options.AddDocumentTransformer((document, context, cancellationToken) =>
                    {
                        // Set OpenAPI info (Title, Versio, etc.)
                        document.Info = new OpenApiInfo
                        {
                            Title = $"{Config.Application.Name} ({SharedConfig.Environment} - {(SharedGetter.IsCloud() ? "Cloud" : "OnPrem")})",
                            Version = $"{Config.Application.Version}",
                            Description = $"{Config.Application.Description}",
                            Contact = new OpenApiContact
                            {
                                Name = "Solutions et Produits",
                                Email = "SolEtPro@rtbf.be"
                            }
                        };

                        // Sort Controllers
                        document.Tags = document.Tags.OrderBy(t => t.Name).ToHashSet();

                        // For Paths, sort by HTTP Method
                        foreach (KeyValuePair<string, IOpenApiPathItem> path in document.Paths)
                        {
                            List<KeyValuePair<HttpMethod, OpenApiOperation>> operationsSorted = [.. path.Value.Operations.OrderBy(op => op.Key.Method)];
                            path.Value.Operations.Clear();
                            foreach (KeyValuePair<HttpMethod, OpenApiOperation> operation in operationsSorted)
                            {
                                path.Value.Operations.Add(operation.Key, operation.Value);
                            }
                        }

                        // Sort Paths
                        List<KeyValuePair<string, IOpenApiPathItem>> pathsSorted = [.. document.Paths.OrderBy(p => p.Key)];
                        document.Paths.Clear();
                        pathsSorted.ForEach(p => document.Paths.Add(p.Key, p.Value));

                        // Return
                        return Task.CompletedTask;
                    });
                });

                // Init Monitoring-related Singletons (Pacemaker API and Middleware for HTTP Requests Logging)
                if (!FlowEnvironments.IsDevelopment(SharedConfig.Environment))
                {
                    // Register as a Singleton the service sending Signs of Life to Pacemaker API
                    _ = builder.Services.AddSingleton(new HostedService_PacemakerAPI_Application(new Library_Common.SharedObjects.Monitoring.StartupInfo(Config.Application, Config.Related.Applications, Config.Related.Databases, Config.Related.Storages)));
                    _ = builder.Services.AddHostedService(sp => sp.GetRequiredService<HostedService_PacemakerAPI_Application>());

                    // Register as a Singleton the service queueing the API HTTP Requests Logs into a Channel, and inserting them to DB at scheduled intervals (the Middleware queues data in the Channel)
                    _ = builder.Services.AddSingleton(Channel.CreateBounded<Library_Authentication.Objects.ApiHttpRequest>(
                        new BoundedChannelOptions(Library_Authentication.Config.Logging.SQLServer.MaxCapacityChannelApiHttpRequests)
                        {
                            FullMode = BoundedChannelFullMode.DropOldest,
                            SingleReader = true,
                            SingleWriter = false
                        }));

                    // Register Background Service Queue Bulk Worker as a Hosted Service - API HTTP Requests to Pacemaker API
                    _ = builder.Services.AddHostedService<BackgroundService_QueueBulkWorker_ApiHttpRequest>();
                }

                // Enable Security
                Library_Authentication.Config.LoadProxiesInfo();
                if (enableSecurity)
                {
                    if (!Config.Related.Databases.Any(d => d.Equals(SharedConfig.SQLServer.API.Database())))
                    {
                        Config.Related.Databases.Add(SharedConfig.SQLServer.API.Database());
                    }

                    Library_Authentication.Config.LoadAuthenticationInfo(Config.Security.Issuer, Config.Security.SecretKey_Instance);
                    _ = builder.Services
                        .AddAuthentication(Library_Authentication.Getter.SetAuthenticationOptions)
                        .AddJwtBearer(Library_Authentication.Getter.SetJwtBearerOptions);
                    _ = builder.Services.AddRateLimiter(Library_Authentication.Getter.SetRateLimiterOptions);
                    _ = builder.Services.AddSingleton<Library_Authentication.Connectors.JWTSecurityToken.Interface, Library_Authentication.Connectors.JWTSecurityToken.Generator>();
                }
                // Disable Security
                else
                {
                    Library_Authentication.Getter.DisableSecurity(builder.Services);
                }

                // Add Swagger
                _ = builder.Services.AddSwaggerGen(options =>
                {
                    options.OrderActionsBy((apiDesc) => $"{apiDesc.ActionDescriptor.RouteValues["controller"]}_{apiDesc.RelativePath}_{apiDesc.HttpMethod}"); // Sort by Controllers, Paths, HTTP Methods
                    options.EnableAnnotations();
                    options.UseInlineDefinitionsForEnums();
                    options.SwaggerDoc(Config.Application.Version, new OpenApiInfo
                    {
                        Title = $"{Config.Application.Name} ({SharedConfig.Environment} - {(SharedGetter.IsCloud() ? "Cloud" : "OnPrem")})",
                        Version = $"{Config.Application.Version}",
                        Description = $"{Config.Application.Description}",
                        Contact = new OpenApiContact
                        {
                            Name = "Solutions et Produits",
                            Email = "SolEtPro@rtbf.be"
                        }
                    });

                    // Enable Security handling in Swagger
                    if (enableSecurity)
                    {
                        options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, Library_Authentication.Getter.GetOpenApiSecurityScheme());
                        options.AddSecurityRequirement((document) => new() { [new(JwtBearerDefaults.AuthenticationScheme, document)] = [] });
                    }
                });

                // Proxy Handler - Configuration
                // Update HttpContext request by exploiting the fact the F5 proxy passes headers "X-Forwarded-For: {HostIP}" and "X-Forwarded-Proto: https"
                // Make .NET app "F5 proxy Aware"
                //  - Tell .NET to update HttpContext.Connection.RemoteIpAddress
                //  - Switch the scheme to http or https dynamically (fix Scalar http/https conflict)
                _ = builder.Services.Configure<ForwardedHeadersOptions>(options =>
                {
                    // Forward headers
                    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                    // Security to avoid IP spoofing
                    // If someone directly requests the API (bypassing the proxy) specifying a spoofed X-Forwarded-For header, it'll look at the sender's IP, see it's not in trusted list, and ignore the header
                    if (SharedGetter.IsCloud())
                    {
                        foreach (Proxy proxy in Library_Authentication.Config.Proxies.Where(i => i.Cloud))
                        {
                            if (proxy.IsRange)
                            {
                                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(proxy.HostIP));
                            }
                            else
                            {
                                options.KnownProxies.Add(IPAddress.Parse(proxy.HostIP));
                            }
                        }
                    }
                    else
                    {
                        foreach (Proxy proxy in Library_Authentication.Config.Proxies.Where(i => i.OnPrem))
                        {
                            if (proxy.IsRange)
                            {
                                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(proxy.HostIP));
                            }
                            else
                            {
                                options.KnownProxies.Add(IPAddress.Parse(proxy.HostIP));
                            }
                        }
                    }
                });

                // Build
                WebApplication app = builder.Build();

                #region Middlewares
                // Proxy Handler
                // Use Proxy Handler configured above to interpret correct ClientIP
                _ = app.UseForwardedHeaders();

                // Exception Handler (The Safety Net)
                // Transform exception into clean HTTP 500 and make sure the correct status code is sent to the API HTTP Requests Logging Middleware
                // Must be at the very top to catch any error occurring in any middleware below it
                _ = app.UseExceptionHandler("/error");

                // Routing (The Mapper)
                // Match the URL to a specific Controller action
                _ = app.UseRouting();

                // Logging (The Observers)
                // Placed here so they capture the full request duration and see 401/403 errors from the Security middlewares below
                _ = app.UseMiddleware<RequestLogging_TextFile>();
                if (!FlowEnvironments.IsDevelopment(SharedConfig.Environment))
                {
                    _ = app.UseMiddleware<RequestLogging_SQLServer>(Config.Application);
                }

                // Swagger (The Established UI)
                // If we want to skip Swagger Logs (eg: GET /index.css, GET /swagger/V1/swagger.json, etc.), then it should be placed before the Logging middleware
                // It's a middleware rather than an endpoint because Swagger is a legacy library as opposed to modern Scalar
                _ = app.UseSwagger();
                _ = app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint($"/swagger/{Config.Application.Version}/swagger.json", $"{Config.Application.Name} {Config.Application.Version}");
                    options.DocumentTitle = $"{Config.Application.Name} ({SharedConfig.Environment} - {(SharedGetter.IsCloud() ? "Cloud" : "OnPrem")})";
                    options.RoutePrefix = string.Empty; // For pseudo-redirection purpose
                });

                // Security (The Guards)
                if (enableSecurity)
                {
                    _ = app.UseAuthentication();
                    _ = app.UseRateLimiter();
                }
                _ = app.UseAuthorization(); // Required outside the 'if' in order to use the [Authorize] decorations
                #endregion

                #region Endpoints
                // Add Debug Endpoints
                if (FlowEnvironments.IsDevelopment(SharedConfig.Environment) || FlowEnvironments.IsStaging(SharedConfig.Environment))
                {
                    _ = app.MapGet("/Debug/Proxy", (HttpContext context) => new
                    {
                        ClientIP = context.GetRemoteIPAddress(),
                        context.Request.Scheme,
                        context.Request.Protocol
                    });
                    _ = app.MapGet("/Debug/Headers", (HttpContext context) =>
                    {
                        return context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()).OrderBy(i => i.Key).ToDictionary();
                    });
                }

                // Controllers (The Execution)
                _ = app.MapControllers();

                // Map on /openapi/v1.json the JSON documentation created by AddOpenApi()
                _ = app.MapOpenApi();

                // Scalar (The Challenging UI)
                // Map Scalar website enabling endpoints requesting, JSON downloading, UI rendering, etc.                
                _ = app.MapScalarApiReference(options =>
                {
                    _ = options
                        .EnableDarkMode()
                        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                        .WithTitle($"{Config.Application.Name} ({SharedConfig.Environment} - {(SharedGetter.IsCloud() ? "Cloud" : "OnPrem")})")
                        .AddPreferredSecuritySchemes(JwtBearerDefaults.AuthenticationScheme);

                    // Enable Security handling in Scalar
                    if (enableSecurity)
                    {
                        _ = options
                            .AddPreferredSecuritySchemes(JwtBearerDefaults.AuthenticationScheme);
                    }
                });
                #endregion

                // Run
                app.Run();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Application terminated unexpectedly");
                throw;
            }
            finally
            {
                Logger.LogApplicationInfo_Stop(SharedGetter.GetApplicationInfoLogs(Config.Application));
                Logger.Close();
            }
        }
    }
}
