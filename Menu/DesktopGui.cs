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
    private const float HiddenX   = -360f;
    private const float VisibleX  = 12f;

    private static readonly Color Accent     = new(0.616f, 0.306f, 0.867f);
    private static readonly Color AccentDim  = new(0.40f,  0.20f,  0.58f);
    private static readonly Color TextMain   = new(0.93f,  0.93f,  0.96f);
    private static readonly Color TextDim    = new(0.62f,  0.60f,  0.70f);

    private float systemX = HiddenX;
    private float modsX   = HiddenX;

    private int   currentGuiTab;
    private bool  menuVisible = true;
    private bool  stylesReady;

    private Vector2 modScrollPosition;
    private string  searchQuery = "";

    private Rect systemWindowRect = new(HiddenX, 24, 235, 200);
    private Rect modsWindowRect   = new(HiddenX, 24, 340, 600);

    private GUIStyle windowStyle;
    private GUIStyle buttonStyle;
    private GUIStyle toggleOnStyle;
    private GUIStyle toggleOffStyle;
    private GUIStyle labelStyle;
    private GUIStyle dimLabelStyle;
    private GUIStyle searchFieldStyle;

    private Texture2D gradientTex;
    private Texture2D accentTex;
    private Texture2D buttonOffTex;
    private Texture2D buttonHoverTex;
    private Texture2D buttonOnTex;

    private Texture2D logoTexture;

    // Cached once per second in Update so OnGUI never touches game APIs that can fail
    private string cachedFps    = "0";
    private string cachedRoom   = "Offline";
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

        cachedFps     = fpsFrames.ToString();
        fpsFrames     = 0;
        fpsTimer      = Time.unscaledTime;

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

        bool hoverArea = mousePos.x < 620f && mousePos.y < 720f;

        systemX = Mathf.Lerp(systemX, hoverArea ? VisibleX : HiddenX, Time.deltaTime * AnimSpeed);
        modsX   = Mathf.Lerp(modsX,   hoverArea ? VisibleX + 250f : HiddenX, Time.deltaTime * AnimSpeed);

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

        gradientTex = CreateGradientTexture(new Color(0.10f, 0.08f, 0.16f, 0.97f), new Color(0.05f, 0.04f, 0.08f, 0.97f));
        accentTex   = CreateSolidColorTexture(Accent);
        buttonOffTex  = CreateSolidColorTexture(new Color(0.14f, 0.13f, 0.20f, 1f));
        buttonHoverTex= CreateSolidColorTexture(new Color(0.22f, 0.19f, 0.30f, 1f));
        buttonOnTex   = CreateSolidColorTexture(new Color(0.42f, 0.20f, 0.62f, 1f));

        windowStyle = new GUIStyle(GUI.skin.window)
        {
            normal    = { background = gradientTex, textColor = Accent },
            onNormal  = { background = gradientTex, textColor = Accent },
            border    = new RectOffset(6, 6, 6, 6),
            padding   = new RectOffset(14, 14, 14, 14),
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

        stylesReady = true;
    }

    private static Texture2D CreateSolidColorTexture(Color color)
    {
        Texture2D texture = new(2, 2);
        texture.SetPixels([color, color, color, color]);
        texture.Apply();

        return texture;
    }

    private static Texture2D CreateGradientTexture(Color top, Color bottom)
    {
        const int height = 64;

        Texture2D texture = new(1, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
        };

        Color[] pixels = new Color[height];

        for (int i = 0; i < height; i++)
            pixels[i] = Color.Lerp(top, bottom, i / (float)(height - 1));

        texture.SetPixels(pixels);
        texture.Apply();

        return texture;
    }

    // ---------- system window ----------

    private void DrawSystemWindow(int windowID)
    {
        try
        {
            GUI.DragWindow(new Rect(0, 0, systemWindowRect.width, 26));

            DrawWindowHeader("SYSTEM");

            GUILayout.Space(6);

            int fps = int.TryParse(cachedFps, out int f) ? f : 0;
            Color fpsColor = fps >= 60 ? new Color(0.45f, 0.90f, 0.55f)
                            : fps >= 30 ? new Color(0.95f, 0.80f, 0.35f)
                            : new Color(0.95f, 0.45f, 0.45f);

            DrawStatRow("FPS",     cachedFps,    fpsColor);
            DrawStatRow("Room",    cachedRoom);
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
        }
        catch (Exception e)
        {
            LogErrorOnce("system-window", e);
        }
    }

    private void DrawStatRow(string key, string value, Color? valueColor = null)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(key, dimLabelStyle, GUILayout.Width(70));

        GUIStyle style = labelStyle;

        if (valueColor != null)
        {
            style = new GUIStyle(labelStyle)
            {
                normal = { textColor = valueColor.Value },
            };
        }

        GUILayout.Label(value, style);
        GUILayout.EndHorizontal();
    }

    // ---------- mods window ----------

    private void DrawModsWindow(int windowID)
    {
        try
        {
            GUI.DragWindow(new Rect(0, 0, modsWindowRect.width, 26));

            DrawLogo();

            GUILayout.Space(4);

            DrawAccentBar();

            GUILayout.Space(6);

            bool isAdmin = HamburburData.IsLocalAdmin || HamburburData.IsLocalSuperAdmin;

            EnsureValidTab(isAdmin);

            DrawCategoryTabs(isAdmin);

            GUILayout.Space(6);

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
        }
        catch (Exception e)
        {
            LogErrorOnce("mods-window", e);
        }
    }

    private void DrawWindowHeader(string title)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(title, labelStyle, GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();

        DrawAccentBar();
    }

    private void DrawAccentBar()
    {
        Rect barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(2), GUILayout.ExpandWidth(true));

        if (Event.current.type == EventType.Repaint && accentTex != null)
            GUI.DrawTexture(barRect, accentTex);
    }

    private void DrawLogo()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (logoTexture != null)
        {
            float maxW = 200f;
            float maxH = 84f;

            float aspect = logoTexture.width / (float)logoTexture.height;
            float width  = maxH * aspect;

            if (width > maxW)
            {
                width  = maxW;
            }

            float height = width / aspect;

            Rect logoRect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));

            DrawLogoGlow(logoRect);
            GUI.DrawTexture(logoRect, logoTexture, ScaleMode.ScaleToFit, true);
        }
        else
        {
            GUIStyle big = new(labelStyle)
            {
                fontSize  = 22,
                fontStyle = FontStyle.BoldAndItalic,
                normal    = { textColor = Accent },
            };

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
        float buttonWidth = (modsWindowRect.width - 38f) / 2f;

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

            bool selected = currentGuiTab == i;
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
            shown++;
        }

        if (shown == 0)
            GUILayout.Label("No matches", dimLabelStyle);

        GUILayout.EndScrollView();
    }

    private static bool IsCategoryNavButton(ButtonInfo button)
    {
        if (button.buttonText == "Return to Main")
            return true;

        foreach (ButtonCategory category in Buttons.Categories)
            if (button.buttonText == category.name)
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

    private void DrawModButton(ButtonInfo button, bool isAdmin)
    {
        if (!isAdmin && IsButtonInAdminCategory(button))
            return;

        switch (button.mode)
        {
            case ButtonMode.Toggle:
            {
                bool state = GUILayout.Toggle(button.enabled, button.GetDisplayText(), button.enabled ? toggleOnStyle : toggleOffStyle, GUILayout.Height(24));

                if (state != button.enabled)
                    Main.Toggle(button);

                break;
            }

            case ButtonMode.Incremental:
            {
                GUILayout.BeginHorizontal();

                if (GUILayout.Button("-", buttonStyle, GUILayout.Width(28), GUILayout.Height(24)))
                    Main.Toggle(button, false);

                GUILayout.Label(button.GetDisplayText(), labelStyle, GUILayout.Height(24));

                if (GUILayout.Button("+", buttonStyle, GUILayout.Width(28), GUILayout.Height(24)))
                    Main.Toggle(button, true);

                GUILayout.EndHorizontal();

                break;
            }

            default:
            {
                if (GUILayout.Button(button.GetDisplayText(), buttonStyle, GUILayout.Height(24)))
                    Main.Toggle(button);

                break;
            }
        }
    }

    // ---------- helpers ----------

    private static bool IsButtonInAdminCategory(ButtonInfo button)
    {
        foreach (ButtonCategory category in Buttons.Categories)
            if (category.adminOnly && Array.IndexOf(category.buttons, button) != -1)
                return true;

        return false;
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
