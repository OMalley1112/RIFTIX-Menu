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
    private const float WindowH   = 650f;
    private const float HiddenX   = -(WindowW + 24f);
    private const float VisibleX  = 12f;
    private const float SidebarW  = 155f;
    private const float BodyH     = 490f;

    private static readonly Color Accent   = new(0.616f, 0.306f, 0.867f);
    private static readonly Color TextMain = new(0.93f,  0.93f,  0.96f);
    private static readonly Color TextDim  = new(0.62f,  0.60f,  0.70f);

    private float windowX = HiddenX;
    private int   currentGuiTab;
    private bool  menuVisible = true;
    private bool  stylesReady;

    private Vector2 modScrollPosition;
    private string  searchQuery = "";
    private int     lastDrawnTab = -1;

    private Rect windowRect = new(HiddenX, 24, WindowW, WindowH);

    private GUIStyle windowStyle;
    private GUIStyle buttonStyle;
    private GUIStyle toggleOnStyle;
    private GUIStyle toggleOffStyle;
    private GUIStyle tabOnStyle;
    private GUIStyle tabOffStyle;
    private GUIStyle labelStyle;
    private GUIStyle dimLabelStyle;
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
    private Texture2D sidebarTex;
    private Texture2D scrollbarThumbTex;

    private Texture2D logoTexture;

    private string cachedFps     = "0";
    private string cachedRoom    = "Offline";
    private string cachedPlayers = "1";
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
            cachedRoom = PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null
                                     ? PhotonNetwork.CurrentRoom.Name
                                     : "Offline";

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
        Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        mousePos.y = Screen.height - mousePos.y;

        bool hoverArea = mousePos.x < WindowW + 40f && mousePos.y < WindowH + 60f;

        windowX = Mathf.Lerp(windowX, hoverArea ? VisibleX : HiddenX, Time.deltaTime * AnimSpeed);
        windowRect.x = windowX;
    }

    private void OnGUI()
    {
        if (!menuVisible)
            return;

        InitializeStyles();

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
        sidebarTex           = CreateSolidColorTexture(new Color(0.03f, 0.02f, 0.06f, 0.55f));
        scrollbarThumbTex    = CreateSolidColorTexture(new Color(0.42f, 0.26f, 0.60f, 1f));

        windowStyle = new GUIStyle(GUI.skin.window)
        {
            normal    = { textColor = Accent },
            onNormal  = { textColor = Accent },
            padding   = new RectOffset(14, 14, 14, 12),
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

        tabOnStyle = new GUIStyle(toggleOnStyle)
        {
            alignment = TextAnchor.MiddleLeft,
            padding   = new RectOffset(12, 6, 2, 2),
        };

        tabOffStyle = new GUIStyle(toggleOffStyle)
        {
            alignment = TextAnchor.MiddleLeft,
            padding   = new RectOffset(12, 6, 2, 2),
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

        // sidebar tint + divider (inset so it stays inside the rounded corners)
        GUI.DrawTexture(new Rect(3f, 8f, SidebarW - 3f, height - 16f), sidebarTex);

        if (accentBarTex != null)
        {
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.14f);
            GUI.DrawTexture(new Rect(SidebarW - 1f, 10f, 1f, height - 20f), accentBarTex);
            GUI.color = previous;
        }
    }

    private void DrawAccentBar()
    {
        Rect barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(2.5f), GUILayout.ExpandWidth(true));

        if (Event.current.type != EventType.Repaint || accentBarTex == null)
            return;

        Color previous = GUI.color;
        GUI.color = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.04f, 1f), 0.5f, 1f);
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

            GUILayout.Space(10);

            bool isAdmin = HamburburData.IsLocalAdmin || HamburburData.IsLocalSuperAdmin;

            EnsureValidTab(isAdmin);

            GUILayout.BeginHorizontal(GUILayout.Height(BodyH));

            DrawSidebar(isAdmin);
            GUILayout.Space(10);
            DrawContentPane(isAdmin);

            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            GUILayout.BeginHorizontal();

            searchQuery = GUILayout.TextField(searchQuery, searchFieldStyle, GUILayout.Height(24));

            if (GUILayout.Button("X", buttonStyle, GUILayout.Width(30)))
                searchQuery = "";

            GUILayout.EndHorizontal();

            DrawHoverTooltip();

            // thin top strip only — keep the Copy button and stats clickable
            GUI.DragWindow(new Rect(0, 0, windowRect.width, 34));
        }
        catch (Exception e)
        {
            LogErrorOnce("window", e);
        }
    }

    private void DrawHeader()
    {
        GUILayout.BeginHorizontal();

        // logo
        if (logoTexture != null)
        {
            float aspect = logoTexture.width / (float)logoTexture.height;
            float height = 66f;
            float width  = height * aspect;

            Rect logoRect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));

            DrawLogoGlow(logoRect);
            GUI.DrawTexture(logoRect, logoTexture, ScaleMode.ScaleToFit, true);
        }
        else
        {
            GUIStyle big = MakeColoredLabel(Accent);
            big.fontSize  = 24;
            big.fontStyle = FontStyle.BoldAndItalic;
            GUILayout.Label(Constants.Name, big, GUILayout.Height(60));
        }

        GUILayout.Space(12);

        // stats block, right side
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        GUILayout.FlexibleSpace();

        GUIStyle nameStyle = MakeColoredLabel(Accent);
        nameStyle.fontSize = 16;
        nameStyle.fontStyle = FontStyle.BoldAndItalic;

        GUILayout.Label(Constants.Name + "  <size=10><color=#9999AA>mod menu</color></size>", nameStyle);

        GUILayout.BeginHorizontal();

        int fps = int.TryParse(cachedFps, out int f) ? f : 0;
        GUIStyle fpsStyle = fps >= 60 ? fpsGreenStyle : fps >= 30 ? fpsYellowStyle : fpsRedStyle;

        GUILayout.Label($"FPS {cachedFps}", fpsStyle);
        GUILayout.Space(10);
        GUILayout.Label($"Players {cachedPlayers}", dimLabelStyle);
        GUILayout.Space(10);
        GUILayout.Label($"v{Constants.Version}", dimLabelStyle);

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        GUILayout.Label($"Room {cachedRoom}", dimLabelStyle);

        if (GUILayout.Button(new GUIContent("Copy", "Copy the room name to your clipboard"), buttonStyle, GUILayout.Width(46), GUILayout.Height(19)))
            GUIUtility.systemCopyBuffer = cachedRoom;

        GUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();
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

    private void DrawSidebar(bool isAdmin)
    {
        GUILayout.BeginVertical(GUILayout.Width(SidebarW));

        for (int i = 0; i < Buttons.Categories.Length; i++)
        {
            ButtonCategory category = Buttons.Categories[i];

            if (category.adminOnly && !isAdmin)
                continue;

            if (CountVisibleButtons(category, isAdmin) == 0)
                continue;

            int enabledCount = 0;

            foreach (ButtonInfo button in category.buttons)
                if (button.enabled)
                    enabledCount++;

            string label = category.name;

            if (enabledCount > 0)
                label += $"  <color=#C79BFF>{enabledCount}</color>";

            bool selected  = currentGuiTab == i;
            GUIStyle style = selected ? tabOnStyle : tabOffStyle;

            if (GUILayout.Toggle(selected, label, style, GUILayout.Height(26), GUILayout.ExpandWidth(true)))
                currentGuiTab = i;
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button(new GUIContent("Disconnect", "Leave the current room"), buttonStyle, GUILayout.Height(24)))
            PhotonNetwork.Disconnect();

        GUILayout.Space(4);

        if (GUILayout.Button(new GUIContent("Quit", "Exit the game"), buttonStyle, GUILayout.Height(24)))
            Application.Quit();

        GUILayout.Space(4);

        GUILayout.Label("F11 hides this menu", dimLabelStyle);

        GUILayout.EndVertical();
    }

    private void DrawContentPane(bool isAdmin)
    {
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

        if (currentGuiTab != lastDrawnTab)
        {
            modScrollPosition = Vector2.zero;
            lastDrawnTab      = currentGuiTab;
        }

        modScrollPosition = GUILayout.BeginScrollView(modScrollPosition, GUILayout.ExpandHeight(true));

        List<ButtonInfo> buttons = [];

        ButtonCategory category = Buttons.Categories[currentGuiTab];

        if (!string.IsNullOrEmpty(searchQuery))
        {
            foreach (ButtonInfo button in Buttons.AllButtons)
            {
                if (IsCategoryNavButton(button))
                    continue;

                if (!isAdmin && IsButtonInAdminCategory(button))
                    continue;

                if (button.buttonText.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                    buttons.Add(button);
            }
        }
        else
        {
            foreach (ButtonInfo button in category.buttons)
            {
                if (IsCategoryNavButton(button))
                    continue;

                if (!isAdmin && IsButtonInAdminCategory(button))
                    continue;

                buttons.Add(button);
            }
        }

        GUILayout.Label(string.IsNullOrEmpty(searchQuery) ? category.name : $"Search: {searchQuery}", dimLabelStyle);
        GUILayout.Space(4);

        DrawButtonGrid(buttons);

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void DrawButtonGrid(List<ButtonInfo> buttons)
    {
        int i = 0;

        while (i < buttons.Count)
        {
            ButtonInfo first = buttons[i];

            // incremental controls take a full row
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
                    Main.Toggle(button);

                break;
            }

            case ButtonMode.Incremental:
            {
                GUILayout.BeginHorizontal();

                if (GUILayout.Button(new GUIContent("-", button.toolTip), buttonStyle, GUILayout.Width(30), GUILayout.Height(24)))
                    Main.Toggle(button, false);

                GUILayout.Label(button.GetDisplayText(), labelStyle, GUILayout.Height(24));

                if (GUILayout.Button(new GUIContent("+", button.toolTip), buttonStyle, GUILayout.Width(30), GUILayout.Height(24)))
                    Main.Toggle(button, true);

                GUILayout.EndHorizontal();

                break;
            }

            default:
            {
                if (GUILayout.Button(new GUIContent(button.GetDisplayText(), button.toolTip), buttonStyle, GUILayout.Height(24), GUILayout.ExpandWidth(true)))
                    Main.Toggle(button);

                break;
            }
        }
    }

    private void DrawHoverTooltip()
    {
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
        ButtonCategory category = Buttons.Categories[currentGuiTab];

        if ((!category.adminOnly || isAdmin) && CountVisibleButtons(category, isAdmin) > 0)
            return;

        currentGuiTab = FindFirstVisibleCategory();
    }

    private void LogErrorOnce(string key, Exception e)
    {
        if (!loggedErrors.Add(key))
            return;

        Debug.LogError($"{Constants.Name} // DesktopGui error ({key}): {e}");
    }
}
