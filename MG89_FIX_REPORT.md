# Отчёт: фикс mg89-92 (Sette) + рефакторинг диагностики

Дата: 2026-09-05. Ветка: main (backup: `backup/pre-mg89-fix`).

## 1. Диагностика Части А: проверка гипотез

| Гипотеза | Вердикт | Доказательство |
|---|---|---|
| **H1: 26cf98a сломал трансформации** | ✅ **ПОДТВЕРЖДЕНА** | `git show 26cf98a -- Program.cs`: при рефакторинге цикла экспорта удалены строки `SkipTransformForAttr8192 = !mapName.Equals("Sette")` и `SkipTransformObjectIds.Clear()` |
| H2: потеря parent-child/TRS нод | ❌ опровергнута | Иерархия нод не менялась; mg89-92 — плоские ноды с baked-вершинами |
| H3: парсинг FVertex-пула | ❌ опровергнута | `BspFile.cs` не менялся с d54b712 (`git diff d54b712..HEAD` пуст); functionId=4 → raw FVertex, как в donor-версии |
| H4: привязка к анимационным точкам | ❌ (как отдельная причина) | ObjectId=9 статичен (Frames=0), анимация не нужна; нужен был именно object transform |

### Механика регрессии

mg89-92 (и mg88) в Sette — группы `attr=8192, objectId=9`. Дефолт `SkipTransformForAttr8192 = true`
(«safety override») пропускал их object transform. Рабочая версия (≤ d54b712) для Sette
**выключала** этот пропуск → вершины получали матрицу объекта 9 → единая группа в центре карты.
26cf98a случайно выкинул per-map override → Sette попала под глобальный skip → модели остались
в локальных координатах 0..3500 у начала координат («слетели»).

До/после (центры нод mg89-92 в GLB):
- **Сломано:** min ≈ 0, max ≈ 2800–3500 (raw local space)
- **Исправлено:** центры ≈ (−140..−290, −140..−300, −140..−300) — единый кластер, трансформ объекта 9 применён

Код геометрии после фикса побайтово эквивалентен последней известной рабочей версии d54b712
(проверено `git diff` — отличия только в диагностических логах).

## 2. Патч А — `5b33f33 fix(sette)`

`Program.cs`: восстановлены 4 строки в начале цикла экспорта карты (до `BspFile.Load`):

```csharp
RFMapToolSharp.Collision.BspFile.SkipTransformForAttr8192 =
    !string.Equals(mapName, "Sette", StringComparison.OrdinalIgnoreCase);
RFMapToolSharp.Collision.BspFile.SkipTransformObjectIds.Clear();
```

Локальный фикс, BSP-парсер не тронут, поведение других карт не изменено (для них skip остаётся).

## 3. Патч Б — `ef84770 refactor(export)`

Новый `Export/DiagnosticsOutput.cs` — единая маршрутизация отладочного вывода:

```
RF_Release/
├── Sette/Sette.glb                     ← только результат
├── _diagnostics/Sette/*.json           ← все 23 debug-файла + копия Spt
├── _logs/Sette_export_20260905_210623.log   ← tee консоли по каждой карте
└── _reports/texture_report_Sette.json
```

Точки записи переведены в `GltfExporter` (7 файлов + SPT-лог), `Program.cs`
(4 BSP-отчёта + donor-диагностика Sette + дефолтный путь `--bsp-dump`), `TextureDiagnostics`.
Legacy-файлы в папках карт не удаляются; `--cleanup-diagnostics` перечисляет их и удаляет
только после интерактивного подтверждения (в неинтерактивном режиме — no-op).

Полный diff: `git show ef84770` (4 файла, +290/−45).

## 4. Часть В

См. `POINT_ELEMENTS_ANALYSIS.md`: классификация из 5 типов (BSP anim objects, SPT-объекты,
SPT-партиклы, RVP, прочее), карта «парсится/экспортируется», структура `AnchoredElement`,
оценка ~5–7 дней, рекомендуемый порядок работ.

## 5. Результаты тестов

| Тест | До фикса | После |
|---|---|---|
| Sette: mg89-92 позиции | raw local (0..3500, у origin) | object-9 transform (кластер у центра карты) |
| Sette: текстуры | 81/81 встроено | без изменений (code=1: debug-куб без текстуры — норма) |
| Cauldron01 (Volcanic Cauldron) | — | ✅ 178/178 текстур, code=1 |
| Полный батч | 41/41 карт OK (Arena117/Wounded_Land удалены из MSH/Map между прогонами внешне; ранее 43/43) | ✅ |
| Dungeon00 | code=2 — известное ограничение BSP-парсера (сжатые вершины формата 2004 г.), не затронут | без изменений |
| Структура RF_Release | JSON рядом с GLB | ✅ только .glb в папке карты; диагностика/логи/отчёты по своим папкам |

Коммиты: `9753851` (build fix), `0fcbda5` (texture diagnostics), `5b33f33` (mg89-92 fix),
`ef84770` (diagnostics refactor), далее — этот отчёт и документ Части В.
