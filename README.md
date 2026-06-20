# Помощник кладовщика

Тестовое приложение для учета складских движений: приход товара на склад извне, расход со склада, перемещение между складами и отчет по остаткам на выбранное время.

## Стек

- Backend: ASP.NET Core Web API, .NET 10
- ORM: Entity Framework Core 10
- DB: PostgreSQL
- Frontend: Vue 3 + Vite + TypeScript
- Tests: xUnit
- API UI: Scalar + OpenAPI

Исходное ТЗ указывало ASP.NET Core 3.x и EF Core 2.2/3.0, но задание старое. Проект реализован на актуальном стеке .NET 10 / EF Core 10.

## Реализовано

- Справочник складов: `Id`, `Name`, seed из 3 складов.
- Справочник номенклатур: `Id`, `Name`, seed из 7 позиций.
- Движения:
  - приход: извне на склад;
  - расход: со склада вне компании;
  - перемещение: со склада на склад.
- В одном движении может быть несколько ТМЦ.
- Номенклатуры в одном движении не повторяются.
- Количество должно быть больше нуля.
- Запрещен расход/перемещение, если после операции остаток станет отрицательным.
- Список движений с временем, направлением, ТМЦ и удалением.
- Отчет остатков по складу на выбранное время.
- Unit-тесты расчета остатков и основных валидаций.

## Структура

```text
warehouse-assistant
  src/WarehouseAssistant.Api
  tests/WarehouseAssistant.Tests
  frontend
  docker-compose.yml
  README.md
```

## Запуск

### 1. PostgreSQL

```powershell
docker compose -p warehouse_assistant up -d
```

Порт PostgreSQL на хосте: `55432`.

### 2. Миграции

```powershell
dotnet tool restore
dotnet ef database update --project src/WarehouseAssistant.Api --startup-project src/WarehouseAssistant.Api
```

### 3. Backend

```powershell
dotnet run --project src/WarehouseAssistant.Api
```

Backend по умолчанию: `http://localhost:5052`.

Scalar UI:

```text
http://localhost:5052/scalar/v1
```

OpenAPI JSON:

```text
http://localhost:5052/openapi/v1.json
```

### 4. Frontend

```powershell
cd frontend
npm install
npm run dev
```

Frontend: `http://localhost:5173`.

## Проверка

Backend tests:

```powershell
dotnet test
```

Frontend build:

```powershell
cd frontend
npm run build
```

## API

```text
GET    /api/catalogs
GET    /api/movements
POST   /api/movements
DELETE /api/movements/{id}
GET    /api/stocks?warehouseId=1&at=2026-06-19T12:00:00Z
```

## Что можно добавить сверх ТЗ

- CRUD справочников.
- Редактирование движений.
- Авторизацию и роли.
- Аудит создания/удаления движений.
- Интеграционные тесты API на PostgreSQL через Testcontainers.
- Производственный Dockerfile для API и frontend.
