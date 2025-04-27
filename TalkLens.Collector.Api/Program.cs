using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using System.Text;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Infrastructure.Database;
using TalkLens.Collector.Infrastructure.Repositories;
using TalkLens.Collector.Infrastructure.Services;
using TalkLens.Collector.Infrastructure.Services.Telegram;

var builder = WebApplication.CreateBuilder(args);

// Добавляем сервисы в контейнер
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Настраиваем Swagger с поддержкой JWT
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TalkLens Collector API",
        Version = "v1",
        Description = "API для сбора и анализа данных из мессенджеров"
    });

    // Добавляем определение безопасности для JWT Bearer
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Введите JWT-токен в формате: Bearer {ваш_токен}"
    });

    // Добавляем требование безопасности для всех методов API
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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

// Настройка подключения к БД
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddScoped<TalkLensDbContext>(sp => new TalkLensDbContext(connectionString));

// Настройка JWT аутентификации
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"])),
        ClockSkew = TimeSpan.Zero // Убираем стандартный запас в 5 минут
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
            {
                context.Response.Headers.Add("Token-Expired", "true");
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// Добавляем SignalR
builder.Services.AddSignalR();

// Регистрируем Redis ConnectionMultiplexer как синглтон
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    string connectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
    int dbNumber = 0;
    if (int.TryParse(builder.Configuration["Redis:DbNumber"], out int configDbNumber))
    {
        dbNumber = configDbNumber;
    }
    
    var options = new ConfigurationOptions
    {
        EndPoints = { connectionString },
        DefaultDatabase = dbNumber
    };
    
    return ConnectionMultiplexer.Connect(options);
});

// Регистрация зависимостей для кэширования
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<RedisTelegramSessionCache>();
builder.Services.AddSingleton<RedisTelegramApiCache>();

// Регистрация хранилища сессий Telegram
var storageProvider = builder.Configuration["Storage:Provider"] ?? "Minio";
if (storageProvider.Equals("Minio", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ITelegramSessionStorage, MinioTelegramSessionStorage>();
    Console.WriteLine("Зарегистрировано хранилище сессий Telegram на базе MinIO");
}
else
{
    // В рамках этой задачи реализуем только MinIO
    builder.Services.AddSingleton<ITelegramSessionStorage, MinioTelegramSessionStorage>();
    Console.WriteLine($"Хранилище типа '{storageProvider}' не поддерживается, используется MinIO");
}

// Регистрация сервисов для ограничения запросов и кэширования
builder.Services.AddSingleton<TelegramRateLimiter>();

// Используем стандартный TelegramApiCache, так как редис кэш еще не работает корректно
builder.Services.AddSingleton<TelegramApiCache>(provider => 
    new TelegramApiCache(
        provider.GetRequiredService<IMemoryCache>(),
        provider.GetRequiredService<IConfiguration>(),
        provider.GetRequiredService<ILogger<TelegramApiCache>>()));

// Для отладки добавляем вывод информации о Redis ConnectionMultiplexer
using (var scope = builder.Services.BuildServiceProvider().CreateScope())
{
    try
    {
        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        var db = redis.GetDatabase();
        Console.WriteLine($"[DEBUG] Redis ConnectionMultiplexer успешно создан. Доступно DB: {db.Database}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Ошибка при создании ConnectionMultiplexer: {ex.Message}");
    }
}

// Регистрация менеджера сессий Telegram
builder.Services.AddSingleton<TelegramSessionManager>();

// Регистрация сервисов сессий
builder.Services.AddScoped<ITelegramSessionService, TelegramSessionService>();
// Также регистрируем сервис как реализацию ISessionService для паттерна стратегия
builder.Services.AddScoped<ISessionService>(provider => 
    provider.GetRequiredService<ITelegramSessionService>());

builder.Services.AddScoped<ITelegramSessionRepository, TelegramSessionRepository>();

// Регистрируем сервисы
builder.Services.AddSingleton<TelegramMessageCollectorService>();
builder.Services.AddSingleton<TelegramSubscriptionManager>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TelegramMessageCollectorService>());

var app = builder.Build();

// Инициализируем Redis кэш для TelegramClientCache
try
{
    using (var scope = app.Services.CreateScope())
    {
        var redisCache = scope.ServiceProvider.GetRequiredService<RedisTelegramSessionCache>();
        TelegramClientCache.InitializeRedisCache(redisCache);
        Console.WriteLine("Redis кэш для Telegram сессий успешно инициализирован");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[ERROR] Ошибка при инициализации Redis кэша: {ex.Message}");
    Console.WriteLine("Будет использоваться локальное кэширование сессий в памяти");
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "TalkLens Collector API v1");
    options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    options.EnableDeepLinking();
    options.DisplayRequestDuration();
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run(); 