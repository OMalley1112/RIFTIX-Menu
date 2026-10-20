using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace StupidTemplate.Classes.Console;

public static class AdminStore
{
    private static readonly Dictionary<string, string> RuntimeAdmins = [];
    private static bool loaded;

    private static string FilePath => Path.Combine(Paths.ConfigPath, "riftix.admins.txt");

    public static int Count
    {
        get
        {
            EnsureLoaded();
            return RuntimeAdmins.Count;
        }
    }

    public static IEnumerable<string> Names
    {
        get
        {
            EnsureLoaded();
            return RuntimeAdmins.Values;
        }
    }

    public static void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;

        try
        {
            if (!File.Exists(FilePath))
                return;

            foreach (string line in File.ReadAllLines(FilePath))
            {
                int separator = line.IndexOf('|');

                if (separator <= 0 || separator >= line.Length - 1)
                    continue;

                string id   = line.Substring(0, separator).Trim();
                string name = line.Substring(separator + 1).Trim();

                if (id.Length != 0 && name.Length != 0)
                    RuntimeAdmins[id] = name;
            }
        }
        catch { }
    }

    public static void MergeInto()
    {
        EnsureLoaded();

        foreach (KeyValuePair<string, string> admin in RuntimeAdmins)
            HamburburData.Admins[admin.Key] = admin.Value;
    }

    public static bool Add(string userId, string name)
    {
        EnsureLoaded();

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(name))
            return false;

        RuntimeAdmins[userId] = name;
        HamburburData.Admins[userId] = name;
        Save();
        return true;
    }

    public static bool Remove(string userId)
    {
        EnsureLoaded();

        bool removed = RuntimeAdmins.Remove(userId);
        removed      = HamburburData.Admins.Remove(userId) || removed;

        if (removed)
            Save();

        return removed;
    }

    public static bool Contains(string userId)
    {
        EnsureLoaded();
        return RuntimeAdmins.ContainsKey(userId) || HamburburData.Admins.ContainsKey(userId);
    }

    private static void Save()
    {
        try
        {
            string[] lines = new string[RuntimeAdmins.Count];
            int index = 0;

            foreach (KeyValuePair<string, string> admin in RuntimeAdmins)
                lines[index++] = admin.Key + "|" + admin.Value.Replace('|', '/');

            File.WriteAllLines(FilePath, lines);
        }
        catch { }
    }
}
