using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using StupidTemplate.Classes;
using StupidTemplate.Notifications;
using UnityEngine;
using GTConsole = StupidTemplate.Classes.Console.Console;
using HamburburData = StupidTemplate.Classes.Console.HamburburData;

namespace StupidTemplate.Menu;

// The small Console Assets page: a picture and the Thor hammer.
//
// Press the picture and the TikTok picture - a still photo with its song baked
// into a single mp4 - appears on a bare screen floating in front of you: no TV
// furniture, just the picture turned to face you. Every command goes out with
// ReceiverGroup.All, so the whole room spawns the same screen at the same spot
// showing the same picture; players who join later get it through Console's
// asset sync (which re-sends the texture and the video url).
//
// Press the hammer and Mjolnir - Lowpoly_hamm wrapped in a root named
// ThorHammer, longest edge fitted to 0.55 m, colliders stripped at build time -
// lands on your left hand, also networked. Hold it above your head and the
// whole room gets a lightning strike on it; late joiners get the hammer through
// the same asset sync (anchor and grip pose re-sent).
//
// The Steam Icon is the glowing Steam logo: it spawns from this repo's
// bundles/steamicon, sits on an anchor you pick (hand or head) and animates
// on the owner's client - bobbing, spinning, glowing and shedding sparks.
// Only the client that pressed the button runs the animation; everyone else
// just sees the anchored object through the usual asset sync.
public abstract class ConsoleAssetPages
{
    // The bare-screen asset from the Console bundle: root "VideoPlayer" with a
    // single Plane child "Video" (built-in 10x10 plane on a custom VideoMat
    // shader - its _MainTex is exactly what asset-settexture writes, so the
    // photo lands on the mesh even though the VideoPlayer itself renders to a
    // RenderTexture nobody displays).
    private const string ScreenBundle = "console.main1";
    private const string ScreenAsset  = "VideoPlayer";
    private const string ScreenChild  = "Video";

    // "The Park is Closed" (Jurassic World OST) under the TikTok picture,
    // encoded as park.mp4 and committed to this menu's public repo. Served
    // straight off raw.githubusercontent.com: no redirect hop for the player
    // to follow, and Range requests are answered, which is what Unity's
    // VideoPlayer needs to stream a file. The VideoPlayer's audio runs in
    // Direct mode, so the song plays without an AudioSource.
    private const string PictureVideoUrl =
            "https://raw.githubusercontent.com/OMalley1112/RIFTIX-Menu/main/media/park.mp4";

    // The photo itself on the plane's _MainTex: the video frames go to an
    // internal RenderTexture that is never displayed, so this is what the room
    // actually sees. Portrait shot, so the plane is scaled to match its
    // 2160x2752 aspect instead of the stock 16:9.
    private const string PictureImageUrl =
            "https://raw.githubusercontent.com/OMalley1112/RIFTIX-Menu/main/media/park.jpg";

    // How far in front of your face the picture floats.
    private const float SpawnDistance = 3f;

    // The plane measures 15.24 x 8.52 units at scale 1 (10 unit mesh through
    // the child's 1.524/0.852 scale), so these give roughly 0.95 x 1.20 m -
    // the photo's own aspect ratio, about 22 degrees tall at 3 metres.
    private static readonly Vector3 PictureScale = new(0.062f, 0.141f, 0.062f);

    private static int pictureAssetId = -1;

    // The Thor hammer: Lowpoly_hamm wrapped in a root named ThorHammer, its
    // longest edge fitted to 0.55 m and its colliders stripped at build time
    // (Assets/Build Thor Hammer Bundle in the bundle project), committed to
    // this repo under bundles/ - ServerData has no room for it, so Console
    // falls back to RiftixAssetsURL when the download there misses.
    private const string HammerBundle = "thor_hammer";
    private const string HammerAsset  = "ThorHammer";

    // BindObject's anchor slots: 0 head, 1 left hand, 2 right hand, 3 body.
    private const int HammerAnchor = 1;

    // How far above your head the hammer must be to call a bolt; the hand has
    // to drop back below your head between strikes (hysteresis - holding it up
    // there cannot machine-gun bolts).
    private const float RaiseHeight = 0.2f;

    private static int hammerAssetId = -1;

    public static string PictureDisplay()
    {
        ButtonInfo button = Buttons.GetIndex("TikTok Picture");

        return
                $"TikTok Picture: {(button != null && button.enabled
                                             ? "<color=green>ON</color>"
                                             : "<color=red>OFF</color>")}";
    }

    public static string HammerDisplay()
    {
        ButtonInfo button = Buttons.GetIndex("Thor Hammer");

        return
                $"Thor Hammer: {(button != null && button.enabled
                                              ? "<color=green>ON</color>"
                                              : "<color=red>OFF</color>")}";
    }

    public static void SpawnPicture()
    {
        if (!CanSpawn(out string reason))
        {
            // Saved switch states are replayed at startup before the menu has
            // ever been opened: that path flips back off quietly (the state
            // saves as OFF, so it cannot come back every launch). Any real
            // press - VR menu or desktop GUI - gets the refusal reason.
            SetSwitchOff("TikTok Picture");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification(reason);

            return;
        }

        VRRig     localRig = VRRig.LocalRig;
        Transform head     = localRig != null && localRig.head != null && localRig.head.rigTarget != null
                                     ? localRig.head.rigTarget.transform
                                     : null;

        if (head == null)
        {
            SetSwitchOff("TikTok Picture");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification("Could not find your head - try again inside a room.");

            return;
        }

        // Float it SpawnDistance metres ahead of you at eye height, turned to
        // face you. The plane's normal points along the root's +Z after the
        // child's built-in 90 degree tilt, and its UVs run so that +Z at the
        // viewer keeps the photo upright and unmirrored - so plain
        // LookRotation back at your head is the whole orientation.
        Vector3 forward = head.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();

        Vector3 position = head.position + forward * SpawnDistance;

        Vector3 toPlayer = head.position - position;
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude < 0.0001f)
            toPlayer = -forward;

        toPlayer.Normalize();

        Quaternion rotation = Quaternion.LookRotation(toPlayer);

        pictureAssetId = GTConsole.GetFreeAssetID();

        // Spawn first; every command after it waits for the asset to exist
        // (Console gives each one up to ten seconds), so the screen arrives in
        // place with the picture already on it.
        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, ScreenBundle, ScreenAsset, pictureAssetId);
        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, pictureAssetId, position);
        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, pictureAssetId, rotation);
        GTConsole.ExecuteCommand("asset-setscale", ReceiverGroup.All, pictureAssetId, PictureScale);
        GTConsole.ExecuteCommand("asset-settexture", ReceiverGroup.All, pictureAssetId, ScreenChild,
                PictureImageUrl);
        GTConsole.ExecuteCommand("asset-setvideo", ReceiverGroup.All, pictureAssetId, ScreenChild,
                PictureVideoUrl);

        NotifiLib.SendNotification("Picture is up in front of you - the whole room sees it.");

        CoroutineManager.Instance.StartCoroutine(ResendPicture());
    }

    public static void RemovePicture()
    {
        if (Alive(pictureAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, pictureAssetId);

        pictureAssetId = -1;
    }

    // Anyone still downloading the bundle when the spawn went out would drop
    // the follow-up commands (Console waits ten seconds for the asset), so
    // re-send everything once everyone has had twelve seconds to catch up.
    // SetVideoURL skips a clip that is already playing, so the song does not
    // restart on this pass.
    private static IEnumerator ResendPicture()
    {
        yield return new WaitForSeconds(12f);

        if (!Alive(pictureAssetId))
            yield break;

        Transform screen = GTConsole.ConsoleAssets[pictureAssetId].AssetObject.transform;

        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, pictureAssetId, screen.position);
        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, pictureAssetId, screen.rotation);
        GTConsole.ExecuteCommand("asset-setscale", ReceiverGroup.All, pictureAssetId, PictureScale);
        GTConsole.ExecuteCommand("asset-settexture", ReceiverGroup.All, pictureAssetId, ScreenChild,
                PictureImageUrl);
        GTConsole.ExecuteCommand("asset-setvideo", ReceiverGroup.All, pictureAssetId, ScreenChild,
                PictureVideoUrl);
    }

    // Mjolnir on your left hand, networked to the whole room. The bundle is
    // committed to this repo (bundles/thor_hammer) and Console falls back to
    // RiftixAssetsURL when ServerData does not know it.
    public static void SpawnHammer()
    {
        if (!CanSpawn(out string reason))
        {
            // Same quiet flip-off as the picture: saved switch states replay
            // at startup before a room exists (a real press gets the reason).
            SetSwitchOff("Thor Hammer");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification(reason);

            return;
        }

        hammerAssetId = GTConsole.GetFreeAssetID();

        // Spawn first; every command after it waits for the asset to exist
        // (Console gives each one up to ten seconds). The anchor lands the
        // hammer on your left hand - BindObject's slot 1 - and the zero grip
        // pose puts its origin in the palm; colliders are stripped here too,
        // because a held collider lets you climb your own hammer.
        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, HammerBundle, HammerAsset, hammerAssetId);
        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, hammerAssetId);
        GTConsole.ExecuteCommand("asset-setanchor", ReceiverGroup.All, hammerAssetId, HammerAnchor,
                PhotonNetwork.LocalPlayer.ActorNumber);
        GTConsole.ExecuteCommand("asset-setlocalposition", ReceiverGroup.All, hammerAssetId, Vector3.zero);
        GTConsole.ExecuteCommand("asset-setlocalrotation", ReceiverGroup.All, hammerAssetId,
                Quaternion.identity);

        NotifiLib.SendNotification("Thor hammer on your left hand - raise it up to call lightning.");

        CoroutineManager.Instance.StartCoroutine(ResendHammer());
        CoroutineManager.Instance.StartCoroutine(WatchForRaise(hammerAssetId));
    }

    public static void RemoveHammer()
    {
        if (Alive(hammerAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, hammerAssetId);

        hammerAssetId = -1;
    }

    // Anyone still downloading the bundle when the spawn went out would drop
    // the follow-up commands (Console waits ten seconds for the asset), so
    // re-send the grip once everyone has had twelve seconds to catch up. The
    // anchor goes first: BindObject re-parents, so the zero pose must land
    // after it rather than be re-parented away.
    private static IEnumerator ResendHammer()
    {
        int id = hammerAssetId;

        yield return new WaitForSeconds(12f);

        if (!Alive(id))
            yield break;

        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, id);
        GTConsole.ExecuteCommand("asset-setanchor", ReceiverGroup.All, id, HammerAnchor,
                PhotonNetwork.LocalPlayer.ActorNumber);
        GTConsole.ExecuteCommand("asset-setlocalposition", ReceiverGroup.All, id, Vector3.zero);
        GTConsole.ExecuteCommand("asset-setlocalrotation", ReceiverGroup.All, id, Quaternion.identity);
    }

    // Only the hammer's owner watches: raise it above your head and the room
    // gets one lightning strike on it through the existing networked "strike"
    // command (the bolt is drawn upward from the position it is given, so it
    // erupts out of the hammer). Dropping the hand back below your head
    // re-arms the trigger, and the loop dies with the hammer or the room.
    private static IEnumerator WatchForRaise(int assetId)
    {
        bool armed = false;

        while (Alive(assetId))
        {
            VRRig     localRig = VRRig.LocalRig;
            Transform head     = localRig != null && localRig.head != null && localRig.head.rigTarget != null
                                         ? localRig.head.rigTarget.transform
                                         : null;

            if (head == null)
            {
                yield return null;
                continue;
            }

            Transform hammer = GTConsole.ConsoleAssets[assetId].AssetObject.transform;
            float     handY  = hammer.position.y;
            float     headY  = head.position.y;

            if (handY <= headY)
                armed = true;
            else if (armed && handY > headY + RaiseHeight)
            {
                armed = false;

                GTConsole.ExecuteCommand("strike", ReceiverGroup.All, hammer.position);
            }

            yield return null;
        }
    }

    // ---------------------------------------------------------------------
    // Steam Icon: the glowing Steam logo, ported from OMMALLEYGT's other
    // menu (CXS). The asset comes from this repo's bundles/steamicon (a
    // prefab named SteamIcon with its sound baked in) - ServerData does not
    // host it, so Console's download falls back to RiftixAssetsURL.
    // ---------------------------------------------------------------------

    private static int steamIconId = -1;
    private static bool steamIconOnHand;
    private static float steamIconAnimStart;
    private static Vector3 steamIconWorldSize = Vector3.one;
    private static Material[] steamIconMaterials;
    private static Light steamIconLight;
    private static ParticleSystem steamIconParticles;
    private static ParticleSystem steamIconSparks;
    private static TrailRenderer steamIconTrail;
    private static Texture2D steamParticleTexture;
    private static Texture2D shockwaveTexture;
    private static int steamIconAnchorMode;
    private static int steamIconMode;
    private static int steamIconColorTheme;
    private static int steamIconSizeMode = 1;
    private static bool steamIconSparksOn;
    private static Vector3 steamIconSmoothPos;
    private static bool steamIconHasSmoothPos;
    private static Vector3 steamIconFreePos;
    private static float steamIconSpin;
    private static float steamIconSpinBoost;
    private static GameObject steamIconShockwave;
    private static Material steamIconShockwaveMat;
    private static float steamIconShockwaveT;
    private static Vector3 steamIconShockwavePos;

    private static float steamIconSpinSpeed = 90f;
    private static float steamIconGlowMult = 1f;
    private static float steamIconFollowDist = 0.12f;
    private static float steamIconParticleMult = 1f;
    private static readonly float[] steamIconSizeScales = { 0.5f, 1f, 1.5f, 2f };

    private static Transform SteamIconAnchor
    {
        get
        {
            GorillaTagger tagger = GorillaTagger.Instance;
            if (tagger == null) return null;

            switch (steamIconAnchorMode)
            {
                case 1: return tagger.rightHandTransform;
                case 2: return tagger.headCollider != null ? tagger.headCollider.transform : null;
                default: return tagger.leftHandTransform;
            }
        }
    }

    public static string SteamIconDisplay()
    {
        ButtonInfo button = Buttons.GetIndex("Steam Icon");

        return
                $"Steam Icon: {(button != null && button.enabled
                                              ? "<color=green>ON</color>"
                                              : "<color=red>OFF</color>")}";
    }

    public static void SpawnSteamIcon()
    {
        if (!CanSpawn(out string reason))
        {
            // Same quiet flip-off as the picture and hammer: saved switch
            // states replay at startup before a room exists.
            SetSwitchOff("Steam Icon");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification(reason);

            return;
        }

        if (steamIconId >= 0)
            return;

        GorillaTagger tagger = GorillaTagger.Instance;

        if (tagger == null || tagger.bodyCollider == null)
        {
            SetSwitchOff("Steam Icon");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification("Could not find your body - try again inside a room.");

            return;
        }

        steamIconId = GTConsole.GetFreeAssetID();
        steamIconOnHand = false;
        steamIconMaterials = null;
        steamIconLight = null;
        steamIconParticles = null;

        Transform anchor = SteamIconAnchor;
        Vector3 spawnPosition = anchor != null
            ? anchor.position + (Vector3.up * 0.1f)
            : tagger.bodyCollider.transform.position + (Vector3.up * 0.35f);

        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, "steamicon", "SteamIcon", steamIconId);
        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, steamIconId);
        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, steamIconId, spawnPosition);
        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, steamIconId, Quaternion.identity);
        ApplySteamIconMultiplayerAnchor();

        GTConsole.ExecuteCommand("notify", ReceiverGroup.All, "OMALLEYGT Made this Console Assets");

        CoroutineManager.Instance.StartCoroutine(ResendSteamIcon());
    }

    private static void ApplySteamIconMultiplayerAnchor()
    {
        if (steamIconId < 0) return;

        int bindId = steamIconAnchorMode == 1 ? 2 : steamIconAnchorMode == 2 ? 0 : 1;
        Vector3 localOffset = steamIconAnchorMode == 2
            ? new Vector3(0f, 0.35f, 0.5f)
            : new Vector3(0f, 0.28f, 0.12f);

        GTConsole.ExecuteCommand("asset-setanchor", ReceiverGroup.All, steamIconId, bindId);
        GTConsole.ExecuteCommand("asset-setlocalposition", ReceiverGroup.All, steamIconId, localOffset);
        GTConsole.ExecuteCommand("asset-setlocalrotation", ReceiverGroup.All, steamIconId, Quaternion.identity);
    }

    // Anyone still downloading the bundle when the spawn went out would drop
    // the anchor commands (Console waits ten seconds for the asset), so
    // re-send the grip once everyone has had twelve seconds to catch up.
    private static IEnumerator ResendSteamIcon()
    {
        int id = steamIconId;

        yield return new WaitForSeconds(12f);

        if (id < 0 || steamIconId != id || !Alive(id))
            yield break;

        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, id);
        ApplySteamIconMultiplayerAnchor();
    }

    public static void DestroySteamIcon()
    {
        if (steamIconId < 0) return;

        int id = steamIconId;
        Material[] materials = steamIconMaterials;
        Light light = steamIconLight;
        ParticleSystem particles = steamIconParticles;
        ParticleSystem sparks = steamIconSparks;
        TrailRenderer trail = steamIconTrail;

        steamIconId = -1;
        steamIconOnHand = false;
        steamIconMaterials = null;
        steamIconLight = null;
        steamIconParticles = null;
        steamIconSparks = null;
        steamIconTrail = null;

        if (steamIconShockwave != null)
        {
            UnityEngine.Object.Destroy(steamIconShockwave);
            steamIconShockwave = null;
            steamIconShockwaveMat = null;
        }

        if (GTConsole.ConsoleAssets.TryGetValue(id, out GTConsole.ConsoleAsset asset) &&
            asset.AssetObject != null &&
            GorillaTagger.Instance != null)
        {
            asset.AssetObject.transform.SetParent(null, true);

            if (particles != null)
            {
                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTime = 0f;
            }

            if (sparks != null)
            {
                ParticleSystem.EmissionModule sparkEmission = sparks.emission;
                sparkEmission.rateOverTime = 0f;
            }

            if (trail != null)
                trail.emitting = false;

            CoroutineManager.Instance.StartCoroutine(DespawnSteamIconRoutine(asset.AssetObject, id, materials, light));
        }
        else
        {
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, id);
        }
    }

    private static IEnumerator DespawnSteamIconRoutine(GameObject icon, int id, Material[] materials, Light light)
    {
        float elapsed = 0f;
        const float duration = 0.35f;
        Vector3 startScale = icon.transform.localScale;
        Color despawnColor = CurrentSteamIconColor();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float glow = 1f - t;

            icon.transform.localScale = startScale * (1f - (t * t));
            icon.transform.Rotate(0f, 540f * Time.deltaTime, 0f, Space.World);

            SetSteamIconEmission(glow, materials, despawnColor);

            if (light != null)
                light.intensity = 2f * glow;

            yield return null;
        }

        SetSteamIconEmission(0f, materials, despawnColor);

        if (light != null)
            light.intensity = 0f;

        GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, id);

        if (icon != null)
            UnityEngine.Object.Destroy(icon);
    }

    // The knobs behind the incremental buttons. Changing the anchor re-sends
    // the networked grip too, so the room's copy moves with it; colour, size,
    // sparks and motion only paint the owner's own view, exactly like the
    // original menu did.
    public static void SteamIconAnchorNext(bool increment)
    {
        steamIconAnchorMode = Next(steamIconAnchorMode, 3, increment);

        if (steamIconId >= 0)
            ApplySteamIconMultiplayerAnchor();
    }

    public static void SteamIconMoveNext(bool increment) => steamIconMode = Next(steamIconMode, 3, increment);

    public static void SteamIconColorNext(bool increment) => steamIconColorTheme = Next(steamIconColorTheme, 6, increment);

    public static void SteamIconSizeNext(bool increment) => steamIconSizeMode = Next(steamIconSizeMode, steamIconSizeScales.Length, increment);

    // Wraps either way round: left on the bump goes back a step.
    private static int Next(int value, int length, bool increment) =>
            (value + (increment ? 1 : -1) + length) % length;

    public static void SteamIconSparksOn() => steamIconSparksOn = true;

    public static void SteamIconSparksOff() => steamIconSparksOn = false;

    public static string SteamAnchorDisplay()
    {
        string[] names = { "Left Hand", "Right Hand", "Head", };

        return $"Steam Anchor: {names[steamIconAnchorMode]}";
    }

    public static string SteamMoveDisplay()
    {
        string[] names = { "Follow", "Orbit", "Stay", };

        return $"Steam Move: {names[steamIconMode]}";
    }

    public static string SteamColorDisplay()
    {
        string[] names = { "Blue", "Rainbow", "Green", "Red", "Gold", "Purple", };

        return $"Steam Color: {names[steamIconColorTheme]}";
    }

    public static string SteamSizeDisplay()
    {
        string[] names = { "Small", "Medium", "Large", "XL", };

        return $"Steam Size: {names[steamIconSizeMode]}";
    }

    public static string SteamSparksDisplay()
    {
        ButtonInfo button = Buttons.GetIndex("Steam Sparks");

        return
                $"Steam Sparks: {(button != null && button.enabled
                                                ? "<color=green>ON</color>"
                                                : "<color=red>OFF</color>")}";
    }

    // Called every frame from Console.Update. Only the client that pressed
    // the button has steamIconId set, so remotes just keep the networked
    // anchor instead of fighting over the transform.
    public static void UpdateSteamIcon()
    {
        if (steamIconId < 0) return;

        if (!GTConsole.ConsoleAssets.TryGetValue(steamIconId, out GTConsole.ConsoleAsset asset) ||
            asset.AssetObject == null)
            return;

        GorillaTagger tagger = GorillaTagger.Instance;

        if (tagger == null || tagger.headCollider == null)
            return;

        Transform icon = asset.AssetObject.transform;
        Transform anchor = SteamIconAnchor;

        if (!steamIconOnHand && anchor != null)
        {
            steamIconWorldSize = icon.lossyScale;
            icon.SetParent(anchor, false);

            FixSteamIconMaterial(asset.AssetObject);
            CreateSteamIconFX(asset.AssetObject);
            CreateSteamIconShockwave(icon.position);

            steamIconAnimStart = Time.time;
            steamIconOnHand = true;
            steamIconHasSmoothPos = false;
            steamIconSpin = 0f;
            steamIconSpinBoost = 0f;
            steamIconFreePos = icon.position;
        }

        if (anchor == null)
            return;

        if (icon.parent != anchor)
            icon.SetParent(anchor, false);

        float t = Mathf.Clamp01((Time.time - steamIconAnimStart) / 0.6f);
        float elastic = ElasticOut(t);
        float breathe = t >= 1f ? 1f + (0.04f * Mathf.Sin(Time.time * 2.5f)) : 1f;

        float bob = Mathf.Sin(Time.time * 2f);
        bool headMode = steamIconAnchorMode == 2;
        Vector3 headForward = tagger.headCollider.transform.forward;

        float upOffset = headMode ? 0.35f + (bob * 0.02f) : 0.28f + (bob * 0.03f);
        float forwardOffset = headMode ? steamIconFollowDist * 4f : steamIconFollowDist;

        Vector3 targetPosition;

        switch (steamIconMode)
        {
            case 1:
                float orbitAngle = Time.time * 1.6f;
                Vector3 orbitCenter = tagger.headCollider.transform.position;
                targetPosition = orbitCenter
                    + new Vector3(Mathf.Cos(orbitAngle) * 0.55f, 0.25f + (bob * 0.05f), Mathf.Sin(orbitAngle) * 0.55f);
                break;
            case 2:
                targetPosition = steamIconFreePos + (Vector3.up * (bob * 0.05f));
                break;
            default:
                targetPosition = anchor.position
                    + (Vector3.up * upOffset)
                    + (headForward * forwardOffset);
                break;
        }

        targetPosition -= Vector3.up * (0.15f * (1f - EaseOutCubic(t)));

        if (!steamIconHasSmoothPos)
        {
            steamIconSmoothPos = targetPosition;
            steamIconHasSmoothPos = true;
        }

        Vector3 previousSmooth = steamIconSmoothPos;
        steamIconSmoothPos = Vector3.Lerp(steamIconSmoothPos, targetPosition, 1f - Mathf.Exp(-12f * Time.deltaTime));

        Vector3 followVelocity = (steamIconSmoothPos - previousSmooth) / Mathf.Max(Time.deltaTime, 0.0001f);
        float followSpeed = followVelocity.magnitude;

        icon.position = steamIconSmoothPos;

        steamIconSpinBoost = Mathf.Lerp(steamIconSpinBoost, Mathf.Clamp(followSpeed * 8f, 0f, 500f), Time.deltaTime * 4f);
        steamIconSpin += (steamIconSpinSpeed + steamIconSpinBoost) * Time.deltaTime;

        Quaternion baseRotation = Quaternion.Euler(bob * 8f, steamIconSpin, Mathf.Sin(Time.time * 1.6f) * 6f);

        if (followSpeed > 0.4f)
        {
            Vector3 leanAxis = Vector3.Cross(Vector3.up, followVelocity / followSpeed);
            if (leanAxis.sqrMagnitude > 0.001f)
                baseRotation = Quaternion.AngleAxis(Mathf.Clamp(followSpeed * 10f, 0f, 45f), leanAxis.normalized) * baseRotation;
        }

        icon.rotation = baseRotation;

        Vector3 parentScale = anchor.lossyScale;
        Vector3 targetWorld = steamIconWorldSize * (elastic * breathe * steamIconSizeScales[steamIconSizeMode]);
        icon.localScale = new Vector3(
            targetWorld.x / SafeScale(parentScale.x),
            targetWorld.y / SafeScale(parentScale.y),
            targetWorld.z / SafeScale(parentScale.z));

        Color themeColor = CurrentSteamIconColor();
        float flare = 1f + Mathf.Clamp(followSpeed * 0.25f, 0f, 1.5f);
        float pulse = (0.75f + (0.45f * Mathf.Sin(Time.time * 3f))) * t * flare * steamIconGlowMult;
        SetSteamIconEmission(pulse, steamIconMaterials, themeColor);

        if (steamIconLight != null)
        {
            steamIconLight.color = themeColor;
            steamIconLight.intensity = ((1.2f + (0.8f * Mathf.Sin(Time.time * 3f))) * t) * flare * steamIconGlowMult;
        }

        if (steamIconParticles != null)
        {
            ParticleSystem.MainModule particleMain = steamIconParticles.main;
            particleMain.startColor = new Color(themeColor.r, themeColor.g, themeColor.b, 0.45f);

            ParticleSystem.EmissionModule emission = steamIconParticles.emission;
            emission.rateOverTime = 22f * t * steamIconParticleMult;
        }

        if (steamIconSparks != null)
        {
            ParticleSystem.MainModule sparkMain = steamIconSparks.main;
            sparkMain.startColor = new Color(themeColor.r * 1.4f, themeColor.g * 1.4f, themeColor.b * 1.4f, 0.9f);

            ParticleSystem.EmissionModule sparkEmission = steamIconSparks.emission;
            sparkEmission.rateOverTime = steamIconSparksOn ? (8f + (followSpeed * 40f)) * t * steamIconParticleMult : 0f;
        }

        if (steamIconTrail != null)
            steamIconTrail.emitting = steamIconSparksOn && t >= 1f;

        UpdateSteamIconShockwave();
    }

    private static Color CurrentSteamIconColor()
    {
        switch (steamIconColorTheme)
        {
            case 1: return Color.HSVToRGB(Mathf.Repeat(Time.time * 0.15f, 1f), 0.65f, 1.2f);
            case 2: return new Color(0.2f, 1.2f, 0.5f);
            case 3: return new Color(1.3f, 0.25f, 0.3f);
            case 4: return new Color(1.3f, 0.95f, 0.3f);
            case 5: return new Color(1.2f, 0.35f, 1.1f);
            default: return new Color(0.45f, 0.75f, 1.1f);
        }
    }

    private static float ElasticOut(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;

        return (Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3f))) + 1f;
    }

    private static float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static float SafeScale(float scale)
    {
        return Mathf.Abs(scale) < 0.0001f ? 1f : scale;
    }

    private static void SetSteamIconEmission(float intensity, Material[] materials, Color glow)
    {
        if (materials == null) return;

        Color emission = glow * intensity;

        foreach (Material material in materials)
        {
            if (material != null)
                material.SetColor("_EmissionColor", emission);
        }
    }

    private static void FixSteamIconMaterial(GameObject iconObject)
    {
        Shader shader =
            Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Standard");

        if (shader == null)
            return;

        List<Material> materials = new List<Material>();

        foreach (Renderer renderer in iconObject.GetComponentsInChildren<Renderer>(true))
        {
            Material source = renderer.sharedMaterial;
            if (source == null) continue;

            Material replacement = new Material(shader);

            if (source.mainTexture != null)
            {
                replacement.mainTexture = source.mainTexture;
                replacement.SetTexture("_BaseMap", source.mainTexture);
            }

            replacement.color = Color.white;
            replacement.SetColor("_BaseColor", Color.white);
            replacement.EnableKeyword("_EMISSION");
            replacement.SetColor("_EmissionColor", CurrentSteamIconColor());
            replacement.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            renderer.sharedMaterial = replacement;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            materials.Add(replacement);
        }

        steamIconMaterials = materials.ToArray();
    }

    private static void CreateSteamIconFX(GameObject iconObject)
    {
        GameObject lightObject = new GameObject("SteamIconLight");
        lightObject.transform.SetParent(iconObject.transform, false);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.45f, 0.7f, 1f);
        light.range = 2.4f;
        light.intensity = 0f;
        light.shadows = LightShadows.None;

        steamIconLight = light;

        GameObject particleObject = new GameObject("SteamIconSteam");
        particleObject.transform.SetParent(iconObject.transform, false);

        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = 1.2f;
        main.startSpeed = 0.3f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.11f);
        main.startColor = new Color(0.55f, 0.8f, 1f, 0.45f);
        main.maxParticles = 100;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.02f;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.14f;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.4f));

        ParticleSystem.ColorOverLifetimeModule colorOverLife = particles.colorOverLifetime;
        colorOverLife.enabled = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0.3f, 0.5f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();

        Shader particleShader =
            Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Universal Render Pipeline/Particles/Lit")
            ?? Shader.Find("Particles/Standard Unlit")
            ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended")
            ?? Shader.Find("Sprites/Default");

        if (particleShader != null)
        {
            Material particleMaterial = new Material(particleShader);
            particleMaterial.mainTexture = GetSteamParticleTexture();
            particleRenderer.material = particleMaterial;
        }

        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;

        steamIconParticles = particles;

        GameObject sparkObject = new GameObject("SteamIconSparks");
        sparkObject.transform.SetParent(iconObject.transform, false);

        ParticleSystem sparks = sparkObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule sparkMainModule = sparks.main;
        sparkMainModule.loop = true;
        sparkMainModule.playOnAwake = true;
        sparkMainModule.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        sparkMainModule.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        sparkMainModule.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.035f);
        sparkMainModule.maxParticles = 120;
        sparkMainModule.simulationSpace = ParticleSystemSimulationSpace.World;
        sparkMainModule.gravityModifier = 0.25f;
        sparkMainModule.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule sparkEmission = sparks.emission;
        sparkEmission.rateOverTime = 0f;

        ParticleSystem.ShapeModule sparkShape = sparks.shape;
        sparkShape.shapeType = ParticleSystemShapeType.Sphere;
        sparkShape.radius = 0.1f;

        ParticleSystem.SizeOverLifetimeModule sparkSize = sparks.sizeOverLifetime;
        sparkSize.enabled = true;
        sparkSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

        ParticleSystem.ColorOverLifetimeModule sparkColorOverLife = sparks.colorOverLifetime;
        sparkColorOverLife.enabled = true;
        sparkColorOverLife.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer sparkRenderer = sparkObject.GetComponent<ParticleSystemRenderer>();

        if (particleShader != null)
        {
            Material sparkMaterial = new Material(particleShader);
            sparkMaterial.mainTexture = GetSteamParticleTexture();
            sparkRenderer.material = sparkMaterial;
        }

        sparkRenderer.renderMode = ParticleSystemRenderMode.Billboard;

        steamIconSparks = sparks;

        TrailRenderer trail = iconObject.AddComponent<TrailRenderer>();
        trail.time = 0.35f;
        trail.minVertexDistance = 0.025f;
        trail.widthCurve = AnimationCurve.EaseInOut(0f, 0.14f, 1f, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.emitting = false;

        if (particleShader != null)
        {
            Material trailMaterial = new Material(particleShader);
            trailMaterial.mainTexture = GetSteamParticleTexture();
            trail.material = trailMaterial;
        }

        ApplySteamIconTrailColor(trail, CurrentSteamIconColor());

        steamIconTrail = trail;
    }

    private static void ApplySteamIconTrailColor(TrailRenderer trail, Color color)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(0.65f, 0f), new GradientAlphaKey(0f, 1f) });

        trail.colorGradient = gradient;
    }

    private static void CreateSteamIconShockwave(Vector3 position)
    {
        if (steamIconShockwave == null)
        {
            steamIconShockwave = GameObject.CreatePrimitive(PrimitiveType.Quad);

            Collider collider = steamIconShockwave.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.Destroy(collider);

            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Standard");

            if (shader != null)
            {
                steamIconShockwaveMat = new Material(shader);
                steamIconShockwaveMat.mainTexture = GetShockwaveTexture();
                steamIconShockwave.GetComponent<Renderer>().sharedMaterial = steamIconShockwaveMat;
            }

            steamIconShockwave.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            steamIconShockwave.GetComponent<Renderer>().receiveShadows = false;
        }

        steamIconShockwavePos = position;
        steamIconShockwave.transform.position = position;
        steamIconShockwave.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        steamIconShockwave.transform.localScale = Vector3.one * 0.3f;
        steamIconShockwaveT = 0f;
    }

    private static void UpdateSteamIconShockwave()
    {
        if (steamIconShockwave == null || steamIconShockwaveT >= 0.6f)
            return;

        steamIconShockwaveT += Time.deltaTime;

        float p = Mathf.Clamp01(steamIconShockwaveT / 0.6f);
        float alpha = (1f - p) * (1f - p) * 0.8f;

        steamIconShockwave.transform.localScale = Vector3.one * (0.3f + (p * 2.4f));

        if (steamIconShockwaveMat != null)
        {
            Color color = CurrentSteamIconColor();
            steamIconShockwaveMat.SetColor("_BaseColor", new Color(color.r, color.g, color.b, alpha));
            steamIconShockwaveMat.color = new Color(color.r, color.g, color.b, alpha);
        }

        if (steamIconShockwaveT >= 0.6f)
        {
            UnityEngine.Object.Destroy(steamIconShockwave);
            steamIconShockwave = null;
            steamIconShockwaveMat = null;
        }
    }

    private static Texture2D GetShockwaveTexture()
    {
        if (shockwaveTexture != null)
            return shockwaveTexture;

        const int size = 128;
        shockwaveTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        shockwaveTexture.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Mathf.Sqrt(((x - center) * (x - center)) + ((y - center) * (y - center))) / center;
                float band = Mathf.Clamp01(1f - (Mathf.Abs(distance - 0.75f) / 0.25f));
                float alpha = band * band;

                shockwaveTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        shockwaveTexture.Apply();

        return shockwaveTexture;
    }

    private static Texture2D GetSteamParticleTexture()
    {
        if (steamParticleTexture != null)
            return steamParticleTexture;

        const int size = 64;
        steamParticleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        steamParticleTexture.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Mathf.Sqrt(((x - center) * (x - center)) + ((y - center) * (y - center))) / center;
                float alpha = Mathf.Clamp01(1f - distance);
                alpha *= alpha;

                steamParticleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        steamParticleTexture.Apply();

        return steamParticleTexture;
    }

    private static bool Alive(int assetId) =>
            assetId >= 0 &&
            GTConsole.ConsoleAssets.TryGetValue(assetId, out GTConsole.ConsoleAsset asset) &&
            asset.AssetObject != null;

    // Console clears every asset when the room is left (ClearConsoleAssets):
    // every switch goes with them, so they must read OFF again instead of
    // claiming assets that no longer exist.
    public static void ResetSwitches()
    {
        SetSwitchOff("TikTok Picture");
        SetSwitchOff("Thor Hammer");
        SetSwitchOff("Steam Icon");

        pictureAssetId = -1;
        hammerAssetId  = -1;

        steamIconId            = -1;
        steamIconOnHand        = false;
        steamIconMaterials     = null;
        steamIconLight         = null;
        steamIconParticles     = null;
        steamIconSparks        = null;
        steamIconTrail         = null;
        steamIconHasSmoothPos  = false;

        if (steamIconShockwave != null)
        {
            UnityEngine.Object.Destroy(steamIconShockwave);
            steamIconShockwave    = null;
            steamIconShockwaveMat = null;
        }
    }

    private static void SetSwitchOff(string buttonText)
    {
        ButtonInfo button = Buttons.GetIndex(buttonText);

        if (button != null)
            button.enabled = false;
    }

    private static bool CanSpawn(out string reason)
    {
        if (!PhotonNetwork.InRoom)
        {
            reason = "Join a room before spawning assets.";

            return false;
        }

        // Every receiver, this client included, drops an asset command from a
        // sender it does not hold as an administrator.
        if (!HamburburData.Admins.ContainsKey(PhotonNetwork.LocalPlayer.UserId))
        {
            reason =
                    "<color=grey>[</color><color=red>ADMIN</color><color=grey>]</color> " +
                    "Only Console admins can spawn assets.";

            return false;
        }

        reason = null;

        return true;
    }
}
