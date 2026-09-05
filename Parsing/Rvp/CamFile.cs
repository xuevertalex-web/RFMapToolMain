using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace RFMapToolSharp.Rvp;

/// <summary>
/// Парсер .cam (RF Online, версия 1.2): анимационные треки dummy-нод
/// для RVP-катсцен/ambient-объектов (летающие корабли, бабочки и т.п.).
///
/// Формат (выведен реверсом по 13 картам):
///   header:  f32 version(1.2), u32 ?, u32 ?, u32 totalFrame
///   блоки:   cameraNN — пропускаем (переменная длина, не нужны для расстановки)
///            DummyNN:
///              char  name[64]   (ASCII, нуль-паддинг)
///              char  parent[64]
///              f32   baseMatrix[16]  (4x4, обычно ~identity)
///              f32   basePos[3]
///              f32   baseQuat[4]     (x,y,z,w)
///              u32   numPosKeys
///              u32   numRotKeys
///              u32   reserved (=0)
///              posKeys: numPos × { f32 x,y,z, frame }   (16 байт)
///              rotKeys: numRot × { f32 qx,qy,qz,qw, frame } (20 байт)
///            Последний ключ последнего массива может быть усечён на 4 байта
///            (без frame) — формально длина блока = 232 + 16·numPos + 20·numRot − 4.
///            У части карт после ключей идут дополнительные неизвестные данные —
///            они пропускаются по границе следующего блока.
/// </summary>
public sealed class CamFile
{
    public float Version { get; private set; }
    public uint TotalFrame { get; private set; }
    public List<CamDummy> Dummies { get; } = new();
    public List<string> Warnings { get; } = new();

    public sealed class CamDummy
    {
        public string Name { get; set; } = string.Empty;
        public string Parent { get; set; } = string.Empty;
        public Vector3 BasePos { get; set; }
        public Quaternion BaseQuat { get; set; }
        public List<KeyValuePair<float, Vector3>> PosKeys { get; } = new();   // frame -> pos
        public List<KeyValuePair<float, Quaternion>> RotKeys { get; } = new(); // frame -> quat
    }

    private static readonly Regex NameRegex = new(@"(?:camera|Dummy)\d+\x00", RegexOptions.Compiled);
    private static readonly Regex NameCheck = new(@"^(camera|Dummy)\d+$", RegexOptions.Compiled);

    public static CamFile Load(string path)
    {
        var data = File.ReadAllBytes(path);
        var file = new CamFile();
        if (data.Length < 16) { file.Warnings.Add("file too small"); return file; }

        var br = new BinaryReader(new MemoryStream(data));
        file.Version = br.ReadSingle();
        br.ReadUInt32();
        br.ReadUInt32();
        file.TotalFrame = br.ReadUInt32();

        // Кандидаты на начало блока: ASCII-имя вида cameraNN / DummyNN с нулём.
        var text = Encoding.ASCII.GetString(data);
        var candidates = new List<int>();
        foreach (Match m in NameRegex.Matches(text))
        {
            int off = m.Index;
            if (off % 4 != 0) continue;
            var name = ReadName(data, off);
            if (!NameCheck.IsMatch(name)) continue;
            candidates.Add(off);
        }
        candidates.Sort();
        // Родительские ссылки — ровно через 64 байта после старта блока.
        var set = new HashSet<int>(candidates);
        var starts = new List<int>();
        foreach (var c in candidates)
            if (!set.Contains(c - 64)) starts.Add(c);
        starts.Add(data.Length);

        for (int i = 0; i < starts.Count - 1; i++)
        {
            int s = starts[i], e = starts[i + 1];
            var name = ReadName(data, s);
            if (name.Length == 0) continue;
            if (name.StartsWith("camera", StringComparison.OrdinalIgnoreCase))
                continue; // камеры нам не нужны
            // 228 байт — легитимный пустой блок (numPos=numRot=0, без reserved):
            // статичный якорь только с basePos/baseQuat.
            if (e - s < 228) { file.Warnings.Add($"{name}: block too small ({e - s})"); continue; }

            uint numPos = BitConverter.ToUInt32(data, s + 220);
            uint numRot = BitConverter.ToUInt32(data, s + 224);
            long posBytes = (long)numPos * 16;
            long rotBytes = (long)numRot * 20;
            // −4: последний ключ последнего массива может быть усечён (без frame).
            if (numPos > 100000 || numRot > 100000 || s + 232 + posBytes + rotBytes - 4 > data.Length)
            {
                // Ложное срабатывание внутри camera-блока — пропускаем.
                file.Warnings.Add($"{name}: insane counts (pos={numPos}, rot={numRot}) — skipped");
                continue;
            }

            var dummy = new CamDummy
            {
                Name = name,
                Parent = ReadName(data, s + 64),
                BasePos = new Vector3(
                    BitConverter.ToSingle(data, s + 192),
                    BitConverter.ToSingle(data, s + 196),
                    BitConverter.ToSingle(data, s + 200)),
                BaseQuat = NormalizeQuat(
                    BitConverter.ToSingle(data, s + 204),
                    BitConverter.ToSingle(data, s + 208),
                    BitConverter.ToSingle(data, s + 212),
                    BitConverter.ToSingle(data, s + 216))
            };

            int p = s + 232;
            for (int k = 0; k < numPos; k++)
            {
                // Блок может быть оборван концом файла; последний ключ может
                // быть усечён на 4 байта (без frame) — тогда читаем только xyz.
                if (p + 12 > data.Length) break;
                float x = BitConverter.ToSingle(data, p);
                float y = BitConverter.ToSingle(data, p + 4);
                float z = BitConverter.ToSingle(data, p + 8);
                float fr = p + 16 <= data.Length
                    ? BitConverter.ToSingle(data, p + 12)
                    : (dummy.PosKeys.Count > 0 ? dummy.PosKeys[^1].Key : 0f);
                if (IsFinite(x) && IsFinite(y) && IsFinite(z) && IsFinite(fr))
                    dummy.PosKeys.Add(new KeyValuePair<float, Vector3>(fr, new Vector3(x, y, z)));
                p += 16;
            }
            for (int k = 0; k < numRot; k++)
            {
                if (p + 16 > data.Length) break;
                // последний ключ может быть усечён (без frame)
                bool truncated = p + 20 > s + 232 + posBytes + rotBytes || p + 20 > e || p + 20 > data.Length;
                float qx = BitConverter.ToSingle(data, p);
                float qy = BitConverter.ToSingle(data, p + 4);
                float qz = BitConverter.ToSingle(data, p + 8);
                float qw = BitConverter.ToSingle(data, p + 12);
                float fr = truncated ? (dummy.RotKeys.Count > 0 ? dummy.RotKeys[^1].Key : 0f)
                                     : BitConverter.ToSingle(data, p + 16);
                var q = NormalizeQuat(qx, qy, qz, qw);
                if (IsFinite(q.X) && IsFinite(q.Y) && IsFinite(q.Z) && IsFinite(q.W))
                    dummy.RotKeys.Add(new KeyValuePair<float, Quaternion>(fr, q));
                p += 20;
            }

            // В конце pos/rot-массивов часто лежит ключ-терминатор: дублирует
            // финальный трансформ, но с frame=0 (кадры идут монотонно возрастая,
            // а последний внезапно 0). В анимационном канале он даёт скачок
            // в момент t=0 к конечной позе — отбрасываем такие ключи.
            DropSentinelKeys(dummy.PosKeys);
            DropSentinelKeys(dummy.RotKeys);

            // −4 универсально: либо усечён последний ключ, либо (в пустых
            // 228-байтных блоках) отсутствует поле reserved.
            long expected = s + 232 + posBytes + rotBytes - 4;
            if (expected != e)
                file.Warnings.Add($"{name}: block end mismatch (expected {expected - s}, actual {e - s}) — keys kept, tail skipped");

            file.Dummies.Add(dummy);
        }

        return file;
    }

    private static void DropSentinelKeys<T>(List<KeyValuePair<float, T>> keys)
    {
        while (keys.Count >= 2 && keys[^1].Key < keys[^2].Key)
            keys.RemoveAt(keys.Count - 1);
    }

    private static string ReadName(byte[] data, int off)
    {
        if (off < 0 || off + 64 > data.Length) return string.Empty;
        int len = 0;
        while (len < 64 && data[off + len] != 0) len++;
        return Encoding.ASCII.GetString(data, off, len);
    }

    private static Quaternion NormalizeQuat(float x, float y, float z, float w)
    {
        float len = MathF.Sqrt(x * x + y * y + z * z + w * w);
        if (len < 1e-6f || !IsFinite(len)) return Quaternion.Identity;
        return new Quaternion(x / len, y / len, z / len, w / len);
    }

    private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
}
