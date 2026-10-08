using System.Text.Json;
using RAM.Modern.Models;
namespace RAM.Modern.Services;
public static class ProfileStore
{
 public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RAM.Modern");
 public static string Pathname => Path.Combine(Folder,"profiles.json");
 private static readonly JsonSerializerOptions Opt = new() { WriteIndented=true, PropertyNameCaseInsensitive=true };
 public static List<AccountProfile> Load()
 {
  try { if (!File.Exists(Pathname)) return new(); return JsonSerializer.Deserialize<List<AccountProfile>>(File.ReadAllText(Pathname),Opt) ?? new(); }
  catch (Exception e) { throw new IOException("Cannot read profiles. File preserved at " + Pathname,e); }
 }
 public static void Save(IEnumerable<AccountProfile> items)
 {
  Directory.CreateDirectory(Folder);
  string tmp = Pathname + ".tmp";
  File.WriteAllText(tmp,JsonSerializer.Serialize(items,Opt));
  if(File.Exists(Pathname)) { File.Copy(Pathname,Pathname+".bak",true); File.Move(tmp,Pathname,true); }
  else File.Move(tmp,Pathname);
 }
}
