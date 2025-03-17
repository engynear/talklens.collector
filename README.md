# TalkLens.Collector

Микросервис для сбора метрик из мессенджеров в рамках проекта TalkLens.

## Требования

- .NET 8.0 SDK
- Docker и Docker Compose
- PostgreSQL (доступен через Docker Compose)
- Goose (для миграций)

## Структура проекта

Проект построен на принципах Clean Architecture и разделен на следующие слои:

- **TalkLens.Collector.Domain** - доменные сущности и интерфейсы
- **TalkLens.Collector.Infrastructure** - реализация доступа к данным и внешним сервисам
- **TalkLens.Collector.Api** - API контроллеры и конфигурация приложения

## Развертывание

1. Клонируйте репозиторий:
```bash
git clone https://github.com/your-username/TalkLens.Collector.git
cd TalkLens.Collector
```

2. Установите goose (если еще не установлен):
```bash
go install github.com/pressly/goose/v3/cmd/goose@latest
```

3. Запустите миграции:
```bash
cd TalkLens.Collector.Infrastructure/Migrations
chmod +x run_migrations.sh
./run_migrations.sh
```

4. Запустите приложение через Docker Compose:
```bash
docker-compose up -d
```

## Конфигурация

### Переменные окружения

Сервис использует следующие переменные окружения:

- `POSTGRES_DB` - имя базы данных (по умолчанию: talklens_auth)
- `POSTGRES_USER` - пользователь базы данных (по умолчанию: postgres)
- `POSTGRES_PASSWORD` - пароль базы данных (по умолчанию: postgres)
- `POSTGRES_HOST` - хост базы данных (по умолчанию: postgres)
- `POSTGRES_PORT` - порт базы данных (по умолчанию: 5432)
- `Jwt__Secret` - секретный ключ для проверки JWT токенов
- `Jwt__Issuer` - издатель JWT токенов
- `Jwt__Audience` - аудитория JWT токенов

### JWT

Настройте JWT в файле `TalkLens.Collector.Api/appsettings.json` или через переменные окружения:
```json
{
  "Jwt": {
    "Secret": "your-256-bit-secret-your-256-bit-secret-your-256-bit-secret",
    "Issuer": "talklens-auth",
    "Audience": "talklens-collector"
  }
}
```

**Важно**: Секретный ключ должен быть одинаковым во всех микросервисах для корректной проверки токенов.

## API Endpoints

### Messenger Sessions

- `GET /api/messengersessions` - получить все сессии пользователя
- `GET /api/messengersessions/{id}` - получить сессию по ID
- `POST /api/messengersessions` - создать новую сессию
- `POST /api/messengersessions/{id}/connect` - подключить сессию
- `POST /api/messengersessions/{id}/disconnect` - отключить сессию
- `DELETE /api/messengersessions/{id}` - удалить сессию

## Аутентификация

Все API endpoints защищены JWT-токенами. Токен должен быть передан в заголовке `Authorization` в формате:
```
Authorization: Bearer <your-jwt-token>
```

## Разработка

### Добавление нового мессенджера

1. Добавьте новый тип в enum `MessengerType`
2. Создайте новый класс, реализующий интерфейс `IMessengerClient`
3. Зарегистрируйте новый клиент в `Program.cs`

### Сбор метрик

Для сбора метрик из мессенджера:
1. Реализуйте логику сбора в методе `StartCollectingMetricsAsync` соответствующего клиента
2. Сохраняйте метрики в базу данных через соответствующий репозиторий

### Миграции

Для создания новой миграции:
```bash
cd TalkLens.Collector.Infrastructure/Migrations
goose create new_migration_name sql
```

## Лицензия

MIT 