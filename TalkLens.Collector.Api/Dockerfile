FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем файлы проектов
COPY ["TalkLens.Collector.Api/TalkLens.Collector.Api.csproj", "TalkLens.Collector.Api/"]
COPY ["TalkLens.Collector.Domain/TalkLens.Collector.Domain.csproj", "TalkLens.Collector.Domain/"]
COPY ["TalkLens.Collector.Infrastructure/TalkLens.Collector.Infrastructure.csproj", "TalkLens.Collector.Infrastructure/"]

# Восстанавливаем зависимости
RUN dotnet restore "TalkLens.Collector.Api/TalkLens.Collector.Api.csproj"

# Копируем исходный код
COPY . .

# Собираем приложение
RUN dotnet build "TalkLens.Collector.Api/TalkLens.Collector.Api.csproj" -c Release -o /app/build

# Публикуем приложение
FROM build AS publish
RUN dotnet publish "TalkLens.Collector.Api/TalkLens.Collector.Api.csproj" -c Release -o /app/publish

# Создаем финальный образ
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TalkLens.Collector.Api.dll"] 