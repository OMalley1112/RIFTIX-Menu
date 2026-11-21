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
    private const float AnimSpeed   = 12f;
    private const float WindowW     = 600f;
    private const float WindowH     = 660f;
    private const float HiddenX     = -(WindowW + 24f);
    private const float VisibleX    = 12f;

    // The mockup's rows: six to a page, tall and full width, one column.
    private const int   RowsPerPage = 6;
    private const float RowH        = 64f;
    private const float RowGap      = 10f;

    private static readonly Color Accent     = new(0.616f, 0.306f, 0.867f);
    private static readonly Color TextMain   = new(0.93f,  0.93f,  0.96f);
    private static readonly Color TextDim    = new(0.62f,  0.60f,  0.70f);
    private static readonly Color TitleText  = new(0.74f,  0.74f,  0.79f);
    private static readonly Color ChevronCol = new(0.78f,  0.72f,  0.95f);

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

    // One list at a time, paged: the context changes reset the page.
    private int    listPage;
    private bool   searchOpen;
    private string searchQuery      = "";
    private string lastSearchQuery  = "";
    private GuiView lastView        = GuiView.Home;
    private int    lastDrawnTab     = -1;
    private float  ignoreTooltipsUntil;

    private Rect windowRect = new(HiddenX, 42f, WindowW, WindowH);

    private GUIStyle windowStyle;
    private GUIStyle rowStyle;
    private GUIStyle rowOnStyle;
    private GUIStyle rowLabelStyle;
    private GUIStyle pillStyle;
    private GUIStyle linkStyle;
    private GUIStyle labelStyle;
    private GUIStyle dimLabelStyle;
    private GUIStyle chevronStyle;
    private GUIStyle searchFieldStyle;
    private GUIStyle tooltipStyle;

    private Texture2D roundedBackgroundTex;
    private Texture2D accentBarTex;

    private Texture2D logoTexture;

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

    private void LateUpdate()
    {
        try
        {
            StupidTemplate.Mods.DabUp.ApplyPose();
        }
        catch (Exception e)
        {
            LogErrorOnce("dabup", e);
        }
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

        // ...then clamp the anchor, so the window can never be dragged off screen
        // and lost. The top edge is held low enough for the disconnect pill
        // that rides above the panel.
        baseX = Mathf.Clamp(baseX, 0f, Mathf.Max(0f, Screen.width - windowRect.width - 8f));

        float maxY = Mathf.Max(0f, Screen.height - windowRect.height - 8f);
        float minY = Mathf.Min(42f, maxY);

        windowRect.y = Mathf.Clamp(windowRect.y, minY, maxY);

        // auto-opens on startup; F11 slides it in/out at the position you left it
        slideT       = Mathf.Lerp(slideT, menuVisible ? 1f : 0f, Time.deltaTime * AnimSpeed);
        windowX      = Mathf.Lerp(HiddenX, baseX, slideT);
        windowRect.x = windowX;
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

        DrawDisconnectPill();
    }

    // ---------- styles ----------

    private void InitializeStyles()
    {
        if (stylesReady)
            return;

        roundedBackgroundTex = GenerateRoundedBackground();
        accentBarTex         = CreateSolidColorTexture(Color.white);

        Texture2D rowTex        = GenerateRowTexture(12f, new Color(0.10f,  0.078f, 0.17f,  1f),
                new Color(0.55f, 0.49f,  0.75f, 0.95f));
        Texture2D rowHoverTex   = GenerateRowTexture(12f, new Color(0.16f,  0.12f,  0.26f,  1f),
                new Color(0.62f, 0.55f,  0.82f, 1f));
        Texture2D rowOnTex      = GenerateRowTexture(12f, new Color(0.35f,  0.18f,  0.56f,  1f),
                new Color(0.68f, 0.58f,  0.88f, 1f));
        Texture2D pillTex       = GenerateRowTexture(28f, new Color(0.10f,  0.078f, 0.17f,  0.97f),
                new Color(0.55f, 0.49f,  0.75f, 0.95f));
        Texture2D pillHoverTex  = GenerateRowTexture(28f, new Color(0.18f,  0.14f,  0.28f,  1f),
                new Color(0.68f, 0.60f,  0.88f, 1f));
        Texture2D fieldTex      = CreateSolidColorTexture(new Color(0.14f, 0.13f, 0.20f, 1f));
        Texture2D fieldHoverTex = CreateSolidColorTexture(new Color(0.24f, 0.20f, 0.33f, 1f));

        windowStyle = new GUIStyle(GUI.skin.window)
        {
            normal    = { textColor = Accent },
            onNormal  = { textColor = Accent },
            padding   = new RectOffset(14, 14, 12, 12),
            fontSize  = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            normal    = { textColor = TextMain },
            fontStyle = FontStyle.Bold,
            fontSize  = 12,
            richText  = true,
        };

        dimLabelStyle = new GUIStyle(labelStyle)
        {
            normal = { textColor = TextDim },
        };

        rowLabelStyle = new GUIStyle(labelStyle)
        {
            fontSize = 14,
        };

        // The mockup's rows: dark purple slab, light lavender outline,
        // white bold text on the left, the chevron drawn after it.
        rowStyle = new GUIStyle(GUI.skin.label)
        {
            normal    = { background = rowTex,   textColor = TextMain },
            hover     = { background = rowHoverTex, textColor = Color.white },
            active    = { background = rowHoverTex, textColor = Color.white },
            border    = new RectOffset(16, 16, 16, 16),
            alignment = TextAnchor.MiddleLeft,
            padding   = new RectOffset(20, 44, 6, 6),
            fontSize  = 15,
            fontStyle = FontStyle.Bold,
            richText  = true,
            wordWrap  = false,
            clipping  = TextClipping.Clip,
        };

        rowOnStyle = new GUIStyle(rowStyle)
        {
            normal = { background = rowOnTex, textColor = Color.white },
            hover  = { background = rowOnTex, textColor = Color.white },
            active = { background = rowOnTex, textColor = Color.white },
        };

        // The pills: DISCONNECT on top, PREV / NEXT / U / S along the
        // bottom - stadium outline, no fill to speak of.
        pillStyle = new GUIStyle(GUI.skin.button)
        {
            normal    = { background = pillTex,      textColor = TextMain },
            hover     = { background = pillHoverTex, textColor = Color.white },
            active    = { background = pillHoverTex, textColor = Color.white },
            border    = new RectOffset(26, 26, 26, 26),
            alignment = TextAnchor.MiddleCenter,
            fontSize  = 13,
            fontStyle = FontStyle.Bold,
            richText  = true,
        };

        // Version left / stats right share one line; the stats half is a
        // invisible button that copies the room name.
        linkStyle = new GUIStyle(dimLabelStyle)
        {
            alignment = TextAnchor.MiddleRight,
            richText  = true,
        };

        linkStyle.hover.textColor   = Color.white;
        linkStyle.active.textColor = Color.white;

        chevronStyle = new GUIStyle(labelStyle)
        {
            normal    = { textColor = ChevronCol },
            alignment = TextAnchor.MiddleCenter,
            fontSize  = 18,
        };

        searchFieldStyle = new GUIStyle(GUI.skin.textField)
        {
            normal    = { background = fieldTex,      textColor = TextMain },
            focused   = { background = fieldHoverTex, textColor = Color.white },
            hover     = { background = fieldHoverTex, textColor = TextMain },
            fontStyle = FontStyle.Bold,
            padding   = new RectOffset(8, 8, 4, 4),
            fontSize  = 12,
        };

        tooltipStyle = new GUIStyle(GUI.skin.box)
        {
            normal    = { background = fieldTex, textColor = TextMain },
            wordWrap  = true,
            fontStyle = FontStyle.Italic,
            fontSize  = 11,
            padding   = new RectOffset(8, 8, 6, 6),
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

    // A flat-filled rounded slab with an outline, nine-sliced by the
    // style's border so wide rows and stadium pills both stay clean.
    private static Texture2D GenerateRowTexture(float radius, Color fill, Color borderCol)
    {
        const int   size   = 64;
        const float border = 2f;

        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
        };

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

                Color c;

                if (dist < -border * 0.5f)
                    c = fill;
                else if (dist < border * 0.5f)
                    c = borderCol;
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
    }

    // The hairline under the title and above the bottom bar.
    private void DrawSeparator()
    {
        Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.Height(1f), GUILayout.ExpandWidth(true));

        if (Event.current.type != EventType.Repaint || accentBarTex == null)
            return;

        Color previous = GUI.color;
        GUI.color = new Color(0.55f, 0.50f, 0.68f, 0.35f);
        GUI.DrawTexture(rect, accentBarTex);
        GUI.color = previous;
    }

    // ---------- main window ----------

    private void DrawWindow(int windowID)
    {
        try
        {
            DrawWindowBackground();

            GUILayout.Space(2);

            bool isAdmin = HamburburData.LocalAdminAccess;

            EnsureValidTab(isAdmin);

            DrawHeader();

            DrawSeparator();

            GUILayout.Space(8);

            DrawContentPane(isAdmin);

            GUILayout.Space(6);

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

    // The mockup's head: the menu name centred large, a hairline, then
    // the version on the left and the live stats on the right (the stats
    // half is clickable - it copies the room name).
    private void DrawHeader()
    {
        string title =
                !string.IsNullOrEmpty(searchQuery)
                        ? "SEARCH"
                        : view == GuiView.Category
                                  ? Buttons.Categories[currentGuiTab].name
                                  : Constants.Name;

        GUIStyle titleStyle = MakeColoredLabel(TitleText);
        titleStyle.fontSize  = 26;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;

        GUILayout.Label(title, titleStyle, GUILayout.Height(36));

        GUILayout.BeginHorizontal();

        GUILayout.Label($"V{Constants.Version}", dimLabelStyle);

        GUILayout.FlexibleSpace();

        int fps = int.TryParse(cachedFps, out int f) ? f : 0;
        string fpsColor = fps >= 60 ? "#73E68C" : fps >= 30 ? "#F2CC59" : "#F27373";

        string stats =
                $"<color={fpsColor}>{cachedFps} FPS</color>  ·  {cachedPlayers}P  ·  {cachedRoom}";

        if (GUILayout.Button(new GUIContent(stats, "Click to copy the room name"),
                             linkStyle))
            SafeCopyRoom();

        GUILayout.EndHorizontal();
    }

    // The disconnect pill that rides above the panel, centred on it and
    // sliding with it.
    private void DrawDisconnectPill()
    {
        const float w = 220f;
        const float h = 30f;

        float x = windowRect.x + (windowRect.width - w) * 0.5f;
        float y = Mathf.Max(4f, windowRect.y - h - 10f);

        if (GUI.Button(new Rect(x, y, w, h),
                       new GUIContent("DISCONNECT", "Leave the room"), pillStyle))
            SafeDisconnect();
    }

    // ---------- content ----------

    private void DrawContentPane(bool isAdmin)
    {
        // Any change of context starts the list back at page one.
        if (currentGuiTab != lastDrawnTab || searchQuery != lastSearchQuery || view != lastView)
        {
            listPage         = 0;
            lastDrawnTab     = currentGuiTab;
            lastSearchQuery  = searchQuery;
            lastView         = view;
        }

        if (!string.IsNullOrEmpty(searchQuery))
        {
            DrawModList(CollectSearchButtons(isAdmin), "Nothing matches that search");
            return;
        }

        if (view == GuiView.Category)
        {
            DrawModList(CollectCategoryButtons(Buttons.Categories[currentGuiTab], isAdmin),
                    "Nothing here");
            return;
        }

        DrawHomeList(isAdmin);
    }

    private void DrawHomeList(bool isAdmin)
    {
        // The live event's kill switch, right here on the desktop: one
        // press to raise the sky countdown for the room, one press to
        // take it all down - no in-game menu needed.
        if (isAdmin)
        {
            ButtonInfo countdown = Buttons.GetIndex("Countdown");

            if (countdown != null)
            {
                bool   on    = countdown.enabled;
                string label = on
                        ? "  COUNTDOWN IS LIVE - PRESS TO END IT  "
                        : "  START THE COUNTDOWN LIVE EVENT  ";

                Rect rect = GUILayoutUtility.GetRect(GUIContent.none, rowStyle,
                        GUILayout.ExpandWidth(true), GUILayout.Height(44f));

                if (GUI.Button(rect, new GUIContent(label, countdown.toolTip),
                               on ? rowOnStyle : rowStyle))
                    SafeToggle(countdown);

                DrawRowChevron(rect);
                GUILayout.Space(RowGap);
            }
        }

        List<int> visible = [];

        for (int i = 0; i < Buttons.Categories.Length; i++)
        {
            ButtonCategory category = Buttons.Categories[i];

            if (category.adminOnly && !isAdmin)
                continue;

            if (CountVisibleButtons(category, isAdmin) == 0)
                continue;

            visible.Add(i);
        }

        if (visible.Count == 0)
        {
            GUILayout.Label("Nothing here", dimLabelStyle);

            return;
        }

        int maxPage = (visible.Count - 1) / RowsPerPage;

        if (listPage < 0)
            listPage = maxPage;
        else if (listPage > maxPage)
            listPage = 0;

        int start = listPage * RowsPerPage;
        int end   = Mathf.Min(visible.Count, start + RowsPerPage);

        for (int n = start; n < end; n++)
        {
            int            index    = visible[n];
            ButtonCategory category = Buttons.Categories[index];

            int mods = CountVisibleButtons(category, isAdmin);

            int on = 0;

            foreach (ButtonInfo button in category.buttons)
                if (button.enabled)
                    on++;

            string subtitle = $"{mods} mods" +
                              (on > 0 ? $"  ·  <color=#C79BFF>{on} on</color>" : "");

            string text = $"<b>{category.name}</b>\n" +
                          $"<size=10><color=#99AABB>{subtitle}</color></size>";

            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, rowStyle,
                    GUILayout.ExpandWidth(true), GUILayout.Height(RowH));

            if (GUI.Button(rect, new GUIContent(text, $"{mods} mods"), rowStyle))
            {
                currentGuiTab = index;
                view          = GuiView.Category;
                listPage      = 0;
                SuppressTooltipsBriefly();
            }

            DrawRowChevron(rect);
            GUILayout.Space(RowGap);
        }
    }

    private void DrawModList(List<ButtonInfo> buttons, string emptyText)
    {
        if (buttons.Count == 0)
        {
            GUILayout.Label(emptyText, dimLabelStyle);

            return;
        }

        int maxPage = (buttons.Count - 1) / RowsPerPage;

        if (listPage < 0)
            listPage = maxPage;
        else if (listPage > maxPage)
            listPage = 0;

        int start = listPage * RowsPerPage;
        int end   = Mathf.Min(buttons.Count, start + RowsPerPage);

        for (int i = start; i < end; i++)
        {
            DrawModRow(buttons[i]);
            GUILayout.Space(RowGap);
        }
    }

    private void DrawModRow(ButtonInfo button)
    {
        Rect rect = GUILayoutUtility.GetRect(GUIContent.none, rowStyle,
                GUILayout.ExpandWidth(true), GUILayout.Height(RowH));

        if (button.mode == ButtonMode.Incremental)
        {
            GUI.Box(rect, GUIContent.none, rowStyle);

            if (GUI.Button(new Rect(rect.x + 8f, rect.y + 8f, 46f, RowH - 16f),
                           new GUIContent("-", button.toolTip), pillStyle))
                SafeToggle(button, false);

            GUI.Label(new Rect(rect.x + 62f, rect.y, rect.width - 124f, RowH),
                    button.GetDisplayText(), rowLabelStyle);

            if (GUI.Button(new Rect(rect.xMax - 54f, rect.y + 8f, 46f, RowH - 16f),
                           new GUIContent("+", button.toolTip), pillStyle))
                SafeToggle(button, true);

            return;
        }

        // State rides on the fill: purple when it is on, dark when it is
        // off - and every row carries the mockup's chevron.
        if (GUI.Button(rect, new GUIContent(button.GetDisplayText(), button.toolTip),
                       button.enabled ? rowOnStyle : rowStyle))
            SafeToggle(button);

        DrawRowChevron(rect);
    }

    private void DrawRowChevron(Rect rect)
    {
        if (Event.current.type != EventType.Repaint)
            return;

        GUI.Label(new Rect(rect.xMax - 40f, rect.y, 24f, rect.height), ">", chevronStyle);
    }

    private void DrawFooter()
    {
        if (searchOpen)
        {
            GUILayout.BeginHorizontal();

            searchQuery = GUILayout.TextField(searchQuery, searchFieldStyle,
                    GUILayout.Height(32), GUILayout.ExpandWidth(true));

            if (GUILayout.Button(new GUIContent("X", "Clear the search"), pillStyle,
                                 GUILayout.Width(34), GUILayout.Height(32)))
            {
                searchQuery = "";
                SuppressTooltipsBriefly();
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        DrawSeparator();
        GUILayout.Space(6);

        GUILayout.BeginHorizontal();

        // U - home: the logo when it loaded, the letter otherwise.
        GUIContent home = logoTexture != null
                                  ? new GUIContent(logoTexture, "Back to the category list")
                                  : new GUIContent("U", "Back to the category list");

        if (GUILayout.Button(home, pillStyle, GUILayout.Width(46), GUILayout.Height(32)))
            GoHome();

        GUILayout.FlexibleSpace();

        if (GUILayout.Button(new GUIContent("< PREV", "Previous page"), pillStyle,
                             GUILayout.Width(110), GUILayout.Height(32)))
            listPage--;

        GUILayout.Space(12);

        if (GUILayout.Button(new GUIContent("NEXT >", "Next page"), pillStyle,
                             GUILayout.Width(110), GUILayout.Height(32)))
            listPage++;

        GUILayout.FlexibleSpace();

        if (GUILayout.Button(new GUIContent("S", "Search"), pillStyle,
                             GUILayout.Width(46), GUILayout.Height(32)))
        {
            searchOpen = !searchOpen;

            if (!searchOpen)
            {
                searchQuery = "";
                SuppressTooltipsBriefly();
            }
        }

        GUILayout.EndHorizontal();
    }

    private void GoHome()
    {
        view         = GuiView.Home;
        searchQuery  = "";
        searchOpen   = false;
        listPage     = 0;

        SuppressTooltipsBriefly();
    }

    private void SuppressTooltipsBriefly()
    {
        ignoreTooltipsUntil = Time.unscaledTime + 0.3f;
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

            if (MatchesSearch(button))
                buttons.Add(button);
        }

        return buttons;
    }

    // Matches what is actually printed on the button plus its tooltip, the same
    // way the VR menu's search does - matching only buttonText meant searching
    // for e.g. "8fps" or "lag" found nothing.
    private bool MatchesSearch(ButtonInfo button)
    {
        if (string.IsNullOrEmpty(searchQuery))
            return true;

        if (button.GetDisplayText().Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
            return true;

        return !string.IsNullOrEmpty(button.toolTip) &&
               button.toolTip.Contains(searchQuery, StringComparison.OrdinalIgnoreCase);
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
        // "Return to Settings", "Menu" and "Movement" retarget the VR menu's
        // category, which the desktop view does not follow - listing them here
        // would only offer rows that do nothing when clicked.
        if (button.buttonText == "Return to Main"      ||
            button.buttonText == "Return to Settings" ||
            button.buttonText == "Menu"               ||
            button.buttonText == "Movement")
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
        bool isAdmin = HamburburData.LocalAdminAccess;

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

    private GUIStyle MakeColoredLabel(Color color)
    {
        return new GUIStyle(labelStyle)
        {
            normal = { textColor = color },
        };
    }

    private void LogErrorOnce(string key, Exception e)
    {
        if (!loggedErrors.Add(key))
            return;

        Debug.LogError($"{Constants.Name} // DesktopGui error ({key}): {e}");
    }
}
