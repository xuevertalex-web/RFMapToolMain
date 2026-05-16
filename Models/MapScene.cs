using System.Collections.Generic;
using RFMapToolSharp.Collision;
using RFMapToolSharp.Materials;
using RFMapToolSharp.Rvp;
using RFMapToolSharp.Spt;
using RFMapToolSharp.Textures;

namespace RFMapToolSharp.Models;

/// <summary>
/// Высокоуровневая модель карты: геометрия, материалы, коллизия, партиклы, кат-сцены.
/// </summary>
public class MapScene
{
    public string Name { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty; // папка карты (map\cora и т.п.)

    // Текстуры R3T
    public List<R3TTextureEntry> Textures { get; set; } = new();

    // Материалы / R3M (+ расширение R3X)
    public R3MMaterialFile? MaterialFile { get; set; }
    public R3XMaterialFile? MaterialExt { get; set; }

    // BSP / EBP
    public BspFile? Bsp { get; set; }
    public ExtBspFile? ExtBsp { get; set; }

    // SPT — партиклы/объекты
    public List<SptParticle> Particles { get; set; } = new();

    // RVP — кат-сцены/треки
    public RvpFile? Movie { get; set; }
}

