# Сервис распределённой блокировки ресурсов на основе in-memory хранилищадля облачной системы управления информационными базами 1С:Предприятие.

Сервис выступает оркестратором блокировок ресурсов: клиенты (узлы облачной системы) запрашивают доступ к ресурсу 
(например, к информационной базе), получая в ответ запись `ResourceLock`. Пока блокировка не истекла или не была удалена, 
ресурс считается занятым операцией `OperationId`.

## Архитектура и структура проекта

```
TCP-клиент
     │
     ▼
TCP-сервер (ArrayPool)
     │
     ▼
Парсер команд (zero-alloc)
      │
      ▼
Ядро хранилища (ReaderWriterLockSlim)
```

| Проект | Назначение |
|---|---|
| `ResourceLocker.Core` | Модель `ResourceLock`, парсер команд, потокобезопасное хранилище |
| `ResourceLocker.Server` | TCP-сервер, точка входа, OpenTelemetry |
| `ResourceLocker.SourceGenerator` | Roslyn Source Generator бинарной сериализации |
| `ResourceLocker.Client` | TCP-клиент протокола (`ResourceLockTcpClient`) — переиспользуется нагрузочным тестом и другими потребителями |
| `ResourceLocker.LoadTest` | Нагрузочные сценарии NBomber |
| `ResourceLocker.Core.Tests` | Юнит-тесты (xUnit) |
| `ResourceLocker.SourceGenerator.Benchmarks` | Бенчмарки BenchmarkDotNet (сериализация, парсер команд) |
### Взаимодействие компонентов

1. TCP-сервер принимает соединение, буферизует входящие байты через `ArrayPool`, накапливает сообщение до символа-терминатора `;`.
2. Собранная строка команды передаётся в `CommandParser.Parse`, который без аллокаций выделяет команду, ключ и значение.
3. В зависимости от команды (`SET`/`GET`/`DELETE`) вызывается соответствующий метод `IResourceLockStore`, значение сериализуется/десериализуется бинарно (Source Generator) для хранения и в JSON — для сетевого обмена с клиентом.
4. Ответ отправляется клиенту, метрики и трейсы публикуются через OpenTelemetry.

## Модель данных

```csharp
public class ResourceLock
{
    public required string ResourceType { get; set; }
    public required string OperationId { get; set; }
    public DateTimeOffset LockedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LeaseExpiresAt { get; set; }
}
```

`ResourceId` не хранится как поле модели — он используется только как ключ команды/словаря.

Истечение аренды (`LeaseExpiresAt`): при `GET` сервер сравнивает `LeaseExpiresAt` с текущим временем и возвращает `(nil)`, если аренда истекла, даже если запись физически ещё присутствует в хранилище. 
to-do: Добавить очистку протухших записей.

## Протокол

Команды передаются текстом, разделены пробелом, завершаются символом `;`:

```
SET <key> <json>;
GET <key>;
DELETE <key>;
```

Пример захвата блокировки:

```
set infobase-1 {"resourceType":"1C:InfoBase","operationId":"operation-42","leaseExpiresAt":"2026-09-01T23:00:00+00:00"};
→ OK

get infobase-1;
→ {"ResourceType":"1C:InfoBase","OperationId":"operation-42","LockedAt":"...","LeaseExpiresAt":"2026-09-01T23:00:00+00:00"}

delete infobase-1;
→ OK

get infobase-1;
→ (nil)
```

JSON-десериализация регистронезависима (`PropertyNameCaseInsensitive = true`), поэтому имена свойств можно передавать как в camelCase, так и в PascalCase.

## Запуск

Сборка решения:

```bash
dotnet build ResourceLocker.sln
```

Запуск сервера (слушает `127.0.0.1:8080`):

```bash
dotnet run --project ResourceLocker.Server
```

Юнит-тесты:

```bash
dotnet test ResourceLocker.Core.Tests
```

Бенчмарки (обязательно в Release):

```bash
dotnet run --project ResourceLocker.SourceGenerator.Benchmarks -c Release
```

Нагрузочное тестирование (сервер должен быть запущен отдельно):

```bash
dotnet run --project ResourceLocker.LoadTest -c Release
```

## Результаты тестов

### BenchmarkDotNet

Окружение: 11th Gen Intel Core i5-1135G7 2.40GHz, .NET 8.0.25, Linux, Release-сборка.

**Сериализация `ResourceLock` (Source Generator vs System.Text.Json):**

| Метод | Mean | StdDev | Allocated |
|---|---:|---:|---:|
| `TestSourceGenerator` (бинарный, Source Generator) | 1 107.7 ns | 54.15 ns | 1 728 B |
| `TestSystemTextJson` | 895.3 ns | 23.05 ns | 456 B |

В данном сценарии сгенерированная бинарная сериализация через `BinaryWriter`/`MemoryStream` оказалась медленнее и затратнее по аллокациям, чем `System.Text.Json` — накладные расходы дают промежуточные `MemoryStream`/`BinaryWriter`. 

**Парсер команд (`CommandParser.Parse`):**

| Метод | Mean | StdDev | Allocated |
|---|---:|---:|---:|
| `ParseSetCommand` | 36.45 ns | 0.185 ns | **0 B** |

Парсер команд на `ReadOnlySpan<char>` подтверждённо не выделяет управляемую память на разбор команды — цель zero-allocation парсинга достигнута.

### NBomber (нагрузочное тестирование)

Три сценария по 10 c прогрева + 30 c нагрузки с интенсивностью 100 запросов/сек каждый, сервер — Release-сборка, локальный TCP (`127.0.0.1:8080`):

| Сценарий | Запросов | OK | Fail | RPS | p50 (ms) | p95 (ms) | p99 (ms) |
|---|---:|---:|---:|---:|---:|---:|---:|
| `lock resource on tcp-server` (SET) | 3000 | 3000 | 0 | 100 | 0.46 | 1.39 | 3.42 |
| `get lock from tcp-server` (GET) | 3000 | 1394 | 1606 | 46.5 | 0.46 | 1.34 | 3.28 |
| `unlock resource on tcp-server` (DELETE) | 3000 | 3000 | 0 | 100 | 0.44 | 1.32 | 3.32 |

Сценарий `GET` в тестовом клиенте использует случайный ключ из диапазона `resource:0`–`resource:99` независимо от того, была ли по этому ключу ранее установлена блокировка сценарием `SET` — часть промахов (`fail`, они же корректные `(nil)`-ответы) в этом отчёте ожидаема и не указывает на дефект сервера.

Сервер стабильно держит 100 RPS на операциях `SET`/`DELETE` при задержке p99 ниже 3.5 мс на локальном соединении без единой ошибки соединения.

## Продвинутые возможности

- **Source Generator** (`ResourceLocker.SourceGenerator`) — генерирует бинарный сериализатор для `ResourceLock` во время компиляции, без рефлексии в рантайме.
- **OpenTelemetry** (`ResourceLocker.Server/Program.cs`) — метрики (счётчики и гистограммы длительности по каждой команде: `set_command_total`, `get_command_total`, `delete_command_total` и соответствующие `*_duration_seconds`) и трейсинг (`ActivitySource` на каждую обработанную команду) экспортируются в консоль через `OpenTelemetry.Exporter.Console`.
