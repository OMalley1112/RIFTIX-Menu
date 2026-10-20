using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
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
    private const float AnimSpeed  = 12f;
    private const float HiddenX    = -340f;
    private const float VisibleX   = 10f;

    private static readonly Color AccentColor    = new(0.616f, 0.306f, 0.867f);
    private static readonly Color WindowBgColor  = new(0.07f,  0.06f,  0.10f, 0.96f);
    private static readonly Color ButtonOffColor = new(0.13f,  0.12f,  0.18f, 1f);
    private static readonly Color ButtonOnColor  = new(0.42f,  0.20f,  0.62f, 1f);
    private static readonly Color ButtonHoverColor= new(0.22f,  0.18f,  0.30f, 1f);

    private float systemX = HiddenX;
    private float modsX   = HiddenX;
    private int   currentGuiTab;
    private bool  menuVisible = true;
    private bool  stylesInitialized;
    private Vector2 modScrollPosition;
    private string  searchQuery = "";

    private Rect systemWindowRect = new(HiddenX, 20, 230, 210);
    private Rect modsWindowRect   = new(HiddenX, 20, 340, 620);

    private GUIStyle windowStyle;
    private GUIStyle buttonStyle;
    private GUIStyle toggleOnStyle;
    private GUIStyle toggleOffStyle;
    private GUIStyle labelStyle;
    private GUIStyle headerStyle;
    private GUIStyle searchFieldStyle;

    private Texture2D windowBackgroundTex;
    private Texture2D buttonNormalTex;
    private Texture2D buttonHoverTex;
    private Texture2D toggleOnTex;
    private Texture2D toggleOffTex;

    private Texture2D logoTexture;

    private void Awake()
    {
        logoTexture = StupidAssetUtils.LoadTexture2D("watermark.png");

        currentGuiTab = FindFirstVisibleCategory();
    }

    private void Update()
    {
        if (UnityInput.Current.GetKeyDown(KeyCode.F11))
            menuVisible = !menuVisible;

        UpdateAnimations();
    }

    private void UpdateAnimations()
    {
        Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        mousePos.y = Screen.height - mousePos.y;

        bool hoverArea = mousePos.x < 620f && mousePos.y < 720f;

        systemX = Mathf.Lerp(systemX, hoverArea ? VisibleX : HiddenX, Time.deltaTime * AnimSpeed);
        modsX   = Mathf.Lerp(modsX,   hoverArea ? VisibleX + 245f : HiddenX, Time.deltaTime * AnimSpeed);

        systemWindowRect.x = systemX;
        modsWindowRect.x   = modsX;
    }

    private void OnGUI()
    {
        if (!menuVisible)
            return;

        InitializeStyles();

        systemWindowRect = GUI.Window(0, systemWindowRect, DrawSystemWindow, "  RIFTIX  //  System", windowStyle);
        modsWindowRect   = GUI.Window(1, modsWindowRect,   DrawModsWindow,   "  RIFTIX  //  Mods",   windowStyle);
    }

    private void InitializeStyles()
    {
        if (stylesInitialized)
            return;

        windowBackgroundTex = CreateSolidColorTexture(WindowBgColor);
        buttonNormalTex     = CreateSolidColorTexture(ButtonOffColor);
        buttonHoverTex      = CreateSolidColorTexture(ButtonHoverColor);
        toggleOnTex         = CreateSolidColorTexture(ButtonOnColor);
        toggleOffTex        = CreateSolidColorTexture(ButtonOffColor);

        windowStyle = new GUIStyle(GUI.skin.window)
        {
            normal      = { background = windowBackgroundTex, textColor = AccentColor },
            onNormal    = { background = windowBackgroundTex, textColor = AccentColor },
            border      = new RectOffset(8, 8, 8, 8),
            padding     = new RectOffset(12, 12, 28, 12),
            fontStyle   = FontStyle.Bold,
            fontSize    = 13,
            alignment   = TextAnchor.UpperLeft,
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            normal      = { background = buttonNormalTex, textColor = Color.white },
            hover       = { background = buttonHoverTex,  textColor = Color.white },
            active      = { background = toggleOnTex,     textColor = Color.white },
            fontStyle   = FontStyle.Bold,
            alignment   = TextAnchor.MiddleCenter,
            wordWrap    = true,
            fontSize    = 12,
        };

        toggleOnStyle = new GUIStyle(buttonStyle)
        {
            normal   = { background = toggleOnTex, textColor = Color.white },
            hover    = { background = toggleOnTex, textColor = Color.white },
            onNormal = { background = toggleOnTex, textColor = Color.white },
        };

        toggleOffStyle = new GUIStyle(buttonStyle)
        {
            normal = { background = buttonNormalTex, textColor = new Color(1f, 1f, 1f, 0.75f) },
            hover  = { background = buttonHoverTex,  textColor = Color.white },
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            normal    = { textColor = Color.white },
            fontStyle = FontStyle.Bold,
            fontSize  = 12,
        };

        headerStyle = new GUIStyle(labelStyle)
        {
            normal    = { textColor = AccentColor },
            fontSize  = 15,
            fontStyle = FontStyle.BoldAndItalic,
        };

        searchFieldStyle = new GUIStyle(GUI.skin.textField)
        {
            normal    = { background = buttonNormalTex, textColor = Color.white },
            focused   = { background = buttonHoverTex,  textColor = Color.white },
            hover     = { background = buttonHoverTex,  textColor = Color.white },
            fontStyle = FontStyle.Bold,
            padding   = new RectOffset(8, 8, 4, 4),
            fontSize  = 12,
        };

        stylesInitialized = true;
    }

    private static Texture2D CreateSolidColorTexture(Color color)
    {
        Texture2D texture = new(2, 2);
        texture.SetPixels([color, color, color, color]);
        texture.Apply();

        return texture;
    }

    private void DrawSystemWindow(int windowID)
    {
        GUI.DragWindow(new Rect(0, 0, systemWindowRect.width, 22));

        GUILayout.Space(4);

        string fps = GetLocalFPS();
        string room = PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null
                                  ? PhotonNetwork.CurrentRoom.Name
                                  : "Offline";
        int players = PhotonNetwork.InRoom ? PhotonNetwork.PlayerList.Length : 1;

        GUILayout.Label($"FPS: {fps}",       labelStyle);
        GUILayout.Label($"Room: {room}",     labelStyle);
        GUILayout.Label($"Players: {players}", labelStyle);
        GUILayout.Label($"Version: {Constants.Version}", labelStyle);

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Disconnect", buttonStyle, GUILayout.Height(26)))
            PhotonNetwork.Disconnect();

        GUILayout.Space(4);

        if (GUILayout.Button("Quit Game", buttonStyle, GUILayout.Height(26)))
            Application.Quit();
    }

    private void DrawModsWindow(int windowID)
    {
        GUI.DragWindow(new Rect(0, 0, modsWindowRect.width, 22));

        GUILayout.Space(2);

        DrawLogo();

        GUILayout.Space(6);

        bool isAdmin = HamburburData.IsLocalAdmin || HamburburData.IsLocalSuperAdmin;
        float buttonWidth = (modsWindowRect.width - 40f) / 2f;

        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();

        int count = 0;

        foreach (ButtonCategory category in Buttons.Categories)
        {
            if (category.adminOnly && !isAdmin)
                continue;

            if (count > 0 && count % 2 == 0)
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
            }

            GUIStyle tabStyle = currentGuiTab == Array.IndexOf(Buttons.Categories, category)
                                            ? toggleOnStyle
                                            : toggleOffStyle;

            if (GUILayout.Toggle(currentGuiTab == Array.IndexOf(Buttons.Categories, category),
                                 category.name,
                                 tabStyle,
                                 GUILayout.Width(buttonWidth),
                                 GUILayout.Height(22)))
                currentGuiTab = Array.IndexOf(Buttons.Categories, category);

            count++;
        }

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

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
    }

    private void DrawLogo()
    {
        if (logoTexture == null)
        {
            GUILayout.Label(Constants.Name, headerStyle);

            return;
        }

        float width  = modsWindowRect.width - 30f;
        float height = width * (logoTexture.height / (float)logoTexture.width);

        if (height > 110f)
        {
            height = 110f;
            width = height * (logoTexture.width / (float)logoTexture.height);
        }

        Rect logoRect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));

        GUI.DrawTexture(logoRect, logoTexture, ScaleMode.ScaleToFit, true);
    }

    private void DrawCategory(ButtonCategory category, bool isAdmin)
    {
        modScrollPosition = GUILayout.BeginScrollView(modScrollPosition);

        foreach (ButtonInfo button in category.buttons)
        {
            if (button.buttonText == "Return to Main")
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
            if (button.buttonText == "Return to Main")
                continue;

            if (!isAdmin && IsButtonInAdminCategory(button))
                continue;

            if (!button.buttonText.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                continue;

            DrawModButton(button, isAdmin);
            shown++;
        }

        GUILayout.Label($"Results: {shown}", labelStyle);

        if (shown == 0)
            GUILayout.Label("No matches", labelStyle);

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
                bool state = GUILayout.Toggle(button.enabled, button.GetDisplayText(), button.enabled ? toggleOnStyle : toggleOffStyle, GUILayout.Height(24));

                if (state != button.enabled)
                    Main.Toggle(button);

                break;
            }

            case ButtonMode.Incremental:
            {
                GUILayout.BeginHorizontal();

                if (GUILayout.Button("<", buttonStyle, GUILayout.Width(30)))
                    Main.Toggle(button, false);

                GUILayout.Label(button.GetDisplayText(), labelStyle, GUILayout.Height(24));

                if (GUILayout.Button(">", buttonStyle, GUILayout.Width(30)))
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
            if (!Buttons.Categories[i].adminOnly || isAdmin)
                return i;

        return 0;
    }

    private static string GetLocalFPS()
    {
        if (VRRig.LocalRig == null)
            return "0";

        FieldInfo fieldInfo = AccessTools.Field(VRRig.LocalRig.GetType(), "fps");

        return fieldInfo?.GetValue(VRRig.LocalRig)?.ToString() ?? "0";
    }
}
