using System;
using System.Diagnostics;
using StupidTemplate.Notifications;

namespace StupidTemplate.Mods;

public abstract class Science
{
    private static readonly string[] ProjectPaths =
    {
            @"C:\Users\Charl\Downloads\virus-science-project\index.html",
            @"C:\Users\Charl\Desktop\virus-science-project\index.html",
    };

    private static readonly string[] Facts =
    {
            "Viruses are NOT alive - they cannot eat, grow, or reproduce on their own!",
            "Thousands of viruses could fit on the head of a pin.",
            "Soap tears apart a virus's fat jacket - wash your hands for 20 seconds!",
            "Antibiotics fight bacteria, NOT viruses.",
            "Most viruses are harmless - the common cold is one of them.",
            "Bacteriophages look like tiny lunar landers and hunt bacteria.",
            "The first vaccine was made in 1796 to stop smallpox.",
            "Plant viruses like tobacco mosaic cannot infect people at all.",
            "A virus needs a living cell to copy itself - like a recipe needs a kitchen.",
            "Memory cells remember a vaccine for years, so your body responds in hours.",
    };

    private static int factIndex;

    public static void OpenProject()
    {
        try
        {
            foreach (string path in ProjectPaths)
            {
                if (System.IO.File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo(path)
                    {
                            UseShellExecute = true,
                    });

                    NotifiLib.SendNotification("Opening your virus science project!");
                    return;
                }
            }

            NotifiLib.SendNotification("Virus project not found on this PC");
        }
        catch (Exception e)
        {
            NotifiLib.SendNotification("Could not open project: " + e.Message);
        }
    }

    public static void NextFact() =>
            NotifiLib.SendNotification("Virus Fact: " + Facts[factIndex++ % Facts.Length]);
}
