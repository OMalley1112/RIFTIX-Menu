using UnityEngine;
using static StupidTemplate.Menu.Main;

namespace StupidTemplate.Mods;

// Hand written "Fun Mods" tab: local only eye candy you can flip on in a
// public lobby. Nothing in here reaches across the network. Every toggle
// lazily builds its own objects inside method() and tears them down again
// in disableMethod, so leaving the page never leaks GameObjects behind.
public abstract class Fun
{
    private static Shader particleShader;

    // Rainbow Trail
    private static TrailRenderer  rainbowTrail;
    private static ParticleSystem trailSparkles;

    // Finger Painter
    private static TrailRenderer leftFingerTrail;
    private static TrailRenderer rightFingerTrail;

    // Confetti Hands
    private static ParticleSystem leftConfetti;
    private static ParticleSystem rightConfetti;

    // Confetti Aura
    private static ParticleSystem confettiAura;

    // Sparkle Feet
    private static ParticleSystem footSparkles;

    // Orbiting Sparks
    private static ParticleSystem orbitSparks;
    private static float          orbitAngle;

    // Disco Head Light
    private static Light discoLight;

    // Zoom Lens
    private static float cachedFieldOfView = -1f;

    // Fireworks
    private static ParticleSystem fireworkSky;

    // Confetti Cannon
    private static ParticleSystem confettiCannon;

    // Snow Shower
    private static ParticleSystem snowShower;

    // Camera Flash
    private static Light flashLight;
    private static float flashStartedAt = -10f;
    private static float nextFlashAt;

    // Applause
    private static ParticleSystem clapBurst;
    private static bool           clapHand;
    private static float          nextClapAt;

    // Change Trail Width
    private static readonly string[] trailWidthNames =
    [
            "Hairline",
            "Thin",
            "Normal",
            "Thick",
            "Chunky",
    ];

    private static readonly float[] trailWidthValues =
    [
            0.02f,
            0.05f,
            0.09f,
            0.15f,
            0.24f,
    ];

    private static int trailWidthIndex = 2;

    public static string TrailWidthName =>
            trailWidthNames[trailWidthIndex];

    // Shared lookups: one shader for every particle system this tab spawns.
    private static Shader ParticleShader()
    {
        if (particleShader == null)
        {
            particleShader =
                    Shader.Find("Universal Render Pipeline/Particles/Unlit")
                    ?? Shader.Find("Universal Render Pipeline/Particles/Lit")
                    ?? Shader.Find("Particles/Standard Unlit")
                    ?? Shader.Find("Sprites/Default");
        }

        return particleShader;
    }

    private static Material ParticleMaterial()
    {
        Shader shader =
                ParticleShader() ?? Shader.Find("Sprites/Default");

        return shader != null
                ? new Material(shader)
                : null;
    }

    private static Gradient RainbowGradient()
    {
        Gradient gradient = new Gradient();

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.red,     0.00f),
                new GradientColorKey(Color.yellow,  0.17f),
                new GradientColorKey(Color.green,   0.34f),
                new GradientColorKey(Color.cyan,    0.51f),
                new GradientColorKey(Color.blue,    0.68f),
                new GradientColorKey(Color.magenta, 0.85f),
                new GradientColorKey(Color.red,     1.00f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.7f),
                new GradientAlphaKey(0f, 1f),
            });

        return gradient;
    }

    private static Gradient ConfettiGradient()
    {
        Gradient gradient = new Gradient();

        GradientColorKey[] colours = new GradientColorKey[6];

        for (int i = 0; i < colours.Length; i++)
        {
            colours[i] =
                    new GradientColorKey(
                            Random.ColorHSV(0f, 1f, 0.75f, 1f, 0.9f, 1f),
                            i / (float)(colours.Length - 1));
        }

        gradient.SetKeys(
            colours,
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.6f),
                new GradientAlphaKey(0f, 1f),
            });

        return gradient;
    }

    private static Gradient FadeGradient(Color colour)
    {
        Gradient gradient = new Gradient();

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(colour, 0f),
                new GradientColorKey(colour, 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.55f),
                new GradientAlphaKey(0f, 1f),
            });

        return gradient;
    }

    // Builds a looping billboard emitter. Mods that need their own motion
    // (falling snow, a firework boom) tweak the modules afterwards.
    private static ParticleSystem CreateEmitter(
            string   name,
            Gradient gradient,
            float    rate,
            float    speed,
            float    size,
            float    lifetime)
    {
        GameObject holder       = new GameObject(name);
        ParticleSystem particles = holder.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.loop            = true;
        main.playOnAwake     = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 400;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(lifetime);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
        main.startSize       = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor      = Color.white;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = 0.06f;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate);

        ParticleSystem.SizeOverLifetimeModule sizeOverLife = particles.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size    = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.25f));

        ParticleSystem.ColorOverLifetimeModule colourOverLife = particles.colorOverLifetime;
        colourOverLife.enabled = true;
        colourOverLife.color   = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer particleRenderer = holder.GetComponent<ParticleSystemRenderer>();

        if (particleRenderer != null)
        {
            Material material = ParticleMaterial();

            if (material != null)
                particleRenderer.material = material;

            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        particles.Play();

        return particles;
    }

    private static void SetDrift(
            ParticleSystem particles,
            float          horizontal,
            float          down,
            float          up)
    {
        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space   = ParticleSystemSimulationSpace.World;
        velocity.x       = new ParticleSystem.MinMaxCurve(-horizontal, horizontal);
        velocity.y       = new ParticleSystem.MinMaxCurve(down,       up);
        velocity.z       = new ParticleSystem.MinMaxCurve(-horizontal, horizontal);
    }

    private static void Clear(ref ParticleSystem particles)
    {
        if (particles == null)
            return;

        Object.Destroy(particles.gameObject);
        particles = null;
    }

    private static void Clear(ref TrailRenderer trail)
    {
        if (trail == null)
            return;

        Object.Destroy(trail.gameObject);
        trail = null;
    }

    private static void Clear(ref Light light)
    {
        if (light == null)
            return;

        Object.Destroy(light.gameObject);
        light = null;
    }

    // ------------------------------------------------------------------
    // Rainbow Trail
    // ------------------------------------------------------------------
    public static void RainbowTrail()
    {
        if (rainbowTrail == null)
        {
            GameObject holder = new GameObject("RIFTIX_RainbowTrail");

            rainbowTrail = holder.AddComponent<TrailRenderer>();
            rainbowTrail.time              = 0.45f;
            rainbowTrail.minVertexDistance = 0.02f;
            rainbowTrail.widthCurve        = TrailWidthCurve();
            rainbowTrail.colorGradient     = RainbowGradient();
            rainbowTrail.material          = ParticleMaterial();
        }

        if (trailSparkles == null)
        {
            trailSparkles =
                    CreateEmitter(
                            "RIFTIX_TrailSparkles",
                            RainbowGradient(),
                            16f,
                            0.4f,
                            0.045f,
                            0.7f);

            SetDrift(trailSparkles, 0.25f, -0.15f, 0.35f);
        }

        Vector3 head = GorillaTagger.Instance.headCollider.transform.position;

        rainbowTrail.transform.position   = head;
        trailSparkles.transform.position  = head;
    }

    public static void DisableRainbowTrail()
    {
        Clear(ref rainbowTrail);
        Clear(ref trailSparkles);
    }

    // ------------------------------------------------------------------
    // Finger Painter
    // ------------------------------------------------------------------
    public static void FingerPainter()
    {
        if (leftFingerTrail == null)
            leftFingerTrail = CreateFingerTrail("RIFTIX_LeftFingerTrail");

        if (rightFingerTrail == null)
            rightFingerTrail = CreateFingerTrail("RIFTIX_RightFingerTrail");

        leftFingerTrail.transform.position  = TrueLeftHand().position;
        rightFingerTrail.transform.position = TrueRightHand().position;
    }

    public static void DisableFingerPainter()
    {
        Clear(ref leftFingerTrail);
        Clear(ref rightFingerTrail);
    }

    private static TrailRenderer CreateFingerTrail(string name)
    {
        GameObject holder = new GameObject(name);

        TrailRenderer trail = holder.AddComponent<TrailRenderer>();
        trail.time              = 0.4f;
        trail.minVertexDistance = 0.015f;
        trail.widthCurve        = TrailWidthCurve();
        trail.colorGradient     = ConfettiGradient();
        trail.material          = ParticleMaterial();

        return trail;
    }

    // ------------------------------------------------------------------
    // Confetti Hands
    // ------------------------------------------------------------------
    public static void ConfettiHands()
    {
        if (leftConfetti == null)
        {
            leftConfetti =
                    CreateEmitter(
                            "RIFTIX_LeftConfetti",
                            ConfettiGradient(),
                            40f,
                            1.8f,
                            0.04f,
                            0.9f);

            SetDrift(leftConfetti, 0.55f, -0.5f, 0.6f);
        }

        if (rightConfetti == null)
        {
            rightConfetti =
                    CreateEmitter(
                            "RIFTIX_RightConfetti",
                            ConfettiGradient(),
                            40f,
                            1.8f,
                            0.04f,
                            0.9f);

            SetDrift(rightConfetti, 0.55f, -0.5f, 0.6f);
        }

        leftConfetti.transform.position  = TrueLeftHand().position;
        rightConfetti.transform.position = TrueRightHand().position;
    }

    public static void DisableConfettiHands()
    {
        Clear(ref leftConfetti);
        Clear(ref rightConfetti);
    }

    // ------------------------------------------------------------------
    // Confetti Aura
    // ------------------------------------------------------------------
    public static void ConfettiAura()
    {
        if (confettiAura == null)
        {
            confettiAura =
                    CreateEmitter(
                            "RIFTIX_ConfettiAura",
                            ConfettiGradient(),
                            34f,
                            0.5f,
                            0.045f,
                            1.2f);

            ParticleSystem.ShapeModule shape = confettiAura.shape;
            shape.shapeType       = ParticleSystemShapeType.Sphere;
            shape.radius          = 0.45f;
            shape.radiusThickness = 1f;

            SetDrift(confettiAura, 0.15f, 0.25f, 0.6f);
        }

        confettiAura.transform.position = GorillaTagger.Instance.headCollider.transform.position;
    }

    public static void DisableConfettiAura()
    {
        Clear(ref confettiAura);
    }

    // ------------------------------------------------------------------
    // Sparkle Feet
    // ------------------------------------------------------------------
    public static void SparkleFeet()
    {
        if (footSparkles == null)
        {
            footSparkles =
                    CreateEmitter(
                            "RIFTIX_FootSparkles",
                            RainbowGradient(),
                            30f,
                            0.5f,
                            0.04f,
                            0.6f);

            SetDrift(footSparkles, 0.3f, -0.1f, 0.45f);
        }

        footSparkles.transform.position =
                GorillaTagger.Instance.bodyCollider.transform.position - Vector3.up * 0.3f;
    }

    public static void DisableSparkleFeet()
    {
        Clear(ref footSparkles);
    }

    // ------------------------------------------------------------------
    // Orbiting Sparks
    // ------------------------------------------------------------------
    public static void OrbitingSparks()
    {
        if (orbitSparks == null)
        {
            orbitSparks =
                    CreateEmitter(
                            "RIFTIX_OrbitSparks",
                            RainbowGradient(),
                            26f,
                            0.2f,
                            0.05f,
                            0.5f);

            ParticleSystem.MainModule main = orbitSparks.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.2f);

            SetDrift(orbitSparks, 0.12f, -0.05f, 0.2f);
        }

        orbitAngle += 110f * Time.deltaTime;

        Vector3 centre  = GorillaTagger.Instance.headCollider.transform.position;
        Quaternion spin = Quaternion.Euler(0f, orbitAngle, 0f);

        orbitSparks.transform.position =
                centre + spin * new Vector3(0.45f, 0.12f * Mathf.Sin(Time.time * 2.4f), 0f);
    }

    public static void DisableOrbitingSparks()
    {
        Clear(ref orbitSparks);
        orbitAngle = 0f;
    }

    // ------------------------------------------------------------------
    // Disco Head Light
    // ------------------------------------------------------------------
    public static void DiscoHeadLight()
    {
        if (discoLight == null)
        {
            GameObject holder = new GameObject("RIFTIX_DiscoLight");

            discoLight = holder.AddComponent<Light>();
            discoLight.type      = LightType.Point;
            discoLight.range     = 7f;
            discoLight.intensity = 2.6f;
            discoLight.shadows   = LightShadows.None;
        }

        discoLight.transform.position =
                GorillaTagger.Instance.headCollider.transform.position + Vector3.up * 0.25f;

        discoLight.color =
                Color.HSVToRGB(Mathf.Repeat(Time.time * 0.35f, 1f), 0.85f, 1f);
    }

    public static void DisableDiscoHeadLight()
    {
        Clear(ref discoLight);
    }

    // ------------------------------------------------------------------
    // Zoom Lens
    // ------------------------------------------------------------------
    public static void ZoomLens()
    {
        Camera camera = Camera.main;

        if (camera == null)
            return;

        if (cachedFieldOfView < 0f)
            cachedFieldOfView = camera.fieldOfView;

        camera.fieldOfView =
                Mathf.Lerp(camera.fieldOfView, 28f, Time.deltaTime * 8f);
    }

    public static void DisableZoomLens()
    {
        Camera camera = Camera.main;

        if (camera != null && cachedFieldOfView > 0f)
            camera.fieldOfView = cachedFieldOfView;

        cachedFieldOfView = -1f;
    }

    // ------------------------------------------------------------------
    // Fireworks
    // ------------------------------------------------------------------
    public static void Fireworks()
    {
        if (fireworkSky == null)
        {
            fireworkSky =
                    CreateEmitter(
                            "RIFTIX_Fireworks",
                            RainbowGradient(),
                            0f,
                            3f,
                            0.07f,
                            1.8f);

            ParticleSystem.MainModule main = fireworkSky.main;
            main.duration         = 1.8f;
            main.gravityModifier  = 0.35f;
            main.startSpeed       = new ParticleSystem.MinMaxCurve(2.4f, 3.6f);

            ParticleSystem.EmissionModule emission = fireworkSky.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.2f, 42) });

            ParticleSystem.ShapeModule shape = fireworkSky.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = 0.35f;

            SetDrift(fireworkSky, 1.3f, -0.5f, 1.5f);
        }

        Camera camera = Camera.main;

        Vector3 origin =
                camera != null
                        ? camera.transform.position
                          + camera.transform.forward * 6f
                          + Vector3.up * 2.5f
                        : GorillaTagger.Instance.headCollider.transform.position
                          + Vector3.up * 3f;

        fireworkSky.transform.position = origin;
    }

    public static void DisableFireworks()
    {
        Clear(ref fireworkSky);
    }

    // ------------------------------------------------------------------
    // Confetti Cannon
    // ------------------------------------------------------------------
    public static void ConfettiCannon()
    {
        if (confettiCannon == null)
        {
            confettiCannon =
                    CreateEmitter(
                            "RIFTIX_ConfettiCannon",
                            ConfettiGradient(),
                            55f,
                            2.6f,
                            0.05f,
                            1.1f);

            ParticleSystem.ShapeModule shape = confettiCannon.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle     = 15f;
            shape.radius    = 0.03f;

            ParticleSystem.MainModule main = confettiCannon.main;
            main.gravityModifier = 0.4f;

            SetDrift(confettiCannon, 0.35f, -0.6f, 0.15f);
        }

        (Vector3 position, Quaternion rotation, _, _, _) = TrueRightHand();

        confettiCannon.transform.SetPositionAndRotation(position, rotation);
    }

    public static void DisableConfettiCannon()
    {
        Clear(ref confettiCannon);
    }

    // ------------------------------------------------------------------
    // Snow Shower
    // ------------------------------------------------------------------
    public static void SnowShower()
    {
        if (snowShower == null)
        {
            snowShower =
                    CreateEmitter(
                            "RIFTIX_SnowShower",
                            FadeGradient(Color.white),
                            60f,
                            0.1f,
                            0.035f,
                            4f);

            ParticleSystem.MainModule main = snowShower.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.1f);

            ParticleSystem.ShapeModule shape = snowShower.shape;
            shape.shapeType       = ParticleSystemShapeType.Sphere;
            shape.radius          = 2.4f;
            shape.radiusThickness = 1f;

            SetDrift(snowShower, 0.05f, -0.55f, -0.3f);
        }

        snowShower.transform.position =
                GorillaTagger.Instance.headCollider.transform.position + Vector3.up * 1.5f;
    }

    public static void DisableSnowShower()
    {
        Clear(ref snowShower);
    }

    // ------------------------------------------------------------------
    // Camera Flash
    // ------------------------------------------------------------------
    public static void CameraFlash()
    {
        Camera camera = Camera.main;

        if (camera == null)
            return;

        if (flashLight == null)
        {
            GameObject holder = new GameObject("RIFTIX_CameraFlash");

            flashLight = holder.AddComponent<Light>();
            flashLight.type      = LightType.Point;
            flashLight.range     = 14f;
            flashLight.intensity = 0f;
            flashLight.shadows   = LightShadows.None;
        }

        if (Time.time >= nextFlashAt)
        {
            nextFlashAt    = Time.time + Random.Range(2f, 4.5f);
            flashStartedAt = Time.time;
        }

        float age = Time.time - flashStartedAt;

        flashLight.intensity =
                age < 0.1f
                        ? Mathf.Lerp(7f, 0f, age / 0.1f)
                        : 0f;

        flashLight.transform.position =
                camera.transform.position + camera.transform.forward * 0.5f;
    }

    public static void DisableCameraFlash()
    {
        Clear(ref flashLight);
        flashStartedAt = -10f;
        nextFlashAt    = 0f;
    }

    // ------------------------------------------------------------------
    // Applause
    // ------------------------------------------------------------------
    public static void Applause()
    {
        GorillaTagger tagger = GorillaTagger.Instance;

        if (tagger == null || VRRig.LocalRig == null)
            return;

        if (clapBurst == null)
        {
            clapBurst =
                    CreateEmitter(
                            "RIFTIX_ClapBurst",
                            ConfettiGradient(),
                            0f,
                            1.2f,
                            0.05f,
                            0.8f);

            ParticleSystem.ShapeModule shape = clapBurst.shape;
            shape.radius = 0.02f;
        }

        if (Time.time < nextClapAt)
            return;

        nextClapAt = Time.time + Random.Range(3.5f, 7f);

        tagger.StartVibration(true,  tagger.tagHapticStrength, tagger.tagHapticDuration);
        tagger.StartVibration(false, tagger.tagHapticStrength, tagger.tagHapticDuration);

        VRRig.LocalRig.PlayHandTapLocal(8, clapHand, 2.5f);
        clapHand = !clapHand;

        Vector3 centre = (TrueLeftHand().position + TrueRightHand().position) * 0.5f;

        clapBurst.transform.position = centre;

        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
        emit.position   = centre;
        emit.startColor = Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.9f, 1f);

        clapBurst.Emit(emit, 14);
    }

    public static void DisableApplause()
    {
        Clear(ref clapBurst);
        clapHand     = false;
        nextClapAt   = 0f;
    }

    // ------------------------------------------------------------------
    // Change Trail Width (incremental)
    // ------------------------------------------------------------------
    private static AnimationCurve TrailWidthCurve()
    {
        float width = trailWidthValues[trailWidthIndex];

        return AnimationCurve.EaseInOut(0f, width, 1f, width * 0.18f);
    }

    public static void ChangeTrailWidth(bool increment)
    {
        trailWidthIndex +=
                increment
                        ? 1
                        : -1;

        if (trailWidthIndex >= trailWidthValues.Length)
            trailWidthIndex = 0;
        else if (trailWidthIndex < 0)
            trailWidthIndex = trailWidthValues.Length - 1;

        if (rainbowTrail != null)
            rainbowTrail.widthCurve = TrailWidthCurve();

        if (leftFingerTrail != null)
            leftFingerTrail.widthCurve = TrailWidthCurve();

        if (rightFingerTrail != null)
            rightFingerTrail.widthCurve = TrailWidthCurve();
    }
}
