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
    private const float HiddenX   = -380f;
    private const float VisibleX  = 12f;

    private static readonly Color Accent    = new(0.616f, 0.306f, 0.867f);
    private static readonly Color TextMain  = new(0.93f,  0.93f,  0.96f);
    private static readonly Color TextDim   = new(0.62f,  0.60f,  0.70f);

    private float systemX = HiddenX;
    private float modsX   = HiddenX;

    private int   currentGuiTab;
    private bool  menuVisible = true;
    private bool  stylesReady;

    private Vector2 modScrollPosition;
    private string  searchQuery = "";

    private Rect systemWindowRect = new(HiddenX, 24, 240, 210);
    private Rect modsWindowRect   = new(HiddenX, 24, 350, 640);

    private GUIStyle windowStyle;
    private GUIStyle buttonStyle;
    private GUIStyle toggleOnStyle;
    private GUIStyle toggleOffStyle;
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
    private Texture2D scrollbarThumbTex;

    private Texture2D logoTexture;

    // Cached in Update so OnGUI never touches game APIs that can fail
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

        UpdateAnimations();
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

    private void UpdateAnimations()
    {
        Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        mousePos.y = Screen.height - mousePos.y;

        bool hoverArea = mousePos.x < 640f && mousePos.y < 740f;

        systemX = Mathf.Lerp(systemX, hoverArea ? VisibleX : HiddenX, Time.deltaTime * AnimSpeed);
        modsX   = Mathf.Lerp(modsX,   hoverArea ? VisibleX + 255f : HiddenX, Time.deltaTime * AnimSpeed);

        systemWindowRect.x = systemX;
        modsWindowRect.x   = modsX;
    }

    private void OnGUI()
    {
        if (!menuVisible)
            return;

        InitializeStyles();

        systemWindowRect = GUI.Window(0, systemWindowRect, DrawSystemWindow, "", windowStyle);
        modsWindowRect   = GUI.Window(1, modsWindowRect,   DrawModsWindow,   "", windowStyle);
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
            padding   = new RectOffset(16, 16, 16, 16),
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

        // Purple scrollbar
        GUI.skin.verticalScrollbar.normal.background          = buttonOffTex;
        GUI.skin.verticalScrollbarThumb.normal.background     = scrollbarThumbTex;
        GUI.skin.verticalScrollbarThumb.hover.background      = scrollbarThumbTex;
        GUI.skin.verticalScrollbarThumb.active.background     = scrollbarThumbTex;
        GUI.skin.verticalScrollbar.fixedWidth                 = 10f;
        GUI.skin.verticalScrollbarThumb.fixedWidth            = 10f;

        stylesReady = true;
    }

    private GUIStyle MakeColoredLabel(Color color)
    {
        GUIStyle style = new(labelStyle)
        {
            normal = { textColor = color },
        };

        return style;
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
                    // y=0 is the bottom row; lerp so the top of the panel is lighter
                    c = Color.Lerp(fillBottom, fillTop, y / (float)(size - 1));
                    c.a = 0.97f;
                }
                else if (dist < border * 0.5f)
                {
                    c = borderCol;
                }
                else
                {
                    // 1px antialiased falloff outside the border
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

    // ---------- window chrome ----------

    private static readonly Rect TlCorner = new(0f,  0.75f, 0.25f, 0.25f);
    private static readonly Rect TrCorner = new(0.75f, 0.75f, 0.25f, 0.25f);
    private static readonly Rect BlCorner = new(0f,  0f,    0.25f, 0.25f);
    private static readonly Rect BrCorner = new(0.75f, 0f,    0.25f, 0.25f);
    private static readonly Rect TopEdge  = new(0.25f, 0.75f, 0.5f,  0.25f);
    private static readonly Rect BotEdge  = new(0.25f, 0f,    0.5f,  0.25f);
    private static readonly Rect LeftEdge = new(0f,    0.25f, 0.25f, 0.5f);
    private static readonly Rect RightEdge= new(0.75f, 0.25f, 0.25f, 0.5f);
    private static readonly Rect Center   = new(0.25f, 0.25f, 0.5f,  0.5f);

    private void DrawWindowBackground(float width, float height)
    {
        if (Event.current.type != EventType.Repaint || roundedBackgroundTex == null)
            return;

        const float c = 18f;

        // corners
        GUI.DrawTextureWithTexCoords(new Rect(0, 0, c, c),               roundedBackgroundTex, TlCorner);
        GUI.DrawTextureWithTexCoords(new Rect(width - c, 0, c, c),       roundedBackgroundTex, TrCorner);
        GUI.DrawTextureWithTexCoords(new Rect(0, height - c, c, c),      roundedBackgroundTex, BlCorner);
        GUI.DrawTextureWithTexCoords(new Rect(width - c, height - c, c, c), roundedBackgroundTex, BrCorner);

        // edges
        GUI.DrawTextureWithTexCoords(new Rect(c, 0, width - c * 2f, c),               roundedBackgroundTex, TopEdge);
        GUI.DrawTextureWithTexCoords(new Rect(c, height - c, width - c * 2f, c),      roundedBackgroundTex, BotEdge);
        GUI.DrawTextureWithTexCoords(new Rect(0, c, c, height - c * 2f),              roundedBackgroundTex, LeftEdge);
        GUI.DrawTextureWithTexCoords(new Rect(width - c, c, c, height - c * 2f),      roundedBackgroundTex, RightEdge);

        // center
        GUI.DrawTextureWithTexCoords(new Rect(c, c, width - c * 2f, height - c * 2f), roundedBackgroundTex, Center);
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

    // ---------- system window ----------

    private void DrawSystemWindow(int windowID)
    {
        try
        {
            DrawWindowBackground(systemWindowRect.width, systemWindowRect.height);

            GUILayout.Space(2);

            GUILayout.Label("SYSTEM", labelStyle);

            DrawAccentBar();

            GUILayout.Space(8);

            int fps = int.TryParse(cachedFps, out int f) ? f : 0;
            GUIStyle fpsStyle = fps >= 60 ? fpsGreenStyle : fps >= 30 ? fpsYellowStyle : fpsRedStyle;

            DrawStatRow("FPS",     cachedFps,    fpsStyle);
            DrawRoomRow();
            DrawStatRow("Players", cachedPlayers);
            DrawStatRow("Version", Constants.Version);

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Disconnect", buttonStyle, GUILayout.Height(26)))
                PhotonNetwork.Disconnect();

            GUILayout.Space(6);

            if (GUILayout.Button("Quit", buttonStyle, GUILayout.Height(26)))
                Application.Quit();

            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0, 0, systemWindowRect.width, 30));
        }
        catch (Exception e)
        {
            LogErrorOnce("system-window", e);
        }
    }

    private void DrawStatRow(string key, string value, GUIStyle valueStyle = null)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(key, dimLabelStyle, GUILayout.Width(70));
        GUILayout.Label(value, valueStyle ?? labelStyle);
        GUILayout.EndHorizontal();
    }

    private void DrawRoomRow()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Room", dimLabelStyle, GUILayout.Width(70));
        GUILayout.Label(cachedRoom, labelStyle);

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Copy", buttonStyle, GUILayout.Width(46), GUILayout.Height(20)))
            GUIUtility.systemCopyBuffer = cachedRoom;

        GUILayout.EndHorizontal();
    }

    // ---------- mods window ----------

    private void DrawModsWindow(int windowID)
    {
        try
        {
            DrawWindowBackground(modsWindowRect.width, modsWindowRect.height);

            GUILayout.Space(2);

            DrawLogo();

            GUILayout.Space(2);

            DrawAccentBar();

            GUILayout.Space(8);

            bool isAdmin = HamburburData.IsLocalAdmin || HamburburData.IsLocalSuperAdmin;

            EnsureValidTab(isAdmin);

            DrawCategoryTabs(isAdmin);

            GUILayout.Space(8);

            if (!string.IsNullOrEmpty(searchQuery))
                DrawSearchResults(isAdmin);
            else
                DrawCategory(Buttons.Categories[currentGuiTab], isAdmin);

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();

            searchQuery = GUILayout.TextField(searchQuery, searchFieldStyle, GUILayout.Height(24));

            if (GUILayout.Button("X", buttonStyle, GUILayout.Width(30)))
                searchQuery = "";

            GUILayout.EndHorizontal();

            DrawHoverTooltip();

            GUI.DragWindow(new Rect(0, 0, modsWindowRect.width, 30));
        }
        catch (Exception e)
        {
            LogErrorOnce("mods-window", e);
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

        // keep on screen
        if (tipRect.xMax > Screen.width)
            tipRect.x = mousePos.x - tipRect.width - 8f;

        if (tipRect.yMax > Screen.height)
            tipRect.y = mousePos.y - tipRect.height - 8f;

        GUI.Box(tipRect, content, tooltipStyle);
    }

    private void DrawLogo()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (logoTexture != null)
        {
            float maxW = 210f;
            float maxH = 88f;

            float aspect = logoTexture.width / (float)logoTexture.height;
            float width  = maxH * aspect;

            if (width > maxW)
            {
                width = maxW;
            }

            float height = width / aspect;

            Rect logoRect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));

            DrawLogoGlow(logoRect);
            GUI.DrawTexture(logoRect, logoTexture, ScaleMode.ScaleToFit, true);
        }
        else
        {
            GUIStyle big = MakeColoredLabel(Accent);
            big.fontSize  = 22;
            big.fontStyle = FontStyle.BoldAndItalic;

            GUILayout.Label(Constants.Name, big, GUILayout.Height(40));
        }

        GUILayout.FlexibleSpace();
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

    private void DrawCategoryTabs(bool isAdmin)
    {
        float buttonWidth = (modsWindowRect.width - 44f) / 2f;

        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();

        int col = 0;

        for (int i = 0; i < Buttons.Categories.Length; i++)
        {
            ButtonCategory category = Buttons.Categories[i];

            if (category.adminOnly && !isAdmin)
                continue;

            if (CountVisibleButtons(category, isAdmin) == 0)
                continue;

            if (col >= 2)
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                col = 0;
            }

            int enabledCount = 0;

            foreach (ButtonInfo button in category.buttons)
                if (button.enabled)
                    enabledCount++;

            string label = category.name;

            if (enabledCount > 0)
                label += $" <color=#C79BFF>{enabledCount}</color>";

            bool selected  = currentGuiTab == i;
            GUIStyle style = selected ? toggleOnStyle : toggleOffStyle;

            if (GUILayout.Toggle(selected, label, style, GUILayout.Width(buttonWidth), GUILayout.Height(22)))
                currentGuiTab = i;

            col++;
        }

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawCategory(ButtonCategory category, bool isAdmin)
    {
        modScrollPosition = GUILayout.BeginScrollView(modScrollPosition);

        foreach (ButtonInfo button in category.buttons)
        {
            if (IsCategoryNavButton(button))
                continue;

            DrawModButton(button, isAdmin);
            GUILayout.Space(2);
        }

        GUILayout.EndScrollView();
    }

    private void DrawSearchResults(bool isAdmin)
    {
        modScrollPosition = GUILayout.BeginScrollView(modScrollPosition);

        int shown = 0;

        foreach (ButtonInfo button in Buttons.AllButtons)
        {
            if (IsCategoryNavButton(button))
                continue;

            if (!isAdmin && IsButtonInAdminCategory(button))
                continue;

            if (!button.buttonText.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                continue;

            DrawModButton(button, isAdmin);
            GUILayout.Space(2);
            shown++;
        }

        if (shown == 0)
            GUILayout.Label("No matches", dimLabelStyle);

        GUILayout.EndScrollView();
    }

    private void DrawModButton(ButtonInfo button, bool isAdmin)
    {
        if (!isAdmin && IsButtonInAdminCategory(button))
            return;

        switch (button.mode)
        {
            case ButtonMode.Toggle:
            {
                GUIContent content = new(button.GetDisplayText(), button.toolTip);
                bool       state    = GUILayout.Toggle(button.enabled, content, button.enabled ? toggleOnStyle : toggleOffStyle, GUILayout.Height(24));

                if (state != button.enabled)
                    Main.Toggle(button);

                break;
            }

            case ButtonMode.Incremental:
            {
                GUILayout.BeginHorizontal();

                if (GUILayout.Button(new GUIContent("-", button.toolTip), buttonStyle, GUILayout.Width(28), GUILayout.Height(24)))
                    Main.Toggle(button, false);

                GUILayout.Label(button.GetDisplayText(), labelStyle, GUILayout.Height(24));

                if (GUILayout.Button(new GUIContent("+", button.toolTip), buttonStyle, GUILayout.Width(28), GUILayout.Height(24)))
                    Main.Toggle(button, true);

                GUILayout.EndHorizontal();

                break;
            }

            default:
            {
                if (GUILayout.Button(new GUIContent(button.GetDisplayText(), button.toolTip), buttonStyle, GUILayout.Height(24)))
                    Main.Toggle(button);

                break;
            }
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
