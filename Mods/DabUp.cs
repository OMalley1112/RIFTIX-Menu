using System.Collections.Generic;
using UnityEngine;

namespace StupidTemplate.Mods;

public abstract class DabUp
{
    private const float TouchDistance  = 0.30f;
    private const float EscapeDistance = 1.5f;
    private const float EaseRate       = 14f;

    private static bool dapActive;
    private static bool wasGripping;
    private static bool easingOut;
    private static bool dapHandRight;
    private static bool dapInitialized;

    private static Transform  dapTarget;
    private static Vector3    currentPos;
    private static Quaternion currentRot;

    public static void Tick()
    {
        bool grip;
        bool gripRight;

        try
        {
            grip      = ControllerInputPoller.instance.leftGrab || ControllerInputPoller.instance.rightGrab;
            gripRight = ControllerInputPoller.instance.rightGrab;
        }
        catch
        {
            return;
        }

        bool pressed = grip && !wasGripping;
        wasGripping = grip;

        if (dapActive)
        {
            Camera cam = Camera.main;

            bool escaped = dapTarget == null || cam == null ||
                           Vector3.Distance(cam.transform.position, dapTarget.position) > EscapeDistance;

            if (!grip || escaped)
            {
                dapActive  = false;
                easingOut  = dapInitialized;
            }
        }

        if (pressed && !dapActive)
            TryStartDap(gripRight);

        if (dapActive || easingOut)
            ApplyPose();
    }

    public static void ApplyPose()
    {
        if (!dapActive && !easingOut)
            return;

        GorillaTagger tagger = GorillaTagger.Instance;
        Camera         cam    = Camera.main;

        if (tagger == null || cam == null)
            return;

        Transform myHand = dapHandRight
                                   ? tagger.rightHandTransform
                                   : tagger.leftHandTransform;

        if (myHand == null)
            return;

        Vector3    trackedPos = myHand.position;
        Quaternion trackedRot = myHand.rotation;

        if (dapActive && dapTarget != null)
        {
            if (!dapInitialized)
            {
                currentPos      = trackedPos;
                currentRot      = trackedRot;
                dapInitialized  = true;
            }

            float k = 1f - Mathf.Exp(-EaseRate * Time.deltaTime);

            currentPos = Vector3.Lerp(currentPos, dapTarget.position, k);
            currentRot = Quaternion.Slerp(currentRot, dapTarget.rotation, k);

            SetDapHand(currentPos, currentRot);
        }
        else if (!dapActive && easingOut)
        {
            float k = 1f - Mathf.Exp(-EaseRate * 1.5f * Time.deltaTime);

            currentPos = Vector3.Lerp(currentPos, trackedPos, k);
            currentRot = Quaternion.Slerp(currentRot, trackedRot, k);

            SetDapHand(currentPos, currentRot);

            if (Vector3.Distance(currentPos, trackedPos) < 0.01f)
                easingOut = false;
        }
    }

    public static void Disable()
    {
        dapActive   = false;
        wasGripping = false;
        easingOut   = dapInitialized;
    }

    private static void TryStartDap(bool rightHand)
    {
        GorillaTagger tagger = GorillaTagger.Instance;

        if (tagger == null)
            return;

        Transform myHand = rightHand
                                   ? tagger.rightHandTransform
                                   : tagger.leftHandTransform;

        if (myHand == null)
            return;

        Transform nearest  = null;
        float     best     = TouchDistance;

        IReadOnlyList<VRRig> rigs = VRRigCache.ActiveRigs;

        for (int i = 0; i < rigs.Count; i++)
        {
            VRRig rig = rigs[i];

            if (rig == null || rig.isLocal)
                continue;

            if (rig.leftHandTransform != null)
            {
                float distance = Vector3.Distance(myHand.position, rig.leftHandTransform.position);

                if (distance < best)
                {
                    best    = distance;
                    nearest = rig.leftHandTransform;
                }
            }

            if (rig.rightHandTransform != null)
            {
                float distance = Vector3.Distance(myHand.position, rig.rightHandTransform.position);

                if (distance < best)
                {
                    best    = distance;
                    nearest = rig.rightHandTransform;
                }
            }
        }

        if (nearest == null)
            return;

        dapHandRight    = rightHand;
        dapTarget       = nearest;
        dapActive       = true;
        easingOut       = false;
        dapInitialized  = false;

        Clap((myHand.position + nearest.position) * 0.5f);
    }

    private static void SetDapHand(Vector3 position, Quaternion rotation)
    {
        try
        {
            GorillaTagger tagger = GorillaTagger.Instance;

            if (tagger != null)
            {
                Transform hand = dapHandRight
                                         ? tagger.rightHandTransform
                                         : tagger.leftHandTransform;

                hand?.SetPositionAndRotation(position, rotation);
            }

            VRRig rig = VRRig.LocalRig;

            if (rig != null)
            {
                Transform rigHand = dapHandRight
                                            ? rig.rightHandTransform
                                            : rig.leftHandTransform;

                rigHand?.SetPositionAndRotation(position, rotation);
            }
        }
        catch { }
    }

    private static void Clap(Vector3 position)
    {
        try
        {
            VRRig.LocalRig.PlayHandTapLocal(8, false, 3f);
            VRRig.LocalRig.PlayHandTapLocal(8, true,  3f);
        }
        catch { }

        try
        {
            GorillaTagger tagger = GorillaTagger.Instance;

            tagger.StartVibration(false, tagger.tagHapticStrength, tagger.tagHapticDuration);
            tagger.StartVibration(true,  tagger.tagHapticStrength, tagger.tagHapticDuration);
        }
        catch { }

        SteamBurst(position);
        SteamBurst(position + Vector3.up * 0.12f);
    }

    private static void SteamBurst(Vector3 position)
    {
        GameObject steamObject = new("RIFTIX_DabSteam");
        steamObject.transform.position = position;

        ParticleSystem particles = steamObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.loop            = false;
        main.playOnAwake     = true;
        main.duration        = 0.4f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
        main.startColor      = new Color(1f, 1f, 1f, 0.65f);
        main.gravityModifier = -0.25f;
        main.maxParticles    = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts([new ParticleSystem.Burst(0f, 14)]);

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType       = ParticleSystemShapeType.Sphere;
        shape.radius          = 0.06f;
        shape.radiusThickness = 1f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;

        Gradient fade = new();
        fade.SetKeys(
                [new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f)],
                [new GradientAlphaKey(0.65f, 0f), new GradientAlphaKey(0f, 1f)]);

        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);

        Object.Destroy(steamObject, 2f);
    }
}
