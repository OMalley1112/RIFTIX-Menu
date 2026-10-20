using System;
using System.Reflection;
using GorillaLocomotion.Gameplay;
using Photon.Pun;
using StupidTemplate.Classes;
using UnityEngine;
using static StupidTemplate.Menu.Main;
using static StupidTemplate.Mods.GunHelper;
using Notify = StupidTemplate.Notifications.NotifiLib;

namespace StupidTemplate.Mods;

public abstract class Halloween
{
    private static HalloweenGhostChaser lucy;
    private static GameObject           terraformer;

    private static float nextLucySearchTime;
    private static bool  terraformerLogged;

    private static HalloweenGhostChaser GetLucy()
    {
        if (lucy == null && Time.time >= nextLucySearchTime)
        {
            nextLucySearchTime = Time.time + 2f;

            GameObject obj = FindSceneObject(
                    "Environment Objects/05Maze_PersistentObjects/Ghosts/Halloween Ghost/FloatingChaseSkeleton");

            if (obj != null)
                lucy = obj.GetComponentInChildren<HalloweenGhostChaser>(true);
        }

        return lucy;
    }

    private static GameObject FindSceneObject(string path)
    {
        GameObject obj = GameObject.Find(path);

        if (obj != null)
            return obj;

        int    slash    = path.LastIndexOf('/');
        string leaf     = slash >= 0 ? path.Substring(slash + 1) : path;
        string leafLow  = leaf.ToLower();
        GameObject fuzzy = null;

        foreach (GameObject candidate in UnityEngine.Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.name == leaf)
                return candidate;

            if (fuzzy == null && candidate.name.ToLower().Contains(leafLow))
                fuzzy = candidate;
        }

        return fuzzy;
    }

    private static string GetHierarchyPath(Transform t)
    {
        string path = t.name;

        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }

        return path;
    }

    private static GameObject GetTerraformer()
    {
        if (terraformer == null)
        {
            terraformer = FindSceneObject(
                    "Environment Objects/LocalObjects_Prefab/Forest/2026_Halloween_Forest/Terraformer");

            if (terraformer != null && !terraformerLogged)
            {
                terraformerLogged = true;
                Debug.Log($"{Constants.Name} // Terraformer found at " + GetHierarchyPath(terraformer.transform));
            }
        }

        return terraformer;
    }

    private static void SetTerraformerActive(bool active)
    {
        GameObject obj = GetTerraformer();

        if (obj == null)
        {
            Debug.LogWarning($"{Constants.Name} // Terraformer not found in scene");
            Notify.SendNotification("Terraformer not found in scene");
            return;
        }

        if (active)
        {
            if (!obj.activeInHierarchy)
            {
                Transform t = obj.transform;

                while (t != null && !obj.activeInHierarchy)
                {
                    if (!t.gameObject.activeSelf)
                        t.gameObject.SetActive(true);

                    t = t.parent;
                }
            }

            Notify.SendNotification("Terraformer enabled");
        }
        else
        {
            obj.SetActive(false);
            Notify.SendNotification("Terraformer disabled");
        }
    }

    public static void EnableTerraformer()  => SetTerraformerActive(true);
    public static void DisableTerraformer() => SetTerraformerActive(false);

    private static void SummonLucy(bool summoned)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        HalloweenGhostChaser ghost = GetLucy();

        if (ghost == null || !ghost.IsMine)
            return;

        ghost.timeGongStarted = Time.time;
        ghost.currentState    = HalloweenGhostChaser.ChaseState.Gong;
        ghost.isSummoned      = summoned;
    }

    public static void SpawnLucy()       => SummonLucy(false);
    public static void SpawnYellowLucy() => SummonLucy(true);
    public static void SpawnBlueLucy()   => SummonLucy(true);
    public static void SpawnRedLucy()    => SummonLucy(true);

    // Gun aiming and trigger checks live in GunHelper (RIFTIX's RenderGun replaces Gemstone's GunLib).

    public static void MoveLucyGun()
    {
        GameObject pointer = RenderGun().NewPointer;

        if (!Trigger)
            return;

        HalloweenGhostChaser ghost = GetLucy();

        if (ghost != null && ghost.IsMine)
            ghost.transform.position = pointer.transform.position + Vector3.up * 1f;
    }

    public static void LucyChaseGun()
    {
        if (!Trigger)
            return;

        VRRig                target = Rig;
        HalloweenGhostChaser ghost   = GetLucy();

        if (ghost == null || target == null)
            return;

        if (!PhotonNetwork.IsMasterClient || !ghost.IsMine)
            return;

        ghost.currentState = HalloweenGhostChaser.ChaseState.Chasing;
        ghost.targetPlayer = target.Creator;
        ghost.followTarget = target.head.rigTarget;
    }

    public static void LucyGrabGun()
    {
        if (!Trigger)
            return;

        VRRig                target = Rig;
        HalloweenGhostChaser ghost   = GetLucy();

        if (ghost == null || target == null)
            return;

        if (!PhotonNetwork.IsMasterClient || !ghost.IsMine)
            return;

        ghost.isSummoned      = true;
        ghost.targetPlayer    = target.Creator;
        ghost.followTarget    = target.head.rigTarget;
        ghost.currentState    = HalloweenGhostChaser.ChaseState.Grabbing;
    }

    public static void LucyGrabAll()
    {
        HalloweenGhostChaser ghost = GetLucy();

        if (ghost == null)
            return;

        if (!PhotonNetwork.IsMasterClient || !ghost.IsMine)
            return;

        ghost.isSummoned      = true;
        ghost.currentState    = HalloweenGhostChaser.ChaseState.Grabbing;
        ghost.grabTime        = Time.time;
        ghost.grabDuration    = float.MaxValue;
        ghost.minGrabCooldown = 0f;

        ghost.leftArm.localEulerAngles   = ghost.leftArmGrabbingLocal;
        ghost.rightArm.localEulerAngles  = ghost.rightArmGrabbingLocal;
        ghost.leftHand.localEulerAngles  = ghost.leftHandGrabbingLocal;
        ghost.rightHand.localEulerAngles = ghost.rightHandGrabbingLocal;
        ghost.ghostBody.transform.localPosition  = ghost.ghostOffsetGrabbingLocal;
        ghost.ghostBody.transform.localEulerAngles = ghost.ghostGrabbingEulerRotation;
        ghost.ghostBody.SetActive(true);

        if (ghost.ghostMaterial != null)
            ghost.ghostMaterial.color = ghost.summonedColor;

        if (ghost.laugh != null)
        {
            ghost.laugh.volume = 0.25f;
            ghost.laugh.GTPlayOneShot(ghost.deepLaugh);
        }

        ghost.possibleTarget.Clear();

        foreach (VRRig rig in VRRigCache.ActiveRigs)
        {
            if (rig == null || rig == GorillaTagger.Instance.offlineVRRig)
                continue;

            if (rig.Creator == null)
                continue;

            ghost.possibleTarget.Add(rig.Creator);

            if (rig.Creator == NetworkSystem.Instance.LocalPlayer)
            {
                ghost.targetPlayer = rig.Creator;
                ghost.followTarget = rig.head.rigTarget;
                GorillaTagger.Instance.ApplyStatusEffect(GorillaTagger.StatusEffect.Frozen,
                                                         GorillaTagger.Instance.tagCooldown);
                GorillaTagger.Instance.StartVibration(true, ghost.hapticStrength, ghost.hapticDuration);
                GorillaTagger.Instance.StartVibration(false, ghost.hapticStrength, ghost.hapticDuration);
                GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.up * ghost.grabSpeed;
                EquipmentInteractor.instance.ForceStopClimbing();
            }
            else
            {
                ghost.targetPlayer = rig.Creator;
                ghost.followTarget = rig.head.rigTarget;
                ghost.transform.rotation = ghost.followTarget.rotation;
                ghost.transform.position  = ghost.followTarget.position;
                ghost.WriteDataFusion();
            }
        }
    }

    public static void SlowLucy()
    {
        HalloweenGhostChaser ghost = GetLucy();

        if (ghost == null || !ghost.IsMine || !PhotonNetwork.IsMasterClient)
            return;

        ghost.currentSpeed         = 1f;
        ghost.velocityStep         = 0.25f;
        ghost.velocityIncreaseTime = 20f;
    }

    public static void FastLucy()
    {
        HalloweenGhostChaser ghost = GetLucy();

        if (ghost == null || !ghost.IsMine || !PhotonNetwork.IsMasterClient)
            return;

        ghost.currentSpeed         = 15f;
        ghost.velocityStep         = 3f;
        ghost.velocityIncreaseTime = 5f;
    }

    public static void ResetLucySpeed()
    {
        HalloweenGhostChaser ghost = GetLucy();

        if (ghost == null || !ghost.IsMine || !PhotonNetwork.IsMasterClient)
            return;

        ghost.currentSpeed         = 3f;
        ghost.velocityStep         = 1f;
        ghost.velocityIncreaseTime = 20f;
    }

    public static void FastBroomsticks()  => SetBroomstickSpeed(10f);
    public static void SlowBroomsticks()  => SetBroomstickSpeed(60f);
    public static void ResetBroomsticks() => SetBroomstickSpeed(30f);

    private static readonly FieldInfo broomstickProgressField =
            typeof(NoncontrollableBroomstick).GetField("progressPerFixedUpdate",
                                                       BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo broomstickCyclesField =
            typeof(NoncontrollableBroomstick).GetField("secondsToCycles",
                                                       BindingFlags.NonPublic | BindingFlags.Instance);

    private static void SetBroomstickSpeed(float duration)
    {
        NoncontrollableBroomstick[] sticks = UnityEngine.Object.FindObjectsByType<NoncontrollableBroomstick>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (NoncontrollableBroomstick stick in sticks)
        {
            if (stick == null)
                continue;

            stick.duration = duration;

            broomstickProgressField?.SetValue(stick, Time.fixedDeltaTime / duration);
            broomstickCyclesField?.SetValue(stick, 1.0 / duration);
        }

        Debug.Log($"{Constants.Name} // Broomstick duration set to {duration:0.#}s on {sticks.Length} broomstick(s)");
        Notify.SendNotification($"Broomsticks: {sticks.Length} found, duration {duration:0.#}s");
    }
}
