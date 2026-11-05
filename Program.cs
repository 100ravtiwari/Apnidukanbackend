using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using Dukan.API.Data;
using Dukan.API.Middleware;
using Dukan.API.Services;
using Dukan.API.Hubs;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Controllers & SignalR WebSockets
builder.Services.AddControllers();
builder.Services.AddSignalR();

// 2. Configure Database (Microsoft SQL Server)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? "Server=localhost;Database=DukanDB;Trusted_Connection=True;TrustServerCertificate=True;";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    }));

// 2b. Configure Redis (StackExchange.Redis) with Resilient Background Connection
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var redisConfig = config.GetSection("Redis");
    var redisConnString = redisConfig["Configuration"]
        ?? $"{redisConfig["Host"] ?? "34.93.68.9:6379"},password={redisConfig["Password"]},abortConnect=false,connectTimeout=5000,syncTimeout=5000";

    var options = ConfigurationOptions.Parse(redisConnString);
    options.AbortOnConnectFail = false; // Never crash on startup if network/host is initializing
    options.ConnectTimeout = 5000;
    options.SyncTimeout = 5000;
    return ConnectionMultiplexer.Connect(options);
});

// 3. Register Application Services
builder.Services.AddSingleton<IRedisPersonaService, RedisPersonaService>();
builder.Services.AddScoped<IHomepageService, HomepageService>();
builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IStorageService, GoogleCloudStorageService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ISmsService, SmsService>();

// 4. Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Key"] ?? "Dukan_Super_Secret_Fallback_Key_1234567890!";

var keyBytes = Encoding.UTF8.GetBytes(secretKey);
var symmetricKey = new SymmetricSecurityKey(keyBytes) { KeyId = "dukan-jwt-key" };

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = symmetricKey,
        IssuerSigningKeys = new[] { symmetricKey },
        IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) => new[] { symmetricKey },
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"] ?? "DukanAPI",
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"] ?? "DukanClient",
        ClockSkew = TimeSpan.Zero
    };

    // Support WebSocket JWT token via query string
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/order"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// 5. Configure CORS (allow frontend client requests and SignalR WebSockets)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// 6. Configure Swagger with JWT Bearer support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Dukan Store API",
        Version = "v1",
        Description = "Complete E-Commerce backend for Kirana / Retail Shop with Auth, Inventory, Cart and Orders."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// 7. Auto-migrate and Seed Database
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbInitializer.InitializeAsync(dbContext);
}

// 8. Middleware Pipeline
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Dukan Store API v1");
    c.RoutePrefix = "swagger"; // Available at http://localhost:<port>/swagger
});

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<OrderHub>("/hubs/order");

// Health check endpoint for Kubernetes probes & monitoring
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

app.Run();
