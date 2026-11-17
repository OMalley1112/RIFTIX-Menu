using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using GorillaLocomotion;
using HarmonyLib;
using StupidTemplate.Classes;
using StupidTemplate.Notifications;
using StupidTemplate.Tools;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using static StupidTemplate.Menu.Settings;
using static StupidTemplate.Menu.Buttons;
using Button = StupidTemplate.Classes.Button;
using HamburburData = StupidTemplate.Classes.Console.HamburburData;
using Random = UnityEngine.Random;

/*
 * Hello, current and future developers!
 * This is ii's Stupid Template, a base mod menu template for Gorilla Tag.
 *
 * Comments are placed around the code showing you how certain classes work, such as the settings, buttons, and notifications.
 *
 * If you need help with the template, you may join the Discord server https://discord.gg/iidk or contact @zlothyy on Discord
 * It's full of talented developers that can show you the way and how things work.
 *
 * Thank you, and enjoy the template!
 */

namespace StupidTemplate.Menu;

[HarmonyPatch(typeof(GTPlayer), nameof(GTPlayer.LateUpdate))]
public class Main : MonoBehaviour
{

    private const  float TextRenderScale   = 0.001f;
    private const  float TextSurfaceOffset = 0.0035f;
    private static int?  noInvisLayerMask;

    // Where the aim ball rests when the ray finds nothing to land on: open air
    // within this range, or a couple of metres past whoever was in the way.
    private const float OpenAirDistance  = 30f;
    private const float PlayerOvershoot = 2.5f;

    // Variables
    // Important
    // Objects
    public static GameObject menu;
    public static GameObject menuBackground;
    public static GameObject canvasObject;

    private static Texture2D watermarkTexture;
    private static Material  watermarkMaterial;

    private static readonly Vector3 MenuScale = new(
            0.1f,
            0.3f,
            0.3825f);

    // Screenshot layout: a header bar across the top (logo, wordmark, clock
    // and three round icon buttons) over a single column of full width rows,
    // each ending in a chevron, with the pager pills and the ping readout on
    // the bottom bar. All numbers are in menu-local units where +Y is the left
    // side of the screen and +Z is up; text helpers take world units, so
    // multiply by MenuScale to convert.
    private const float LogoY         = 0.365f;   // logo disc centre, across the panel
    private const float LogoZ         = 0.477f;   // logo disc centre, up the panel
    private const float TitleStartY   = 0.295f;   // where the wordmark starts
    private const float ClockEdgeY    = -0.41f;   // right edge of the time / date block
    private const float IconsZ        = 0.432f;   // search / favorites / exit circles
    private const float IconDiameter  = 0.08f;
    private const float IconSpacing   = 0.09f;
    private const float IconRightY    = -0.418f;  // rightmost (exit) circle centre
    private const float DividerZ      = 0.385f;   // rule under the header

    private const float GridCenterY   = 0f;       // rows are centred across the panel
    private const float RowWidth      = 0.88f;    // full width row plate
    private const float GridRowHeight = 0.082f;
    private const float GridTopZ      = 0.36f;    // top edge of the first row
    private const float GridBottomZ   = -0.44f;   // bottom edge of the last row
    private const float RowGapMax     = 0.03f;

    private const float RowTitleY     = 0.10f;    // title rect centre; text starts +0.40
    private const float StateEdgeY    = -0.30f;   // right edge of the ON / OFF word
    private const float ChevronY      = -0.36f;   // chevron centre, across the row

    private const float SearchWidth   = RowWidth; // width the favorites hint borrows
    private const float FooterZ       = -0.498f;  // pager pills / ping line

    private static GameObject searchRoot;

    private static GameObject leftReference;
    private static GameObject rightReference;

    public static SphereCollider leftButtonCollider;
    public static SphereCollider rightButtonCollider;

    private static bool menuButtonWasPressed;
    private static bool menuToggledOpen;

    private static bool menuClosing;
    private static bool menuShouldDropOnClose;

    private static GameObject   searchKeyboard;
    private static string       searchQuery   = string.Empty;
    private static ButtonInfo[] searchResults = Array.Empty<ButtonInfo>();
    private static bool         searchKeyboardFollowing;

    private static bool searchUsingDesktopKeyboard;

    public static Camera TPC;
    public static Text   pingText;

    // Menu wordmark + the footer page counter
    private static Text riftixText;
    private static Text pageCounterText;

    // Menu clock in the header: rewritten every throttle tick so the time
    // does not go stale while the menu sits open.
    private static Text headerTimeText;
    private static Text headerDateText;

    private static string lastClockText;
    private static string lastDateText;

    // Pressing the header's exit icon keeps the menu closed even while the
    // menu button is still held down - otherwise the next Prefix frame would
    // cancel the close and bounce it straight back open.
    private static bool closeLatched;

    // Writing the ping text every single frame forces a canvas rebuild every
    // frame, so it is only rewritten on the throttle tick below.
    private static float nextPingUpdateTime;

    // Throttle for the "menu build failed" log - a deterministic build failure
    // would otherwise print once per frame.
    private static float nextBuildErrorLogTime;

    // Shared chrome materials + shader lookups. Shader.Find is a linear search
    // through every shader in the game, so these are created once and reused
    // instead of on every menu rebuild.
    private static Shader   spritesShader;
    private static Material chromeCardMaterial;
    private static Material   scrollTrackMaterial;
    private static Material   pagerPillMaterial;
    private static Material   logoMaterial;
    private static Material   dividerMaterial;

    // Camera lookups (desktop / keyboard mode). GameObject.Find is expensive and
    // used to run every frame, so the results are cached here.
    private static Transform cachedVirtualCamera;
    private static float     nextCameraLookupTime;
    private static float     nextVcamLookupTime;

    private static GameObject   GunPointer;
    private static LineRenderer GunLine;

    // Rows currently alive on the menu, keyed by the button they display. Lets a
    // normal toggle update itself in place instead of destroying and rebuilding
    // the entire menu on every single button press (that was the main in-game hitch).
    private static readonly Dictionary<ButtonInfo, LiveRow> liveRows = new();

    // Bumped by every full rebuild so stale row references are detected.
    private static int menuBuildId;

    // Hidden menu kept around between open / close so reopening is instant.
    private static GameObject stashedMenu;

    // Everything that makes up the scrollable list and the tab bar. Page, tab and
    // category jumps only rebuild these instead of the whole menu.
    private static readonly List<GameObject> listObjects = new();
    private static readonly List<Text>       listTexts   = new();

    private static readonly List<GameObject> tabObjects = new();
    private static readonly List<Text>       tabTexts   = new();

    // Where freshly created UI is recorded (null = chrome, never rebuilt here).
    private static List<GameObject> currentObjects;
    private static List<Text>       currentTexts;

    // Search bar label lives in the chrome but follows the list state.
    private static Text searchLabel;

    private static void Track(GameObject created)
    {
        currentObjects?.Add(created);
    }

    private static void Track(Text created)
    {
        currentTexts?.Add(created);
    }

    private static void BeginTrack(List<GameObject> objects, List<Text> texts)
    {
        currentObjects = objects;
        currentTexts   = texts;
    }

    private static void EndTrack()
    {
        currentObjects = null;
        currentTexts   = null;
    }

    private static void DestroyTracked(
            List<GameObject> objects,
            List<Text>       texts)
    {
        foreach (GameObject created in objects)
            if (created != null)
                Destroy(created);

        foreach (Text created in texts)
            if (created != null)
                Destroy(created.gameObject);

        objects.Clear();
        texts.Clear();
    }

    private static Vector3 smoothedGunPosition;
    private static Vector3 gunSmoothVelocity;
    private static bool     gunWasHolding;

    // Data
    public static int pageNumber;

    private static ButtonCategory currentCategory;

    private static readonly ButtonInfo previousPageButton = new()
    {
            buttonText  = "PreviousPage",
            displayText = () => "< Previous Page",
            method      = PreviousPage,
            mode        = ButtonMode.Action,
            toolTip     = "Moves to the previous page.",
    };

    private static readonly ButtonInfo nextPageButton = new()
    {
            buttonText  = "NextPage",
            displayText = () => "Next Page >",
            method      = NextPage,
            mode        = ButtonMode.Action,
            toolTip     = "Moves to the next page.",
    };

    private static readonly ButtonInfo searchButton = new()
    {
            buttonText = "Search",

            displayText = () =>
                                  IsSearching
                                          ? $"Search [{(string.IsNullOrEmpty(searchQuery) ? "..." : searchQuery)}]"
                                          : "Search",

            method  = ToggleSearch,
            mode    = ButtonMode.Action,
            toolTip = "Searches for buttons in the menu.",
    };

    // FAVORITES in the header works as a toggle: press it a second time and
    // the normal list comes back, so one button covers both of the old tabs.
    private static readonly ButtonInfo favoritesTabButton = new()
    {
            buttonText  = "FavoritesTab",
            displayText = () => "FAVORITES",
            method      = () => SetTab(activeTab == 1 ? 0 : 1),
            mode        = ButtonMode.Action,
            toolTip     = "Shows only the mods you starred.",
    };

    // Exit icon at the right end of the header row.
    private static readonly ButtonInfo exitMenuButton = new()
    {
            buttonText  = "CloseMenu",
            displayText = () => "Close",
            method      = CloseMenuFromHeader,
            mode        = ButtonMode.Action,
            toolTip     = "Closes the menu.",
    };

    // The logo disc doubles as a home button.
    private static readonly ButtonInfo logoButton = new()
    {
            buttonText  = "LogoHome",
            displayText = () => "RIFTIX",
            method      = GoHome,
            mode        = ButtonMode.Action,
            toolTip     = "Back to the main page.",
    };

    // Corner pill that goes back to the last room we were in. The name is
    // unique so nothing collides with a real category button.
    private static readonly ButtonInfo rejoinButton = new()
    {
            buttonText  = "RejoinRoom",
            displayText = () => "Rejoin",
            method      = RejoinRoom,
            mode        = ButtonMode.Action,
            toolTip     = "Rejoins the last room you were in.",
    };

    // Captured every frame while a room is active (Prefix), so it survives
    // leaving - pressing Rejoin from the lobby goes back to it.
    private static string lastRoomName;

    // 0 = HOME tab, 1 = FAVORITES tab
    public static int activeTab;

    public static bool IsSearching { get; private set; }

    private static void SetTab(int tab)
    {
        if (activeTab == tab)
            return;

        activeTab = tab;
        pageNumber = 0;
    }

    private static void GoHome()
    {
        activeTab  = 0;
        pageNumber = 0;

        SetCategory("Main");
    }

    // The header's exit icon: latch the close so holding the menu button no
    // longer cancels it, then close like a normal release would. The latch is
    // let go in Prefix as soon as nothing is holding the menu open.
    private static void CloseMenuFromHeader()
    {
        if (IsSearching)
            CloseSearch(false);

        closeLatched = true;

        BeginCloseMenu(false);
    }

    // Back into the last room we were in. AttemptToJoinSpecificRoom handles
    // the leave + join internally, the same call the game's room code screen
    // makes, so this works from the lobby after a Leave or a kick.
    private static void RejoinRoom()
    {
        if (string.IsNullOrEmpty(lastRoomName))
        {
            NotifiLib.SendNotification("No room to rejoin yet - join a room first.");

            return;
        }

        try
        {
            GorillaNetworking.PhotonNetworkController.Instance.AttemptToJoinSpecificRoom(
                    lastRoomName,
                    GorillaNetworking.JoinType.Solo);

            NotifiLib.SendNotification("Rejoining " + lastRoomName + "...");
        }
        catch (Exception exc)
        {
            Debug.LogError($"{Constants.Name} // Rejoin failed: {exc}");
        }
    }

    public static ButtonCategory CurrentCategory =>
            currentCategory ??= GetCategory("Main");

    public static void SetCategory(string categoryName)
    {
        ButtonCategory category = GetCategory(categoryName);

        if (category == null)
        {
            Debug.LogError($"{Constants.Name} // Category {categoryName} does not exist.");

            return;
        }

        if (category.adminOnly &&
            !HamburburData.LocalAdminAccess)
        {
            NotifiLib.SendNotification(
                    "<color=grey>[</color><color=red>ADMIN</color><color=grey>]</color> " +
                    "This category is only available to admins.");

            return;
        }

        currentCategory = category;
        pageNumber      = 0;

        if (IsSearching)
            CloseSearch(false);
    }

    // Visual parts of one list row, kept around so the row can refresh itself in
    // place instead of forcing a full menu rebuild.
    private sealed class LiveRow
    {
        public GameObject ownerMenu;
        public ButtonMode mode;

        public Text title;
        public Text subtitle;
        public Text state;

        public readonly List<ColorChanger> changers = new();

        public void Refresh(ButtonInfo target)
        {
            ExtGradient rowColors =
                    target.enabled
                            ? target.enabledColor  ?? buttonColors[1]
                            : target.disabledColor ?? buttonColors[0];

            for (int i = 0; i < changers.Count; i++)
                if (changers[i] != null)
                    changers[i].colors = rowColors;

            if (title == null)
                return;

            // Screenshot style rows: one white line, the ON / OFF word on the
            // right carries the state (and the chevron beyond it stays put).
            title.text  = RowTitleText(target);
            title.color = Color.white;

            if (subtitle != null)
                subtitle.text = GetRowSubtitle(target);

            if (state != null)
            {
                state.text = target.enabled
                                     ? "ON"
                                     : "OFF";

                state.color = target.enabled
                                      ? new Color32(199, 155, 255, 255)
                                      : new Color32(140, 140, 140, 255);
            }
        }
    }

    // Constant
    public static void Prefix()
    {
        try
        {
            // Remember the active room for the Rejoin corner pill - this has
            // to happen while we are still in it, before any Leave or kick.
            if (Photon.Pun.PhotonNetwork.InRoom &&
                Photon.Pun.PhotonNetwork.CurrentRoom != null)
                lastRoomName = Photon.Pun.PhotonNetwork.CurrentRoom.Name;

            // Open the menu with either face button on the active hand:
            // X or Y on the left controller, A or B on the right one.
            bool menuButtonPressed =
                    !rightHanded &&
                    (ControllerInputPoller.instance.leftControllerPrimaryButton   ||
                     ControllerInputPoller.instance.leftControllerSecondaryButton) ||
                    rightHanded &&
                    (ControllerInputPoller.instance.rightControllerPrimaryButton   ||
                     ControllerInputPoller.instance.rightControllerSecondaryButton);

            bool keyboardOpen =
                    UnityInput.Current.GetKey(keyboardButton);

            // The header's exit icon only holds the menu down while the menu
            // button itself is down; letting go arms it again for the next open.
            if (!menuButtonPressed &&
                !keyboardOpen)
                closeLatched = false;

            if (IsSearching)
            {
                if (searchUsingDesktopKeyboard)
                    HandleDesktopSearchInput();
                else
                    UpdateSearchLayout();
            }

            if (menu != null)
                UpdateMenuAnimation();

            bool keepOpen =
                    menuButtonPressed ||
                    keyboardOpen      ||
                    IsSearching;

            bool keyboardMode =
                    keyboardOpen ||
                    IsSearching &&
                    searchUsingDesktopKeyboard;

            if (menu == null)
            {
                if (keepOpen &&
                    !closeLatched)
                {
                    // Reuse the hidden menu from the last close instead of
                    // rebuilding it, which is what made opening stutter.
                    if (stashedMenu != null)
                        OpenStashedMenu();
                    else
                        CreateMenu();

                    CreateReferences();

                    RecenterMenu(
                            rightHanded,
                            keyboardMode);
                }
            }
            else if (keepOpen)
            {
                if (menuClosing &&
                    !closeLatched)
                    CancelMenuClose();

                CreateReferences();

                RecenterMenu(
                        rightHanded,
                        keyboardMode);
            }
            else if (!menuClosing)
            {
                BeginCloseMenu(
                        dropMenu &&
                        !keyboardOpen);
            }
        }
        catch (Exception exc)
        {
            Debug.LogError(
                    $"{Constants.Name} // Error initializing at {exc.StackTrace}: {exc.Message}");
        }

        try
        {
            if (GunPointer != null)
            {
                if (!GunPointer.activeSelf)
                    Destroy(GunPointer);
                else
                    GunPointer.SetActive(false);
            }

            if (GunLine != null)
            {
                if (!GunLine.gameObject.activeSelf)
                {
                    Destroy(GunLine.gameObject);
                    GunLine = null;
                }
                else
                    GunLine.gameObject.SetActive(false);
            }
        }
        catch { }

        try
        {
            if (pingText != null && Time.unscaledTime >= nextPingUpdateTime)
            {
                nextPingUpdateTime =
                        Time.unscaledTime +
                        0.5f;

                // Room ping on the bottom right, "-- ms" in the lobby or on
                // the desktop where there is no room to measure.
                bool inRoom =
                        Photon.Pun.PhotonNetwork.InRoom &&
                        Photon.Pun.PhotonNetwork.CurrentRoom != null;

                pingText.text =
                        inRoom
                                ? Photon.Pun.PhotonNetwork.GetPing() + " ms"
                                : "-- ms";

                // Header clock, only touched when the string really changed.
                string nowTime = System.DateTime.Now.ToString("h:mm tt");
                string nowDate = System.DateTime.Now.ToString("M/d/yyyy");

                if (nowTime != lastClockText &&
                    headerTimeText != null)
                {
                    lastClockText        = nowTime;
                    headerTimeText.text  = nowTime;
                }

                if (nowDate != lastDateText &&
                    headerDateText != null)
                {
                    lastDateText        = nowDate;
                    headerDateText.text = nowDate;
                }
            }

            // Deferred preference writes (marked by button presses).
            Preferences.FlushDirty();

            // Animated text gradient sweep + button press pop animation.
            GradientText.Tick();
            Button.TickPops();

            ButtonInfo[] allButtons =
                    AllButtons;

            foreach (ButtonInfo button in allButtons)
            {
                if (button.mode != ButtonMode.Toggle ||
                    !button.enabled                  ||
                    button.method == null)
                {
                    continue;
                }

                try
                {
                    button.method.Invoke();
                }
                catch (Exception exc)
                {
                    Debug.LogError(
                            $"{Constants.Name} // Error with mod {button.buttonText} at {exc.StackTrace}: {exc.Message}");
                }
            }
        }
        catch (Exception exc)
        {
            Debug.LogError(
                    $"{Constants.Name} // Error with executing mods at {exc.StackTrace}: {exc.Message}");
        }
    }

    // Functions
    // Shared chrome material (header / footer cards). Built once, reused for the
    // lifetime of the plugin instead of allocating + Shader.Find on every rebuild.
    private static Material GetChromeCardMaterial()
    {
        if (chromeCardMaterial == null)
        {
            chromeCardMaterial =
                    new Material(GetSpritesShader())
                    {
                            color = new Color32(
                                    26,
                                    20,
                                    38,
                                    242),
                    };
        }

        return chromeCardMaterial;
    }

    // Flat grey pager pills (screenshot style footer).
    private static Material GetPagerPillMaterial()
    {
        if (pagerPillMaterial == null)
        {
            pagerPillMaterial =
                    new Material(GetSpritesShader())
                    {
                            color = new Color32(
                                    74,
                                    70,
                                    88,
                                    245),
                    };
        }

        return pagerPillMaterial;
    }

    // Purple logo disc in the header.
    private static Material GetLogoMaterial()
    {
        if (logoMaterial == null)
        {
            logoMaterial =
                    new Material(GetSpritesShader())
                    {
                            color = new Color32(
                                    147,
                                    96,
                                    250,
                                    255),
                    };
        }

        return logoMaterial;
    }

    // The thin rule between the header and the list.
    private static Material GetDividerMaterial()
    {
        if (dividerMaterial == null)
        {
            dividerMaterial =
                    new Material(GetSpritesShader())
                    {
                            color = new Color32(
                                    150,
                                    150,
                                    170,
                                    180),
                    };
        }

        return dividerMaterial;
    }

    private static Material GetScrollTrackMaterial()
    {
        if (scrollTrackMaterial == null)
        {
            scrollTrackMaterial =
                    new Material(GetSpritesShader())
                    {
                            color = new Color32(
                                    16,
                                    12,
                                    26,
                                    255),
                    };
        }

        return scrollTrackMaterial;
    }

    private static Shader GetSpritesShader()
    {
        if (spritesShader == null)
            spritesShader = Shader.Find("Sprites/Default");

        return spritesShader;
    }

    // Cached "CM vcam1" lookup. Returns null while the throttle window is open and
    // the camera has not been found yet, so the scene is not scanned every frame.
    private static Transform GetVirtualCamera(bool forceLookup = false)
    {
        if (cachedVirtualCamera != null)
            return cachedVirtualCamera;

        if (!forceLookup &&
            Time.unscaledTime < nextVcamLookupTime)
            return null;

        nextVcamLookupTime =
                Time.unscaledTime +
                5f;

        cachedVirtualCamera =
                GameObject.Find("Shoulder Camera")
                         ?.transform
                          .Find("CM vcam1");

        return cachedVirtualCamera;
    }

    public static void CreateMenu(bool playOpenAnimation = true)
    {
        // Full rebuild: drop the old row registrations (and any hidden menu) so
        // nothing can hold a reference to destroyed objects.
        liveRows.Clear();
        menuBuildId++;

        listObjects.Clear();
        listTexts.Clear();
        tabObjects.Clear();
        tabTexts.Clear();

        currentObjects = null;
        currentTexts   = null;

        if (stashedMenu != null)
        {
            Destroy(stashedMenu);

            stashedMenu = null;
        }

        // Menu Holder
        menu = GameObject.CreatePrimitive(PrimitiveType.Cube);

        Destroy(menu.GetComponent<Rigidbody>());
        Destroy(menu.GetComponent<BoxCollider>());
        Destroy(menu.GetComponent<Renderer>());

        menu.transform.localScale = MenuScale;

        menuClosing = false;

        // Menu Background
        menuBackground = GameObject.CreatePrimitive(PrimitiveType.Cube);

        Destroy(menuBackground.GetComponent<Rigidbody>());
        Destroy(menuBackground.GetComponent<BoxCollider>());

        menuBackground.transform.SetParent(
                menu.transform,
                false);

        menuBackground.transform.localRotation =
                Quaternion.identity;

        menuBackground.transform.localPosition =
                new Vector3(
                        0.5f,
                        0f,
                        0f);

        if (roundedButtons)
        {
            RoundedButtonMesh.Apply(
                    menuBackground,
                    menuSize,
                    menuCornerRadius,
                    buttonCornerSegments);
        }
        else
        {
            menuBackground.transform.localScale =
                    menuSize;
        }

        ColorChanger colorChanger =
                menuBackground.AddComponent<ColorChanger>();

        colorChanger.colors =
                backgroundColor;

        colorChanger.spatialGradient =
                buttonGradients;

        colorChanger.verticalGradient =
                verticalButtonGradients;

        // Canvas
        canvasObject =
                new GameObject("Menu Canvas");

        canvasObject.transform.SetParent(
                menu.transform,
                true);

        Canvas canvas =
                canvasObject.AddComponent<Canvas>();

        CanvasScaler canvasScaler =
                canvasObject.AddComponent<CanvasScaler>();

        canvasObject.AddComponent<GraphicRaycaster>();

        canvas.renderMode =
                RenderMode.WorldSpace;

        canvasScaler.dynamicPixelsPerUnit =
                1500f;

        // Screenshot style header: round logo disc + wordmark on the left,
        // the clock stacked on the right, the rule underneath - and the three
        // round icon buttons tracked separately, because a press on one of
        // them only rebuilds the list.
        GameObject logo =
                GameObject.CreatePrimitive(PrimitiveType.Sphere);

        logo.name  = $"{Constants.Name}_Logo";
        logo.layer = 2;

        Destroy(logo.GetComponent<Rigidbody>());

        Collider logoCollider =
                logo.GetComponent<Collider>();

        logoCollider.isTrigger = true;

        Button logoPress =
                logo.AddComponent<Button>();

        logoPress.relatedButton = logoButton;

        logo.transform.SetParent(
                menu.transform,
                false);

        logo.transform.localRotation =
                Quaternion.identity;

        logo.transform.localPosition =
                new Vector3(
                        0.56f,
                        LogoY,
                        LogoZ);

        // Local scale: the menu scales its three axes differently, so these
        // numbers make the disc come out perfectly round on screen.
        logo.transform.localScale =
                new Vector3(
                        0.3f,
                        0.1f,
                        0.0784f);

        logo.GetComponent<Renderer>().material =
                GetLogoMaterial();

        // The R sitting inside the disc.
        CreateHeaderText(
                new Vector3(
                        0.064f,
                        LogoY * MenuScale.y,
                        LogoZ * MenuScale.z),
                new Vector2(
                        0.03f,
                        0.014f),
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                Color.white).text = "R";

        // Wordmark, left aligned so it starts just past the logo. MiddleLeft
        // text starts at its rect's left edge, so the centre sits half a rect
        // width in from TitleStartY.
        riftixText = CreateHeaderText(
                new Vector3(
                        0.064f,
                        TitleStartY * MenuScale.y - 0.10f,
                        LogoZ * MenuScale.z),
                new Vector2(
                        0.20f,
                        0.018f),
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                Color.white);

        riftixText.text = "RIFTIX";

        // Clock block: time over date, both hung off the same right edge.
        headerTimeText = CreateHeaderText(
                new Vector3(
                        0.064f,
                        ClockEdgeY * MenuScale.y + 0.07f,
                        0.512f * MenuScale.z),
                new Vector2(
                        0.14f,
                        0.009f),
                TextAnchor.MiddleRight,
                FontStyle.Bold,
                new Color32(
                        235,
                        235,
                        245,
                        255));

        headerTimeText.text =
                lastClockText =
                        System.DateTime.Now.ToString("h:mm tt");

        headerDateText = CreateHeaderText(
                new Vector3(
                        0.064f,
                        ClockEdgeY * MenuScale.y + 0.07f,
                        0.484f * MenuScale.z),
                new Vector2(
                        0.14f,
                        0.008f),
                TextAnchor.MiddleRight,
                FontStyle.Normal,
                new Color32(
                        175,
                        175,
                        195,
                        255));

        headerDateText.text =
                lastDateText =
                        System.DateTime.Now.ToString("M/d/yyyy");

        // Where the search field reads while it is being typed into.
        searchLabel = CreateHeaderText(
                new Vector3(
                        0.064f,
                        -0.003f,
                        IconsZ * MenuScale.z),
                new Vector2(
                        0.108f,
                        0.009f),
                TextAnchor.MiddleRight,
                FontStyle.Italic,
                new Color32(
                        150,
                        150,
                        160,
                        255));

        searchLabel.text =
                searchButton.GetDisplayText();

        // Thin rule between the header and the list.
        GameObject divider =
                GameObject.CreatePrimitive(PrimitiveType.Cube);

        divider.name  = $"{Constants.Name}_Divider";
        divider.layer = 2;

        Destroy(divider.GetComponent<Rigidbody>());
        Destroy(divider.GetComponent<BoxCollider>());

        divider.transform.SetParent(
                menu.transform,
                false);

        divider.transform.localRotation =
                Quaternion.identity;

        divider.transform.localPosition =
                new Vector3(
                        0.56f,
                        0f,
                        DividerZ);

        divider.transform.localScale =
                new Vector3(
                        0.05f,
                        0.88f,
                        0.004f);

        divider.GetComponent<Renderer>().material =
                GetDividerMaterial();

        CreateTrackedHeaderIcons();

        // Rejoin + Leave: the screenshot's two pills floating outside the
        // panel's bottom-right corner, Rejoin above Leave.
        if (disconnectButton)
        {
            CreateEdgePill(rejoinButton, "Rejoin", -0.31f);
            CreateEdgePill(GetIndex("Disconnect"), "Leave", -0.40f);
        }

        try
        {
            CreateMenuButtons();
        }
        catch (Exception exc)
        {
            // `menu` is already non-null by the time the buttons are built, so a
            // build that throws half way leaves a menu the open check believes is
            // complete: only the buttons created before the throw ever work, and
            // nothing retries. Throw the whole thing away so the next frame
            // rebuilds from scratch and the real error lands in the log.
            if (Time.unscaledTime >= nextBuildErrorLogTime)
            {
                nextBuildErrorLogTime =
                        Time.unscaledTime +
                        5f;

                Debug.LogError(
                        $"{Constants.Name} // Menu build failed, discarding it: {exc}");
            }

            Destroy(menu);

            menu = null;

            return;
        }

        if (animateMenu &&
            playOpenAnimation)
        {
            menu.transform.localScale =
                    MenuScale *
                    0.01f;
        }
    }

    // One rounded pill floating just outside the panel's bottom-right edge,
    // with its centred caption on the canvas (Rejoin / Leave).
    private static void CreateEdgePill(ButtonInfo button, string label, float localZ)
    {
        if (button == null)
            return;

        GameObject pill =
                GameObject.CreatePrimitive(PrimitiveType.Cube);

        if (!UnityInput.Current.GetKey(keyboardButton))
            pill.layer = 2;

        Destroy(pill.GetComponent<Rigidbody>());

        BoxCollider pillCollider =
                pill.GetComponent<BoxCollider>();

        pillCollider.isTrigger = true;

        pill.transform.SetParent(menu.transform, false);
        pill.transform.localRotation = Quaternion.identity;
        pill.transform.localPosition =
                new Vector3(
                        0.56f,
                        -0.67f,
                        localZ);

        Vector3 pillSize =
                new(
                        0.09f,
                        0.26f,
                        0.045f);

        if (roundedButtons)
        {
            RoundedButtonMesh.Apply(
                    pill,
                    pillSize,
                    buttonCornerRadius,
                    buttonCornerSegments);
        }
        else
            pill.transform.localScale = pillSize;

        Button press =
                pill.AddComponent<Button>();

        press.relatedButton = button;

        ColorChanger colorChanger =
                pill.AddComponent<ColorChanger>();

        colorChanger.colors           = buttonColors[0];
        colorChanger.spatialGradient  = buttonGradients;
        colorChanger.verticalGradient = verticalButtonGradients;

        Text pillText = new GameObject
        {
                transform =
                {
                        parent = canvasObject.transform,
                },
        }.AddComponent<Text>();

        pillText.text                  = label;
        pillText.font                  = currentFont;
        pillText.fontSize              = 1;
        pillText.color                 = Color.white;
        pillText.alignment             = TextAnchor.MiddleCenter;
        pillText.resizeTextForBestFit  = true;
        pillText.resizeTextMinSize     = 0;

        RectTransform pillTransform =
                pillText.GetComponent<RectTransform>();

        // The pill sits outside the panel (menu y -0.67); text runs in world
        // units, so the height comes back through MenuScale.z.
        pillTransform.localPosition =
                new Vector3(
                        0.064f,
                        -0.67f * MenuScale.y,
                        localZ * MenuScale.z);

        pillTransform.sizeDelta =
                new Vector2(
                        0.07f,
                        0.01f);

        pillTransform.localRotation =
                Quaternion.Euler(
                        180f,
                        90f,
                        90f);
    }

    // Creates a world space Text on the menu canvas, matching the title/fps pattern.
    private static Text CreateHeaderText(
            Vector3   position,
            Vector2   size,
            TextAnchor alignment,
            FontStyle style,
            Color     color)
    {
        Text text = new GameObject
        {
                transform =
                {
                        parent = canvasObject.transform,
                },
        }.AddComponent<Text>();

        text.font                 = currentFont;
        text.fontSize             = 1;
        text.color                = color;
        text.fontStyle            = style;
        text.alignment            = alignment;
        text.supportRichText      = true;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize    = 0;

        RectTransform component =
                text.GetComponent<RectTransform>();

        component.localPosition =
                Vector3.zero;

        component.sizeDelta =
                size;

        // Local transforms: the menu may already be rotated onto the hand when
        // this runs (page counters and header labels get rebuilt), and a world
        // transform here would leave them facing world-zero instead of you.
        component.localPosition =
                position;

        component.localRotation =
                Quaternion.Euler(
                        180f,
                        90f,
                        90f);

        return text;
    }

    private static void CreateWatermark()
    {
        const string WatermarkName = "watermark.png";

        watermarkTexture ??=
                StupidAssetUtils.LoadTexture2D(WatermarkName);

        if (watermarkTexture == null)
        {
            Debug.LogError(
                    $"{Constants.Name} // Failed to load {WatermarkName}.");

            return;
        }

        if (watermarkMaterial == null)
        {
            Shader shader =
                    Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogError(
                        $"{Constants.Name} // Failed to find watermark shader.");

                return;
            }

            watermarkMaterial =
                    new Material(shader)
                    {
                            mainTexture = watermarkTexture,
                            color       = Color.white,
                    };
        }

        GameObject watermark =
                GameObject.CreatePrimitive(
                        PrimitiveType.Quad);

        watermark.name =
                $"{Constants.Name}_Watermark";

        Destroy(
                watermark.GetComponent<Collider>());

        watermark.transform.SetParent(
                menu.transform,
                false);

        float backSurface =
                menuBackground.transform.localPosition.x -
                menuSize.x / 2f                          -
                0.002f;

        watermark.transform.localPosition =
                new Vector3(
                        backSurface,
                        0f,
                        0f);

        watermark.transform.localRotation =
                Quaternion.Euler(
                        0f,
                        -90f,
                        0f) *
                Quaternion.Euler(
                        0f,
                        0f,
                        -90f);

        float aspect =
                watermarkTexture.width /
                (float)watermarkTexture.height;

        const float Height = 0.55f;

        float correctedWidth =
                Height *
                aspect *
                (MenuScale.z / MenuScale.y);

        watermark.transform.localScale =
                new Vector3(
                        correctedWidth,
                        Height,
                        1f);

        Renderer renderer =
                watermark.GetComponent<Renderer>();

        renderer.sharedMaterial =
                watermarkMaterial;
    }

    private static ButtonInfo[] GetVisibleButtons()
    {
        if (IsSearching)
            return searchResults;

        ButtonInfo[] buttons = CurrentCategory.buttons;

        if (activeTab == 1)
            buttons = buttons.Where(button => button.favorite).ToArray();

        // Admin-only pages stay out of a non-admin's menu, the way the
        // desktop GUI already hides their categories: both the entry buttons
        // that lead there and any favorite pointing into them would only
        // dead-end at SetCategory's refusal toast.
        if (!HamburburData.LocalAdminAccess)
            buttons = buttons.Where(button => !OpensAdminCategory(button) &&
                                              !IsInAdminCategory(button))
                             .ToArray();

        return buttons;
    }

    // Entry buttons carry the name of the category they open, so an exact
    // name lookup tells the "Console Assets" row apart from any other button.
    private static bool OpensAdminCategory(ButtonInfo button) =>
            GetCategory(button.buttonText)?.adminOnly == true;

    // A button living inside an admin-only category (a favorite, say) is as
    // unusable for a non-admin as the entry that leads there.
    private static bool IsInAdminCategory(ButtonInfo button)
    {
        foreach (ButtonCategory category in Categories)
            if (category.adminOnly && Array.IndexOf(category.buttons, button) != -1)
                return true;

        return false;
    }

    private static int GetContentButtonsPerPage()
    {
        // One full width row per line, and the list fills the band between
        // the header rule and the pager - so the setting is allowed to run
        // higher than the old two column grid's six, but not so high that
        // the rows turn into slivers.
        int perPage = Mathf.Clamp(buttonsPerPage, 1, 10);

        if (!pageButtonsAtTop)
            return perPage;

        return Mathf.Max(1, perPage - 2);
    }

    private static int GetLastPage()
    {
        ButtonInfo[] visibleButtons = GetVisibleButtons();

        if (visibleButtons.Length == 0)
            return 0;

        int pageSize = GetContentButtonsPerPage();

        return (visibleButtons.Length - 1) / pageSize;
    }

    private static void PreviousPage()
    {
        int lastPage = GetLastPage();

        pageNumber--;

        if (pageNumber < 0)
            pageNumber = lastPage;
    }

    private static void NextPage()
    {
        int lastPage = GetLastPage();

        pageNumber++;

        if (pageNumber > lastPage)
            pageNumber = 0;
    }

    private static void CreateMenuButtons()
    {
        // Clamp the page before anything is drawn: the footer bar writes its
        // "N / M" counter below, while CreateList only clamps later - so a page
        // count that shrank (top page buttons toggled, a favorite removed)
        // showed "03 / 02" until the next press.
        int lastPage = GetLastPage();

        if (pageNumber > lastPage)
            pageNumber = lastPage;

        // The header icons travel with the list: the FAVORITES highlight
        // depends on which tab is open, so both are rebuilt together.
        CreateFooterBar();

        CreateList();
    }

    private static void CreateTrackedHeaderIcons()
    {
        BeginTrack(tabObjects, tabTexts);

        try
        {
            CreateHeaderIcons();
        }
        finally
        {
            EndTrack();
        }
    }

    // Rows, scrollbar and page counter state - everything a page / tab / category
    // change touches. One full width row per button, top to bottom.
    private static void CreateList()
    {
        BeginTrack(listObjects, listTexts);

        try
        {
            ButtonInfo[] visibleButtons = GetVisibleButtons();

            int pageSize  = GetContentButtonsPerPage();
            int rowOffset = 0;

            if (pageButtonsAtTop)
            {
                CreateButton(0, previousPageButton);
                CreateButton(1, nextPageButton);

                rowOffset = 2;
            }

            int lastPage = GetLastPage();

            if (pageNumber > lastPage)
                pageNumber = lastPage;

            int startIndex = pageNumber * pageSize;

            int visibleCount = Math.Min(
                    pageSize,
                    Math.Max(0, visibleButtons.Length - startIndex));

            if (visibleCount == 0)
            {
                if (activeTab == 1)
                    CreateFavoritesHint();

                return;
            }

            for (int i = 0; i < visibleCount; i++)
            {
                CreateButton(
                        i + rowOffset,
                        visibleButtons[startIndex + i]);
            }

            CreatePageScrollbar();
        }
        finally
        {
            EndTrack();
        }
    }

    // Rows on a page: one button per row now that the list is a single column.
    private static int GridRowsPerPage()
    {
        int cells = GetContentButtonsPerPage();

        if (pageButtonsAtTop)
            cells += 2;

        return Mathf.Max(1, cells);
    }

    // Centre height of one list row: rows are distributed down the band
    // between GridTopZ and GridBottomZ so the list fills the area between
    // the header rule and the pager no matter how many rows are on it.
    private static float RowCenterZ(int row)
    {
        const float rowHeight = GridRowHeight;

        int   rows = GridRowsPerPage();
        float band = GridTopZ - GridBottomZ;

        float gap =
                rows > 1
                        ? Mathf.Min(RowGapMax, (band - rows * rowHeight) / (rows - 1))
                        : 0f;

        float stack = rows * rowHeight + (rows - 1) * gap;

        float topCenter =
                GridTopZ -
                rowHeight / 2f   -
                (band - stack) / 2f;

        return topCenter - row * (rowHeight + gap);
    }

    private static string PageCounterValue() =>
            $"{pageNumber + 1:00} / {GetLastPage() + 1:00}";

    // Cool detail: slim page scrollbar pinned to the right edge of the grid.
    private static void CreatePageScrollbar()
    {
        const float trackCenterZ = (GridTopZ + GridBottomZ) / 2f;
        const float trackLength  = GridTopZ - GridBottomZ;

        Vector3 trackPosition = new(
                0.56f,
                -0.4635f,
                trackCenterZ);

        Vector3 trackSize = new(
                0.05f,
                0.02f,
                trackLength);

        GameObject track =
                GameObject.CreatePrimitive(PrimitiveType.Cube);

        Track(track);

        track.name  = $"{Constants.Name}_ScrollTrack";
        track.layer = 2;

        Destroy(track.GetComponent<Rigidbody>());
        Destroy(track.GetComponent<BoxCollider>());

        track.transform.SetParent(menu.transform, false);
        track.transform.localRotation = Quaternion.identity;
        track.transform.localPosition  = trackPosition;

        if (roundedButtons)
        {
            RoundedButtonMesh.Apply(
                    track,
                    trackSize,
                    0.01f,
                    buttonCornerSegments);
        }
        else
            track.transform.localScale = trackSize;

        track.GetComponent<Renderer>().material =
                GetScrollTrackMaterial();

        int lastPage = GetLastPage();

        int totalPages = lastPage + 1;

        // Thumb shrinks as pages pile up and slides down as you page forward.
        float thumbLength =
                trackLength / Mathf.Max(1, totalPages);

        float progress =
                lastPage > 0
                        ? pageNumber / (float)lastPage
                        : 0f;

        float thumbTop =
                trackCenterZ +
                trackLength / 2f                      -
                progress * (trackLength - thumbLength);

        Vector3 thumbSize = new(
                0.052f,
                0.022f,
                thumbLength);

        Vector3 thumbPosition = new(
                0.562f,
                -0.4635f,
                thumbTop - thumbLength / 2f);

        GameObject thumb =
                GameObject.CreatePrimitive(PrimitiveType.Cube);

        Track(thumb);

        thumb.name  = $"{Constants.Name}_ScrollThumb";
        thumb.layer = 2;

        Destroy(thumb.GetComponent<Rigidbody>());
        Destroy(thumb.GetComponent<BoxCollider>());

        thumb.transform.SetParent(menu.transform, false);
        thumb.transform.localRotation = Quaternion.identity;
        thumb.transform.localPosition  = thumbPosition;

        if (roundedButtons)
        {
            RoundedButtonMesh.Apply(
                    thumb,
                    thumbSize,
                    0.011f,
                    buttonCornerSegments);
        }
        else
            thumb.transform.localScale = thumbSize;

        ColorChanger thumbChanger =
                thumb.AddComponent<ColorChanger>();

        thumbChanger.colors = buttonColors[1];
    }

    // The screenshot layout's header navigation: three round buttons tucked
    // under the clock - search, the FAVORITES toggle and the exit icon. They
    // live in the tracked tab lists, so a press (which only rebuilds the
    // list) repaints the FAVORITES highlight together with the rows.
    private static void CreateHeaderIcons()
    {
        float searchY = IconRightY + IconSpacing * 2f;
        float heartY  = IconRightY + IconSpacing;

        CreateHeaderIcon(
                searchButton,
                searchY,
                "○");

        favoritesTabButton.enabled = activeTab == 1;

        CreateHeaderIcon(
                favoritesTabButton,
                heartY,
                "♥");

        CreateHeaderIcon(
                exitMenuButton,
                IconRightY,
                "→");

        // The magnifier handle: Arial has no magnifier character, so the
        // search icon is a ring (the O glyph above) with a short tilted bar
        // stuck to its lower right - the trick the old search bar used,
        // shrunk down to fit inside the circle.
        GameObject handle =
                GameObject.CreatePrimitive(PrimitiveType.Cube);

        Track(handle);

        handle.name  = $"{Constants.Name}_SearchHandle";
        handle.layer = 2;

        Destroy(handle.GetComponent<Rigidbody>());
        Destroy(handle.GetComponent<BoxCollider>());

        handle.transform.SetParent(menu.transform, false);

        handle.transform.localPosition =
                new Vector3(
                        0.595f,
                        searchY - 0.024f,
                        IconsZ - 0.024f);

        // 45 degrees on screen needs 51.9 degrees here because the menu
        // scale is non-uniform (0.3 across, 0.3825 down).
        handle.transform.localRotation =
                Quaternion.Euler(
                        -51.9f,
                        0f,
                        0f);

        handle.transform.localScale =
                new Vector3(
                        0.014f,
                        0.026f,
                        0.011f);

        ColorChanger handleChanger =
                handle.AddComponent<ColorChanger>();

        handleChanger.colors = buttonColors[1];
    }

    // One round header button: plate + glyph. The plate's corner radius is
    // capped at half its height, which rounds it all the way into a circle.
    private static void CreateHeaderIcon(
            ButtonInfo button,
            float      menuY,
            string     glyph)
    {
        // The menu scales its axes differently, so the plate is a little
        // shorter than it is wide and still comes out round on screen.
        Vector3 iconSize = new(
                0.05f,
                IconDiameter,
                IconDiameter * 0.784f);

        GameObject icon =
                GameObject.CreatePrimitive(PrimitiveType.Cube);

        Track(icon);

        icon.name  = $"{Constants.Name}_HeaderIcon";
        icon.layer = 2;

        Destroy(icon.GetComponent<Rigidbody>());

        BoxCollider iconCollider =
                icon.GetComponent<BoxCollider>();

        iconCollider.isTrigger = true;

        icon.transform.SetParent(
                menu.transform,
                false);

        icon.transform.localRotation = Quaternion.identity;

        icon.transform.localPosition =
                new Vector3(
                        0.56f,
                        menuY,
                        IconsZ);

        RoundedButtonMesh.Apply(
                icon,
                iconSize,
                Mathf.Min(iconSize.y, iconSize.z) / 2f,
                buttonCornerSegments);

        Button press =
                icon.AddComponent<Button>();

        press.relatedButton = button;

        ExtGradient colors = button.enabled
                                     ? button.enabledColor  ?? buttonColors[1]
                                     : button.disabledColor ?? buttonColors[0];

        ColorChanger colorChanger =
                icon.AddComponent<ColorChanger>();

        colorChanger.colors           = colors;
        colorChanger.spatialGradient  = button.useGradient ?? buttonGradients;
        colorChanger.verticalGradient = verticalButtonGradients;

        CreateRowText(
                glyph,
                new Vector2(
                        0.020f,
                        0.013f),
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                button.enabled
                        ? Color.white
                        : new Color32(
                                235,
                                235,
                                245,
                                255),
                IconsZ * MenuScale.z,
                menuY * MenuScale.y);
    }

    // Bottom bar: previous / page counter / next spread across the bottom,
    // with the ping readout tucked into the far right corner (Qubit style).
    private static void CreateFooterBar()
    {
        float footerWorldZ =
                FooterZ *
                MenuScale.z;

        // With "Top Page Buttons" on, the prev/next pair lives in the first
        // two list rows instead (CreateList) - drawing it here as well showed
        // both copies of the page controls at once.
        if (!pageButtonsAtTop)
        {
            CreateFooterPill(
                    previousPageButton,
                    0.175f,
                    "<",
                    footerWorldZ);

            CreateFooterPill(
                    nextPageButton,
                    -0.175f,
                    ">",
                    footerWorldZ);
        }

        // Page counter in the gap the two pills leave between them.
        pageCounterText = CreateHeaderText(
                new Vector3(
                        0.064f,
                        0f,
                        footerWorldZ),
                new Vector2(
                        0.10f,
                        0.009f),
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                new Color32(
                        175,
                        175,
                        185,
                        255));

        pageCounterText.text =
                PageCounterValue();

        // Ping under the rows, right aligned like the clock above it; Prefix
        // keeps the text fresh once every half second.
        pingText = CreateHeaderText(
                new Vector3(
                        0.064f,
                        -0.41f * MenuScale.y + 0.045f,
                        footerWorldZ - 0.004f),
                new Vector2(
                        0.09f,
                        0.007f),
                TextAnchor.MiddleRight,
                FontStyle.Normal,
                new Color32(
                        150,
                        150,
                        160,
                        255));

        pingText.text =
                "-- ms";
    }

    // One flat grey pager pill: collider + glyph, no outline, no counter.
    private static void CreateFooterPill(
            ButtonInfo button,
            float      menuY,
            string     glyph,
            float      worldZ)
    {
        // Square-ish on screen: the menu is 1.275 times taller than it is
        // wide once scaled, so the plate is a little shorter than it is wide.
        Vector3 pillSize = new(
                buttonSize.x,
                0.103f,
                0.078f);

        GameObject pill =
                GameObject.CreatePrimitive(PrimitiveType.Cube);

        pill.name  = $"{Constants.Name}_PagerPill";
        pill.layer = 2;

        Destroy(pill.GetComponent<Rigidbody>());

        BoxCollider pillCollider =
                pill.GetComponent<BoxCollider>();

        pillCollider.isTrigger = true;

        pill.transform.SetParent(
                menu.transform,
                false);

        pill.transform.localRotation =
                Quaternion.identity;

        pill.transform.localPosition =
                new Vector3(
                        0.56f,
                        menuY,
                        FooterZ);

        if (roundedButtons)
        {
            // A third of the short side: round enough for the screenshot's
            // squircle, not all the way into an oval.
            RoundedButtonMesh.Apply(
                    pill,
                    pillSize,
                    0.026f,
                    buttonCornerSegments);
        }
        else
            pill.transform.localScale = pillSize;

        pill.GetComponent<Renderer>().material =
                GetPagerPillMaterial();

        Button press =
                pill.AddComponent<Button>();

        press.relatedButton = button;

        CreateRowText(
                glyph,
                new Vector2(
                        0.06f,
                        0.02f),
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                Color.white,
                worldZ,
                menuY * MenuScale.y);
    }

    // Shown on the FAVORITES tab when nothing has been starred yet.
    private static void CreateFavoritesHint()
    {
        const float bandCenterZ = (GridTopZ + GridBottomZ) / 2f;

        float worldZ =
                bandCenterZ *
                MenuScale.z;

        ButtonInfo hint = new()
        {
                buttonText = "FavoritesHint",
                mode       = ButtonMode.Action,
                toolTip    = "No favorites yet.",
        };

        CreateButtonObject(
                hint,
                new Vector3(
                        buttonSize.x,
                        SearchWidth,
                        0.18f),
                new Vector3(
                        0.56f,
                        GridCenterY,
                        bandCenterZ),
                false,
                false,
                false);

        CreateRowText(
                "No favorites yet",
                new Vector2(
                        0.19f,
                        0.015f),
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                textColors[0],
                worldZ + 0.005f,
                GridCenterY * MenuScale.y);

        CreateRowText(
                "Double tap a mod to star it",
                new Vector2(
                        0.19f,
                        0.0085f),
                TextAnchor.MiddleLeft,
                FontStyle.Normal,
                new Color32(
                        150,
                        150,
                        150,
                        255),
                worldZ - 0.0075f,
                GridCenterY * MenuScale.y);
    }

    private static void CreateSearchKeyboard()
    {
        if (searchUsingDesktopKeyboard ||
            searchKeyboard != null)
        {
            return;
        }

        EnsureSearchRoot();

        searchKeyboard =
                new GameObject(
                        $"{Constants.Name}_SearchKeyboard");

        searchKeyboard.transform.SetParent(
                searchRoot.transform,
                false);

        GameObject background =
                GameObject.CreatePrimitive(
                        PrimitiveType.Cube);

        Destroy(background.GetComponent<Rigidbody>());
        Destroy(background.GetComponent<BoxCollider>());

        background.transform.SetParent(
                searchKeyboard.transform,
                false);

        Vector3 backgroundSize =
                new(
                        0.025f,
                        0.68f,
                        0.28f);

        if (roundedButtons)
        {
            RoundedButtonMesh.Apply(
                    background,
                    backgroundSize,
                    0.025f,
                    buttonCornerSegments);
        }
        else
        {
            background.transform.localScale =
                    backgroundSize;
        }

        ColorChanger backgroundColorChanger =
                background.AddComponent<ColorChanger>();

        backgroundColorChanger.colors =
                backgroundColor;

        backgroundColorChanger.spatialGradient =
                buttonGradients;

        backgroundColorChanger.verticalGradient =
                verticalButtonGradients;

        GameObject keyboardCanvas = new("Canvas");

        keyboardCanvas.transform.SetParent(
                searchKeyboard.transform,
                false);

        Canvas canvas =
                keyboardCanvas.AddComponent<Canvas>();

        CanvasScaler scaler =
                keyboardCanvas.AddComponent<CanvasScaler>();

        canvas.renderMode =
                RenderMode.WorldSpace;

        scaler.dynamicPixelsPerUnit =
                2000f;

        CreateSearchKeyboardRow(
                keyboardCanvas,
                "QWERTYUIOP",
                0.085f);

        CreateSearchKeyboardRow(
                keyboardCanvas,
                "ASDFGHJKL",
                0.03f);

        CreateSearchKeyboardRow(
                keyboardCanvas,
                "ZXCVBNM",
                -0.025f);

        const float BottomZ =
                -0.092f;

        CreateSearchKey(
                keyboardCanvas,
                "SPACE",
                "SPACE",
                -0.185f,
                BottomZ,
                0.25f);

        CreateSearchKey(
                keyboardCanvas,
                "BACK",
                "BACK",
                0.008f,
                BottomZ,
                0.12f);

        CreateSearchKey(
                keyboardCanvas,
                "CLEAR",
                "CLEAR",
                0.131f,
                BottomZ,
                0.11f);

        CreateSearchKey(
                keyboardCanvas,
                "X",
                "CLOSE",
                0.249f,
                BottomZ,
                0.11f);

        ApplySearchLayout();
    }

    private static void CreateSearchKeyboardRow(
            GameObject keyboardCanvas,
            string     characters,
            float      verticalPosition)
    {
        const float KeyWidth = 0.055f;
        const float KeyGap   = 0.008f;

        float totalWidth =
                characters.Length       * KeyWidth +
                (characters.Length - 1) * KeyGap;

        float startPosition =
                -totalWidth / 2f +
                KeyWidth    / 2f;

        for (int i = 0; i < characters.Length; i++)
        {
            string character = characters[i].ToString();

            float horizontalPosition =
                    startPosition +
                    i * (KeyWidth + KeyGap);

            CreateSearchKey(
                    keyboardCanvas,
                    character,
                    character,
                    horizontalPosition,
                    verticalPosition,
                    KeyWidth);
        }
    }

    private static void CreateSearchKey(
            GameObject keyboardCanvas,
            string     label,
            string     value,
            float      horizontalPosition,
            float      verticalPosition,
            float      width)
    {
        const float KeyDepth  = 0.023f;
        const float KeyHeight = 0.045f;

        float localHorizontalPosition =
                -horizontalPosition;

        GameObject key =
                GameObject.CreatePrimitive(
                        PrimitiveType.Cube);

        key.layer = 2;

        Destroy(key.GetComponent<Rigidbody>());

        key.transform.SetParent(
                searchKeyboard.transform,
                false);

        key.transform.localPosition = new Vector3(
                0.025f,
                localHorizontalPosition,
                verticalPosition);

        Vector3 keySize = new(
                KeyDepth,
                width,
                KeyHeight);

        BoxCollider collider =
                key.GetComponent<BoxCollider>();

        collider.isTrigger = true;

        SearchKeyButton keyButton =
                key.AddComponent<SearchKeyButton>();

        keyButton.value = value;

        if (roundedButtons)
        {
            RoundedButtonMesh.Apply(
                    key,
                    keySize,
                    0.01f,
                    buttonCornerSegments);
        }
        else
            key.transform.localScale = keySize;

        ColorChanger colorChanger =
                key.AddComponent<ColorChanger>();

        colorChanger.colors           = buttonColors[0];
        colorChanger.spatialGradient  = buttonGradients;
        colorChanger.verticalGradient = verticalButtonGradients;

        Text text = new GameObject
        {
                transform =
                {
                        parent = keyboardCanvas.transform,
                },
        }.AddComponent<Text>();

        text.font                 = currentFont;
        text.text                 = label;
        text.fontSize             = 1;
        text.color                = textColors[0];
        text.alignment            = TextAnchor.MiddleCenter;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize    = 0;

        RectTransform component =
                text.GetComponent<RectTransform>();

        component.localPosition = new Vector3(
                0.04f,
                localHorizontalPosition,
                verticalPosition);

        component.sizeDelta = new Vector2(
                width * 0.85f,
                0.034f);

        // Local: the keyboard gets its own world rotation from the search
        // layout, so the letters have to ride it instead of facing world-zero.
        component.localRotation =
                Quaternion.Euler(
                        180f,
                        90f,
                        90f);
    }

    // One cell of the two column grid: cells fill left to right, top to
    // bottom. Incremental rows break out of their cell and span the whole
    // grid width so "- value +" keeps room for the value.
    public static void CreateButton(
            int        cell,
            ButtonInfo method)
    {
        // One button per line: the cell index is the row, and every plate
        // spans the full width of the list.
        int row = cell;

        float rowZ =
                RowCenterZ(row);

        float rowWorldZ =
                rowZ *
                MenuScale.z;

        if (method.mode == ButtonMode.Incremental &&
            incrementalButtons)
        {
            // Split row: the value keeps the middle, the - and + pads sit
            // flush to the ends of the same full width plate.
            const float SideWidth   = 0.15f;
            const float SideOffset  = 0.34f;
            const float CenterWidth = 0.50f;

            float rowY = GridCenterY;

            GameObject incrementMinus =
                    CreateButtonObject(
                            method,
                            new Vector3(
                                    buttonSize.x,
                                    SideWidth,
                                    GridRowHeight),
                            new Vector3(
                                    0.56f,
                                    rowY + SideOffset,
                                    rowZ),
                            true,
                            true,
                            false);

            GameObject incrementCenter =
                    CreateButtonObject(
                            method,
                            new Vector3(
                                    buttonSize.x,
                                    CenterWidth,
                                    GridRowHeight),
                            new Vector3(
                                    0.56f,
                                    rowY,
                                    rowZ),
                            false,
                            false,
                            false);

            GameObject incrementPlus =
                    CreateButtonObject(
                            method,
                            new Vector3(
                                    buttonSize.x,
                                    SideWidth,
                                    GridRowHeight),
                            new Vector3(
                                    0.56f,
                                    rowY - SideOffset,
                                    rowZ),
                            true,
                            true,
                            true);

            CreateButtonText(
                    "-",
                    (rowY + SideOffset) * MenuScale.y,
                    rowWorldZ,
                    0.04f,
                    Color.white);

            Text valueText =
                    CreateButtonText(
                            method.GetDisplayText(),
                            rowY * MenuScale.y,
                            rowWorldZ,
                            0.13f,
                            Color.white);

            CreateButtonText(
                    "+",
                    (rowY - SideOffset) * MenuScale.y,
                    rowWorldZ,
                    0.04f,
                    Color.white);

            RegisterRow(
                    method,
                    valueText,
                    null,
                    null,
                    incrementMinus,
                    incrementCenter,
                    incrementPlus);

            return;
        }

        GameObject rowObject =
                CreateButtonObject(
                        method,
                        new Vector3(
                                buttonSize.x,
                                RowWidth,
                                GridRowHeight),
                        new Vector3(
                                0.56f,
                                GridCenterY,
                                rowZ),
                        true,
                        false,
                        false);

        // Screenshot style row: the title hugs the left edge and the chevron
        // the right one, with toggles carrying an ON / OFF word between them.
        // MiddleLeft text starts at its rect's left edge, so the rect centre
        // sits half a width in from where the title should begin.
        Text rowTitle =
                CreateRowText(
                        RowTitleText(method),
                        new Vector2(
                                0.18f,
                                0.014f),
                        TextAnchor.MiddleLeft,
                        FontStyle.Bold,
                        Color.white,
                        rowWorldZ,
                        RowTitleY * MenuScale.y);

        Text rowState = null;

        if (method.mode == ButtonMode.Toggle)
        {
            // Same trick mirrored: MiddleRight ends at the rect's right edge,
            // so the centre sits half a width in from StateEdgeY.
            rowState =
                    CreateRowText(
                            method.enabled
                                    ? "ON"
                                    : "OFF",
                            new Vector2(
                                    0.03f,
                                    0.011f),
                            TextAnchor.MiddleRight,
                            FontStyle.Bold,
                            method.enabled
                                    ? new Color32(
                                            199,
                                            155,
                                            255,
                                            255)
                                    : new Color32(
                                            140,
                                            140,
                                            140,
                                            255),
                            rowWorldZ,
                            (StateEdgeY + 0.05f) * MenuScale.y);
        }

        if (method.mode != ButtonMode.Incremental)
        {
            CreateRowText(
                    ">",
                    new Vector2(
                            0.03f,
                            0.013f),
                    TextAnchor.MiddleCenter,
                    FontStyle.Bold,
                    new Color32(
                            205,
                            205,
                            215,
                            255),
                    rowWorldZ,
                    ChevronY * MenuScale.y);
        }

        RegisterRow(
                method,
                rowTitle,
                null,
                rowState,
                rowObject);
    }

    // Remembers the pieces of a row so a plain toggle can update them in place.
    private static void RegisterRow(
            ButtonInfo target,
            Text       title,
            Text       subtitle,
            Text       state,
            params GameObject[] rowObjects)
    {
        if (target.mode == ButtonMode.Action)
            return;

        LiveRow row = new()
        {
                ownerMenu = menu,
                mode      = target.mode,
                title     = title,
                subtitle  = subtitle,
                state     = state,
        };

        foreach (GameObject rowObject in rowObjects)
        {
            if (rowObject == null)
                continue;

            ColorChanger changer =
                    rowObject.GetComponent<ColorChanger>();

            if (changer != null)
                row.changers.Add(changer);
        }

        liveRows[target] = row;
    }

    // Updates a single row after a button press. Returns false when the button
    // needs a real rebuild (layout buttons, nav buttons, missing row, ...).
    private static bool TryRefreshRow(ButtonInfo target, int buildBefore)
    {
        // The pressed button already rebuilt the menu itself.
        if (menuBuildId != buildBefore)
            return true;

        if (menu == null ||
            target.mode == ButtonMode.Action ||
            IsLayoutButton(target))
            return false;

        if (!liveRows.TryGetValue(target, out LiveRow row) ||
            row == null                                    ||
            row.ownerMenu != menu)
            return false;

        row.Refresh(target);

        return true;
    }

    // Settings buttons change how the menu itself is built (size, page size, theme,
    // rounded corners, outlines, ...), so they always need a full rebuild.
    private static HashSet<ButtonInfo> layoutButtons;

    private static bool IsLayoutButton(ButtonInfo target)
    {
        layoutButtons ??= BuildLayoutButtons();

        return layoutButtons.Contains(target);
    }

    private static HashSet<ButtonInfo> BuildLayoutButtons()
    {
        HashSet<ButtonInfo> buttons = new();

        foreach (ButtonCategory category in Categories)
        {
            if (category.name != "Settings" &&
                category.name != "Menu Settings")
                continue;

            foreach (ButtonInfo button in category.buttons)
                buttons.Add(button);
        }

        return buttons;
    }

    // Repaints every live row, used when a hidden menu is shown again after mods
    // changed state while it was closed.
    private static void RefreshAllRows()
    {
        foreach (KeyValuePair<ButtonInfo, LiveRow> pair in liveRows)
        {
            if (pair.Value == null || pair.Value.ownerMenu != menu)
                continue;

            try
            {
                pair.Value.Refresh(pair.Key);
            }
            catch (Exception exc)
            {
                Debug.LogError(
                        $"{Constants.Name} // Error refreshing row {pair.Key.buttonText}: {exc}");
            }
        }
    }

    // Screenshot style row line: plain white title, with the star only when
    // the row is a favorite. Value rows (incremental) stay a plain value.
    private static string RowTitleText(ButtonInfo target)
    {
        if (target.mode == ButtonMode.Incremental)
            return target.GetDisplayText();

        string titleValue =
                target.GetDisplayText();

        if (target.favorite)
            titleValue =
                    "<color=#C79BFF>♥</color> " +
                    titleValue;

        return titleValue;
    }

    // Short one line subtitle shown under a row title (Qubit style list rows).
    private static string GetRowSubtitle(ButtonInfo method)
    {
        if (method.mode == ButtonMode.Toggle)
            return method.enabled
                           ? "On - tap to turn off"
                           : "Off - tap to turn on";

        if (method.mode == ButtonMode.Incremental)
            return "Tap - or + to adjust";

        string tip = method.toolTip ?? string.Empty;

        if (string.IsNullOrWhiteSpace(tip) ||
            tip.StartsWith("This button doesn't"))
            return "Tap to use this option";

        int sentenceEnd = tip.IndexOf('.');

        if (sentenceEnd > 0)
            tip = tip.Substring(0, sentenceEnd);

        tip = tip.Trim();

        if (tip.Length > 36)
        {
            int cut = tip.LastIndexOf(' ', 36);

            if (cut < 18)
                cut = 36;

            tip = tip.Substring(0, cut).TrimEnd() + "...";
        }

        return tip;
    }

    // One line of text placed at a fixed spot on a button row.
    // Colours for gradient text: the theme's bright accent fading to its text
    // colour, so the sweep matches the current colour scheme.
    private static void ConfigureGradient(GradientText gradientText)
    {
        MenuTheme theme = themes[themeIndex];

        gradientText.colorA = theme.enabledButtonSecondary;
        gradientText.colorB = theme.enabledText;
    }

    private static Text CreateRowText(
            string    value,
            Vector2   size,
            TextAnchor alignment,
            FontStyle style,
            Color     color,
            float     worldZ,
            float     localY = 0.028f,
            bool      gradient = false)
    {
        GameObject textObject =
                new()
                {
                        transform =
                        {
                                parent = canvasObject.transform,
                        },
                };

        Text text =
                gradient
                        ? textObject.AddComponent<GradientText>()
                        : textObject.AddComponent<Text>();

        if (text is GradientText gradientText)
            ConfigureGradient(gradientText);

        text.font                 = currentFont;
        text.text                 = value;
        text.fontSize             = 1;
        text.color                = color;
        text.fontStyle            = style;
        text.alignment            = alignment;
        text.supportRichText      = true;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize    = 0;

        RectTransform component =
                text.GetComponent<RectTransform>();

        // +Y is the left side of the menu, so MiddleLeft text starts inset from it.
        component.localPosition =
                new Vector3(
                        0.064f,
                        localY,
                        worldZ);

        component.sizeDelta =
                size;

        // Local rotation: rows and tabs are rebuilt while the menu is already
        // rotated onto the hand, so a world rotation turned them sideways.
        component.localRotation =
                Quaternion.Euler(
                        180f,
                        90f,
                        90f);

        Track(text);

        return text;
    }

    private static GameObject CreateButtonObject(
            ButtonInfo method,
            Vector3    size,
            Vector3    localPosition,
            bool       clickable,
            bool       incremental,
            bool       positive,
            bool       chrome = false)
    {
        GameObject buttonObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

        Track(buttonObject);

        // Buttons are ALWAYS Ignore Raycast. This used to be conditional on Q
        // being held at the moment the menu happened to be built, so the layer
        // depended on when you rebuilt rather than when you clicked - and the
        // desktop click ray used the default mask, which skips layer 2 anyway.
        // Between them, a Main / Mods button could be pressed and do nothing.
        buttonObject.layer = 2;

        Destroy(buttonObject.GetComponent<Rigidbody>());

        // Local, not world: this runs again on every page/category rebuild,
        // long after the menu has been rotated onto your hand. A world rotation
        // here is what made rebuilt rows come out turned 90 degrees while the
        // chrome built at startup still looked right.
        buttonObject.transform.parent        = menu.transform;
        buttonObject.transform.localRotation = Quaternion.identity;
        buttonObject.transform.localPosition = localPosition;

        BoxCollider collider = buttonObject.GetComponent<BoxCollider>();

        if (clickable)
        {
            collider.isTrigger = true;

            Button button = buttonObject.AddComponent<Button>();
            button.relatedButton = method;
            button.incremental   = incremental;
            button.positive      = positive;
        }
        else
            Destroy(collider);

        bool useRounded = method.useRounded ?? roundedButtons;

        if (useRounded)
            RoundedButtonMesh.Apply(buttonObject, size, buttonCornerRadius, buttonCornerSegments);
        else
            buttonObject.transform.localScale = size;

        if (chrome)
        {
            // Screenshot style rows: the plate keeps its collider so the row
            // can still be pressed, but it draws nothing - no fill, no
            // outline, no colour cycling.
            Renderer rowRenderer =
                    buttonObject.GetComponent<Renderer>();

            if (rowRenderer != null)
                rowRenderer.enabled = false;

            return buttonObject;
        }

        CreateOutline(
                menu.transform,
                localPosition,
                size,
                buttonCornerRadius,
                useRounded);

        ExtGradient colors = method.enabled
                                     ? method.enabledColor  ?? buttonColors[1]
                                     : method.disabledColor ?? buttonColors[0];

        ColorChanger colorChanger = buttonObject.AddComponent<ColorChanger>();
        colorChanger.colors           = colors;
        colorChanger.spatialGradient  = method.useGradient ?? buttonGradients;
        colorChanger.verticalGradient = verticalButtonGradients;

        return buttonObject;
    }

    private static Text CreateButtonText(
            string value,
            float  localY,
            float  worldZ,
            float  width,
            Color  color,
            bool   gradient = false)
    {
        GameObject textObject =
                new()
                {
                        transform =
                        {
                                parent = canvasObject.transform,
                        },
                };

        Text text =
                gradient
                        ? textObject.AddComponent<GradientText>()
                        : textObject.AddComponent<Text>();

        if (text is GradientText gradientText)
            ConfigureGradient(gradientText);

        text.font                 = currentFont;
        text.text                 = value;
        text.supportRichText      = true;
        text.fontSize             = 1;
        text.color                = color;
        text.alignment            = TextAnchor.MiddleCenter;
        text.fontStyle            = FontStyle.Italic;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize    = 0;

        RectTransform component =
                text.GetComponent<RectTransform>();

        component.localPosition =
                new Vector3(
                        0.064f,
                        localY,
                        worldZ);

        component.sizeDelta =
                new Vector2(
                        width,
                        0.03f);

        // Local: incremental +/- and value text rebuild with the row.
        component.localRotation =
                Quaternion.Euler(
                        180f,
                        90f,
                        90f);

        Track(text);

        return text;
    }

    private static void CreateOutline(
            Transform parent,
            Vector3   localPosition,
            Vector3   size,
            float     cornerRadius,
            bool      rounded)
    {
        if (!outlines)
            return;

        GameObject outline = GameObject.CreatePrimitive(PrimitiveType.Cube);

        Track(outline);

        outline.name  = "UI Outline";
        outline.layer = 2;

        Destroy(outline.GetComponent<Rigidbody>());
        Destroy(outline.GetComponent<BoxCollider>());

        outline.transform.SetParent(parent, false);
        outline.transform.localPosition = localPosition;
        outline.transform.localRotation = Quaternion.identity;

        Vector3 outlineSize = new(
                size.x * 0.8f,
                size.y + outlineThickness,
                size.z + outlineThickness);

        if (rounded)
        {
            RoundedButtonMesh.Apply(
                    outline,
                    outlineSize,
                    cornerRadius + outlineThickness / 2f,
                    buttonCornerSegments);
        }
        else
            outline.transform.localScale = outlineSize;

        ColorChanger colorChanger = outline.AddComponent<ColorChanger>();
        colorChanger.colors = outlineColor;
    }

    // Rebuilds only the tab bar + list (rows, scrollbar, page counters) instead of
    // the whole menu, so paging / switching category / switching tab is cheap.
    private static void RecreateList()
    {
        if (menu == null)
            return;

        DestroyTracked(listObjects, listTexts);
        DestroyTracked(tabObjects, tabTexts);

        // Those rows are gone, so their registrations have to go too.
        liveRows.Clear();
        menuBuildId++;

        CreateTrackedHeaderIcons();
        CreateList();

        // Counters and the search label live in the chrome, so update them by hand.
        if (pageCounterText != null)
            pageCounterText.text = PageCounterValue();

        if (searchLabel != null)
            searchLabel.text = searchButton.GetDisplayText();
    }

    public static void RecreateMenu()
    {
        if (menu == null)
            return;

        Transform previousParent =
                menu.transform.parent;

        Vector3 previousPosition =
                menu.transform.position;

        Quaternion previousRotation =
                menu.transform.rotation;

        Vector3 previousLocalPosition =
                menu.transform.localPosition;

        Quaternion previousLocalRotation =
                menu.transform.localRotation;

        Destroy(menu);

        menu =
                null;

        CreateMenu(false);

        // CreateMenu throws the menu away when a build fails, so there may be
        // nothing left to re-parent.
        if (menu == null)
            return;

        if (previousParent != null)
        {
            menu.transform.SetParent(
                    previousParent,
                    false);

            menu.transform.localPosition =
                    previousLocalPosition;

            menu.transform.localRotation =
                    previousLocalRotation;
        }
        else
        {
            menu.transform.position =
                    previousPosition;

            menu.transform.rotation =
                    previousRotation;
        }

        menu.transform.localScale =
                MenuScale;

        if (IsSearching &&
            !searchUsingDesktopKeyboard)
        {
            ApplySearchLayout();
        }
    }

    public static void RecenterMenu(
            bool isRightHanded,
            bool isKeyboardCondition)
    {
        if (menu == null)
            return;

        if (IsSearching &&
            !searchUsingDesktopKeyboard)
        {
            UpdateSearchLayout();

            return;
        }

        if (!isKeyboardCondition)
        {
            if (menu.transform.parent != null)
            {
                menu.transform.SetParent(
                        null,
                        true);
            }

            if (!isRightHanded)
            {
                menu.transform.position =
                        GorillaTagger.Instance
                                     .leftHandTransform
                                     .position;

                menu.transform.rotation =
                        GorillaTagger.Instance
                                     .leftHandTransform
                                     .rotation;
            }
            else
            {
                menu.transform.position =
                        GorillaTagger.Instance
                                     .rightHandTransform
                                     .position;

                Vector3 rotation =
                        GorillaTagger.Instance
                                     .rightHandTransform
                                     .rotation
                                     .eulerAngles;

                rotation +=
                        new Vector3(
                                0f,
                                0f,
                                180f);

                menu.transform.rotation =
                        Quaternion.Euler(
                                rotation);
            }

            return;
        }

        try
        {
            // A scene wide GameObject.Find scans every loaded object, so only run it
            // when the camera reference is missing (or was destroyed), not every frame.
            if (TPC == null && Time.unscaledTime >= nextCameraLookupTime)
            {
                nextCameraLookupTime =
                        Time.unscaledTime +
                        5f;

                TPC =
                        GameObject.Find(
                                           "Player Objects/Third Person Camera/Shoulder Camera")
                                  ?.GetComponent<Camera>();
            }
        }
        catch
        {
            // ignored
        }

        // Cached: a scene wide GameObject.Find every frame was pure waste.
        Transform virtualCamera =
                GetVirtualCamera();

        if (virtualCamera != null &&
            virtualCamera.gameObject.activeSelf)
            virtualCamera.gameObject.SetActive(false);

        if (TPC == null)
            return;

        TPC.transform.position =
                new Vector3(
                        -999f,
                        -999f,
                        -999f);

        TPC.transform.rotation =
                Quaternion.identity;

        GameObject bg =
                GameObject.CreatePrimitive(
                        PrimitiveType.Cube);

        bg.transform.localScale =
                new Vector3(
                        10f,
                        10f,
                        0.01f);

        bg.transform.position =
                TPC.transform.position +
                TPC.transform.forward;

        Color realColor =
                backgroundColor.GetCurrentColor();

        bg.GetComponent<Renderer>()
          .material
          .color =
                new Color32(
                        (byte)(realColor.r * 50),
                        (byte)(realColor.g * 50),
                        (byte)(realColor.b * 50),
                        255);

        Destroy(
                bg,
                0.05f);

        menu.transform.SetParent(
                TPC.transform,
                true);

        menu.transform.position =
                TPC.transform.position       +
                TPC.transform.forward * 0.5f +
                TPC.transform.up      * -0.02f;

        menu.transform.rotation =
                TPC.transform.rotation *
                Quaternion.Euler(
                        -90f,
                        90f,
                        0f);

        // The third person camera can exist in VR too, and there is not always a
        // mouse - a null Mouse.current used to throw here and abort the whole
        // menu tick for that frame.
        if (Mouse.current == null || !Mouse.current.leftButton.isPressed)
            return;

        Ray ray =
                TPC.ScreenPointToRay(
                        Mouse.current.position.ReadValue());

        // Menu buttons sit on Ignore Raycast (layer 2) on purpose, so hand and
        // gun rays can never press them by accident. Physics.Raycast's default
        // mask skips exactly that layer, so this ray hit nothing at all and the
        // click was silently dropped - which reads as "the Main and Mods buttons
        // are broken". This is the one raycast that IS meant to reach the menu:
        // take every layer, force triggers on, then keep the nearest button.
        RaycastHit[] mouseHits =
                Physics.RaycastAll(
 ray,
                        100f,
                        ~0,
                        QueryTriggerInteraction.Collide);

        Button collide = null;

        float closestDistance = float.MaxValue;

        foreach (RaycastHit candidate in mouseHits)
        {
            if (candidate.distance >= closestDistance)
                continue;

            Button found =
                    candidate.transform
 .gameObject
                             .GetComponent<Button>();

            if (found == null)
                continue;

            closestDistance = candidate.distance;
            collide         = found;
        }

        if (collide == null)
            return;

        SphereCollider simulatedCollider =
                rightButtonCollider != null
                        ? rightButtonCollider
                        : leftButtonCollider;

        if (simulatedCollider != null)
            collide?.OnTriggerEnter(simulatedCollider);
    }

    private static void UpdateMenuAnimation()
    {
        if (menu == null)
            return;

        Vector3 targetScale =
                menuClosing
                        ? Vector3.zero
                        : MenuScale;

        if (!animateMenu)
        {
            menu.transform.localScale =
                    targetScale;

            if (menuClosing)
                FinishCloseMenu();

            return;
        }

        float interpolation =
                1f -
                Mathf.Exp(
                        -menuAnimationSpeed *
                        Time.unscaledDeltaTime);

        menu.transform.localScale =
                Vector3.Lerp(
                        menu.transform.localScale,
                        targetScale,
                        interpolation);

        if (!menuClosing)
            return;

        if (menu.transform.localScale.sqrMagnitude >
            0.00001f)
            return;

        FinishCloseMenu();
    }

    private static void BeginCloseMenu(bool shouldDrop)
    {
        if (menu == null ||
            menuClosing)
            return;

        menuClosing =
                true;

        menuShouldDropOnClose =
                shouldDrop;

        if (searchRoot != null &&
            menu.transform.parent ==
            searchRoot.transform)
        {
            menu.transform.SetParent(
                    null,
                    true);
        }

        if (shouldDrop)
        {
            Rigidbody rigidbody =
                    menu.GetComponent<Rigidbody>();

            if (rigidbody == null)
                rigidbody = menu.AddComponent<Rigidbody>();

            rigidbody.linearVelocity =
                    (rightHanded
                             ? GTPlayer.Instance.RightHand.velocityTracker
                             : GTPlayer.Instance.LeftHand.velocityTracker)
                   .GetAverageVelocity(
                            true,
                            0);
        }

        if (!animateMenu)
            FinishCloseMenu();
    }

    private static void CancelMenuClose()
    {
        if (menu == null)
            return;

        menuClosing =
                false;

        menuShouldDropOnClose =
                false;

        Rigidbody rigidbody =
                menu.GetComponent<Rigidbody>();

        if (rigidbody != null)
            Destroy(rigidbody);
    }

    private static void FinishCloseMenu()
    {
        if (menu == null)
            return;

        GameObject oldMenu = menu;

        menu        = null;
        menuClosing = false;
        bool dropped = menuShouldDropOnClose;
        menuShouldDropOnClose = false;

        DestroyReferences();

        // Cached "CM vcam1" lookup instead of a scene wide GameObject.Find.
        Transform virtualCamera =
                GetVirtualCamera(true);

        if (virtualCamera != null &&
            !virtualCamera.gameObject.activeSelf)
            virtualCamera.gameObject.SetActive(true);

        // The menu is hidden and reused on the next open, so flush any pending
        // preference write now instead of leaving it dangling.
        Preferences.FlushDirty(true);

        if (dropped && !animateMenu)
        {
            // Still visibly falling for a couple of seconds, so let it finish.
            Destroy(oldMenu, 2f);

            return;
        }

        StashMenu(oldMenu);
    }

    // Keeps the closed menu alive (hidden) so reopening it is instant.
    private static void StashMenu(GameObject oldMenu)
    {
        if (stashedMenu != null &&
            stashedMenu != oldMenu)
            Destroy(stashedMenu);

        Rigidbody rigidbody =
                oldMenu.GetComponent<Rigidbody>();

        if (rigidbody != null)
            Destroy(rigidbody);

        oldMenu.transform.SetParent(null, true);
        oldMenu.transform.localScale = MenuScale;
        oldMenu.SetActive(false);

        stashedMenu = oldMenu;
    }

    // Shows a stashed menu again and repaints it, since mods may have changed
    // state while it was hidden.
    private static void OpenStashedMenu()
    {
        menu        = stashedMenu;
        stashedMenu = null;

        menuClosing           = false;
        menuShouldDropOnClose = false;

        menu.SetActive(true);

        menu.transform.localScale =
                animateMenu
                        ? MenuScale * 0.01f
                        : MenuScale;

        RefreshAllRows();

        // Make sure the ping readout repaints straight away on reopen.
        nextPingUpdateTime = 0f;
    }

    private static void CreateReferences()
    {
        if (leftReference == null)
        {
            leftReference = CreateReference(
                    GorillaTagger.Instance.leftHandTransform,
                    out leftButtonCollider);
        }

        if (rightReference == null)
        {
            rightReference = CreateReference(
                    GorillaTagger.Instance.rightHandTransform,
                    out rightButtonCollider);
        }

        bool searchKeyboardOpen =
                IsSearching                 &&
                !searchUsingDesktopKeyboard &&
                searchKeyboard != null;

        bool leftActive =
                searchKeyboardOpen ||
                rightHanded;

        bool rightActive =
                searchKeyboardOpen ||
                !rightHanded;

        leftReference.SetActive(
                leftActive);

        rightReference.SetActive(
                rightActive);
    }

    private static GameObject CreateReference(
            Transform          hand,
            out SphereCollider collider)
    {
        GameObject handReference =
                GameObject.CreatePrimitive(
                        PrimitiveType.Sphere);

        handReference.name =
                $"{Constants.Name}_ButtonReference";

        handReference.transform.SetParent(
                hand,
                false);

        handReference.transform.localPosition =
                new Vector3(
                        0f,
                        -0.1f,
                        0f);

        handReference.transform.localRotation =
                Quaternion.identity;

        handReference.transform.localScale =
                new Vector3(
                        0.01f,
                        0.01f,
                        0.01f);

        collider =
                handReference.GetComponent<SphereCollider>();

        ColorChanger colorChanger =
                handReference.AddComponent<ColorChanger>();

        colorChanger.colors =
                backgroundColor;

        colorChanger.spatialGradient =
                false;

        return handReference;
    }

    private static void DestroyReferences()
    {
        if (leftReference != null)
            Destroy(leftReference);

        if (rightReference != null)
            Destroy(rightReference);

        leftReference  = null;
        rightReference = null;

        leftButtonCollider  = null;
        rightButtonCollider = null;
    }

    public static bool TryGetReferenceHand(
            Collider collider,
            out bool rightHand)
    {
        if (collider != null &&
            collider == rightButtonCollider)
        {
            rightHand = true;

            return true;
        }

        if (collider != null &&
            collider == leftButtonCollider)
        {
            rightHand = false;

            return true;
        }

        rightHand = false;

        return false;
    }

    // This is an overload, it allows you to do two different methods of toggling a mod, either by name, or by a ButtonInfo.
    public static void Toggle(string buttonText, bool? incrementDirection = null)
    {
        ButtonInfo button = GetIndex(buttonText);

        if (button == null)
        {
            Debug.LogError($"{Constants.Name} // Button {buttonText} does not exist.");

            return;
        }

        Toggle(button, incrementDirection);
    }

    public static void Toggle(ButtonInfo target, bool? incrementDirection = null)
    {
        // Which build of the menu did we start on? If the pressed button rebuilds
        // the menu by itself we must not rebuild it a second time.
        int buildAtStart = menuBuildId;

        // Double tap a mod to star it for the FAVORITES tab. Incremental rows
        // are deliberately excluded: "- value +" is meant to be tapped over and
        // over, and the press cooldown (0.2 s) is shorter than this window
        // (0.6 s), so the second tap of a quick pair always landed inside it and
        // starred the row instead of changing the value.
        if (target.mode != ButtonMode.Action &&
            target.mode != ButtonMode.Incremental)
        {
            bool doubleTap =
                    Time.time - target.lastPressTime <= 0.6f;

            target.lastPressTime = Time.time;

            if (doubleTap)
            {
                target.favorite = !target.favorite;

                NotifiLib.SendNotification(
                        "<color=grey>[</color><color=#C79BFF>STAR</color><color=grey>]</color> " +
                        (target.favorite
                                 ? "Added "   + target.GetDisplayText() + " to your favorites."
                                 : "Removed " + target.GetDisplayText() + " from your favorites."));

                Preferences.MarkDirty();

                // Only the star beside the title changes, unless we are looking at the
                // FAVORITES list itself, where the row has to vanish.
                if (activeTab != 1 && TryRefreshRow(target, buildAtStart))
                    return;

                RecreateMenu();

                return;
            }
        }

        switch (target.mode)
        {
            case ButtonMode.Toggle:
            {
                target.enabled = !target.enabled;

                if (target.enabled)
                {
                    NotifiLib.SendNotification(
                            "<color=grey>[</color><color=green>ENABLE</color><color=grey>]</color> " +
                            target.toolTip);

                    if (target.enableMethod != null)
                    {
                        try
                        {
                            target.enableMethod.Invoke();
                        }
                        catch (Exception exc)
                        {
                            Debug.LogError($"{Constants.Name} // Error enabling {target.buttonText}: {exc}");
                        }
                    }
                }
                else
                {
                    NotifiLib.SendNotification(
                            "<color=grey>[</color><color=red>DISABLE</color><color=grey>]</color> " +
                            target.toolTip);

                    if (target.disableMethod != null)
                    {
                        try
                        {
                            target.disableMethod.Invoke();
                        }
                        catch (Exception exc)
                        {
                            Debug.LogError($"{Constants.Name} // Error disabling {target.buttonText}: {exc}");
                        }
                    }
                }

                break;
            }

            case ButtonMode.Action:
            {
                if (target.method != null)
                {
                    try
                    {
                        target.method.Invoke();
                    }
                    catch (Exception exc)
                    {
                        Debug.LogError($"{Constants.Name} // Error invoking {target.buttonText}: {exc}");
                    }
                }

                break;
            }

            case ButtonMode.Incremental:
            {
                bool positive = incrementDirection ?? true;

                if (target.incrementMethod != null)
                {
                    try
                    {
                        target.incrementMethod.Invoke(positive);
                    }
                    catch (Exception exc)
                    {
                        Debug.LogError($"{Constants.Name} // Error changing {target.buttonText}: {exc}");
                    }
                }

                break;
            }
        }

        Preferences.MarkDirty();

        // The button may have rebuilt the menu by itself while it ran.
        if (menuBuildId != buildAtStart)
            return;

        // Page, tab, category and search jumps only change the list: rebuilding the
        // header, footer and canvas as well was wasted work.
        if (target.mode == ButtonMode.Action &&
            !IsLayoutButton(target))
        {
            RecreateList();

            return;
        }

        // Update just this row when we can. Destroying and rebuilding the whole
        // menu on every press was the stutter you felt in game.
        if (!TryRefreshRow(target, buildAtStart))
            RecreateMenu();
    }

    private static void ToggleSearch()
    {
        if (IsSearching)
        {
            CloseSearch(false);

            return;
        }

        IsSearching =
                true;

        searchQuery =
                string.Empty;

        pageNumber =
                0;

        searchUsingDesktopKeyboard =
                UnityInput.Current.GetKey(keyboardButton) ||
                menu != null &&
                TPC  != null &&
                menu.transform.parent ==
                TPC.transform;

        RefreshSearchResults();

        if (!searchUsingDesktopKeyboard)
        {
            EnsureSearchRoot();
            CreateSearchKeyboard();

            UpdateSearchLayout(true);
        }
    }

    private static void CloseSearch(bool recreate)
    {
        IsSearching =
                false;

        searchQuery =
                string.Empty;

        searchResults =
                Array.Empty<ButtonInfo>();

        if (menu       != null &&
            searchRoot != null &&
            menu.transform.parent ==
            searchRoot.transform)
        {
            menu.transform.SetParent(
                    null,
                    true);
        }

        if (searchKeyboard != null)
        {
            Destroy(searchKeyboard);

            searchKeyboard =
                    null;
        }

        if (searchRoot != null)
        {
            Destroy(searchRoot);

            searchRoot =
                    null;
        }

        searchKeyboardFollowing =
                false;

        searchUsingDesktopKeyboard =
                false;

        pageNumber =
                0;

        if (recreate)
            RecreateList();
    }

    private static void EnsureSearchRoot()
    {
        if (searchRoot != null)
            return;

        searchRoot =
                new GameObject(
                        $"{Constants.Name}_SearchRoot");
    }

    private static void UpdateSearchLayout(
            bool snap = false)
    {
        if (searchUsingDesktopKeyboard)
            return;

        EnsureSearchRoot();

        Camera headCamera =
                Camera.main;

        if (headCamera == null)
            return;

        Transform head =
                headCamera.transform;

        Vector3 forward =
                Vector3.ProjectOnPlane(
                        head.forward,
                        Vector3.up);

        if (forward.sqrMagnitude < 0.001f)
            forward = head.forward;

        forward.Normalize();

        Vector3 targetPosition =
                head.position;

        Quaternion targetRotation =
                Quaternion.LookRotation(
                        forward,
                        Vector3.up);

        if (snap)
        {
            searchRoot.transform.position =
                    targetPosition;

            searchRoot.transform.rotation =
                    targetRotation;
        }
        else
        {
            float interpolation =
                    1f -
                    Mathf.Exp(
                            -searchKeyboardFollowSpeed *
                            Time.unscaledDeltaTime);

            searchRoot.transform.position =
                    Vector3.Lerp(
                            searchRoot.transform.position,
                            targetPosition,
                            interpolation);

            searchRoot.transform.rotation =
                    Quaternion.Slerp(
                            searchRoot.transform.rotation,
                            targetRotation,
                            interpolation);
        }

        ApplySearchLayout();
    }

    private static void ApplySearchLayout()
    {
        if (searchRoot == null)
            return;

        if (menu != null)
        {
            if (menu.transform.parent !=
                searchRoot.transform)
            {
                menu.transform.SetParent(
                        searchRoot.transform,
                        false);
            }

            menu.transform.localPosition =
                    new Vector3(
                            0f,
                            0.02f,
                            0.70f);

            menu.transform.rotation =
                    searchRoot.transform.rotation *
                    Quaternion.Euler(
                            -90f,
                            90f,
                            0f);
        }

        if (searchKeyboard == null)
            return;

        if (searchKeyboard.transform.parent !=
            searchRoot.transform)
        {
            searchKeyboard.transform.SetParent(
                    searchRoot.transform,
                    false);
        }

        searchKeyboard.transform.localPosition =
                new Vector3(
                        0f,
                        -0.4f,
                        0.56f);

        Vector3 keyboardUp =
                (Vector3.up +
                 searchRoot.transform.forward)
               .normalized;

        searchKeyboard.transform.rotation =
                Quaternion.LookRotation(
                        keyboardUp,
                        -searchRoot.transform.right);
    }

    private static void HandleDesktopSearchInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        foreach (Key keyCode in from key in keyboard.allKeys where key is { wasPressedThisFrame: true, } select key.keyCode)
        {
                switch (keyCode)
                {
                        case Key.Backspace:
                        {
                                HandleSearchKey("BACK");

                                return;
                        }

                        case Key.Space:
                        {
                                HandleSearchKey("SPACE");

                                return;
                        }

                        case Key.Delete:
                        {
                                HandleSearchKey("CLEAR");

                                return;
                        }

                        case Key.Escape:
                        {
                                HandleSearchKey("CLOSE");

                                return;
                        }
                }

                if (keyCode >= Key.A &&
                    keyCode <= Key.Z)
                {
                        HandleSearchKey(
                                        keyCode.ToString());

                        return;
                }

                if (keyCode >= Key.Digit1 &&
                    keyCode <= Key.Digit9)
                {
                        string keyName =
                                        keyCode.ToString();

                        HandleSearchKey(
                                        keyName.Substring(
                                                        keyName.Length - 1));

                        return;
                }

                if (keyCode == Key.Digit0)
                {
                        HandleSearchKey("0");

                        return;
                }
        }
    }

    private static void RefreshSearchResults()
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            searchResults = [];

            return;
        }

        // Walk the categories instead of the flat AllButtons array so admin
        // rows stay hidden in search exactly as SetCategory hides them -
        // otherwise typing "kick" or "eagle" surfaced buttons a non-admin
        // cannot use. The entry buttons on Main carry a category's name, so
        // those are dropped too: searching "console" must not hand a
        // non-admin a row that only leads to the refusal toast.
        bool isAdmin = HamburburData.LocalAdminAccess;

        searchResults = (from category in Categories where isAdmin || !category.adminOnly from button in category.buttons where isAdmin || !OpensAdminCategory(button) let displayText = button.GetDisplayText() let nameMatch = displayText.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0 let toolTipMatch = !string.IsNullOrEmpty(button.toolTip) && button.toolTip.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0 where nameMatch || toolTipMatch select button).ToArray();
    }

    public static void HandleSearchKey(string value)
    {
        if (!IsSearching)
            return;

        switch (value)
        {
            case "BACK":
            {
                if (searchQuery.Length > 0)
                    searchQuery = searchQuery.Substring(0, searchQuery.Length - 1);

                break;
            }

            case "SPACE":
            {
                if (searchQuery.Length < 32)
                    searchQuery += " ";

                break;
            }

            case "CLEAR":
            {
                searchQuery = string.Empty;

                break;
            }

            case "CLOSE":
            {
                CloseSearch(true);

                return;
            }

            default:
            {
                if (searchQuery.Length < 32)
                    searchQuery += value;

                break;
            }
        }

        pageNumber = 0;

        RefreshSearchResults();

        // Only the results change while typing - a full menu rebuild per keystroke
        // was by far the most expensive thing the search bar did.
        RecreateList();
    }

    public static Vector3 RandomVector3(float range = 1f) =>
            new(Random.Range(-range,     range),
                    Random.Range(-range, range),
                    Random.Range(-range, range));

    public static Quaternion RandomQuaternion(float range = 360f) =>
            Quaternion.Euler(Random.Range(0f, range),
                    Random.Range(0f,          range),
                    Random.Range(0f,          range));

    public static Color RandomColor(byte range = 255, byte alpha = 255) =>
            new Color32((byte)Random.Range(0, range),
                    (byte)Random.Range(0,     range),
                    (byte)Random.Range(0,     range),
                    alpha);

    public static (Vector3 position, Quaternion rotation, Vector3 up, Vector3 forward, Vector3 right) TrueLeftHand()
    {
        Quaternion rot = GorillaTagger.Instance.leftHandTransform.rotation * GTPlayer.Instance.LeftHand.handRotOffset;

        return (GorillaTagger.Instance.leftHandTransform.position + GorillaTagger.Instance.leftHandTransform.rotation * GTPlayer.Instance.LeftHand.handOffset, rot, rot * Vector3.up, rot * Vector3.forward, rot * Vector3.right);
    }

    public static (Vector3 position, Quaternion rotation, Vector3 up, Vector3 forward, Vector3 right) TrueRightHand()
    {
        Quaternion rot = GorillaTagger.Instance.rightHandTransform.rotation * GTPlayer.Instance.RightHand.handRotOffset;

        return (GorillaTagger.Instance.rightHandTransform.position + GorillaTagger.Instance.rightHandTransform.rotation * GTPlayer.Instance.RightHand.handOffset, rot, rot * Vector3.up, rot * Vector3.forward, rot * Vector3.right);
    }

    public static void WorldScale(GameObject obj, Vector3 targetWorldScale)
    {
        Vector3 parentScale = obj.transform.parent.lossyScale;
        obj.transform.localScale = new Vector3(
                targetWorldScale.x / parentScale.x,
                targetWorldScale.y / parentScale.y,
                targetWorldScale.z / parentScale.z
        );
    }

    public static void FixStickyColliders(GameObject platform)
    {
        Vector3[] localPositions =
        [
                        new(0, 1f, 0),
                new(0, -1f, 0),
                new(1f, 0, 0),
                new(-1f, 0, 0),
                new(0, 0, 1f),
                new(0, 0, -1f),
        ];

        Quaternion[] localRotations =
        [
                        Quaternion.Euler(90,  0,   0),
                Quaternion.Euler(-90, 0,   0),
                Quaternion.Euler(0,   -90, 0),
                Quaternion.Euler(0,   90,  0),
                Quaternion.identity,
                Quaternion.Euler(0, 180, 0),
        ];

        for (int i = 0; i < localPositions.Length; i++)
        {
            GameObject side = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                if (platform.GetComponent<GorillaSurfaceOverride>() != null)
                {
                    side.AddComponent<GorillaSurfaceOverride>().overrideIndex = platform.GetComponent<GorillaSurfaceOverride>().overrideIndex;
                }
            }
            catch { }

            const float Size = 0.025f;
            side.transform.SetParent(platform.transform);
            side.transform.position = localPositions[i] * (Size / 2);
            side.transform.rotation = localRotations[i];
            WorldScale(side, new Vector3(Size, Size, 0.01f));
            side.GetComponent<Renderer>().enabled = false;
        }
    }

    public static (RaycastHit Ray, GameObject NewPointer) RenderGun(
            int?        overrideLayerMask = null,
            GunSettings settings          = null)
    {
        GunSettings currentSettings = settings ?? gunSettings;

        // Gemstone-style gun: hold a grip to bring the gun up in that hand.
        bool rightGrab = ControllerInputPoller.instance.rightGrab;
        bool leftGrab  = ControllerInputPoller.instance.leftGrab;
        bool holding   = rightGrab || leftGrab;

        if (rightGrab && !leftGrab)
            currentSettings.hand = XRNode.RightHand;
        else if (leftGrab && !rightGrab)
            currentSettings.hand = XRNode.LeftHand;

        if (!holding)
        {
            if (GunPointer != null)
                GunPointer.SetActive(false);

            if (GunLine != null)
                GunLine.gameObject.SetActive(false);

            gunWasHolding = false;

            return (default, GunPointer);
        }

        Transform gunTransform = currentSettings.hand == XRNode.LeftHand
                                         ? GorillaTagger.Instance.leftHandTransform
                                         : GorillaTagger.Instance.rightHandTransform;

        Vector3 startPosition = gunTransform.position;
        Vector3 direction     = gunTransform.forward;

        Vector3 rayOrigin = startPosition + direction / 4f;

        bool didHit;

        RaycastHit ray;

        if (overrideLayerMask.HasValue)
        {
                didHit = Physics.Raycast(
                                rayOrigin,
                                direction,
                                out ray,
                                currentSettings.maxDistance,
                                overrideLayerMask.Value,
                                QueryTriggerInteraction.Ignore);
        }
        else
        {
                didHit = GunRaycast(
                                rayOrigin,
                                direction,
                                currentSettings.maxDistance,
                                out ray);
        }

        Vector3 endPosition;

        // The aim line and pointer never stop on a player: when the target ray
        // lands on someone, re-shoot past them so the visuals rest on the world
        // behind instead of sticking to people. The ray returned below still
        // contains players so the admin guns can pick their target.
        VRRig hitRig =
                didHit && ray.collider != null
                        ? ray.collider.GetComponentInParent<VRRig>()
                        : null;

        bool hitPlayer = hitRig != null && hitRig != VRRig.LocalRig;

        // The dot has to stay visible: past a player with nothing to land on it
        // stops a couple of metres beyond them, and open air stops at a range
        // you can still see rather than the full 512 metre throw, which left
        // the ball stranded kilometres out with the line.
        if (hitPlayer && !overrideLayerMask.HasValue)
                endPosition =
                        GunRaycast(rayOrigin, direction, currentSettings.maxDistance,
                                out RaycastHit worldRay, true)
                                ? worldRay.point
                                : ray.point + direction * PlayerOvershoot;
        else if (didHit)
                endPosition = ray.point;
        else
                endPosition = startPosition + direction *
                        Mathf.Min(currentSettings.maxDistance, OpenAirDistance);

        // Smooth the pointer toward the aim point (Gemstone gun feel).
        if (!gunWasHolding || currentSettings.pointerSmoothness <= 0.001f)
        {
            smoothedGunPosition = endPosition;
            gunSmoothVelocity   = Vector3.zero;
        }
        else
        {
            smoothedGunPosition = Vector3.SmoothDamp(
                    smoothedGunPosition,
                    endPosition,
                    ref gunSmoothVelocity,
                    currentSettings.pointerSmoothness);
        }

        gunWasHolding = true;
        endPosition   = smoothedGunPosition;

        if (GunPointer == null)
        {
            GunPointer = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            Destroy(GunPointer.GetComponent<Collider>());

            Renderer pointerRenderer = GunPointer.GetComponent<Renderer>();
            pointerRenderer.material = new Material(Shader.Find("GUI/Text Shader"));
        }

        GunPointer.SetActive(true);
        GunPointer.transform.position = endPosition;

        // A fixed ball shrinks to a speck once the aim point is tens of metres
        // out, so past a modest distance it grows to keep the same apparent
        // size on screen - the dot is always findable.
        float pointerDistance = Vector3.Distance(startPosition, endPosition);

        GunPointer.transform.localScale =
                Vector3.one *
                currentSettings.pointerSize *
                Mathf.Max(1f, pointerDistance / 12f);

        Renderer currentPointerRenderer = GunPointer.GetComponent<Renderer>();
        currentPointerRenderer.enabled = currentSettings.showPointer;

        bool pressed =
                holding;

        ExtGradient pointerColors = pressed
                                            ? currentSettings.pointerActiveColor ?? buttonColors[1]
                                            : currentSettings.pointerIdleColor   ?? buttonColors[0];

        currentPointerRenderer.material.color = pointerColors.GetCurrentColor();

        if (GunLine == null)
        {
            GameObject lineObject = new($"{Constants.Name}_GunLine");

            GunLine                   = lineObject.AddComponent<LineRenderer>();
            GunLine.useWorldSpace     = true;
            GunLine.numCapVertices    = 4;
            GunLine.numCornerVertices = 4;
            GunLine.material          = new Material(Shader.Find("GUI/Text Shader"));
        }

        GunLine.gameObject.SetActive(true);

        ExtGradient lineColors = currentSettings.lineColor ?? backgroundColor;

        GunLine.startColor = lineColors.GetCurrentColor();
        GunLine.endColor   = lineColors.GetCurrentColor(0.5f);

        GunLine.startWidth = currentSettings.lineWidth;
        GunLine.endWidth   = currentSettings.lineWidth;

        RenderGunLine(
                startPosition,
                endPosition,
                gunTransform,
                currentSettings);

        return (ray, GunPointer);
    }
    
    private static bool GunRaycast(
                    Vector3        origin,
                    Vector3        direction,
                    float          maxDistance,
                    out RaycastHit hit,
                    bool           skipPlayers = false)
    {
            Ray ray = new(origin, direction);

            RaycastHit[] hits = Physics.RaycastAll(
                            ray,
                            maxDistance,
                            ~0,
                            QueryTriggerInteraction.Ignore);

            hit = default(RaycastHit);

            float closestDistance = float.MaxValue;

            foreach (RaycastHit candidate in hits)
            {
                    Collider collider = candidate.collider;

                    if (collider == null)
                    {
                            continue;
                    }

                    int layerMask = 1 << collider.gameObject.layer;

                    bool isInteractable =
                                    (layerMask & GTPlayer.Instance.locomotionEnabledLayers) != 0;

                    VRRig rig = collider.GetComponentInParent<VRRig>();

                    bool isRig =
                                    rig != null &&
                                    rig != VRRig.LocalRig;

                    // The aim visuals call this with skipPlayers so their line
                    // and pointer pass people by instead of resting on them.
                    if (skipPlayers && isRig)
                    {
                            continue;
                    }

                    if (!isInteractable && !isRig)
                    {
                            continue;
                    }

                    if (candidate.distance >= closestDistance)
                    {
                            continue;
                    }

                    closestDistance = candidate.distance;
                    hit             = candidate;
            }

            return hit.collider != null;
    }

    private static void RenderGunLine(
            Vector3     startPosition,
            Vector3     endPosition,
            Transform   gunTransform,
            GunSettings settings)
    {
        if (settings.lineStyle == GunLineStyle.Straight)
        {
            GunLine.positionCount = 2;

            GunLine.SetPosition(0, startPosition);
            GunLine.SetPosition(1, endPosition);

            return;
        }

        int quality = Mathf.Clamp(settings.lineQuality, 4, 64);

        GunLine.positionCount = quality;

        if (settings.lineStyle == GunLineStyle.Curve)
        {
            Vector3 controlPoint =
                    Vector3.Lerp(startPosition, endPosition, 0.5f) +
                    gunTransform.up * settings.curveAmount;

            for (int i = 0; i < quality; i++)
            {
                float t        = i / (float)(quality - 1);
                float inverseT = 1f - t;

                Vector3 position =
                        inverseT * inverseT * startPosition    +
                        2f       * inverseT * t * controlPoint +
                        t        * t        * endPosition;

                GunLine.SetPosition(i, position);
            }

            return;
        }

        Vector3 lineDirection = endPosition - startPosition;

        Vector3 waveAxis = Vector3.Cross(
                lineDirection.normalized,
                gunTransform.up);

        if (waveAxis.sqrMagnitude < 0.001f)
            waveAxis = gunTransform.right;

        waveAxis.Normalize();

        for (int i = 0; i < quality; i++)
        {
            float t = i / (float)(quality - 1);

            Vector3 position = Vector3.Lerp(
                    startPosition,
                    endPosition,
                    t);

            float edgeFade = Mathf.Sin(t * Mathf.PI);

            float wave =
                    Mathf.Sin(
                            t         * Mathf.PI * 2f * settings.waveFrequency +
                            Time.time * 8f) *
                    settings.waveAmount     *
                    edgeFade;

            position += waveAxis * wave;

            GunLine.SetPosition(i, position);
        }
    }
}