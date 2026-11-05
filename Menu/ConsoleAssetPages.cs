using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Photon.Pun;
using Photon.Realtime;
using StupidTemplate.Classes;
using StupidTemplate.Notifications;
using UnityEngine;
using static StupidTemplate.Menu.Main;
using GTConsole = StupidTemplate.Classes.Console.Console;
using HamburburData = StupidTemplate.Classes.Console.HamburburData;

namespace StupidTemplate.Menu;

/*
 * Console Assets - the sending half of the Console asset system.
 *
 * Console has always been able to receive an asset: "asset-spawn" arrives over the
 * network, the bundle downloads, and the object appears for everyone in the room.
 * Nothing in this menu ever sent one. These pages send them: a browser over the
 * hosted bundles, the Gemstone TV + sofa, a hand-held video player, a list of
 * everything currently in the room, and a menu scan.
 *
 * Every command goes to ReceiverGroup.All, which Console applies locally as well
 * as remotely, so the spawner sees what the room sees. Receivers drop asset
 * commands from anyone they do not hold as an administrator, so spawning is
 * gated on the same server admin list the receivers check.
 */
public abstract class ConsoleAssetPages
{
    private const string RootCategory    = "Console Assets";
    private const string ObjectsCategory = "Console Objects";
    private const string SpawnedCategory = "Spawned Assets";

    private const string TvBundle     = "consolehamburburassets";
    private const string ScreenBundle = "console.main1";

    // Gemstone, hamburbur and Seralyth all place the TV and sofa at these spots.
    private static readonly Vector3    TvPosition   = new(-57.1f, 5.6f, -37f);
    private static readonly Vector3    SofaPosition = new(-51.8f, 4.2f, -37.4f);
    private static readonly Quaternion TvRotation   = Quaternion.Euler(270f, 0f,   0f);
    private static readonly Quaternion SofaRotation = Quaternion.Euler(270f, 270f, 0f);

    private static int tvAssetId     = -1;
    private static int sofaAssetId   = -1;
    private static int screenAssetId = -1;
    private static int videoIndex;

    private static string openBundle;

    // A bundle started downloading once is shared by every page that wants it.
    private static readonly Dictionary<string, Task> bundleLoads = new();

    // Videos the other menus already serve to the TV's VideoPlayer child.
    private static readonly (string Name, string Url)[] Videos =
    {
            ("Sky - Carti",
             "https://github.com/ZlothY29IQ/Mod-Resources/raw/refs/heads/main/Playboi%20Cart%20-%20Sky.mp4"),
            ("Caramell Dansen",     "https://files.hamburbur.org/caramelldansen.mp4"),
            ("Dancing Monkeys",
             "https://github.com/ZlothY29IQ/Mod-Resources/raw/refs/heads/main/monkeys_dancing.mp4"),
            ("Over - Carti",             "https://files.hamburbur.org/Over-PlayboiCarti.mp4"),
            ("Rendezvous - Don Toliver", "https://files.hamburbur.org/Rendezvous-DonToliver.mp4"),
            ("Did you pray today?",      "https://files.hamburbur.org/didyoupraytoday.mp4"),
            ("Crazy Russian Guy",        "https://files.hamburbur.org/crazyrussianguy.mp4"),
            ("Tom Holland Moment",       "https://files.hamburbur.org/tomhollandmoment.mp4"),
            ("USA Rat",                  "https://files.hamburbur.org/usamenu.mp4"),
            ("Rizz",                     "https://files.hamburbur.org/rizz.mp4"),
            ("Five Nights at Freddy's 2", "https://files.hamburbur.org/FNaF2_UnityReady.mp4"),
            ("GPT Mod Menu",              "https://files.hamburbur.org/gptmodmenu-soupvr.mp4"),
    };

    private static string CurrentVideoUrl => Videos[videoIndex].Url;

    // Bundle names hosted on the Console asset server, as of this build.
    private static readonly string[] Bundles =
    {
            "altasmap1", "altasmap4", "amongus_pink", "amongus_red", "angrybird", "aot",
            "atlas-blue-th", "atlas-light-purple-th", "atlas-purple-th", "baldi", "baldi_angry", "banhammer",
            "basketball", "bckrms", "beam", "blackrsword", "blackstar", "block",
            "bloodstar", "blue", "bluekevinthekube", "boomywoomy", "boryrosesword", "bouncyhammer",
            "brainrotparty", "brite", "broom", "brsword", "btools", "cageyes",
            "caseohf", "ccc", "cgh", "cghnorb", "chair", "chasarena",
            "cherrybomb", "chickenmapthing", "cinemamovie_theater", "city", "clickbaitmenu", "colii",
            "commandblock", "concert", "console.main1", "consolehamburburassets", "cosmetics", "crosser",
            "cube", "dancer", "dbz", "domain", "domains", "donationnuke",
            "dragonclaw", "ducity", "effects", "enderbasketball", "enderhammer", "enderscythe",
            "endertravis", "endportalstar", "errorstar", "events", "fairyisland", "flamething",
            "flasheffects", "fnafchase", "football", "friendmirror", "fullysetcoli", "glassbridge",
            "glitchesmaps", "gm_mcdonalds", "granny", "gth", "gun", "heaven",
            "heavensword", "heavenswordfixed", "hishiba", "hn", "huss", "hut",
            "icesroseword", "iimenu", "iitomb", "industrysravenger", "iphone", "jailcell",
            "jman", "jobapplication", "jukebox", "karambit", "kars", "kittysword",
            "knife", "l115a33", "lacuca", "leviathan", "lonlyisl", "lowtaper",
            "map", "maps", "maps2", "maxwell", "mccsword", "mcsword",
            "medkit", "menuobject", "minitravis", "minos", "mistscythe", "monster2",
            "monsters", "mrbeast_meme", "nameless", "namelesslaser", "netflix", "nextbot",
            "novadomain", "novaindihcatah", "objects", "oldtag", "omega", "overkillcharge",
            "overkillfire", "overkillgun", "patapim", "pigeon", "pizzaman", "portal",
            "portalgun", "portalsword", "powerglove", "purple", "purpleshoot", "ra",
            "raa", "raaa", "raaaa", "randomstuff", "ravenger", "ravyravy",
            "rblxcarpet", "rbsword", "realknife", "red", "reefbackreaper", "rezero",
            "rgbendersword", "roblox", "runningfreddy", "saoplayericon", "sawgun", "scarylarry",
            "shibaholdable", "shotgun1", "shrek", "simplehouse", "sis", "skele",
            "skibidi toilet", "smurfcat", "soggy", "sogsog", "sonic", "sp",
            "spraypaint", "squidgame", "star_win", "starglitcher", "starpe", "starwand",
            "stormblade", "subwaysurfers", "summons", "super-crown", "theboliedone", "titan",
            "towerer", "travis", "travisv2", "tri", "tung-tung-tung-sahur", "veritymonster",
            "viltrumite", "vr", "whiterose", "wii", "wings", "wworld",
            "zeldasword",
    };

    // ---------------------------------------------------------------------
    //  Root page state displays
    // ---------------------------------------------------------------------

    public static string TvDisplay() =>
            $"Gemstone TV: {SwitchWord("Gemstone TV")}";

    public static string VideoPlayerDisplay() =>
            $"Video Player: {SwitchWord("Video Player")}";

    private static string SwitchWord(string buttonText)
    {
        ButtonInfo button = Buttons.GetIndex(buttonText);

        return button != null && button.enabled
                       ? "<color=green>ON</color>"
                       : "<color=red>OFF</color>";
    }

    public static string VideoDisplay() =>
            $"Video: <color=#C79BFF>{Videos[videoIndex].Name}</color>";

    public static string SpawnedDisplay() =>
            $"In-Room Assets <color=grey>[{GTConsole.ConsoleAssets.Count}]</color>";

    // ---------------------------------------------------------------------
    //  Gemstone TV + hand video player
    // ---------------------------------------------------------------------

    public static void EnableTv()
    {
        if (!CanSpawn(out string reason))
        {
            // Saved switch states are restored before a room is joined; flipping
            // back off silently beats a notification at plugin startup.
            SetSwitchOff("Gemstone TV");

            if (PhotonNetwork.InRoom)
                NotifiLib.SendNotification(reason);

            return;
        }

        tvAssetId   = GTConsole.GetFreeAssetID();
        sofaAssetId = GTConsole.GetFreeAssetID();

        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, TvBundle, "TV", tvAssetId);
        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, TvBundle, "sofa", sofaAssetId);

        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, tvAssetId, TvPosition);
        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, sofaAssetId, SofaPosition);

        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, tvAssetId, TvRotation);
        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, sofaAssetId, SofaRotation);

        GTConsole.ExecuteCommand("asset-setvideo", ReceiverGroup.All, tvAssetId, "VideoPlayer", CurrentVideoUrl);

        NotifiLib.SendNotification("Gemstone TV spawned - the whole room can see it.");

        CoroutineManager.Instance.StartCoroutine(ResendVideoCommands());
    }

    public static void DisableTv()
    {
        if (Alive(tvAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, tvAssetId);

        if (Alive(sofaAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, sofaAssetId);

        tvAssetId   = -1;
        sofaAssetId = -1;
    }

    public static void EnableVideoPlayer()
    {
        if (!CanSpawn(out string reason))
        {
            SetSwitchOff("Video Player");

            if (PhotonNetwork.InRoom)
                NotifiLib.SendNotification(reason);

            return;
        }

        screenAssetId = GTConsole.GetFreeAssetID();

        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, ScreenBundle, "VideoPlayer", screenAssetId);
        GTConsole.ExecuteCommand("asset-setanchor", ReceiverGroup.All, screenAssetId, 1);
        GTConsole.ExecuteCommand("asset-setscale", ReceiverGroup.All, screenAssetId, new Vector3(0.05f, 0.05f, 0.05f));
        GTConsole.ExecuteCommand("asset-setlocalposition", ReceiverGroup.All, screenAssetId,
                new Vector3(0f, 0.04f, 0.12f));
        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, screenAssetId);
        GTConsole.ExecuteCommand("asset-setvideo", ReceiverGroup.All, screenAssetId, "Video", CurrentVideoUrl);

        NotifiLib.SendNotification("Video player spawned on your left hand.");

        CoroutineManager.Instance.StartCoroutine(ResendVideoCommands());
    }

    public static void DisableVideoPlayer()
    {
        if (Alive(screenAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, screenAssetId);

        screenAssetId = -1;
    }

    public static void CycleVideo()
    {
        videoIndex = (videoIndex + 1) % Videos.Length;

        if (Alive(tvAssetId))
            GTConsole.ExecuteCommand("asset-setvideo", ReceiverGroup.All, tvAssetId, "VideoPlayer", CurrentVideoUrl);

        if (Alive(screenAssetId))
            GTConsole.ExecuteCommand("asset-setvideo", ReceiverGroup.All, screenAssetId, "Video", CurrentVideoUrl);

        NotifiLib.SendNotification("Video: " + Videos[videoIndex].Name);
    }

    // Receivers wait ten seconds for an asset to exist before dropping a command
    // about it, so anyone still downloading the bundle gets the state re-sent.
    private static IEnumerator ResendVideoCommands()
    {
        yield return new WaitForSeconds(12f);

        if (Alive(tvAssetId))
        {
            Transform tv = GTConsole.ConsoleAssets[tvAssetId].AssetObject.transform;

            GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, tvAssetId, tv.position);
            GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, tvAssetId, tv.rotation);
            GTConsole.ExecuteCommand("asset-setvideo", ReceiverGroup.All, tvAssetId, "VideoPlayer",
                    CurrentVideoUrl);
        }

        if (Alive(sofaAssetId))
        {
            Transform sofa = GTConsole.ConsoleAssets[sofaAssetId].AssetObject.transform;

            GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, sofaAssetId, sofa.position);
            GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, sofaAssetId, sofa.rotation);
        }

        if (Alive(screenAssetId))
        {
            GTConsole.ExecuteCommand("asset-setanchor", ReceiverGroup.All, screenAssetId, 1);
            GTConsole.ExecuteCommand("asset-setvideo", ReceiverGroup.All, screenAssetId, "Video",
                    CurrentVideoUrl);
        }
    }

    // ---------------------------------------------------------------------
    //  Menu scan - which menu is each player running?
    // ---------------------------------------------------------------------

    public static void ScanMenus()
    {
        if (!PhotonNetwork.InRoom)
        {
            NotifiLib.SendNotification("Join a room before scanning menus.");

            return;
        }

        // Replies are only processed by a sender the receiver holds as an admin,
        // and only displayed while the indicator window below is open.
        if (!HamburburData.Admins.ContainsKey(PhotonNetwork.LocalPlayer.UserId))
        {
            NotifiLib.SendNotification(
                    "<color=grey>[</color><color=red>ADMIN</color><color=grey>]</color> " +
                    "Only Console admins can scan menus.");

            return;
        }

        GTConsole.IndicatorDelay = Time.time + 10f;

        GTConsole.ExecuteCommand("isusing", ReceiverGroup.All);

        NotifiLib.SendNotification("Scanning menus - replies will pop up for 10 seconds.");
    }

    // ---------------------------------------------------------------------
    //  Bundle browser
    // ---------------------------------------------------------------------

    public static void BackToRoot()
    {
        openBundle = null;

        SetCategory(RootCategory);
    }

    public static void OpenBundles()
    {
        openBundle = null;

        ButtonInfo[] buttons = new ButtonInfo[Bundles.Length + 1];
        buttons[0] = BackButton();

        for (int i = 0; i < Bundles.Length; i++)
        {
            string bundle = Bundles[i];

            buttons[i + 1] = new ButtonInfo
            {
                    buttonText = bundle,
                    mode       = ButtonMode.Action,
                    method     = () => OpenBundle(bundle),
                    toolTip    = $"Opens the {bundle} bundle and lists what is inside.",
            };
        }

        SetPage(ObjectsCategory, buttons);
    }

    public static void OpenBundle(string bundle)
    {
        openBundle = bundle;

        SetPage(ObjectsCategory,
                new[]
                {
                        BackButton(),
                        Label($"Downloading {bundle}..."),
                });

        CoroutineManager.Instance.StartCoroutine(FillBundlePage(bundle));
    }

    /// <summary>Whether this bundle's page is still the one the user is looking at.</summary>
    private static bool StillBrowsing(string bundle) =>
            openBundle == bundle &&
            CurrentCategory != null &&
            CurrentCategory.name == ObjectsCategory;

    private static IEnumerator FillBundlePage(string bundle)
    {
        if (!GTConsole.AssetBundlePool.ContainsKey(bundle))
        {
            if (!bundleLoads.TryGetValue(bundle, out Task load))
                bundleLoads[bundle] = load = GTConsole.LoadAssetBundle(bundle);

            while (!load.IsCompleted)
                yield return null;

            // Observe any download error so it is not left unobserved.
            _ = load.Exception;

            if (bundleLoads.TryGetValue(bundle, out Task running) && running == load)
                bundleLoads.Remove(bundle);
        }

        // The user moved on while the bundle downloaded.
        if (!StillBrowsing(bundle))
            yield break;

        if (!GTConsole.AssetBundlePool.TryGetValue(bundle, out AssetBundle loaded))
        {
            SetPage(ObjectsCategory,
                    new[]
                    {
                            BackButton(),
                            Label("This bundle would not load."),
                    });

            yield break;
        }

        // A scene bundle carries no loadable assets and throws if asked for them.
        if (loaded.isStreamedSceneAssetBundle)
        {
            SetPage(ObjectsCategory,
                    new[]
                    {
                            BackButton(),
                            Label("This bundle holds a scene, not objects."),
                    });

            yield break;
        }

        // Async so a bundle of several megabytes does not stall the frame, which
        // in a headset reads as the game hanging.
        AssetBundleRequest request = loaded.LoadAllAssetsAsync<GameObject>();

        while (!request.isDone)
            yield return null;

        if (!StillBrowsing(bundle))
            yield break;

        List<ButtonInfo> buttons = new() { BackButton(), };

        foreach (GameObject asset in request.allAssets.OfType<GameObject>())
        {
            string assetName = asset.name;

            buttons.Add(new ButtonInfo
            {
                    buttonText = assetName,
                    mode       = ButtonMode.Action,
                    method     = () => Spawn(bundle, assetName),
                    toolTip    = $"Spawns {assetName} for everyone in the room.",
            });
        }

        if (buttons.Count == 1)
            buttons.Add(Label("This bundle holds no objects."));

        SetPage(ObjectsCategory, buttons.ToArray());
    }

    // ---------------------------------------------------------------------
    //  Spawning for the room
    // ---------------------------------------------------------------------

    public static void Spawn(string bundle, string assetName)
    {
        if (!CanSpawn(out string reason))
        {
            NotifiLib.SendNotification(reason);

            return;
        }

        CoroutineManager.Instance.StartCoroutine(SpawnRoutine(bundle, assetName));
    }

    private static IEnumerator SpawnRoutine(string bundle, string assetName)
    {
        // Prove the bundle loads here before the whole room is asked for it.
        if (!GTConsole.AssetBundlePool.ContainsKey(bundle))
        {
            if (!bundleLoads.TryGetValue(bundle, out Task load))
            {
                NotifiLib.SendNotification($"Downloading {bundle}...");

                bundleLoads[bundle] = load = GTConsole.LoadAssetBundle(bundle);
            }

            while (!load.IsCompleted)
                yield return null;

            _ = load.Exception;

            if (bundleLoads.TryGetValue(bundle, out Task running) && running == load)
                bundleLoads.Remove(bundle);
        }

        if (!GTConsole.AssetBundlePool.ContainsKey(bundle) || !PhotonNetwork.InRoom)
            yield break;

        int assetId = GTConsole.GetFreeAssetID();

        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, bundle, assetName, assetId);

        // Placement waits for the local copy, so it lands on a real object
        // instead of being dropped while the bundle is still downloading.
        float deadline = Time.time + 15f;

        while (Time.time < deadline && !Alive(assetId))
            yield return null;

        if (!Alive(assetId))
        {
            NotifiLib.SendNotification(
                    $"<color=red>{assetName} did not appear.</color> The reason is in the BepInEx log.");

            yield break;
        }

        Vector3 position = SpawnPosition();
        Quaternion rotation = FacingMe(position);

        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, assetId, position);
        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, assetId, rotation);

        NotifiLib.SendNotification($"Spawned <color=#C79BFF>{assetName}</color> for the whole room.");

        // And again for clients still downloading the bundle.
        yield return new WaitForSeconds(8f);

        if (Alive(assetId))
        {
            Transform placed = GTConsole.ConsoleAssets[assetId].AssetObject.transform;

            GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, assetId, placed.position);
            GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, assetId, placed.rotation);
        }
    }

    private static Vector3 SpawnPosition()
    {
        Transform head = GorillaTagger.Instance.headCollider.transform;

        return head.position + head.forward * 2f;
    }

    /// <summary>A rotation that turns an asset at <paramref name="from"/> to face you, kept upright.</summary>
    private static Quaternion FacingMe(Vector3 from)
    {
        Transform head = GorillaTagger.Instance.headCollider.transform;

        Vector3 toMe = head.position - from;
        toMe.y      = 0f;

        // Spawned right at your head, there is no direction to you; face back
        // along your view instead.
        if (toMe.sqrMagnitude < 0.0001f)
        {
            toMe = -head.forward;
            toMe.y = 0f;
        }

        return toMe.sqrMagnitude < 0.0001f ? Quaternion.identity : Quaternion.LookRotation(toMe);
    }

    // ---------------------------------------------------------------------
    //  Everything currently in the room
    // ---------------------------------------------------------------------

    public static void OpenSpawned()
    {
        openBundle = null;

        List<ButtonInfo> buttons = new()
        {
                new()
                {
                        buttonText = "Return to Console Assets",
                        mode       = ButtonMode.Action,
                        method     = BackToRoot,
                        toolTip    = "Returns to the console assets page.",
                },

                new()
                {
                        buttonText = "Refresh List",
                        mode       = ButtonMode.Action,
                        method     = OpenSpawned,
                        toolTip    = "Re-reads the assets currently in the room.",
                },
        };

        foreach (KeyValuePair<int, GTConsole.ConsoleAsset> pair in GTConsole.ConsoleAssets)
        {
            int    id   = pair.Key;
            string name = pair.Value.AssetName;

            buttons.Add(new ButtonInfo
            {
                    buttonText  = $"Remove {name}",
                    mode        = ButtonMode.Action,
                    method      = () => RemoveAsset(id),
                    displayText = () => $"Remove <color=#C79BFF>{name}</color>",
                    toolTip     = $"Removes {name} for everyone in the room.",
            });
        }

        if (GTConsole.ConsoleAssets.Count == 0)
            buttons.Add(Label("No assets in the room yet."));

        buttons.Add(new ButtonInfo
        {
                buttonText = "Remove Everything",
                mode       = ButtonMode.Action,
                method     = RemoveAll,
                toolTip    = "Removes every Console asset in the room.",
        });

        SetPage(SpawnedCategory, buttons.ToArray());
    }

    private static void RemoveAsset(int assetId)
    {
        if (Alive(assetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, assetId);

        OpenSpawned();
    }

    private static void RemoveAll()
    {
        // A copy: the local destroy below mutates the dictionary as it runs.
        foreach (int assetId in GTConsole.ConsoleAssets.Keys.ToArray())
            if (Alive(assetId))
                GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, assetId);

        OpenSpawned();
    }

    // ---------------------------------------------------------------------
    //  Shared helpers
    // ---------------------------------------------------------------------

    private static void SetPage(string categoryName, ButtonInfo[] buttons)
    {
        ButtonCategory category = Buttons.GetCategory(categoryName);

        if (category != null)
            category.buttons = buttons;

        SetCategory(categoryName);
    }

    private static ButtonInfo BackButton() => new()
    {
            buttonText = "Return to Console Assets",
            mode       = ButtonMode.Action,
            method     = BackToRoot,
            toolTip    = "Returns to the console assets page.",
    };

    private static ButtonInfo Label(string text) => new()
    {
            buttonText = text,
            mode       = ButtonMode.Action,
            toolTip    = "Nothing to press here.",
    };

    private static bool Alive(int assetId) =>
            assetId >= 0 &&
            GTConsole.ConsoleAssets.TryGetValue(assetId, out GTConsole.ConsoleAsset asset) &&
            asset.AssetObject != null;

    private static void SetSwitchOff(string buttonText)
    {
        ButtonInfo button = Buttons.GetIndex(buttonText);

        if (button != null)
            button.enabled = false;
    }

    private static bool CanSpawn(out string reason)
    {
        if (!PhotonNetwork.InRoom)
        {
            reason = "Join a room before spawning assets.";

            return false;
        }

        // Every receiver, this client included, drops an asset command from a
        // sender it does not hold as an administrator.
        if (!HamburburData.Admins.ContainsKey(PhotonNetwork.LocalPlayer.UserId))
        {
            reason =
                    "<color=grey>[</color><color=red>ADMIN</color><color=grey>]</color> " +
                    "Only Console admins can spawn assets.";

            return false;
        }

        reason = null;

        return true;
    }
}
