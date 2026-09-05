# Часть В: Отдельные элементы на картах — классификация и план (анализ, без кода)

Дата: 2026-09-05; обновлено 2026-09-06 (EBP/RVP экспортируются, добавлен формат `.cam`).
Основано на актуальном коде парсеров (`Parsing/`) и экспортёра (`Export/`).

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
  `RvpPrepareBinding`: ObjectName + DummyName) + реверснутый `.cam`
  (треки dummy-якорей, формат — в разделе 5).
- **Статус парсинга:** ✅. В `.rvp` без секции `[Track]` строки вида `ani *b_01 0`,
  идущие после `[PrepareTrack]`, биндинги не затирают (второй токен биндинга обязан
  быть dummy-именем вида `*DummyNN`); служебные блоки `*magic N`
  (`magic_id/char_link/char_dummy/map_dummy`) объектами не считаются.
- **Статус экспорта:** ✅ (с 2026-09-06) — ноды `RVP_<объект>` с мешем `.msh`
  (через `SptModelBridge`, резолв от корня данных клиента) и pos/rot-каналами
  из треков `.cam` (кадр/30 = сек, зеркалирование `MirrorSptMatrix`).
  Привязка объект→якорь — по `[PrepareTrack]` (`*b_01 *Dummy01`).
  10 карт с объектами, 219 нод; no-dummy = 0 везде. Нечитаемые `.msh`
  (bone-меши, «not enough triangles») → fallback-маркер `RVP_<объект>_box`.
  Диагностика: `_diagnostics/<map>/rvp_objects.json`.

### Тип 5. Прочее распарсенное, но не интегрированное
- `.ebp` (ExtBspFile): ✅ экспортируется (с 2026-09-06) — FX/SND-эмиттеры
  (фонтаны, лава, листопад, ambient-звуки) маркер-нодами `FX_<i>_<имя>` /
  `SND_<i>_<имя>` с метаданными в `extras` (effect path, fade, shader, wave…).
  Картовых `.R3E` в данных нет — сами системы частиц не экспортируются.
  Диагностика: `_diagnostics/<map>/ebp_fx.json`.
- `.r3x` (туман, lens flare, env entity) — читается, не интегрирован.
- Entity RPK — только отчёты.

## 2. Что уже парсится vs что нет

| Элемент | Парсинг | Экспорт в GLB |
|---|---|---|
| BSP anim objects (треки, parent) | ✅ | ✅ (все карты, иерархия нод) |
| SPT-объекты (helper'ы: bbox+TRS+node_tm) | ✅ | ✅ маркер-боксы реального размера / real-if-supported |
| SPT-партиклы | ✅ | ❌ |
| RVP objects/tracks/bindings | ✅ | ✅ ноды `RVP_*` с мешами и анимацией из `.cam` |
| EBP (FX/SND-эмиттеры) | ✅ | ✅ маркер-ноды `FX_*`/`SND_*` с extras |
| R3X / RPK | ✅ / отчёты | ❌ |

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
| RVP dummy-привязки (RvpPrepareBinding) в сцену | ✅ сделано | реверс `.cam`, `Parsing/Rvp/CamFile.cs` |
| Итого | ~5–7 дней | |

## 5. Формат `.cam` (RVP-якоря, реверс по 13+ картам)

Бинарный, little-endian. Парсер: `Parsing/Rvp/CamFile.cs`.

```
header:  f32 version (1.2), u32 ?, u32 ?, u32 totalFrame
```

Далее блоки двух видов; границы блоков ищутся по ASCII-именам regex
`(camera|Dummy)\d+\x00` с выравниванием `off % 4 == 0` (родительские ссылки
отфильтровываются: они ровно через 64 байта после старта блока).

- **cameraNN** — переменная длина, для расстановки объектов не нужны, пропускаются.
- **DummyNN**:

```
char  name[64]      (ASCII, нуль-паддинг)
char  parent[64]
f32   baseMatrix[16]  (4x4, обычно ~identity)
f32   basePos[3]
f32   baseQuat[4]     (x,y,z,w)
u32   numPos
u32   numRot
u32   reserved (=0)
posKeys: numPos × { f32 x,y,z, frame }        — 16 байт
rotKeys: numRot × { f32 qx,qy,qz,qw, frame }  — 20 байт
```

Особенности:

- Длина блока = `232 + 16·numPos + 20·numRot − 4`: последний ключ последнего
  массива усечён на 4 байта (без frame). У пустых статичных якорей
  (numPos=numRot=0) блок = 228 байт (нет ещё и reserved).
- В конце pos/rot-массивов часто лежит **ключ-терминатор**: дублирует финальный
  трансформ, но с `frame=0`. Такие немонотонные хвостовые ключи отбрасываются —
  иначе в GLB-канале объект телепортируется в конечную позу на t=0.
- У части карт после ключей идут дополнительные неизвестные данные
  (напр. Dungeon04 Dummy07: +1448 байт) — пропускаются по границе следующего
  блока, ключи читаются (warning «block end mismatch»).
- Статичные якоря (пустые блоки 228 байт) экспортируются с basePos/baseQuat
  без каналов анимации.

Приоритет: сначала обобщение BSP-анимаций + иерархия (снимает класс проблем «слетевших»
объектов на других картах), затем резолвер моделей SPT (убирает debug-кубы).
