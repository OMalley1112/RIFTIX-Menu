using System;
using System.Diagnostics;
using StupidTemplate.Notifications;
using UnityEngine;
using UnityEngine.XR;
using static StupidTemplate.Menu.Main;

namespace StupidTemplate.Mods;

public abstract class Science
{
    private static readonly string[] ProjectPaths =
    {
            @"C:\Users\Charl\Downloads\virus-science-project\index.html",
            @"C:\Users\Charl\Desktop\virus-science-project\index.html",
    };

    private static readonly string[] Facts =
    {
            "Viruses are NOT alive - they cannot eat, grow, or reproduce on their own!",
            "Thousands of viruses could fit on the head of a pin.",
            "Soap tears apart a virus's fat jacket - wash your hands for 20 seconds!",
            "Antibiotics fight bacteria, NOT viruses.",
            "Most viruses are harmless - the common cold is one of them.",
            "Bacteriophages look like tiny lunar landers and hunt bacteria.",
            "The first vaccine was made in 1796 to stop smallpox.",
            "Plant viruses like tobacco mosaic cannot infect people at all.",
            "A virus needs a living cell to copy itself - like a recipe needs a kitchen.",
            "Memory cells remember a vaccine for years, so your body responds in hours.",
    };

    private static int factIndex;

    public static void OpenProject()
    {
        try
        {
            foreach (string path in ProjectPaths)
            {
                if (System.IO.File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo(path)
                    {
                            UseShellExecute = true,
                    });

                    NotifiLib.SendNotification("Opening your virus science project!");
                    return;
                }
            }

            NotifiLib.SendNotification("Virus project not found on this PC");
        }
        catch (Exception e)
        {
            NotifiLib.SendNotification("Could not open project: " + e.Message);
        }
    }

    public static void NextFact() =>
            NotifiLib.SendNotification("Virus Fact: " + Facts[factIndex++ % Facts.Length]);

    private static bool  virusGunOn;
    private static float nextSprayTime;

    public static void EnableVirusGun()
    {
        virusGunOn = true;
        NotifiLib.SendNotification("Virus Gun ON - grip to aim, pull trigger to spray!");
    }

    public static void DisableVirusGun()
    {
        virusGunOn = false;
    }

    public static void VirusGun()
    {
        try
        {
            if (!virusGunOn)
                return;

            (RaycastHit _, GameObject NewPointer) gunData = RenderGun();

            GameObject pointer = gunData.NewPointer;

            if (pointer == null || ControllerInputPoller.TriggerFloat(XRNode.RightHand) <= 0.5f)
                return;

            if (Time.time < nextSprayTime)
                return;

            nextSprayTime = Time.time + 0.08f;

            Vector3    point  = pointer.transform.position;
            Color32    color  = factIndex % 2 == 0
                                        ? new Color32(77, 255, 158, 255)
                                        : new Color32(180, 140, 255, 255);

            factIndex++;

            VirusBurst(point, color);

            GorillaTagger tagger = GorillaTagger.Instance;

            if (tagger != null)
                tagger.StartVibration(true, 0.15f, 0.04f);
        }
        catch { }
    }

    private static bool       virusAuraOn;
    private static GameObject auraObject;

    public static void EnableVirusAura()
    {
        virusAuraOn = true;
        NotifiLib.SendNotification("Virus Aura ON - a green mist follows you!");

        if (auraObject != null)
            return;

        auraObject = new GameObject("RIFTIX_VirusAura");

        ParticleSystem particles = auraObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.loop            = true;
        main.playOnAwake     = true;
        main.duration        = 2f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.08f, 0.3f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
        main.startColor      = new Color(0.3f, 1f, 0.55f, 0.5f);
        main.gravityModifier = -0.12f;
        main.maxParticles    = 140;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 28f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType       = ParticleSystemShapeType.Sphere;
        shape.radius          = 0.4f;
        shape.radiusThickness = 1f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;

        Gradient fade = new();
        fade.SetKeys(
                [
                        new GradientColorKey(new Color(0.3f, 1f, 0.55f), 0f),
                        new GradientColorKey(new Color(0.75f, 1f, 0.4f), 1f),
                ],
                [new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f)]);

        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);

        particles.Play();
    }

    public static void DisableVirusAura()
    {
        virusAuraOn = false;

        if (auraObject != null)
        {
            UnityEngine.Object.Destroy(auraObject);
            auraObject = null;
        }
    }

    public static void VirusAura()
    {
        try
        {
            if (!virusAuraOn || auraObject == null)
                return;

            Transform head = GorillaTagger.Instance.headCollider?.transform;

            if (head != null)
                auraObject.transform.position = head.position;
        }
        catch { }
    }

    private static bool  autoFactsOn;
    private static float nextFactTime;

    public static void EnableAutoFacts()
    {
        autoFactsOn  = true;
        nextFactTime = Time.time + 1f;
    }

    public static void DisableAutoFacts()
    {
        autoFactsOn = false;
    }

    public static void AutoFacts()
    {
        try
        {
            if (!autoFactsOn || Time.time < nextFactTime)
                return;

            nextFactTime = Time.time + 9f;
            NextFact();
        }
        catch { }
    }

    private static void VirusBurst(Vector3 position, Color32 color)
    {
        GameObject virusObject = new("RIFTIX_VirusBurst");
        virusObject.transform.position = position;

        ParticleSystem particles = virusObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.loop            = false;
        main.playOnAwake     = true;
        main.duration        = 0.4f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.25f, 0.85f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.13f);
        main.startColor      = (Color)color;
        main.gravityModifier = -0.3f;
        main.maxParticles    = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts([new ParticleSystem.Burst(0f, 10)]);

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType       = ParticleSystemShapeType.Sphere;
        shape.radius          = 0.05f;
        shape.radiusThickness = 1f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;

        Gradient fade = new();
        fade.SetKeys(
                [new GradientColorKey((Color)color, 0f), new GradientColorKey((Color)color, 1f)],
                [new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f)]);

        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);

        UnityEngine.Object.Destroy(virusObject, 2f);
    }
}
