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

// The small Console Assets page: a picture, the Thor hammer, the Travis
// statue, the Jurassic Park gate and the Steam Icon.
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
// Press Travis and the travisscott statue from Console's ServerData appears
// three metres in front of you, standing on the floor and turned to face you -
// networked, with the placement re-sent twelve seconds later for anyone still
// downloading the 5.46 MB bundle.
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
    // Travis: the travisscott figure from Console's ServerData (the bundle
    // is 5.46 MB, hosted by Seralyth and by OMMALLEYGT's fork, so the
    // download lands on the first or second try - no copy in this repo).
    // Spawned as a statue in front of you: colliders stripped, networked
    // like every other asset here, position re-sent for slow downloaders.
    // ---------------------------------------------------------------------

    private const string TravisBundle = "travis";
    private const string TravisAsset  = "travisscott";

    // The prefab is built at statue size; this is the scale the other
    // menus spawn it at, which lands it around life size.
    private static readonly Vector3 TravisScale = Vector3.one * 0.38f;

    private static int travisAssetId = -1;

    public static string TravisDisplay()
    {
        ButtonInfo button = Buttons.GetIndex("Travis");

        return
                $"Travis: {(button != null && button.enabled
                                     ? "<color=green>ON</color>"
                                     : "<color=red>OFF</color>")}";
    }

    public static void SpawnTravis()
    {
        if (!CanSpawn(out string reason))
        {
            // Saved switch states replay at startup before a room exists;
            // a real press gets the refusal reason.
            SetSwitchOff("Travis");

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
            SetSwitchOff("Travis");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification("Could not find your head - try again inside a room.");

            return;
        }

        Vector3 forward = head.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();

        // Three metres ahead of you, turned to face you...
        Vector3 position = head.position + forward * SpawnDistance;

        // ...and standing on whatever is under that spot: the figure has no
        // fixed spot on the map, so a downward ray picks the floor instead
        // of guessing a height and leaving it floating or buried.
        if (Physics.Raycast(head.position + (forward * SpawnDistance), Vector3.down, out RaycastHit hit, 12f))
            position.y = hit.point.y;
        else
            position.y = head.position.y - 1f;

        Vector3 toPlayer = head.position - position;
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude < 0.0001f)
            toPlayer = -forward;

        toPlayer.Normalize();

        Quaternion rotation = Quaternion.LookRotation(toPlayer);

        travisAssetId = GTConsole.GetFreeAssetID();

        // Spawn first; every command after it waits for the asset to exist
        // (Console gives each one up to ten seconds).
        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, TravisBundle, TravisAsset, travisAssetId);
        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, travisAssetId);
        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, travisAssetId, position);
        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, travisAssetId, rotation);
        GTConsole.ExecuteCommand("asset-setscale", ReceiverGroup.All, travisAssetId, TravisScale);

        NotifiLib.SendNotification("Travis is standing in front of you - the whole room sees it.");

        CoroutineManager.Instance.StartCoroutine(ResendTravis());
    }

    public static void RemoveTravis()
    {
        if (Alive(travisAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, travisAssetId);

        travisAssetId = -1;
    }

    // 5.46 MB is the slowest bundle here: anyone still pulling it when the
    // spawn went out would drop the placement commands (Console waits ten
    // seconds per asset), so re-send them at twelve seconds.
    private static IEnumerator ResendTravis()
    {
        int id = travisAssetId;

        yield return new WaitForSeconds(12f);

        if (id < 0 || travisAssetId != id || !Alive(id))
            yield break;

        Transform statue = GTConsole.ConsoleAssets[id].AssetObject.transform;

        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, id);
        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, id, statue.position);
        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, id, statue.rotation);
        GTConsole.ExecuteCommand("asset-setscale", ReceiverGroup.All, id, TravisScale);
    }

    // ---------------------------------------------------------------------
    // Jurassic Park gate: the prefab from this menu's asset bundle template
    // project (mesh, materials and a looping dinosaur roar baked in), from
    // this repo's bundles/jurassicparkgate. The source model is in
    // centimetres, so instead of guessing a scale the owner measures its own
    // copy once it has spawned, fits it to a six metre width, stands it on
    // the floor - and broadcasts that final transform. Everyone else applies
    // the same absolute numbers, so no per-frame traffic is needed.
    // ---------------------------------------------------------------------

    private const string GateBundle = "jurassicparkgate";
    private const string GateAsset  = "JurassicParkGate Variant";

    // How wide the gate stands, and how far in front of you it goes up.
    // Nine metres matches the model's own proportions (9.27 x 9.74 at the
    // prefab's authored scale), so the fit barely nudges it - it exists to
    // guarantee the same size everywhere, not to restyle it.
    private const float GateWidth    = 9f;
    private const float GateDistance = 5f;

    private static int gateAssetId = -1;

    // The fitted transform, kept for the twelve second resend.
    private static Vector3    gatePosition;
    private static Quaternion gateRotation = Quaternion.identity;
    private static Vector3    gateScale    = Vector3.one;

    public static string GateDisplay()
    {
        ButtonInfo button = Buttons.GetIndex("Jurassic Gate");

        return
                $"Jurassic Gate: {(button != null && button.enabled
                                            ? "<color=green>ON</color>"
                                            : "<color=red>OFF</color>")}";
    }

    public static void SpawnGate()
    {
        if (!CanSpawn(out string reason))
        {
            // Saved switch states replay at startup before a room exists;
            // a real press gets the refusal reason.
            SetSwitchOff("Jurassic Gate");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification(reason);

            return;
        }

        if (gateAssetId >= 0)
            return;

        VRRig     localRig = VRRig.LocalRig;
        Transform head     = localRig != null && localRig.head != null && localRig.head.rigTarget != null
                                     ? localRig.head.rigTarget.transform
                                     : null;

        if (head == null)
        {
            SetSwitchOff("Jurassic Gate");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification("Could not find your head - try again inside a room.");

            return;
        }

        Vector3 forward = head.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();

        // Five metres ahead (the gate is wide), turned to face you, with
        // the floor under that spot as its base.
        Vector3 spawnPoint = head.position + (forward * GateDistance);

        if (Physics.Raycast(head.position + (forward * GateDistance), Vector3.down, out RaycastHit hit, 15f))
            spawnPoint.y = hit.point.y;
        else
            spawnPoint.y = head.position.y - 1f;

        Vector3 toPlayer = head.position - spawnPoint;
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude < 0.0001f)
            toPlayer = -forward;

        toPlayer.Normalize();

        Quaternion rotation = Quaternion.LookRotation(toPlayer);

        gateAssetId = GTConsole.GetFreeAssetID();

        // Spawn first; the placement below waits for the asset to exist
        // (Console gives every later command up to ten seconds).
        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, GateBundle, GateAsset, gateAssetId);
        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, gateAssetId);

        CoroutineManager.Instance.StartCoroutine(PlaceGate(gateAssetId, spawnPoint, rotation));
        CoroutineManager.Instance.StartCoroutine(ResendGate());
    }

    public static void RemoveGate()
    {
        if (Alive(gateAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, gateAssetId);

        gateAssetId = -1;
    }

    // Measure, fit and ground the local copy, then broadcast the result.
    // Ten second budget so it always finishes before the resend fires.
    private static IEnumerator PlaceGate(int id, Vector3 spawnPoint, Quaternion rotation)
    {
        float timeout = Time.time + 10f;

        while (Time.time < timeout && !Alive(id))
            yield return null;

        // Switched off while still loading: nothing to report.
        if (gateAssetId != id)
            yield break;

        if (!Alive(id))
        {
            // This used to be a silent no-op: no bundle, no asset, no gate,
            // and the button simply appeared broken. Say what happened.
            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification(
                        "<color=red>Jurassic Gate</color> did not load - bundle download or asset problem.");

            yield break;
        }

        Transform gate = GTConsole.ConsoleAssets[id].AssetObject.transform;

        // Fit by measured width: the model carries no scale the menu could
        // read (centimetre source units), and guessing would either bury the
        // gate in the floor or shrink it to a keychain.
        Bounds local = LocalGateBounds(gate);

        if (local.size.x > 0.01f)
            gate.localScale = gate.localScale * (GateWidth / local.size.x);

        gate.rotation = rotation;

        // Ground and centre it: slide until the base sits on the floor spot
        // and the opening is centred over it.
        Bounds world = WorldGateBounds(gate);

        Vector3 delta = spawnPoint - world.center;
        delta.y       = spawnPoint.y - world.min.y;

        gate.position += delta;

        gatePosition = gate.position;
        gateRotation = gate.rotation;
        gateScale    = gate.localScale;

        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, id);
        GTConsole.ExecuteCommand("asset-setposition",       ReceiverGroup.All, id, gatePosition);
        GTConsole.ExecuteCommand("asset-setrotation",       ReceiverGroup.All, id, gateRotation);
        GTConsole.ExecuteCommand("asset-setscale",          ReceiverGroup.All, id, gateScale);

        if (!Preferences.RestoringButtonStates)
            NotifiLib.SendNotification("The gate is standing in front of you - the whole room sees it.");
    }

    // The gate's tight box in its own root space: how wide, how tall, how
    // deep - independent of where the prefab happened to be sitting.
    private static Bounds LocalGateBounds(Transform gate)
    {
        bool   has   = false;
        Bounds total = new Bounds();

        foreach (MeshFilter filter in gate.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;

            Bounds   mesh    = filter.sharedMesh.bounds;
            Matrix4x4 toRoot = gate.worldToLocalMatrix * filter.transform.localToWorldMatrix;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = mesh.center + Vector3.Scale(mesh.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));

                point = toRoot.MultiplyPoint3x4(point);

                if (!has)
                {
                    total = new Bounds(point, Vector3.zero);
                    has   = true;
                }
                else
                    total.Encapsulate(point);
            }
        }

        return total;
    }

    private static Bounds WorldGateBounds(Transform gate)
    {
        bool   has   = false;
        Bounds total = new Bounds();

        foreach (Renderer renderer in gate.GetComponentsInChildren<Renderer>(true))
        {
            if (!has)
            {
                total = renderer.bounds;
                has   = true;
            }
            else
                total.Encapsulate(renderer.bounds);
        }

        return total;
    }

    // The bundle is 5 MB: anyone still pulling it when the placement went
    // out would drop the transform commands (Console waits ten seconds per
    // asset), so re-send them at twelve seconds.
    private static IEnumerator ResendGate()
    {
        int id = gateAssetId;

        yield return new WaitForSeconds(12f);

        if (id < 0 || gateAssetId != id || !Alive(id))
            yield break;

        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, id);
        GTConsole.ExecuteCommand("asset-setposition",       ReceiverGroup.All, id, gatePosition);
        GTConsole.ExecuteCommand("asset-setrotation",       ReceiverGroup.All, id, gateRotation);
        GTConsole.ExecuteCommand("asset-setscale",          ReceiverGroup.All, id, gateScale);
    }

    // ---------------------------------------------------------------------
    // Steam Icon: the glowing Steam logo, ported from OMMALLEYGT's other
    // menu (CXS). The asset comes from this repo's bundles/steamicon (a
    // prefab named SteamIcon with its sound baked in) - ServerData does not
    // host it, so Console's download falls back to RiftixAssetsURL.
    // ---------------------------------------------------------------------

    private static int steamIconId = -1;
    private static bool steamIconOnHand;
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

    // Which theme and which animation clock the icon on screen is drawing
    // with: the owner's own knobs, or the networked state for a room copy.
    private static int steamIconActiveColorTheme;
    private static float steamIconElapsed;
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

    // PhotonNetwork.Time is the room's shared clock: every client agrees on
    // the bob, orbit and glow phase, which is what lets a remote re-run the
    // owner's animation locally instead of receiving it frame by frame.
    private static float SteamIconClock => (float)PhotonNetwork.Time;

    // Animation time measured from the networked start epoch. Subtracted in
    // double first: Photon's time is TickCount based (can sit in the
    // millions), where float subtraction would quantise the bob into steps.
    private static float SteamIconElapsed(float startTime) => (float)(PhotonNetwork.Time - startTime);

    // When the owner spawned the icon - the epoch every client animates
    // from, so entrances, bob and base spin stay locked together.
    private static float steamIconStartTime;

    // The knobs as they travel over the network, keyed by asset id. The
    // owner also keeps one entry for its own icon (the broadcast runs
    // through the local half of ExecuteCommand too), but the owner path
    // reads the plain statics, so that copy is only used when forwarding
    // the state to a player who joins later.
    private static readonly Dictionary<int, SteamIconState> steamIconStates = new();

    // The networked icon this client animates when it is not the spawner.
    private static int steamIconRemoteId = -1;
    private static bool steamIconRemoteAdopted;

    // Everything a non-owner needs to draw the icon exactly like the
    // spawner does: anchor and motion mode, colour, size, sparks, and the
    // shared start clock.
    private readonly struct SteamIconState(int anchorMode, int moveMode, int colorTheme, int sizeMode, bool sparks, float startTime)
    {
        public int AnchorMode { get; } = anchorMode;
        public int MoveMode   { get; } = moveMode;
        public int ColorTheme { get; } = colorTheme;
        public int SizeMode   { get; } = sizeMode;
        public bool Sparks    { get; } = sparks;
        public float StartTime { get; } = startTime;
    }

    private static SteamIconState OwnerSteamIconState() =>
            new(steamIconAnchorMode, steamIconMode, steamIconColorTheme, steamIconSizeMode, steamIconSparksOn,
                    steamIconStartTime);

    // Console routes "steamiconstate" here for every receiver. Storing it
    // keyed by asset id means late joiners get the same snapshot their
    // Console sync replays the asset with.
    public static void ApplySteamIconState(int assetId, int anchorMode, int moveMode, int colorTheme, int sizeMode,
            int sparks, float startTime)
    {
        steamIconStates[assetId] = new SteamIconState(anchorMode, moveMode, colorTheme, sizeMode, sparks != 0,
                startTime);

        // Not the spawner: this is the icon we should animate from now on.
        if (steamIconId < 0)
            steamIconRemoteId = assetId;
    }

    // Console replays a steamicon asset to a joining player - hand the
    // state snapshot along with it so their copy animates too.
    public static void ForwardSteamIconState(int assetId, int targetActor)
    {
        if (steamIconStates.TryGetValue(assetId, out SteamIconState state))
            SendSteamIconState(assetId, state, targetActor);
    }

    // Broadcast the owner's current knobs; called on spawn, on every knob
    // turn and on the periodic resend that heals missed packets.
    private static void SendSteamIconState()
    {
        if (steamIconId < 0) return;

        SendSteamIconState(steamIconId, OwnerSteamIconState(), -1);
    }

    private static void SendSteamIconState(int assetId, SteamIconState state, int targetActor)
    {
        int sparks = state.Sparks ? 1 : 0;

        if (targetActor >= 0)
            GTConsole.ExecuteCommand("steamiconstate", targetActor, assetId, state.AnchorMode, state.MoveMode,
                    state.ColorTheme, state.SizeMode, sparks, state.StartTime);
        else
            GTConsole.ExecuteCommand("steamiconstate", ReceiverGroup.All, assetId, state.AnchorMode, state.MoveMode,
                    state.ColorTheme, state.SizeMode, sparks, state.StartTime);
    }

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

        // If we were animating someone else's icon, let go of the local FX
        // hung on it first - from here on this client owns the animation.
        if (steamIconRemoteAdopted)
            ReleaseRemoteSteamIconVisuals();

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

        // The shared epoch: remotes animate from this too, so the entrance
        // and bob phases line up with the spawner's instead of restarting.
        steamIconStartTime = SteamIconClock;

        Transform anchor = SteamIconAnchor;
        Vector3 spawnPosition = anchor != null
            ? anchor.position + (Vector3.up * 0.1f)
            : tagger.bodyCollider.transform.position + (Vector3.up * 0.35f);

        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, "steamicon", "SteamIcon", steamIconId);
        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, steamIconId);
        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, steamIconId, spawnPosition);
        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, steamIconId, Quaternion.identity);
        ApplySteamIconMultiplayerAnchor();

        // The anchor commands move the transform; this snapshot carries the
        // knobs (colour, size, sparks, motion, start clock) so the room can
        // animate its copies the same way.
        SendSteamIconState();

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

        // The state snapshot rides along too: whoever missed it at spawn
        // (still downloading, packet drop) catches up here.
        SendSteamIconState();
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

    // The knobs behind the incremental buttons. Every turn re-broadcasts the
    // state snapshot (one small event), so the room's copy of the icon
    // changes anchor, motion, colour, size and sparks together with yours.
    public static void SteamIconAnchorNext(bool increment)
    {
        steamIconAnchorMode = Next(steamIconAnchorMode, 3, increment);

        if (steamIconId >= 0)
            ApplySteamIconMultiplayerAnchor();

        SendSteamIconState();
    }

    public static void SteamIconMoveNext(bool increment)
    {
        steamIconMode = Next(steamIconMode, 3, increment);
        SendSteamIconState();
    }

    public static void SteamIconColorNext(bool increment)
    {
        steamIconColorTheme = Next(steamIconColorTheme, 6, increment);
        SendSteamIconState();
    }

    public static void SteamIconSizeNext(bool increment)
    {
        steamIconSizeMode = Next(steamIconSizeMode, steamIconSizeScales.Length, increment);
        SendSteamIconState();
    }

    // Wraps either way round: left on the bump goes back a step.
    private static int Next(int value, int length, bool increment) =>
            (value + (increment ? 1 : -1) + length) % length;

    public static void SteamIconSparksOn()
    {
        steamIconSparksOn = true;
        SendSteamIconState();
    }

    public static void SteamIconSparksOff()
    {
        steamIconSparksOn = false;
        SendSteamIconState();
    }

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

    // Called every frame from Console.Update. The client that pressed the
    // button animates its own icon from the plain statics; every other
    // client re-runs the very same animation on the room's copy, driven by
    // the state snapshot that arrived over the network and bound to the
    // spawner's rig - so the whole room sees the bob, orbit, glow and
    // sparks without a single per-frame packet.
    public static void UpdateSteamIcon()
    {
        if (steamIconId >= 0)
            UpdateOwnedSteamIcon();
        else
            UpdateRemoteSteamIcon();
    }

    private static void UpdateOwnedSteamIcon()
    {
        if (!GTConsole.ConsoleAssets.TryGetValue(steamIconId, out GTConsole.ConsoleAsset asset) ||
            asset.AssetObject == null)
            return;

        GorillaTagger tagger = GorillaTagger.Instance;

        if (tagger == null || tagger.headCollider == null)
            return;

        Transform anchor = SteamIconAnchor;

        if (anchor == null)
            return;

        steamIconActiveColorTheme = steamIconColorTheme;

        if (!steamIconOnHand)
            AdoptSteamIcon(asset, anchor, true);

        // Fetched after adoption: the spawner starts the shared entrance
        // clock while adopting, and this copy has to carry that epoch.
        AnimateSteamIcon(asset, OwnerSteamIconState(), anchor, tagger.headCollider.transform);
    }

    private static void UpdateRemoteSteamIcon()
    {
        if (steamIconRemoteId < 0) return;

        if (!steamIconStates.TryGetValue(steamIconRemoteId, out SteamIconState state))
            return;

        if (!GTConsole.ConsoleAssets.TryGetValue(steamIconRemoteId, out GTConsole.ConsoleAsset asset) ||
            asset.AssetObject == null)
        {
            // The room's copy is gone (or still downloading): take down any
            // local FX we hung on it and wait instead of animating nothing.
            if (steamIconRemoteAdopted)
                ReleaseRemoteSteamIconVisuals();

            return;
        }

        // The asset is bound to the spawner's rig by the networked anchor
        // command - follow that rig, never your own.
        VRRig ownerRig = GTConsole.GetVRRigFromActor(asset.BindPlayerActor);

        if (ownerRig == null)
            return;

        Transform head = RemoteSteamIconHead(ownerRig);
        Transform anchor = RemoteSteamIconAnchor(ownerRig, state.AnchorMode, head);

        if (anchor == null || head == null)
            return;

        steamIconActiveColorTheme = state.ColorTheme;

        if (!steamIconOnHand)
            AdoptSteamIcon(asset, anchor, false);

        AnimateSteamIcon(asset, state, anchor, head);
    }

    // The moment the asset is on screen: pin it to the anchor, give it the
    // local glow materials and FX, and start the animation phase. The
    // spawner also restarts the shared entrance clock here and broadcasts
    // it, so the room plays the entrance from the same instant the icon
    // became visible to the person who spawned it.
    private static void AdoptSteamIcon(GTConsole.ConsoleAsset asset, Transform anchor, bool owner)
    {
        Transform icon = asset.AssetObject.transform;

        steamIconWorldSize = icon.lossyScale;
        icon.SetParent(anchor, false);

        FixSteamIconMaterial(asset.AssetObject);
        CreateSteamIconFX(asset.AssetObject);
        CreateSteamIconShockwave(icon.position);

        steamIconOnHand = true;
        steamIconHasSmoothPos = false;
        steamIconSpin = 0f;
        steamIconSpinBoost = 0f;
        steamIconFreePos = icon.position;

        if (owner)
        {
            steamIconStartTime = SteamIconClock;
            SendSteamIconState();
        }
        else
        {
            steamIconRemoteAdopted = true;
        }
    }

    // Where the spawner's icon hangs on the spawner's rig: hands, or the
    // head for the head anchor - the mirror of SteamIconAnchor above.
    private static Transform RemoteSteamIconAnchor(VRRig rig, int anchorMode, Transform head)
    {
        switch (anchorMode)
        {
            case 1:  return rig.rightHandTransform;
            case 2:  return head;
            default: return rig.leftHandTransform;
        }
    }

    private static Transform RemoteSteamIconHead(VRRig rig)
    {
        if (rig == null) return null;

        if (rig.headMesh != null)
            return rig.headMesh.transform;

        return rig.head != null && rig.head.rigTarget != null ? rig.head.rigTarget.transform : null;
    }

    // The shared animation itself - one body for the spawner and every
    // spectator alike. Phases run on time elapsed from the networked start
    // clock (computed in double, so float precision at Photon's TickCount
    // based time never steps the bob), which is what keeps two clients
    // holding the same state drawing the same pose.
    private static void AnimateSteamIcon(GTConsole.ConsoleAsset asset, SteamIconState state, Transform anchor,
            Transform head)
    {
        Transform icon = asset.AssetObject.transform;

        if (icon.parent != anchor)
            icon.SetParent(anchor, false);

        float elapsed = SteamIconElapsed(state.StartTime);
        steamIconElapsed = elapsed;

        float t = Mathf.Clamp01(elapsed / 0.6f);
        float elastic = ElasticOut(t);
        float breathe = t >= 1f ? 1f + (0.04f * Mathf.Sin(elapsed * 2.5f)) : 1f;

        float bob = Mathf.Sin(elapsed * 2f);
        bool headMode = state.AnchorMode == 2;
        Vector3 headForward = head.forward;

        float upOffset = headMode ? 0.35f + (bob * 0.02f) : 0.28f + (bob * 0.03f);
        float forwardOffset = headMode ? steamIconFollowDist * 4f : steamIconFollowDist;

        Vector3 targetPosition;

        switch (state.MoveMode)
        {
            case 1:
                float orbitAngle = elapsed * 1.6f;
                Vector3 orbitCenter = head.position;
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
        steamIconSpin += steamIconSpinBoost * Time.deltaTime;

        // The base spin runs off the shared clock (wrapped so hours of play
        // never eat float precision); the follow boost is a local flourish
        // layered on top of it.
        float spin = ((elapsed * steamIconSpinSpeed) % 360f) + steamIconSpin;

        Quaternion baseRotation = Quaternion.Euler(bob * 8f, spin, Mathf.Sin(elapsed * 1.6f) * 6f);

        if (followSpeed > 0.4f)
        {
            Vector3 leanAxis = Vector3.Cross(Vector3.up, followVelocity / followSpeed);
            if (leanAxis.sqrMagnitude > 0.001f)
                baseRotation = Quaternion.AngleAxis(Mathf.Clamp(followSpeed * 10f, 0f, 45f), leanAxis.normalized) * baseRotation;
        }

        icon.rotation = baseRotation;

        Vector3 parentScale = anchor.lossyScale;
        Vector3 targetWorld = steamIconWorldSize * (elastic * breathe * steamIconSizeScales[state.SizeMode]);
        icon.localScale = new Vector3(
            targetWorld.x / SafeScale(parentScale.x),
            targetWorld.y / SafeScale(parentScale.y),
            targetWorld.z / SafeScale(parentScale.z));

        Color themeColor = CurrentSteamIconColor();
        float flare = 1f + Mathf.Clamp(followSpeed * 0.25f, 0f, 1.5f);
        float pulse = (0.75f + (0.45f * Mathf.Sin(elapsed * 3f))) * t * flare * steamIconGlowMult;
        SetSteamIconEmission(pulse, steamIconMaterials, themeColor);

        if (steamIconLight != null)
        {
            steamIconLight.color = themeColor;
            steamIconLight.intensity = ((1.2f + (0.8f * Mathf.Sin(elapsed * 3f))) * t) * flare * steamIconGlowMult;
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
            sparkEmission.rateOverTime = state.Sparks ? (8f + (followSpeed * 40f)) * t * steamIconParticleMult : 0f;
        }

        if (steamIconTrail != null)
            steamIconTrail.emitting = state.Sparks && t >= 1f;

        UpdateSteamIconShockwave();
    }

    // Let go of the local FX hung on a room-owned icon without touching the
    // asset itself - it belongs to whoever spawned it.
    private static void ReleaseRemoteSteamIconVisuals()
    {
        if (steamIconShockwave != null)
        {
            UnityEngine.Object.Destroy(steamIconShockwave);
            steamIconShockwave    = null;
            steamIconShockwaveMat = null;
        }

        if (steamIconLight != null)
            UnityEngine.Object.Destroy(steamIconLight.gameObject);

        if (steamIconParticles != null)
            UnityEngine.Object.Destroy(steamIconParticles.gameObject);

        if (steamIconSparks != null)
            UnityEngine.Object.Destroy(steamIconSparks.gameObject);

        if (steamIconTrail != null)
            UnityEngine.Object.Destroy(steamIconTrail);

        steamIconMaterials     = null;
        steamIconLight         = null;
        steamIconParticles     = null;
        steamIconSparks        = null;
        steamIconTrail         = null;
        steamIconOnHand        = false;
        steamIconHasSmoothPos  = false;
        steamIconRemoteAdopted = false;
    }

    private static Color CurrentSteamIconColor()
    {
        switch (steamIconActiveColorTheme)
        {
            case 1: return Color.HSVToRGB(Mathf.Repeat(steamIconElapsed * 0.15f, 1f), 0.65f, 1.2f);
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
        SetSwitchOff("Travis");
        SetSwitchOff("Jurassic Gate");
        SetSwitchOff("Steam Icon");

        pictureAssetId = -1;
        hammerAssetId  = -1;
        travisAssetId  = -1;
        gateAssetId    = -1;

        steamIconId            = -1;
        steamIconOnHand        = false;
        steamIconMaterials     = null;
        steamIconLight         = null;
        steamIconParticles     = null;
        steamIconSparks        = null;
        steamIconTrail         = null;
        steamIconHasSmoothPos  = false;

        // Networked animation state: the room's snapshots, the icon this
        // client was drawing on behalf of a remote spawner, and the shared
        // epoch - all meaningless once the room's assets are gone.
        steamIconStates.Clear();
        steamIconRemoteId       = -1;
        steamIconRemoteAdopted  = false;
        steamIconStartTime      = 0f;
        steamIconElapsed        = 0f;

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
