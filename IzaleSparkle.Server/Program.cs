using IzaleSparkle.Application;
using IzaleSparkle.Infrastructure;
using IzaleSparkle.Infrastructure.Persistence;
using IzaleSparkle.Server.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtection-Keys")));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = "Izale Sparkle API",
        Version     = "v1",
        Description = "Izale Sparkle Fine Jewellery · Clean Architecture · .NET 10 · Blazor WASM Hosted"
    });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization", Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer", BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your JWT token."
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {{
        new Microsoft.OpenApi.Models.OpenApiSecurityScheme { Reference = new Microsoft.OpenApi.Models.OpenApiReference
            { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
        }, Array.Empty<string>()
    }});
});

// JWT Authentication
// Use IsNullOrWhiteSpace (not ??) so an empty config value also falls back —
// otherwise an empty Jwt:Secret produces a zero-length key and crashes at startup.
var jwtSecret   = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
    jwtSecret = "IzaleSparkle-Super-Secret-Key-Must-Be-32-Chars!!";
var jwtIssuer   = builder.Configuration["Jwt:Issuer"]   ?? "IzaleSparkle";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "IzaleSparkleUsers";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer   = true, ValidIssuer   = jwtIssuer,
            ValidateAudience = true, ValidAudience = jwtAudience,
            ValidateLifetime = true, ClockSkew     = TimeSpan.Zero,
        };
        // TV app (old Tizen browser) can't send Authorization headers without a
        // CORS preflight it fails on — accept the token as a query parameter too.
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (string.IsNullOrEmpty(ctx.Token) &&
                    ctx.Request.Query.TryGetValue("access_token", out var t))
                    ctx.Token = t;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    o.AddPolicy("AuthUsers", p => p.RequireAuthenticatedUser());
});

builder.Services.AddCors(o => o.AddPolicy("TvApp", p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddResponseCompression();
builder.Services.AddResponseCaching();
builder.Services.AddRateLimiter(opt =>
{
    opt.AddFixedWindowLimiter("api", l =>
    {
        l.PermitLimit = 100; l.Window = TimeSpan.FromMinutes(1);
        l.QueueProcessingOrder = QueueProcessingOrder.OldestFirst; l.QueueLimit = 10;
    });
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        await DbSeeder.SeedAsync(db, cfg);
    }
    catch (SqlException ex) when (app.Environment.IsDevelopment())
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseStartup");
        logger.LogError(ex,
            "Database seeding failed. The site will still start, but API data and admin features may not work until SQL Server is reachable.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Izale Sparkle API v1"); c.RoutePrefix = "swagger"; });
    app.UseWebAssemblyDebugging();
}
else { app.UseHsts(); }

app.UseCors("TvApp");
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseResponseCaching();
app.UseBlazorFrameworkFiles();

// Serves all wwwroot static files including /uploads/* since uploads folder is inside wwwroot
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers().RequireRateLimiting("api");
app.MapFallbackToFile("index.html");
app.Run();
