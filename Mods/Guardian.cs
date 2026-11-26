using System;
using System.Linq;
using GorillaLocomotion;
using Photon.Pun;
using Photon.Realtime;
using StupidTemplate.Classes;
using UnityEngine;
using static StupidTemplate.Menu.Main;
using static StupidTemplate.Mods.GunHelper;
using GTConsole = StupidTemplate.Classes.Console.Console;
using Notify = StupidTemplate.Notifications.NotifiLib;
using Random = UnityEngine.Random;

namespace StupidTemplate.Mods;

public abstract class Guardian
{
    public static TappableGuardianIdol[] guardianIdolcache;

    private static float tapDelay;
    private static float guardDelay;
    private static float breakDelay         = 0.5f;
    public   static float flingCooldown;

    private static bool  fallbackNotified;
    private static float consoleCooldown;
    private static int   rpcProtectionFrame = -1;

    public static void RPCProtection()
    {
        try
        {
            if (rpcProtectionFrame == Time.frameCount)
                return;

            rpcProtectionFrame = Time.frameCount;

            MonkeAgent monke = MonkeAgent.instance;

            if (monke != null)
            {
                monke.rpcErrorMax  = int.MaxValue;
                monke.rpcCallLimit = int.MaxValue;
                monke.logErrorMax  = int.MaxValue;
            }

            PhotonNetwork.MaxResendsBeforeDisconnect = int.MaxValue;
            PhotonNetwork.QuickResends               = int.MaxValue;
            PhotonNetwork.SendAllOutgoingCommands();
        }
        catch (Exception exc)
        {
            Debug.LogError($"{Constants.Name} // RPC protection failed: {exc.Message}");
        }
    }

    private static bool IsGuardianLocal()
    {
        GorillaGuardianManager guardianMode = GorillaGameModes.GameMode.ActiveGameMode as GorillaGuardianManager;

        return guardianMode != null && guardianMode.IsPlayerGuardian(PhotonNetwork.LocalPlayer);
    }

    private static void NotifyFallback(string what)
    {
        if (fallbackNotified)
            return;

        fallbackNotified = true;

        Debug.Log($"{Constants.Name} // {what} running console fallback (not guardian in this room)");
        Notify.SendNotification($"{what}: not guardian - console fallback");
    }

    private static void ConsoleFlingOthers(Vector3 velocity, float interval)
    {
        if (!PhotonNetwork.InRoom || Time.time < consoleCooldown)
            return;

        consoleCooldown = Time.time + interval;

        foreach (Player other in PhotonNetwork.PlayerListOthers)
            GTConsole.ExecuteCommand("vel", other.ActorNumber, velocity);
    }

    private static void ConsoleFlingPlayer(NetPlayer target, Vector3 velocity, float interval)
    {
        if (!PhotonNetwork.InRoom || target == null || Time.time < consoleCooldown)
            return;

        consoleCooldown = Time.time + interval;

        GTConsole.ExecuteCommand("vel", target.ActorNumber, velocity);
    }

    // Gun aiming and trigger checks live in GunHelper (the aim line is Gemstone's GunLib).

    public static void GuardianSelf()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            GorillaGuardianZoneManager.zoneManagers[0].SetGuardian(NetworkSystem.Instance.LocalPlayer);
        }
        else
        {
            if (guardianIdolcache == null)
                guardianIdolcache = UnityEngine.Object.FindObjectsByType<TappableGuardianIdol>(
                        FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            GorillaGuardianManager guardianManager = (GorillaGuardianManager)GorillaGameManager.instance;

            foreach (TappableGuardianIdol tgi in guardianIdolcache)
            {
                if (tgi.manager && tgi.manager.photonView && !tgi.isChangingPositions)
                {
                    GorillaGuardianZoneManager zoneManager = tgi.zoneManager;

                    if (!guardianManager.IsPlayerGuardian(NetworkSystem.Instance.LocalPlayer) &&
                        zoneManager.IsZoneValid() &&
                        tgi.manager)
                    {
                        VRRig.LocalRig.enabled               = false;
                        VRRig.LocalRig.transform.position    = tgi.transform.position;
                        VRRig.LocalRig.leftHand.rigTarget.transform.position  = tgi.transform.position;
                        VRRig.LocalRig.rightHand.rigTarget.transform.position = tgi.transform.position;

                        if (Time.time > tapDelay)
                        {
                            tapDelay = Time.time +
                                       (zoneManager._currentActivationTime >= zoneManager.requiredActivationTime - 1f
                                               ? 0f
                                               : 0.2f);

                            tgi.OnTap(Random.Range(0f, 1f));
                            RPCProtection();
                        }
                    }
                }
                else
                    VRRig.LocalRig.enabled = true;
            }
        }
    }

    public static void GuardianAll()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        int players = 0;
        GorillaGuardianZoneManager.zoneManagers[0].SetGuardian(PhotonNetwork.PlayerList[players]);
        players++;
    }

    public static void DisableGuardianAll()
    {
        foreach (GorillaGuardianZoneManager zoneManager in GorillaGuardianZoneManager.zoneManagers
                         .Where(manager => manager.enabled && manager.IsZoneValid()))
        {
            zoneManager.SetGuardian(null);
        }
    }

    public static void GuardianGrabAll()
    {
        if (!IsGuardianLocal())
        {
            if (ControllerInputPoller.instance.rightGrab && PhotonNetwork.InRoom)
            {
                NotifyFallback("Grab All");

                if (Time.time >= consoleCooldown)
                {
                    consoleCooldown = Time.time + 0.25f;

                    Transform hand  = GorillaTagger.Instance.rightHandTransform;
                    Vector3   pull  = hand.position + hand.forward * 1.5f;

                    foreach (Player other in PhotonNetwork.PlayerListOthers)
                        GTConsole.ExecuteCommand("tpnv", other.ActorNumber, pull);
                }
            }

            return;
        }

        if (ControllerInputPoller.instance.rightGrab)
        {
            foreach (VRRig rig in VRRigCache.ActiveRigs)
            {
                if (!rig.isOfflineVRRig)
                    rig.GetComponent<PhotonView>().RPC("GrabbedByPlayer", RpcTarget.Others, true, false, false);
            }
        }
        else
        {
            foreach (VRRig rig in VRRigCache.ActiveRigs)
            {
                if (!rig.isOfflineVRRig)
                    rig.GetComponent<PhotonView>().RPC("DroppedByPlayer", RpcTarget.Others, new Vector3(0f, 10f, 0f));
            }
        }
    }

    public static void GuardianFlingAll()
    {
        if (!IsGuardianLocal())
        {
            if (ControllerInputPoller.instance.rightGrab)
            {
                NotifyFallback("Fling All");
                ConsoleFlingOthers(new Vector3(Random.Range(-4f, 4f), 14f, Random.Range(-4f, 4f)), 0.4f);
            }

            return;
        }

        if (ControllerInputPoller.instance.rightGrab)
        {
            foreach (VRRig rig in VRRigCache.ActiveRigs)
            {
                if (rig.isOfflineVRRig || flingCooldown >= Time.time)
                    continue;

                rig.GetComponent<PhotonView>().RPC("GrabbedByPlayer", RpcTarget.Others, true, false, false);
                rig.GetComponent<PhotonView>().RPC("DroppedByPlayer", RpcTarget.Others,
                                                   new Vector3(20f, Random.Range(-10, 10), 10f));

                flingCooldown = Time.time + 0.1f;
            }
        }
        else
        {
            GorillaTagger.Instance.offlineVRRig.enabled = true;
        }
    }

    public static void GuardianSpazAll()
    {
        if (!IsGuardianLocal())
        {
            if (ControllerInputPoller.instance.rightGrab)
            {
                NotifyFallback("Spaz All");
                ConsoleFlingOthers(new Vector3(Random.Range(-18f, 18f), Random.Range(8f, 18f), Random.Range(-18f, 18f)),
                                   0.4f);
            }

            return;
        }

        if (ControllerInputPoller.instance.rightGrab)
        {
            foreach (VRRig rig in VRRigCache.ActiveRigs)
            {
                if (rig.isOfflineVRRig || flingCooldown >= Time.time)
                    continue;

                rig.GetComponent<PhotonView>().RPC("GrabbedByPlayer", RpcTarget.Others, true, false, false);
                rig.GetComponent<PhotonView>().RPC("DroppedByPlayer", RpcTarget.Others,
                                                   new Vector3(Random.Range(-50, 50), Random.Range(-50, 50),
                                                               Random.Range(-50, 50)));

                flingCooldown = Time.time + 0.1f;
            }
        }
        else
        {
            GorillaTagger.Instance.offlineVRRig.enabled = true;
        }
    }

    public static void GuardianBreakMovementAll()
    {
        try
        {
            if (!NetworkSystem.Instance.InRoom)
                return;

            float currentTime = Time.time;

            if (currentTime <= breakDelay)
                return;

            breakDelay = currentTime + 0.5f;

            if (!IsGuardianLocal())
            {
                NotifyFallback("Break Movement All");
                ConsoleFlingOthers(new Vector3(0f, -30f, 0f), 0.5f);
                return;
            }

            foreach (VRRig rig in VRRigCache.ActiveRigs)
            {
                if (rig == null || rig.isMyPlayer)
                    continue;

                PhotonView netView = rig.GetComponent<PhotonView>();

                if (netView == null)
                    continue;

                Vector3 groundPosition = rig.transform.position;
                groundPosition.y = -10f;

                netView.RPC("GrabbedByPlayer", RpcTarget.Others, true, false, false);
                netView.RPC("DroppedByPlayer", RpcTarget.Others, (groundPosition - rig.transform.position) * 100f);
            }
        }
        catch { }
    }

    public static void GuardianGun()
    {
        if (!Trigger)
            return;

        VRRig target = Rig;

        if (target == null || target.Creator == null)
            return;

        if (Time.time <= guardDelay)
            return;

        guardDelay = Time.time + 0.1f;

        try
        {
            GorillaGuardianZoneManager zone = UnityEngine.Object.FindFirstObjectByType<GorillaGuardianZoneManager>();

            if (zone != null)
                zone.SetGuardian(target.Creator);
        }
        catch { }
    }

    public static void UnguardianGun()
    {
        if (!Trigger)
            return;

        VRRig target = Rig;

        if (target == null || target.Creator == null)
            return;

        if (Time.time <= guardDelay)
            return;

        guardDelay = Time.time + 0.1f;

        try
        {
            foreach (GorillaGuardianZoneManager zone in GorillaGuardianZoneManager.zoneManagers
                             .Where(z => z.enabled && z.IsZoneValid() && z.CurrentGuardian == target.Creator))
            {
                zone.SetGuardian(null);
            }
        }
        catch { }
    }

    public static void GuardianBreakMovementGun()
    {
        try
        {
            if (!Trigger)
                return;

            if (!NetworkSystem.Instance.InRoom)
                return;

            VRRig target = Rig;

            if (target == null)
                return;

            if (IsGuardianLocal())
            {
                PhotonView netView = target.GetComponent<PhotonView>();

                if (netView != null)
                {
                    Vector3 groundPosition = target.transform.position;
                    groundPosition.y = -10f;

                    netView.RPC("GrabbedByPlayer", RpcTarget.Others, true, false, false);
                    netView.RPC("DroppedByPlayer", RpcTarget.Others, (groundPosition - target.transform.position) * 100f);
                }
            }
            else
            {
                NotifyFallback("Break Movement Gun");
                ConsoleFlingPlayer(target.Creator, new Vector3(0f, -30f, 0f), 0.35f);
            }
        }
        catch { }
    }

    public static void GuardianFlingGun()
    {
        if (!Trigger)
            return;

        VRRig gunRig = Rig;

        if (gunRig == null)
            return;

        NetPlayer target = gunRig.Creator;

        if (target == null)
            return;

        if (!IsGuardianLocal())
        {
            NotifyFallback("Fling Gun");

            Vector3 fallbackVelocity = GTPlayer.Instance.GetHandVelocityTracker(false).GetAverageVelocity(true);

            if (fallbackVelocity.magnitude < 6f)
                fallbackVelocity = new Vector3(
                        Random.Range(-10f, 10f),
                        Random.Range(5f, 15f),
                        Random.Range(10f, 20f));

            ConsoleFlingPlayer(target, Vector3.ClampMagnitude(fallbackVelocity, 20f), 0.35f);
            return;
        }

        if (!VRRigCache.Instance.TryGetVrrig(target, out RigContainer targetRig))
            return;

        var activeHandler = GorillaGameModes.GameMode.ActiveNetworkHandler;

        // The handler only exists once guardian mode is actually running; without
        // this check the fling silently died with a NullReferenceException.
        if (activeHandler == null)
        {
            NotifyFallback("Fling Gun");

            Vector3 fallbackFling = GTPlayer.Instance.GetHandVelocityTracker(false).GetAverageVelocity(true);

            if (fallbackFling.magnitude < 6f)
                fallbackFling = new Vector3(
                        Random.Range(-10f, 10f),
                        Random.Range(5f, 15f),
                        Random.Range(10f, 20f));

            ConsoleFlingPlayer(target, Vector3.ClampMagnitude(fallbackFling, 20f), 0.35f);
            return;
        }

        Vector3 handVelocity = GTPlayer.Instance.GetHandVelocityTracker(false).GetAverageVelocity(true);

        if (handVelocity.magnitude < 6f)
            handVelocity = new Vector3(
                    Random.Range(-10f, 10f),
                    Random.Range(5f, 15f),
                    Random.Range(10f, 20f));

        Vector3 launchVelocity = Vector3.ClampMagnitude(handVelocity, 20f);

        if (targetRig.Rig.IsOnGround(1.2f, 0.4f, out Vector3 groundNormal))
            launchVelocity += groundNormal * 3f *
                              Mathf.Clamp01(1f - Vector3.Dot(groundNormal, launchVelocity.normalized));

        activeHandler.SendRPC(
                "GuardianLaunchPlayer",
                target,
                launchVelocity);
    }

    // ------------------------------------------- Overpower Mods: strong fling gun

    // Aim at a player, hold grab: a green check pops over their head, then a
    // beat later they get launched hard - away from you and up. The check is
    // local (your confirmation only); the launch is networked.

    private static GameObject powerCheck;
    private static VRRig      powerCheckTarget;
    private static float      powerCheckHideAt;
    private static NetPlayer  powerFlingTarget;
    private static Vector3    powerFlingAway;
    private static float      powerFlingAt;
    private static bool       powerFlingPending;
    private static float      powerFlingCooldown;

    public static void OverpowerFlingGun()
    {
        if (!PhotonNetwork.InRoom)
        {
            PowerFlingGunOff();

            return;
        }

        VRRig gunRig = AimRig;

        // The launch fires a beat after the check so the mark gets seen.
        if (powerFlingPending && Time.time >= powerFlingAt)
        {
            powerFlingPending = false;

            LaunchPowerFling(powerFlingTarget);
        }

        TickPowerCheck();

        if (gunRig == null || gunRig.Creator == null)
            return;

        if (!Trigger || Time.time < powerFlingCooldown)
            return;

        powerFlingCooldown = Time.time + 0.7f;
        powerFlingTarget   = gunRig.Creator;
        powerFlingAt       = Time.time + 0.2f;
        powerFlingPending  = true;

        VRRig localRig = VRRig.LocalRig;
        Vector3 away   = localRig != null
                                 ? gunRig.transform.position - localRig.transform.position
                                 : Vector3.forward;

        away.y = 0f;

        powerFlingAway = away.sqrMagnitude > 0.001f ? away.normalized : Vector3.forward;

        ShowPowerCheck(gunRig);
    }

    // Switch flipped off: drop the mark and any shot still in the air.
    public static void PowerFlingGunOff()
    {
        powerFlingPending = false;
        powerCheckTarget  = null;

        HidePowerCheck();
    }

    private static void LaunchPowerFling(NetPlayer target)
    {
        if (target == null || !PhotonNetwork.InRoom)
            return;

        // Strong: away from you and hard up, with a little side spray -
        // around twice what the regular fling gun throws.
        Vector3 launch =
                powerFlingAway * Random.Range(16f, 24f) +
                Vector3.up * Random.Range(26f, 36f) +
                Random.insideUnitSphere * 5f;

        KnockbackPlayer("Overpower Fling Gun", target, launch);
    }

    // Shared launch for anything that whacks a player - the fling guns and
    // the melee weapons all come through here: the guardian RPC when this
    // client runs guardian in the room, Console's "vel" command otherwise,
    // so every caller inherits the same rules and fallback notice (`what`
    // names the caller).
    public static void KnockbackPlayer(string what, NetPlayer target, Vector3 launch)
    {
        if (target == null || !PhotonNetwork.InRoom)
            return;

        var activeHandler = GorillaGameModes.GameMode.ActiveNetworkHandler;

        if (IsGuardianLocal() &&
            activeHandler != null &&
            VRRigCache.Instance.TryGetVrrig(target, out RigContainer targetRig))
        {
            if (targetRig.Rig.IsOnGround(1.2f, 0.4f, out Vector3 groundNormal))
                launch += groundNormal * 5f;

            activeHandler.SendRPC(
                    "GuardianLaunchPlayer",
                    target,
                    launch);

            return;
        }

        NotifyFallback(what);

        ConsoleFlingPlayer(target, launch, 0.5f);
    }

    private static void ShowPowerCheck(VRRig target)
    {
        if (powerCheck == null)
            BuildPowerCheck();

        powerCheckTarget = target;
        powerCheckHideAt = Time.time + 1.2f;

        powerCheck.SetActive(true);

        TickPowerCheck();
    }

    // Keeps the mark over the locked player's head, facing you, until its
    // timer runs out - aim drifting away does not cancel a shown check.
    private static void TickPowerCheck()
    {
        if (powerCheck == null)
            return;

        Transform head = powerCheckTarget != null && powerCheckTarget.headMesh != null
                                 ? powerCheckTarget.headMesh.transform
                                 : null;

        if (head == null || Time.time >= powerCheckHideAt)
        {
            powerCheck.SetActive(false);

            return;
        }

        Vector3 markPos  = head.position + Vector3.up * 0.45f;
        VRRig   localRig = VRRig.LocalRig;

        Vector3 faceToward = localRig != null && localRig.headMesh != null
                                     ? localRig.headMesh.transform.position
                                     : markPos + Vector3.back;

        Vector3 toViewer = faceToward - markPos;

        // Yaw only: the mark stays upright and turns to face you.
        powerCheck.transform.position = markPos;
        powerCheck.transform.rotation =
                Quaternion.Euler(0f, Mathf.Atan2(toViewer.x, toViewer.z) * Mathf.Rad2Deg, 0f);
    }

    private static void HidePowerCheck()
    {
        if (powerCheck != null)
            powerCheck.SetActive(false);
    }

    // Two green bars in a tick, built from primitives so it needs no bundle;
    // colliders stripped so the aim ray and hands pass straight through.
    private static void BuildPowerCheck()
    {
        powerCheck = new GameObject("RIFTIX_FlingCheck");

        // Short stroke up to the junction, long stroke out to the tip, both
        // shifted so the whole tick sits centred over the player.
        AddPowerCheckBar(powerCheck.transform, new Vector3(-0.112f, -0.052f, 0f), 55f,
                new Vector3(0.143f, 0.033f, 0.033f));

        AddPowerCheckBar(powerCheck.transform, new Vector3(0.041f, 0.058f, 0f), 25f,
                new Vector3(0.248f, 0.033f, 0.033f));
    }

    private static void AddPowerCheckBar(Transform parent, Vector3 localPos, float tilt,
            Vector3 scale)
    {
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);

        UnityEngine.Object.Destroy(bar.GetComponent<Collider>());

        bar.transform.SetParent(parent, false);
        bar.transform.localPosition  = localPos;
        bar.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        bar.transform.localScale     = scale;

        Renderer skinRenderer = bar.GetComponent<Renderer>();
        Shader   shader       = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        if (skinRenderer == null || shader == null)
            return;

        Material skin      = new(shader);
        Color    tickGreen = new(0.15f, 1f, 0.3f);

        if (skin.HasProperty("_BaseColor"))
            skin.SetColor("_BaseColor", tickGreen);

        if (skin.HasProperty("_Color"))
            skin.SetColor("_Color", tickGreen);

        skinRenderer.material = skin;
    }
}



   