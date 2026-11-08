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

    public static bool  gunLocked;
    public static VRRig lockTarget;

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
    public static Text   fpsObject;

    // Smoothed FPS so the VR menu counter stops jumping every single frame
    private static float fpsSmoothed = 60f;

    // Header card text elements (greeting, menu name, stats, clock and date)
    private static Text greetingText;
    private static Text riftixText;
    private static Text stayedTimeText;
    private static Text playerNumberText;
    private static Text clockText;
    private static Text dateText;
    private static Text pageCounterText;
    private static Text pagerCounterText;

    // Throttle so the header strings are only rebuilt about once per second
    private static float nextHeaderUpdateTime;

    // Writing fpsObject.text every single frame forces a canvas rebuild every
    // frame, so the counter is only rewritten when the number changes anyway.
    private static int   lastFpsValue = -1;
    private static float nextFpsUpdateTime;

    // Throttle for the "menu build failed" log - a deterministic build failure
    // would otherwise print once per frame.
    private static float nextBuildErrorLogTime;

    // Shared chrome materials + shader lookups. Shader.Find is a linear search
    // through every shader in the game, so these are created once and reused
    // instead of on every menu rebuild.
    private static Shader   spritesShader;
    private static Material chromeCardMaterial;
    private static Material scrollTrackMaterial;

    // Camera lookups (desktop / keyboard mode). GameObject.Find is expensive and
    // used to run every frame, so the results are cached here.
    private static Transform cachedVirtualCamera;
    private static float     nextCameraLookupTime;
    private static float     nextVcamLookupTime;

    // Room session tracking for the "Stayed Time" stat
    private static Photon.Realtime.Room trackedRoom;
    private static float                roomJoinRealtime;

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

    private static readonly ButtonInfo homeTabButton = new()
    {
            buttonText  = "HomeTab",
            displayText = () => "HOME",
            method      = () => SetTab(0),
            mode        = ButtonMode.Action,
            toolTip     = "Shows every mod in this category.",
    };

    private static readonly ButtonInfo favoritesTabButton = new()
    {
            buttonText  = "FavoritesTab",
            displayText = () => "FAVORITES",
            method      = () => SetTab(1),
            mode        = ButtonMode.Action,
            toolTip     = "Shows only the mods you starred.",
    };

    private static readonly ButtonInfo homeBarButton = new()
    {
            buttonText  = "HomeBar",
            displayText = () => "Home",
            method      = GoHome,
            mode        = ButtonMode.Action,
            toolTip     = "Jumps back to the first page of the main list.",
    };

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

            if (mode == ButtonMode.Incremental)
            {
                // Value rows show the plain value and never take the star prefix.
                title.text  = target.GetDisplayText();
                title.color = textColors[0];
            }
            else
            {
                string titleValue =
                        target.GetDisplayText();

                if (target.favorite)
                    titleValue =
                            "<color=#C79BFF>♥</color> " +
                            titleValue;

                title.text  = titleValue;
                title.color = target.enabled
                                      ? textColors[1]
                                      : textColors[0];
            }

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
                if (keepOpen)
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
                if (menuClosing)
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
            if (fpsObject != null && Time.unscaledTime >= nextFpsUpdateTime)
            {
                nextFpsUpdateTime =
                        Time.unscaledTime +
                        0.5f;

                float rawFps = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

                fpsSmoothed = Mathf.Lerp(fpsSmoothed, rawFps, Time.unscaledDeltaTime * 3f);

                int roundedFps =
                        Mathf.RoundToInt(fpsSmoothed);

                if (roundedFps != lastFpsValue)
                {
                    lastFpsValue = roundedFps;

                    fpsObject.text =
                            "FPS: " +
                            roundedFps;
                }
            }

            if (Time.unscaledTime >= nextHeaderUpdateTime)
            {
                nextHeaderUpdateTime =
                        Time.unscaledTime +
                        1f;

                UpdateHeaderInfo();
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

        CreateOutline(
                menu.transform,
                menuBackground.transform.localPosition,
                menuSize,
                menuCornerRadius,
                roundedButtons);

        // Cool detail: thin accent frame ringing the whole panel.
        Vector3 panelFrameSize = new(
                0.096f,
                menuSize.y + 0.05f,
                menuSize.z + 0.05f);

        GameObject panelFrame =
                GameObject.CreatePrimitive(PrimitiveType.Cube);

        panelFrame.name  = $"{Constants.Name}_AccentFrame";
        panelFrame.layer = 2;

        Destroy(panelFrame.GetComponent<Rigidbody>());
        Destroy(panelFrame.GetComponent<BoxCollider>());

        panelFrame.transform.SetParent(menu.transform, false);
        panelFrame.transform.localRotation = Quaternion.identity;
        panelFrame.transform.localPosition  = new Vector3(0.498f, 0f, 0f);

        if (roundedButtons)
        {
            RoundedButtonMesh.Apply(
                    panelFrame,
                    panelFrameSize,
                    menuCornerRadius,
                    buttonCornerSegments);
        }
        else
            panelFrame.transform.localScale = panelFrameSize;

        ColorChanger panelFrameChanger =
                panelFrame.AddComponent<ColorChanger>();

        panelFrameChanger.colors = buttonColors[1];

        ColorChanger colorChanger =
                menuBackground.AddComponent<ColorChanger>();

        colorChanger.colors =
                backgroundColor;

        colorChanger.spatialGradient =
                buttonGradients;

        colorChanger.verticalGradient =
                verticalButtonGradients;

        CreateWatermark();

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

        // (The old floating title row was replaced by the HOME / FAVORITES tab bar.
        //  The nickname moved into the header greeting.)

        // FPS
        if (fpsCounter)
        {
            fpsObject = new GameObject
            {
                    transform =
                    {
                            parent = canvasObject.transform,
                    },
            }.AddComponent<Text>();

            fpsObject.font =
                    currentFont;

            fpsObject.text =
                    "FPS: " +
                    Mathf.Ceil(
                            1f /
                            Time.unscaledDeltaTime);

            // New text object, so force the next counter write.
            lastFpsValue = -1;

            fpsObject.color =
                    textColors[0];

            fpsObject.fontSize =
                    1;

            fpsObject.supportRichText =
                    true;

            fpsObject.fontStyle =
                    FontStyle.Italic;

            fpsObject.alignment =
                    TextAnchor.MiddleRight;

            fpsObject.horizontalOverflow =
                    HorizontalWrapMode.Overflow;

            fpsObject.resizeTextForBestFit =
                    true;

            fpsObject.resizeTextMinSize =
                    0;

            RectTransform fpsTransform =
                    fpsObject.GetComponent<RectTransform>();

            fpsTransform.localPosition =
                    Vector3.zero;

            fpsTransform.sizeDelta =
                    new Vector2(
                            0.12f,
                            0.0064f);

            // Top right of the header card, on the same line as the clock
            fpsTransform.position =
                    new Vector3(
                            0.064f,
                            -0.065f,
                            0.1357f);

            fpsTransform.rotation =
                    Quaternion.Euler(
                            180f,
                            90f,
                            90f);
        }

        // Header Card
        GameObject headerCard =
                GameObject.CreatePrimitive(
                        PrimitiveType.Cube);

        headerCard.name =
                $"{Constants.Name}_HeaderCard";

        headerCard.layer = 2;

        Destroy(headerCard.GetComponent<Rigidbody>());
        Destroy(headerCard.GetComponent<BoxCollider>());

        headerCard.transform.SetParent(
                menu.transform,
                false);

        headerCard.transform.localRotation =
                Quaternion.identity;

        headerCard.transform.localPosition =
                new Vector3(
                        0.535f,
                        0f,
                        0.4035f);

        Vector3 headerCardSize =
                new(
                        0.06f,
                        0.86f,
                        0.185f);

        if (roundedButtons)
        {
            RoundedButtonMesh.Apply(
                    headerCard,
                    headerCardSize,
                    0.03f,
                    buttonCornerSegments);
        }
        else
        {
            headerCard.transform.localScale =
                    headerCardSize;
        }

        // Dark translucent panel behind the header text
        headerCard.GetComponent<Renderer>().material =
                GetChromeCardMaterial();

        CreateOutline(
                menu.transform,
                headerCard.transform.localPosition,
                headerCardSize,
                0.03f,
                roundedButtons);

        // Cool detail: accent border hugging the header card.
        Vector3 headerFrameSize = new(
                0.056f,
                headerCardSize.y + 0.016f,
                headerCardSize.z + 0.016f);

        GameObject headerFrame =
                GameObject.CreatePrimitive(PrimitiveType.Cube);

        headerFrame.name  = $"{Constants.Name}_HeaderFrame";
        headerFrame.layer = 2;

        Destroy(headerFrame.GetComponent<Rigidbody>());
        Destroy(headerFrame.GetComponent<BoxCollider>());

        headerFrame.transform.SetParent(menu.transform, false);
        headerFrame.transform.localRotation = Quaternion.identity;
        headerFrame.transform.localPosition  =
                new Vector3(
                        0.533f,
                        0f,
                        0.4035f);

        if (roundedButtons)
        {
            RoundedButtonMesh.Apply(
                    headerFrame,
                    headerFrameSize,
                    0.035f,
                    buttonCornerSegments);
        }
        else
            headerFrame.transform.localScale = headerFrameSize;

        ColorChanger headerFrameChanger =
                headerFrame.AddComponent<ColorChanger>();

        headerFrameChanger.colors = buttonColors[1];

        // Greeting (small grey line picked by time of day, top left of the card)
        greetingText = CreateHeaderText(
                new Vector3(
                        0.064f,
                        0.065f,
                        0.183f),
                new Vector2(
                        0.12f,
                        0.0054f),
                TextAnchor.MiddleLeft,
                FontStyle.Italic,
                new Color32(
                        170,
                        170,
                        170,
                        255));

        // Big accent menu name (left aligned, Qubit style)
        riftixText = CreateHeaderText(
                new Vector3(
                        0.064f,
                        0.05f,
                        0.1688f),
                new Vector2(
                        0.15f,
                        0.017f),
                TextAnchor.MiddleLeft,
                FontStyle.Bold | FontStyle.Italic,
                new Color32(
                        199,
                        155,
                        255,
                        255));

        // Cool detail: soft violet glow behind the wordmark (Atlas style).
        UnityEngine.UI.Outline riftixGlow =
                riftixText.gameObject.AddComponent<UnityEngine.UI.Outline>();

        riftixGlow.effectColor     =
                new Color32(
                        124,
                        92,
                        255,
                        210);

        riftixGlow.effectDistance  =
                new Vector2(
                        0.0016f,
                        -0.0016f);

        riftixGlow.useGraphicAlpha = false;

        // Stat row labels (left side of the card)
        Text stayedLabel = CreateHeaderText(
                new Vector3(
                        0.064f,
                        0.065f,
                        0.1543f),
                new Vector2(
                        0.12f,
                        0.0064f),
                TextAnchor.MiddleLeft,
                FontStyle.Italic,
                new Color32(
                        150,
                        150,
                        150,
                        255));

        stayedLabel.text =
                "<color=#C79BFF>•</color> Stayed Time";

        Text playersLabel = CreateHeaderText(
                new Vector3(
                        0.064f,
                        0.065f,
                        0.1459f),
                new Vector2(
                        0.12f,
                        0.0064f),
                TextAnchor.MiddleLeft,
                FontStyle.Italic,
                new Color32(
                        150,
                        150,
                        150,
                        255));

        playersLabel.text =
                "<color=#C79BFF>•</color> Player Number";

        // Stat row values (right side of the card)
        stayedTimeText = CreateHeaderText(
                new Vector3(
                        0.064f,
                        -0.065f,
                        0.1543f),
                new Vector2(
                        0.12f,
                        0.0064f),
                TextAnchor.MiddleRight,
                FontStyle.Italic,
                textColors[0]);

        playerNumberText = CreateHeaderText(
                new Vector3(
                        0.064f,
                        -0.065f,
                        0.1459f),
                new Vector2(
                        0.12f,
                        0.0064f),
                TextAnchor.MiddleRight,
                FontStyle.Italic,
                textColors[0]);

        // Clock and date
        clockText = CreateHeaderText(
                new Vector3(
                        0.064f,
                        0f,
                        0.1357f),
                new Vector2(
                        0.2f,
                        0.0084f),
                TextAnchor.MiddleCenter,
                FontStyle.Italic,
                textColors[0]);

        dateText = CreateHeaderText(
                new Vector3(
                        0.064f,
                        0f,
                        0.1269f),
                new Vector2(
                        0.2f,
                        0.0056f),
                TextAnchor.MiddleCenter,
                FontStyle.Italic,
                new Color32(
                        170,
                        170,
                        170,
                        255));

        UpdateHeaderInfo();

        // Disconnect
        if (disconnectButton)
        {
            GameObject disconnectbutton =
                    GameObject.CreatePrimitive(
                            PrimitiveType.Cube);

            if (!UnityInput.Current.GetKey(keyboardButton))
                disconnectbutton.layer = 2;

            Destroy(
                    disconnectbutton.GetComponent<Rigidbody>());

            BoxCollider disconnectCollider =
                    disconnectbutton.GetComponent<BoxCollider>();

            disconnectCollider.isTrigger =
                    true;

            disconnectbutton.transform.SetParent(
                    menu.transform,
                    false);

            disconnectbutton.transform.localRotation =
                    Quaternion.identity;

            disconnectbutton.transform.localPosition =
                    new Vector3(
                            0.56f,
                            -0.29f,
                            0.4631f);

            // Small pill tucked into the top right of the header card
            Vector3 disconnectSize =
                    new(
                            0.09f,
                            0.26f,
                            0.045f);

            if (roundedButtons)
            {
                RoundedButtonMesh.Apply(
                        disconnectbutton,
                        disconnectSize,
                        buttonCornerRadius,
                        buttonCornerSegments);
            }
            else
            {
                disconnectbutton.transform.localScale =
                        disconnectSize;
            }

            Button disconnectPress =
                    disconnectbutton.AddComponent<Button>();

            disconnectPress.relatedButton =
                    GetIndex("Disconnect");

            CreateOutline(
                    menu.transform,
                    disconnectbutton.transform.localPosition,
                    disconnectSize,
                    buttonCornerRadius,
                    roundedButtons);

            colorChanger =
                    disconnectbutton.AddComponent<ColorChanger>();

            colorChanger.colors =
                    buttonColors[0];

            colorChanger.spatialGradient =
                    buttonGradients;

            colorChanger.verticalGradient =
                    verticalButtonGradients;

            Text disconnectText = new GameObject
            {
                    transform =
                    {
                            parent = canvasObject.transform,
                    },
            }.AddComponent<Text>();

            disconnectText.text =
                    "Disconnect";

            disconnectText.font =
                    currentFont;

            disconnectText.fontSize =
                    1;

            disconnectText.color =
                    textColors[0];

            disconnectText.alignment =
                    TextAnchor.MiddleCenter;

            disconnectText.resizeTextForBestFit =
                    true;

            disconnectText.resizeTextMinSize =
                    0;

            RectTransform disconnectTransform =
                    disconnectText.GetComponent<RectTransform>();

            disconnectTransform.localPosition =
                    new Vector3(
                            0.064f,
                            -0.087f,
                            0.17713f);

            disconnectTransform.sizeDelta =
                    new Vector2(
                            0.07f,
                            0.01f);

            disconnectTransform.rotation =
                    Quaternion.Euler(
                            180f,
                            90f,
                            90f);
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

        component.position =
                position;

        component.rotation =
                Quaternion.Euler(
                        180f,
                        90f,
                        90f);

        return text;
    }

    // Refreshes the greeting, clock, date, stayed time and player count strings.
    public static void UpdateHeaderInfo()
    {
        DateTime now = DateTime.Now;

        int hour = now.Hour;

        if (greetingText != null)
        {
            string playerName =
                    Photon.Pun.PhotonNetwork.LocalPlayer != null &&
                    !string.IsNullOrEmpty(Photon.Pun.PhotonNetwork.LocalPlayer.NickName)
                            ? Photon.Pun.PhotonNetwork.LocalPlayer.NickName
                            : "OMALLEYGHT";

            greetingText.text =
                    (hour >= 5  && hour <= 11 ? "Good morning~"   :
                     hour >= 12 && hour <= 17 ? "Good afternoon~" :
                     hour >= 18 && hour <= 22 ? "Good evening~"   :
                                                "Good night~")     +
                    "  <color=#7F7F7F>•</color>  "                 +
                    playerName                                     +
                    (HamburburData.LocalAdminAccess
                             ? " <color=grey>[</color><color=#C79BFF>Owner</color><color=grey>]</color>"
                             : "");
        }

        if (clockText != null)
        {
            clockText.text =
                    "- "                     +
                    now.ToString("hh:mm tt") +
                    " -";
        }

        if (dateText != null)
        {
            dateText.text =
                    now.ToString("yyyy/MM/dd") +
                    " - "                     +
                    now.ToString("ddd")       +
                    ".";
        }

        Photon.Realtime.Room room =
                Photon.Pun.PhotonNetwork.CurrentRoom;

        if (room != trackedRoom)
        {
            trackedRoom      = room;
            roomJoinRealtime = Time.realtimeSinceStartup;
        }

        string stayedValue = "0m";
        string playerValue = "1ppl";

        if (room != null)
        {
            float elapsed =
                    Time.realtimeSinceStartup -
                    roomJoinRealtime;

            int totalMinutes =
                    Mathf.Max(
                            0,
                            (int)(elapsed / 60f));

            stayedValue =
                    totalMinutes >= 60
                            ? $"{totalMinutes / 60}h {totalMinutes % 60:00}m"
                            : $"{totalMinutes}m";

            playerValue =
                    room.PlayerCount +
                    "ppl";
        }

        if (stayedTimeText != null)
            stayedTimeText.text = stayedValue;

        if (playerNumberText != null)
            playerNumberText.text = playerValue;
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

        if (activeTab != 1)
            return CurrentCategory.buttons;

        return CurrentCategory.buttons
                              .Where(button => button.favorite)
                              .ToArray();
    }

    private static int GetContentButtonsPerPage()
    {
        // The rebuilt layout fits exactly six rows between the search bar
        // and the bottom bar, so extra rows are capped here.
        int perPage = Mathf.Clamp(buttonsPerPage, 1, 6);

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
        // The tab bar travels with the list: its highlight depends on which tab is
        // open, so both are rebuilt together when the list changes.
        CreateTrackedTabBar();

        CreateSearchBar();
        CreateFooterBar();

        CreateList();
    }

    private static void CreateTrackedTabBar()
    {
        BeginTrack(tabObjects, tabTexts);

        try
        {
            CreateTabBar();
        }
        finally
        {
            EndTrack();
        }
    }

    // Rows, scrollbar and page counter state - everything a page / tab / category
    // change touches.
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
                CreateButton(0f,            previousPageButton);
                CreateButton(buttonSpacing, nextPageButton);

                rowOffset = 2;
            }

            // Previous / next live in the bottom bar only now, so the old
            // floating side arrows outside the panel are gone.

            int lastPage = GetLastPage();

            if (pageNumber > lastPage)
                pageNumber = lastPage;

            CreatePageScrollbar();

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
                        (i + rowOffset) * buttonSpacing,
                        visibleButtons[startIndex + i]);
            }
        }
        finally
        {
            EndTrack();
        }
    }

    private static string PageCounterValue() =>
            $"{pageNumber + 1:00} / {GetLastPage() + 1:00}";

    // Cool detail: slim page scrollbar pinned to the right edge of the list.
    private static void CreatePageScrollbar()
    {
        const float trackCenterZ = -0.1f;
        const float trackLength  = 0.58f;

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

    // HOME / FAVORITES tabs, right under the header card (Atlas + Qubit).
    private static void CreateTabBar()
    {
        Vector3 tabSize = new(
                buttonSize.x,
                0.42f,
                0.042f);

        float tabWorldZ =
                0.277f *
                MenuScale.z;

        homeTabButton.enabled      = activeTab == 0;
        favoritesTabButton.enabled = activeTab == 1;

        CreateButtonObject(
                homeTabButton,
                tabSize,
                new Vector3(
                        0.56f,
                        0.225f,
                        0.277f),
                true,
                false,
                false);

        CreateRowText(
                "HOME",
                new Vector2(
                        0.12f,
                        0.012f),
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                activeTab == 0
                        ? Color.white
                        : new Color32(
                                165,
                                165,
                                165,
                                255),
                tabWorldZ,
                0.0675f);

        CreateButtonObject(
                favoritesTabButton,
                tabSize,
                new Vector3(
                        0.56f,
                        -0.225f,
                        0.277f),
                true,
                false,
                false);

        CreateRowText(
                "♥ FAVORITES",
                new Vector2(
                        0.12f,
                        0.012f),
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                activeTab == 1
                        ? Color.white
                        : new Color32(
                                165,
                                165,
                                165,
                                255),
                tabWorldZ,
                -0.0675f);
    }

    // Search field with the page counter tucked on its right (Qubit style).
    private static void CreateSearchBar()
    {
        float searchWorldZ =
                0.2245f *
                MenuScale.z;

        CreateButtonObject(
                searchButton,
                new Vector3(
                        buttonSize.x,
                        0.86f,
                        0.043f),
                new Vector3(
                        0.56f,
                        0f,
                        0.2245f),
                true,
                false,
                false);

        searchLabel =
                CreateRowText(
                        searchButton.GetDisplayText(),
                        new Vector2(
                                0.12f,
                                0.011f),
                        TextAnchor.MiddleLeft,
                        FontStyle.Italic,
                        textColors[0],
                        searchWorldZ,
                        0.035f);

        // Cool detail: magnifier drawn from an O glyph + a tilted bar handle
        //  (Arial has no magnifier glyph, so we build one).
        CreateRowText(
                "○",
                new Vector2(
                        0.024f,
                        0.013f),
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                new Color32(
                        199,
                        155,
                        255,
                        255),
                searchWorldZ,
                0.112f);

        GameObject searchHandle =
                GameObject.CreatePrimitive(PrimitiveType.Cube);

        searchHandle.name  = $"{Constants.Name}_SearchHandle";
        searchHandle.layer = 2;

        Destroy(searchHandle.GetComponent<Rigidbody>());
        Destroy(searchHandle.GetComponent<BoxCollider>());

        searchHandle.transform.SetParent(menu.transform, false);

        searchHandle.transform.localPosition =
                new Vector3(
                        0.615f,
                        0.3427f,
                        0.2004f);

        // 45 degrees on screen requires 51.9 degrees here because the menu
        //  scale is non-uniform (0.3 across, 0.3825 down).
        searchHandle.transform.localRotation =
                Quaternion.Euler(
                        -51.9f,
                        0f,
                        0f);

        searchHandle.transform.localScale =
                new Vector3(
                        0.02f,
                        0.035f,
                        0.014f);

        ColorChanger handleChanger =
                searchHandle.AddComponent<ColorChanger>();

        handleChanger.colors = buttonColors[1];

        pageCounterText = CreateHeaderText(
                new Vector3(
                        0.064f,
                        -0.065f,
                        searchWorldZ),
                new Vector2(
                        0.12f,
                        0.009f),
                TextAnchor.MiddleRight,
                FontStyle.Bold,
                new Color32(
                        175,
                        175,
                        175,
                        255));

        pageCounterText.text =
                PageCounterValue();
    }

    // Bottom bar: home button + previous / page counter / next (Atlas + Qubit).
    private static void CreateFooterBar()
    {
        Vector3 footerSize = new(
                0.06f,
                0.9f,
                0.09f);

        Vector3 footerPosition = new(
                0.535f,
                0f,
                -0.451f);

        GameObject footerCard =
                GameObject.CreatePrimitive(
                        PrimitiveType.Cube);

        footerCard.name =
                $"{Constants.Name}_FooterCard";

        footerCard.layer = 2;

        Destroy(footerCard.GetComponent<Rigidbody>());
        Destroy(footerCard.GetComponent<BoxCollider>());

        footerCard.transform.SetParent(
                menu.transform,
                false);

        footerCard.transform.localRotation =
                Quaternion.identity;

        footerCard.transform.localPosition =
                footerPosition;

        if (roundedButtons)
        {
            RoundedButtonMesh.Apply(
                    footerCard,
                    footerSize,
                    0.03f,
                    buttonCornerSegments);
        }
        else
            footerCard.transform.localScale =
                    footerSize;

        footerCard.GetComponent<Renderer>().material =
                GetChromeCardMaterial();

        CreateOutline(
                menu.transform,
                footerPosition,
                footerSize,
                0.03f,
                roundedButtons);

        float footerWorldZ =
                -0.451f *
                MenuScale.z;

        Vector3 footerKeySize = new(
                buttonSize.x,
                0.16f,
                0.09f);

        CreateButtonObject(
                homeBarButton,
                footerKeySize,
                new Vector3(
                        0.56f,
                        0.36f,
                        -0.451f),
                true,
                false,
                false);

        CreateRowText(
                "⌂",
                new Vector2(
                        0.06f,
                        0.02f),
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                textColors[0],
                footerWorldZ,
                0.108f);

        CreateButtonObject(
                previousPageButton,
                new Vector3(
                        buttonSize.x,
                        0.16f,
                        0.07f),
                new Vector3(
                        0.56f,
                        0.19f,
                        -0.451f),
                true,
                false,
                false);

        CreateRowText(
                "‹",
                new Vector2(
                        0.06f,
                        0.02f),
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                textColors[0],
                footerWorldZ,
                0.057f);

        CreateButtonObject(
                nextPageButton,
                new Vector3(
                        buttonSize.x,
                        0.16f,
                        0.07f),
                new Vector3(
                        0.56f,
                        -0.19f,
                        -0.451f),
                true,
                false,
                false);

        CreateRowText(
                "›",
                new Vector2(
                        0.06f,
                        0.02f),
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                textColors[0],
                footerWorldZ,
                -0.057f);

        pagerCounterText = CreateHeaderText(
                new Vector3(
                        0.064f,
                        0f,
                        footerWorldZ),
                new Vector2(
                        0.1f,
                        0.014f),
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                new Color32(
                        205,
                        205,
                        205,
                        255));

        pagerCounterText.text =
                PageCounterValue();

        // Cool detail: small accent wordmark balancing the right end of the bar.
        CreateHeaderText(
                new Vector3(
                        0.064f,
                        -0.108f,
                        footerWorldZ),
                new Vector2(
                        0.06f,
                        0.012f),
                TextAnchor.MiddleCenter,
                FontStyle.Bold | FontStyle.Italic,
                new Color32(
                        199,
                        155,
                        255,
                        255)).text = "RIFTIX";
    }

    // Shown on the FAVORITES tab when nothing has been starred yet.
    private static void CreateFavoritesHint()
    {
        float worldZ =
                0.150f *
                MenuScale.z;

        ButtonInfo hint = new()
        {
                buttonText = "FavoritesHint",
                mode       = ButtonMode.Action,
                toolTip    = "No favorites yet.",
        };

        CreateButtonObject(
                hint,
                buttonSize,
                new Vector3(
                        0.56f,
                        0f,
                        0.150f),
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
                worldZ + 0.005f);

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
                worldZ - 0.0075f);
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

        component.rotation =
                Quaternion.Euler(
                        180f,
                        90f,
                        90f);
    }

    public static void CreateButton(
            float      offset,
            ButtonInfo method)
    {
        float zPosition =
                0.150f -
                offset;

        float rowWorldZ =
                zPosition *
                MenuScale.z;

        if (method.mode == ButtonMode.Incremental &&
            incrementalButtons)
        {
            const float CenterWidth     = 0.52f;
            const float IncrementWidth  = 0.17f;
            const float IncrementOffset = 0.365f;
            const float TextScale       = 0.3f;

            GameObject incrementMinus =
                    CreateButtonObject(
                            method,
                            new Vector3(
                                    buttonSize.x,
                                    IncrementWidth,
                                    buttonSize.z),
                            new Vector3(
                                    0.56f,
                                    IncrementOffset,
                                    zPosition),
                            true,
                            true,
                            false);

            GameObject incrementCenter =
                    CreateButtonObject(
                            method,
                            new Vector3(
                                    buttonSize.x,
                                    CenterWidth,
                                    buttonSize.z),
                            new Vector3(
                                    0.56f,
                                    0f,
                                    zPosition),
                            false,
                            false,
                            false);

            GameObject incrementPlus =
                    CreateButtonObject(
                            method,
                            new Vector3(
                                    buttonSize.x,
                                    IncrementWidth,
                                    buttonSize.z),
                            new Vector3(
                                    0.56f,
                                    -IncrementOffset,
                                    zPosition),
                            true,
                            true,
                            true);

            CreateButtonText(
                    "-",
                    IncrementOffset * TextScale,
                    offset,
                    0.04f,
                    textColors[0]);

            Text valueText =
                    CreateButtonText(
                            method.GetDisplayText(),
                            0f,
                            offset,
                            0.12f,
                            textColors[0],
                            true);

            CreateButtonText(
                    "+",
                    -IncrementOffset * TextScale,
                    offset,
                    0.04f,
                    textColors[0]);

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
                        buttonSize,
                        new Vector3(
                                0.56f,
                                0f,
                                zPosition),
                        true,
                        false,
                        false);

        // Qubit style row: bold title left, grey subtitle under it, chevron on the right.
        string titleValue =
                method.GetDisplayText();

        if (method.favorite)
            titleValue =
                    "<color=#C79BFF>♥</color> " +
                    titleValue;

        Text rowTitle =
                CreateRowText(
                        titleValue,
                        new Vector2(0.19f, 0.015f),
                        TextAnchor.MiddleLeft,
                        FontStyle.Bold,
                        method.enabled
                                ? textColors[1]
                                : textColors[0],
                        rowWorldZ + 0.005f,
                        gradient: true);

        Text rowSubtitle =
                CreateRowText(
                        GetRowSubtitle(method),
                        new Vector2(0.19f, 0.0085f),
                        TextAnchor.MiddleLeft,
                        FontStyle.Normal,
                        new Color32(
                                150,
                                150,
                                150,
                                255),
                        rowWorldZ - 0.0075f);

        Text rowState =
                null;

        if (method.mode == ButtonMode.Action)
        {
            rowState =
                    CreateRowText(
                            ">",
                            new Vector2(0.03f, 0.016f),
                            TextAnchor.MiddleRight,
                            FontStyle.Bold,
                            new Color32(
                                    199,
                                    155,
                                    255,
                                    255),
                            rowWorldZ + 0.001f,
                            -0.11f);
        }
        else
        {
            rowState =
                    CreateRowText(
                            method.enabled ? "ON" : "OFF",
                            new Vector2(0.045f, 0.0105f),
                            TextAnchor.MiddleRight,
                            FontStyle.Bold,
                            method.enabled
                                    ? new Color32(199, 155, 255, 255)
                                    : new Color32(140, 140, 140, 255),
                            rowWorldZ + 0.001f,
                            -0.1025f);
        }

        RegisterRow(
                method,
                rowTitle,
                rowSubtitle,
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

        component.rotation =
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
            bool       positive)
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

        buttonObject.transform.parent        = menu.transform;
        buttonObject.transform.rotation      = Quaternion.identity;
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
            float  offset,
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
                        (0.150f - offset) * MenuScale.z);

        component.sizeDelta =
                new Vector2(
                        width,
                        0.03f);

        component.rotation =
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

        CreateTrackedTabBar();
        CreateList();

        // Counters and the search label live in the chrome, so update them by hand.
        if (pageCounterText != null)
            pageCounterText.text = PageCounterValue();

        if (pagerCounterText != null)
            pagerCounterText.text = PageCounterValue();

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

        lastFpsValue = -1;

        UpdateHeaderInfo();
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

        ButtonInfo[] allButtons = AllButtons;

        searchResults = (from button in allButtons let displayText = button.GetDisplayText() let nameMatch = displayText.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0 let toolTipMatch = !string.IsNullOrEmpty(button.toolTip) && button.toolTip.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0 where nameMatch || toolTipMatch select button).ToArray();
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

        if (!holding && !gunLocked)
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

        if (gunLocked && lockTarget != null)
            endPosition = lockTarget.transform.position;
        else if (didHit)
            endPosition = ray.point;
        else
            endPosition = startPosition + direction * currentSettings.maxDistance;

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
        GunPointer.transform.position   = endPosition;
        GunPointer.transform.localScale = Vector3.one * currentSettings.pointerSize;

        Renderer currentPointerRenderer = GunPointer.GetComponent<Renderer>();
        currentPointerRenderer.enabled = currentSettings.showPointer;

        bool pressed =
                gunLocked ||
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
                    out RaycastHit hit)
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