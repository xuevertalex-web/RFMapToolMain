using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RFMapToolSharp.Editor
{
    public static class EditorGenerator
    {
        public static EditorTemplate LoadTemplate(string path)
        {
            var json = File.ReadAllText(path);
            var t = JsonSerializer.Deserialize<EditorTemplate>(json);
            if (t == null) throw new InvalidOperationException("Template parse failed.");
            if (t.ModelPool == null || t.ModelPool.Count == 0) throw new InvalidOperationException("ModelPool is empty.");
            return t;
        }

        public static List<GeneratedObject> Generate(EditorTemplate t)
        {
            var rnd = new Random(t.Seed);
            var result = new List<GeneratedObject>(t.ObjectCount);

            for (int i = 0; i < t.ObjectCount; i++)
            {
                var model = t.ModelPool[rnd.Next(t.ModelPool.Count)];
                result.Add(new GeneratedObject
                {
                    ModelName = model,
                    X = Lerp(t.MinX, t.MaxX, (float)rnd.NextDouble()),
                    Y = Lerp(t.MinY, t.MaxY, (float)rnd.NextDouble()),
                    Z = Lerp(t.MinZ, t.MaxZ, (float)rnd.NextDouble()),
                    RotY = (float)rnd.NextDouble() * 360f,
                    Scale = 0.8f + (float)rnd.NextDouble() * 0.4f
                });
            }

            return result;
        }

        public static void SavePlan(string outputPath, EditorTemplate t, List<GeneratedObject> objects)
        {
            var dto = new
            {
                Template = t.Name,
                t.Seed,
                Count = objects.Count,
                Objects = objects
            };
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(outputPath, json);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}

