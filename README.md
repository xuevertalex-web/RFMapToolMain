# RFMapToolSharp

Консольный инструмент на C# (.NET 8) для карт RF Online: чтение геометрии, материалов,
текстур и анимаций объектов из игровых форматов и экспорт в `.glb` (glTF 2.0),
а также задел на обратное направление (GLB → BSP, репак карт).

## Структура репозитория

| Папка | Назначение |
|---|---|
| `Maps/Map/` | Входные карты клиента RF Online (`.bsp`, `.r3m`, `.r3t`, `.spt` и пр.). Не коммитится. |
| `ReadyMaps/` | Готовые результаты экспорта (см. ниже). Не коммитится. |
| `Parsing/` | Парсеры форматов: BSP/EBP, R3M (материалы), R3T (текстуры), SPT, RPK и др. |
| `Export/` | Экспорт в GLB (SharpGLTF), диагностика, мост SPT-моделей. |
| `Editor/` | Задел редактора карт (шаблоны, dry-run). |
| `tools/` | Вспомогательные утилиты (инвентарь ресурсов, guard-скрипты). |
| `_runs/` | Логи пакетных прогонов. Не коммитится. |

## Структура результата (`ReadyMaps/`)

```
ReadyMaps/
  <map>/<map>.glb            — готовая карта (только GLB, без мусора)
  _diagnostics/<map>/*.json  — отладочные дампы (ноды, матрицы объектов, UV-аномалии…)
  _reports/texture_report_<map>.json — сводка по текстурам
  _logs/<map>_export_<ts>.log        — tee-лог консоли прогона
```

`--cleanup-diagnostics` удаляет legacy-диагностику из `<map>/` (интерактивно спрашивает
подтверждение; в батче — no-op).

## Что умеет экспорт

- **Геометрия BSP** (`.bsp`): вершины, грани, матгруппы; фильтры вытянутых/аномальных
  треугольников (по умолчанию только логируются, не вырезаются).
- **Материалы** (`.r3m`): привязка текстур, эвристики прозрачности (по имени и alpha type).
- **Текстуры** (`.r3t`): DDS → PNG в памяти (DXT1/3/5, ATI, BC7), встраивание в GLB.
- **Анимации BSP-объектов** (все карты, все анимированные объекты):
  - mesh-ноды вложены в иерархию объектных нод `BSP_obj{id}` по parent-chain;
  - каналы — world-дельты от запечённой позы, статичная поза не меняется
    (на baked-кадре цепочка = identity);
  - объекты с анизотропным scale, где дельта содержит shear и не раскладывается в TRS,
    факторизуются в точную цепочку из трёх TRS-нод `_inv/_rot/_scl`
    (пример: Sette obj1/2, scale ~0.007 по одной оси);
  - статичные объекты каналов не получают, пустых `animations` в GLB нет.
- **SPT-объекты**: debug-маркеры (кубы) с позицией/поворотом/масштабом.
  Реальные модели по `modelName` пока не подгружаются (см. Roadmap).

## Требования и сборка

- Windows, .NET SDK 8 (запинен в `global.json`: 8.0.424)
- Зависимости: SharpGLTF, Pfim, SixLabors.ImageSharp

```bash
dotnet build -c Debug
```

## Запуск

Без аргументов — интерактивное меню. Папка карт ищется автоматически:
`rf_path.txt` рядом с exe → `<cwd>/Map|map` → обход вверх от cwd и exe →
`C:\Games\RF_Online\Map`.

### Основные флаги (batch mode)

| Флаг | Описание |
|---|---|
| `--all` | Экспорт всех карт без меню |
| `--map Sette` | Экспорт по части имени карты |
| `--filter=Vol*` | Экспорт по маске (`*`, `?`, без учёта регистра) |
| `--resume` | Пропускать карты, у которых GLB новее всех исходников |
| `--validate` | Round-trip проверка каждого GLB через SharpGLTF; exit code `0/1/2` = OK/warnings/errors |
| `--cleanup-diagnostics` | Чистка legacy-диагностики (с подтверждением) |

Пример полного прогона:

```bash
cd Maps
../bin/Debug/net8.0/RFMapToolSharp.exe --all --validate
```

### Флаги режимов трансформации объектов (диагностические)

`--no-object-transform`, `--force-object-transform`, `--object-transform-mode`,
`--object-translation-mode`, `--animated-objects-mode`, `--object-transform-target`,
`--strict-legacy-object-transform`, `--frame`, `--decompress-mode` — переключают
варианты запекания object transform. В этих режимах иерархия нод отключается
и используется плоский fallback-экспорт каналов.

### SPT

`--spt-mode off|markers|real-if-supported`, `--spt-rot-order`, `--spt-scale-mul`,
`--no-spt-pivot-fix`.

### Обратное направление (задел, частично работает)

`--glb-to-bsp-*`, `--glb-insert-*`, `--glb-to-rf-*`, `--repack-map`, `--repack-bsp`,
`--bsp-dump`, `--bsp-patch`, `--bsp-apply`, `--entity-report`, `--rf-inventory-*`.
См. `Program.cs` — эти режимы в активной разработке.

## Диагностика

- Логи стадий: `[TEX]` (загрузка `.r3t`), `[CONV]` (DDS→PNG с форматом и размером),
  `[GLTF]` (материалы, иерархия, анимации).
- `texture_report_<map>.json`: счётчики total/found/converted/embedded/failed,
  список материалов без текстур.
- Дампы анимаций объектов: переменная окружения `RF_DEBUG_OBJANIM=1` пишет
  `objanim_frames.json` (world-матрицы по кадрам) и `objanim_tracks_obj*.json`
  (сырые pos/rot/scale треки) в `_diagnostics/<map>/`.

## Актуальный статус (2026-09)

- Батч-экспорт всех 41 BSP-карты проходит; `--validate` даёт code=1 (warnings)
  на всех, кроме `Dungeon00` (code=2 — известное ограничение: сжатые вершины
  формата 2004 года парсером пока не поддерживаются).
- Sette: все 8 анимированных BSP-объектов экспортируются с точными каналами
  (включая shear-факторизацию obj1/2).

## Известные ограничения

- `Dungeon00`: сжатые вершины старого формата — вне текущего scope.
- SPT-объекты — debug-кубы, не реальные модели.
- `.ebp`, `.rvp`, `.r3x` читаются, но не интегрированы в финальную сцену.
- Качество материалов/UV может отличаться от игры на сложных картах.
- В части исходников встречается mojibake (смешение UTF-8/Windows-1251)
  в старых комментариях — на работу не влияет.
- NU1902: предупреждение безопасности `SixLabors.ImageSharp 3.1.7` (medium).

## Roadmap

- Резолвер `modelName` → реальные модели SPT вместо debug-кубов.
- Более точная реконструкция материалов и шейдерных параметров RF.
- Интеграция `.ebp`, `.rvp`, `.r3x` в GLB-сцену.
- Проверка parent-chain анимаций на картах с иерархией объектов
  (на текущих 41 карте все объекты корневые).
- Чистка warning'ов, исправление кодировок, обновление пакетов.

## Поисковые ключи

RF Online, RFOnline, Rising Force Online, RF map tool, RF Online map export,
RF Online BSP, RF Online R3M, RF Online R3T, RF Online SPT, RF Online GLB,
RF Online GLTF, RF Online modding, RF Online tools.
