using System;
using System.Collections.Generic;
using BepInEx;
using Photon.Pun;
using StupidTemplate.Classes;
using StupidTemplate.Tools;
using UnityEngine;
using UnityEngine.InputSystem;
using HamburburData = StupidTemplate.Classes.Console.HamburburData;
using Main = StupidTemplate.Menu.Main;

namespace StupidTemplate.Menu;

public class DesktopGui : MonoBehaviour
{
    private const float AnimSpeed = 12f;
    private const float WindowW   = 600f;
    private const float WindowH   = 660f;
    private const float HiddenX   = -(WindowW + 24f);
    private const float VisibleX  = 12f;

    private static readonly Color Accent   = new(0.616f, 0.306f, 0.867f);
    private static readonly Color TextMain = new(0.93f,  0.93f,  0.96f);
    private static readonly Color TextDim  = new(0.62f,  0.60f,  0.70f);

    private enum GuiView
    {
        Home,
        Category,
    }

    private float windowX = HiddenX;
    private float baseX   = VisibleX;
    private float slideT  = 0f;
    private int   currentGuiTab;
    private GuiView view = GuiView.Home;
    private bool  menuVisible = true;
    private bool  stylesReady;

    private Vector2 modScrollPosition;
    private string  searchQuery  = "";
    private int     lastDrawnTab = -1;
    private float   ignoreTooltipsUntil;

    private Rect windowRect = new(HiddenX, 24, WindowW, WindowH);

    private GUIStyle windowStyle;
    private GUIStyle buttonStyle;
    private GUIStyle toggleOnStyle;
    private GUIStyle toggleOffStyle;
    private GUIStyle cardStyle;
    private GUIStyle backStyle;
    private GUIStyle labelStyle;
    private GUIStyle dimLabelStyle;
    private GUIStyle chevronStyle;
    private GUIStyle sectionStyle;
    private GUIStyle searchFieldStyle;
    private GUIStyle tooltipStyle;
    private GUIStyle fpsGreenStyle;
    private GUIStyle fpsYellowStyle;
    private GUIStyle fpsRedStyle;

    private Texture2D roundedBackgroundTex;
    private Texture2D accentBarTex;
    private Texture2D buttonOffTex;
    private Texture2D buttonHoverTex;
    private Texture2D buttonOnTex;
    private Texture2D scrollbarThumbTex;

    private Texture2D logoTexture;

    private readonly Dictionary<int, Color> categoryStripeColors = [];

    private string cachedFps      = "0";
    private string cachedRoom     = "Offline";
    private string cachedRoomFull = "Offline";
    private string cachedPlayers  = "1";
    private float  fpsTimer;
    private int    fpsFrames;

    private readonly HashSet<string> loggedErrors = [];

    private void Awake()
    {
        try
        {
            logoTexture = StupidAssetUtils.LoadTexture2D("watermark.png");
        }
        catch (Exception e)
        {
            LogErrorOnce("logo-load", e);
        }

        currentGuiTab = FindFirstVisibleCategory();
    }

    private void Update()
    {
        try
        {
            if (UnityInput.Current.GetKeyDown(KeyCode.F11))
                menuVisible = !menuVisible;
        }
        catch (Exception e)
        {
            LogErrorOnce("f11", e);
        }

        UpdateAnimation();
        UpdateStats();
    }

    private void UpdateStats()
    {
        fpsFrames++;

        if (Time.unscaledTime - fpsTimer < 1f)
            return;

        cachedFps = fpsFrames.ToString();
        fpsFrames = 0;
        fpsTimer  = Time.unscaledTime;

        try
        {
            cachedRoomFull = PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null
                                          ? PhotonNetwork.CurrentRoom.Name
                                          : "Offline";

            cachedRoom = cachedRoomFull.Length > 22
                                     ? cachedRoomFull.Substring(0, 20) + ".."
                                     : cachedRoomFull;

            cachedPlayers = PhotonNetwork.InRoom && PhotonNetwork.PlayerList != null
                                        ? PhotonNetwork.PlayerList.Length.ToString()
                                        : "1";
        }
        catch (Exception e)
        {
            LogErrorOnce("stats", e);
        }
    }

    private void UpdateAnimation()
    {
        // absorb last frame's drag so the slide animation never snaps the window back
        if (slideT > 0.85f)
            baseX = windowRect.x;

        // auto-opens on startup; F11 slides it in/out at the position you left it
        slideT    = Mathf.Lerp(slideT, menuVisible ? 1f : 0f, Time.deltaTime * AnimSpeed);
        windowX   = Mathf.Lerp(HiddenX, baseX, slideT);
        windowRect.x = windowX;

        // keep the window on screen vertically no matter where it's dragged
        windowRect.y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, Screen.height - 80f));
    }

    private void OnGUI()
    {
        if (!menuVisible)
            return;

        try
        {
            InitializeStyles();
        }
        catch (Exception e)
        {
            LogErrorOnce("styles", e);
            return;
        }

        windowRect = GUI.Window(0, windowRect, DrawWindow, "", windowStyle);
    }

    // ---------- styles ----------

    private void InitializeStyles()
    {
        if (stylesReady)
            return;

        roundedBackgroundTex = GenerateRoundedBackground();
        accentBarTex         = CreateSolidColorTexture(Color.white);
        buttonOffTex         = CreateSolidColorTexture(new Color(0.14f, 0.13f, 0.20f, 1f));
        buttonHoverTex       = CreateSolidColorTexture(new Color(0.24f, 0.20f, 0.33f, 1f));
        buttonOnTex          = CreateSolidColorTexture(new Color(0.42f, 0.20f, 0.62f, 1f));
        scrollbarThumbTex    = CreateSolidColorTexture(new Color(0.42f, 0.26f, 0.60f, 1f));

        windowStyle = new GUIStyle(GUI.skin.window)
        {
            normal    = { textColor = Accent },
            onNormal  = { textColor = Accent },
            padding   = new RectOffset(14, 14, 12, 12),
            fontSize  = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            normal    = { background = buttonOffTex, textColor = TextMain },
            hover     = { background = buttonHoverTex, textColor = Color.white },
            active    = { background = buttonOnTex, textColor = Color.white },
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap  = true,
            fontSize  = 12,
            richText  = true,
        };

        toggleOnStyle = new GUIStyle(buttonStyle)
        {
            normal   = { background = buttonOnTex, textColor = Color.white },
            hover    = { background = buttonOnTex, textColor = Color.white },
            onNormal = { background = buttonOnTex, textColor = Color.white },
        };

        toggleOffStyle = new GUIStyle(buttonStyle)
        {
            normal = { background = buttonOffTex, textColor = new Color(TextMain.r, TextMain.g, TextMain.b, 0.8f) },
            hover  = { background = buttonHoverTex, textColor = Color.white },
        };

        cardStyle = new GUIStyle(buttonStyle)
        {
            alignment = TextAnchor.MiddleLeft,
            padding   = new RectOffset(16, 8, 6, 6),
            fontSize  = 13,
            wordWrap  = true,
        };

        backStyle = new GUIStyle(buttonStyle)
        {
            alignment = TextAnchor.MiddleLeft,
            padding   = new RectOffset(10, 6, 2, 2),
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            normal    = { textColor = TextMain },
            fontStyle = FontStyle.Bold,
            fontSize  = 12,
        };

        dimLabelStyle = new GUIStyle(labelStyle)
        {
            normal = { textColor = TextDim },
        };

        chevronStyle = new GUIStyle(labelStyle)
        {
            normal    = { textColor = new Color(TextDim.r, TextDim.g, TextDim.b, 0.7f) },
            alignment = TextAnchor.MiddleCenter,
            fontSize  = 15,
        };

        sectionStyle = new GUIStyle(labelStyle)
        {
            normal    = { textColor = Accent },
            fontSize  = 13,
            fontStyle = FontStyle.BoldAndItalic,
        };

        searchFieldStyle = new GUIStyle(GUI.skin.textField)
        {
            normal    = { background = buttonOffTex, textColor = TextMain },
            focused   = { background = buttonHoverTex, textColor = Color.white },
            hover     = { background = buttonHoverTex, textColor = TextMain },
            fontStyle = FontStyle.Bold,
            padding   = new RectOffset(8, 8, 4, 4),
            fontSize  = 12,
        };

        tooltipStyle = new GUIStyle(GUI.skin.box)
        {
            normal    = { background = buttonOffTex, textColor = TextMain },
            wordWrap  = true,
            fontStyle = FontStyle.Italic,
            fontSize  = 11,
            padding   = new RectOffset(8, 8, 6, 6),
        };

        fpsGreenStyle  = MakeColoredLabel(new Color(0.45f, 0.90f, 0.55f));
        fpsYellowStyle = MakeColoredLabel(new Color(0.95f, 0.80f, 0.35f));
        fpsRedStyle    = MakeColoredLabel(new Color(0.95f, 0.45f, 0.45f));

        GUI.skin.verticalScrollbar.normal.background      = buttonOffTex;
        GUI.skin.verticalScrollbarThumb.normal.background = scrollbarThumbTex;
        GUI.skin.verticalScrollbarThumb.hover.background  = scrollbarThumbTex;
        GUI.skin.verticalScrollbarThumb.active.background = scrollbarThumbTex;
        GUI.skin.verticalScrollbar.fixedWidth             = 10f;
        GUI.skin.verticalScrollbarThumb.fixedWidth        = 10f;

        stylesReady = true;
    }

    private GUIStyle MakeColoredLabel(Color color)
    {
        return new GUIStyle(labelStyle)
        {
            normal = { textColor = color },
        };
    }

    private static Texture2D CreateSolidColorTexture(Color color)
    {
        Texture2D texture = new(2, 2);
        texture.SetPixels([color, color, color, color]);
        texture.Apply();

        return texture;
    }

    private static Texture2D GenerateRoundedBackground()
    {
        const int   size   = 64;
        const float radius = 14f;
        const float border = 2.2f;

        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
        };

        Color fillTop    = new(0.11f, 0.09f, 0.17f, 0.97f);
        Color fillBottom = new(0.05f, 0.04f, 0.08f, 0.97f);
        Color borderCol  = new(0.48f, 0.32f, 0.68f, 0.85f);

        Color[] pixels = new Color[size * size];
        float   half   = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs(x + 0.5f - half);
                float py = Mathf.Abs(y + 0.5f - half);

                float dx = px - half + radius;
                float dy = py - half + radius;

                float outsideX = Mathf.Max(dx, 0f);
                float outsideY = Mathf.Max(dy, 0f);

                float dist = Mathf.Min(Mathf.Max(dx, dy), 0f) +
                             Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY) -
                             radius;

                Color c = Color.clear;

                if (dist < -border * 0.5f)
                {
                    c = Color.Lerp(fillBottom, fillTop, y / (float)(size - 1));
                    c.a = 0.97f;
                }
                else if (dist < border * 0.5f)
                {
                    c = borderCol;
                }
                else
                {
                    float fade = Mathf.Clamp01(-(dist - border * 0.5f));
                    c   = borderCol;
                    c.a = borderCol.a * fade;
                }

                pixels[y * size + x] = c;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return texture;
    }

    // ---------- chrome ----------

    private static readonly Rect TlCorner = new(0f,    0.75f, 0.25f, 0.25f);
    private static readonly Rect TrCorner = new(0.75f, 0.75f, 0.25f, 0.25f);
    private static readonly Rect BlCorner = new(0f,    0f,    0.25f, 0.25f);
    private static readonly Rect BrCorner = new(0.75f, 0f,    0.25f, 0.25f);
    private static readonly Rect TopEdge  = new(0.25f, 0.75f, 0.5f,  0.25f);
    private static readonly Rect BotEdge  = new(0.25f, 0f,    0.5f,  0.25f);
    private static readonly Rect LeftEdge = new(0f,    0.25f, 0.25f, 0.5f);
    private static readonly Rect RightEdge= new(0.75f, 0.25f, 0.25f, 0.5f);
    private static readonly Rect Center   = new(0.25f, 0.25f, 0.5f,  0.5f);

    private void DrawWindowBackground()
    {
        if (Event.current.type != EventType.Repaint || roundedBackgroundTex == null)
            return;

        float width  = windowRect.width;
        float height = windowRect.height;
        const float c = 18f;

        GUI.DrawTextureWithTexCoords(new Rect(0, 0, c, c),                   roundedBackgroundTex, TlCorner);
        GUI.DrawTextureWithTexCoords(new Rect(width - c, 0, c, c),           roundedBackgroundTex, TrCorner);
        GUI.DrawTextureWithTexCoords(new Rect(0, height - c, c, c),          roundedBackgroundTex, BlCorner);
        GUI.DrawTextureWithTexCoords(new Rect(width - c, height - c, c, c),  roundedBackgroundTex, BrCorner);

        GUI.DrawTextureWithTexCoords(new Rect(c, 0, width - c * 2f, c),              roundedBackgroundTex, TopEdge);
        GUI.DrawTextureWithTexCoords(new Rect(c, height - c, width - c * 2f, c),     roundedBackgroundTex, BotEdge);
        GUI.DrawTextureWithTexCoords(new Rect(0, c, c, height - c * 2f),             roundedBackgroundTex, LeftEdge);
        GUI.DrawTextureWithTexCoords(new Rect(width - c, c, c, height - c * 2f),     roundedBackgroundTex, RightEdge);

        GUI.DrawTextureWithTexCoords(new Rect(c, c, width - c * 2f, height - c * 2f), roundedBackgroundTex, Center);

        // drag handle pill at top center
        if (accentBarTex != null)
        {
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.28f);
            GUI.DrawTexture(new Rect(width * 0.5f - 24f, 9f, 48f, 5f), accentBarTex);
            GUI.color = previous;
        }
    }

    private void DrawAccentBar()
    {
        Rect barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(2.5f), GUILayout.ExpandWidth(true));

        if (Event.current.type != EventType.Repaint || accentBarTex == null)
            return;

        Color previous = GUI.color;
        GUI.color = Color.HSVToRGB(Mathf.Repeat(0.66f + Mathf.Sin(Time.time * 0.35f) * 0.12f, 1f), 0.55f, 1f);
        GUI.DrawTexture(barRect, accentBarTex);
        GUI.color = previous;
    }

    // ---------- main window ----------

    private void DrawWindow(int windowID)
    {
        try
        {
            DrawWindowBackground();

            GUILayout.Space(2);

            DrawHeader();

            DrawAccentBar();

            GUILayout.Space(8);

            bool isAdmin = HamburburData.IsLocalAdmin || HamburburData.IsLocalSuperAdmin;

            EnsureValidTab(isAdmin);

            DrawContentPane(isAdmin);

            GUILayout.Space(8);

            DrawFooter();

            DrawHoverTooltip();

            // whole window is draggable: empty areas start a drag, buttons/fields still work
            GUI.DragWindow(new Rect(0, 0, windowRect.width, windowRect.height));
        }
        catch (Exception e)
        {
            LogErrorOnce("window", e);
        }
    }

    private void DrawHeader()
    {
        GUILayout.BeginHorizontal(GUILayout.Height(64));

        if (logoTexture != null)
        {
            float aspect = logoTexture.width / (float)logoTexture.height;
            float height = 56f;
            float width  = height * aspect;

            Rect logoRect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));

            DrawLogoGlow(logoRect);
            GUI.DrawTexture(logoRect, logoTexture, ScaleMode.ScaleToFit, true);
        }
        else
        {
            GUIStyle big = MakeColoredLabel(Accent);
            big.fontSize  = 22;
            big.fontStyle = FontStyle.BoldAndItalic;
            GUILayout.Label(Constants.Name, big, GUILayout.Height(52));
        }

        GUILayout.Space(10);

        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

        GUIStyle nameStyle = MakeColoredLabel(Accent);
        nameStyle.fontSize  = 15;
        nameStyle.fontStyle = FontStyle.BoldAndItalic;

        GUILayout.Label(Constants.Name, nameStyle);

        GUILayout.BeginHorizontal();

        int fps = int.TryParse(cachedFps, out int f) ? f : 0;
        GUIStyle fpsStyle = fps >= 60 ? fpsGreenStyle : fps >= 30 ? fpsYellowStyle : fpsRedStyle;

        GUILayout.Label($"FPS {cachedFps}",         fpsStyle);
        GUILayout.Space(10);
        GUILayout.Label($"Players {cachedPlayers}", dimLabelStyle);
        GUILayout.Space(10);
        GUILayout.Label($"Room {cachedRoom}",       dimLabelStyle);

        if (GUILayout.Button("Copy", buttonStyle, GUILayout.Width(46), GUILayout.Height(19)))
            SafeCopyRoom();

        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }

    private void DrawLogoGlow(Rect logoRect)
    {
        if (logoTexture == null || Event.current.type != EventType.Repaint)
            return;

        float pad = logoRect.height * 0.22f;
        Rect glowRect = new(logoRect.x - pad, logoRect.y - pad, logoRect.width + pad * 2f, logoRect.height + pad * 2f);

        Color previous = GUI.color;
        GUI.color = new Color(0.75f, 0.55f, 1f, 0.35f);
        GUI.DrawTexture(glowRect, logoTexture, ScaleMode.ScaleToFit, true);
        GUI.color = previous;
    }

    // ---------- content ----------

    private void DrawContentPane(bool isAdmin)
    {
        if (!string.IsNullOrEmpty(searchQuery))
        {
            DrawSearchView(isAdmin);
            return;
        }

        if (view == GuiView.Category)
            DrawCategoryView(isAdmin);
        else
            DrawHomeView(isAdmin);
    }

    private void DrawHomeView(bool isAdmin)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("CATEGORIES", sectionStyle);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"v{Constants.Version}", dimLabelStyle);
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        modScrollPosition = GUILayout.BeginScrollView(modScrollPosition, GUILayout.ExpandHeight(true));

        float cardW = (WindowW - 28f - 10f) / 2f;
        const float cardH = 58f;

        int col = 0;
        GUILayout.BeginHorizontal();

        for (int i = 0; i < Buttons.Categories.Length; i++)
        {
            ButtonCategory category = Buttons.Categories[i];

            if (category.adminOnly && !isAdmin)
                continue;

            int visible = CountVisibleButtons(category, isAdmin);

            if (visible == 0)
                continue;

            if (col >= 2)
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                col = 0;
            }

            DrawCategoryCard(i, category, visible, cardW, cardH);
            col++;
        }

        for (; col < 2; col++)
            GUILayout.Space(cardW);

        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        int totalMods = 0;
        int totalOn   = 0;
        int catCount  = 0;

        for (int i = 0; i < Buttons.Categories.Length; i++)
        {
            ButtonCategory category = Buttons.Categories[i];

            if (category.adminOnly && !isAdmin)
                continue;

            int visible = CountVisibleButtons(category, isAdmin);

            if (visible == 0)
                continue;

            catCount++;
            totalMods += visible;

            foreach (ButtonInfo button in category.buttons)
                if (button.enabled)
                    totalOn++;
        }

        GUILayout.Label($"{catCount} categories  ·  {totalMods} mods  ·  <color=#C79BFF>{totalOn} active</color>", dimLabelStyle);

        GUILayout.EndScrollView();
    }

    private void DrawCategoryCard(int index, ButtonCategory category, int visibleCount, float width, float height)
    {
        Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));

        int onCount = 0;

        foreach (ButtonInfo button in category.buttons)
            if (button.enabled)
                onCount++;

        string subtitle = $"{visibleCount} mods";

        if (onCount > 0)
            subtitle += $"  ·  <color=#C79BFF>{onCount} on</color>";

        string text = $"<b>{category.name}</b>\n<size=10><color=#99AABB>{subtitle}</color></size>";

        bool clicked = GUI.Button(rect, new GUIContent(text), cardStyle);

        if (Event.current.type == EventType.Repaint && accentBarTex != null)
        {
            Color previous = GUI.color;
            GUI.color = GetStripeColor(index);
            GUI.DrawTexture(new Rect(rect.x, rect.y + 6f, 4f, rect.height - 12f), accentBarTex);
            GUI.color = previous;

            GUI.Label(new Rect(rect.xMax - 28f, rect.y, 20f, rect.height), ">", chevronStyle);
        }

        if (clicked)
        {
            currentGuiTab     = index;
            view              = GuiView.Category;
            modScrollPosition = Vector2.zero;
            lastDrawnTab      = -1;
            SuppressTooltipsBriefly();
        }
    }

    private void SuppressTooltipsBriefly()
    {
        ignoreTooltipsUntil = Time.unscaledTime + 0.3f;
    }

    private Color GetStripeColor(int index)
    {
        if (categoryStripeColors.TryGetValue(index, out Color cached))
            return cached;

        string name = Buttons.Categories[index].name;

        int hash = 0;

        foreach (char c in name)
            hash = unchecked(hash * 31 + c);

        float hue = Mathf.Repeat(Mathf.Abs(hash % 360) / 360f, 1f);

        Color color = Color.HSVToRGB(hue, 0.65f, 1f);
        categoryStripeColors[index] = color;

        return color;
    }

    private void DrawCategoryView(bool isAdmin)
    {
        ButtonCategory category = Buttons.Categories[currentGuiTab];

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("  < Back", backStyle, GUILayout.Width(80), GUILayout.Height(24)))
        {
            view = GuiView.Home;
            SuppressTooltipsBriefly();
        }

        GUILayout.Space(6);

        GUILayout.Label(category.name.ToUpperInvariant(), sectionStyle);

        GUILayout.FlexibleSpace();

        List<ButtonInfo> buttons = CollectCategoryButtons(category, isAdmin);

        GUILayout.Label($"{buttons.Count} mods", dimLabelStyle);

        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        if (currentGuiTab != lastDrawnTab)
        {
            modScrollPosition = Vector2.zero;
            lastDrawnTab      = currentGuiTab;
        }

        modScrollPosition = GUILayout.BeginScrollView(modScrollPosition, GUILayout.ExpandHeight(true));

        DrawButtonGrid(buttons);

        GUILayout.EndScrollView();
    }

    private void DrawSearchView(bool isAdmin)
    {
        List<ButtonInfo> buttons = CollectSearchButtons(isAdmin);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"SEARCH: {searchQuery.ToUpperInvariant()}", sectionStyle);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"{buttons.Count} results", dimLabelStyle);
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        modScrollPosition = GUILayout.BeginScrollView(modScrollPosition, GUILayout.ExpandHeight(true));

        DrawButtonGrid(buttons);

        GUILayout.EndScrollView();
    }

    private List<ButtonInfo> CollectCategoryButtons(ButtonCategory category, bool isAdmin)
    {
        List<ButtonInfo> buttons = [];

        foreach (ButtonInfo button in category.buttons)
        {
            if (IsCategoryNavButton(button))
                continue;

            if (!isAdmin && IsButtonInAdminCategory(button))
                continue;

            buttons.Add(button);
        }

        return buttons;
    }

    private List<ButtonInfo> CollectSearchButtons(bool isAdmin)
    {
        List<ButtonInfo> buttons = [];

        foreach (ButtonInfo button in Buttons.AllButtons)
        {
            if (IsCategoryNavButton(button))
                continue;

            if (!isAdmin && IsButtonInAdminCategory(button))
                continue;

            if (button.buttonText.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                buttons.Add(button);
        }

        return buttons;
    }

    private void DrawButtonGrid(List<ButtonInfo> buttons)
    {
        if (buttons.Count == 0)
        {
            GUILayout.Label("Nothing here", dimLabelStyle);

            return;
        }

        int i = 0;

        while (i < buttons.Count)
        {
            ButtonInfo first = buttons[i];

            if (first.mode == ButtonMode.Incremental)
            {
                DrawModButton(first);
                GUILayout.Space(3);
                i++;
                continue;
            }

            GUILayout.BeginHorizontal();

            DrawModButton(first);

            if (i + 1 < buttons.Count && buttons[i + 1].mode != ButtonMode.Incremental)
            {
                DrawModButton(buttons[i + 1]);
                i += 2;
            }
            else
            {
                GUILayout.FlexibleSpace();
                i++;
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(3);
        }
    }

    private void DrawModButton(ButtonInfo button)
    {
        switch (button.mode)
        {
            case ButtonMode.Toggle:
            {
                GUIContent content = new(button.GetDisplayText(), button.toolTip);
                bool       state    = GUILayout.Toggle(button.enabled, content, button.enabled ? toggleOnStyle : toggleOffStyle, GUILayout.Height(24), GUILayout.ExpandWidth(true));

                if (state != button.enabled)
                    SafeToggle(button);

                break;
            }

            case ButtonMode.Incremental:
            {
                GUILayout.BeginHorizontal();

                if (GUILayout.Button(new GUIContent("-", button.toolTip), buttonStyle, GUILayout.Width(30), GUILayout.Height(24)))
                    SafeToggle(button, false);

                GUILayout.Label(button.GetDisplayText(), labelStyle, GUILayout.Height(24));

                if (GUILayout.Button(new GUIContent("+", button.toolTip), buttonStyle, GUILayout.Width(30), GUILayout.Height(24)))
                    SafeToggle(button, true);

                GUILayout.EndHorizontal();

                break;
            }

            default:
            {
                if (GUILayout.Button(new GUIContent(button.GetDisplayText(), button.toolTip), buttonStyle, GUILayout.Height(24), GUILayout.ExpandWidth(true)))
                    SafeToggle(button);

                break;
            }
        }
    }

    private void DrawFooter()
    {
        GUILayout.BeginHorizontal();

        searchQuery = GUILayout.TextField(searchQuery, searchFieldStyle, GUILayout.Height(24));

        if (GUILayout.Button("X", buttonStyle, GUILayout.Width(30), GUILayout.Height(24)))
        {
            searchQuery = "";
            SuppressTooltipsBriefly();
        }

        GUILayout.Space(6);

        if (GUILayout.Button("Disconnect", buttonStyle, GUILayout.Width(90), GUILayout.Height(24)))
            SafeDisconnect();

        GUILayout.Space(4);

        if (GUILayout.Button("Quit", buttonStyle, GUILayout.Width(60), GUILayout.Height(24)))
            Application.Quit();

        GUILayout.EndHorizontal();

        GUILayout.Space(2);

        GUILayout.Label("F11 toggles this menu", dimLabelStyle);
    }

    private void DrawHoverTooltip()
    {
        if (Time.unscaledTime < ignoreTooltipsUntil)
            return;

        string tip = GUI.tooltip;

        if (string.IsNullOrEmpty(tip) || Event.current.type != EventType.Repaint)
            return;

        Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        mousePos.y = Screen.height - mousePos.y;

        float maxWidth = 280f;
        GUIContent content = new(tip);
        float height = tooltipStyle.CalcHeight(content, maxWidth - 16f);

        Rect tipRect = new(mousePos.x + 16f, mousePos.y + 16f, maxWidth, height + 12f);

        if (tipRect.xMax > Screen.width)
            tipRect.x = mousePos.x - tipRect.width - 8f;

        if (tipRect.yMax > Screen.height)
            tipRect.y = mousePos.y - tipRect.height - 8f;

        GUI.Box(tipRect, content, tooltipStyle);
    }

    // ---------- safe actions ----------

    private void SafeToggle(ButtonInfo button, bool? direction = null)
    {
        try
        {
            Main.Toggle(button, direction);
        }
        catch (Exception e)
        {
            LogErrorOnce("toggle-" + button.buttonText, e);
        }
    }

    private void SafeCopyRoom()
    {
        try
        {
            GUIUtility.systemCopyBuffer = cachedRoomFull;
        }
        catch (Exception e)
        {
            LogErrorOnce("copy", e);
        }
    }

    private void SafeDisconnect()
    {
        try
        {
            PhotonNetwork.Disconnect();
        }
        catch (Exception e)
        {
            LogErrorOnce("disconnect", e);
        }
    }

    // ---------- helpers ----------

    private static bool IsCategoryNavButton(ButtonInfo button)
    {
        if (button.buttonText == "Return to Main")
            return true;

        foreach (ButtonCategory category in Buttons.Categories)
            if (button.buttonText == category.name)
                return true;

        return false;
    }

    private static bool IsButtonInAdminCategory(ButtonInfo button)
    {
        foreach (ButtonCategory category in Buttons.Categories)
            if (category.adminOnly && Array.IndexOf(category.buttons, button) != -1)
                return true;

        return false;
    }

    private static int CountVisibleButtons(ButtonCategory category, bool isAdmin)
    {
        int count = 0;

        foreach (ButtonInfo button in category.buttons)
        {
            if (IsCategoryNavButton(button))
                continue;

            if (!isAdmin && IsButtonInAdminCategory(button))
                continue;

            count++;
        }

        return count;
    }

    private static int FindFirstVisibleCategory()
    {
        bool isAdmin = HamburburData.IsLocalAdmin || HamburburData.IsLocalSuperAdmin;

        for (int i = 0; i < Buttons.Categories.Length; i++)
            if ((!Buttons.Categories[i].adminOnly || isAdmin) && CountVisibleButtons(Buttons.Categories[i], isAdmin) > 0)
                return i;

        return 0;
    }

    private void EnsureValidTab(bool isAdmin)
    {
        if (view != GuiView.Category)
            return;

        ButtonCategory category = Buttons.Categories[currentGuiTab];

        if ((!category.adminOnly || isAdmin) && CountVisibleButtons(category, isAdmin) > 0)
            return;

        view = GuiView.Home;
    }

    private void LogErrorOnce(string key, Exception e)
    {
        if (!loggedErrors.Add(key))
            return;

        Debug.LogError($"{Constants.Name} // DesktopGui error ({key}): {e}");
    }
}
