using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace RFMapToolSharp.Parsing
{
    public class SptMapObject
    {
        public string ModelName;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale;
    }

    public static class SptMapParser
    {
        public static List<SptMapObject> Parse(string filePath)
        {
            var list = new List<SptMapObject>();
            if (!File.Exists(filePath)) return list;

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                {
                    if (fs.Length < 4) return list;

                    int count = br.ReadInt32();
                    // Защита от заголовка версии
                    if (count > 100000 || count < 0) count = br.ReadInt32();

                    for (int i = 0; i < count; i++)
                    {
                        var obj = new SptMapObject();
                        // 1. Имя (100 байт)
                        obj.ModelName = CleanString(br.ReadBytes(100));
                        // 2. Позиция
                        obj.Position = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        // 3. Вращение
                        obj.Rotation = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        // 4. Масштаб
                        obj.Scale = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

                        if (!string.IsNullOrEmpty(obj.ModelName)) list.Add(obj);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SPT Error] {Path.GetFileName(filePath)}: {ex.Message}");
            }
            return list;
        }

        private static string CleanString(byte[] bytes)
        {
            int nullIdx = Array.IndexOf(bytes, (byte)0);
            return nullIdx >= 0 
                ? Encoding.ASCII.GetString(bytes, 0, nullIdx).Trim() 
                : Encoding.ASCII.GetString(bytes).Trim();
        }
    }
}