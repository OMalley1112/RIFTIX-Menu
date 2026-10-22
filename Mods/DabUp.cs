using UnityEngine;

namespace StupidTemplate.Mods;

public abstract class DabUp
{
    private const float EaseRate = 10f;

    private static bool poseActive;
    private static bool wasGripping;
    private static bool easingOut;
    private static bool poseInitialized;
    private static bool dabSideRight = true;

    private static Vector3    currentLeftPos;
    private static Vector3    currentRightPos;
    private static Quaternion currentLeftRot;
    private static Quaternion currentRightRot;

    public static void Tick()
    {
        bool grips;

        try
        {
            grips = ControllerInputPoller.instance.leftGrab && ControllerInputPoller.instance.rightGrab;
        }
        catch
        {
            return;
        }

        if (grips && !wasGripping)
        {
            wasGripping     = true;
            poseActive      = true;
            easingOut       = false;
            dabSideRight    = !dabSideRight;
            poseInitialized = false;

            Clap();
        }
        else if (!grips && wasGripping)
        {
            wasGripping = false;
            poseActive  = false;
            easingOut   = poseInitialized;
        }

        if (poseActive || easingOut)
            ApplyPose();
    }

    public static void ApplyPose()
    {
        if (!poseActive && !easingOut)
            return;

        GorillaTagger tagger = GorillaTagger.Instance;
        Camera         cam    = Camera.main;

        if (tagger == null || cam == null ||
            tagger.leftHandTransform == null || tagger.rightHandTransform == null)
            return;

        Vector3    trackedL  = tagger.leftHandTransform.position;
        Vector3    trackedR  = tagger.rightHandTransform.position;
        Quaternion trackedLR = tagger.leftHandTransform.rotation;
        Quaternion trackedRR = tagger.rightHandTransform.rotation;

        if (poseActive)
        {
            Vector3 leftOffset;
            Vector3 rightOffset;

            if (dabSideRight)
            {
                rightOffset = new Vector3(-0.10f, -0.04f, 0.20f);
                leftOffset  = new Vector3(-0.52f, -0.16f, 0.06f);
            }
            else
            {
                leftOffset  = new Vector3(0.10f, -0.04f, 0.20f);
                rightOffset = new Vector3(0.52f, -0.16f, 0.06f);
            }

            Transform head = cam.transform;

            Vector3 dabL = head.position + head.rotation * leftOffset;
            Vector3 dabR = head.position + head.rotation * rightOffset;

            if (!poseInitialized)
            {
                currentLeftPos  = trackedL;
                currentRightPos = trackedR;
                currentLeftRot  = trackedLR;
                currentRightRot = trackedRR;
                poseInitialized = true;
            }

            float k = 1f - Mathf.Exp(-EaseRate * Time.deltaTime);

            currentLeftPos  = Vector3.Lerp(currentLeftPos, dabL, k);
            currentRightPos = Vector3.Lerp(currentRightPos, dabR, k);
            currentLeftRot  = Quaternion.Slerp(currentLeftRot, head.rotation, k);
            currentRightRot = Quaternion.Slerp(currentRightRot, head.rotation, k);

            SetHands(currentLeftPos, currentLeftRot, currentRightPos, currentRightRot);
        }
        else
        {
            float k = 1f - Mathf.Exp(-EaseRate * 1.5f * Time.deltaTime);

            currentLeftPos  = Vector3.Lerp(currentLeftPos, trackedL, k);
            currentRightPos = Vector3.Lerp(currentRightPos, trackedR, k);
            currentLeftRot  = Quaternion.Slerp(currentLeftRot, trackedLR, k);
            currentRightRot = Quaternion.Slerp(currentRightRot, trackedRR, k);

            SetHands(currentLeftPos, currentLeftRot, currentRightPos, currentRightRot);

            if (Vector3.Distance(currentLeftPos, trackedL)  < 0.01f &&
                Vector3.Distance(currentRightPos, trackedR) < 0.01f)
                easingOut = false;
        }
    }

    public static void Disable()
    {
        poseActive  = false;
        wasGripping = false;
        easingOut   = poseInitialized;
    }

    private static void SetHands(Vector3 leftPos, Quaternion leftRot, Vector3 rightPos, Quaternion rightRot)
    {
        try
        {
            GorillaTagger tagger = GorillaTagger.Instance;

            if (tagger != null)
            {
                if (tagger.leftHandTransform != null)
                    tagger.leftHandTransform.SetPositionAndRotation(leftPos, leftRot);

                if (tagger.rightHandTransform != null)
                    tagger.rightHandTransform.SetPositionAndRotation(rightPos, rightRot);
            }

            VRRig rig = VRRig.LocalRig;

            if (rig != null)
            {
                if (rig.leftHandTransform != null)
                    rig.leftHandTransform.SetPositionAndRotation(leftPos, leftRot);

                if (rig.rightHandTransform != null)
                    rig.rightHandTransform.SetPositionAndRotation(rightPos, rightRot);
            }
        }
        catch { }
    }

    private static void Clap()
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

        try
        {
            GorillaTagger tagger = GorillaTagger.Instance;

            if (tagger != null)
            {
                if (tagger.leftHandTransform != null)
                    SteamBurst(tagger.leftHandTransform.position);

                if (tagger.rightHandTransform != null)
                    SteamBurst(tagger.rightHandTransform.position);
            }
        }
        catch { }
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
