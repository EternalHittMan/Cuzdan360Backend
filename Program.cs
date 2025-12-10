using System.Threading.RateLimiting;
using Cuzdan360Backend.Configurations;
using Microsoft.EntityFrameworkCore;
using Cuzdan360Backend.Data;
using Cuzdan360Backend.Extensions;
using Cuzdan360Backend.Middlewares;
using Cuzdan360Backend.Repositories;
using Cuzdan360Backend.Services; // 👈 Gerekli
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

// ------------------------
// Services Configuration
// ------------------------

builder.Services.AddMemoryCache();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure()
    );
});

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNextApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:9003") 
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

builder.Services.Configure<JwtConfiguration>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddTransient<JwtConfiguration>();
builder.Services.AddTransient<TokenService>();
builder.Services.AddHostedService<TokenCleanupService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerGen(SwaggerConfiguration.Configure);

builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EmailService>();

// --- YENİ EKLENDİ ---
// NewsService'i IHttpClientFactory (HTTP istekleri için) ile birlikte kaydet
builder.Services.AddHttpClient<NewsService>();
builder.Services.AddScoped<NewsService>();

// Gemini Receipt Service
builder.Services.AddHttpClient<GeminiReceiptService>();
builder.Services.AddScoped<GeminiReceiptService>();

// Financial Advice Service
builder.Services.AddHttpClient<AdviceService>();
builder.Services.AddScoped<AdviceService>();

// Background Worker
builder.Services.AddHostedService<RecurringTransactionWorker>();
// --- BİTİŞ ---


builder.Services.AddHttpContextAccessor();

builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<SmtpSettings>>().Value);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Services.AddLogging();

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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireClaim("Permission", "1"));
});

var app = builder.Build();

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

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Cuzdan360 API V1");
    c.RoutePrefix = string.Empty; 
});

app.UseHttpsRedirection();

app.UseCors("AllowNextApp");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();