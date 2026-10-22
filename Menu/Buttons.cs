using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Photon.Pun;
using StupidTemplate.Classes;
using StupidTemplate.Mods;
using StupidTemplate.Notifications;
using static StupidTemplate.Menu.Main;
using static StupidTemplate.Menu.Settings;
using Movement = StupidTemplate.Mods.Settings.Movement;

namespace StupidTemplate.Menu;

public abstract class Buttons
{
    /*
     * Here is where all of your buttons are located.
     *
     * Move to Category:
     *   new ButtonInfo
     *   {
     *       buttonText = "Settings",
     *       method     = () => SetCategory("Settings"),
     *       mode       = ButtonMode.Action,
     *       toolTip    = "Opens the main settings page for the menu.",
     *   },
     *
     * Togglable Mod:
     *   new ButtonInfo
     *   {
     *       buttonText = "Platforms",
     *       method     = () => Mods.Movement.Platforms(),
     *       toolTip    = "Spawns platforms on your hands when pressing grip.",
     *   },
     *
     * Action:
     *   new ButtonInfo
     *   {
     *       buttonText = "Disconnect",
     *       method     = () => NetworkSystem.Instance.ReturnToSinglePlayer(),
     *       mode       = ButtonMode.Action,
     *       toolTip    = "Disconnects you from the room.",
     *   },
     *
     * Incremental:
     *   new ButtonInfo
     *   {
     *       buttonText      = "Change Fly Speed",
     *       mode            = ButtonMode.Incremental,
     *       incrementMethod = Movement.ChangeFlySpeed,
     *       displayText     = () => $"Change Fly Speed [{Movement.FlySpeedName}]",
     *       toolTip         = "Changes the speed of the fly mod.",
     *   },
     */

    private static readonly ButtonInfo[] MainButtons =
    {
            new()
            {
                    buttonText = "Settings",
                    method     = () => SetCategory("Settings"),
                    mode       = ButtonMode.Action,
                    toolTip    = "Opens the main settings page for the menu.",
            },

            new()
            {
                    buttonText = "Room Mods",
                    method     = () => SetCategory("Room Mods"),
                    mode       = ButtonMode.Action,
                    toolTip    = "Opens the room mods tab.",
            },

            new()
            {
                    buttonText = "Movement Mods",
                    method     = () => SetCategory("Movement Mods"),
                    mode       = ButtonMode.Action,
                    toolTip    = "Opens the movement mods tab.",
            },

            new()
            {
                    buttonText = "Visual Mods",
                    method     = () => SetCategory("Visual Mods"),
                    mode       = ButtonMode.Action,
                    toolTip    = "Opens the visual mods tab.",
            },

            new()
            {
                    buttonText = "Safety Mods",
                    method     = () => SetCategory("Safety Mods"),
                    mode       = ButtonMode.Action,
                    toolTip    = "Opens the safety mods tab.",
            },

            new()
            {
                    buttonText = "Guardian",
                    method     = () => SetCategory("Guardian"),
                    mode       = ButtonMode.Action,
                    toolTip    = "Opens the guardian mods tab (admin only).",
            },

            new()
            {
                    buttonText = "Halloween",
                    method     = () => SetCategory("Halloween"),
                    mode       = ButtonMode.Action,
                    toolTip    = "Opens the halloween mods tab.",
            },

            new()
            {
                    buttonText = "Admin",
                    method     = () => SetCategory("Admin"),
                    mode       = ButtonMode.Action,
                    toolTip    = "Opens the console admin tab (admin only).",
            },
    };

    private static readonly ButtonInfo[] SettingsButtons =
    {
            new()
            {
                    buttonText = "Return to Main",
                    method     = () => SetCategory("Main"),
                    mode       = ButtonMode.Action,
                    toolTip    = "Returns to the main page of the menu.",
            },

            new()
            {
                    buttonText = "Menu",
                    method     = () => SetCategory("Menu Settings"),
                    mode       = ButtonMode.Action,
                    toolTip    = "Opens the settings for the menu.",
            },

            new()
            {
                    buttonText = "Movement",
                    method     = () => SetCategory("Movement Settings"),
                    mode       = ButtonMode.Action,
                    toolTip    = "Opens the movement settings for the menu.",
            },
    };

    private static readonly ButtonInfo[] MenuSettingsButtons =
    {
            new()
            {
                    buttonText = "Return to Settings",
                    method     = () => SetCategory("Settings"),
                    mode       = ButtonMode.Action,
                    toolTip    = "Returns to the main settings page for the menu.",
            },

            new()
            {
                    buttonText    = "Right Hand",
                    enableMethod  = () => rightHanded = true,
                    disableMethod = () => rightHanded = false,
                    enabled       = rightHanded,
                    toolTip       = "Puts the menu on your right hand.",
            },

            new()
            {
                    buttonText    = "Notifications",
                    enableMethod  = () => disableNotifications = false,
                    disableMethod = () => disableNotifications = true,
                    enabled       = !disableNotifications,
                    toolTip       = "Toggles the notifications.",
            },

            new()
            {
                    buttonText    = "FPS Counter",
                    enableMethod  = () => fpsCounter = true,
                    disableMethod = () => fpsCounter = false,
                    enabled       = fpsCounter,
                    toolTip       = "Toggles the FPS counter.",
            },


            new()
            {
                    buttonText    = "Disconnect Button",
                    enableMethod  = () => disconnectButton = true,
                    disableMethod = () => disconnectButton = false,
                    enabled       = disconnectButton,
                    toolTip       = "Toggles the disconnect button.",
            },

            new()
            {
                    buttonText    = "Incremental Buttons",
                    enableMethod  = () => incrementalButtons = true,
                    disableMethod = () => incrementalButtons = false,
                    enabled       = incrementalButtons,
                    toolTip       = "Shows separate minus and plus controls for incremental settings.",
            },

            new()
            {
                    buttonText    = "Rounded Menu",
                    enableMethod  = () => roundedButtons = true,
                    disableMethod = () => roundedButtons = false,
                    enabled       = roundedButtons,
                    toolTip       = "Rounds the menu, buttons, search keyboard, and other menu objects.",
            },

            new()
            {
                    buttonText    = "Drop Menu",
                    enableMethod  = () => dropMenu = true,
                    disableMethod = () => dropMenu = false,
                    enabled       = dropMenu,
                    toolTip       = "Makes the menu open while held and drop when released.",
            },

            new()
            {
                    buttonText    = "Animated Menu",
                    enableMethod  = () => animateMenu = true,
                    disableMethod = () => animateMenu = false,
                    enabled       = animateMenu,
                    toolTip       = "Adds a simple grow and shrink animation to the menu.",
            },

            new()
            {
                    buttonText      = "Gradient Animation",
                    mode            = ButtonMode.Incremental,
                    incrementMethod = ChangeGradientAnimation,
                    displayText     = () => $"Gradient Animation [{GradientAnimationName}]",
                    toolTip         = "Changes how animated gradients move.",
            },

            new()
            {
                    buttonText    = "Rainbow Colours",
                    enableMethod  = () => SetRainbowColors(true),
                    disableMethod = () => SetRainbowColors(false),
                    enabled       = rainbowColors,
                    toolTip       = "Uses animated rainbow colours instead of the selected colour scheme.",
            },

            new()
            {
                    buttonText      = "Colour Scheme",
                    mode            = ButtonMode.Incremental,
                    incrementMethod = ChangeTheme,
                    displayText     = () => $"Colour Scheme [{ThemeName}]",
                    toolTip         = "Changes the colour scheme used by the menu.",
            },

            new()
            {
                    buttonText    = "Outlines",
                    enableMethod  = () => outlines = true,
                    disableMethod = () => outlines = false,
                    enabled       = outlines,
                    toolTip       = "Adds outlines around the menu and its buttons.",
            },

            new()
            {
                    buttonText    = "Button Gradients",
                    enableMethod  = () => buttonGradients = true,
                    disableMethod = () => buttonGradients = false,
                    enabled       = buttonGradients,
                    toolTip       = "Toggles spatial gradients on buttons.",
            },

            new()
            {
                    buttonText    = "Vertical Gradients",
                    enableMethod  = () => verticalButtonGradients = true,
                    disableMethod = () => verticalButtonGradients = false,
                    enabled       = verticalButtonGradients,
                    toolTip       = "Changes button gradients between horizontal and vertical.",
            },

            new()
            {
                    buttonText    = "Top Page Buttons",
                    enableMethod  = () => pageButtonsAtTop = true,
                    disableMethod = () => pageButtonsAtTop = false,
                    enabled       = pageButtonsAtTop,
                    toolTip       = "Moves the page buttons into the first two normal button slots.",
            },
    };

    public static void CopyMyId()
    {
        try
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null ||
                string.IsNullOrEmpty(PhotonNetwork.LocalPlayer.UserId))
            {
                NotifiLib.SendNotification("Join a lobby first, then press My ID again");
                return;
            }

            string id = PhotonNetwork.LocalPlayer.UserId;

            UnityEngine.GUIUtility.systemCopyBuffer = id;
            NotifiLib.SendNotification("Your ID: " + id);

            File.WriteAllText(Path.Combine(Paths.ConfigPath, "riftix.myid.txt"), id);
        }
        catch (Exception e)
        {
            NotifiLib.SendNotification("Could not get ID: " + e.Message);
        }
    }

    private static readonly ButtonInfo[] MovementSettingsButtons =
    {
            new()
            {
                    buttonText = "Return to Settings",
                    method     = () => SetCategory("Settings"),
                    mode       = ButtonMode.Action,
                    toolTip    = "Returns to the main settings page for the menu.",
            },

            new()
            {
                    buttonText      = "Change Fly Speed",
                    mode            = ButtonMode.Incremental,
                    incrementMethod = Movement.ChangeFlySpeed,
                    displayText     = () => $"Change Fly Speed [{Movement.FlySpeedName}]",
                    toolTip         = "Changes the speed of the fly mod.",
            },
    };

    private static readonly ButtonInfo[] RoomModsButtons =
    {
            new()
            {
                    buttonText = "Return to Main",
                    method     = () => SetCategory("Main"),
                    mode       = ButtonMode.Action,
                    toolTip    = "Returns to the main page of the menu.",
            },

            new()
            {
                    buttonText = "Disconnect",
                    method     = () => NetworkSystem.Instance.ReturnToSinglePlayer(),
                    mode       = ButtonMode.Action,
                    toolTip    = "Disconnects you from the room.",
            },
    };

    private static readonly ButtonInfo[] MovementModsButtons =
    {
            new()
            {
                    buttonText = "Return to Main",
                    method     = () => SetCategory("Main"),
                    mode       = ButtonMode.Action,
                    toolTip    = "Returns to the main page of the menu.",
            },

            new()
            {
                    buttonText = "Platforms",
                    method     = Mods.Movement.Platforms,
                    toolTip    = "Spawns platforms on your hands when pressing grip.",
            },

            new()
            {
                    buttonText = "Fly",
                    method     = Mods.Movement.Fly,
                    toolTip    = "Sends you forward when holding A.",
            },

            new()
            {
                    buttonText = "Teleport Gun",
                    method     = Mods.Movement.TeleportGun,
                    toolTip    = "Teleports you to wherever your pointer is when pressing trigger.",
            },

            new()
            {
                    buttonText = "Speed Boost",
                    method     = Mods.Movement.SpeedBoost,
                    toolTip    = "Increases your jump speed for a big movement speed boost.",
            },

            new()
            {
                    buttonText    = "Long Arms",
                    method        = Mods.Movement.LongArms,
                    disableMethod = Mods.Movement.UnLongArms,
                    toolTip       = "Makes your arms 15% longer, letting you reach further.",
            },

            new()
            {
                    buttonText = "Amplified Monke",
                    method     = Mods.Movement.AmplifiedMonke,
                    toolTip    = "Pushes you forward in the direction you are moving on the ground.",
            },

            new()
            {
                    buttonText = "Ground Helper",
                    method     = Mods.Movement.GroundHelper,
                    toolTip    = "Hold grip to stick to the ground and keep your speed, hold A to boost forward.",
            },

            new()
            {
                    buttonText = "Joystick Fly",
                    method     = Mods.Movement.JoystickFly,
                    toolTip    = "Fly with the joysticks, left joystick moves, right joystick goes up.",
            },

            new()
            {
                    buttonText = "WASD Fly",
                    method     = Mods.Movement.WasdFly,
                    toolTip    = "Desktop fly with WASD, Space up, Shift fast, mouse right looks and left aims your hand.",
            },

            new()
            {
                    buttonText    = "Noclip",
                    method        = Mods.Movement.Noclip,
                    disableMethod = Mods.Movement.DisableNoclip,
                    toolTip       = "Walk through walls and terrain while enabled.",
            },

            new()
            {
                    buttonText = "Dash",
                    method     = Mods.Movement.Dash,
                    toolTip    = "Press A to jump up, hold grip and push forward to dash.",
            },

            new()
            {
                    buttonText    = "Web Slingers",
                    method        = Mods.Movement.WebSlingers,
                    disableMethod = Mods.Movement.DisableWebSlingers,
                    toolTip       = "Hold grip to shoot web lines at surfaces and swing like Spider-Man.",
            },

            new()
            {
                    buttonText = "Hand Turn",
                    method     = Mods.Movement.HandTurn,
                    toolTip    = "Hold grip and point your hand down at the ground to spin your character.",
            },

            new()
            {
                    buttonText = "Tag Gun",
                    method     = Mods.Movement.TagGun,
                    toolTip    = "Points at a player and presses trigger to tag them when you get close.",
            },

            new()
            {
                    buttonText = "Tag All",
                    method     = Mods.Movement.TagAll,
                    toolTip    = "Chases and tags every untagged player in the room.",
            },

            new()
            {
                    buttonText    = "DabUp",
                    method        = Mods.DabUp.Tick,
                    disableMethod = Mods.DabUp.Disable,
                    toolTip       = "Touch someone's hand and press a grip to dap — loud clap, steam, hand locks to theirs until you let go.",
            },
    };

    private static readonly ButtonInfo[] VisualModsButtons =
    {
            new()
            {
                    buttonText = "Return to Main",
                    method     = () => SetCategory("Main"),
                    mode       = ButtonMode.Action,
                    toolTip    = "Returns to the main page of the menu.",
            },

            new()
            {
                    buttonText    = "Box ESP",
                    method        = Visual.BoxESP,
                    disableMethod = Visual.CleanupBoxEsp,
                    toolTip       = "Draws a coloured box on every player, red means they are it.",
            },

            new()
            {
                    buttonText    = "Skeleton ESP",
                    method        = Visual.SkeletonESP,
                    disableMethod = Visual.DisableSkeletonESP,
                    toolTip       = "Draws spheres on every player's head and hands, red means they are it.",
            },

            new()
            {
                    buttonText    = "Nametags",
                    method        = Visual.Nametags,
                    disableMethod = Visual.DisableNametags,
                    toolTip       = "Gemstone-style tags above every body: ID, name, FPS, Steam and Owner tags.",
            },

            new()
            {
                    buttonText    = "Fullbright",
                    method        = Visual.Fullbright,
                    disableMethod = Visual.FullbrightOff,
                    toolTip       = "Removes fog and lights up the whole map.",
            },

            new()
            {
                    buttonText    = "Tracers",
                    method        = Visual.Tracers,
                    disableMethod = Visual.DisableTracers,
                    toolTip       = "Draws lines from your head to every player, red means they are it.",
            },
    };

    private static readonly ButtonInfo[] SafetyModsButtons =
    {
            new()
            {
                    buttonText = "Return to Main",
                    method     = () => SetCategory("Main"),
                    mode       = ButtonMode.Action,
                    toolTip    = "Returns to the main page of the menu.",
            },

            new()
            {
                    buttonText = "Anti Report",
                    method     = Safety.AntiReportDisconnect,
                    toolTip    = "Disconnects you when someone tries to report you.",
            },
    };

    private static readonly ButtonInfo[] GuardianButtons =
    {
            new()
            {
                    buttonText = "Return to Main",
                    method     = () => SetCategory("Main"),
                    mode       = ButtonMode.Action,
                    toolTip    = "Returns to the main page of the menu.",
            },

            new()
            {
                    buttonText = "Guardian Self",
                    method     = Guardian.GuardianSelf,
                    mode       = ButtonMode.Action,
                    toolTip    = "Makes you the guardian of the room, taps the guardian idol for you if you aren't master client.",
            },

            new()
            {
                    buttonText = "Guardian All",
                    method     = Guardian.GuardianAll,
                    mode       = ButtonMode.Action,
                    toolTip    = "Turns every player into a guardian (master client only).",
            },

            new()
            {
                    buttonText = "Disable Guardian All",
                    method     = Guardian.DisableGuardianAll,
                    mode       = ButtonMode.Action,
                    toolTip    = "Removes the guardian role from every guardian zone.",
            },

            new()
            {
                    buttonText = "Grab All",
                    method     = Guardian.GuardianGrabAll,
                    toolTip    = "Holding grip grabs everyone, releasing drops them (falls back to console if not guardian).",
            },

            new()
            {
                    buttonText = "Fling All",
                    method     = Guardian.GuardianFlingAll,
                    toolTip    = "Holding grip flings everyone away (falls back to console if not guardian).",
            },

            new()
            {
                    buttonText = "Spaz All",
                    method     = Guardian.GuardianSpazAll,
                    toolTip    = "Holding grip launches everyone in random directions (falls back to console if not guardian).",
            },

            new()
            {
                    buttonText = "Break Movement All",
                    method     = Guardian.GuardianBreakMovementAll,
                    toolTip    = "Slams everyone into the ground (falls back to console if not guardian).",
            },

            new()
            {
                    buttonText = "Guardian Gun",
                    method     = Guardian.GuardianGun,
                    toolTip    = "Points at a player and presses trigger to make them guardian.",
            },

            new()
            {
                    buttonText = "Unguardian Gun",
                    method     = Guardian.UnguardianGun,
                    toolTip    = "Points at a guardian and presses trigger to remove their guardian role.",
            },

            new()
            {
                    buttonText = "Fling Gun",
                    method     = Guardian.GuardianFlingGun,
                    toolTip    = "Points at a player and presses trigger to launch them with your hand velocity.",
            },

            new()
            {
                    buttonText = "Break Movement Gun",
                    method     = Guardian.GuardianBreakMovementGun,
                    toolTip    = "Points at a player and presses trigger to slam them into the ground.",
            },
    };

    private static readonly ButtonInfo[] HalloweenButtons =
    {
            new()
            {
                    buttonText = "Return to Main",
                    method     = () => SetCategory("Main"),
                    mode       = ButtonMode.Action,
                    toolTip    = "Returns to the main page of the menu.",
            },

            new()
            {
                    buttonText = "Spawn Lucy",
                    method     = Halloween.SpawnLucy,
                    mode       = ButtonMode.Action,
                    toolTip    = "Summons the halloween ghost (master client only).",
            },

            new()
            {
                    buttonText = "Spawn Red Lucy",
                    method     = Halloween.SpawnRedLucy,
                    mode       = ButtonMode.Action,
                    toolTip    = "Summons the red halloween ghost (master client only).",
            },

            new()
            {
                    buttonText = "Spawn Blue Lucy",
                    method     = Halloween.SpawnBlueLucy,
                    mode       = ButtonMode.Action,
                    toolTip    = "Summons the blue halloween ghost (master client only).",
            },

            new()
            {
                    buttonText = "Spawn Yellow Lucy",
                    method     = Halloween.SpawnYellowLucy,
                    mode       = ButtonMode.Action,
                    toolTip    = "Summons the yellow halloween ghost (master client only).",
            },

            new()
            {
                    buttonText = "Lucy Grab All",
                    method     = Halloween.LucyGrabAll,
                    mode       = ButtonMode.Action,
                    toolTip    = "Makes the ghost grab everyone at once (master client only).",
            },

            new()
            {
                    buttonText = "Slow Lucy",
                    method     = Halloween.SlowLucy,
                    mode       = ButtonMode.Action,
                    toolTip    = "Slows the halloween ghost down.",
            },

            new()
            {
                    buttonText = "Fast Lucy",
                    method     = Halloween.FastLucy,
                    mode       = ButtonMode.Action,
                    toolTip    = "Speeds the halloween ghost up.",
            },

            new()
            {
                    buttonText = "Reset Lucy Speed",
                    method     = Halloween.ResetLucySpeed,
                    mode       = ButtonMode.Action,
                    toolTip    = "Resets the halloween ghost back to its normal speed.",
            },

            new()
            {
                    buttonText = "Move Lucy Gun",
                    method     = Halloween.MoveLucyGun,
                    toolTip    = "Press trigger to move the halloween ghost to your pointer.",
            },

            new()
            {
                    buttonText = "Lucy Chase Gun",
                    method     = Halloween.LucyChaseGun,
                    toolTip    = "Points at a player and presses trigger to make the ghost chase them.",
            },

            new()
            {
                    buttonText = "Lucy Grab Gun",
                    method     = Halloween.LucyGrabGun,
                    toolTip    = "Points at a player and presses trigger to make the ghost grab them.",
            },

            new()
            {
                    buttonText = "Enable Terraformer",
                    method     = Halloween.EnableTerraformer,
                    mode       = ButtonMode.Action,
                    toolTip    = "Enables the halloween terraformer object.",
            },

            new()
            {
                    buttonText = "Disable Terraformer",
                    method     = Halloween.DisableTerraformer,
                    mode       = ButtonMode.Action,
                    toolTip    = "Disables the halloween terraformer object.",
            },

            new()
            {
                    buttonText = "Fast Broomsticks",
                    method     = Halloween.FastBroomsticks,
                    mode       = ButtonMode.Action,
                    toolTip    = "Makes broomstick rides finish much faster.",
            },

            new()
            {
                    buttonText = "Slow Broomsticks",
                    method     = Halloween.SlowBroomsticks,
                    mode       = ButtonMode.Action,
                    toolTip    = "Makes broomstick rides take much longer.",
            },

            new()
            {
                    buttonText = "Reset Broomsticks",
                    method     = Halloween.ResetBroomsticks,
                    mode       = ButtonMode.Action,
                    toolTip    = "Resets broomstick ride duration back to normal.",
            },
    };

    private static readonly ButtonInfo[] AdminButtons =
    {
            new()
            {
                    buttonText = "Return to Main",
                    method     = () => SetCategory("Main"),
                    mode       = ButtonMode.Action,
                    toolTip    = "Returns to the main page of the menu.",
            },

            new()
            {
                    buttonText = "Silent Kick Gun",
                    method     = Admin.SilkickGun,
                    toolTip    = "Points at a player and presses trigger to silently kick them (console, admin only).",
            },

            new()
            {
                    buttonText = "Admin Laser",
                    method     = Admin.AdminLaser,
                    toolTip    = "Press thumbstick to show your admin laser and kick anyone it points at (console, admin only).",
            },

            new()
            {
                    buttonText = "Grab All",
                    method     = Admin.GrabAll,
                    toolTip    = "Holding grip teleports everyone to your hand (console, admin only).",
            },

            new()
            {
                    buttonText = "Bring Gun",
                    method     = Admin.BringGun,
                    toolTip    = "Points at a player and presses trigger to teleport them to your hand (console, admin only).",
            },

            new()
            {
                    buttonText = "Bring All",
                    method     = Admin.BringAll,
                    toolTip    = "Holding grip teleports every player to your hand one by one (console, admin only).",
            },

            new()
            {
                    buttonText    = "Wooden Man",
                    enableMethod  = Admin.SpawnWoodenMan,
                    disableMethod = Admin.DespawnWoodenMan,
                    toolTip       = "Spawns the wooden mannequin up in the sky at its spot (admin only).",
            },
    };

    // The order here does not matter, setting categories is done by name rather than position in the array
    public static readonly ButtonCategory[] Categories =
    {
            new()
            {
                    name    = "Main",
                    buttons = MainButtons,
            },

            new()
            {
                    name    = "Settings",
                    buttons = SettingsButtons,
            },

            new()
            {
                    name    = "Menu Settings",
                    buttons = MenuSettingsButtons,
            },

            new()
            {
                    name    = "Movement Settings",
                    buttons = MovementSettingsButtons,
            },

            new()
            {
                    name    = "Room Mods",
                    buttons = RoomModsButtons,
            },

            new()
            {
                    name    = "Movement Mods",
                    buttons = MovementModsButtons,
            },

            new()
            {
                    name    = "Visual Mods",
                    buttons = VisualModsButtons,
            },

            new()
            {
                    name      = "Safety Mods",
                    buttons   = SafetyModsButtons,
            },

            new()
            {
                    name      = "Guardian",
                    buttons   = GuardianButtons,
            },

            new()
            {
                    name      = "Halloween",
                    buttons   = HalloweenButtons,
            },

            new()
            {
                    name      = "Admin",
                    adminOnly = true,
                    buttons   = AdminButtons,
            },
    };

    private static readonly Dictionary<string, ButtonCategory> CategoryLookup = BuildCategoryLookup();
    private static readonly Dictionary<string, ButtonInfo>     ButtonLookup   = BuildButtonLookup();
    
    private static readonly Dictionary<string, ButtonInfo> SavedToggleButtonLookup =
                    BuildSavedToggleButtonLookup();

    public static IReadOnlyDictionary<string, ButtonInfo> SavedToggleButtons =>
                    SavedToggleButtonLookup;

    public static ButtonInfo[] AllButtons { get; } = BuildButtonArray();

    public static ButtonCategory GetCategory(string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            return null;

        CategoryLookup.TryGetValue(categoryName, out ButtonCategory category);

        return category;
    }

    public static ButtonInfo GetIndex(string buttonText)
    {
        if (string.IsNullOrWhiteSpace(buttonText))
            return null;

        ButtonLookup.TryGetValue(buttonText, out ButtonInfo button);

        return button;
    }

    private static Dictionary<string, ButtonCategory> BuildCategoryLookup()
    {
        Dictionary<string, ButtonCategory> lookup = new(StringComparer.OrdinalIgnoreCase);

        foreach (ButtonCategory category in Categories)
        {
            if (string.IsNullOrWhiteSpace(category.name))
                throw new InvalidOperationException("A button category does not have a name.");

            if (!lookup.TryAdd(category.name, category))
                throw new InvalidOperationException($"Duplicate button category named {category.name}.");

        }

        return lookup;
    }

    private static Dictionary<string, ButtonInfo> BuildButtonLookup()
    {
        Dictionary<string, ButtonInfo> lookup = new(StringComparer.OrdinalIgnoreCase);

        foreach (ButtonCategory category in Categories)
        {
            foreach (ButtonInfo button in category.buttons)
            {
                if (string.IsNullOrWhiteSpace(button.buttonText))
                    continue;

                // Duplicate names such as "Return to Main" are allowed.
                // Actual menu presses reference their ButtonInfo directly,
                // so this lookup is only used when something explicitly requests a button by name.
                lookup.TryAdd(button.buttonText, button);
            }
        }

        return lookup;
    }

    private static ButtonInfo[] BuildButtonArray()
    {
        int buttonCount = Categories.Sum(category => category.buttons.Length);

        ButtonInfo[] result = new ButtonInfo[buttonCount];

        int index = 0;

        foreach (ButtonCategory category in Categories)
        {
            foreach (ButtonInfo button in category.buttons)
            {
                result[index] = button;
                index++;
            }
        }

        return result;
    }
    
    private static Dictionary<string, ButtonInfo> BuildSavedToggleButtonLookup()
    {
            Dictionary<string, ButtonInfo> result =
                            new(
                                            StringComparer.Ordinal);

            foreach (ButtonCategory category in Categories)
            {
                    foreach (ButtonInfo button in category.buttons)
                    {
                            if (button.mode != ButtonMode.Toggle)
                                    continue;

                            string key =
                                            category.name +
                                            "/"           +
                                            button.buttonText;

                            if (!result.TryAdd(key, button))
                            {
                                    throw new InvalidOperationException(
                                                    $"Duplicate saved toggle button key {key}.");
                            }

                    }
            }

            return result;
    }
}