using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using System.Text;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Infrastructure.Database;
using TalkLens.Collector.Infrastructure.Repositories;
using TalkLens.Collector.Infrastructure.Services.Telegram;
using TalkLens.Collector.Infrastructure.Extensions;
using TalkLens.Collector.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Регистрация опций для конфигурации
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
builder.Services.Configure<MessageCollectorOptions>(builder.Configuration.GetSection(MessageCollectorOptions.SectionName));
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
// Регистрация вложенных опций
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection(RateLimitOptions.SectionName));
builder.Services.Configure<TelegramCacheOptions>(builder.Configuration.GetSection(TelegramCacheOptions.SectionName));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TalkLens Collector API",
        Version = "v1",
        Description = "API для сбора и анализа данных из мессенджеров"
    });
    
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


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddTransient<Func<TalkLensDbContext>>(_ =>
    () => new TalkLensDbContext(connectionString));

// Настройка JWT аутентификации
builder.Services
    .AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
{
    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();
    
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtOptions?.Issuer,
        ValidAudience = jwtOptions?.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtOptions?.SecretKey ?? string.Empty)),
        ClockSkew = TimeSpan.Zero // Убираем стандартный запас в 5 минут
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
            {
                context.Response.Headers.Append("Token-Expired", "true");
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisOptions = builder.Configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>();
    
    var connectionString = redisOptions?.ConnectionString ?? "localhost:6379";
    var dbNumber = 0;
    
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
var storageOptions = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>();
var storageProvider = storageOptions?.Provider ?? "Minio";

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
builder.Services.AddSingleton<TelegramApiCache>();

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

// Регистрация сервиса мониторинга обновлений Telegram
builder.Services.AddHostedService<TelegramUpdateMonitorService>();

// Регистрация репозиториев
builder.Services.AddScoped<ITelegramSessionRepository, TelegramSessionRepository>();
builder.Services.AddScoped<ITelegramSubscriptionRepository, TelegramSubscriptionRepository>();
builder.Services.AddScoped<ITelegramMessageRepository, TelegramMessageRepository>();
builder.Services.AddScoped<IChatMetricsHistoryRepository, ChatMetricsHistoryRepository>();
builder.Services.AddScoped<ITelegramUserRecommendationRepository, TelegramUserRecommendationRepository>();

// Регистрация сервисов метрик
builder.Services.AddScoped<IChatMetricsService, TelegramChatMetricsService>();

// Регистрация сервиса сбора сообщений
builder.Services.AddSingleton<TelegramMessageCollectorService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<TelegramMessageCollectorService>());

// Регистрация Kafka сервиса
builder.Services.AddKafka(builder.Configuration);

var app = builder.Build();

// Инициализируем сессии из Redis при запуске приложения
using (var scope = app.Services.CreateScope())
{
    try
    {
        Console.WriteLine("[INFO] Начинаем инициализацию сессий из Redis...");
        var redisCache = scope.ServiceProvider.GetRequiredService<RedisTelegramSessionCache>();
        var telegramSessionService = scope.ServiceProvider.GetRequiredService<ITelegramSessionService>();
        
        // Запускаем инициализацию сессий с таймаутом
        var initTask = Task.Run(async () => 
        {
            try 
            {
                Console.WriteLine("[INFO] Запущена задача инициализации сессий из Redis");
                await redisCache.InitializeSessionsFromRedis();
                Console.WriteLine("[INFO] Сессии Telegram успешно инициализированы из Redis");
                
                // Восстанавливаем все активные сессии из базы данных
                Console.WriteLine("[INFO] Начинаем восстановление всех активных сессий из базы данных...");
                var restoredCount = await telegramSessionService.RestoreAllActiveSessionsAsync(CancellationToken.None);
                Console.WriteLine($"[INFO] Восстановлено {restoredCount} активных сессий из базы данных");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Ошибка при инициализации сессий: {ex.Message}");
                Console.WriteLine($"[ERROR] Стек вызовов: {ex.StackTrace}");
            }
        });
        
        var completed = initTask.Wait(TimeSpan.FromSeconds(60));
        
        if (!completed)
        {
            Console.WriteLine("[WARNING] Превышено время ожидания при инициализации сессий. Продолжаем запуск приложения.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Не удалось получить необходимые сервисы для инициализации сессий: {ex.Message}");
        Console.WriteLine($"[ERROR] Стек вызовов: {ex.StackTrace}");
    }
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