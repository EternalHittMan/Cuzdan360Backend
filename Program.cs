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
    Console.WriteLine($"[DEBUG] ConnectionString Server: {connectionString?.Split(';').FirstOrDefault(s => s.StartsWith("Server="))}");
    
    // Use a fixed version to avoid connection attempts during startup/configuration
    // This fixes the race condition where app starts before DB is ready.
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 44)), 
        mySqlOptions => mySqlOptions.EnableRetryOnFailure()
    );
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNextApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:9003", "http://localhost:3000") 
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

builder.Services.Configure<JwtConfiguration>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddTransient<JwtConfiguration>();
builder.Services.AddTransient<TokenService>();
builder.Services.AddHostedService<TokenCleanupService>();

builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen(); // Duplicate removed
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


    // --- DATABASE MIGRATION START ---
    // Skip migration if running from EF Tool (to avoid connection loop during design time)
    if (Environment.GetEnvironmentVariable("EF_TOOL") != "true")
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Retry policy for database connection/migration
        int maxRetries = 10;
        int delaySeconds = 2;
        
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                if (dbContext.Database.CanConnect())
                {
                     dbContext.Database.Migrate();
                     break; // Success
                }
                else
                {
                    throw new Exception("Cannot connect to database.");
                }
            }
            catch (Exception ex)
            {
                var logger = app.Services.GetRequiredService<ILogger<Program>>();
                if (i == maxRetries - 1)
                {
                    logger.LogCritical(ex, "Database migration failed after {MaxRetries} attempts.", maxRetries);
                    throw; // Re-throw on last attempt
                }
                logger.LogWarning("Database migration attempt {Attempt} failed. Retrying in {Delay}s... Error: {Message}", i + 1, delaySeconds, ex.Message);
                System.Threading.Thread.Sleep(delaySeconds * 1000);
            }
        }
    }
    catch (Exception ex)
    {
        // Log critical failure
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogCritical(ex, "Startup aborted due to database migration failure.");
        throw; 
    }
    }
    // --- DATABASE MIGRATION END ---

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