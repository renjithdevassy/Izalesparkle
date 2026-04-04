using IzaleSparkle.Application;
using IzaleSparkle.Infrastructure;
using IzaleSparkle.Infrastructure.Persistence;
using IzaleSparkle.Server.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
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
var jwtSecret   = builder.Configuration["Jwt:Secret"]   ?? "IzaleSparkle-Super-Secret-32Chars!";
var jwtIssuer   = builder.Configuration["Jwt:Issuer"]   ?? "IzaleSparkle";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "IzaleSparkleUsers";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer   = true, ValidIssuer   = jwtIssuer,
        ValidateAudience = true, ValidAudience = jwtAudience,
        ValidateLifetime = true, ClockSkew     = TimeSpan.Zero,
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    o.AddPolicy("AuthUsers", p => p.RequireAuthenticatedUser());
});

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
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    await DbSeeder.SeedAsync(db, cfg);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Izale Sparkle API v1"); c.RoutePrefix = "swagger"; });
    app.UseWebAssemblyDebugging();
}
else { app.UseHsts(); }

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
