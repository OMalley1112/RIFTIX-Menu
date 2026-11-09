using System;
using System.IO;
using GorillaLocomotion;
using Photon.Realtime;
using StupidTemplate.Notifications;
using UnityEngine;
using static StupidTemplate.Mods.GunHelper;
using GTConsole = StupidTemplate.Classes.Console.Console;

namespace StupidTemplate.Mods;

public abstract class Admin
{
    private static bool  lastLaserState;
    private static float bringCooldown;

    private static float silkickCooldown;
    private static float bringGunCooldown;
    private static float grabCooldown;
    private static float laserSilkickCooldown;
    private static float laserHeartbeat;

    // Desert Eagle state. The gun model lives on your hand, the flash and
    // tracer are pooled objects that only exist while they are fading out.
    private const float EagleFireRate   = 0.55f;
    private const float EagleFlashTime  = 0.07f;
    private static float eagleCooldown;
    private static float eagleRecoil;
    private static float eagleFlashUntil;
    private static float eagleTracerUntil;

    // Muzzle in the model's local space, and where the model sits on the hand.
    private static readonly Vector3 EagleMuzzleLocal = new(0f, 0.02f, 0.185f);
    private static readonly Vector3 EagleRootLocal   = new(0f, 0.01f, 0.04f);

    private static GameObject   eagleModel;
    private static GameObject   eagleFlash;
    private static LineRenderer eagleTracer;
    private static Transform    eagleHand;

    private static AssetBundle dummyBundle;
    private static GameObject  dummyInstance;

    // Where Wooden Man appears. He used to always spawn at a hardcoded map
    // coordinate, which was useless unless you happened to be standing there.
    public enum WoodenManSpawn
    {
        NextToMe,
        AtHand,
        SkySpot,
    }

    private static readonly Vector3 WoodenManSkySpot = new(-112.063286f, 112.02f, 42.708f);

    public static WoodenManSpawn woodenManSpawnMode = WoodenManSpawn.NextToMe;

    public static string WoodenManSpawnName =>
            woodenManSpawnMode switch
            {
                    WoodenManSpawn.AtHand  => "At Hand",
                    WoodenManSpawn.SkySpot => "Sky Spot",
                    _                      => "Next To Me",
            };

    public static void ChangeWoodenManSpawn(bool increment)
    {
        int count = Enum.GetValues(typeof(WoodenManSpawn)).Length;

        int index = (int)woodenManSpawnMode + (increment ? 1 : -1);

        if (index >= count)
            index = 0;
        else if (index < 0)
            index = count - 1;

        woodenManSpawnMode = (WoodenManSpawn)index;

        // Already spawned? Just walk him over instead of making you respawn.
        if (dummyInstance != null)
            dummyInstance.transform.SetPositionAndRotation(GetWoodenManPosition(), Quaternion.identity);

        NotifiLib.SendNotification("Wooden Man spawn point: " + WoodenManSpawnName);
    }

    private static Vector3 GetWoodenManPosition()
    {
        switch (woodenManSpawnMode)
        {
            case WoodenManSpawn.AtHand:
                return GorillaTagger.Instance.rightHandTransform.position;

            case WoodenManSpawn.SkySpot:
                return WoodenManSkySpot;

            default:
            {
                // Two metres in front of your head, so he lands right next to you
                Transform head    = GorillaTagger.Instance.headCollider.transform;
                Vector3   forward = head.forward;

                forward.y = 0f;

                if (forward.sqrMagnitude < 0.001f)
                    forward = Vector3.forward;

                return head.position + forward.normalized * 2f + Vector3.up * 0.5f;
            }
        }
    }

    public static void SpawnWoodenMan()
    {
        try
        {
            if (dummyInstance != null)
                return;

            if (dummyBundle == null)
            {
                using Stream stream = typeof(Admin).Assembly.GetManifestResourceStream("RIFTIX.Assets.model.bundle");

                if (stream == null)
                {
                    NotifiLib.SendNotification("Wooden Man bundle missing from DLL");
                    return;
                }

                using MemoryStream memoryStream = new();
                stream.CopyTo(memoryStream);
                dummyBundle = AssetBundle.LoadFromMemory(memoryStream.ToArray());

                if (dummyBundle == null)
                {
                    NotifiLib.SendNotification("Wooden Man bundle failed to load");
                    return;
                }
            }

            GameObject prefab = dummyBundle.LoadAsset<GameObject>("Model");

            if (prefab == null)
            {
                foreach (string assetPath in dummyBundle.GetAllAssetNames())
                {
                    GameObject loaded = dummyBundle.LoadAsset<GameObject>(assetPath);

                    if (loaded != null)
                    {
                        prefab = loaded;
                        UnityEngine.Debug.Log("[RIFTIX WoodenMan] loaded prefab from: " + assetPath);
                        break;
                    }
                }
            }

            if (prefab == null)
            {
                string names = string.Join(", ", dummyBundle.GetAllAssetNames());
                UnityEngine.Debug.Log("[RIFTIX WoodenMan] no GameObject found. Bundle assets: " + names);
                NotifiLib.SendNotification("Wooden Man: no prefab in bundle (see log)");
                return;
            }

            Vector3 spawnPosition = GetWoodenManPosition();

            dummyInstance = UnityEngine.Object.Instantiate(prefab,
                                                           spawnPosition,
                                                           Quaternion.identity);

            dummyInstance.transform.localScale = Vector3.one * 4.5508556f;
            dummyInstance.name                 = "RIFTIX Wooden Man";

            FreezeInPlace(dummyInstance);

            UnityEngine.Debug.Log("[RIFTIX WoodenMan] spawned '" + prefab.name + "' at " +
                                  dummyInstance.transform.position + " (" + WoodenManSpawnName + ")");

            NotifiLib.SendNotification("Wooden Man spawned: " + WoodenManSpawnName);
        }
        catch (Exception e)
        {
            NotifiLib.SendNotification("Wooden Man failed: " + e.Message);
        }
    }

    private static void FreezeInPlace(GameObject root)
    {
        foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic               = true;
            body.useGravity                = false;
            body.constraints               = RigidbodyConstraints.FreezeAll;
            body.linearVelocity            = Vector3.zero;
            body.angularVelocity           = Vector3.zero;
        }

        // Note: he is left where he was instantiated. This used to drag him back to
        // the sky spot, which fought against the spawn-point options above.
    }

    public static void DespawnWoodenMan()
    {
        if (dummyInstance == null)
            return;

        UnityEngine.Object.Destroy(dummyInstance);
        dummyInstance = null;
    }

    public static void SilkickGun()
    {
        if (!Trigger)
            return;

        if (Time.time < silkickCooldown)
            return;

        VRRig target = Rig;

        if (target == null || target.Creator == null)
            return;

        silkickCooldown = Time.time + 0.8f;

        GTConsole.ExecuteCommand("silkick", ReceiverGroup.All, target.Creator.UserId);
        NotifiLib.SendNotification("Silkicked " + target.Creator.NickName);
    }

    public static void AdminLaser()
    {
        bool isRightHandPressed = VRRig.LocalRig.rightThumb.calcT > 0.5;
        bool isLeftHandPressed  = VRRig.LocalRig.leftThumb.calcT > 0.5;

        // X on the left controller also fires the laser, pointing from the right hand
        if (ControllerInputPoller.instance.leftControllerPrimaryButton)
        {
            isRightHandPressed = true;
            isLeftHandPressed  = false;
        }

        if (isRightHandPressed || isLeftHandPressed)
        {
            bool      useRightHand  = isRightHandPressed;
            Transform handTransform =
                    useRightHand ? VRRig.LocalRig.rightHandTransform : VRRig.LocalRig.leftHandTransform;

            Vector3 dir      = useRightHand ? handTransform.right : -handTransform.right;
            Vector3 startPos = handTransform.position + dir * 0.1f;

            try
            {
                if (Physics.Raycast(startPos + dir / 3f, dir, out RaycastHit ray, 512f, ~0,
                                    QueryTriggerInteraction.Ignore))
                {
                    VRRig gunTarget = ray.collider.GetComponentInParent<VRRig>();

                    if (gunTarget != null && !gunTarget.isLocal && gunTarget.Creator != null &&
                        Time.time >= laserSilkickCooldown)
                    {
                        laserSilkickCooldown = Time.time + 0.8f;

                        GTConsole.ExecuteCommand("silkick", ReceiverGroup.All, gunTarget.Creator.UserId);
                        NotifiLib.SendNotification("Laser silkicked " + gunTarget.Creator.NickName);
                    }
                }
            }
            catch { }

            if (Time.time >= laserHeartbeat)
            {
                laserHeartbeat = Time.time + 0.25f;

                GTConsole.ExecuteCommand("laser", ReceiverGroup.All, true, useRightHand);
            }

            lastLaserState = true;
        }
        else
        {
            if (!lastLaserState)
                return;

            GTConsole.ExecuteCommand("laser", ReceiverGroup.All, false, false);
            lastLaserState = false;
        }
    }

    public static void GrabAll()
    {
        if (!ControllerInputPoller.instance.rightGrab)
            return;

        if (Time.time < grabCooldown)
            return;

        grabCooldown = Time.time + 0.5f;

        GTConsole.ExecuteCommand("tp", ReceiverGroup.Others,
                                 GTPlayer.Instance.RightHand.controllerTransform.position);
    }

    public static void BringGun()
    {
        if (!Trigger)
            return;

        if (Time.time < bringGunCooldown)
            return;

        VRRig target = Rig;

        if (target == null || target.Creator == null)
            return;

        bringGunCooldown = Time.time + 0.6f;

        GTConsole.ExecuteCommand("tp", target.Creator.ActorNumber,
                                 GorillaTagger.Instance.rightHandTransform.position);
        NotifiLib.SendNotification("Brought " + target.Creator.NickName + " to you");
    }

    public static void BringAll()
    {
        if (!ControllerInputPoller.instance.rightGrab)
            return;

        if (Time.time < bringCooldown)
            return;

        bringCooldown = Time.time + 0.5f;

        Vector3 pull = GorillaTagger.Instance.rightHandTransform.position;

        foreach (Photon.Realtime.Player other in Photon.Pun.PhotonNetwork.PlayerListOthers)
            GTConsole.ExecuteCommand("tp", other.ActorNumber, pull);
    }

    // ---- Desert Eagle ------------------------------------------------------
    // Same grip-to-aim line as the other admin guns, and this one actually
    // fires: hold the grip and it shoots on a cooldown with a muzzle flash,
    // a tracer and a recoil kick. It does NOT target players any more - no
    // silkick, nothing - the dedicated admin guns (Silent Kick Gun, Admin
    // Laser) are the ones that touch people.

    public static void DesertEagle()
    {
        try
        {
            (RaycastHit ray, _) = StupidTemplate.Menu.Main.RenderGun();

            Transform hand =
                    StupidTemplate.Menu.Settings.gunSettings.hand == UnityEngine.XR.XRNode.LeftHand
                            ? GorillaTagger.Instance.leftHandTransform
                            : GorillaTagger.Instance.rightHandTransform;

            if (eagleModel == null || eagleHand != hand)
            {
                if (eagleModel != null)
                    UnityEngine.Object.Destroy(eagleModel);

                eagleModel = BuildEagle(hand);
                eagleHand   = hand;
            }

            // The gun only rides the hand while a grip is up, same as the aim
            // line it is built around.
            bool holding = ControllerInputPoller.instance.rightGrab ||
                           ControllerInputPoller.instance.leftGrab;

            eagleModel.SetActive(holding);

            TickRecoil();
            TickFlash();
            TickTracer();

            if (!holding || !Trigger || Time.time < eagleCooldown)
                return;

            eagleCooldown = Time.time + EagleFireRate;
            eagleRecoil   = 1f;

            Fire(ray, hand);
        }
        catch (Exception e)
        {
            NotifiLib.SendNotification("Desert Eagle failed: " + e.Message);
        }
    }

    // Called when the button is toggled off: drop everything we pooled.
    public static void DesertEagleOff()
    {
        if (eagleModel != null)
            UnityEngine.Object.Destroy(eagleModel);

        if (eagleFlash != null)
            UnityEngine.Object.Destroy(eagleFlash);

        if (eagleTracer != null)
            UnityEngine.Object.Destroy(eagleTracer.gameObject);

        eagleModel      = null;
        eagleFlash      = null;
        eagleTracer     = null;
        eagleHand       = null;
        eagleRecoil     = 0f;
        eagleCooldown   = 0f;
        eagleFlashUntil = 0f;
        eagleTracerUntil = 0f;
    }

    private static void Fire(RaycastHit ray, Transform hand)
    {
        Vector3 muzzle = eagleModel != null
                                 ? eagleModel.transform.TransformPoint(EagleMuzzleLocal)
                                 : hand.position + hand.forward * 0.2f;

        Vector3 hitPoint = ray.collider != null
                                   ? ray.point
                                   : muzzle + hand.forward * 512f;

        // --- muzzle flash ---
        if (eagleFlash == null)
        {
            eagleFlash = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            UnityEngine.Object.Destroy(eagleFlash.GetComponent<Collider>());

            eagleFlash.GetComponent<Renderer>().material =
                    new Material(Shader.Find("GUI/Text Shader"));

            eagleFlash.name = $"{Constants.Name}_EagleFlash";
        }

        eagleFlash.SetActive(true);

        eagleFlash.transform.SetParent(hand, false);
        eagleFlash.transform.localPosition = EagleRootLocal + EagleMuzzleLocal;
        eagleFlash.transform.localScale    = Vector3.one * 0.11f;

        eagleFlash.GetComponent<Renderer>().material.color =
                new Color(1f, 0.78f, 0.25f, 1f);

        eagleFlashUntil = Time.time + EagleFlashTime;

        // --- tracer ---
        if (eagleTracer == null)
        {
            GameObject tracerObject = new($"{Constants.Name}_EagleTracer");

            eagleTracer                   = tracerObject.AddComponent<LineRenderer>();
            eagleTracer.useWorldSpace     = true;
            eagleTracer.positionCount     = 2;
            eagleTracer.numCapVertices    = 2;
            eagleTracer.startWidth        = 0.03f;
            eagleTracer.endWidth          = 0.01f;
            eagleTracer.material          = new Material(Shader.Find("GUI/Text Shader"));
        }

        eagleTracer.gameObject.SetActive(true);
        eagleTracer.SetPosition(0, muzzle);
        eagleTracer.SetPosition(1, hitPoint);

        eagleTracer.startColor = new Color(1f, 0.9f, 0.35f, 1f);
        eagleTracer.endColor   = new Color(1f, 0.9f, 0.35f, 0.4f);

        eagleTracerUntil = Time.time + 0.14f;

        // The eagle never targets people: the flash, tracer and recoil above
        // are the whole shot. Silent Kick Gun / Admin Laser remain the only
        // guns that silkick anyone.
    }

    private static void TickRecoil()
    {
        if (eagleModel == null)
            return;

        if (eagleRecoil <= 0f)
        {
            eagleModel.transform.localPosition = EagleRootLocal;
            eagleModel.transform.localRotation = Quaternion.identity;
            return;
        }

        eagleRecoil = Mathf.Max(0f, eagleRecoil - Time.deltaTime * 7f);

        float kick = eagleRecoil * eagleRecoil;

        // Kick back towards the hand and pitch the muzzle up, then settle.
        eagleModel.transform.localPosition =
                EagleRootLocal + new Vector3(0f, 0f, -0.05f * kick);

        eagleModel.transform.localRotation = Quaternion.Euler(-22f * kick, 0f, 0f);
    }

    private static void TickFlash()
    {
        if (eagleFlash == null || !eagleFlash.activeSelf)
            return;

        float left = eagleFlashUntil - Time.time;

        if (left <= 0f)
        {
            eagleFlash.SetActive(false);
            return;
        }

        float t = left / EagleFlashTime;

        eagleFlash.transform.localScale = Vector3.one * (0.04f + 0.07f * t);
    }

    private static void TickTracer()
    {
        if (eagleTracer == null || !eagleTracer.gameObject.activeSelf)
            return;

        float left = eagleTracerUntil - Time.time;

        if (left <= 0f)
        {
            eagleTracer.gameObject.SetActive(false);
            return;
        }

        float alpha = left / 0.14f;

        eagleTracer.startColor = new Color(1f, 0.9f, 0.35f, alpha);
        eagleTracer.endColor   = new Color(1f, 0.9f, 0.35f, alpha * 0.4f);
    }

    // Four-ish cubes shaped into a Desert Eagle: dark frame, silver slide,
    // angled grip, barrel, trigger guard and sights. Flat colours - it is
    // read at arm's length, not inspected up close.
    private static GameObject BuildEagle(Transform hand)
    {
        GameObject root = new($"{Constants.Name}_DesertEagle");

        root.transform.SetParent(hand, false);
        root.transform.localPosition = EagleRootLocal;
        root.transform.localRotation = Quaternion.identity;

        AddEaglePart(root, new Vector3(0f, -0.005f, 0.095f),
                new Vector3(0.030f, 0.042f, 0.150f),
                new Color(0.10f, 0.10f, 0.11f));

        AddEaglePart(root, new Vector3(0f, 0.026f, 0.095f),
                new Vector3(0.033f, 0.024f, 0.155f),
                new Color(0.58f, 0.58f, 0.62f));

        AddEaglePart(root, new Vector3(0f, 0.005f, 0.180f),
                new Vector3(0.018f, 0.020f, 0.030f),
                new Color(0.04f, 0.04f, 0.05f));

        AddEaglePart(root, new Vector3(0f, -0.062f, 0.030f),
                new Vector3(0.026f, 0.090f, 0.040f),
                new Color(0.06f, 0.06f, 0.07f),
                Quaternion.Euler(12f, 0f, 0f));

        AddEaglePart(root, new Vector3(0f, -0.030f, 0.078f),
                new Vector3(0.024f, 0.008f, 0.050f),
                new Color(0.08f, 0.08f, 0.09f));

        AddEaglePart(root, new Vector3(0f, 0.045f, 0.160f),
                new Vector3(0.006f, 0.014f, 0.008f),
                new Color(0.70f, 0.70f, 0.72f));

        AddEaglePart(root, new Vector3(0f, 0.042f, 0.030f),
                new Vector3(0.020f, 0.012f, 0.012f),
                new Color(0.70f, 0.70f, 0.72f));

        return root;
    }

    private static void AddEaglePart(GameObject root,
                                      Vector3   localPosition,
                                      Vector3   scale,
                                      Color     color,
                                      Quaternion? localRotation = null)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);

        Collider partCollider = part.GetComponent<Collider>();

        if (partCollider != null)
        {
            partCollider.enabled = false;
            UnityEngine.Object.Destroy(partCollider);
        }

        part.transform.SetParent(root.transform, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation ?? Quaternion.identity;
        part.transform.localScale    = scale;

        part.GetComponent<Renderer>().material = new Material(Shader.Find("GUI/Text Shader"));
        part.GetComponent<Renderer>().material.color = color;
    }
}
