using System.Collections.Generic;
using GorillaLocomotion;
using StupidTemplate.Menu;
using UnityEngine;
using static StupidTemplate.Menu.Main;
using static StupidTemplate.Mods.GunHelper;

namespace StupidTemplate.Mods;

// Gun mods that ride the shared aim line in GunLib.Render(). Everything in
// here is local only: the ray, the dot and whatever the gun drops belong to
// your own game and no network command goes out.
public abstract class Guns
{
    private const float GrappleSpeed  = 14f;
    private const float PaintInterval = 0.08f;
    private const float BalloonLife   = 8f;
    private const float BalloonScale  = 0.12f;
    private const float BeaconLife    = 25f;

    private const int MaxPaintSplats = 120;
    private const int MaxBeacons     = 6;

    private sealed class TimedObject
    {
        public GameObject Object;
        public float      Born;
    }

    private static readonly List<TimedObject> balloons     = new();
    private static readonly List<TimedObject> beacons      = new();
    private static readonly List<GameObject>  paintSplats  = new();

    private static ParticleSystem confettiSpray;
    private static Shader        sprayShader;
    private static Shader        textShader;

    private static bool  balloonGripped;
    private static bool  beaconGripped;
    private static float nextPaintAt;

    // ---- shared aim -----------------------------------------------------

    // GunLib.Render() is called first on purpose: it is what picks the active
    // hand, so Trigger (the grip on that hand) is only read once the hand is
    // up to date. The ray and dot it hands back are then resolved to a
    // single aim point, preferring the dot the player can actually see.
    private static bool AimFrom(RaycastHit ray, GameObject pointer, out Vector3 point)
    {
        if (pointer != null && pointer.activeSelf)
        {
            point = pointer.transform.position;

            return true;
        }

        point = ray.collider != null ? ray.point : Vector3.zero;

        return ray.collider != null;
    }

    private static Material SprayMaterial()
    {
        if (sprayShader == null)
        {
            sprayShader =
                    Shader.Find("Universal Render Pipeline/Particles/Unlit")
                    ?? Shader.Find("Universal Render Pipeline/Particles/Lit")
                    ?? Shader.Find("Particles/Standard Unlit")
                    ?? Shader.Find("Sprites/Default");
        }

        return sprayShader != null
                ? new Material(sprayShader)
                : null;
    }

    // The flat unlit look the gun line and pointer already use. Looked up
    // once: the paint gun asks for it a dozen times a second.
    private static Material TextMaterial()
    {
        if (textShader == null)
            textShader = Shader.Find("GUI/Text Shader");

        return textShader != null
                ? new Material(textShader)
                : null;
    }

    private static void ClearList(List<TimedObject> list)
    {
        foreach (TimedObject entry in list)
        {
            if (entry.Object != null)
                Object.Destroy(entry.Object);
        }

        list.Clear();
    }

    // ---- Grapple Gun ----------------------------------------------------

    // Grip to pull yourself along the aim line, like a rope you fire into
    // the wall. Releases stop you dead a couple of metres short of it.
    public static void GrappleGun()
    {
        (RaycastHit ray, GameObject pointer) = GunLib.Render();

        if (!AimTrigger || !AimFrom(ray, pointer, out Vector3 target))
            return;

        GTPlayer player    = GTPlayer.Instance;
        GorillaTagger tagger = GorillaTagger.Instance;

        if (player == null || tagger == null)
            return;

        Vector3 from = player.transform.position;
        Vector3 to   = target + Vector3.up * 0.5f;

        float step = GrappleSpeed * Time.deltaTime;

        if (Vector3.Distance(from, to) <= step + 1.5f)
            return;

        player.transform.position      = Vector3.MoveTowards(from, to, step);
        tagger.rigidbody.linearVelocity = Vector3.zero;
    }

    // ---- Confetti Gun ---------------------------------------------------

    // Holds a cloud of confetti on the aim point for as long as the grip is
    // up. Grip fires, same as every other gun in the menu.
    public static void ConfettiGun()
    {
        (RaycastHit ray, GameObject pointer) = GunLib.Render();

        if (!Trigger)
            return;

        if (!AimFrom(ray, pointer, out Vector3 point))
            return;

        if (confettiSpray == null)
            confettiSpray = BuildConfettiSpray();

        int count = Mathf.Max(1, Mathf.RoundToInt(260f * Time.deltaTime));

        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
        emit.position      = point;
        emit.velocity      = Random.insideUnitSphere * 1.7f + Vector3.up * 0.7f;
        emit.startSize     = Random.Range(0.02f, 0.055f);
        emit.startLifetime = Random.Range(0.8f, 1.5f);
        emit.startColor    = Random.ColorHSV(0f, 1f, 0.75f, 1f, 0.9f, 1f);

        confettiSpray.Emit(emit, count);
    }

    public static void DisableConfettiGun()
    {
        if (confettiSpray != null)
        {
            Object.Destroy(confettiSpray.gameObject);
            confettiSpray = null;
        }
    }

    private static ParticleSystem BuildConfettiSpray()
    {
        GameObject holder = new GameObject("RIFTIX_ConfettiSpray");

        ParticleSystem particles = holder.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.loop            = true;
        main.playOnAwake     = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 600;
        main.startSpeed      = 0f;
        main.startSize       = 0.04f;
        main.startLifetime   = 1.2f;
        main.gravityModifier = 0.35f;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = 0.05f;

        ParticleSystemRenderer renderer = holder.GetComponent<ParticleSystemRenderer>();

        if (renderer != null)
        {
            Material material = SprayMaterial();

            if (material != null)
                renderer.material = material;

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        return particles;
    }

    // ---- Paint Gun ------------------------------------------------------

    // Stamps a flattened blob of paint onto whatever the ray is touching,
    // for as long as the trigger is held. Trigger fires so you can aim
    // without painting over everything, like Teleport Gun.
    public static void PaintGun()
    {
        (RaycastHit ray, GameObject pointer) = GunLib.Render();

        if (!AimTrigger || Time.time < nextPaintAt)
            return;

        if (!AimFrom(ray, pointer, out Vector3 point) || ray.collider == null)
            return;

        nextPaintAt = Time.time + PaintInterval;

        GameObject splat = GameObject.CreatePrimitive(PrimitiveType.Sphere);

        // Paint is decoration: no collider, so it never becomes something you
        // can climb on or bump into.
        Object.Destroy(splat.GetComponent<Collider>());

        splat.transform.position = point + ray.normal * 0.004f;
        splat.transform.rotation = Quaternion.LookRotation(ray.normal);

        float size = Random.Range(0.05f, 0.12f);
        splat.transform.localScale = new Vector3(size, size, 0.012f);

        Renderer renderer = splat.GetComponent<Renderer>();

        Material material = TextMaterial();

        if (renderer != null && material != null)
        {
            material.color    = Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.85f, 1f);
            renderer.material = material;
        }

        paintSplats.Add(splat);

        // Keep the trail of paint bounded so a long session never piles up
        // hundreds of objects in the scene.
        while (paintSplats.Count > MaxPaintSplats)
        {
            GameObject oldest = paintSplats[0];
            paintSplats.RemoveAt(0);

            if (oldest != null)
                Object.Destroy(oldest);
        }
    }

    public static void DisablePaintGun()
    {
        foreach (GameObject splat in paintSplats)
        {
            if (splat != null)
                Object.Destroy(splat);
        }

        paintSplats.Clear();
        nextPaintAt = 0f;
    }

    // ---- Balloon Gun ----------------------------------------------------

    // One balloon per grip press. It drifts up, sways, then pops.
    public static void BalloonGun()
    {
        (RaycastHit ray, GameObject pointer) = GunLib.Render();

        bool grip = Trigger;

        if (grip && !balloonGripped && AimFrom(ray, pointer, out Vector3 point))
            SpawnBalloon(point);

        balloonGripped = grip;

        TickBalloons();
    }

    public static void DisableBalloonGun()
    {
        ClearList(balloons);
        balloonGripped = false;
    }

    private static void SpawnBalloon(Vector3 point)
    {
        GameObject balloon = GameObject.CreatePrimitive(PrimitiveType.Sphere);

        Object.Destroy(balloon.GetComponent<Collider>());

        balloon.transform.position = point;
        balloon.transform.localScale = Vector3.one * BalloonScale;

        Renderer renderer = balloon.GetComponent<Renderer>();

        Material material = TextMaterial();

        if (renderer != null && material != null)
        {
            material.color    = Random.ColorHSV(0f, 1f, 0.55f, 0.9f, 0.85f, 1f);
            renderer.material = material;
        }

        balloons.Add(new TimedObject { Object = balloon, Born = Time.time });
    }

    private static void TickBalloons()
    {
        float now = Time.time;

        for (int i = balloons.Count - 1; i >= 0; i--)
        {
            TimedObject entry = balloons[i];

            if (entry.Object == null)
            {
                balloons.RemoveAt(i);
                continue;
            }

            float age = now - entry.Born;

            if (age >= BalloonLife)
            {
                Object.Destroy(entry.Object);
                balloons.RemoveAt(i);
                continue;
            }

            Vector3 drift =
                    new Vector3(
                            Mathf.Sin(now * 1.7f + entry.Born),
                            0f,
                            Mathf.Cos(now * 1.3f + entry.Born)) * 0.12f;

            entry.Object.transform.position +=
                    (Vector3.up * 0.55f + drift) * Time.deltaTime;

            // Pop over the last half second instead of vanishing.
            entry.Object.transform.localScale =
                    Vector3.one *
                    (age > BalloonLife - 0.5f
                            ? Mathf.Lerp(0f, BalloonScale, (BalloonLife - age) / 0.5f)
                            : BalloonScale);
        }
    }

    // ---- Beacon Gun -----------------------------------------------------

    // Drops a lit marker post at the aim point, one per grip press. The
    // oldest goes once you are past the cap, and turning the mod off clears
    // them all.
    public static void BeaconGun()
    {
        (RaycastHit ray, GameObject pointer) = GunLib.Render();

        bool grip = Trigger;

        if (grip && !beaconGripped && AimFrom(ray, pointer, out Vector3 point))
            SpawnBeacon(point);

        beaconGripped = grip;

        TickBeacons();
    }

    public static void DisableBeaconGun()
    {
        ClearList(beacons);
        beaconGripped = false;
    }

    private static void SpawnBeacon(Vector3 point)
    {
        GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        Object.Destroy(beacon.GetComponent<Collider>());

        beacon.transform.position  = point + Vector3.up * 0.5f;
        beacon.transform.localScale = new Vector3(0.06f, 0.5f, 0.06f);

        Color colour = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.1f, 1f), 0.8f, 1f);

        Renderer renderer = beacon.GetComponent<Renderer>();

        Material material = TextMaterial();

        if (renderer != null && material != null)
        {
            material.color    = colour;
            renderer.material = material;
        }

        GameObject lamp = new GameObject("RIFTIX_BeaconLight");
        lamp.transform.SetParent(beacon.transform, false);
        lamp.transform.localPosition = new Vector3(0f, 0.6f, 0f);

        Light light = lamp.AddComponent<Light>();
        light.type      = LightType.Point;
        light.range     = 5f;
        light.intensity = 2f;
        light.color     = colour;
        light.shadows   = LightShadows.None;

        beacons.Add(new TimedObject { Object = beacon, Born = Time.time });

        while (beacons.Count > MaxBeacons)
        {
            TimedObject oldest = beacons[0];
            beacons.RemoveAt(0);

            if (oldest.Object != null)
                Object.Destroy(oldest.Object);
        }
    }

    private static void TickBeacons()
    {
        float now = Time.time;

        for (int i = beacons.Count - 1; i >= 0; i--)
        {
            TimedObject entry = beacons[i];

            if (entry.Object == null)
            {
                beacons.RemoveAt(i);
                continue;
            }

            if (now - entry.Born < BeaconLife)
                continue;

            Object.Destroy(entry.Object);
            beacons.RemoveAt(i);
        }
    }
}
