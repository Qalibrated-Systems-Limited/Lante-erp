using LanteGateway;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Serilog;
using Serilog.Enrichers.Span;
using StackExchange.Redis;
using System.Net.Http.Json;
using System.Text;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog((ctx, services, config) =>
    {
        config
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithSpan()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] TraceId={TraceId} {Message:lj}{NewLine}{Exception}")
            .WriteTo.File("logs/lante-gateway-.log", rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] TraceId={TraceId} {Message:lj}{NewLine}{Exception}");
    });

    // #220 -- distributed tracing. The gateway is the mesh's entry point for almost every
    // request, so this is where most traces are actually rooted; ASP.NET Core instrumentation
    // starts the root span here and HttpClient instrumentation propagates it through YARP's
    // proxied calls to every downstream service.
    var otlpEndpoint = builder.Configuration["Otel:OtlpEndpoint"]
        ?? "http://lante-jaeger-collector.new-erp.svc.cluster.local:4317";
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(serviceName: "gateway-service"))
        .WithTracing(t => t
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

    // JWT verification via user-service's JWKS (#215) — the gateway never held the signing key.
    var jwtIssuer = builder.Configuration["JWT:Issuer"] ?? "LanteUserService";
    var jwtAudience = builder.Configuration["JWT:Audience"] ?? "LanteUserService";
    var jwksUrl = builder.Configuration["JWT:JwksUrl"] ?? "http://lante-user-service:8080/.well-known/jwks.json";
    var jwksResolver = new LanteGateway.Authentication.JwtIssuerSigningKeyResolver(new HttpClient(), jwksUrl);

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer("Bearer", options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeyResolver = jwksResolver.ResolveSigningKeys,
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

    // Routes carry YARP's own reserved AuthorizationPolicy names in yarp.json:
    // "Default" (Ocelot routes with a non-empty AuthenticationProviderKey — requires the app's
    // default policy below) and "Anonymous" (Ocelot routes with no/empty AuthenticationProviderKey
    // — YARP skips authorization entirely). Do NOT AddPolicy() under either name — YARP special-
    // cases these two exact strings internally, and registering real policies with the same names
    // collides with that special-casing and throws at startup for every route.
    builder.Services.AddAuthorization(options =>
    {
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .Build();
    });

    // CORS — allow all for dev; tighten per environment via appsettings
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // Health checks
    builder.Services.AddHealthChecks();

    // Real-time tenant-suspension enforcement (short-TTL cache + internal status check).
    // L1 (IMemoryCache) is always present; L2 (Redis) is layered in when configured (#223) so
    // the cache and the stampede lock below are shared across gateway replicas instead of each
    // replica independently hammering user-service and only deduping its own misses.
    builder.Services.AddMemoryCache();
    var useRedis = builder.Configuration.GetValue<bool>("UseRedis", false);
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
    if (useRedis && !string.IsNullOrEmpty(redisConnectionString))
    {
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "LanteGateway";
        });
        builder.Services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnectionString));
        Log.Information("Gateway tenant-status cache: Redis-backed (L1 memory + L2 Redis).");
    }
    else
    {
        Log.Information("Gateway tenant-status cache: process-local only (Redis not configured).");
    }
    builder.Services.AddHttpClient("UserServiceInternal");

    // Per-route rate limiting — mirrors ocelot.json's RateLimitOptions (login/2FA/invite/
    // license-validate/ticketing-portal/platform-login), applied via each route's
    // RateLimiterPolicy in yarp.json.
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        foreach (var (policyName, limit) in new[]
        {
            ("rl-10-per-min", 10), ("rl-20-per-min", 20), ("rl-30-per-min", 30), ("rl-60-per-min", 60),
        })
        {
            options.AddPolicy(policyName, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromMinutes(1) }));
        }
    });

    // YARP — routes/clusters loaded from yarp.json (generated from the retired ocelot.json;
    // see scripts/convert-ocelot-to-yarp.py). ClaimsToHeaderTransformProvider replicates
    // Ocelot's AddHeadersToRequest for the one route that needs it (tenants-all).
    builder.Configuration
        .AddJsonFile("yarp.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"yarp.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

    builder.Services
        .AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
        .AddTransforms<ClaimsToHeaderTransformProvider>();

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    // Handle OPTIONS preflight before it reaches the proxy pipeline
    app.Use(async (context, next) =>
    {
        if (context.Request.Method == "OPTIONS")
        {
            // Reflect the caller's Origin so tenant subdomains (e.g. qsl.localhost:3000) pass preflight,
            // not just the bare localhost:3000 host.
            var origin = context.Request.Headers["Origin"].ToString();
            context.Response.Headers["Access-Control-Allow-Origin"] = string.IsNullOrEmpty(origin) ? "*" : origin;
            context.Response.Headers["Vary"] = "Origin";
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, PATCH, OPTIONS";
            context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, X-Tenant-Subdomain, X-Tenant-Schema";
            context.Response.StatusCode = 200;
            await context.Response.CompleteAsync();
            return;
        }
        await next();
    });
    app.UseCors();

    // Prometheus metrics scrape endpoint
    app.UseMetricServer();
    app.UseHttpMetrics();

    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    // Real-time tenant-suspension enforcement. A suspended tenant's already-issued JWTs are
    // still validly signed and unexpired — nothing about the token itself changes — so this is
    // the single choke point (every downstream request passes through the gateway) that checks
    // current tenant status per request. Cached briefly per tenant so we don't hit user-service
    // on every single request; fails open (lets the request through) if the check itself fails,
    // so a brief user-service blip doesn't take the whole platform down.
    //
    // One SemaphoreSlim per tenant, created lazily and never removed — the tenant set is small
    // and stable (not per-request/ephemeral), so this doesn't grow unbounded in practice. This
    // only dedupes misses within one process though: at N gateway replicas, each replica still
    // independently misses and calls user-service. When Redis is configured (#223), an L2 cache
    // shared across replicas plus a short-lived Redis lock close that gap — only the replica
    // that wins the lock calls user-service; the rest either read the L2 value the winner just
    // wrote or, on the rare timing where they beat it, fail open exactly as a real outage would.
    var tenantStatusLocks = new System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim>();
    var l1Ttl = TimeSpan.FromSeconds(5);
    var l2Ttl = TimeSpan.FromSeconds(30);
    var lockTtl = TimeSpan.FromSeconds(5);

    async Task<bool> FetchTenantActiveAsync(HttpContext context, string tenantId)
    {
        try
        {
            var config = context.RequestServices.GetRequiredService<IConfiguration>();
            var baseUrl = config["UserService:BaseUrl"];
            if (string.IsNullOrEmpty(baseUrl)) return true;

            var client = context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("UserServiceInternal");
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            var serviceKey = config["InternalServices:ServiceKey"];
            if (!string.IsNullOrEmpty(serviceKey))
                client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);

            var response = await client.GetAsync($"internal/tenants/{tenantId}/status");
            if (!response.IsSuccessStatusCode) return true;

            var body = await response.Content.ReadFromJsonAsync<TenantStatusResponse>();
            return body?.IsActive ?? true;
        }
        catch
        {
            return true;
        }
    }

    async Task<bool> GetTenantActiveAsync(HttpContext context, string tenantId)
    {
        var l1 = context.RequestServices.GetRequiredService<IMemoryCache>();
        var l1Key = $"tenant-active:{tenantId}";
        if (l1.TryGetValue(l1Key, out bool cachedL1)) return cachedL1;

        var l2 = context.RequestServices.GetService<IDistributedCache>();
        var l2Key = $"gateway:tenant-active:{tenantId}";

        if (l2 != null)
        {
            var l2Bytes = await l2.GetAsync(l2Key);
            if (l2Bytes is { Length: > 0 })
            {
                var l2Value = l2Bytes[0] == 1;
                l1.Set(l1Key, l2Value, l1Ttl);
                return l2Value;
            }
        }

        // Both layers missed. Dedupe within this process first (cheap, always available), then
        // — when Redis is configured — dedupe across replicas with a short-lived SET-NX lock so
        // only one replica in the whole mesh actually calls user-service for this tenant.
        var gate = tenantStatusLocks.GetOrAdd(tenantId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            if (l1.TryGetValue(l1Key, out cachedL1)) return cachedL1;

            bool isActive;
            var muxer = context.RequestServices.GetService<IConnectionMultiplexer>();
            if (l2 != null && muxer != null)
            {
                var db = muxer.GetDatabase();
                var lockKey = $"gateway:tenant-active-lock:{tenantId}";
                var acquiredLock = await db.StringSetAsync(lockKey, Environment.MachineName, lockTtl, When.NotExists);
                if (acquiredLock)
                {
                    try
                    {
                        isActive = await FetchTenantActiveAsync(context, tenantId);
                        await l2.SetAsync(l2Key, [(byte)(isActive ? 1 : 0)],
                            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = l2Ttl });
                    }
                    finally
                    {
                        await db.KeyDeleteAsync(lockKey);
                    }
                }
                else
                {
                    // Another replica holds the lock and is fetching. Give it a moment to publish
                    // to L2 rather than every waiting replica calling user-service itself; if it
                    // still hasn't (slow user-service, or it crashed mid-fetch), fail open exactly
                    // as a direct outage would rather than block the request further.
                    await Task.Delay(100);
                    var l2Retry = await l2.GetAsync(l2Key);
                    isActive = l2Retry is { Length: > 0 } ? l2Retry[0] == 1 : await FetchTenantActiveAsync(context, tenantId);
                }
            }
            else
            {
                isActive = await FetchTenantActiveAsync(context, tenantId);
            }

            l1.Set(l1Key, isActive, l1Ttl);
            return isActive;
        }
        finally
        {
            gate.Release();
        }
    }

    app.Use(async (context, next) =>
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenantId))
        {
            var isActive = await GetTenantActiveAsync(context, tenantId);

            if (!isActive)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = "Your company's account has been suspended. Please contact your account manager to restore access." });
                return;
            }
        }

        await next();
    });

    // Central audit log: one write path for every state-changing request the gateway proxies,
    // rather than each of the ~12 microservices building its own audit table/endpoint. Only
    // method/path/status/actor are captured — the gateway never sees request/response bodies, so
    // it can't record "what changed", only "which endpoint, by whom, with what result". Fire-and-
    // forget and fails silently: a broken audit write must never fail the caller's real request.
    app.Use(async (context, next) =>
    {
        await next();

        var method = context.Request.Method;
        if (method is "GET" or "HEAD" or "OPTIONS") return;

        var actorEmail = context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(actorEmail)) return; // unauthenticated (e.g. login) — nothing to attribute

        var actorId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var schema = context.User.FindFirst("schema")?.Value;
        var statusCode = context.Response.StatusCode;
        var path = context.Request.Path.Value ?? "";

        var config = context.RequestServices.GetRequiredService<IConfiguration>();
        var baseUrl = config["UserService:BaseUrl"];
        var serviceKey = config["InternalServices:ServiceKey"];
        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(serviceKey)) return;

        var client = context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("UserServiceInternal");
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, baseUrl.TrimEnd('/') + "/internal/audit-log")
            {
                Content = JsonContent.Create(new { method, path, statusCode, actorEmail, actorId })
            };
            request.Headers.Add("X-Internal-Key", serviceKey);
            if (!string.IsNullOrEmpty(schema)) request.Headers.Add("X-Tenant-Schema", schema);
            await client.SendAsync(request);
        }
        catch
        {
            // Best-effort — never let a broken audit write affect the real response already sent.
        }
    });

    // Health check endpoint (anonymous)
    app.MapHealthChecks("/health").WithMetadata(new AllowAnonymousAttribute());

    app.MapGet("/", () => Results.Ok(new { status = "ok", service = "Lante Gateway", timestamp = DateTime.UtcNow }))
       .WithMetadata(new AllowAnonymousAttribute());

    app.MapReverseProxy();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Lante Gateway failed to start");
}
finally
{
    Log.CloseAndFlush();
}

record TenantStatusResponse(bool IsActive);
