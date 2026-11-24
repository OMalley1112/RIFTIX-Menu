using StupidTemplate.Notifications;
using UnityEngine;

namespace StupidTemplate.Mods;

// Easter egg: a rat that hides somewhere around the map. Touch it with a
// hand or your body and you get a notification. It re-hides somewhere new
// each time, and after a while it gets bored and moves on its own. Local
// only - nothing about it crosses the network.
public static class Rat
{
    private const float TouchRange   = 0.45f;
    private const float WanderRange  = 25f;
    private const float FoundCooldown = 4f;
    private const float WanderEvery  = 14f;

    private static GameObject rat;
    private static float      nextWanderAt;
    private static float      nextTouchAt;
    private static int        findCount;

    // Called every frame from the console tick.
    public static void Tick()
    {
        GorillaTagger tagger = GorillaTagger.Instance;

        if (tagger == null || tagger.bodyCollider == null)
            return;

        Vector3 playerPos = tagger.bodyCollider.transform.position;

        if (rat == null)
        {
            if (!TrySpawn(playerPos))
                return;
        }
        else if (Time.time >= nextWanderAt)
        {
            // Never found: the rat scurries off to a new hiding spot.
            if (!TrySpawn(playerPos))
                return;
        }

        if (Time.time < nextTouchAt)
            return;

        Vector3 ratPos = rat.transform.position;

        bool touched =
                Vector3.Distance(ratPos, tagger.headCollider.transform.position) < TouchRange ||
                Vector3.Distance(ratPos, tagger.leftHandTransform.position)        < TouchRange ||
                Vector3.Distance(ratPos, tagger.rightHandTransform.position)       < TouchRange;

        if (!touched)
            return;

        findCount++;
        nextTouchAt = Time.time + FoundCooldown;

        NotifiLib.SendNotification(
                findCount == 1
                        ? "You found the rat! <color=yellow>It squeaks and bolts.</color>"
                        : "You found the rat again! <color=yellow>(" + findCount + ")</color>");

        try
        {
            GorillaTagger.Instance.StartVibration(true, GorillaTagger.Instance.tagHapticStrength, 0.3f);
            GorillaTagger.Instance.StartVibration(false, GorillaTagger.Instance.tagHapticStrength, 0.3f);
        }
        catch
        {
            // ignored
        }

        // Bolt somewhere new straight away.
        TrySpawn(playerPos);
    }

    // Drops a fresh rat on walkable ground near the player. Returns false
    // when the map has no suitable spot this second; the next frame tries
    // again rather than giving up.
    private static bool TrySpawn(Vector3 playerPos)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector2 circle = Random.insideUnitCircle * WanderRange;
            Vector3 origin = playerPos + new Vector3(circle.x, 12f, circle.y);

            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 30f))
                continue;

            // Skip walls and ceilings: only flat-enough, walkable ground.
            if (Vector3.Dot(hit.normal, Vector3.up) < 0.75f)
                continue;

            if (Vector3.Distance(hit.point, playerPos) < 4f)
                continue;

            if (rat == null)
                rat = BuildRat();

            rat.transform.position = hit.point + Vector3.up * 0.06f;
            rat.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            rat.SetActive(true);

            nextWanderAt = Time.time + WanderEvery;
            nextTouchAt  = Time.time + 1f;

            return true;
        }

        return false;
    }

    private static GameObject BuildRat()
    {
        GameObject root = new GameObject("RIFTIX_Rat");

        // Body: a squashed capsule lying flat.
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Object.Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.055f, 0f);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.transform.localScale     = new Vector3(0.09f, 0.09f, 0.06f);
        Paint(body, new Color(0.28f, 0.24f, 0.22f));

        // Head.
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(head.GetComponent<Collider>());
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0f, 0.06f, 0.09f);
        head.transform.localScale    = Vector3.one * 0.07f;
        Paint(head, new Color(0.32f, 0.27f, 0.24f));

        // Ears.
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject ear = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(ear.GetComponent<Collider>());
            ear.transform.SetParent(root.transform, false);
            ear.transform.localPosition = new Vector3(side * 0.03f, 0.095f, 0.075f);
            ear.transform.localScale    = new Vector3(0.028f, 0.028f, 0.014f);
            Paint(ear, new Color(0.55f, 0.35f, 0.33f));
        }

        // Eyes.
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(eye.GetComponent<Collider>());
            eye.transform.SetParent(root.transform, false);
            eye.transform.localPosition = new Vector3(side * 0.024f, 0.07f, 0.115f);
            eye.transform.localScale    = Vector3.one * 0.014f;
            Paint(eye, Color.black);
        }

        // Snout.
        GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(nose.GetComponent<Collider>());
        nose.transform.SetParent(root.transform, false);
        nose.transform.localPosition = new Vector3(0f, 0.055f, 0.128f);
        nose.transform.localScale    = Vector3.one * 0.022f;
        Paint(nose, new Color(0.85f, 0.45f, 0.5f));

        // Tail: a thin stretched sphere trailing behind.
        GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(tail.GetComponent<Collider>());
        tail.transform.SetParent(root.transform, false);
        tail.transform.localPosition = new Vector3(0f, 0.035f, -0.105f);
        tail.transform.localScale    = new Vector3(0.014f, 0.014f, 0.13f);
        Paint(tail, new Color(0.7f, 0.5f, 0.5f));

        return root;
    }

    private static void Paint(GameObject part, Color colour)
    {
        Renderer rend = part.GetComponent<Renderer>();

        if (rend == null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("Standard")
                        ?? Shader.Find("Sprites/Default");

        if (shader == null)
            return;

        Material skin = new(shader);

        if (skin.HasProperty("_BaseColor"))
            skin.SetColor("_BaseColor", colour);

        if (skin.HasProperty("_Color"))
            skin.SetColor("_Color", colour);

        rend.material = skin;
    }

    // Wipe the rat when the plugin unloads.
    public static void Clear()
    {
        if (rat != null)
            Object.Destroy(rat);

        rat       = null;
        findCount = 0;
    }
}
