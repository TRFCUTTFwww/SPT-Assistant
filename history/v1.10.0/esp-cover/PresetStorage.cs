using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace LocalSpt5Esp;
internal static class PresetStorage {
 sealed class Document<T>{public Document(){}public int Version{get;set;}=1;public List<T> Items{get;set;}=new();}
 static readonly JsonSerializerOptions options=new(){WriteIndented=true,PropertyNameCaseInsensitive=true};
 internal static List<T> Load<T>(string path){if(!File.Exists(path))return new();var document=JsonSerializer.Deserialize<Document<T>>(File.ReadAllText(path),options)??throw new InvalidDataException("标签文件为空。");if(document.Version!=1)throw new InvalidDataException("不支持的标签文件版本。");return document.Items??throw new InvalidDataException("标签列表无效。");}
 internal static void Save<T>(string path,List<T> items){string content=JsonSerializer.Serialize(new Document<T>{Items=items},options);Directory.CreateDirectory(Path.GetDirectoryName(path)!);string temporary=path+".tmp";File.WriteAllText(temporary,content);if(File.Exists(path))File.Copy(path,path+".bak",true);File.Move(temporary,path,true);}
}
