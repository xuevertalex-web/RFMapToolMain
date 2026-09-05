# Часть В: Отдельные элементы на картах — классификация и план (анализ, без кода)

Дата: 2026-09-05. Основано на актуальном коде парсеров (`Parsing/`) и экспортёра (`Export/`).

## 1. Классификация элементов, привязанных к точкам

### Тип 1. BSP animated objects (анимированные объекты BSP)
- **Источник:** чанк анимированных объектов в `.bsp` (`ReadAniObject`: Flag, Parent, Frames,
  PosCnt/RotCnt/ScaleCnt + оффсеты треков, базовые Pos/Quat/Scale).
- **Связь с геометрией:** `MatGroup.ObjectId` (1-based) указывает на объект; вершины группы
  домножаются на матрицу объекта (`ObjectMatrices`), объекты образуют parent-chain.
- **Статус парсинга:** ✅ полностью (треки pos/rot/scale сэмплируются, `GetObjectAnimationSamples`).
- **Статус экспорта:** ✅ (с 2026-09-05) — GLB-анимации для всех объектов на всех картах
  (иерархия нод `BSP_obj{id}`, world-дельты, shear-факторизация). Снят глобальный skip
  трансформа для `attr=8192`: все object-группы на всех картах имеют этот attr, и skip
  оставлял их геометрию в raw local space («слетевшие» объекты) и глушил анимации.

### Тип 2. SPT-объекты (helper-объёмы: триггеры, телепорты, модули данжей)
- **Источник:** бинарные `.spt` в `Map/<map>/Spt/*.spt` и `*EXT.spt` в корне карты
  (`SptMapParser`: записи ModelName[100] + Position + Rotation + Scale; текстовый fallback
  для EXT-скриптов). Все встреченные в раздаче `.spt` — текстовые `script_begin`
  helper-скрипты: у записей есть локальный bbox и `node_tm`, видимых моделей нет.
- **Статус парсинга:** ✅ (бинарный и текстовый варианты; для текстовых также
  bbox, флаг `-music`/`-id N`, сырая `node_tm`).
- **Статус экспорта:** ✅ — полупрозрачные маркер-боксы реального размера (bbox),
  полный `node_tm` (позиция+поворот+масштаб), цвет по классу (helper/music/portal/spawn).
  Режим `real-if-supported` подхватывает `.msh/.mod/.obj` через `SptModelBridge`,
  но в текущей раздаче карт таких файлов нет — резолвить нечего.

### Тип 3. SPT-партиклы (эффекты)
- **Источник:** блоки `[Particle]` в `.spt` (`SptParticle`: PositionType Box/Sphere/SphereEdge,
  StartPos, Gravity, StartPower, треки Alpha/Color/Scale/ZRot/YRot, Flicker и т.д.).
- **Статус парсинга:** ✅ полный снимок полей (`SptFile.Particles`).
- **Статус экспорта:** ❌ не экспортируются. В текущей раздаче карт блоков `[Particle]`
  нет вообще (проверено grep по всем `.spt`) — данных для проверки нет.

### Тип 4. RVP-элементы (кат-сцены/треки)
- **Источник:** `.rvp` (`RvpObject`: Name, TexPath, **Bone**, Animations, Collision, Shadow,
  MeshId, Scale; `RvpTrack`: Type = camera/fade_in/magic/ani + Frame + Args;
  `RvpPrepareBinding`: ObjectName + DummyName).
- **Статус парсинга:** ✅.
- **Статус экспорта:** ❌ не интегрированы в GLB-сцену.

### Тип 5. Прочее распарсенное, но не интегрированное
- `.ebp` (ExtBspFile), `.r3x` (туман, lens flare, env entity), Entity RPK (только отчёты).

## 2. Что уже парсится vs что нет

| Элемент | Парсинг | Экспорт в GLB |
|---|---|---|
| BSP anim objects (треки, parent) | ✅ | ✅ (все карты, иерархия нод) |
| SPT-объекты (helper'ы: bbox+TRS+node_tm) | ✅ | ✅ маркер-боксы реального размера / real-if-supported |
| SPT-партиклы | ✅ | ❌ |
| RVP objects/tracks/bindings | ✅ | ❌ |
| EBP / R3X / RPK | ✅ / ✅ / отчёты | ❌ |

## 3. Предлагаемая структура данных для привязки к анимационным точкам

```csharp
public enum AnchorKind { BspObject, SptObject, SptParticle, RvpDummy }

public sealed class AnchoredElement
{
    public AnchorKind Kind;
    public string Name;            // ModelName / ObjectName / имя партикла
    public string SourceFile;      // откуда прочитан
    public int    ObjectId;        // для BspObject (1-based), иначе -1
    public string ParentAnchor;    // Parent (BSP) / DummyName (RVP) / "" 
    public Vector3 Position; public Vector3 Rotation; public Vector3 Scale; // локальный TRS
    public List<AnimTrack> Tracks; // сэмплированные pos/rot/scale (BSP/RVP)
    public string? MeshRef;        // resolved путь к модели (когда появится резолвер)
}
```

Экспорт: каждый элемент → glTF node; parent-chain BSP-объектов → иерархия нод;
треки → glTF animation channels (обобщить существующий Sette-блок на все объекты);
партиклы → ноды-эмиттеры с `extras` (параметры эмиссии) или KHR_-расширение на будущее.

## 4. Оценка объёма работ

| Задача | Оценка | Зависимости |
|---|---|---|
| Обобщить анимации BSP на все объекты (не только Sette 1/2) | ✅ сделано | готовые сэмплеры |
| Иерархия нод по Parent для BSP-объектов | ✅ сделано | аккуратно с MirrorY |
| Резолвер modelName → реальная модель | ⏸ нет данных | в раздаче карт нет файлов моделей; мост `SptModelBridge` готов |
| Экспорт SPT-партиклей как нод-эмиттеров с extras | 1 д | маппинг полей SptParticle |
| RVP dummy-привязки (RvpPrepareBinding) в сцену | 1–2 д | согласование имён с BSP/SPT |
| Итого | ~5–7 дней | |

Приоритет: сначала обобщение BSP-анимаций + иерархия (снимает класс проблем «слетевших»
объектов на других картах), затем резолвер моделей SPT (убирает debug-кубы).
