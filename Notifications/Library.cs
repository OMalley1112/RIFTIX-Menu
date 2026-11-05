using System;
using System.Linq;
using BepInEx;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.UI;
using static StupidTemplate.Menu.Settings;

namespace StupidTemplate.Notifications;

[BepInPlugin("org.gorillatag.lars.notifications2", "NotificationLibrary", "1.0.5")]
public class NotifiLib : BaseUnityPlugin
{

    public static int NoticationThreshold = 30;

    public static string PreviousNotification;

    private static Text notificationText;

    public static bool IsEnabled = true;

    private readonly Material alertText = new(Shader.Find("GUI/Text Shader"));

    private readonly int notificationDecayTime = 144;

    private bool hasInit;

    private GameObject hudObj;

    private GameObject hudObj2;

    private GameObject mainCamera;

    private string newtext;

    private int notificationDecayTimeCounter;

    private string[] notifilines;

    private Text testtext;

    // Fancy notification panel: rounded card + accent border + stripe that
    // sit behind the text, plus a soft drop shadow on the glyphs.
    private static Image  cardImage;
    private static Image  cardBorder;
    private static Image  accentBar;
    private static Shadow textShadow;

    // Timestamps for the pop punch and card colour flash.
    private static float punchStart = -10f;
    private static float flashStart = -10f;

    private static readonly Color cardColor   = new Color32(20, 15, 32, 235);
    private static readonly Color accentColor = new Color32(199, 155, 255, 255);

    private void Awake() => Logger.LogInfo("Plugin NotificationLibrary is loaded!");

    private void LateUpdate()
    {
        if (!hasInit)
        {
            if (GTPlayer.Instance?.mainCamera == null)
                return;

            Init();

            hasInit = true;
        }

        if (hudObj2    == null ||
            mainCamera == null ||
            testtext   == null)
        {
            return;
        }

        hudObj2.transform.position =
                mainCamera.transform.position;

        hudObj2.transform.rotation =
                mainCamera.transform.rotation;

        UpdateFancyCard();

        if (string.IsNullOrEmpty(testtext.text))
        {
            notificationDecayTimeCounter = 0;

            return;
        }

        notificationDecayTimeCounter++;

        if (notificationDecayTimeCounter <= notificationDecayTime)
            return;

        notifilines                  = null;
        newtext                      = "";
        notificationDecayTimeCounter = 0;

        notifilines =
                testtext.text
                        .Split(Environment.NewLine.ToCharArray())
                        .Skip(1)
                        .ToArray();

        foreach (string text in notifilines)
        {
            if (!string.IsNullOrEmpty(text))
                newtext += text + "\n";
        }

        testtext.text = newtext;
    }

    private void Init()
    {
        mainCamera   = GTPlayer.Instance.mainCamera.gameObject;
        hudObj       = new GameObject();
        hudObj2      = new GameObject();
        hudObj2.name = "NOTIFICATIONLIB_HUD_OBJ";
        hudObj.name  = "NOTIFICATIONLIB_HUD_OBJ";
        hudObj.AddComponent<Canvas>();
        hudObj.AddComponent<CanvasScaler>();
        hudObj.AddComponent<GraphicRaycaster>();
        hudObj.GetComponent<Canvas>().enabled                    = true;
        hudObj.GetComponent<Canvas>().renderMode                 = RenderMode.WorldSpace;
        hudObj.GetComponent<Canvas>().worldCamera                = mainCamera.GetComponent<Camera>();
        hudObj.GetComponent<RectTransform>().sizeDelta           = new Vector2(5f, 5f);
        hudObj.GetComponent<RectTransform>().position            = new Vector3(mainCamera.transform.position.x, mainCamera.transform.position.y, mainCamera.transform.position.z);
        hudObj.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
        hudObj2.transform.position                               = new Vector3(mainCamera.transform.position.x, mainCamera.transform.position.y, mainCamera.transform.position.z - 4.6f);
        hudObj.transform.parent                                  = hudObj2.transform;
        hudObj.GetComponent<RectTransform>().localPosition       = new Vector3(0f, 0f, 1.6f);
        Vector3 eulerAngles = hudObj.GetComponent<RectTransform>().rotation.eulerAngles;
        eulerAngles.y                                 = -270f;
        hudObj.transform.localScale                   = new Vector3(1f, 1f, 1f);
        hudObj.GetComponent<RectTransform>().rotation = Quaternion.Euler(eulerAngles);
        testtext = new GameObject
        {
                transform =
                {
                        parent = hudObj.transform,
                },
        }.AddComponent<Text>();

        testtext.text                        = "";
        testtext.fontSize                    = 30;
        testtext.font                        = currentFont;
        testtext.rectTransform.sizeDelta     = new Vector2(450f, 210f);
        testtext.alignment                   = TextAnchor.LowerLeft;
        testtext.rectTransform.localScale    = new Vector3(0.00333333333f, 0.00333333333f, 0.33333333f);
        testtext.rectTransform.localPosition = new Vector3(-1f,            -1f,            -0.5f);
        testtext.material                    = alertText;

        // Rounded panel behind the notifications: border ring, dark card,
        // accent stripe, and a drop shadow on the text itself.
        Sprite cardSprite = CreateRoundedSprite(64, 22);

        cardBorder = CreateCardObject(
                hudObj.transform,
                cardSprite,
                new Color32(199, 155, 255, 110),
                new Vector2(480f, 234f));

        cardImage = CreateCardObject(
                hudObj.transform,
                cardSprite,
                cardColor,
                new Vector2(470f, 224f));

        GameObject barObject =
                new()
                {
                        transform =
                        {
                                parent = cardImage.transform,
                        },
                };

        accentBar = barObject.AddComponent<Image>();
        accentBar.color                     = accentColor;
        accentBar.raycastTarget             = false;
        accentBar.rectTransform.sizeDelta   = new Vector2(12f, 224f);
        accentBar.rectTransform.localPosition = new Vector3(-225f, 0f, 0f);

        textShadow = testtext.gameObject.AddComponent<Shadow>();
        textShadow.effectColor     = new Color(0f, 0f, 0f, 0.75f);
        textShadow.effectDistance  = new Vector2(2f, -2f);
        textShadow.useGraphicAlpha = true;

        // Text draws on top of the panel.
        testtext.transform.SetAsLastSibling();

        notificationText = testtext;
    }

    // Panel image sharing the text's exact transform so it sits flush behind it.
    private Image CreateCardObject(Transform parent, Sprite sprite, Color color, Vector2 size)
    {
        GameObject cardObject =
                new()
                {
                        transform =
                        {
                                parent = parent,
                        },
                };

        Image image = cardObject.AddComponent<Image>();

        image.sprite        = sprite;
        image.type          = Image.Type.Sliced;
        image.color         = color;
        image.raycastTarget = false;

        image.rectTransform.localPosition = testtext.rectTransform.localPosition;
        image.rectTransform.localRotation = testtext.rectTransform.localRotation;
        image.rectTransform.localScale    = testtext.rectTransform.localScale;
        image.rectTransform.sizeDelta     = size;

        return image;
    }

    // Soft rounded-square sprite used by the notification card.
    private static Sprite CreateRoundedSprite(int size, int radius)
    {
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false);

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Distance to the inner rect; zero in the straight-edge zones,
                // real distance only in the four corners.
                float px = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float py = Mathf.Clamp(y + 0.5f, radius, size - radius);

                float distance =
                        Vector2.Distance(
                                new Vector2(x + 0.5f, y + 0.5f),
                                new Vector2(px,             py));

                float alpha =
                        Mathf.Clamp01(radius - distance + 0.5f);

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                1f, // pixels per unit: sprite units match canvas units
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
    }

    // Pop punch on the HUD and accent flash on the card, plus visibility.
    private void UpdateFancyCard()
    {
        if (cardImage == null)
            return;

        bool fancy  = fancyNotifications;
        bool hasText = !string.IsNullOrEmpty(testtext.text);
        bool show   = fancy && hasText;

        cardImage.enabled = show;

        if (cardBorder != null)
            cardBorder.enabled = show;

        if (accentBar != null)
            accentBar.enabled = show;

        if (textShadow != null)
            textShadow.enabled = fancy;

        // Squeeze the whole HUD when a notification arrives, then ease back.
        float punchT =
                (Time.unscaledTime - punchStart) /
                0.25f;

        if (hudObj != null)
        {
            if (punchT >= 1f)
                hudObj.transform.localScale = Vector3.one;
            else
            {
                float strength =
                        1f +
                        0.15f * (1f - punchT) * (1f - punchT);

                hudObj.transform.localScale = Vector3.one * strength;
            }
        }

        if (!fancy)
            return;

        // Flash the card from accent purple back to its dark resting colour.
        float flashT =
                (Time.unscaledTime - flashStart) /
                0.45f;

        cardImage.color =
                flashT >= 1f
                        ? cardColor
                        : Color.Lerp(accentColor, cardColor, flashT);
    }

    public static void SendNotification(string notificationText)
    {
        if (!disableNotifications)
        {
            try
            {
                if (IsEnabled && PreviousNotification != notificationText)
                {
                    if (!notificationText.Contains(Environment.NewLine))
                    {
                        notificationText += Environment.NewLine;
                    }

                    NotifiLib.notificationText.text            += notificationText;
                    NotifiLib.notificationText.supportRichText =  true;
                    PreviousNotification                       =  notificationText;

                    if (fancyNotifications)
                    {
                        punchStart = Time.unscaledTime;
                        flashStart = Time.unscaledTime;
                    }
                }
            }
            catch
            {
                Debug.LogError("Notification failed, object probably nil due to third person ; " + notificationText);
            }
        }
    }

    public static void ClearAllNotifications() =>
            //NotifiLib.NotifiText.text = "<color=grey>[</color><color=green>SUCCESS</color><color=grey>]</color> <color=white>Notifications cleared.</color>" + Environment.NewLine;
            notificationText.text = "";

    public static void ClearPastNotifications(int amount)
    {
        string text = notificationText.text.Split(Environment.NewLine.ToCharArray()).Skip(amount).ToArray().Where(text2 => text2 != "").Aggregate("", (current, text2) => current + text2 + "\n");

        notificationText.text = text;
    }
}