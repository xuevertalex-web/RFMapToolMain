using System;
using System.Collections.Generic;
using System.IO;
using Pfim;
using RFMapToolSharp.Collision;
using RFMapToolSharp.Materials;
using RFMapToolSharp.Models;

namespace RFMapToolSharp.Parsing.Bsp;

/// <summary>
/// Пересборка UV с учётом флагов R3M (_UV_SCALE/_ROTATE/_SCROLL/_LAVA/_METAL/gradients),
/// масштаба матгруппы (BSP Scale) и detailScale. Поддерживает опциональное время анимации.
/// </summary>
public static class UvReconstruction
{
    private record TexInfo(int Width, int Height);

    private readonly record struct MatGroupInfo(
        float GroupScale,
        float DetailScale,
        bool UseScale, float Scale,
        bool UseRotate, float Rotate,
        bool UseScrollU, float ScrollU,
        bool UseScrollV, float ScrollV,
        bool UseLava, float LavaWave, float LavaSpeed,
        bool UseMetal, float MetalFactor,
        bool UseGradU, bool UseGradV);

    public static void Apply(MapScene scene, float animationTimeSeconds = 0f)
    {
        if (scene.Bsp == null || scene.Bsp.UV.Count == 0 || scene.Bsp.VertexRefs.Count == 0)
            return;

        var matInfos = BuildMatGroupInfos(scene);
        var refs = scene.Bsp.VertexRefs;

        if (scene.Bsp.Vertices.Count != refs.Count)
            return;

        var rebuilt = new Vector2f[refs.Count];

        for (int i = 0; i < refs.Count; i++)
        {
            var reference = refs[i];
            if (reference.MatGroupIndex < 0 || reference.MatGroupIndex >= matInfos.Count)
                continue;

            var info = matInfos[reference.MatGroupIndex];
            int uvIndex = reference.UvIndex;
            if (uvIndex < 0 || uvIndex >= scene.Bsp.UV.Count)
                continue;

            float u = scene.Bsp.UV[uvIndex].X;
            float v = scene.Bsp.UV[uvIndex].Y;

            // Масштаб матгруппы (Scale в BSP)
            if (info.GroupScale != 0f && info.GroupScale != 1f)
            {
                u *= info.GroupScale;
                v *= info.GroupScale;
            }

            // Detail map scale (из R3M)
            if (info.DetailScale != 0f && info.DetailScale != 1f)
            {
                u *= info.DetailScale;
                v *= info.DetailScale;
            }

            // _UV_SCALE
            if (info.UseScale && info.Scale != 0f && info.Scale != 1f)
            {
                u *= info.Scale;
                v *= info.Scale;
            }

            // _UV_ROTATE вокруг (0,0)
            if (info.UseRotate && info.Rotate != 0f)
            {
                float cos = MathF.Cos(info.Rotate);
                float sin = MathF.Sin(info.Rotate);
                float ru = u * cos - v * sin;
                float rv = u * sin + v * cos;
                u = ru;
                v = rv;
            }

            // _UV_SCROLL_U/V с учётом времени
            if (info.UseScrollU) u += info.ScrollU * animationTimeSeconds;
            if (info.UseScrollV) v += info.ScrollV * animationTimeSeconds;

            // Лава/металл — волновое смещение (snapshot по заданному времени)
            if (info.UseLava && info.LavaWave != 0f)
            {
                float phase = animationTimeSeconds * info.LavaSpeed;
                u += MathF.Sin(phase) * info.LavaWave;
                v += MathF.Cos(phase) * info.LavaWave;
            }
            if (info.UseMetal && info.MetalFactor != 0f)
            {
                float phase = animationTimeSeconds;
                u += MathF.Sin(phase) * info.MetalFactor;
                v += MathF.Cos(phase) * info.MetalFactor;
            }

            rebuilt[i] = new Vector2f { X = u, Y = v };
        }

        scene.Bsp.OverrideUv(rebuilt);
    }

    private static List<MatGroupInfo> BuildMatGroupInfos(MapScene scene)
    {
        var infos = new List<MatGroupInfo>(scene.Bsp!.MatGroups.Count);
        for (int i = 0; i < scene.Bsp.MatGroups.Count; i++)
        {
            var mg = scene.Bsp.MatGroups[i];
            var mat = GetMaterial(scene.MaterialFile, mg.MtlId);

            float detailScale = mat?.DetailScale ?? 1f;
            float groupScale = (mg.Scale != 0f) ? mg.Scale : 1f;
            bool useScale = false, useRotate = false, useScrollU = false, useScrollV = false;
            float scale = 1f, rotate = 0f, scrollU = 0f, scrollV = 0f;

            bool useLava = false, useMetal = false, useGradU = false, useGradV = false;
            float lavaWave = 0f, lavaSpeed = 0f, metalFactor = 0f;

            if (mat != null && mat.Layers.Count > 0)
            {
                var layer = mat.Layers[0];
                const uint UV_ENV = 0x00000001;
                const uint UV_LAVA = 0x00000004;
                const uint UV_METAL_FLOOR = 0x00000002;
                const uint UV_METAL_WALL = 0x00000008;
                const uint UV_METAL = 0x0000000a;
                const uint UV_SCROLL_U = 0x00000010;
                const uint UV_SCROLL_V = 0x00000020;
                const uint UV_ROTATE = 0x00000040;
                const uint UV_SCALE = 0x00000080;
                const uint UV_GRADIENT_ALPHA_U = 0x00000100;
                const uint UV_GRADIENT_ALPHA_V = 0x00000200;

                if ((layer.Flag & UV_SCALE) != 0)
                {
                    scale = FixedShortToFloat(layer.UvScaleStart);
                    useScale = true;
                }

                if ((layer.Flag & UV_ROTATE) != 0)
                {
                    rotate = FixedShortToFloat(layer.UvRotate);
                    useRotate = true;
                }

                if ((layer.Flag & UV_SCROLL_U) != 0)
                {
                    scrollU = FixedShortToFloat(layer.UvScrollU);
                    useScrollU = true;
                }

                if ((layer.Flag & UV_SCROLL_V) != 0)
                {
                    scrollV = FixedShortToFloat(layer.UvScrollV);
                    useScrollV = true;
                }

                if ((layer.Flag & UV_LAVA) != 0)
                {
                    lavaWave = FixedShortToFloat(layer.UvLavaWave);
                    lavaSpeed = FixedShortToFloat(layer.UvLavaSpeed);
                    useLava = true;
                }

                if ((layer.Flag & (UV_METAL | UV_METAL_FLOOR | UV_METAL_WALL)) != 0)
                {
                    metalFactor = FixedShortToFloat(layer.UvMetal);
                    useMetal = true;
                }

                if ((layer.Flag & UV_GRADIENT_ALPHA_U) != 0) useGradU = true;
                if ((layer.Flag & UV_GRADIENT_ALPHA_V) != 0) useGradV = true;
                // UV_ENV пока не используем (env mapping).
            }

            infos.Add(new MatGroupInfo(
                groupScale, detailScale,
                useScale, scale,
                useRotate, rotate,
                useScrollU, scrollU,
                useScrollV, scrollV,
                useLava, lavaWave, lavaSpeed,
                useMetal, metalFactor,
                useGradU, useGradV));
        }

        return infos;
    }

    private static R3MMaterial? GetMaterial(R3MMaterialFile? file, int matId)
    {
        if (file == null) return null;
        if (matId < 0 || matId >= file.Materials.Count) return null;
        return file.Materials[matId];
    }

    private static float FixedShortToFloat(short value) => value / 256f;
}
