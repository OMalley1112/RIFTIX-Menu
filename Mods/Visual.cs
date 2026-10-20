using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using StupidTemplate.Classes;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace StupidTemplate.Mods;

public abstract class Visual
{
    private static readonly Vector3 sphereScaleHand = new(0.1f, 0.1f, 0.1f);
    private static readonly Vector3 sphereScaleHead = new(0.2f, 0.2f, 0.2f);
    private static readonly Vector3 boxEspScale     = new(0.2f, 0.4f, 0.2f);
    private static readonly Vector3 nametagOffset   = new(0f, 0.9f, 0f);

    private class ESPBoxData
    {
        public GameObject BoxObject;
        public Renderer   BoxRenderer;
        public VRRig      Rig;
    }

    private class ESPSkeletonData
    {
        public GameObject HeadObj;
        public Renderer   HeadRenderer;
        public VRRig      Rig;
        public GameObject LeftHandObj;
        public Renderer   LeftHandRenderer;
        public GameObject RightHandObj;
        public Renderer   RightHandRenderer;
    }

    private class NametagData
    {
        public GameObject    CanvasObject;
        public VRRig         Rig;
        public TextMeshPro   TextComponent;
    }

    private static bool IsTagged(VRRig rig) =>
            rig.Creator != null && GorillaGameModes.GameMode.LocalIsTagged(rig.Creator);

    private static Color RigColor(VRRig rig)
    {
        if (IsTagged(rig))
            return Color.red;

        if (rig.mainSkin != null && rig.mainSkin.sharedMaterial != null)
            return rig.mainSkin.sharedMaterial.color;

        return Color.white;
    }

    private static string GetFPS(VRRig player)
    {
        FieldInfo fieldInfo = AccessTools.Field(player.GetType(), "fps");

        return fieldInfo?.GetValue(player)?.ToString() ?? "0";
    }

    // Box ESP
    private static readonly Dictionary<int, ESPBoxData> ESPBoxes = new();
    private static          Material                    espMaterial;
    private static          float                       lastBoxCleanupTime;
    private static readonly List<int>                   boxRemoveBuffer = [];

    public static void BoxESP()
    {
        if (espMaterial == null)
            espMaterial = new Material(Shader.Find("GUI/Text Shader"));

        if (Time.time >= lastBoxCleanupTime + 1f)
        {
            lastBoxCleanupTime = Time.time;
            boxRemoveBuffer.Clear();

            foreach (KeyValuePair<int, ESPBoxData> pair in ESPBoxes)
            {
                ESPBoxData data = pair.Value;

                if (data == null || data.Rig == null || data.BoxObject == null ||
                    !data.Rig.gameObject.activeInHierarchy)
                {
                    if (data?.BoxObject != null)
                        Object.Destroy(data.BoxObject);

                    boxRemoveBuffer.Add(pair.Key);
                }
            }

            foreach (int key in boxRemoveBuffer)
                ESPBoxes.Remove(key);
        }

        IReadOnlyList<VRRig> rigs = VRRigCache.ActiveRigs;

        for (int i = 0; i < rigs.Count; i++)
        {
            VRRig rig = rigs[i];

            if (rig == null || rig.isLocal || !rig.gameObject.activeInHierarchy)
                continue;

            int id = rig.GetInstanceID();

            if (!ESPBoxes.TryGetValue(id, out ESPBoxData data) || data == null || data.BoxObject == null)
            {
                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "RIFTIX_BoxESP";

                Object.Destroy(box.GetComponent<Collider>());

                Rigidbody rb = box.GetComponent<Rigidbody>();

                if (rb != null)
                    Object.Destroy(rb);

                Renderer renderer = box.GetComponent<Renderer>();
                renderer.material        = espMaterial;
                box.transform.localScale = boxEspScale;

                data         = new ESPBoxData { Rig = rig, BoxObject = box, BoxRenderer = renderer, };
                ESPBoxes[id] = data;
            }

            if (data.BoxObject == null || data.BoxRenderer == null || rig.transform == null)
                continue;

            data.BoxObject.transform.position = rig.transform.position;
            data.BoxObject.transform.rotation = rig.transform.rotation;
            data.BoxRenderer.material.color   = RigColor(rig);
        }
    }

    public static void CleanupBoxEsp()
    {
        foreach (ESPBoxData data in ESPBoxes.Values)
            if (data?.BoxObject != null)
                Object.Destroy(data.BoxObject);

        ESPBoxes.Clear();

        if (espMaterial != null)
        {
            Object.Destroy(espMaterial);
            espMaterial = null;
        }
    }

    // Skeleton ESP
    private static readonly Dictionary<int, ESPSkeletonData> ESPSkeletons = new();
    private static          Material                         skeletonEspMaterial;
    private static          float                            lastSkeletonCleanupTime;
    private static readonly List<int>                        skeletonRemoveBuffer = [];

    public static void SkeletonESP()
    {
        if (skeletonEspMaterial == null)
            skeletonEspMaterial = new Material(Shader.Find("GUI/Text Shader"));

        if (Time.time >= lastSkeletonCleanupTime + 1f)
        {
            lastSkeletonCleanupTime = Time.time;
            skeletonRemoveBuffer.Clear();

            foreach (KeyValuePair<int, ESPSkeletonData> pair in ESPSkeletons)
            {
                ESPSkeletonData data = pair.Value;

                if (data == null || data.Rig == null || !data.Rig.gameObject.activeInHierarchy)
                {
                    if (data?.HeadObj != null)
                        Object.Destroy(data.HeadObj);

                    if (data?.LeftHandObj != null)
                        Object.Destroy(data.LeftHandObj);

                    if (data?.RightHandObj != null)
                        Object.Destroy(data.RightHandObj);

                    skeletonRemoveBuffer.Add(pair.Key);
                }
            }

            foreach (int key in skeletonRemoveBuffer)
                ESPSkeletons.Remove(key);
        }

        IReadOnlyList<VRRig> rigs = VRRigCache.ActiveRigs;

        for (int i = 0; i < rigs.Count; i++)
        {
            VRRig rig = rigs[i];

            if (rig == null || !rig.gameObject.activeInHierarchy)
                continue;

            int id = rig.isLocal ? 999999 : rig.GetInstanceID();

            if (!ESPSkeletons.TryGetValue(id, out ESPSkeletonData data) || data == null)
            {
                GameObject head     = null;
                Renderer   headRend = null;

                if (!rig.isLocal)
                {
                    head      = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    head.name = "RIFTIX_SkeletonESP_Head";

                    Object.Destroy(head.GetComponent<Collider>());

                    head.transform.localScale = sphereScaleHead;
                    headRend                  = head.GetComponent<Renderer>();
                    headRend.material         = skeletonEspMaterial;
                }

                GameObject leftHand  = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                GameObject rightHand = GameObject.CreatePrimitive(PrimitiveType.Sphere);

                leftHand.name  = "RIFTIX_SkeletonESP_Left";
                rightHand.name = "RIFTIX_SkeletonESP_Right";

                Object.Destroy(leftHand.GetComponent<Collider>());
                Object.Destroy(rightHand.GetComponent<Collider>());

                leftHand.transform.localScale  = sphereScaleHand;
                rightHand.transform.localScale = sphereScaleHand;

                Renderer leftRend  = leftHand.GetComponent<Renderer>();
                Renderer rightRend = rightHand.GetComponent<Renderer>();

                leftRend.material  = skeletonEspMaterial;
                rightRend.material = skeletonEspMaterial;

                data = new ESPSkeletonData
                {
                        Rig               = rig,
                        HeadObj           = head,
                        HeadRenderer      = headRend,
                        LeftHandObj       = leftHand,
                        LeftHandRenderer  = leftRend,
                        RightHandObj      = rightHand,
                        RightHandRenderer = rightRend,
                };

                ESPSkeletons[id] = data;
            }

            if (rig.head == null || rig.leftHand == null || rig.rightHand == null)
                continue;

            if (!rig.isLocal && data.HeadObj != null && rig.head.rigTarget != null)
            {
                data.HeadObj.transform.position = rig.head.rigTarget.transform.position;
                data.HeadObj.transform.rotation = rig.head.rigTarget.transform.rotation;
            }

            if (data.LeftHandObj != null && rig.leftHand.rigTarget != null)
            {
                data.LeftHandObj.transform.position = rig.leftHand.rigTarget.transform.position;
                data.LeftHandObj.transform.rotation = rig.leftHand.rigTarget.transform.rotation;
            }

            if (data.RightHandObj != null && rig.rightHand.rigTarget != null)
            {
                data.RightHandObj.transform.position = rig.rightHand.rigTarget.transform.position;
                data.RightHandObj.transform.rotation = rig.rightHand.rigTarget.transform.rotation;
            }

            Color color = RigColor(rig);

            if (data.HeadRenderer != null)
                data.HeadRenderer.material.color = color;

            if (data.LeftHandRenderer != null)
                data.LeftHandRenderer.material.color = color;

            if (data.RightHandRenderer != null)
                data.RightHandRenderer.material.color = color;
        }
    }

    public static void DisableSkeletonESP()
    {
        foreach (ESPSkeletonData data in ESPSkeletons.Values)
        {
            if (data == null)
                continue;

            if (data.HeadObj != null)
                Object.Destroy(data.HeadObj);

            if (data.LeftHandObj != null)
                Object.Destroy(data.LeftHandObj);

            if (data.RightHandObj != null)
                Object.Destroy(data.RightHandObj);
        }

        ESPSkeletons.Clear();

        if (skeletonEspMaterial != null)
        {
            Object.Destroy(skeletonEspMaterial);
            skeletonEspMaterial = null;
        }
    }

    // Nametags
    private static readonly Dictionary<int, NametagData> ActiveNametags = new();
    private static          float                        lastNametagCleanupTime;
    private static readonly List<int>                   nametagRemoveBuffer = [];

    public static void Nametags()
    {
        if (Time.time >= lastNametagCleanupTime + 1f)
        {
            lastNametagCleanupTime = Time.time;
            nametagRemoveBuffer.Clear();

            foreach (KeyValuePair<int, NametagData> pair in ActiveNametags)
            {
                NametagData data = pair.Value;

                if (data == null || data.Rig == null || data.CanvasObject == null ||
                    !data.Rig.gameObject.activeInHierarchy)
                {
                    if (data?.CanvasObject != null)
                        Object.Destroy(data.CanvasObject);

                    nametagRemoveBuffer.Add(pair.Key);
                }
            }

            foreach (int key in nametagRemoveBuffer)
                ActiveNametags.Remove(key);
        }

        IReadOnlyList<VRRig> rigs = VRRigCache.ActiveRigs;

        for (int i = 0; i < rigs.Count; i++)
        {
            VRRig rig = rigs[i];

            if (rig == null || rig.isLocal || !rig.gameObject.activeInHierarchy)
                continue;

            int id = rig.GetInstanceID();

            if (!ActiveNametags.TryGetValue(id, out NametagData data) || data == null || data.CanvasObject == null)
            {
                GameObject canvasObj = new("RIFTIX_Nametag_Canvas");

                TextMeshPro textMesh = canvasObj.AddComponent<TextMeshPro>();
                textMesh.fontSize                = 2f;
                textMesh.alignment               = TextAlignmentOptions.Center;
                textMesh.rectTransform.sizeDelta = new Vector2(4f, 1f);

                data = new NametagData
                {
                        Rig           = rig,
                        CanvasObject  = canvasObj,
                        TextComponent = textMesh,
                };

                ActiveNametags[id] = data;
            }

            if (data.CanvasObject == null || data.TextComponent == null || rig.transform == null ||
                rig.head == null)
                continue;

            Transform headTransform = rig.head.rigTarget != null ? rig.head.rigTarget.transform : rig.transform;
            data.CanvasObject.transform.position = headTransform.position + nametagOffset;

            Camera cam = Camera.main;

            if (cam != null)
            {
                Vector3 directionToCam = cam.transform.position - data.CanvasObject.transform.position;
                data.CanvasObject.transform.rotation = Quaternion.LookRotation(-directionToCam, Vector3.up);
            }

            string photonNick = "Unknown";
            string playerFps  = "0";

            NetPlayer creator = rig.Creator;

            if (creator != null)
            {
                photonNick = creator.NickName;
                playerFps  = GetFPS(rig);
            }

            string hexColor = "#FFFFFF";

            if (IsTagged(rig))
                hexColor = "#FF3333";
            else if (rig.mainSkin != null && rig.mainSkin.sharedMaterial != null)
                hexColor = "#" + ColorUtility.ToHtmlStringRGB(rig.mainSkin.sharedMaterial.color);

            data.TextComponent.text = $"<color={hexColor}>{photonNick}</color>\n<size=75%>FPS: {playerFps}</size>";

            if (VRRig.LocalRig != null && VRRig.LocalRig.playerText1 != null &&
                VRRig.LocalRig.playerText1.font != null)
                data.TextComponent.font = VRRig.LocalRig.playerText1.font;
        }
    }

    public static void DisableNametags()
    {
        foreach (NametagData data in ActiveNametags.Values)
            if (data?.CanvasObject != null)
                Object.Destroy(data.CanvasObject);

        ActiveNametags.Clear();
    }

    // Fullbright
    private static Color                       cachedAmbientColor;
    private static bool                        cachedFog;
    private static AmbientMode                 cachedAmbientMode;
    private static bool                        fullbrightCached;

    public static void Fullbright()
    {
        if (!fullbrightCached)
        {
            cachedAmbientColor = RenderSettings.ambientLight;
            cachedFog          = RenderSettings.fog;
            cachedAmbientMode  = RenderSettings.ambientMode;
            fullbrightCached   = true;
        }

        RenderSettings.fog          = false;
        RenderSettings.ambientMode  = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white * 1.5f;
    }

    public static void FullbrightOff()
    {
        if (!fullbrightCached)
            return;

        RenderSettings.fog          = cachedFog;
        RenderSettings.ambientMode  = cachedAmbientMode;
        RenderSettings.ambientLight = cachedAmbientColor;
        fullbrightCached            = false;
    }

    // Tracers
    private static readonly Dictionary<int, LineRenderer> tracerLines = new();
    private static          Material                      tracerMaterial;

    public static void Tracers()
    {
        if (tracerMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("GUI/Text Shader");

            if (shader == null)
                return;

            tracerMaterial = new Material(shader);
        }

        IReadOnlyList<VRRig> rigs = VRRigCache.ActiveRigs;

        VRRig localRig = VRRig.LocalRig;

        if (localRig == null || localRig.headConstraint == null)
            return;

        Transform head = localRig.headConstraint;

        for (int i = 0; i < rigs.Count; i++)
        {
            VRRig rig = rigs[i];

            if (rig == null || rig.isLocal || !rig.gameObject.activeInHierarchy)
                continue;

            int id = rig.GetInstanceID();

            if (!tracerLines.TryGetValue(id, out LineRenderer line) || line == null)
            {
                GameObject go = new("RIFTIX_Tracer");
                line = go.AddComponent<LineRenderer>();
                line.useWorldSpace  = true;
                line.sharedMaterial = tracerMaterial;
                line.positionCount  = 2;
                line.startWidth     = 0.012f;
                line.endWidth       = 0.004f;
                line.numCapVertices = 4;

                tracerLines[id] = line;
            }

            Transform target = rig.headMesh != null ? rig.headMesh.transform : rig.transform;

            line.SetPosition(0, head.position);
            line.SetPosition(1, target.position);

            Color color = RigColor(rig);
            color.a = 1f;

            line.startColor = color;
            line.endColor   = color;
        }
    }

    public static void DisableTracers()
    {
        foreach (LineRenderer line in tracerLines.Values)
            if (line != null)
                Object.Destroy(line.gameObject);

        tracerLines.Clear();
    }
}
