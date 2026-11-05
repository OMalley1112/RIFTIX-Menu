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
}
