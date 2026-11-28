using System.Collections.Generic;
using UnityEngine;

namespace StupidTemplate.Mods;

// The "Sentry" power set. Everything in here mutates ONLY this client's view:
// no GTConsole.ExecuteCommand is ever issued, so no Photon event goes out and
// the rest of the room is completely unaffected. Flip a toggle off and the
// original colours come straight back.
public abstract class Sentry
{
    // Reading a material's main colour works for both URP Lit (_BaseColor) and
    // the older Standard shader (_Color); guard so we never touch a material
    // that has no colour property at all.
    private static Color ReadMainColor(Material m)
    {
        if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
        if (m.HasProperty("_Color"))     return m.GetColor("_Color");
        return Color.white;
    }

    private static void WriteMainColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
    }

    // ------------------------------------------------------------------
    // Sentry Rig - your own body goes solid black with white eyes.
    //
    // renderer.material returns an instanced copy, so only YOUR rig changes
    // and nobody else's body turns black. Re-applied every frame while the
    // toggle is on so a cosmetic refresh can't overwrite it; the original
    // colour is only captured the first time we see each renderer.
    // ------------------------------------------------------------------
    private static readonly Dictionary<Renderer, Color> rigOriginal = new();

    public static void SentryRig()
    {
        VRRig rig = VRRig.LocalRig;

        if (rig == null)
            return;

        Renderer[] renderers = rig.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];

            if (r == null)
                continue;

            Material m = r.material;

            if (m == null)
                continue;

            if (!rigOriginal.ContainsKey(r))
                rigOriginal[r] = ReadMainColor(m);

            // Anything that reads as an eye turns white; the rest goes black.
            bool isEye = r.name.IndexOf("eye", System.StringComparison.OrdinalIgnoreCase) >= 0;

            WriteMainColor(m, isEye ? Color.white : Color.black);
        }
    }

    public static void SentryRigOff()
    {
        foreach (KeyValuePair<Renderer, Color> kv in rigOriginal)
        {
            Renderer r = kv.Key;

            if (r == null)
                continue;

            Material m = r.material;

            if (m == null)
                continue;

            WriteMainColor(m, kv.Value);
        }

        rigOriginal.Clear();
    }

    // ------------------------------------------------------------------
    // Black Map - repaints the entire environment black for you only.
    //
    // One pass per enable (the map does not repaint itself), then a cheap
    // early-out every frame. Restored exactly on disable. Because nothing is
    // broadcast, other players keep their normal, visible map.
    // ------------------------------------------------------------------
    private static readonly Dictionary<Renderer, Color> mapOriginal = new();
    private static bool                                 mapApplied;

    public static void BlackMap()
    {
        if (mapApplied)
            return;

        // The environment lives under one root; if the map is not loaded yet
        // Find returns null and we simply try again on the next frame.
        GameObject env = GameObject.Find("Environment Objects");

        if (env == null)
            return;

        Renderer[] renderers = env.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];

            if (r == null)
                continue;

            Material m = r.material;

            if (m == null)
                continue;

            if (!mapOriginal.ContainsKey(r))
                mapOriginal[r] = ReadMainColor(m);

            WriteMainColor(m, Color.black);
        }

        mapApplied = true;
    }

    public static void BlackMapOff()
    {
        foreach (KeyValuePair<Renderer, Color> kv in mapOriginal)
        {
            Renderer r = kv.Key;

            if (r == null)
                continue;

            Material m = r.material;

            if (m == null)
                continue;

            WriteMainColor(m, kv.Value);
        }

        mapOriginal.Clear();
        mapApplied = false;
    }
}
