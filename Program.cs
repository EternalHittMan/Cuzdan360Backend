using System.Threading.RateLimiting;
using Cuzdan360Backend.Configurations;
using Microsoft.EntityFrameworkCore;
using Cuzdan360Backend.Data;
using Cuzdan360Backend.Extensions;
using Cuzdan360Backend.Middlewares;
using Cuzdan360Backend.Repositories;
using Cuzdan360Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

// ------------------------
// Services Configuration
// ------------------------

// Memory Cache
builder.Services.AddMemoryCache();

// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure()
    );
});

// Controllers
builder.Services.AddControllers();

// *** YENİ EKLENDİ: CORS Politikası ***
// Next.js (http://localhost:3000) uygulamasından gelen isteklere izin ver
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNextApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:3000") // Next.js'in çalıştığı adres
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});
// *** BİTTİ: CORS Politikası ***


// JWT Configuration
builder.Services.Configure<JwtConfiguration>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddTransient<JwtConfiguration>();
builder.Services.AddTransient<TokenService>();
builder.Services.AddHostedService<TokenCleanupService>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerGen(SwaggerConfiguration.Configure);

// JWT Authentication
builder.Services.AddJwtAuthentication(builder.Configuration);

// Repositories & Services
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddHttpContextAccessor();

// Email
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<SmtpSettings>>().Value);

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Services.AddLogging();

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("LoginLimit", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString(),
            factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));

    options.AddPolicy("ForgotPasswordLimit", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString(),
            factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = 3, Window = TimeSpan.FromMinutes(10) }));

    options.AddPolicy("OTPLimit", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString(),
            factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = 2, Window = TimeSpan.FromMinutes(5) }));
});

// Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireClaim("Permission", "1"));
});

// ------------------------
// Build App
// ------------------------
var app = builder.Build();

// ------------------------
// Database Migration
// ------------------------
try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred while migrating the database.");
}

// ------------------------
// Middleware Pipeline
// ------------------------
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

// Swagger her ortamda çalışsın
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Cuzdan360 API V1");
    c.RoutePrefix = string.Empty; // Swagger UI kök URL’de açılır
});

app.UseHttpsRedirection();

// *** YENİ EKLENDİ: CORS Middleware ***
// CORS politikasını Authentication ve Authorization'dan ÖNCE uygulayın
// Bu, tarayıcının preflight (OPTIONS) isteklerinin başarılı olmasını sağlar.
app.UseCors("AllowNextApp");

// *** TAŞINDI: Rate Limiter ***
// Rate Limiter'ı kimlik doğrulamadan önce uygulamak daha iyidir.
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
// app.UseRateLimiter(); // Buradan yukarı taşındı
app.MapControllers();

// ------------------------
// Run App
// ------------------------
app.Run();