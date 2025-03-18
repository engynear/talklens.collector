using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Infrastructure.Database;
using TalkLens.Collector.Infrastructure.Repositories;
using TalkLens.Collector.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Добавляем сервисы в контейнер
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// Регистрация зависимостей
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ITelegramSessionService, TelegramSessionService>();
builder.Services.AddScoped<ITelegramSessionRepository, TelegramSessionRepository>();

// Регистрируем сервисы
builder.Services.AddSingleton<TelegramMessageCollectorService>();
builder.Services.AddSingleton<TelegramSubscriptionManager>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TelegramMessageCollectorService>());

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run(); 