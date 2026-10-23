using System.Collections.Generic;

namespace StupidTemplate.Classes.Console;

public class LocalAdmins
{
    // This menu template comes with an implementation of 'Console', the popular Gorilla Tag admin system.
    // You may remove console if you wish, or use it in your own menu.
    // You have to make admins mods yourself, but the admin system is here for you to use.
    // If you want to make yourself an admin on this menu you can add your player id and any name of your you want below.
    // ORDER: player id FIRST, name SECOND — { "PLAYERID", "NAME" }

    public static Dictionary<string, string> Admins = new()
    {
            { "9ED9C592A6C8D4B2", "OMALLEYGT" },
    };

    // Super admins have a higher level of power and have a different icon.
    // It's recommended you give yourself super admin
    // All you need to do is enter the *same* username as the one you put in the Admins Dictionary above
    public static string[] SuperAdmins =
    [
            "OMALLEYGT",
    ];

    // Copies local admins into the live admin dictionary, accepting either
    // { id, name } or a reversed { name, id } entry.
    public static void ApplyTo(Dictionary<string, string> admins)
    {
        foreach (KeyValuePair<string, string> entry in Admins)
        {
            if (LooksLikePlayerId(entry.Key))
                admins[entry.Key] = entry.Value;
            else if (LooksLikePlayerId(entry.Value))
                admins[entry.Value] = entry.Key;
            else
                admins[entry.Key] = entry.Value;
        }
    }

    private static bool LooksLikePlayerId(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 16)
            return false;

        foreach (char character in value)
            if (!System.Uri.IsHexDigit(character))
                return false;

        return true;
    }
}
