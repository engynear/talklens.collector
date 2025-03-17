#!/bin/bash

# Проверяем наличие goose
if ! command -v goose &> /dev/null; then
    echo "goose не установлен. Установите его с помощью:"
    echo "go install github.com/pressly/goose/v3/cmd/goose@latest"
    exit 1
fi

# Параметры подключения к базе данных
DB_HOST=${POSTGRES_HOST:-postgres}
DB_PORT=${POSTGRES_PORT:-5432}
DB_NAME=${POSTGRES_DB:-talklens_auth}
DB_USER=${POSTGRES_USER:-postgres}
DB_PASSWORD=${POSTGRES_PASSWORD:-postgres}

# Формируем строку подключения
DB_STRING="host=$DB_HOST port=$DB_PORT dbname=$DB_NAME user=$DB_USER password=$DB_PASSWORD sslmode=disable"

# Запускаем миграции
echo "Запуск миграций..."
goose -dir . postgres "$DB_STRING" up

if [ $? -eq 0 ]; then
    echo "Миграции успешно применены"
else
    echo "Ошибка при применении миграций"
    exit 1
fi 