using System.Text.Json;
using Microsoft.Win32;

namespace NovaNet.Services;

public sealed class RegEntry
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Existed { get; set; }
    public string Kind { get; set; } = "";
    public string Data { get; set; } = "";
}

public sealed class BackupData
{
    public Dictionary<string, List<RegEntry>> Reg { get; set; } = new();
    public Dictionary<string, List<string>> Extra { get; set; } = new();
    public HashSet<string> Applied { get; set; } = new();
}

/// <summary>Everything NovaNet changes is recorded here first, so "Undo" restores your exact original values.</summary>
public static class Store
{
    static readonly string Dir = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NovaNet");
    static readonly string FilePath = System.IO.Path.Combine(Dir, "backup.json");
    public static BackupData Data { get; private set; } = Load();

    static BackupData Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<BackupData>(File.ReadAllText(FilePath)) ?? new BackupData();
        }
        catch { }
        return new BackupData();
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

public static class Reg
{
    static RegistryKey Root(string hive) => hive switch
    {
        "HKLM" => Registry.LocalMachine,
        "HKCU" => Registry.CurrentUser,
        _ => throw new ArgumentException("Unknown hive " + hive)
    };

    static (RegistryKey Root, string Sub) Split(string path)
    {
        int i = path.IndexOf('\\');
        return (Root(path[..i]), path[(i + 1)..]);
    }

    static void Record(string id, string path, string name)
    {
        if (!Store.Data.Reg.TryGetValue(id, out var list))
        {
            list = new List<RegEntry>();
            Store.Data.Reg[id] = list;
        }
        if (list.Any(e => e.Path == path && e.Name == name)) return; // keep the very first original

        var (root, sub) = Split(path);
        using var k = root.OpenSubKey(sub);
        var old = k?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        list.Add(new RegEntry
        {
            Path = path,
            Name = name,
            Existed = old != null,
            Kind = old == null ? "" : k!.GetValueKind(name).ToString(),
            Data = old?.ToString() ?? ""
        });
        Store.Save();
    }

    public static void SetDword(string id, string path, string name, int value)
    {
        Record(id, path, name);
        var (root, sub) = Split(path);
        using var k = root.CreateSubKey(sub, true)!;
        k.SetValue(name, value, RegistryValueKind.DWord);
    }

    public static void Restore(string id)
    {
        if (!Store.Data.Reg.TryGetValue(id, out var list)) return;
        foreach (var e in list)
        {
            try
            {
                var (root, sub) = Split(e.Path);
                using var k = root.CreateSubKey(sub, true)!;
                if (!e.Existed) k.DeleteValue(e.Name, false);
                else if (e.Kind == "DWord") k.SetValue(e.Name, int.Parse(e.Data), RegistryValueKind.DWord);
                else k.SetValue(e.Name, e.Data, RegistryValueKind.String);
            }
            catch { }
        }
        Store.Data.Reg.Remove(id);
        Store.Save();
    }
}
