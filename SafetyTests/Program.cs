using System;using System.IO;using System.Linq;using System.Text;using System.Text.Json;using RFMapToolSharp.Tools;
var root=Path.Combine(Path.GetTempPath(),"rf_inv_off_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{
var d=Path.Combine(root,"m1");Directory.CreateDirectory(d);
// ascii at offset 0, utf16 after padding
File.WriteAllBytes(Path.Combine(d,"m1.bsp"),BuildAsciiUtf16());
File.WriteAllBytes(Path.Combine(d,"m1.r3t"),Encoding.ASCII.GetBytes("m1.dds m1.dds\0"));
File.WriteAllBytes(Path.Combine(d,"m1.dds"),Encoding.ASCII.GetBytes("DDS\0"));
var outDir=Path.Combine(root,"r");var p=RfInventoryTool.Run(d,outDir);var j=JsonDocument.Parse(File.ReadAllText(p));
var ev=j.RootElement.GetProperty("ReferenceEvidence").EnumerateArray().ToArray();Req(ev.Any(x=>x.GetProperty("Encoding").GetString()=="ascii"),"ascii evidence missing");Req(ev.Any(x=>x.GetProperty("Encoding").GetString()=="utf16le"),"utf16 evidence missing");
Req(ev.SequenceEqual(ev.OrderBy(x=>x.GetProperty("SourceFile").GetString()).ThenBy(x=>x.GetProperty("ByteOffset").GetInt32()).ThenBy(x=>x.GetProperty("ExtractedToken").GetString()),JsonElementComparer.Instance),"non deterministic ordering");
var edges=j.RootElement.GetProperty("DependencyGraph").GetProperty("Edges").EnumerateArray().ToArray();Req(edges.Any(e=>e.GetProperty("ReferenceOffsets").GetArrayLength()>1),"duplicate edge offsets not aggregated");
Req(!ev.Any(x=>x.GetProperty("ExtractedToken").GetString()=="bad.zzz"),"discarded ref emitted offset");
Req(j.RootElement.GetProperty("OffsetEvidenceSummary").TryGetProperty("MinOffset",out _),"offset summary missing");
Console.WriteLine("SAFETYTEST PASS");
}finally{try{Directory.Delete(root,true);}catch{}}
static byte[] BuildAsciiUtf16(){using var ms=new MemoryStream();var a=Encoding.ASCII.GetBytes("m1.r3t m1.dds bad.zzz\0");ms.Write(a); if((ms.Length % 2)!=0) ms.WriteByte(0); for(int i=0;i<10;i++)ms.WriteByte(0); if((ms.Length % 2)!=0) ms.WriteByte(0);foreach(var c in "m1.dds"){ms.WriteByte((byte)c);ms.WriteByte(0);}ms.WriteByte(0);ms.WriteByte(0);foreach(var c in "m1.dds"){ms.WriteByte((byte)c);ms.WriteByte(0);}ms.WriteByte(0);ms.WriteByte(0);return ms.ToArray();}
static void Req(bool c,string m){if(!c)throw new Exception(m);}sealed class JsonElementComparer:IEqualityComparer<JsonElement>{public static readonly JsonElementComparer Instance=new();public bool Equals(JsonElement x,JsonElement y)=>x.GetRawText()==y.GetRawText();public int GetHashCode(JsonElement o)=>o.GetRawText().GetHashCode();}



