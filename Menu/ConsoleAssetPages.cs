using System.Collections;
using System.Collections.Generic;
using System.IO;
using GorillaLocomotion;
using GorillaTag.Gravity;
using Photon.Pun;
using Photon.Realtime;
using StupidTemplate.Classes;
using StupidTemplate.Notifications;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR;
using GTConsole = StupidTemplate.Classes.Console.Console;
using HamburburData = StupidTemplate.Classes.Console.HamburburData;
using ConsoleUtils = StupidTemplate.Classes.Console.ConsoleUtils;

namespace StupidTemplate.Menu;

// The small Console Assets page: a picture, the Thor hammer, the Travis
// statue, the Steam Icon and the Jurassic logo.
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

    // The cowboy set: Cowboy Hat (head anchor) and Schofield (right hand),
    // wrapped roots built by Assets/Build Cowboy Set Bundle in the bundle
    // project and committed to this repo under bundles/cowboy_set - like
    // the hammer, ServerData has no room for it, so Console falls back to
    // RiftixAssetsURL when the download there misses.
    private const string CowboyBundle   = "cowboy_set";
    private const string CowboyHatAsset = "CowboyHatRiftix";
    private const string CowboyGunAsset = "SchofieldRiftix";

    // BindObject's anchor slots: 0 head, 1 left hand, 2 right hand, 3 body.
    private const int CowboyHatAnchor = 0;
    private const int CowboyGunAnchor = 2;

    // Half the wrapper's barrel length: the wrapper is built with the
    // Schofield's longest axis down +Z and its centre as the pivot, so the
    // muzzle is exactly this far along the local forward.
    private const float CowboyMuzzleForward = 0.15f;

    private const float CowboySpinSpeed    = 420f; // degrees per second
    private const float CowboyBulletSpeed  = 55f;  // metres per second

    private static int cowboyHatAssetId = -1;
    private static int cowboyGunAssetId  = -1;

    // The Schofield's local show: the flourish (angle kept across the
    // post-shot pause so it resumes cleanly from level), the trigger edge,
    // the bullet in flight, the muzzle flash burning down, and the kills
    // this client is currently staging.
    private static float      cowboySpinAngle;
    private static float      cowboySpinResumeAt = float.NegativeInfinity;
    private static bool       cowboyTriggerWasDown;
    private static GameObject cowboyBullet;
    private static Vector3    cowboyBulletVelocity;
    private static float      cowboyBulletLife;
    private static GameObject cowboyFlash;
    private static float      cowboyFlashUntil = float.NegativeInfinity;
    private static int        cowboyKillEpoch;
    private static readonly List<VRRig> cowboyKills = new();

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

        NotifiLib.SendNotification("Picture is up.");

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

    // ---------------------------------------------------------------------
    // Holy Picture: the meme picture that comes out of the ground. The
    // photo is this repo's media/holy.jpg - a 364 x 364 square crop of the
    // painting - and its thirteen second song media/holy.mp4, both served
    // straight off raw.githubusercontent like the TikTok picture: no
    // redirect hop and Range answered, which is what Unity's VideoPlayer
    // streams best. The press takes you to the forest beach's picnic
    // table - the one by the gazebo, the game's own asset name is the
    // typo "PincicTable" - stands you beside it and buries the picture
    // in the ground just beyond it; it rises out of the earth into the
    // air, ending four metres above your eyes, while a warm light
    // pulses off it and gold sparkles drift up - the light and sparkles
    // are local (like the Steam Icon's FX: there is no command to hang
    // a light on the room's copy), the picture, the rise keyframes and
    // the song are networked.
    // ---------------------------------------------------------------------

    private const string HolyImageUrl =
            "https://raw.githubusercontent.com/OMalley1112/RIFTIX-Menu/main/media/holy.jpg";

    private const string HolyVideoUrl =
            "https://raw.githubusercontent.com/OMalley1112/RIFTIX-Menu/main/media/holy.mp4";

    // The photo is a square, and huge: 6 m per side (it started as a
    // 0.54 x 1.20 m rectangle). The plane measures 15.24 x 8.52 units at
    // scale 1 - x drives the width, y the height (see PictureScale) - so
    // the two divisors are what keep the sides equal.
    private const float HolySize = 6f;

    private static readonly Vector3 HolyScale =
            new(HolySize / 15.24f, HolySize / 8.52f, HolySize / 15.24f);

    // The picture starts with its centre a half-height under the ground in
    // front of you (nothing of it shows) and finishes this far above your
    // eyes - so it bursts out of the earth and keeps floating up. The
    // ease-out keeps it quick off the mark and gentle at the top.
    private const float HolyFloatHeight = 4f;
    private const float HolyRiseTime    = 4f;

    // Where the everyday Forest copy of the picnic table sits, read out
    // of the game's own level0 scene - the fallback for the moment the
    // live object cannot be found (culled, renamed by an update).
    private static readonly Vector3 ForestTablePosition = new(-22.961f, 3.492f, -141.412f);

    private static int holyAssetId = -1;

    // Cancels a spawn still waiting on the Beach teleport (toggle off,
    // room reset, re-press) - the Big TV's tvSpawnEpoch for this button.
    private static int holySpawnEpoch;

    // Cancels a rise still in flight (toggle off, room reset, re-press).
    private static int holyRiseEpoch;

    // Which asset carries this client's local light and sparkles: the
    // spawner's own picture while the switch is on, otherwise the first
    // copy in the room whose texture url is ours - the fingerprint that
    // works for pictures spawned by other RIFTIX clients too.
    private static int holyFxAssetId = -1;

    // The FX stay off until the picture's centre has cleared the ground,
    // so the light does not shine through the terrain while it is still
    // buried. A remote adoption skips the wait (it sets no spawn).
    private static float holyFxMinY = float.NegativeInfinity;

    private static Light         holyFxLight;
    private static ParticleSystem holyFxSparkles;

    public static string HolyPictureDisplay()
    {
        ButtonInfo button = Buttons.GetIndex("Holy Picture");

        return
                $"Holy Picture: {(button != null && button.enabled
                                          ? "<color=green>ON</color>"
                                          : "<color=red>OFF</color>")}";
    }

    public static void SpawnHolyPicture()
    {
        if (!CanSpawn(out string reason))
        {
            // Saved switch states replay at startup before a room exists;
            // a real press gets the refusal reason.
            SetSwitchOff("Holy Picture");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification(reason);

            return;
        }

        CoroutineManager.Instance.StartCoroutine(HolyPictureSpawn(++holySpawnEpoch));
    }

    public static void RemoveHolyPicture()
    {
        // Cancels a spawn still waiting on the teleport, then a rise still
        // in flight.
        holySpawnEpoch++;
        holyRiseEpoch++;

        if (Alive(holyAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, holyAssetId);

        holyAssetId = -1;

        // From here a remote copy (if any) may get the FX straight away.
        holyFxMinY = float.NegativeInfinity;
    }

    // The show belongs at the forest beach picnic table, so the press
    // finds that table (the live object first, the level0 coordinates as
    // the fallback), stands you a few metres out from it on the side you
    // are watching from and buries the picture in the ground just beyond
    // the table, turned to face you. The spawn itself goes in BURIED: the
    // floor under that spot picks the ground, and the picture starts with
    // its centre half a panel below it so nothing of it shows until the
    // rise drags it out of the earth.
    private static IEnumerator HolyPictureSpawn(int epoch)
    {
        VRRig     localRig = VRRig.LocalRig;
        Transform head     = localRig != null && localRig.head != null && localRig.head.rigTarget != null
                                     ? localRig.head.rigTarget.transform
                                     : null;

        if (head == null)
        {
            SetSwitchOff("Holy Picture");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification("Could not find your head - try again inside a room.");

            yield break;
        }

        Transform table   = FindForestPicnicTable();
        Vector3  tablePos = table != null ? table.position : ForestTablePosition;

        // The side of the table the player is pressing from is the side
        // they watch from. Someone standing on the table itself gets the
        // table's own forward instead.
        Vector3 viewDirection = head.position - tablePos;
        viewDirection.y = 0f;

        if (viewDirection.sqrMagnitude < 0.01f)
        {
            viewDirection = table != null ? table.forward : Vector3.forward;
            viewDirection.y = 0f;
        }

        if (viewDirection.sqrMagnitude < 0.01f)
            viewDirection = Vector3.forward;

        viewDirection.Normalize();

        // Stand them a few metres out on that side, on real floor close
        // to the table's own level; the far side is tried before giving
        // up and letting the press happen wherever they already are.
        bool stood = TryFindStandSpot(tablePos, viewDirection, out Vector3 standSpot) ||
                     TryFindStandSpot(tablePos, -viewDirection, out standSpot);

        if (stood)
        {
            ConsoleUtils.TeleportPlayer(standSpot);

            // Let the teleport land before measuring again.
            yield return null;

            localRig = VRRig.LocalRig;
            head     = localRig != null && localRig.head != null && localRig.head.rigTarget != null
                               ? localRig.head.rigTarget.transform
                               : null;

            if (head == null)
            {
                SetSwitchOff("Holy Picture");

                if (!Preferences.RestoringButtonStates)
                    NotifiLib.SendNotification("Could not find your head - try again inside a room.");

                yield break;
            }

            // Toggled off (or re-pressed) while the teleport was in flight.
            if (epoch != holySpawnEpoch)
                yield break;
        }

        // The picture goes into the ground just beyond the table from
        // where the player ended up, so the table sits in front of the
        // panel and nothing of the two intersect during the rise.
        Vector3 sideDirection = head.position - tablePos;
        sideDirection.y = 0f;

        if (sideDirection.sqrMagnitude < 0.01f)
            sideDirection = viewDirection;

        sideDirection.Normalize();

        Vector3 position = tablePos - sideDirection * 1.2f;
        position.y = tablePos.y;

        // Ground under the picture: cast from just above the verified
        // standing floor (or the table's own level) - low enough to stay
        // under the gazebo roof, high enough to reach the earth.
        Vector3 groundRay = position;
        groundRay.y = (stood ? standSpot.y : tablePos.y) + 0.5f;

        float groundY = Physics.Raycast(groundRay, Vector3.down, out RaycastHit ground, 12f)
                                ? ground.point.y
                                : head.position.y - 1f;

        Vector3 toPlayer = head.position - position;
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude < 0.0001f)
            toPlayer = -sideDirection;

        toPlayer.Normalize();

        Quaternion rotation = Quaternion.LookRotation(toPlayer);

        Vector3 start = position;
        start.y = groundY - (HolySize * 0.5f) - 0.1f;

        Vector3 end = position;
        end.y = head.position.y + HolyFloatHeight;

        // The FX hold off until the picture's centre clears this height.
        holyFxMinY = groundY;

        holyAssetId = GTConsole.GetFreeAssetID();

        // Spawn first; every command after it waits for the asset to exist
        // (Console gives each one up to ten seconds). The room gets the
        // buried position, then the rise keyframes walk it into the air.
        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, ScreenBundle, ScreenAsset, holyAssetId);
        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, holyAssetId, start);
        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, holyAssetId, rotation);
        GTConsole.ExecuteCommand("asset-setscale", ReceiverGroup.All, holyAssetId, HolyScale);
        GTConsole.ExecuteCommand("asset-settexture", ReceiverGroup.All, holyAssetId, ScreenChild,
                HolyImageUrl);
        GTConsole.ExecuteCommand("asset-setvideo", ReceiverGroup.All, holyAssetId, ScreenChild,
                HolyVideoUrl);

        NotifiLib.SendNotification(
                "The holy picture is coming out of the ground at the forest beach table - the whole room sees it.");

        CoroutineManager.Instance.StartCoroutine(
                RiseHolyPicture(++holyRiseEpoch, holyAssetId, start, end));
        CoroutineManager.Instance.StartCoroutine(ResendHolyPicture());
    }

    // The forest beach's picnic table. The game ships a copy in several
    // scenes - the FTUE tutorial, ranked, the rewind snapshot - but only
    // the everyday one lives under LocalObjects_Prefab/Forest, so match
    // the full path and skip anything under a Rewind root; a single
    // unrecognised copy anywhere is good enough too.
    private static Transform FindForestPicnicTable()
    {
        const string suffix =
                "Environment Objects/LocalObjects_Prefab/Forest/Terrain/campgroundstructure/PincicTable";

        Transform only = null;
        int       others = 0;

        foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy ||
                !candidate.gameObject.scene.IsValid())
                continue;

            if (candidate.name != "PincicTable")
                continue;

            string path = GetTransformPath(candidate);

            if (path.EndsWith(suffix) && !path.Contains("Rewind"))
                return candidate;

            only = candidate;
            others++;
        }

        return others == 1 ? only : null;
    }

    private static string GetTransformPath(Transform transform)
    {
        string path = transform.name;

        for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            path = parent.name + "/" + path;

        return path;
    }

    // A floor point a few metres out from the table on the given side:
    // cast down from above any roof, and accept only ground close to the
    // table's own level so a cliff edge or the gazebo roof never becomes
    // the standing spot.
    private static bool TryFindStandSpot(Vector3 tablePosition, Vector3 direction, out Vector3 standSpot)
    {
        foreach (float distance in new[] { 3.5f, 4.5f, 2.5f })
        {
            Vector3 candidate = tablePosition + direction * distance;

            if (!Physics.Raycast(candidate + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 16f))
                continue;

            if (hit.point.y < tablePosition.y - 1.5f || hit.point.y > tablePosition.y + 1.5f)
                continue;

            standSpot = hit.point;
            return true;
        }

        standSpot = Vector3.zero;
        return false;
    }

    // Drags the picture out of the ground and up into the air over four
    // seconds, from its buried start to the resting place above your eyes.
    // The transform moves locally every frame; the room gets a position
    // keyframe every half second (Others - this client already has the
    // exact position) and one last one at the top. SyncConsoleAssets reads
    // the live transform, so a player who joins mid-rise inherits wherever
    // it is and keeps riding the remaining keyframes. While the bundle is
    // still downloading the clock waits: the rise starts the moment the
    // picture appears, not a fixed delay after the press.
    private static IEnumerator RiseHolyPicture(int epoch, int id, Vector3 start, Vector3 end)
    {
        float elapsed  = 0f;
        float nextSync = 0.5f;

        while (elapsed < HolyRiseTime)
        {
            yield return null;

            if (epoch != holyRiseEpoch || id != holyAssetId)
                yield break;

            if (!Alive(id))
                continue;

            elapsed += Time.deltaTime;

            float k = Mathf.Clamp01(elapsed / HolyRiseTime);
            k = 1f - (1f - k) * (1f - k);

            Vector3 position = Vector3.Lerp(start, end, k);

            GTConsole.ConsoleAssets[id].AssetObject.transform.position = position;

            if (elapsed >= nextSync)
            {
                nextSync += 0.5f;

                GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.Others, id, position);
            }
        }

        if (epoch != holyRiseEpoch || id != holyAssetId || !Alive(id))
            yield break;

        GTConsole.ConsoleAssets[id].AssetObject.transform.position = end;
        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.Others, id, end);
    }

    // Anyone still downloading the bundle when the spawn went out would
    // drop the follow-ups (Console waits ten seconds per asset), so
    // re-send everything once everyone has had twelve seconds to catch up.
    // SetVideoURL skips a clip that is already playing, so the song does
    // not restart on this pass; by twelve seconds the rise is long done,
    // so the position here is the picture's resting place.
    private static IEnumerator ResendHolyPicture()
    {
        int id = holyAssetId;

        yield return new WaitForSeconds(12f);

        if (id < 0 || holyAssetId != id || !Alive(id))
            yield break;

        Transform picture = GTConsole.ConsoleAssets[id].AssetObject.transform;

        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, id, picture.position);
        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, id, picture.rotation);
        GTConsole.ExecuteCommand("asset-setscale", ReceiverGroup.All, id, HolyScale);
        GTConsole.ExecuteCommand("asset-settexture", ReceiverGroup.All, id, ScreenChild,
                HolyImageUrl);
        GTConsole.ExecuteCommand("asset-setvideo", ReceiverGroup.All, id, ScreenChild,
                HolyVideoUrl);
    }

    // One line from Console's Update: adopts whichever copy of the picture
    // this client should light (its own while the switch is on, otherwise
    // the first room copy wearing our texture), hangs the FX on it and
    // keeps the warm light breathing.
    public static void HolyPictureTick()
    {
        if (GTConsole.ConsoleAssets.Count == 0)
        {
            if (holyFxAssetId >= 0)
            {
                DropHolyFX();
                holyFxAssetId = -1;
            }

            return;
        }

        int wanted = holyAssetId >= 0 ? holyAssetId : HolyRemoteCandidate();

        if (wanted != holyFxAssetId)
        {
            DropHolyFX();
            holyFxAssetId = wanted;
        }

        if (holyFxAssetId < 0 || !Alive(holyFxAssetId))
            return;

        if (holyFxLight == null)
        {
            // Not while the picture is still under ground: a point light
            // ignores terrain, so it would glow through the ground before
            // anything of the picture shows.
            Vector3 picturePosition =
                    GTConsole.ConsoleAssets[holyFxAssetId].AssetObject.transform.position;

            if (picturePosition.y >= holyFxMinY)
                CreateHolyFX(GTConsole.ConsoleAssets[holyFxAssetId].AssetObject);
        }

        if (holyFxLight != null)
            holyFxLight.intensity = 1.35f + Mathf.Sin(Time.time * 2.4f) * 0.45f;
    }

    private static int HolyRemoteCandidate()
    {
        foreach (KeyValuePair<int, GTConsole.ConsoleAsset> entry in GTConsole.ConsoleAssets)
            if (entry.Value.AssetObject != null && entry.Value.TextureUrl == HolyImageUrl)
                return entry.Key;

        return -1;
    }

    private static void DropHolyFX()
    {
        // Unity's null check reads a destroyed component as null, so an FX
        // whose picture already left the room is skipped instead of
        // throwing on .gameObject.
        if (holyFxLight != null)
            UnityEngine.Object.Destroy(holyFxLight.gameObject);

        if (holyFxSparkles != null)
            UnityEngine.Object.Destroy(holyFxSparkles.gameObject);

        holyFxLight    = null;
        holyFxSparkles = null;
    }

    // The heavenly glow: a warm point light breathing at the photo's
    // centre (it reaches both faces - lights only occlude when they cast
    // shadows) and gold sparkles drifting up its surface. The picture's
    // own scale is lopsided (see HolyScale), so the FX hang off a child
    // that counter-scales back to world size 1 - particle sizes and the
    // spark box then mean what they say.
    private static void CreateHolyFX(GameObject picture)
    {
        GameObject fxRoot = new("RIFTIX_HolyFX");
        fxRoot.transform.SetParent(picture.transform, false);
        fxRoot.transform.localScale = new Vector3(1f / HolyScale.x, 1f / HolyScale.y, 1f / HolyScale.z);

        GameObject lightObject = new("RIFTIX_HolyLight");
        lightObject.transform.SetParent(fxRoot.transform, false);

        Light light = lightObject.AddComponent<Light>();
        light.type      = LightType.Point;
        light.color     = new Color(1f, 0.93f, 0.72f);
        light.range     = 16f;
        light.intensity = 1.35f;
        light.shadows   = LightShadows.None;

        holyFxLight = light;

        GameObject sparkleObject = new("RIFTIX_HolySparkles");
        sparkleObject.transform.SetParent(fxRoot.transform, false);

        ParticleSystem sparks = sparkleObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = sparks.main;
        main.loop            = true;
        main.playOnAwake     = true;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
        main.startColor      = new Color(1f, 0.92f, 0.65f, 0.85f);
        main.maxParticles    = 220;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.01f;
        main.scalingMode     = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule emission = sparks.emission;
        emission.rateOverTime = 40f;

        // A thin box the size of the panel, so the sparkles crawl up it.
        ParticleSystem.ShapeModule shape = sparks.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale     = new Vector3(HolySize, HolySize, 0.12f);

        ParticleSystem.VelocityOverLifetimeModule velocity = sparks.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space   = ParticleSystemSimulationSpace.World;
        velocity.x       = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
        velocity.y       = new ParticleSystem.MinMaxCurve(0.18f, 0.4f);
        velocity.z       = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);

        ParticleSystem.ColorOverLifetimeModule colorOverLife = sparks.colorOverLifetime;
        colorOverLife.enabled = true;

        Gradient gradient = new();
        gradient.SetKeys(
                new[]
                {
                        new GradientColorKey(Color.white, 0f),
                        new GradientColorKey(new Color(1f, 0.9f, 0.6f), 1f),
                },
                new[]
                {
                        new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.45f, 0.5f),
                        new GradientAlphaKey(0f, 1f),
                });
        colorOverLife.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer sparkRenderer = sparkleObject.GetComponent<ParticleSystemRenderer>();

        Shader sparkShader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Particles/Lit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                ?? Shader.Find("Sprites/Default");

        if (sparkShader != null)
        {
            Material sparkMaterial = new(sparkShader);
            sparkMaterial.mainTexture = GetSteamParticleTexture();
            sparkRenderer.material    = sparkMaterial;
        }

        sparkRenderer.renderMode = ParticleSystemRenderMode.Billboard;

        holyFxSparkles = sparks;
    }

    // The player who last put the night up: ourselves when the switch went
    // on, the sender of a nighton command on every other client - so the
    // day can go back when they leave the room instead of the map sitting
    // in the dark with them gone. -1 means nobody has.
    private static int dinoAtmosphereOwner = -1;

    // The night slot in the game's own time list. BetterDayNightManager
    // keeps the lightmap names in a public field, so the index is read
    // out of the live component instead of being guessed - with the
    // dumped sunrise / 10am / noon / 3pm / sunset / night order as the
    // fallback for the day that field comes back empty.
    public static int NightTimeIndex()
    {
        string[] names = BetterDayNightManager.instance != null
                                 ? BetterDayNightManager.instance.dayNightLightmapNames
                                 : null;

        if (names != null)
        {
            for (int i = 0; i < names.Length; i++)
                if (!string.IsNullOrEmpty(names[i]) &&
                    names[i].Trim().ToLowerInvariant() == "night")
                    return i;
        }

        return 5;
    }

    // Called from Console's nighton case on every client that runs it -
    // this is what remembers whose night it is.
    public static void NoteAtmosphere(int actorNumber) => dinoAtmosphereOwner = actorNumber;

    public static void ClearAtmosphereNote() => dinoAtmosphereOwner = -1;

    // The night went out as a raise event, which a player joining later
    // never receives - hand the room's atmosphere over with the rest of
    // the asset sync, or a joiner walks into a storm nobody told them
    // about.
    public static void ForwardAtmosphere(int actorNumber)
    {
        if (dinoAtmosphereOwner >= 0)
            GTConsole.ExecuteCommand("nighton", actorNumber);
    }

    // The player who set the night has left the room. There is nobody
    // left to send the clear to, so the day goes back up here, locally -
    // otherwise the map sits in the dark with them gone.
    public static void AtmosphereOwnerLeft(int actorNumber)
    {
        if (dinoAtmosphereOwner != actorNumber)
            return;

        dinoAtmosphereOwner = -1;

        if (BetterDayNightManager.instance != null)
        {
            BetterDayNightManager.instance.ClearTimeOfDay(true);
            BetterDayNightManager.instance.ClearFixedWeather(true);
        }
    }

    // ---------------------------------------------------------------------
    // Countdown: the live time event - a chessboard the size of a house
    // hanging in the sky twelve metres out and seven up, ticking the day
    // name and the hours, minutes and seconds until tomorrow midnight.
    // The board is networked like everything else on this page: it
    // spawns off this repo's media/countboard.png (dark checkerboard with
    // the teal frame baked in) on the bare console screen, so the whole
    // room sees it and Console's asset sync hands it to anyone who joins
    // later. Every RIFTIX client then hangs its own local half on the
    // room's copy - three world TextMeshPros in the game's nametag font
    // counting down, a breathing teal light and cyan sparkles - adopted
    // by texture-url fingerprint exactly like the Holy Picture's glow, so
    // a joiner gets the whole show the moment they arrive. The spawner
    // owns the motion: a rise with an overshoot onto the perch, then a
    // slow float, every position keyframed to the room twice a second
    // (everyone else just rides the keyframes); at midnight the board
    // bursts up again for the new day, every client spikes its glow -
    // and the finale below fires the show the countdown was counting
    // down to. Adele's Skyfall - the sound of the edit this came from - rides the
    // networked audio carrier: a screen scaled to nothing at your head,
    // audioOnly, so the whole room hears the song.
    // ---------------------------------------------------------------------

    // The board itself off raw.githubusercontent: 1024 x 576, eight by
    // eight dark squares with the teal frame stamped into the pixels.
    private const string CountdownBoardUrl =
            "https://raw.githubusercontent.com/OMalley1112/RIFTIX-Menu/main/media/countboard.png";

    // The song off raw.githubusercontent, muxed from the official lyric
    // video's audio into a black-frame mp4 - the VideoPlayer format this
    // menu has always streamed.
    private const string CountdownVideoUrl =
            "https://raw.githubusercontent.com/OMalley1112/RIFTIX-Menu/main/media/skyfall.mp4";

    // The sky perch: twelve metres out, seven up - thirty degrees above
    // the eye line, where it reads from anywhere in the forest.
    private const float CountdownDistance = 12f;
    private const float CountdownLift     = 7f;

    // The plane measures 15.24 x 8.52 units at scale 1 and the board
    // image is 16:9, so these give roughly 11 x 6.19 m - the checkerboard
    // stretched across the sky.
    private static readonly Vector3 CountdownScale =
            new(11f / 15.24f, 6.19f / 8.52f, 11f / 15.24f);

    // The carrier scaled to nothing: all song, no panel.
    private static readonly Vector3 CountdownAudioScale = new(0.0005f, 0.00088f, 0.0005f);

    private static int countdownAssetId = -1; // the board (networked)
    private static int countdownSongId  = -1; // the song carrier (networked)

    // Whose board this client lights: its own while the switch is on,
    // otherwise the first room copy wearing our texture - the fingerprint
    // that works for boards spawned by other RIFTIX clients too.
    private static int countdownFxAssetId = -1;

    // Cancels a spawn still in flight, the rise and float after it, and
    // the song resend (toggle off, room reset, re-press).
    private static int countdownEpoch;

    // The local half, hung off the room's board through a counter-scaled
    // root (the board sits at a fraction of world scale, so everything
    // inside keeps world-sized texts, light and sparkles).
    private static GameObject   countdownAttach;
    private static TextMeshPro  countdownTitle;
    private static TextMeshPro  countdownClock;
    private static TextMeshPro  countdownDay;
    private static Light        countdownLight;
    private static string       countdownLastClockLine;
    private static string       countdownLastDay;

    // The midnight flash: each client watches the date for itself (the
    // day turns at the same instant everywhere), so the glow bursts on
    // every screen without a command.
    private static System.DateTime countdownDayKey;
    private static float           countdownFlashUntil = float.NegativeInfinity;

    // The day the finale last fired on its own - stamped when the board
    // attaches, so a board raised at 23:59 waits for the real midnight
    // instead of firing on sight, and bumped the moment the date turns.
    private static System.DateTime countdownFinaleDay;

    // Where the board's perch moved to when the finale carried the show
    // into the new map; null means the sky perch it was spawned to. The
    // rise and float read it every pass, so the owner's room keyframes
    // follow the board across maps.
    private static Vector3? countdownRestOverride;

    public static string CountdownDisplay()
    {
        ButtonInfo button = Buttons.GetIndex("Countdown");

        if (button == null || !button.enabled)
            return "Countdown: <color=red>OFF</color>";

        return $"Countdown: <color=cyan>{CountdownClockText(CountdownRemaining())}</color>";
    }

    // Time left until the next local midnight, recomputed from the real
    // clock on every read - it cannot drift while the board is up, and it
    // is the same number for everyone (their own midnight).
    private static System.TimeSpan CountdownRemaining()
    {
        System.TimeSpan left = System.DateTime.Now.Date.AddDays(1) - System.DateTime.Now;

        return left < System.TimeSpan.Zero ? System.TimeSpan.Zero : left;
    }

    private static string CountdownClockText(System.TimeSpan left) =>
            $"{left.Hours:D2}:{left.Minutes:D2}:{left.Seconds:D2}";

    // Tomorrow's weekday, in English whatever the machine's locale says.
    private static string CountdownDayName() =>
            System.DateTime.Now.Date.AddDays(1)
                    .ToString("dddd", System.Globalization.CultureInfo.InvariantCulture)
                    .ToUpperInvariant();

    public static void SpawnCountdown()
    {
        if (!CanSpawn(out string reason))
        {
            // Saved switch states replay at startup before a room exists;
            // a real press gets the refusal reason.
            SetSwitchOff("Countdown");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification(reason);

            return;
        }

        if (CountdownRemoteCandidate() >= 0)
        {
            // The room already has one up (another admin's) - the show is
            // already on its way to everyone; join it, do not double it.
            SetSwitchOff("Countdown");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification("The countdown is already up in the room.");

            return;
        }

        countdownEpoch++;

        CoroutineManager.Instance.StartCoroutine(CountdownShow(countdownEpoch));
        CoroutineManager.Instance.StartCoroutine(CountdownSong(countdownEpoch));

        UnityEngine.Debug.Log("[RIFTIX] Countdown: button accepted - board and song on the way");
    }

    public static void RemoveCountdown()
    {
        countdownEpoch++;

        int board = countdownAssetId;
        countdownAssetId = -1;

        if (Alive(board))
        {
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, board);
            UnityEngine.Debug.Log("[RIFTIX] Countdown: off - board down for the room");
        }
        else if (board >= 0)
        {
            // The board was asked for but has not finished loading - wait
            // it out and take it down the moment it lands, or a bare
            // screen hangs in the sky forever.
            CoroutineManager.Instance.StartCoroutine(KillCountdownLate(board, "board"));
        }

        int song = countdownSongId;
        countdownSongId = -1;

        if (Alive(song))
        {
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, song);
            UnityEngine.Debug.Log("[RIFTIX] Countdown: off - song stopped");
        }
        else if (song >= 0)
        {
            CoroutineManager.Instance.StartCoroutine(KillCountdownLate(song, "song"));
        }

        DropCountdownFX();
        StopFinale();
    }

    // Waits for a cancelled spawn that is still loading and kills it as
    // soon as it exists - but never one that has since become a live
    // asset (the free-id scan can hand the same id to a fresh spawn).
    private static IEnumerator KillCountdownLate(int id, string what)
    {
        for (int i = 0; i < 12 && !Alive(id); i++)
            yield return new WaitForSeconds(1f);

        if (Alive(id) && id != countdownAssetId && id != countdownSongId)
        {
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, id);
            UnityEngine.Debug.Log($"[RIFTIX] Countdown: late-killed cancelled {what} {id}");
        }
    }

    // The spawner's half: the board goes up at the sky perch, rises the
    // last four metres with an overshoot, then floats - every transform
    // keyframed to the room twice a second (ReceiverGroup.Others: this
    // client already has the exact transform), so everyone rides the
    // same motion and a joiner inherits the live transform through
    // Console's asset sync. At midnight it bursts up again for the new
    // day.
    private static IEnumerator CountdownShow(int epoch)
    {
        VRRig     localRig = VRRig.LocalRig;
        Transform head     = localRig != null && localRig.head != null && localRig.head.rigTarget != null
                                     ? localRig.head.rigTarget.transform
                                     : null;

        if (head == null)
        {
            SetSwitchOff("Countdown");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification("Could not find your head - try again.");

            yield break;
        }

        Vector3 forward = head.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();

        Vector3 rest = head.position + forward * CountdownDistance + Vector3.up * CountdownLift;

        Vector3 toPlayer = head.position - rest;
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude < 0.0001f)
            toPlayer = -forward;

        toPlayer.Normalize();

        Quaternion rotation = Quaternion.LookRotation(toPlayer);

        int id = GTConsole.GetFreeAssetID();
        countdownAssetId = id;

        // Spawn first; every command after it waits for the asset to
        // exist (Console gives each one up to ten seconds), so the board
        // arrives in the sky with its texture already on it.
        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, ScreenBundle, ScreenAsset, id);
        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, id, rest);
        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, id, rotation);
        GTConsole.ExecuteCommand("asset-setscale", ReceiverGroup.All, id, CountdownScale);
        GTConsole.ExecuteCommand("asset-settexture", ReceiverGroup.All, id, ScreenChild,
                CountdownBoardUrl);

        UnityEngine.Debug.Log($"[RIFTIX] Countdown: board {id} sent to the sky");

        if (epoch != countdownEpoch)
        {
            // Turned off while the commands were still going out.
            if (Alive(id))
                GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, id);

            if (countdownAssetId == id)
                countdownAssetId = -1;

            yield break;
        }

        // Anyone still downloading the bundle when the spawn went out
        // would drop the follow-ups, so re-send everything once everyone
        // has had twelve seconds to catch up.
        CoroutineManager.Instance.StartCoroutine(ResendCountdownBoard(epoch, id));

        // The bundle can take seconds to land; the rise starts the moment
        // the board appears, not a fixed delay after the press.
        for (int i = 0; i < 600 && !Alive(id); i++)
        {
            if (epoch != countdownEpoch)
                yield break;

            yield return null;
        }

        if (epoch != countdownEpoch || !Alive(id))
            yield break;

        Transform boardTransform = GTConsole.ConsoleAssets[id].AssetObject.transform;

        const float riseTime = 1.6f;

        System.DateTime day = System.DateTime.Now.Date;

        while (epoch == countdownEpoch && countdownAssetId == id && Alive(id))
        {
            // A finale that carried the show into the new map points the
            // perch there; otherwise the sky perch it was spawned to.
            Vector3 perch = countdownRestOverride ?? rest;

            // One rise: from four metres below the perch, easing out and
            // overshooting the resting place before it settles
            // (ease-out-back) - the transform moves locally every frame
            // and the room gets a position keyframe every half second.
            Vector3 start = perch - Vector3.up * 4f;
            float   elapsed = 0f;
            float   syncAt  = Time.time + 0.5f;

            while (elapsed < riseTime)
            {
                yield return null;

                if (epoch != countdownEpoch || countdownAssetId != id || !Alive(id))
                    yield break;

                elapsed += Time.deltaTime;

                float k = Mathf.Clamp01(elapsed / riseTime);
                float e = k - 1f;
                float back = 1f + 2.70158f * e * e * e + 1.70158f * e * e;

                Vector3 pos = Vector3.Lerp(start, perch, back);
                boardTransform.position = pos;

                if (Time.time >= syncAt)
                {
                    syncAt += 0.5f;
                    GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.Others, id, pos);
                }
            }

            if (epoch != countdownEpoch || countdownAssetId != id || !Alive(id))
                yield break;

            boardTransform.position = perch;
            GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.Others, id, perch);

            UnityEngine.Debug.Log("[RIFTIX] Countdown: board settled in the sky");

            // The float until the day turns: a slow breath of height,
            // keyframed to the room the whole way - everyone rides it,
            // joiners included.
            while (epoch == countdownEpoch && countdownAssetId == id && Alive(id))
            {
                yield return null;

                if (System.DateTime.Now.Date != day)
                {
                    // A new day: break out to the rise above and the
                    // board bursts up again - the whole room sees it
                    // respawn for the countdown's next target.
                    day = System.DateTime.Now.Date;
                    break;
                }

                perch = countdownRestOverride ?? rest;
                Vector3 pos = perch + Vector3.up * (Mathf.Sin(Time.time * 0.55f) * 0.35f);
                boardTransform.position = pos;

                if (Time.time >= syncAt)
                {
                    syncAt += 0.5f;
                    GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.Others, id, pos);
                }
            }
        }
    }

    // Anyone still downloading the bundle when the spawn went out would
    // drop the follow-ups (Console waits ten seconds per asset), so
    // re-send everything once everyone has had twelve seconds to catch
    // up. By then the rise is long done, so the position here is the
    // board's live perch.
    private static IEnumerator ResendCountdownBoard(int epoch, int id)
    {
        yield return new WaitForSeconds(12f);

        if (epoch != countdownEpoch || countdownAssetId != id || !Alive(id))
            yield break;

        Transform board = GTConsole.ConsoleAssets[id].AssetObject.transform;

        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, id, board.position);
        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, id, board.rotation);
        GTConsole.ExecuteCommand("asset-setscale", ReceiverGroup.All, id, CountdownScale);
        GTConsole.ExecuteCommand("asset-settexture", ReceiverGroup.All, id, ScreenChild,
                CountdownBoardUrl);
    }

    // One line from Console's Update: adopts whichever copy of the board
    // this client should light (its own while the switch is on, otherwise
    // the first room copy wearing our texture), hangs the local half on
    // it and keeps the show running - the clock, the blinking colons, the
    // breathing glow and the midnight flash.
    public static void CountdownTick()
    {
        if (GTConsole.ConsoleAssets.Count == 0)
        {
            if (countdownFxAssetId >= 0)
            {
                DropCountdownFX();
                countdownFxAssetId = -1;
            }

            return;
        }

        int wanted = countdownAssetId >= 0 && Alive(countdownAssetId)
                ? countdownAssetId
                : CountdownRemoteCandidate();

        if (wanted != countdownFxAssetId)
        {
            DropCountdownFX();
            countdownFxAssetId = wanted;
        }

        if (countdownFxAssetId < 0 || !Alive(countdownFxAssetId))
            return;

        GameObject boardObject = GTConsole.ConsoleAssets[countdownFxAssetId].AssetObject;

        if (countdownAttach == null)
            CreateCountdownAttach(boardObject);

        if (countdownAttach == null)
            return;

        // The board sits at a fraction of world scale (CountdownScale):
        // counter it every frame so the texts, light and sparkles keep
        // world sizes - and so a scale command that lands after the asset
        // exists still gets picked up.
        Vector3 boardScale = boardObject.transform.localScale;

        if (boardScale.x > 0.0001f && boardScale.y > 0.0001f && boardScale.z > 0.0001f)
            countdownAttach.transform.localScale =
                    new Vector3(1f / boardScale.x, 1f / boardScale.y, 1f / boardScale.z);

        // The nametag font can arrive a frame or two late (the rig may
        // not be up when the board was built) - fill it in the moment it
        // exists, exactly like Visual's nametags do.
        if (countdownTitle != null && countdownTitle.font == null &&
            VRRig.LocalRig != null &&
            VRRig.LocalRig.playerText1 != null &&
            VRRig.LocalRig.playerText1.font != null)
        {
            TMP_FontAsset font = VRRig.LocalRig.playerText1.font;

            countdownTitle.font = font;
            countdownClock.font = font;
            countdownDay.font   = font;
        }

        float time  = Time.time;
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * 2.2f);

        // The day turns at the same instant on every machine, so the
        // midnight burst needs no command: the glow spikes here on its
        // own (the spawner's board respawns itself in CountdownShow).
        System.DateTime today = System.DateTime.Now.Date;

        if (today != countdownDayKey)
        {
            countdownDayKey     = today;
            countdownFlashUntil = time + 1.6f;
        }

        // Zero: the date turned, the clock ran out - the finale is the
        // show the countdown existed for. Every client fires its own
        // local copy the instant the day flips (the Finale button runs
        // the same chain early, on demand).
        if (today != countdownFinaleDay)
        {
            countdownFinaleDay = today;

            if (!finaleRunning)
                StartCountdownFinale("midnight");
        }

        if (countdownLight != null)
        {
            float flash = time < countdownFlashUntil ? (countdownFlashUntil - time) / 1.6f : 0f;
            countdownLight.intensity = 1.4f + pulse * 0.8f + flash * 7f;
        }

        if (countdownTitle != null)
            countdownTitle.color = Color.Lerp(new Color(0.25f, 0.95f, 0.9f), Color.white, pulse * 0.7f);

        // The clock: recomputed from the real date every frame (so it is
        // still exact at the midnight it counts down to), the colons
        // blinking by rich-text colour only - the string's width never
        // changes, so the auto-sized text never twitches.
        System.TimeSpan left = CountdownRemaining();

        string colon = time % 1f < 0.5f
                ? "<color=#3df3e0>:</color>"
                : "<color=#0b3b37>:</color>";

        string clock     = CountdownClockText(left);
        string clockLine = clock.Substring(0, 2) + colon +
                           clock.Substring(3, 2) + colon +
                           clock.Substring(6, 2);

        if (countdownClock != null && clockLine != countdownLastClockLine)
        {
            countdownClock.text    = clockLine;
            countdownClock.color   = Color.Lerp(new Color(0.85f, 1f, 0.98f), Color.white, pulse);
            countdownLastClockLine = clockLine;
        }

        string day = CountdownDayName();

        if (countdownDay != null && day != countdownLastDay)
        {
            countdownDay.text = day;
            countdownLastDay  = day;
        }
    }

    // The local half: a counter-scaled root on the room's board carrying
    // the three text lines, the glow and the sparkles - everything at
    // world size, whatever scale the console command put on the board.
    private static void CreateCountdownAttach(GameObject board)
    {
        GameObject root = new("RIFTIX_CountdownAttach");
        root.transform.SetParent(board.transform, false);

        countdownAttach       = root;
        countdownDayKey       = System.DateTime.Now.Date;
        countdownFinaleDay    = System.DateTime.Now.Date;
        countdownLastClockLine = null;
        countdownLastDay      = null;

        countdownTitle = MakeCountdownText(
                root.transform, "Title", new Vector3(0f, 1.95f, 0.05f),
                new Vector2(9.4f, 1.15f), "TOMORROW IN");

        countdownClock = MakeCountdownText(
                root.transform, "Clock", new Vector3(0f, 0f, 0.05f),
                new Vector2(10.2f, 2.7f), CountdownClockText(CountdownRemaining()));

        countdownDay = MakeCountdownText(
                root.transform, "Day", new Vector3(0f, -1.95f, 0.05f),
                new Vector2(9.4f, 1.15f), CountdownDayName());

        if (countdownTitle != null)
            countdownTitle.color = new Color(0.25f, 0.95f, 0.9f);

        if (countdownDay != null)
            countdownDay.color = new Color(0.45f, 0.85f, 0.85f);

        CreateCountdownFX(root.transform);

        UnityEngine.Debug.Log("[RIFTIX] Countdown: local show attached to the room's board");
    }

    // One line of the board: 3D text that reads like the nametags (their
    // font, their outline), auto-sized to its own slot so the clock is as
    // big as the board allows whatever the font's metrics say.
    private static TextMeshPro MakeCountdownText(
            Transform parent, string name, Vector3 localPosition, Vector2 rect, string text)
    {
        GameObject line = new(name);
        line.transform.SetParent(parent, false);
        line.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        line.transform.localPosition = localPosition;

        TextMeshPro mesh = line.AddComponent<TextMeshPro>();
        mesh.fontSize           = 1f;
        mesh.alignment          = TextAlignmentOptions.Center;
        mesh.rectTransform.sizeDelta = rect;
        mesh.enableAutoSizing   = true;
        mesh.fontSizeMin        = 0.06f;
        mesh.fontSizeMax        = 2.5f;
        mesh.textWrappingMode   = TextWrappingModes.NoWrap;
        mesh.outlineColor       = Color.black;
        mesh.outlineWidth       = 0.3f;
        mesh.text               = text;

        if (VRRig.LocalRig != null && VRRig.LocalRig.playerText1 != null)
            mesh.font = VRRig.LocalRig.playerText1.font;

        return mesh;
    }

    // The glow: a teal point light breathing at the board's centre (it
    // reaches the forest below - lights only occlude when they cast
    // shadows) and cyan sparkles drifting up off the checkerboard. Local
    // on every client, hung off the counter-scaled root, so the sizes
    // mean what they say.
    private static void CreateCountdownFX(Transform parent)
    {
        GameObject lightObject = new("CountdownLight");
        lightObject.transform.SetParent(parent, false);

        Light light = lightObject.AddComponent<Light>();
        light.type      = LightType.Point;
        light.color     = new Color(0.3f, 0.95f, 0.9f);
        light.range     = 26f;
        light.intensity = 1.6f;
        light.shadows   = LightShadows.None;

        countdownLight = light;

        GameObject sparkleObject = new("CountdownSparkles");
        sparkleObject.transform.SetParent(parent, false);

        ParticleSystem sparks = sparkleObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = sparks.main;
        main.loop            = true;
        main.playOnAwake     = true;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(2.2f, 4f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.06f, 0.24f);
        main.startColor      = new Color(0.6f, 1f, 0.97f, 0.9f);
        main.maxParticles    = 320;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.02f;
        main.scalingMode     = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule emission = sparks.emission;
        emission.rateOverTime = 70f;

        // A thin box the size of the board, so the sparkles crawl up it.
        ParticleSystem.ShapeModule shape = sparks.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale     = new Vector3(11f, 6.19f, 0.5f);

        ParticleSystem.VelocityOverLifetimeModule velocity = sparks.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space   = ParticleSystemSimulationSpace.World;
        velocity.x       = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
        velocity.y       = new ParticleSystem.MinMaxCurve(0.3f, 0.75f);
        velocity.z       = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);

        ParticleSystem.ColorOverLifetimeModule colorOverLife = sparks.colorOverLifetime;
        colorOverLife.enabled = true;

        Gradient gradient = new();
        gradient.SetKeys(
                new[]
                {
                        new GradientColorKey(new Color(0.75f, 1f, 0.98f), 0f),
                        new GradientColorKey(new Color(0.2f, 0.85f, 0.8f), 1f),
                },
                new[]
                {
                        new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.5f),
                        new GradientAlphaKey(0f, 1f),
                });
        colorOverLife.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer sparkRenderer = sparkleObject.GetComponent<ParticleSystemRenderer>();

        Shader sparkShader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Particles/Lit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                ?? Shader.Find("Sprites/Default");

        if (sparkShader != null)
        {
            Material sparkMaterial = new(sparkShader);
            sparkMaterial.mainTexture = GetSteamParticleTexture();
            sparkRenderer.material    = sparkMaterial;
        }

        sparkRenderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    private static void DropCountdownFX()
    {
        // Unity's null check reads a destroyed component as null, so an
        // attach whose board already left the room is skipped instead of
        // throwing on .gameObject.
        if (countdownAttach != null)
            Object.Destroy(countdownAttach);

        countdownAttach        = null;
        countdownTitle         = null;
        countdownClock         = null;
        countdownDay           = null;
        countdownLight         = null;
        countdownLastClockLine = null;
        countdownLastDay       = null;
    }

    private static int CountdownRemoteCandidate()
    {
        foreach (KeyValuePair<int, GTConsole.ConsoleAsset> entry in GTConsole.ConsoleAssets)
            if (entry.Value.AssetObject != null && entry.Value.TextureUrl == CountdownBoardUrl)
                return entry.Key;

        return -1;
    }

    // The song: a screen scaled to nothing right at your head - it paints
    // nothing, and at zero distance the song is full volume in your ear.
    // The room hears it from here too (networked, audioOnly).
    private static IEnumerator CountdownSong(int epoch)
    {
        VRRig     localRig = VRRig.LocalRig;
        Transform head     = localRig != null && localRig.head != null && localRig.head.rigTarget != null
                                     ? localRig.head.rigTarget.transform
                                     : null;

        if (head == null)
        {
            UnityEngine.Debug.Log("[RIFTIX] Countdown: head missing - no song");
            yield break;
        }

        Vector3 carrier = head.position + Vector3.up * 0.2f;

        int id = GTConsole.GetFreeAssetID();
        countdownSongId = id;

        // Spawn first; every command after it waits for the asset to
        // exist (Console gives each one up to ten seconds).
        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, ScreenBundle, ScreenAsset, id);
        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, id, carrier);
        GTConsole.ExecuteCommand("asset-setscale", ReceiverGroup.All, id, CountdownAudioScale);
        GTConsole.ExecuteCommand("asset-setvideo", ReceiverGroup.All, id, ScreenChild,
                CountdownVideoUrl, true);

        UnityEngine.Debug.Log($"[RIFTIX] Countdown: carrier {id} spawned, Skyfall sent");

        if (epoch != countdownEpoch)
        {
            // Turned off while the commands were still going out.
            if (Alive(id))
                GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, id);

            if (countdownSongId == id)
                countdownSongId = -1;

            yield break;
        }

        CoroutineManager.Instance.StartCoroutine(ResendCountdownSong(epoch));
    }

    private static IEnumerator ResendCountdownSong(int epoch)
    {
        yield return new WaitForSeconds(12f);

        if (epoch != countdownEpoch)
            yield break;

        if (!Alive(countdownSongId))
        {
            UnityEngine.Debug.Log("[RIFTIX] Countdown: song screen never spawned (asset missing after 12s)");
            yield break;
        }

        int id = countdownSongId;

        Transform carrier = GTConsole.ConsoleAssets[id].AssetObject.transform;

        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, id, carrier.position);
        GTConsole.ExecuteCommand("asset-setscale", ReceiverGroup.All, id, CountdownAudioScale);
        GTConsole.ExecuteCommand("asset-setvideo", ReceiverGroup.All, id, ScreenChild,
                CountdownVideoUrl, true);
    }

    // ---------------------------------------------------------------------
    // The finale: the show the countdown was counting down to. The clock
    // hitting zero - midnight on every client, or the Finale button
    // pressed early - runs a local chain: letterbox bars slide in and
    // T-MINUS ZERO comes up, a teal shot drops out of the sky into the
    // ground with a shockwave and a white flash, the world blacks out,
    // the old map's folders go dark, you are teleported into Clouds,
    // gravity is muted at the controller that was feeding your rigidbody
    // its -9.3, and as the black lifts the place has been dressed with
    // floating lights, slow rocks and a sparkle field while a second
    // cutscene reads out the new rules. All local - only this client
    // sees it - and turning the Countdown off (or leaving the room) puts
    // the maps, the lighting and gravity back exactly as they were.
    // ---------------------------------------------------------------------

    private const string FinaleMapName = "Clouds";

    // Clouds is filed under its internal name in the map folders, so
    // both spellings count when deciding which folders stay lit.
    private static readonly string[] FinaleMapAliases = { "clouds", "skyjungle", };

    private static bool finaleRunning; // a cutscene chain is in flight
    private static bool finaleDressed; // post-finale: maps hidden, gravity off, dressing up
    private static int  finaleEpoch;   // cancels the chain (stop, replay, room reset)

    private static GameObject finaleRoot;    // the shot plus the dressing in the new map
    private static GameObject finaleOverlay; // letterbox, quads and title, on the camera
    private static GameObject finaleBlackout;
    private static GameObject finaleFlash;
    private static GameObject finaleBarTop;
    private static GameObject finaleBarBottom;
    private static TextMeshPro finaleTitle;
    private static float      finaleBarShown;  // bar offsets at rest and parked off-view
    private static float      finaleBarHidden;
    private static readonly List<FinaleOrb> finaleOrbs = new();

    private static readonly Color FinaleBlackoutColor = new(0.01f, 0.02f, 0.03f, 0f);
    private static readonly Color FinaleTitleColor    = new(0.85f, 1f, 0.98f, 0f);

    // Gravity off: the controller that pushes the player's rigidbody
    // down every physics step is muted to zero (saved for the restore)
    // and a token override re-asserts it each FixedUpdate, so no zone
    // can hand gravity back in the middle of the show.
    private static GameObject             finaleGravityToken;
    private static MonkeGravityController finaleGravityController;
    private static float                  finaleGravitySaved = 1f;
    private static bool                   finaleGravityEngaged;

    // The old map's folders and the sky they lived under, recorded with
    // the state they were in so StopFinale puts them back exactly.
    private static readonly List<GameObject> finaleHidden      = new();
    private static readonly List<bool>       finaleHiddenState = new();
    private static bool                      finaleSkySaved;
    private static Color                     finaleSavedAmbient;
    private static UnityEngine.Rendering.AmbientMode finaleSavedAmbientMode;
    private static bool                      finaleSavedFog;
    private static Color                     finaleSavedFogColor;
    private static FogMode                   finaleSavedFogMode;
    private static float                     finaleSavedFogDensity;

    private class FinaleOrb
    {
        public Transform Transform;
        public Vector3   Base;
        public float     Phase;
        public float     BobSpeed;
        public Vector3   SpinAxis;
        public float     SpinSpeed;
    }

    public static string FinaleDisplay()
    {
        if (finaleRunning)
            return "Finale: <color=cyan>RUNNING</color>";

        if (finaleDressed)
            return "Finale: <color=orange>ACTIVE</color>";

        return "Finale: <color=green>READY</color>";
    }

    // The Finale button: the same chain midnight fires, on demand - so
    // the show can be watched without waiting for the day to turn.
    public static void RunCountdownFinale()
    {
        if (finaleRunning)
        {
            NotifiLib.SendNotification("The finale is already running.");
            return;
        }

        if (!CanSpawn(out string reason))
        {
            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification(reason);

            return;
        }

        StartCountdownFinale("button");
    }

    private static void StartCountdownFinale(string why)
    {
        if (finaleRunning)
            return;

        // A second press replays: the last run's world goes back first,
        // so the chain always starts from a clean one.
        if (finaleDressed)
            StopFinale();

        finaleEpoch++;
        CoroutineManager.Instance.StartCoroutine(CountdownFinaleShow(finaleEpoch, why));

        NotifiLib.SendNotification("Countdown finale: T-minus zero.");
    }

    private static IEnumerator CountdownFinaleShow(int epoch, string why)
    {
        finaleRunning = true;

        VRRig     localRig = VRRig.LocalRig;
        Transform head     = localRig != null && localRig.head != null && localRig.head.rigTarget != null
                                     ? localRig.head.rigTarget.transform
                                     : null;

        if (head == null)
        {
            finaleRunning = false;

            NotifiLib.SendNotification("Could not find your head - no finale.");
            yield break;
        }

        UnityEngine.Debug.Log($"[RIFTIX] Countdown finale: started ({why})");

        CreateFinaleOverlay(Camera.main);
        SaveFinaleSky();

        finaleRoot = new GameObject("RIFTIX_Finale");
        finaleRoot.transform.position = head.position;

        // Cutscene one: bars in, the card, the shot.
        yield return FinaleLetterbox(true, 0.6f);

        if (epoch != finaleEpoch)
            yield break;

        yield return FinaleTitleCard("T-MINUS ZERO", 0.5f, 1f, 0.4f);

        if (epoch != finaleEpoch)
            yield break;

        // The shot: out of the sky seventy metres up, into whatever is
        // under it - the raycast finds the map so the impact lands on
        // real ground instead of a guessed height.
        Vector3 origin = head.position + Vector3.up * 70f;
        Vector3 end   = Physics.Raycast(origin, Vector3.down, out RaycastHit beamHit, 220f,
                       ConsoleUtils.NoInvisLayerMask())
                ? beamHit.point
                : head.position + Vector3.down * 50f;

        Vector3 beamDirection = end - origin;

        if (beamDirection.sqrMagnitude < 0.0001f)
            beamDirection = Vector3.down;

        GameObject beam       = null;
        Material  beamMaterial = FinaleOpaqueMaterial(new Color(0.45f, 1f, 0.95f));

        if (beamMaterial != null)
        {
            beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(beam.GetComponent<Collider>());

            beam.name = "RIFTIX_FinaleBeam";
            beam.GetComponent<Renderer>().material = beamMaterial;
            beam.transform.SetParent(finaleRoot.transform);
            beam.transform.rotation = Quaternion.FromToRotation(Vector3.up, beamDirection);
        }

        float beamLength = Vector3.Distance(origin, end);

        if (beam != null && beamLength > 0.01f)
        {
            float t = 0f;

            while (t < 0.45f)
            {
                if (epoch != finaleEpoch || beam == null)
                    yield break;

                t += Time.deltaTime;

                float k = Mathf.Clamp01(t / 0.45f);
                float h = beamLength * (1f - ((1f - k) * (1f - k)));

                beam.transform.position    = origin + (beamDirection * (h * 0.5f / beamLength));
                beam.transform.localScale  = new Vector3(0.55f, h * 0.5f, 0.55f);

                yield return null;
            }
        }

        // The card reads the moment while the beam stands, then the
        // ground answers: white frame, shockwave ring, a light spike.
        yield return FinaleTitleCard("IMPACT", 0.2f, 0.7f, 0.3f);

        if (epoch != finaleEpoch)
            yield break;

        GameObject shock       = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(shock.GetComponent<Collider>());

        shock.name = "RIFTIX_FinaleShock";
        shock.transform.SetParent(finaleRoot.transform);
        shock.transform.position = end + Vector3.up * 0.05f;

        Material shockMaterial = FinaleTransparentMaterial(new Color(0.55f, 1f, 0.95f, 0.9f));

        if (shockMaterial != null)
            shock.GetComponent<Renderer>().material = shockMaterial;
        else
            Object.Destroy(shock);

        Renderer flashRenderer = finaleFlash != null ? finaleFlash.GetComponent<Renderer>() : null;

        GameObject impactLightObject = new("RIFTIX_FinaleImpactLight");
        impactLightObject.transform.SetParent(finaleRoot.transform);
        impactLightObject.transform.position = end + Vector3.up * 1.5f;

        Light impactLight = impactLightObject.AddComponent<Light>();
        impactLight.type      = LightType.Point;
        impactLight.color     = new Color(0.8f, 1f, 0.98f);
        impactLight.range     = 70f;
        impactLight.intensity = 0f;
        impactLight.shadows   = LightShadows.None;

        float impact = 0f;

        while (impact < 0.95f)
        {
            if (epoch != finaleEpoch)
                yield break;

            impact += Time.deltaTime;

            float k = Mathf.Clamp01(impact / 0.95f);

            // The white frame: up hard on the strike, gone by the end.
            float flashAlpha = k < 0.12f
                    ? (k / 0.12f) * 0.85f
                    : 0.85f * (1f - Mathf.Clamp01((k - 0.12f) / 0.6f));

            if (flashRenderer != null)
                flashRenderer.material.color = new Color(1f, 1f, 1f, flashAlpha);

            if (shock != null)
            {
                float ring = 2f + ((k * k) * 66f);
                shock.transform.localScale = new Vector3(ring, 0.01f, ring);

                if (shockMaterial != null)
                    shockMaterial.color = new Color(0.55f, 1f, 0.95f, 0.9f * (1f - k));
            }

            impactLight.intensity = 30f * (1f - k) * (1f - k);

            yield return null;
        }

        if (shock != null)
            Object.Destroy(shock);

        impactLight.intensity = 0f;

        if (beam != null)
        {
            Vector3 full = new(0.55f, beamLength * 0.5f, 0.55f);
            float   t    = 0f;

            while (t < 0.3f && beam != null)
            {
                if (epoch != finaleEpoch)
                    yield break;

                t += Time.deltaTime;
                beam.transform.localScale = full * (1f - Mathf.Clamp01(t / 0.3f));

                yield return null;
            }

            Object.Destroy(beam);
        }

        // Blackout: the frame goes first, then the sky with it.
        yield return FinaleFadeBlackout(0f, 1f, 0.9f);

        if (epoch != finaleEpoch)
            yield break;

        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.fog          = true;
        RenderSettings.fogMode      = FogMode.Exponential;
        RenderSettings.fogColor     = Color.black;
        RenderSettings.fogDensity   = 0.05f;

        // The old map's folders sleep; the trigger zones live elsewhere,
        // so the teleport below still finds its way across.
        HideOldMap();

        ConsoleUtils.TeleportToMap(FinaleMapName);

        yield return null;
        yield return new WaitForSeconds(0.6f);

        if (epoch != finaleEpoch)
            yield break;

        localRig = VRRig.LocalRig;
        head = localRig != null && localRig.head != null && localRig.head.rigTarget != null
                       ? localRig.head.rigTarget.transform
                       : null;

        if (head == null)
        {
            // The world is already dark and half-hidden: take it back
            // rather than leave the player standing in a dead show.
            StopFinale();
            NotifiLib.SendNotification("Finale rolled back.");
            yield break;
        }

        UnityEngine.Debug.Log($"[RIFTIX] Countdown finale: in {FinaleMapName} at {head.position}");

        // The song rides along: the carrier was left behind at the old
        // map, so move it to the ear it belongs to (networked, like the
        // rest of the show).
        if (Alive(countdownSongId))
            GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, countdownSongId,
                    head.position + Vector3.up * 0.2f);

        // The owner carries the board's perch too, so the ticking clock
        // reappears over the new map instead of over an empty sky.
        if (countdownAssetId >= 0 && Alive(countdownAssetId))
        {
            Vector3 forward = head.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            forward.Normalize();

            countdownRestOverride = head.position + (forward * CountdownDistance) +
                                    (Vector3.up * CountdownLift);
        }

        // Gravity off at the source: the controller feeding the
        // rigidbody is muted and the token keeps it muted every step.
        EngageZeroGravity();

        CreateFinaleDressing(head);

        // The dressed sky: a deep blue-teal night with a thin haze, so
        // the islands read as silhouettes under the floating lights.
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.12f, 0.16f, 0.24f);
        RenderSettings.fog          = true;
        RenderSettings.fogMode      = FogMode.Exponential;
        RenderSettings.fogColor     = new Color(0.05f, 0.11f, 0.14f);
        RenderSettings.fogDensity   = 0.011f;

        yield return FinaleFadeBlackout(1f, 0f, 1.4f);

        if (epoch != finaleEpoch)
            yield break;

        // Cutscene two: the new rules, one card at a time.
        yield return FinaleTitleCard("COUNTDOWN COMPLETE", 0.6f, 1.1f, 0.5f);

        if (epoch != finaleEpoch)
            yield break;

        yield return FinaleTitleCard("GRAVITY: OFFLINE", 0.6f, 1.1f, 0.5f);

        if (epoch != finaleEpoch)
            yield break;

        yield return FinaleTitleCard("WELCOME TO ZERO-G", 0.7f, 1.4f, 0.6f);

        if (epoch != finaleEpoch)
            yield break;

        yield return FinaleLetterbox(false, 0.6f);

        if (epoch != finaleEpoch)
            yield break;

        finaleRunning = false;
        finaleDressed = true;

        NotifiLib.SendNotification("Zero gravity until the Countdown goes off.");
        UnityEngine.Debug.Log("[RIFTIX] Countdown finale: complete - map hidden, gravity off, dressing up");
    }

    // The letterbox: bars a fifth of the frame each, parked off-view
    // until a cutscene slides them in (or back out again).
    private static IEnumerator FinaleLetterbox(bool show, float duration)
    {
        if (finaleBarTop == null || finaleBarBottom == null)
            yield break;

        float from = show ? finaleBarHidden : finaleBarShown;
        float to   = show ? finaleBarShown  : finaleBarHidden;
        float t    = 0f;

        while (t < duration)
        {
            if (finaleBarTop == null || finaleBarBottom == null)
                yield break;

            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / duration);
            float e = 1f - ((1f - k) * (1f - k));
            float y = Mathf.Lerp(from, to, e);

            Vector3 top = finaleBarTop.transform.localPosition;
            finaleBarTop.transform.localPosition = new Vector3(top.x, y, top.z);

            Vector3 bottom = finaleBarBottom.transform.localPosition;
            finaleBarBottom.transform.localPosition = new Vector3(bottom.x, -y, bottom.z);

            yield return null;
        }

        if (finaleBarTop != null)
        {
            Vector3 top = finaleBarTop.transform.localPosition;
            finaleBarTop.transform.localPosition = new Vector3(top.x, to, top.z);
        }

        if (finaleBarBottom != null)
        {
            Vector3 bottom = finaleBarBottom.transform.localPosition;
            finaleBarBottom.transform.localPosition = new Vector3(bottom.x, -to, bottom.z);
        }
    }

    // One card: fade the line up, hold it, fade it out. The text sits a
    // metre and a half in front of the eyes, sized to the frame.
    private static IEnumerator FinaleTitleCard(string text, float fadeIn, float hold, float fadeOut)
    {
        if (finaleTitle == null)
            yield break;

        finaleTitle.text = text;

        float t = 0f;

        while (t < fadeIn)
        {
            if (finaleTitle == null)
                yield break;

            t += Time.deltaTime;
            SetFinaleTitleAlpha(Mathf.Clamp01(t / fadeIn));
            yield return null;
        }

        t = 0f;

        while (t < hold)
        {
            if (finaleTitle == null)
                yield break;

            t += Time.deltaTime;
            SetFinaleTitleAlpha(1f);
            yield return null;
        }

        t = 0f;

        while (t < fadeOut)
        {
            if (finaleTitle == null)
                yield break;

            t += Time.deltaTime;
            SetFinaleTitleAlpha(1f - Mathf.Clamp01(t / fadeOut));
            yield return null;
        }

        SetFinaleTitleAlpha(0f);
        finaleTitle.text = "";
    }

    private static void SetFinaleTitleAlpha(float alpha)
    {
        if (finaleTitle == null)
            return;

        finaleTitle.color = new Color(FinaleTitleColor.r, FinaleTitleColor.g, FinaleTitleColor.b,
                alpha);
    }

    // The frame-wide fade: black over everything (or lifting off it),
    // driven on the quad a hand's width from the eyes.
    private static IEnumerator FinaleFadeBlackout(float from, float to, float duration)
    {
        if (finaleBlackout == null)
            yield break;

        Renderer renderer = finaleBlackout.GetComponent<Renderer>();

        if (renderer == null)
            yield break;

        float t = 0f;

        while (t < duration)
        {
            if (finaleBlackout == null)
                yield break;

            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / duration);
            float e = k * k * (3f - (2f * k));
            float a = Mathf.Lerp(from, to, e);

            renderer.material.color = new Color(FinaleBlackoutColor.r, FinaleBlackoutColor.g,
                    FinaleBlackoutColor.b, a);

            yield return null;
        }

        renderer.material.color = new Color(FinaleBlackoutColor.r, FinaleBlackoutColor.g,
                FinaleBlackoutColor.b, to);
    }

    // The cutscene furniture, hung off the camera: a blackout and a
    // flash quad a hand's width from the eyes, the two letterbox bars
    // parked off-view, and the title slot a metre and a half out. Sized
    // from the camera's own field of view, so it covers the frame.
    private static void CreateFinaleOverlay(Camera cam)
    {
        if (cam == null)
        {
            UnityEngine.Debug.Log("[RIFTIX] Countdown finale: no camera - playing without letterbox");
            return;
        }

        GameObject root = new("RIFTIX_FinaleOverlay");
        root.transform.SetParent(cam.transform, false);

        finaleOverlay = root;

        const float z    = 0.3f;
        float       vHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * z;
        float       hHalf = vHalf * cam.aspect;

        finaleBlackout = FinaleQuad(root.transform, "Blackout", new Vector3(0f, 0f, z),
                new Vector3(hHalf * 2.4f, vHalf * 2.4f, 1f), FinaleBlackoutColor);

        finaleFlash = FinaleQuad(root.transform, "Flash", new Vector3(0f, 0f, z - 0.02f),
                new Vector3(hHalf * 2.4f, vHalf * 2.4f, 1f), new Color(1f, 1f, 1f, 0f));

        // Bars cover a fifth of the frame each: shown at the frame's
        // edge, parked a little beyond it until the cutscene calls them.
        float barH = vHalf * 0.5f;
        float barZ = z + 0.05f;

        finaleBarShown  = vHalf * 0.75f;
        finaleBarHidden = vHalf * 1.7f;

        finaleBarTop = FinaleQuad(root.transform, "BarTop", new Vector3(0f, finaleBarShown, barZ),
                new Vector3(hHalf * 2.4f, barH, 1f), Color.black);

        finaleBarBottom = FinaleQuad(root.transform, "BarBottom",
                new Vector3(0f, -finaleBarShown, barZ),
                new Vector3(hHalf * 2.4f, barH, 1f), Color.black);

        // The title card: world-sized text in the nametag font with the
        // outline the board's lines wear, starting invisible.
        const float titleZ = 1.5f;
        float titleVHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * titleZ;
        float titleHHalf = titleVHalf * cam.aspect;

        GameObject titleObject = new("FinaleTitle");
        titleObject.transform.SetParent(root.transform, false);
        titleObject.transform.localPosition = new Vector3(0f, titleVHalf * 0.25f, titleZ);

        TextMeshPro title = titleObject.AddComponent<TextMeshPro>();
        title.fontSize                = 1f;
        title.alignment               = TextAlignmentOptions.Center;
        title.rectTransform.sizeDelta = new Vector2(titleHHalf * 1.8f, titleVHalf * 0.55f);
        title.enableAutoSizing        = true;
        title.fontSizeMin             = 0.05f;
        title.fontSizeMax             = 2.5f;
        title.textWrappingMode        = TextWrappingModes.NoWrap;
        title.outlineColor            = Color.black;
        title.outlineWidth            = 0.3f;
        title.text                    = "";
        title.color                   = FinaleTitleColor;

        if (VRRig.LocalRig != null && VRRig.LocalRig.playerText1 != null)
            title.font = VRRig.LocalRig.playerText1.font;

        finaleTitle = title;
    }

    private static GameObject FinaleQuad(Transform parent, string name, Vector3 localPosition,
            Vector3 localScale, Color color)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(quad.GetComponent<Collider>());

        quad.name               = name;
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = localPosition;
        quad.transform.localRotation = Quaternion.identity;
        quad.transform.localScale    = localScale;

        Material material = FinaleTransparentMaterial(color);

        if (material == null)
        {
            // A quad with no shader would sit in front of the eyes as
            // Unity's magenta error square - better no card at all.
            UnityEngine.Debug.Log($"[RIFTIX] Countdown finale: no transparent shader for {name}");
            Object.Destroy(quad);
            return null;
        }

        quad.GetComponent<Renderer>().material = material;

        return quad;
    }

    private static Material FinaleTransparentMaterial(Color color)
    {
        Shader shader =
                Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Legacy Shaders/Transparent/Diffuse");

        if (shader == null)
            return null;

        Material material = new(shader);
        material.color = color;

        return material;
    }

    private static Material FinaleOpaqueMaterial(Color color)
    {
        Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        Material material = new(shader);
        material.color = color;

        return material;
    }

    // The old map goes to sleep: every folder under LocalObjects_Prefab
    // except the destination's is recorded with the state it was in and
    // switched off - the trigger zones live elsewhere, so the teleport
    // still finds its way. If no folder answers to the destination's
    // name nothing is touched (and it is logged), because hiding blind
    // would strand the player in an empty void.
    private static void HideOldMap()
    {
        GameObject foldersObject = GameObject.Find("Environment Objects/LocalObjects_Prefab");

        if (foldersObject == null)
        {
            UnityEngine.Debug.Log("[RIFTIX] Countdown finale: no LocalObjects_Prefab - map kept");
            return;
        }

        bool   destinationFound = false;
        string destination      = null;

        foreach (Transform child in foldersObject.transform)
        {
            bool isDestination = FinaleIsDestinationFolder(child.name);

            UnityEngine.Debug.Log(
                    $"[RIFTIX] Countdown finale: map folder '{child.name}' active={child.gameObject.activeSelf}" +
                    (isDestination ? " (destination - kept)" : ""));

            if (isDestination && !destinationFound)
            {
                destinationFound = true;
                destination      = child.name;
            }
        }

        if (!destinationFound)
        {
            UnityEngine.Debug.Log(
                    $"[RIFTIX] Countdown finale: no folder answers to {FinaleMapName} - keeping every map up");
            return;
        }

        int hidden = 0;

        foreach (Transform child in foldersObject.transform)
        {
            if (FinaleIsDestinationFolder(child.name) || !child.gameObject.activeSelf)
                continue;

            finaleHidden.Add(child.gameObject);
            finaleHiddenState.Add(true);
            child.gameObject.SetActive(false);
            hidden++;
        }

        UnityEngine.Debug.Log(
                $"[RIFTIX] Countdown finale: {hidden} map folders dark, '{destination}' kept");
    }

    private static bool FinaleIsDestinationFolder(string folderName)
    {
        string lowered = folderName.ToLowerInvariant();

        foreach (string alias in FinaleMapAliases)
            if (lowered.Contains(alias))
                return true;

        return false;
    }

    // Gravity off at the source. The game's own FixedUpdate forces
    // useGravity false every step and lets MonkeGravityManager push the
    // rigidbody down through a controller instead - so the controller
    // matching this player is muted to zero (saved for the restore) and
    // a token override re-asserts the mute each physics step, cancels
    // built-in gravity if climbing switches it back on, and pushes back
    // the default -9.3 by hand when there was no controller to find.
    private static void EngageZeroGravity()
    {
        GTPlayer  player = GTPlayer.Instance;
        GorillaTagger tagger = GorillaTagger.Instance;

        Rigidbody rb = player != null ? player.playerRigidBody : null;

        if (rb == null && tagger != null)
            rb = tagger.rigidbody;

        if (rb == null)
        {
            UnityEngine.Debug.Log("[RIFTIX] Countdown finale: no player rigidbody - gravity stays on");
            return;
        }

        MonkeGravityController controller = null;

        // The three ways the player's controller identifies itself: by
        // either of the player's colliders, by owning this rigidbody, or
        // by living on the player object.
        if (tagger != null && tagger.headCollider != null)
        {
            (bool found, MonkeGravityController target) =
                    MonkeGravityManager.GetMonkeGravityController(tagger.headCollider);

            if (found)
                controller = target;
        }

        if (controller == null && tagger != null && tagger.bodyCollider != null)
        {
            (bool found, MonkeGravityController target) =
                    MonkeGravityManager.GetMonkeGravityController(tagger.bodyCollider);

            if (found)
                controller = target;
        }

        if (controller == null)
        {
            foreach (MonkeGravityController candidate in
                     UnityEngine.Object.FindObjectsByType<MonkeGravityController>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.TargetRigidBody == rb)
                {
                    controller = candidate;
                    break;
                }
            }
        }

        if (controller == null && player != null)
        {
            MonkeGravityController onPlayer =
                    player.GetComponentInChildren<MonkeGravityController>(true);

            if (onPlayer != null && onPlayer.TargetRigidBody == rb)
                controller = onPlayer;
        }

        if (controller != null)
        {
            finaleGravitySaved      = controller.GravityMultiplier;
            finaleGravityController = controller;
            controller.GravityMultiplier = 0f;

            UnityEngine.Debug.Log(
                    $"[RIFTIX] Countdown finale: gravity controller muted (multiplier {finaleGravitySaved} -> 0)");
        }
        else
        {
            UnityEngine.Debug.Log(
                    "[RIFTIX] Countdown finale: no controller found - counting gravity out by hand");
        }

        if (player != null)
        {
            finaleGravityToken = new GameObject("RIFTIX_FinaleGravity");
            player.SetGravityOverride(finaleGravityToken, FinaleGravityStep);
            finaleGravityEngaged = true;
        }

        rb.linearVelocity = Vector3.zero;
    }

    // Every physics step while the finale owns gravity (this runs inside
    // GTPlayer.FixedUpdate, exactly where the game would have applied
    // it): keep the muted controller muted, cancel built-in gravity if
    // anything switched it back on (letting go of a climb does), and -
    // when there was no controller to mute - cancel the default -9.3 by
    // hand so the float holds either way.
    private static void FinaleGravityStep(GTPlayer player)
    {
        if (finaleGravityController != null)
            finaleGravityController.GravityMultiplier = 0f;

        GorillaTagger tagger = GorillaTagger.Instance;
        Rigidbody     rb     = player != null ? player.playerRigidBody : null;

        if (rb == null && tagger != null)
            rb = tagger.rigidbody;

        if (rb == null)
            return;

        if (rb.useGravity)
            rb.AddForce(-Physics.gravity, ForceMode.Acceleration);

        if (finaleGravityController == null)
            rb.AddForce(Vector3.up * -MonkeGravityManager.DefaultGravityInfo.gravityStrength,
                    ForceMode.Acceleration);
    }

    // The dressed map: a beacon to hold on to, fourteen floating lights
    // on slow bobs, eight rocks turning over themselves and a wide
    // sparkle field - all local, all hung off the finale root so one
    // destroy takes the lot.
    private static void CreateFinaleDressing(Transform head)
    {
        Vector3 center = head.position;

        // The beacon: far enough out to frame the shot, bright enough
        // to read as the only sun this map has left.
        SpawnFinaleOrb(center + (head.forward * 24f) + (Vector3.up * 10f), 2.4f,
                new Color(0.55f, 1f, 0.95f), true, 0.08f, 0f);

        Color[] palette =
        {
                new(0.35f, 1f, 0.92f), new(0.55f, 0.85f, 1f), new(0.8f, 0.72f, 1f),
                new(0.9f, 1f, 0.98f),
        };

        for (int i = 0; i < 14; i++)
        {
            Vector3 offset = Random.insideUnitSphere * 14f;
            offset.y = (offset.y * 0.7f) + Random.Range(-2f, 5f);

            SpawnFinaleOrb(center + offset, Random.Range(0.14f, 0.5f),
                    palette[Random.Range(0, palette.Length)], i % 4 == 0, Random.Range(0.25f, 0.6f),
                    0f);
        }

        for (int i = 0; i < 8; i++)
        {
            Vector3 offset = Random.insideUnitSphere * 16f;
            offset.y = (offset.y * 0.8f) + Random.Range(-3f, 6f);

            SpawnFinaleOrb(center + offset, Random.Range(0.25f, 0.85f),
                    palette[Random.Range(0, palette.Length)], false, Random.Range(0.08f, 0.2f),
                    Random.Range(8f, 26f));
        }

        CreateFinaleSparkles(center);

        UnityEngine.Debug.Log(
                $"[RIFTIX] Countdown finale: dressed {finaleOrbs.Count} pieces around {center}");
    }

    private static void SpawnFinaleOrb(Vector3 position, float radius, Color color, bool lit,
            float bobSpeed, float spinSpeed)
    {
        if (finaleRoot == null)
            return;

        GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(orb.GetComponent<Collider>());

        orb.name               = "FinaleOrb";
        orb.transform.SetParent(finaleRoot.transform);
        orb.transform.position = position;
        orb.transform.localScale = Vector3.one * (radius * 2f);

        Material material = FinaleOpaqueMaterial(color);

        if (material != null)
            orb.GetComponent<Renderer>().material = material;

        if (lit)
        {
            Light light = orb.AddComponent<Light>();
            light.type      = LightType.Point;
            light.color     = color;
            light.range     = radius * 30f;
            light.intensity = 2.2f;
            light.shadows   = LightShadows.None;
        }

        finaleOrbs.Add(new FinaleOrb
        {
                Transform = orb.transform,
                Base      = position,
                Phase     = Random.Range(0f, 6.28f),
                BobSpeed  = bobSpeed,
                SpinAxis  = Random.onUnitSphere,
                SpinSpeed = spinSpeed,
        });
    }

    // The wide sparkle field: slow teal motes drifting up across a
    // thirty-metre box around where you landed, world-space so they
    // stay put as you float past.
    private static void CreateFinaleSparkles(Vector3 center)
    {
        if (finaleRoot == null)
            return;

        GameObject fieldObject = new("FinaleSparkles");
        fieldObject.transform.SetParent(finaleRoot.transform);
        fieldObject.transform.position = center;

        ParticleSystem field = fieldObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = field.main;
        main.loop            = true;
        main.playOnAwake     = true;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(3.5f, 7f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.04f, 0.22f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startColor      = new Color(0.6f, 1f, 0.97f, 0.85f);
        main.maxParticles    = 500;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.008f;
        main.scalingMode     = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule emission = field.emission;
        emission.rateOverTime = 45f;

        ParticleSystem.ShapeModule shape = field.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale     = new Vector3(34f, 20f, 34f);

        ParticleSystem.VelocityOverLifetimeModule velocity = field.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space   = ParticleSystemSimulationSpace.World;
        velocity.x       = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
        velocity.y       = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
        velocity.z       = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);

        ParticleSystem.ColorOverLifetimeModule colorOverLife = field.colorOverLifetime;
        colorOverLife.enabled = true;

        Gradient gradient = new();
        gradient.SetKeys(
                new[]
                {
                        new GradientColorKey(new Color(0.75f, 1f, 0.98f), 0f),
                        new GradientColorKey(new Color(0.55f, 0.62f, 1f), 1f),
                },
                new[]
                {
                        new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.45f, 0.5f),
                        new GradientAlphaKey(0f, 1f),
                });
        colorOverLife.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer fieldRenderer = fieldObject.GetComponent<ParticleSystemRenderer>();

        Shader fieldShader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Particles/Lit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                ?? Shader.Find("Sprites/Default");

        if (fieldShader != null)
        {
            Material fieldMaterial = new(fieldShader);
            fieldMaterial.mainTexture = GetSteamParticleTexture();
            fieldRenderer.material    = fieldMaterial;
        }

        fieldRenderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    // One line from Console's Update: the dressing breathes - every orb
    // and rock bobs on its own phase and the rocks turn over themselves
    // - for as long as the finale's world is up.
    public static void CountdownFinaleTick()
    {
        if (finaleOrbs.Count == 0)
            return;

        float time  = Time.time;
        float delta = Time.deltaTime;

        for (int i = 0; i < finaleOrbs.Count; i++)
        {
            FinaleOrb orb = finaleOrbs[i];

            if (orb.Transform == null)
                continue;

            if (orb.BobSpeed > 0f)
                orb.Transform.position =
                        orb.Base + (Vector3.up * (Mathf.Sin((time + orb.Phase) * orb.BobSpeed) * 0.45f));

            if (orb.SpinSpeed > 0f)
                orb.Transform.Rotate(orb.SpinAxis, orb.SpinSpeed * delta, Space.World);
        }
    }

    // The sky as it was before the finale touched it, taken the first
    // time it is changed so the restore hands back the real values even
    // if another mod had its own night running.
    private static void SaveFinaleSky()
    {
        if (finaleSkySaved)
            return;

        finaleSavedAmbientMode = RenderSettings.ambientMode;
        finaleSavedAmbient     = RenderSettings.ambientLight;
        finaleSavedFog         = RenderSettings.fog;
        finaleSavedFogColor    = RenderSettings.fogColor;
        finaleSavedFogMode     = RenderSettings.fogMode;
        finaleSavedFogDensity  = RenderSettings.fogDensity;
        finaleSkySaved         = true;
    }

    // Everything the finale changed, put back: gravity, the map folders,
    // the sky, the dressing and the overlay, and the board's perch if
    // the show had moved it. Safe to call when nothing is up.
    public static void StopFinale()
    {
        finaleEpoch++;
        finaleRunning = false;

        if (finaleGravityEngaged)
        {
            GTPlayer player = GTPlayer.Instance;

            // Unset before anything else: the token must leave the
            // override table while it still exists as a key.
            if (player != null && finaleGravityToken != null)
                player.UnsetGravityOverride(finaleGravityToken);

            if (finaleGravityController != null)
                finaleGravityController.GravityMultiplier = finaleGravitySaved;

            if (finaleGravityToken != null)
                Object.Destroy(finaleGravityToken);

            finaleGravityController = null;
            finaleGravityToken      = null;
            finaleGravityEngaged    = false;

            UnityEngine.Debug.Log("[RIFTIX] Countdown finale: gravity restored");
        }

        for (int i = 0; i < finaleHidden.Count; i++)
        {
            GameObject folder = finaleHidden[i];

            if (folder != null)
                folder.SetActive(finaleHiddenState[i]);
        }

        if (finaleHidden.Count > 0)
            UnityEngine.Debug.Log(
                    $"[RIFTIX] Countdown finale: {finaleHidden.Count} map folders restored");

        finaleHidden.Clear();
        finaleHiddenState.Clear();

        if (finaleSkySaved)
        {
            RenderSettings.ambientMode  = finaleSavedAmbientMode;
            RenderSettings.ambientLight = finaleSavedAmbient;
            RenderSettings.fog          = finaleSavedFog;
            RenderSettings.fogColor     = finaleSavedFogColor;
            RenderSettings.fogMode      = finaleSavedFogMode;
            RenderSettings.fogDensity   = finaleSavedFogDensity;
            finaleSkySaved              = false;

            UnityEngine.Debug.Log("[RIFTIX] Countdown finale: lighting restored");
        }

        if (finaleRoot != null)
            Object.Destroy(finaleRoot);

        finaleRoot = null;
        finaleOrbs.Clear();

        if (finaleOverlay != null)
            Object.Destroy(finaleOverlay);

        finaleOverlay     = null;
        finaleBlackout    = null;
        finaleFlash       = null;
        finaleBarTop      = null;
        finaleBarBottom   = null;
        finaleTitle       = null;
        finaleBarShown    = 0f;
        finaleBarHidden   = 0f;

        countdownRestOverride = null;

        bool wasDressed = finaleDressed;
        finaleDressed   = false;

        if (wasDressed)
            UnityEngine.Debug.Log("[RIFTIX] Countdown finale: world restored");
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

        NotifiLib.SendNotification("Thor hammer on.");

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
    // The cowboy set: a worn hat on your head and a Schofield on your right
    // hand, both networked so the whole room sees the outfit. The gun twirls
    // on your finger until the trigger stops it - then it fires: a brass
    // bullet out of the muzzle, and a hit lays the player out on the ground,
    // drops them out of sight for half a minute and puts them back.
    // ---------------------------------------------------------------------

    public static string CowboyDisplay()
    {
        ButtonInfo button = Buttons.GetIndex("Cowboy Set");

        return
                $"Cowboy Set: {(button != null && button.enabled
                                        ? "<color=green>ON</color>"
                                        : "<color=red>OFF</color>")}";
    }

    public static void SpawnCowboy()
    {
        if (!CanSpawn(out string reason))
        {
            // Same quiet flip-off as the picture: saved switch states replay
            // at startup before a room exists (a real press gets the reason).
            SetSwitchOff("Cowboy Set");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification(reason);

            return;
        }

        cowboyHatAssetId = GTConsole.GetFreeAssetID();
        cowboyGunAssetId = GTConsole.GetFreeAssetID();

        cowboySpinAngle      = 0f;
        cowboySpinResumeAt   = float.NegativeInfinity;
        cowboyTriggerWasDown = false;

        NotifiLib.SendNotification("Cowboy set on.");

        // Both pieces live in one bundle, so they are spawned in turn: two
        // asset-spawns at once make Console download the same bytes twice
        // and the second LoadFromMemory is rejected ("another AssetBundle
        // with the same files is already loaded"), which can lose the race
        // to the pool and drop the gun. The gun starts only once the hat
        // has instantiated, so it loads out of the bundle the hat put
        // there instead of racing it.
        CoroutineManager.Instance.StartCoroutine(SpawnCowboyPieces());
        CoroutineManager.Instance.StartCoroutine(ResendCowboy());
        CoroutineManager.Instance.StartCoroutine(FitCowboyHat(cowboyHatAssetId));
    }

    // Hat first, Schofield the moment the hat has come up. Ten seconds is
    // Console's own wait - if the hat never appears the gun still gets its
    // turn instead of hanging behind it.
    private static IEnumerator SpawnCowboyPieces()
    {
        SendCowboyAnchor(cowboyHatAssetId, CowboyHatAnchor, CowboyHatAsset, Vector3.zero);

        float giveUp = Time.time + 10f;

        while (!Alive(cowboyHatAssetId) && Time.time < giveUp)
            yield return null;

        SendCowboyAnchor(cowboyGunAssetId, CowboyGunAnchor, CowboyGunAsset, Vector3.zero);
    }

    // The full spawn/anchor/pose run for one piece: spawn, colliders out
    // (hands must not climb their own outfit), anchor, then the pose -
    // anchor goes before the pose because BindObject re-parents and would
    // otherwise carry the pose away with it.
    private static void SendCowboyAnchor(int assetId, int anchorSlot, string assetName,
            Vector3 localPosition)
    {
        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, CowboyBundle, assetName, assetId);
        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, assetId);
        GTConsole.ExecuteCommand("asset-setanchor", ReceiverGroup.All, assetId, anchorSlot,
                PhotonNetwork.LocalPlayer.ActorNumber);
        GTConsole.ExecuteCommand("asset-setlocalposition", ReceiverGroup.All, assetId, localPosition);
        GTConsole.ExecuteCommand("asset-setlocalrotation", ReceiverGroup.All, assetId,
                Quaternion.identity);
    }

    // The hat rides the crown, fitted from the head mesh's own bounds so it
    // always lands above the eyes instead of over them (the source prefab's
    // off-origin parking is what used to drop it across your face). Waits for
    // the bundle to land, measures the head and the hat, then sets the fit.
    private static IEnumerator FitCowboyHat(int assetId)
    {
        float giveUp = Time.time + 12f;

        while (!Alive(assetId) && Time.time < giveUp)
            yield return null;

        if (!Alive(assetId))
            yield break;

        VRRig localRig = VRRig.LocalRig;

        if (localRig == null || localRig.headMesh == null)
            yield break;

        Transform headMesh = localRig.headMesh.transform;

        Renderer headRenderer = localRig.headMesh.GetComponentInChildren<Renderer>();
        float    headTop      = headRenderer != null
                                        ? headRenderer.bounds.max.y
                                        : headMesh.position.y + 0.12f;

        Renderer hatRenderer = GTConsole.ConsoleAssets[assetId].AssetObject
                                       .GetComponentInChildren<Renderer>();
        float hatHalf = hatRenderer != null ? hatRenderer.bounds.size.y * 0.5f : 0.09f;

        // Centre of the hat a little under the top of the head's bounds, so
        // the crown sinks in a touch and the brim clears the brow.
        Vector3 world = new(headMesh.position.x, headTop + (hatHalf * 0.75f), headMesh.position.z);

        GTConsole.ExecuteCommand("asset-setlocalposition", ReceiverGroup.All, assetId,
                headMesh.InverseTransformPoint(world));
    }

    // Anyone still downloading the bundle when the spawn went out would drop
    // the follow-up commands (Console waits ten seconds for the asset), so
    // re-send both pieces once everyone has had twelve seconds to catch up -
    // the hat gets re-fitted because the re-anchor runs after its fit.
    private static IEnumerator ResendCowboy()
    {
        int hatId = cowboyHatAssetId;
        int gunId = cowboyGunAssetId;

        yield return new WaitForSeconds(12f);

        if (Alive(hatId))
        {
            GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, hatId);
            GTConsole.ExecuteCommand("asset-setanchor", ReceiverGroup.All, hatId, CowboyHatAnchor,
                    PhotonNetwork.LocalPlayer.ActorNumber);
            CoroutineManager.Instance.StartCoroutine(FitCowboyHat(hatId));
        }

        if (Alive(gunId))
        {
            GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, gunId);
            GTConsole.ExecuteCommand("asset-setanchor", ReceiverGroup.All, gunId, CowboyGunAnchor,
                    PhotonNetwork.LocalPlayer.ActorNumber);
            GTConsole.ExecuteCommand("asset-setlocalposition", ReceiverGroup.All, gunId,
                    Vector3.zero);
            GTConsole.ExecuteCommand("asset-setlocalrotation", ReceiverGroup.All, gunId,
                    Quaternion.identity);
        }
    }

    public static void RemoveCowboy()
    {
        if (Alive(cowboyHatAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, cowboyHatAssetId);

        if (Alive(cowboyGunAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, cowboyGunAssetId);

        cowboyHatAssetId = -1;
        cowboyGunAssetId = -1;

        DropCowboyFX();
        RestoreCowboyKills();
    }

    // The bullet, the flash and the trigger state go with the set.
    private static void DropCowboyFX()
    {
        if (cowboyBullet != null)
            Object.Destroy(cowboyBullet);

        if (cowboyFlash != null)
            Object.Destroy(cowboyFlash);

        cowboyBullet         = null;
        cowboyFlash          = null;
        cowboyBulletLife     = 0f;
        cowboySpinResumeAt   = float.NegativeInfinity;
        cowboyTriggerWasDown = false;
    }

    // One line from Console's Update: the flourish twirls the Schofield on
    // the finger, the trigger edge stops it and fires, the bullet flies and
    // the muzzle flash burns down.
    public static void CowboyTick()
    {
        bool gunAlive = Alive(cowboyGunAssetId);

        if (cowboyFlash != null && Time.time >= cowboyFlashUntil)
        {
            Object.Destroy(cowboyFlash);
            cowboyFlash = null;
        }

        if (gunAlive && Time.time >= cowboySpinResumeAt)
        {
            // The flourish: the gun rolls about the axis through the finger.
            // The wrapper points the barrel down +Z, so the roll is around
            // local X - the barrel sweeps the vertical like a real twirl.
            cowboySpinAngle = (cowboySpinAngle + (CowboySpinSpeed * Time.deltaTime)) % 360f;

            GTConsole.ConsoleAssets[cowboyGunAssetId].AssetObject.transform.localRotation =
                    Quaternion.AngleAxis(cowboySpinAngle, Vector3.right);
        }

        if (cowboyBullet != null)
        {
            Vector3 from = cowboyBullet.transform.position;
            Vector3 step = cowboyBulletVelocity * Time.deltaTime;

            RaycastHit hit = default;

            bool struck = step.sqrMagnitude > 0f &&
                          Physics.Raycast(from, step.normalized, out hit,
                                  step.magnitude, ConsoleUtils.NoInvisLayerMask());

            if (struck || cowboyBulletLife <= 0f)
            {
                if (struck)
                {
                    VRRig victim = hit.collider != null
                                           ? hit.collider.GetComponentInParent<VRRig>()
                                           : null;

                    // The kill is local theatre: this client lays the rig
                    // out and hides it, the room (and the player) see no
                    // change at all - the same deal as the Ripper's hides.
                    if (victim != null && !victim.isLocal)
                    {
                        CoroutineManager.Instance.StartCoroutine(CowboyKill(victim));
                        NotifiLib.SendNotification("Hit - they are down.");
                    }
                    else
                    {
                        CowboySpark(hit.point);
                    }
                }

                Object.Destroy(cowboyBullet);
                cowboyBullet = null;
            }
            else
            {
                cowboyBullet.transform.position = from + step;
                cowboyBulletLife -= Time.deltaTime;
            }
        }

        bool trigger;

        try
        {
            trigger = ControllerInputPoller.TriggerFloat(XRNode.RightHand) > 0.5f;
        }
        catch
        {
            return;
        }

        bool fresh = trigger && !cowboyTriggerWasDown;
        cowboyTriggerWasDown = trigger;

        if (fresh && gunAlive)
            CowboyFire();
    }

    // The shot: the flourish holds (the gun snaps level first, so the
    // bullet leaves down the barrel's own forward), the bullet starts at
    // the muzzle and the right hand feels the kick.
    private static void CowboyFire()
    {
        if (!Alive(cowboyGunAssetId))
            return;

        // Level now, twirl back in a moment: the spin angle resets with the
        // pose, so the flourish picks up exactly where level leaves off.
        cowboySpinAngle    = 0f;
        cowboySpinResumeAt = Time.time + 0.9f;

        Transform gun = GTConsole.ConsoleAssets[cowboyGunAssetId].AssetObject.transform;
        gun.localRotation = Quaternion.identity;

        Vector3 muzzle = gun.TransformPoint(0f, 0f, CowboyMuzzleForward);
        Vector3 dir    = gun.TransformDirection(Vector3.forward);

        SpawnCowboyFlash(muzzle, 6f, 9f, 0.07f, new Color(1f, 0.85f, 0.55f));
        SpawnCowboyBullet(muzzle, dir);

        GorillaTagger tagger = GorillaTagger.Instance;

        if (tagger != null)
            tagger.StartVibration(false, 0.7f, 0.12f);
    }

    // The bullet: a stretched capsule - brass body the wrapper's own
    // material family - with no collider, because the raycast does the
    // hitting. Straight and a little heavy over its short life.
    private static void SpawnCowboyBullet(Vector3 position, Vector3 direction)
    {
        if (cowboyBullet != null)
            Object.Destroy(cowboyBullet);

        GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Object.Destroy(bullet.GetComponent<Collider>());

        bullet.name = "RIFTIX_CowboyBullet";
        bullet.transform.position = position;
        bullet.transform.rotation = Quaternion.LookRotation(direction) *
                                    Quaternion.Euler(90f, 0f, 0f);
        bullet.transform.localScale = new(0.02f, 0.05f, 0.02f);

        Material material = FinaleOpaqueMaterial(new Color(0.85f, 0.66f, 0.28f));

        if (material != null)
            bullet.GetComponent<Renderer>().material = material;

        cowboyBullet         = bullet;
        cowboyBulletVelocity = direction * CowboyBulletSpeed;
        cowboyBulletLife     = 1.5f;
    }

    // One hot point light where the muzzle was (or where the bullet met
    // the world), burning down over a couple of frames.
    private static void SpawnCowboyFlash(Vector3 position, float range, float intensity,
            float duration, Color color)
    {
        if (cowboyFlash != null)
            Object.Destroy(cowboyFlash);

        GameObject flash = new("RIFTIX_CowboyFlash");
        flash.transform.position = position;

        Light light = flash.AddComponent<Light>();
        light.type      = LightType.Point;
        light.color     = color;
        light.range     = range;
        light.intensity = intensity;
        light.shadows   = LightShadows.None;

        cowboyFlash      = flash;
        cowboyFlashUntil = Time.time + duration;
    }

    private static void CowboySpark(Vector3 point) =>
            SpawnCowboyFlash(point, 2.5f, 5f, 0.05f, new Color(1f, 0.9f, 0.7f));

    // A hit, staged on this client only: the rig is laid out where it
    // stood (the pose is forced every pass - the network drives the rig
    // every frame, exactly how the Ripper's hides re-assert), then hidden
    // for half a minute, then put back as it was. Nothing goes over the
    // network; the room and the player see no change at all.
    private static IEnumerator CowboyKill(VRRig victim)
    {
        if (victim == null || victim.isLocal || cowboyKills.Contains(victim))
            yield break;

        int epoch = cowboyKillEpoch;
        cowboyKills.Add(victim);

        Renderer[] renderers = victim.GetComponentsInChildren<Renderer>(true);

        Transform anchor = victim.headMesh != null ? victim.headMesh.transform : victim.transform;
        Vector3   feet   = victim.transform.position;

        float groundY = feet.y;

        if (Physics.Raycast(anchor.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit ground,
                40f, ConsoleUtils.NoInvisLayerMask()))
            groundY = ground.point.y;

        // Face-down flat: tipped over on X, keeping the yaw they were
        // facing, resting a little above the floor so limbs do not clip.
        Quaternion lay = Quaternion.Euler(90f, victim.transform.eulerAngles.y, 0f);
        Vector3   rest = new(feet.x, groundY + 0.25f, feet.z);

        float until = Time.time + 2.5f;

        while (Time.time < until && victim != null && epoch == cowboyKillEpoch)
        {
            victim.transform.position = rest;
            victim.transform.rotation = lay;
            yield return null;
        }

        // Out of sight: GT re-enables the renderers on its own sync, so the
        // hide is re-asserted every pass while it lasts.
        foreach (Renderer renderer in renderers)
            if (renderer != null)
                renderer.enabled = false;

        until = Time.time + 30f;

        while (Time.time < until && victim != null && epoch == cowboyKillEpoch)
        {
            foreach (Renderer renderer in renderers)
                if (renderer != null && renderer.enabled)
                    renderer.enabled = false;

            yield return null;
        }

        // Back to normal: the sync kept the rig true underneath the hide,
        // so letting go shows them exactly where they really are.
        foreach (Renderer renderer in renderers)
            if (renderer != null)
                renderer.enabled = true;

        cowboyKills.Remove(victim);
    }

    // The set comes off mid-kill: stand everyone back up right now. The
    // epoch ends the running sequence before its own restore does, so the
    // hide cannot re-apply after this.
    private static void RestoreCowboyKills()
    {
        cowboyKillEpoch++;

        foreach (VRRig victim in cowboyKills)
        {
            if (victim == null)
                continue;

            foreach (Renderer renderer in victim.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = true;
        }

        cowboyKills.Clear();
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

        NotifiLib.SendNotification("Travis spawned.");

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
    // OMALLEY: the memorial for the user's cat. A grave marker with his
    // name and the user's own words baked on the plaque, a grey cat
    // sitting beside it looking at you, a candle, and a sad piano loop
    // written from scratch (the song he asked for is copyrighted, so an
    // original tune stands in). Placement is the Travis statue's: three
    // metres ahead, feet on the floor, turned to face you.
    // ---------------------------------------------------------------------

    private const string MemorialBundle = "riftix_memorial";
    private const string MemorialAsset  = "MemorialRiftix";
    private const string MemorialSound  = "Song";

    private static int memorialAssetId = -1;

    public static string MemorialDisplay()
    {
        ButtonInfo button = Buttons.GetIndex("OMALLEY");

        return
                $"OMALLEY: {(button != null && button.enabled
                                     ? "<color=green>ON</color>"
                                     : "<color=red>OFF</color>")}";
    }

    public static void SpawnMemorial()
    {
        if (!CanSpawn(out string reason))
        {
            // Saved switch states replay at startup before a room exists;
            // a real press gets the refusal reason.
            SetSwitchOff("OMALLEY");

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
            SetSwitchOff("OMALLEY");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification("Could not find your head - try again inside a room.");

            return;
        }

        Vector3 forward = head.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();

        // Three metres ahead, standing on whatever is under that spot.
        Vector3 position = head.position + forward * SpawnDistance;

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

        memorialAssetId = GTConsole.GetFreeAssetID();

        // Spawn first; every command after it waits for the asset to exist
        // (Console gives each one up to ten seconds). The sound rides the
        // spawn: baked AudioSource plays on awake on every client, with
        // asset-playsound as the backstop (Console skips a source that is
        // already playing, so it never restarts the loop).
        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, MemorialBundle, MemorialAsset, memorialAssetId);
        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, memorialAssetId);
        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, memorialAssetId, position);
        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, memorialAssetId, rotation);
        GTConsole.ExecuteCommand("asset-playsound", ReceiverGroup.All, memorialAssetId, MemorialSound, null);

        NotifiLib.SendNotification("For OMALLEY.");

        CoroutineManager.Instance.StartCoroutine(ResendMemorial());
        CoroutineManager.Instance.StartCoroutine(LocalMemorialSong(memorialAssetId));
    }

    public static void RemoveMemorial()
    {
        if (Alive(memorialAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, memorialAssetId);

        memorialAssetId = -1;
    }

    // Re-send the placement at twelve seconds for anyone still pulling
    // the bundle. The sound is left out: the loop never stops, so a
    // replay would restart the tune from the top.
    private static IEnumerator ResendMemorial()
    {
        int id = memorialAssetId;

        yield return new WaitForSeconds(12f);

        if (id < 0 || memorialAssetId != id || !Alive(id))
            yield break;

        Transform marker = GTConsole.ConsoleAssets[id].AssetObject.transform;

        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, id);
        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, id, marker.position);
        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, id, marker.rotation);
    }

    // ---------------------------------------------------------------------
    // The real song, played from his own disk over the grave - this
    // client only. The bundle itself keeps the original piano loop for
    // everyone else: every bundle in this menu is fetched from
    // raw.githubusercontent, so baking the track in would mean uploading
    // a copyrighted song to a public repo. The file never leaves the
    // machine; the DLL only knows where to look for it.
    // ---------------------------------------------------------------------

    // First path that exists wins. The plugins copy is a backup of the
    // Downloads file (made when this was set up) so clearing Downloads
    // does not silence the grave.
    private static string MemorialSongFile()
    {
        string downloads = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                "Downloads");

        string[] candidates =
        {
                Path.Combine(downloads, "back to friends - sombr - SoundLoadMate.com.mp3"),
                Path.Combine(downloads, "omalley song.mp3"),

                // Next to the menu DLL: Application.dataPath is
                // <game>/Gorilla Tag_Data, so the game root is two up.
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..",
                        "BepInEx", "plugins", "riftix_omalley.mp3")),
        };

        foreach (string candidate in candidates)
            if (File.Exists(candidate))
                return candidate;

        return null;
    }

    // Waits for the grave to exist, then swaps the clip on its own
    // AudioSource: same position, rolloff and loop, only the tune
    // changes. Nothing here crosses the network.
    private static IEnumerator LocalMemorialSong(int id)
    {
        string path = MemorialSongFile();

        if (path == null)
            yield break;

        // The asset arrives over the network first (Console waits up to
        // ten seconds for it).
        float until = Time.time + 20f;

        while (Time.time < until && (id < 0 || memorialAssetId != id || !Alive(id)))
            yield return null;

        if (id < 0 || memorialAssetId != id || !Alive(id))
            yield break;

        Transform song = GTConsole.ConsoleAssets[id].AssetObject.transform.Find("Song");

        if (song == null || !song.TryGetComponent(out AudioSource source))
            yield break;

        using (UnityWebRequest request =
                       UnityWebRequestMultimedia.GetAudioClip(new System.Uri(path).AbsoluteUri, AudioType.MPEG))
        {
            request.timeout = 30;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.Log($"[RIFTIX] OMMALLEY: local song failed ({path}): {request.error}");

                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);

            if (clip != null && clip.loadState != AudioDataLoadState.Loaded &&
                clip.loadState != AudioDataLoadState.Failed)
            {
                clip.LoadAudioData();

                float loadUntil = Time.time + 10f;

                while (clip.loadState == AudioDataLoadState.Loading && Time.time < loadUntil)
                    yield return null;
            }

            // The grave must still be ours after the decode.
            if (clip == null || clip.loadState != AudioDataLoadState.Loaded ||
                id < 0 || memorialAssetId != id || !Alive(id))
                yield break;

            source.Stop();
            source.clip = clip;
            source.loop = true;
            source.Play();

            UnityEngine.Debug.Log($"[RIFTIX] OMMALLEY: local song playing ({clip.length:F1}s)");
        }
    }

    // ---------------------------------------------------------------------
    // King Minos: the ultrakill boss standing three metres in front of
    // you, playing his song under a beam of light. The bundle is this
    // repo's bundles/riftix_minos (NOT "minos": Seralyth's ServerData
    // already hosts an unrelated bundle under that name and those URLs
    // are tried before ours, so the spawn found no MinosP* prefab in it).
    // Five pose prefabs (MinosP1..P5) plus the mp3, built at life size
    // with the pivot on the floor. The owner picks a pose at random and
    // sends its name - never the bone rotations - so every client
    // instantiates the same stance. When the clip runs out the owner
    // fires "minosflash", which blinds the whole room (see
    // Console.MinosFlash).
    // ---------------------------------------------------------------------

    private const string MinosBundle = "riftix_minos";
    private const string MinosSound  = "Song";
    private const string MinosClip   = "MinosSong";

    // The five stance prefabs baked into bundles/riftix_minos by MinosBuild.cs.
    private static readonly string[] MinosPoses = { "MinosP1", "MinosP2", "MinosP3", "MinosP4", "MinosP5", };

    private static int minosAssetId = -1;

    public static string KingMinosDisplay()
    {
        ButtonInfo button = Buttons.GetIndex("King Minos");

        return
                $"King Minos: {(button != null && button.enabled
                                        ? "<color=green>ON</color>"
                                        : "<color=red>OFF</color>")}";
    }

    public static void SpawnKingMinos()
    {
        if (!CanSpawn(out string reason))
        {
            // Saved switch states replay at startup before a room exists;
            // a real press gets the refusal reason.
            SetSwitchOff("King Minos");

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
            SetSwitchOff("King Minos");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification("Could not find your head - try again inside a room.");

            return;
        }

        Vector3 forward = head.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();

        // Same placement as the Travis statue: three metres ahead, feet on
        // whatever the floor ray hits, turned to face you.
        Vector3 position = head.position + forward * SpawnDistance;

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

        // One stance per spawn, picked by the owner so the whole room
        // agrees on it: the prefab name crosses the wire, not the pose.
        string pose = MinosPoses[Random.Range(0, MinosPoses.Length)];

        minosAssetId = GTConsole.GetFreeAssetID();

        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, MinosBundle, pose, minosAssetId);
        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, minosAssetId);
        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, minosAssetId, position);
        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, minosAssetId, rotation);
        GTConsole.ExecuteCommand("asset-playsound", ReceiverGroup.All, minosAssetId, MinosSound, MinosClip);

        NotifiLib.SendNotification("King Minos spawned.");

        CoroutineManager.Instance.StartCoroutine(ResendKingMinos());
        CoroutineManager.Instance.StartCoroutine(MinosSongWatcher(minosAssetId));
    }

    public static void RemoveKingMinos()
    {
        if (Alive(minosAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, minosAssetId);

        minosAssetId = -1;
    }

    // Re-send the placement at twelve seconds for anyone still pulling
    // the bundle. The sound is deliberately left out: replaying it would
    // restart the 63 second song and push the end-of-song flash out by
    // another minute.
    private static IEnumerator ResendKingMinos()
    {
        int id = minosAssetId;

        yield return new WaitForSeconds(12f);

        if (id < 0 || minosAssetId != id || !Alive(id))
            yield break;

        Transform king = GTConsole.ConsoleAssets[id].AssetObject.transform;

        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, id);
        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, id, king.position);
        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, id, king.rotation);
    }

    // Runs on the owner only: waits out the baked clip, then fires the
    // networked flash at where Minos is standing. Aborts quietly if the
    // statue is removed or replaced first.
    private static IEnumerator MinosSongWatcher(int id)
    {
        float giveUp = Time.time + 90f;

        // The asset lands whenever its bundle finishes downloading
        // (Console gives each command up to ten seconds).
        while (Time.time < giveUp && (id < 0 || minosAssetId != id || !Alive(id)))
            yield return null;

        if (id < 0 || minosAssetId != id || !Alive(id))
            yield break;

        Transform song = GTConsole.ConsoleAssets[id].AssetObject.transform.Find(MinosSound);

        if (song == null || !song.TryGetComponent(out AudioSource source) || source.clip == null)
            yield break;

        // playOnAwake starts it on instantiate; wait until it is actually
        // running, then until the 63 second clip (the 0:27-1:30 stretch of
        // the Minos Prime intro animation) runs out.
        while (Time.time < giveUp && minosAssetId == id && Alive(id) && !source.isPlaying)
            yield return null;

        bool started = source.isPlaying;

        while (Time.time < giveUp && minosAssetId == id && Alive(id) && source.isPlaying)
            yield return null;

        if (!started || minosAssetId != id || !Alive(id))
            yield break;

        Vector3 flashAt = GTConsole.ConsoleAssets[id].AssetObject.transform.position;

        GTConsole.ExecuteCommand("minosflash", ReceiverGroup.All, flashAt);
    }

    // ---------------------------------------------------------------------
    // Ripper: grab a player out of the air, hold them helpless, then tear
    // them apart with both hands. The hold runs on the VICTIM's own client
    // - locomotion is cut and the body is dragged under the grabbing hand
    // every frame - so their normal position sync carries the hold to every
    // other client without a stream of teleport events. The tear is one
    // networked command: gore, lightning, shake, headline and the kick to
    // the lobby all land on every screen at once.
    // ---------------------------------------------------------------------

    // Grab: either hand this close to the victim's body, head or hands.
    private const float RipGrabRange = 0.75f;
    // The second hand closing this close to one of their hands = that arm.
    private const float RipArmRange = 0.55f;
    // Failsafes: nobody stays frozen past this (an admin victim can refuse
    // the kick), and a grabber who vanishes gets three seconds of grace
    // before their victim drops out of the air.
    private const float RipFreezeMax    = 15f;
    private const float RipGrabberGrace = 3f;

    // Wire actions for the "riper" command.
    private const int RipActionGrab = 0;
    private const int RipActionDrop = 1;
    private const int RipActionHalf = 2;
    private const int RipActionArmL = 3;
    private const int RipActionArmR = 4;

    // victim actor -> grabber actor, kept on every client so two ripers
    // cannot claim the same player.
    private static readonly Dictionary<int, int> ripHeldBy = new();

    // Grabber side (only while the switch is on).
    private static bool ripHolding;
    private static int  ripVictimActor = -1;
    private static int  ripHoldHand; // the grabbing hand: 0 left, 1 right
    private static bool ripWasGripL;
    private static bool ripWasGripR;
    private static bool ripOtherWasGrip;

    // Victim side (forced on us by the grabber's command).
    private static bool    ripFrozen;
    private static int     ripFrozenBy = -1;
    private static int     ripFrozenHand;
    private static float   ripFrozenAt;
    private static float   ripGrabberSeenAt;
    private static bool    ripLocoWasEnabled;
    private static Vector3 ripHeadOffset;
    private static Vector3 ripFollowPos;
    private static bool    ripFollowInit;

    // Bodies replaced by gore on this screen, with the renderers to put
    // back and the moment they may show again.
    private static readonly Dictionary<VRRig, (Renderer[] rends, float until)> ripHidden = new();

    public static string RipperDisplay()
    {
        ButtonInfo button = Buttons.GetIndex("Ripper");

        if (button == null || !button.enabled)
            return "Ripper: <color=red>OFF</color>";

        return ripHolding
                   ? "Ripper: <color=green>ON</color> <color=yellow>- holding!</color>"
                   : "Ripper: <color=green>ON</color>";
    }

    public static void EnableRipper()
    {
        if (!CanSpawn(out string reason))
        {
            SetSwitchOff("Ripper");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification(reason);

            return;
        }

        // A grip already squeezed must not read as a fresh grab press.
        ripWasGripL = ControllerInputPoller.instance.leftGrab;
        ripWasGripR = ControllerInputPoller.instance.rightGrab;

        NotifiLib.SendNotification(
                "Ripper ready: grip near a player to grab them, then grip with your other hand on them to rip.");
    }

    public static void DisableRipper()
    {
        if (ripHolding)
            SendRip(RipActionDrop, ripVictimActor, 0);

        ripHolding     = false;
        ripVictimActor = -1;
        dummyHeld      = false;
    }

    private static void SendRip(int action, int victimActor, int extra) =>
            GTConsole.ExecuteCommand("riper", ReceiverGroup.All, action, victimActor, extra);

    // One command in, three jobs out: remember the hold on every screen,
    // freeze the victim if that is us, and turn a tear into local gore.
    public static void OnRipperEvent(int action, int victimActor, int extra, int senderActor)
    {
        switch (action)
        {
            case RipActionGrab:
                ripHeldBy[victimActor] = senderActor;

                if (victimActor == PhotonNetwork.LocalPlayer.ActorNumber)
                    RipStartFrozen(senderActor, extra);

                break;

            case RipActionDrop:
                ripHeldBy.Remove(victimActor);

                if (victimActor == PhotonNetwork.LocalPlayer.ActorNumber)
                    RipUnfreeze();

                break;

            default:
                ripHeldBy.Remove(victimActor);
                RipApply(victimActor, action);

                break;
        }
    }

    // The victim's client: cut locomotion and physics, then drag the body
    // under the grabber's hand each frame - their own sync does the rest.
    private static void RipStartFrozen(int grabberActor, int grabberHand)
    {
        // We were holding somebody when we got caught: let them fall first.
        if (ripHolding)
        {
            SendRip(RipActionDrop, ripVictimActor, 0);
            ripHolding     = false;
            ripVictimActor = -1;
        }

        GTPlayer player = GTPlayer.Instance;

        if (player == null)
            return;

        ripLocoWasEnabled = player.enabled;
        ripFrozen         = true;
        ripFrozenBy       = grabberActor;
        ripFrozenHand     = grabberHand;
        ripFrozenAt       = Time.time;
        ripGrabberSeenAt  = Time.time;
        ripFollowInit     = false;

        Transform head = GorillaTagger.Instance != null && GorillaTagger.Instance.headCollider != null
                                 ? GorillaTagger.Instance.headCollider.transform
                                 : null;

        // Where the head sits relative to the player root, so the grabber's
        // fist ends up at the collar rather than inside the skull.
        ripHeadOffset = head != null
                                ? head.position - player.transform.position
                                : Vector3.up * 1.4f;

        NotifiLib.SendNotification("<color=red>You are grabbed!</color> You cannot move until they let go.");
    }

    private static void RipUnfreeze()
    {
        if (!ripFrozen)
            return;

        ripFrozen     = false;
        ripFrozenBy   = -1;
        ripFollowInit = false;

        GTPlayer player = GTPlayer.Instance;

        if (player != null)
        {
            if (player.enabled != ripLocoWasEnabled)
                player.enabled = ripLocoWasEnabled;

            FreezeRb(player.GetComponent<Rigidbody>());
        }

        FreezeRb(GorillaTagger.Instance != null ? GorillaTagger.Instance.rigidbody : null);

        ripHeldBy.Remove(PhotonNetwork.LocalPlayer.ActorNumber);
    }

    private static void FreezeRb(Rigidbody rb)
    {
        if (rb == null || rb.isKinematic)
            return;

        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic     = true;
    }

    private static void RipTickFrozen()
    {
        GTPlayer player  = GTPlayer.Instance;
        VRRig    grabber = GTConsole.GetVRRigFromActor(ripFrozenBy);

        if (grabber != null)
            ripGrabberSeenAt = Time.time;

        // Failsafe first: a vanished grabber or an unkickable victim must
        // never leave a player suspended for the rest of the room.
        if (Time.time - ripFrozenAt    > RipFreezeMax ||
            Time.time - ripGrabberSeenAt > RipGrabberGrace)
        {
            RipUnfreeze();

            return;
        }

        if (player == null || grabber == null)
            return;

        Transform hand = ripFrozenHand == 0
                                 ? grabber.leftHandTransform
                                 : grabber.rightHandTransform;

        if (hand == null)
            return;

        if (player.enabled)
            player.enabled = false;

        FreezeRb(player.GetComponent<Rigidbody>());
        FreezeRb(GorillaTagger.Instance != null ? GorillaTagger.Instance.rigidbody : null);

        Vector3 wanted = hand.position - ripHeadOffset + Vector3.down * 0.15f;

        if (!ripFollowInit)
        {
            ripFollowPos  = player.transform.position;
            ripFollowInit = true;
        }

        // Tight exponential follow: the remote hand jitters a little, the
        // held body should not shake along with it.
        float k = 1f - Mathf.Exp(-20f * Time.deltaTime);

        ripFollowPos       = Vector3.Lerp(ripFollowPos, wanted, k);
        player.transform.position = ripFollowPos;
    }

    private static void RipTickGrabber()
    {
        bool gripL, gripR;

        try
        {
            gripL = ControllerInputPoller.instance.leftGrab;
            gripR = ControllerInputPoller.instance.rightGrab;
        }
        catch
        {
            return;
        }

        bool freshL = gripL && !ripWasGripL;
        bool freshR = gripR && !ripWasGripR;

        ripWasGripL = gripL;
        ripWasGripR = gripR;

        if (ripHolding)
        {
            bool holdGrip   = ripHoldHand == 0 ? gripL : gripR;
            bool otherGrip  = ripHoldHand == 0 ? gripR : gripL;
            bool otherFresh = otherGrip && !ripOtherWasGrip;

            ripOtherWasGrip = otherGrip;

            // Let go and they drop back to the ground.
            if (!holdGrip)
            {
                SendRip(RipActionDrop, ripVictimActor, 0);
                ripHolding     = false;
                ripVictimActor = -1;

                return;
            }

            // The other hand closes ON them: tear - their arm if the hand
            // found one of theirs, otherwise straight down the middle.
            if (otherFresh)
            {
                VRRig     held   = GTConsole.GetVRRigFromActor(ripVictimActor);
                Transform other  = ripHoldHand == 0
                                            ? GorillaTagger.Instance.rightHandTransform
                                            : GorillaTagger.Instance.leftHandTransform;

                if (held != null && other != null &&
                    Vector3.Distance(other.position, RipCentre(held)) < RipGrabRange * 1.4f)
                {
                    float toLeft = held.leftHandTransform != null
                                           ? Vector3.Distance(other.position, held.leftHandTransform.position)
                                           : float.MaxValue;
                    float toRight = held.rightHandTransform != null
                                            ? Vector3.Distance(other.position, held.rightHandTransform.position)
                                            : float.MaxValue;

                    int action = toLeft < RipArmRange && toLeft <= toRight
                                         ? RipActionArmL
                                         : toRight < RipArmRange
                                                 ? RipActionArmR
                                                 : RipActionHalf;

                    RipFinish(held, action);
                }
            }

            return;
        }

        // Holding the practice dummy: same grip rules, no network at all.
        if (dummyHeld)
        {
            DummyHoldTick(gripL, gripR);

            return;
        }

        if (!freshL && !freshR)
            return;

        // A fresh grip near somebody starts the grab.
        GorillaTagger tagger = GorillaTagger.Instance;

        if (tagger == null)
            return;

        VRRig victim = null;
        float best   = RipGrabRange;
        int   hand   = 0;

        if (freshL && tagger.leftHandTransform != null)
            TryRipVictim(tagger.leftHandTransform.position, ref victim, ref best, ref hand, 0);

        if (freshR && tagger.rightHandTransform != null)
            TryRipVictim(tagger.rightHandTransform.position, ref victim, ref best, ref hand, 1);

        // No player in reach - see if it's the practice dummy instead.
        if (victim == null || victim.Creator == null)
        {
            TryGrabDummy(freshL, freshR, gripL, gripR, tagger);

            return;
        }

        ripHolding      = true;
        ripVictimActor  = victim.Creator.ActorNumber;
        ripHoldHand     = hand;
        // If the other hand is already squeezed it must open again before
        // a rip can start - grabbing with both hands at once must not tear.
        ripOtherWasGrip = hand == 0 ? gripR : gripL;

        SendRip(RipActionGrab, ripVictimActor, ripHoldHand);

        try
        {
            GorillaTagger.Instance.StartVibration(hand == 0, GorillaTagger.Instance.tagHapticStrength,
                    GorillaTagger.Instance.tagHapticDuration);
        }
        catch
        {
            // ignored - haptics are garnish
        }

        NotifiLib.SendNotification(
                "Grabbed " + victim.Creator.NickName + " - grip with your other hand on them to rip!");
    }

    private static void TryRipVictim(Vector3 at, ref VRRig victim, ref float best, ref int hand, int handIndex)
    {
        IReadOnlyList<VRRig> rigs = VRRigCache.ActiveRigs;

        for (int i = 0; i < rigs.Count; i++)
        {
            VRRig rig = rigs[i];

            if (rig == null || rig.isLocal || rig.Creator == null)
                continue;

            // Somebody is already holding this one.
            if (ripHeldBy.ContainsKey(rig.Creator.ActorNumber))
                continue;

            float d = float.MaxValue;

            if (rig.headMesh != null)
                d = Mathf.Min(d, Vector3.Distance(at, rig.headMesh.transform.position));

            if (rig.bodyTransform != null)
                d = Mathf.Min(d, Vector3.Distance(at, rig.bodyTransform.position));

            if (rig.leftHandTransform != null)
                d = Mathf.Min(d, Vector3.Distance(at, rig.leftHandTransform.position));

            if (rig.rightHandTransform != null)
                d = Mathf.Min(d, Vector3.Distance(at, rig.rightHandTransform.position));

            if (d < best)
            {
                best   = d;
                victim = rig;
                hand   = handIndex;
            }
        }
    }

    // The grabber side of the tear: the gore command, then the room-wide
    // drama (lightning, shake, headline) and the delayed kick.
    private static void RipFinish(VRRig victim, int action)
    {
        int    actor = ripVictimActor;
        string nick  = victim.Creator != null ? victim.Creator.NickName : "a player";
        string id    = victim.Creator != null ? victim.Creator.UserId : null;

        ripHolding     = false;
        ripVictimActor = -1;

        SendRip(action, actor, 0);

        Vector3 centrePos = RipCentre(victim);

        GTConsole.ExecuteCommand("strike", ReceiverGroup.All, centrePos);
        GTConsole.ExecuteCommand("strike", ReceiverGroup.All, centrePos + Random.insideUnitSphere * 1.5f);
        GTConsole.ExecuteCommand("shake", ReceiverGroup.All, 0.35f, 1.1f, false);
        GTConsole.ExecuteCommand("notify", ReceiverGroup.All, nick + " was ripped apart!");

        if (!string.IsNullOrEmpty(id))
            CoroutineManager.Instance.StartCoroutine(RipKick(id, 0.6f));
    }

    // The body is already gore on every screen; half a second later the
    // victim lands in the lobby. Admins refuse the kick - their freeze
    // has its own failsafe.
    private static IEnumerator RipKick(string userId, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (PhotonNetwork.InRoom)
            GTConsole.ExecuteCommand("silkick", ReceiverGroup.All, userId);
    }

    private static IEnumerator RipVictimRelease(float delay)
    {
        yield return new WaitForSeconds(delay);
        RipUnfreeze();
    }

    // Every screen that receives the tear: hide the real body, throw the
    // gore, thump the haptics. Nothing extra crosses the network - each
    // client builds its own pieces from the shared command.
    private static void RipApply(int victimActor, int action)
    {
        VRRig victim = GTConsole.GetVRRigFromActor(victimActor);

        if (victim == null)
            return;

        // The body is replaced by gore for a couple of seconds; the tick
        // re-asserts the hide because GT keeps re-enabling renderers.
        Renderer[] rends = victim.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer r in rends)
            r.enabled = false;

        ripHidden[victim] = (rends, Time.time + 2.5f);

        Vector3 centre = RipCentre(victim);
        Vector3 facing = victim.transform.forward;

        if (facing.sqrMagnitude < 0.001f)
            facing = Vector3.forward;

        // Where the body actually is, taken from the renderer bounds so
        // the chunks come out of a chest rather than out of the head.
        Renderer bodyRend = victim.bodyRenderer != null
                                    ? victim.bodyRenderer.GetComponentInChildren<Renderer>(true)
                                    : null;

        Bounds body = bodyRend != null
                              ? bodyRend.bounds
                              : new Bounds(centre - Vector3.up * 0.8f, new Vector3(0.5f, 1.7f, 0.4f));

        float height = body.size.y;
        Vector3 chest = new(body.center.x, body.min.y + height * 0.66f, body.center.z);
        Vector3 hips  = new(body.center.x, body.min.y + height * 0.30f, body.center.z);

        if (action == RipActionHalf)
        {
            // Two halves: the top cartwheels away, the legs drop.
            RipChunk(chest, new Vector3(body.size.x * 1.15f, height * 0.34f, body.size.z * 1.15f),
                    facing * 1.6f + Vector3.up * 3.8f + Random.insideUnitSphere * 1.3f);
            RipChunk(hips, new Vector3(body.size.x * 1.05f, height * 0.32f, body.size.z * 1.05f),
                    facing * 1.0f + Vector3.down * 1.2f + Random.insideUnitSphere * 1.0f);

            // Limbs and ribs torn off along with the halves - a much
            // messier tear than the two clean pieces it used to be.
            for (int i = 0; i < 7; i++)
            {
                Vector3 from = i % 2 == 0 ? chest : hips;

                RipChunk(from + Random.insideUnitSphere * 0.25f,
                        new Vector3(0.13f, Random.Range(0.14f, 0.34f), 0.13f),
                        Random.insideUnitSphere * 6.5f + Vector3.up * 3.2f,
                        i % 3 == 0 ? PrimitiveType.Sphere : PrimitiveType.Capsule);
            }
        }
        else
        {
            // One arm, torn out at the shoulder and spun away.
            Transform hand = action == RipActionArmL
                                     ? victim.leftHandTransform
                                     : victim.rightHandTransform;

            Vector3 shoulder = hand != null
                                       ? (chest + hand.position) * 0.5f
                                       : chest + (action == RipActionArmL ? Vector3.left : Vector3.right) * 0.25f;

            Vector3 fling = hand != null
                                    ? (hand.position - chest).normalized * 5f
                                    : facing * 3f;

            RipChunk(shoulder, new Vector3(0.14f, height * 0.17f, 0.14f),
                    fling + Vector3.up * 1.5f + Random.insideUnitSphere * 1.5f);

            // Shreds of shoulder and hand meat scatter with it.
            for (int i = 0; i < 5; i++)
                RipChunk(shoulder + Random.insideUnitSphere * 0.18f,
                        Vector3.one * Random.Range(0.07f, 0.17f),
                        fling * Random.Range(0.4f, 1.1f) + Random.insideUnitSphere * 5f + Vector3.up * 2f,
                        i % 2 == 0 ? PrimitiveType.Sphere : PrimitiveType.Capsule);
        }

        RipBurst(chest);
        RipBurst(hips);
        CoroutineManager.Instance.StartCoroutine(RipGoreRain(centre, facing));

        try
        {
            GorillaTagger.Instance.StartVibration(true, GorillaTagger.Instance.tagHapticStrength, 1f);
            GorillaTagger.Instance.StartVibration(false, GorillaTagger.Instance.tagHapticStrength, 1f);
        }
        catch
        {
            // ignored
        }

        // The victim's own screen unfreezes shortly after the tear: the
        // kick either takes them away or (for an admin) fails, and in
        // both cases they must not stay suspended.
        if (victimActor == PhotonNetwork.LocalPlayer.ActorNumber)
            CoroutineManager.Instance.StartCoroutine(RipVictimRelease(1.5f));
    }

    private static Vector3 RipCentre(VRRig rig)
    {
        if (rig.headMesh != null)
            return rig.headMesh.transform.position;

        if (rig.bodyTransform != null)
            return rig.bodyTransform.position;

        return rig.transform.position;
    }

    private static void RipChunk(Vector3 position, Vector3 scale, Vector3 velocity, PrimitiveType shape = PrimitiveType.Capsule)
    {
        GameObject chunk = GameObject.CreatePrimitive(shape);
        chunk.name                = "RIFTIX_RipChunk";
        chunk.transform.position  = position;
        chunk.transform.localScale = scale;
        chunk.transform.rotation  = Random.rotation;

        Renderer rend = chunk.GetComponent<Renderer>();

        if (rend != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard")
                            ?? Shader.Find("Sprites/Default");

            Material flesh = new(shader);

            // Raw meat: mostly dark red, the odd brighter sinew chunk.
            Color meat = Random.value > 0.75f
                                 ? new Color(0.65f, 0.10f, 0.10f)
                                 : new Color(Random.Range(0.30f, 0.50f), 0.03f, 0.03f);

            if (flesh.HasProperty("_BaseColor"))
                flesh.SetColor("_BaseColor", meat);

            if (flesh.HasProperty("_Color"))
                flesh.SetColor("_Color", meat);

            rend.material = flesh;
        }

        Rigidbody rb = chunk.AddComponent<Rigidbody>();
        rb.mass            = 12f;
        rb.linearVelocity  = velocity;
        rb.angularVelocity = Random.insideUnitSphere * 22f;

        Object.Destroy(chunk, 5f);
    }

    // The second wave: gibs rain out of the tear for about a second after
    // the body comes apart, so the mess keeps coming down after the big
    // pieces have landed.
    private static IEnumerator RipGoreRain(Vector3 centre, Vector3 facing)
    {
        for (int wave = 0; wave < 5; wave++)
        {
            yield return new WaitForSeconds(0.1f);

            if (centre == Vector3.zero)
                yield break;

            for (int i = 0; i < 6; i++)
                RipChunk(centre + Random.insideUnitSphere * 0.3f,
                        Vector3.one * Random.Range(0.05f, 0.15f),
                        facing * Random.Range(0.5f, 3f) + Random.insideUnitSphere * 5f + Vector3.up * 2.5f,
                        i % 2 == 0 ? PrimitiveType.Sphere : PrimitiveType.Capsule);
        }
    }

    private static void RipBurst(Vector3 position)
    {
        // Three overlapping bursts: a fast red mist, a heavier spray of
        // dark blood, and a lingering cloud hanging where the body was.
        SpawnBloodMist(position, 140, new Color(0.80f, 0.02f, 0.02f, 0.9f),
                new ParticleSystem.MinMaxCurve(2f, 9f), new ParticleSystem.MinMaxCurve(0.05f, 0.35f), 0.6f);
        SpawnBloodMist(position, 90, new Color(0.35f, 0.01f, 0.01f, 0.95f),
                new ParticleSystem.MinMaxCurve(1f, 5f), new ParticleSystem.MinMaxCurve(0.10f, 0.45f), 1.6f);
        SpawnBloodMist(position, 60, new Color(0.55f, 0.03f, 0.03f, 0.45f),
                new ParticleSystem.MinMaxCurve(0.2f, 1.4f), new ParticleSystem.MinMaxCurve(0.30f, 0.80f), 3.2f);
    }

    private static void SpawnBloodMist(Vector3 position, int count, Color colour,
            ParticleSystem.MinMaxCurve speed, ParticleSystem.MinMaxCurve size, float lifetime)
    {
        GameObject burst = new("RIFTIX_RipBlood");
        burst.transform.position = position + Random.insideUnitSphere * 0.15f;

        ParticleSystem particles = burst.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.loop            = false;
        main.playOnAwake     = true;
        main.duration        = 0.6f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(lifetime * 0.5f, lifetime);
        main.startSpeed      = speed;
        main.startSize       = size;
        main.startColor      = colour;
        main.gravityModifier = 1.4f;
        main.maxParticles    = count;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts([new ParticleSystem.Burst(0f, (short)count)]);

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType       = ParticleSystemShapeType.Sphere;
        shape.radius          = 0.3f;
        shape.radiusThickness = 1f;

        Object.Destroy(burst, lifetime + 2f);
    }

    // Re-assert the hide every frame until the restore time, then put the
    // body back - or forget it if the rig is already gone with its owner.
    private static void RipTickHidden()
    {
        if (ripHidden.Count == 0)
            return;

        List<(VRRig rig, Renderer[] rends)> done = null;

        foreach (KeyValuePair<VRRig, (Renderer[] rends, float until)> entry in ripHidden)
        {
            if (entry.Key == null || Time.time >= entry.Value.until)
            {
                (done ??= new()).Add((entry.Key, entry.Value.rends));

                continue;
            }

            foreach (Renderer r in entry.Value.rends)
            {
                if (r != null && r.enabled)
                    r.enabled = false;
            }
        }

        if (done == null)
            return;

        foreach ((VRRig rig, Renderer[] rends) in done)
        {
            if (rig != null)
                foreach (Renderer r in rends)
                {
                    if (r != null)
                        r.enabled = true;
                }

            ripHidden.Remove(rig);
        }
    }

    // Room is changing: every trace of an active grab dies with it.
    private static void RipperReset()
    {
        ripHolding     = false;
        ripVictimActor = -1;
        ripWasGripL    = false;
        ripWasGripR    = false;

        ripHeldBy.Clear();

        foreach (KeyValuePair<VRRig, (Renderer[] rends, float until)> entry in ripHidden)
        {
            if (entry.Key == null)
                continue;

            foreach (Renderer r in entry.Value.rends)
            {
                if (r != null)
                    r.enabled = true;
            }
        }

        ripHidden.Clear();

        RipUnfreeze();
    }

    // Called from Console.Update every frame.
    public static void RipperTick()
    {
        try
        {
            if (!PhotonNetwork.InRoom)
                return;

            RipTickHidden();

            DummyTick();

            // Holds whose grabber left the room go stale.
            if (ripHeldBy.Count > 0)
            {
                List<int> stale = null;

                foreach (KeyValuePair<int, int> hold in ripHeldBy)
                {
                    bool present = false;

                    foreach (Player p in PhotonNetwork.PlayerList)
                    {
                        if (p.ActorNumber == hold.Value)
                        {
                            present = true;
                            break;
                        }
                    }

                    if (!present)
                        (stale ??= new List<int>()).Add(hold.Key);
                }

                if (stale != null)
                    foreach (int key in stale)
                        ripHeldBy.Remove(key);
            }

            // A held player does not grab - they are the one being held.
            if (ripFrozen)
            {
                RipTickFrozen();

                return;
            }

            ButtonInfo ripper = Buttons.GetIndex("Ripper");

            if (ripper == null || !ripper.enabled)
                return;

            RipTickGrabber();
        }
        catch
        {
            // never let an input hiccup break the frame
        }
    }

    // ---------------------------------------------------------------------
    // Practice dummy: a local grey mannequin for the Ripper to grab. Only
    // this client ever sees it - no command goes out - so it works as a
    // solo target anywhere. Grip near it (Ripper on) to grab, hold it in
    // the air, then grip with your other hand on it to tear it: arm off or
    // straight down the middle, same rules as a player. It reassembles
    // three seconds later.
    // ---------------------------------------------------------------------

    private const float DummyHeadHeight   = 1.45f;
    private const float DummyRespawnDelay = 3f;

    private static GameObject   dummyRoot;
    private static Renderer[]   dummyRends;
    private static Renderer     dummyArmL;
    private static Renderer     dummyArmR;
    private static Material     dummyMaterial;
    private static bool         dummyRipped;
    private static float        dummyRipUntil;
    private static bool         dummyHeld;
    private static int          dummyHand;
    private static bool         dummyOtherWasGrip;
    private static bool         dummyFollowInit;
    private static Vector3      dummyFollowPos;

    public static string DummyDisplay()
    {
        ButtonInfo button = Buttons.GetIndex("Dummy");

        if (button == null || !button.enabled || dummyRoot == null)
            return "Dummy: <color=red>OFF</color>";

        if (dummyRipped)
            return "Dummy: <color=green>ON</color> <color=yellow>- in pieces!</color>";

        return dummyHeld
                   ? "Dummy: <color=green>ON</color> <color=yellow>- held!</color>"
                   : "Dummy: <color=green>ON</color>";
    }

    public static void SpawnDummy()
    {
        if (!CanSpawn(out string reason))
        {
            SetSwitchOff("Dummy");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification(reason);

            return;
        }

        RemoveDummy();
        BuildDummy();

        if (dummyRoot == null)
        {
            SetSwitchOff("Dummy");
            NotifiLib.SendNotification("Could not place the dummy.");

            return;
        }

        NotifiLib.SendNotification(
                "Practice dummy spawned - turn on the Ripper and grip near it to grab it.");
    }

    public static void RemoveDummy()
    {
        dummyHeld      = false;
        dummyRipped    = false;
        dummyFollowInit = false;

        if (dummyRoot != null)
            Object.Destroy(dummyRoot);

        dummyRoot   = null;
        dummyRends  = null;
        dummyArmL   = null;
        dummyArmR   = null;

        if (dummyMaterial != null)
            Object.Destroy(dummyMaterial);

        dummyMaterial = null;
    }

    private static void BuildDummy()
    {
        GorillaTagger tagger = GorillaTagger.Instance;

        if (tagger == null || tagger.bodyCollider == null)
            return;

        Vector3 origin = tagger.bodyCollider.transform.position;

        // A random spot 2-4 m ahead, within +-75 degrees of the way you face.
        Vector3 fwd = Camera.main != null ? Camera.main.transform.forward
                                          : tagger.bodyCollider.transform.forward;

        fwd.y = 0f;

        if (fwd.sqrMagnitude < 0.001f)
            fwd = Vector3.forward;

        fwd.Normalize();

        Vector3 dir    = Quaternion.AngleAxis(Random.Range(-75f, 75f), Vector3.up) * fwd;
        Vector3 spawn  = origin + dir * Random.Range(2f, 4f);

        spawn.y = origin.y - 1.15f;

        dummyRoot = new GameObject("RIFTIX_Dummy");
        dummyRoot.transform.position = spawn;

        // Turn it to face you.
        Vector3 toPlayer = origin - spawn;
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude > 0.001f)
            dummyRoot.transform.rotation = Quaternion.LookRotation(toPlayer);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("Standard")
                        ?? Shader.Find("Sprites/Default");

        dummyMaterial = new Material(shader);

        Color grey = new(0.55f, 0.55f, 0.58f);

        if (dummyMaterial.HasProperty("_BaseColor"))
            dummyMaterial.SetColor("_BaseColor", grey);

        if (dummyMaterial.HasProperty("_Color"))
            dummyMaterial.SetColor("_Color", grey);

        List<Renderer> parts = new();

        parts.Add(AddDummyPart(PrimitiveType.Capsule, new Vector3(0f, 0.95f, 0f),
                new Vector3(0.34f, 0.26f, 0.24f), Vector3.zero));

        parts.Add(AddDummyPart(PrimitiveType.Cube, new Vector3(0f, 1.27f, 0f),
                new Vector3(0.1f, 0.14f, 0.1f), Vector3.zero));

        parts.Add(AddDummyPart(PrimitiveType.Sphere, new Vector3(0f, DummyHeadHeight, 0f),
                new Vector3(0.26f, 0.26f, 0.26f), Vector3.zero));

        dummyArmL = AddDummyPart(PrimitiveType.Capsule, new Vector3(-0.33f, 0.95f, 0f),
                new Vector3(0.09f, 0.26f, 0.09f), new Vector3(0f, 0f, -25f));

        dummyArmR = AddDummyPart(PrimitiveType.Capsule, new Vector3(0.33f, 0.95f, 0f),
                new Vector3(0.09f, 0.26f, 0.09f), new Vector3(0f, 0f, 25f));

        parts.Add(dummyArmL);
        parts.Add(dummyArmR);

        parts.Add(AddDummyPart(PrimitiveType.Capsule, new Vector3(-0.12f, 0.45f, 0f),
                new Vector3(0.14f, 0.45f, 0.14f), Vector3.zero));

        parts.Add(AddDummyPart(PrimitiveType.Capsule, new Vector3(0.12f, 0.45f, 0f),
                new Vector3(0.14f, 0.45f, 0.14f), Vector3.zero));

        dummyRends = parts.ToArray();
        dummyRipped = false;
    }

    private static Renderer AddDummyPart(PrimitiveType type, Vector3 localPos, Vector3 scale,
            Vector3 localEuler)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name                 = "RIFTIX_DummyPart";
        part.transform.SetParent(dummyRoot.transform, false);
        part.transform.localPosition  = localPos;
        part.transform.localEulerAngles = localEuler;
        part.transform.localScale = scale;

        // The dummy is a target, not an obstacle - no colliders.
        Collider col = part.GetComponent<Collider>();

        if (col != null)
            Object.Destroy(col);

        Renderer rend = part.GetComponent<Renderer>();

        if (rend != null && dummyMaterial != null)
            rend.sharedMaterial = dummyMaterial;

        return rend;
    }

    private static Vector3 DummyCentre() =>
            dummyRoot != null
                ? dummyRoot.transform.position + Vector3.up * 0.95f
                : Vector3.zero;

    // Called from RipTickGrabber while the dummy is in hand: same grip
    // rules as holding a player - open the holding hand to drop it, close
    // the other hand ON it to tear it.
    private static void DummyHoldTick(bool gripL, bool gripR)
    {
        if (dummyRoot == null)
        {
            dummyHeld = false;

            return;
        }

        GorillaTagger tagger = GorillaTagger.Instance;

        if (tagger == null)
            return;

        bool holdGrip   = dummyHand == 0 ? gripL : gripR;
        bool otherGrip  = dummyHand == 0 ? gripR : gripL;
        bool otherFresh = otherGrip && !dummyOtherWasGrip;

        dummyOtherWasGrip = otherGrip;

        // Let go: it hangs where you dropped it until the next grab.
        if (!holdGrip)
        {
            dummyHeld = false;

            return;
        }

        // Follow the holding hand, hanging just under it by the head.
        Transform hand = dummyHand == 0 ? tagger.leftHandTransform : tagger.rightHandTransform;

        if (hand != null)
        {
            Vector3 target = hand.position - Vector3.up * (DummyHeadHeight + 0.15f);

            if (!dummyFollowInit)
            {
                dummyFollowPos = target;
                dummyFollowInit = true;
            }
            else
            {
                float k = 1f - Mathf.Exp(-20f * Time.deltaTime);

                dummyFollowPos = Vector3.Lerp(dummyFollowPos, target, k);
            }

            dummyRoot.transform.position = dummyFollowPos;
        }

        if (otherFresh)
        {
            Transform other = dummyHand == 0
                                      ? tagger.rightHandTransform
                                      : tagger.leftHandTransform;

            if (other != null)
                TearDummy(DummyTearAction(other.position));
        }
    }

    // Arm if the tearing hand found one of its tips, down the middle else.
    private static int DummyTearAction(Vector3 at)
    {
        if (dummyRoot == null)
            return RipActionHalf;

        float toLeft  = Vector3.Distance(at, dummyRoot.transform.TransformPoint(-0.45f, 0.85f, 0f));
        float toRight = Vector3.Distance(at, dummyRoot.transform.TransformPoint(0.45f, 0.85f, 0f));

        return toLeft < RipArmRange && toLeft <= toRight
                   ? RipActionArmL
                   : toRight < RipArmRange
                           ? RipActionArmR
                           : RipActionHalf;
    }

    private static void TearDummy(int action)
    {
        if (dummyRoot == null || dummyRipped)
            return;

        Renderer[] targets;

        if (action == RipActionArmL && dummyArmL != null)
            targets = new[] { dummyArmL };
        else if (action == RipActionArmR && dummyArmR != null)
            targets = new[] { dummyArmR };
        else
        {
            action   = RipActionHalf;
            targets  = dummyRends;
        }

        Vector3 centre = DummyCentre();

        // Bounds first: they read zero the moment the parts are hidden.
        foreach (Renderer r in targets)
        {
            if (r == null)
                continue;

            Bounds b     = r.bounds;
            Vector3 fling = (b.center - centre).normalized * 4f + Vector3.up * 2f;

            RipChunk(b.center, b.size * 0.8f, fling);
        }

        if (action == RipActionHalf)
            RipBurst(centre);

        foreach (Renderer r in targets)
        {
            if (r != null)
                r.enabled = false;
        }

        dummyRipped   = true;
        dummyRipUntil = Time.time + DummyRespawnDelay;

        // Half leaves nothing to hold; an arm-off body stays in your grip.
        if (action == RipActionHalf)
            dummyHeld = false;

        try
        {
            GorillaTagger.Instance.StartVibration(dummyHand != 0,
                    GorillaTagger.Instance.tagHapticStrength,
                    GorillaTagger.Instance.tagHapticDuration);
        }
        catch
        {
            // ignored - haptics are garnish
        }

        NotifiLib.SendNotification(
                action == RipActionHalf
                    ? "Dummy torn in half! It reassembles in 3 seconds."
                    : "Dummy's arm torn off! It reassembles in 3 seconds.");
    }

    // No real victim in reach - see if it's the practice dummy instead.
    private static void TryGrabDummy(bool freshL, bool freshR, bool gripL, bool gripR,
            GorillaTagger tagger)
    {
        if (dummyRoot == null || dummyRipped || dummyHeld)
            return;

        if (!freshL && !freshR)
            return;

        Vector3 centre = DummyCentre();
        float   range  = RipGrabRange * 1.5f;

        float dL = freshL && tagger.leftHandTransform != null
                       ? Vector3.Distance(tagger.leftHandTransform.position, centre)
                       : float.MaxValue;

        float dR = freshR && tagger.rightHandTransform != null
                       ? Vector3.Distance(tagger.rightHandTransform.position, centre)
                       : float.MaxValue;

        int hand;

        if (dL < range && dL <= dR)
            hand = 0;
        else if (dR < range)
            hand = 1;
        else
            return;

        dummyHeld         = true;
        dummyHand         = hand;
        // If the other hand is already squeezed it must open again before
        // a rip can start - grabbing with both hands at once must not tear.
        dummyOtherWasGrip = hand == 0 ? gripR : gripL;
        dummyFollowInit   = false;

        try
        {
            GorillaTagger.Instance.StartVibration(hand == 0, GorillaTagger.Instance.tagHapticStrength,
                    GorillaTagger.Instance.tagHapticDuration);
        }
        catch
        {
            // ignored - haptics are garnish
        }

        NotifiLib.SendNotification(
                "Grabbed the dummy - hold it up, then grip with your other hand on it to rip!");
    }

    // Called from RipperTick: put the pieces back together when the timer
    // runs out.
    private static void DummyTick()
    {
        if (!dummyRipped || dummyRoot == null)
            return;

        if (Time.time < dummyRipUntil)
            return;

        foreach (Renderer r in dummyRends)
        {
            if (r != null)
                r.enabled = true;
        }

        dummyRipped = false;

        NotifiLib.SendNotification("The dummy reassembles.");
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

    // ---------------------------------------------------------------------
    // Jurassic Logo: the Jurassic World logo from this menu's asset bundle
    // template project (Logo_joind), fitted to 1.4 m by the bundle build
    // itself and anchored to your left hand like the hammer, with the song
    // baked in as a looping AudioSource that plays on awake. The rig
    // inside the asset carries an Animator that plays its own entrance
    // (scale pop with a -140 degree spin-in) and then floats and turns
    // forever - every client runs that Animator locally from its own
    // instantiate, so no animation frames ever cross the wire.
    // ---------------------------------------------------------------------

    private const string LogoBundle = "logo_joind";
    private const string LogoAsset  = "JurassicLogo";
    private const string LogoSound  = "Song";
    private const string LogoClip   = "jp_logo_song";

    // BindObject's anchor slots: 0 head, 1 left hand, 2 right hand, 3 body.
    // The logo hangs off the left hand like the hammer does.
    private const int LogoAnchor = 1;

    // The bundle bakes the logo 1.4 m wide. At arm's length that is far
    // wider than two eyes can fuse: each eye lands on a different part of
    // it, so it only reads with one eye shut and doubles up with two. The
    // root scale cuts it to 0.5 m, and the pose lifts it clear of the panel
    // and out past the hand to roughly 0.7 m from the face, where both eyes
    // land on the same picture. Axes follow the hand rig the menu uses:
    // -X runs away from your face, +Z is up, +Y is left.
    private static readonly Vector3 LogoLocalPose = new(-0.3f, 0f, 0.45f);
    private static readonly Vector3 LogoRootScale = new(0.357f, 0.357f, 0.357f);

    private static int logoAssetId = -1;

    public static string JpLogoDisplay()
    {
        ButtonInfo button = Buttons.GetIndex("Jurassic Logo");

        return
                $"Jurassic Logo: {(button != null && button.enabled
                                        ? "<color=green>ON</color>"
                                        : "<color=red>OFF</color>")}";
    }

    public static void SpawnJpLogo()
    {
        if (!CanSpawn(out string reason))
        {
            // Saved switch states replay at startup before a room exists;
            // a real press gets the refusal reason.
            SetSwitchOff("Jurassic Logo");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification(reason);

            return;
        }

        if (logoAssetId >= 0)
            return;

        logoAssetId = GTConsole.GetFreeAssetID();

        // Spawn first; every command after it waits for the asset to exist
        // (Console gives each one up to ten seconds). The anchor lands the
        // logo on your left hand - BindObject's slot 1 - and the pose then
        // parks it above and past the hand at a size the eyes can fuse; the
        // baked animator floats and turns it around that point. The song's
        // AudioSource plays on awake inside the bundle, so every client
        // hears it the moment their copy exists, with the playsound command
        // as a backstop for anyone the first pass missed.
        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, LogoBundle, LogoAsset, logoAssetId);
        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, logoAssetId);
        GTConsole.ExecuteCommand("asset-setanchor", ReceiverGroup.All, logoAssetId, LogoAnchor,
                PhotonNetwork.LocalPlayer.ActorNumber);
        GTConsole.ExecuteCommand("asset-setlocalposition", ReceiverGroup.All, logoAssetId, LogoLocalPose);
        GTConsole.ExecuteCommand("asset-setlocalrotation", ReceiverGroup.All, logoAssetId,
                Quaternion.identity);
        GTConsole.ExecuteCommand("asset-setscale", ReceiverGroup.All, logoAssetId, LogoRootScale);
        GTConsole.ExecuteCommand("asset-playsound", ReceiverGroup.All, logoAssetId, LogoSound, LogoClip);

        NotifiLib.SendNotification("Jurassic logo on your hand.");

        CoroutineManager.Instance.StartCoroutine(ResendJpLogo());
    }

    public static void RemoveJpLogo()
    {
        if (Alive(logoAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, logoAssetId);

        logoAssetId = -1;
    }

    // Anyone still downloading the bundle when the spawn went out would drop
    // the follow-ups (Console waits ten seconds per asset), so re-send
    // everything once everyone has had twelve seconds to catch up. The
    // anchor goes first: BindObject re-parents, so the pose and scale must
    // land after it rather than be re-parented away (same order as the
    // hammer).
    // PlayAudioSource skips a source that is already playing, so the song
    // does not restart on this pass.
    private static IEnumerator ResendJpLogo()
    {
        yield return new WaitForSeconds(12f);

        if (!Alive(logoAssetId))
            yield break;

        int id = logoAssetId;

        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, id);
        GTConsole.ExecuteCommand("asset-setanchor", ReceiverGroup.All, id, LogoAnchor,
                PhotonNetwork.LocalPlayer.ActorNumber);
        GTConsole.ExecuteCommand("asset-setlocalposition", ReceiverGroup.All, id, LogoLocalPose);
        GTConsole.ExecuteCommand("asset-setlocalrotation", ReceiverGroup.All, id, Quaternion.identity);
        GTConsole.ExecuteCommand("asset-setscale", ReceiverGroup.All, id, LogoRootScale);
        GTConsole.ExecuteCommand("asset-playsound", ReceiverGroup.All, id, LogoSound, LogoClip);
    }

    // ---------------------------------------------------------------------
    // Cats: the two lowpoly cats from the asset template project - one
    // spinning on your left hand, one spinning on your head - with the
    // song baked into the head cat's looping AudioSource. Built by
    // Assets/Build Cats Bundle in the 2022.3.2f1 bundle project and
    // committed to this repo under bundles/riftix_cats (ServerData has no
    // room for it, so Console falls back to RiftixAssetsURL). The spin is
    // a baked yaw Animator on each cat's Spin child, so every client turns
    // its own copy from its own instantiate with no frames on the wire,
    // and the pose is fitted in world space (the trick FitCowboyHat uses
    // for the hat) so placement never depends on which way an anchor's
    // local axes happen to point.
    // ---------------------------------------------------------------------

    private const string CatsBundle   = "riftix_cats";
    private const string CatHandAsset = "CatHandRiftix";
    private const string CatHeadAsset = "CatHeadRiftix";
    private const string CatsSound    = "Song";
    private const string CatsClip     = "catsong";

    // BindObject's anchor slots: 0 head, 1 left hand, 2 right hand, 3 body.
    private const int CatHandAnchor = 1;
    private const int CatHeadAnchor = 0;

    // The cats used to sit at full bundle size right by your face: 0.26 m
    // of cat at menu-reading distance is wider than two eyes can fuse, so
    // it only read with one eye shut and doubled up with two - the exact
    // problem the Jurassic logo had. Same cure: shrink both cats and push
    // the hand cat out past the hand (horizontally away from your head),
    // to roughly 0.6 m from the face where both eyes land on the same
    // cat. The scale rides SendCat and the resend; the push is baked into
    // the world-space fit, so it survives re-anchoring.
    private const float CatHandScale = 0.7f;
    private const float CatHeadScale = 0.65f;
    private const float CatHandPush  = 0.22f;

    private static int catHandAssetId = -1;
    private static int catHeadAssetId = -1;

    public static string CatsDisplay()
    {
        ButtonInfo button = Buttons.GetIndex("Cats");

        return
                $"Cats: {(button != null && button.enabled
                                  ? "<color=green>ON</color>"
                                  : "<color=red>OFF</color>")}";
    }

    public static void SpawnCats()
    {
        if (!CanSpawn(out string reason))
        {
            // Saved switch states replay at startup before a room exists;
            // a real press gets the refusal reason.
            SetSwitchOff("Cats");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification(reason);

            return;
        }

        if (catHandAssetId >= 0)
            return;

        catHandAssetId = GTConsole.GetFreeAssetID();
        catHeadAssetId = GTConsole.GetFreeAssetID();

        // Spawn first; every command after it waits for the asset to exist
        // (Console gives each one up to ten seconds). Hand cat on your left
        // hand, head cat on your crown; colliders out (hands must not climb
        // their own cats). The zero pose is only a placeholder - FitCats
        // lands both in world space once they exist. The head cat carries
        // the song: baked AudioSource playing on awake, with asset-playsound
        // as the backstop for anyone the first pass missed.
        //
        // Both cats share one bundle, so they go out in turn: firing the two
        // asset-spawns at once makes Console download the same bytes twice
        // and LoadFromMemory the second copy is rejected ("another
        // AssetBundle with the same files is already loaded"), which can
        // drop a piece on the floor of the race. The head cat starts only
        // once the hand cat has instantiated - by then the bundle sits in
        // the pool and the second spawn reuses it.
        CoroutineManager.Instance.StartCoroutine(SpawnCatsPieces());

        NotifiLib.SendNotification("Cats on.");

        CoroutineManager.Instance.StartCoroutine(ResendCats());
        CoroutineManager.Instance.StartCoroutine(FitCats());
    }

    // Hand cat first, then the head cat (and its song) the moment the first
    // has come up. Ten seconds is Console's own wait: if the hand never
    // appears the head still gets its turn instead of hanging behind it.
    private static IEnumerator SpawnCatsPieces()
    {
        SendCat(CatHandAsset, catHandAssetId, CatHandAnchor, CatHandScale);

        float giveUp = Time.time + 10f;

        while (!Alive(catHandAssetId) && Time.time < giveUp)
            yield return null;

        if (!Alive(catHandAssetId))
        {
            // The bundle never arrived (host unreachable, or the file not
            // published yet). Clear the ids and flip the switch so the next
            // press is a clean retry: otherwise the allocated id makes
            // SpawnCats return early and the button sits ON doing nothing.
            catHandAssetId = -1;
            catHeadAssetId = -1;
            SetSwitchOff("Cats");

            NotifiLib.SendNotification("Cats failed to download - press again.");

            yield break;
        }

        SendCat(CatHeadAsset, catHeadAssetId, CatHeadAnchor, CatHeadScale);
        GTConsole.ExecuteCommand("asset-playsound", ReceiverGroup.All, catHeadAssetId, CatsSound,
                CatsClip);
    }

    // The full spawn/anchor/pose run for one cat: spawn, colliders out,
    // anchor, then the placeholder zero pose - anchor before pose because
    // BindObject re-parents and would carry the pose away with it. The
    // root scale goes out last so the fit's lift (measured in the model's
    // own units) still lands the shrunken cat on palm or crown.
    private static void SendCat(string assetName, int assetId, int anchorSlot, float scale)
    {
        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, CatsBundle, assetName, assetId);
        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, assetId);
        GTConsole.ExecuteCommand("asset-setanchor", ReceiverGroup.All, assetId, anchorSlot,
                PhotonNetwork.LocalPlayer.ActorNumber);
        GTConsole.ExecuteCommand("asset-setlocalposition", ReceiverGroup.All, assetId, Vector3.zero);
        GTConsole.ExecuteCommand("asset-setlocalrotation", ReceiverGroup.All, assetId,
                Quaternion.identity);
        GTConsole.ExecuteCommand("asset-setscale", ReceiverGroup.All, assetId,
                Vector3.one * scale);
    }

    // Where each cat sits, in world space: the hand cat a little over the
    // palm (its origin is its own centre, so the lift puts its belly on
    // the hand), the head cat centred on the crown exactly the way the
    // cowboy hat is fitted - both turned to face where you face. The local
    // values are derived from the anchor's own transform, so the pose
    // lands the same whichever way the anchor's axes point.
    private static IEnumerator FitCats()
    {
        // Each cat is fitted the moment its own copy exists: one piece
        // failing to download must not leave the other sitting at its
        // anchor's origin - the head anchor would park a cat inside your
        // head, where you cannot see it at all.
        yield return FitOneCatWhenReady(catHandAssetId, true);
        yield return FitOneCatWhenReady(catHeadAssetId, false);
    }

    // Wait for one cat to exist, then place it. onHand picks the palm lift
    // (hand cat) or the crown fit (head cat).
    private static IEnumerator FitOneCatWhenReady(int assetId, bool onHand)
    {
        if (assetId < 0)
            yield break;

        float giveUp = Time.time + 15f;

        while (!Alive(assetId) && Time.time < giveUp)
            yield return null;

        if (!Alive(assetId))
            yield break;

        // Let the anchor commands land first: the pose commands below go
        // out after them and Console runs commands in order.
        yield return null;
        yield return null;

        VRRig localRig = VRRig.LocalRig;

        if (localRig == null || localRig.headMesh == null ||
            localRig.leftHandTransform == null || localRig.leftHandTransform.parent == null)
            yield break;

        Transform head = localRig.headMesh.transform;
        Transform hand = localRig.leftHandTransform.parent;

        // Top of the head's renderer bounds, the way the hat finds it.
        Renderer headRenderer = localRig.headMesh.GetComponentInChildren<Renderer>();
        float    headTop      = headRenderer != null
                                        ? headRenderer.bounds.max.y
                                        : head.position.y + 0.12f;

        float yaw = head.eulerAngles.y;

        if (onHand)
        {
            // Out past the hand, horizontally away from your head: the
            // cat lands around 0.6 m from the face where both eyes fuse
            // it (the fit is world-space, so the push follows whichever
            // way you face when it runs). The lift is scaled too -
            // LocalHeight reads the model's own units, and the cat now
            // ships at CatHandScale of those.
            Vector3 away = hand.position - head.position;
            away.y = 0f;

            if (away.sqrMagnitude > 0.0001f)
                away = away.normalized * CatHandPush;
            else
                away = Vector3.zero;

            FitOneCat(assetId, hand,
                    hand.position +
                    Vector3.up * (LocalHeight(assetId) * CatHandScale * 0.425f) + away,
                    Quaternion.Euler(0f, yaw, 0f));
        }
        else
        {
            // The head cat keeps the cowboy-hat crown fit, scaled the
            // same way so it still nests on the crown rather than
            // hovering over it.
            FitOneCat(assetId, head,
                    new Vector3(head.position.x,
                            headTop + LocalHeight(assetId) * CatHeadScale * 0.375f,
                            head.position.z),
                    Quaternion.Euler(0f, yaw, 0f));
        }
    }

    // Send one cat's pose against an anchor transform: the local position
    // is the world point re-expressed in the anchor's space and the local
    // rotation is whatever takes the anchor's frame to the wanted world
    // facing - both immune to how the anchor happens to be oriented.
    private static void FitOneCat(int assetId, Transform anchor, Vector3 worldCentre,
            Quaternion worldRotation)
    {
        if (!Alive(assetId))
            return;

        GTConsole.ExecuteCommand("asset-setlocalposition", ReceiverGroup.All, assetId,
                anchor.InverseTransformPoint(worldCentre));
        GTConsole.ExecuteCommand("asset-setlocalrotation", ReceiverGroup.All, assetId,
                Quaternion.Inverse(anchor.rotation) * worldRotation);
    }

    // The asset's own height along its local up axis: a world-space AABB
    // would lie while the anchor is tilted (the hand hangs at spawn), and
    // the lift needs the true half-height either way.
    private static float LocalHeight(int assetId)
    {
        if (!Alive(assetId))
            return 0.14f;

        Transform t = GTConsole.ConsoleAssets[assetId].AssetObject.transform;

        bool  has = false;
        float min = 0f;
        float max = 0f;

        foreach (Renderer renderer in t.GetComponentsInChildren<Renderer>(true))
        {
            Bounds b = renderer.bounds;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + new Vector3((i & 1) == 0 ? -b.extents.x : b.extents.x,
                                                        (i & 2) == 0 ? -b.extents.y : b.extents.y,
                                                        (i & 4) == 0 ? -b.extents.z : b.extents.z);

                float y = t.InverseTransformPoint(corner).y;

                if (!has)
                {
                    min = max = y;
                    has       = true;
                }
                else
                {
                    min = Mathf.Min(min, y);
                    max = Mathf.Max(max, y);
                }
            }
        }

        return has ? max - min : 0.14f;
    }

    // Anyone still downloading the bundle when the spawn went out would drop
    // the follow-ups (Console waits ten seconds per asset), so re-send both
    // cats once everyone has had twelve seconds to catch up - the fit runs
    // again after the re-anchor (the pose must land after BindObject has
    // re-parented it), and the song resend skips a source already playing.
    private static IEnumerator ResendCats()
    {
        int handId = catHandAssetId;
        int headId = catHeadAssetId;

        yield return new WaitForSeconds(12f);

        if (handId == catHandAssetId && Alive(handId))
        {
            GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, handId);
            GTConsole.ExecuteCommand("asset-setanchor", ReceiverGroup.All, handId, CatHandAnchor,
                    PhotonNetwork.LocalPlayer.ActorNumber);
            GTConsole.ExecuteCommand("asset-setscale", ReceiverGroup.All, handId,
                    Vector3.one * CatHandScale);
        }

        if (headId == catHeadAssetId && Alive(headId))
        {
            GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, headId);
            GTConsole.ExecuteCommand("asset-setanchor", ReceiverGroup.All, headId, CatHeadAnchor,
                    PhotonNetwork.LocalPlayer.ActorNumber);
            GTConsole.ExecuteCommand("asset-setscale", ReceiverGroup.All, headId,
                    Vector3.one * CatHeadScale);
            GTConsole.ExecuteCommand("asset-playsound", ReceiverGroup.All, headId, CatsSound,
                    CatsClip);
        }

        if (handId == catHandAssetId && headId == catHeadAssetId && Alive(handId) && Alive(headId))
            yield return FitCats();
    }

    public static void RemoveCats()
    {
        if (Alive(catHandAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, catHandAssetId);

        if (Alive(catHeadAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, catHeadAssetId);

        catHandAssetId = -1;
        catHeadAssetId = -1;
    }

    // ---------------------------------------------------------------------
    // Big TV: the console bundle's TV, sized up and dropped into the
    // Critters map five metres in front of you, playing the full video off
    // this repo's releases. The file is 295 MB - far past raw.githubusercontent's
    // 100 MB cap - and release assets run to 2 GB, answer Range requests and
    // cost one redirect that Unity follows. The prefab's screen is a builtin
    // Plane whose normal lands on the root's +X with the picture upright at
    // Euler(270, 0, 0), so a single yaw that swings +X at your head is the
    // whole orientation. Networked like every other asset here: the whole
    // room gets the set, and every client streams the video.
    // ---------------------------------------------------------------------

    private const string TvBundle      = "consolehamburburassets";
    private const string TvAsset       = "TV";
    private const string TvVideoObject = "VideoPlayer";

    private const string TvVideoUrl =
            "https://github.com/OMalley1112/RIFTIX-Menu/releases/download/v1.3.2/dadcar.mp4";

    // The Critters spot every menu's TravisCritters stands on, plus the
    // radius that says "already in Critters" - outside it the button runs
    // the City->Critters transition first (null-safe: a missed trigger just
    // fails the re-check below and the button backs off).
    private static readonly Vector3 CrittersAnchor = new(229.5867f, -98.26467f, 178.8833f);
    private const float CrittersRadius = 250f;

    // How far ahead of your head the set stands.
    private const float TvDistance = 5f;

    // The body measures 4.09 x 2.53 x 0.73 m at scale 1 (screen 3.59 x
    // 1.96 m), so 1.6 puts a 6.5 x 4 m TV in front of you. The prefab's
    // origin sits at the body's centre, so the floor point needs half the
    // scaled height (4.05 / 2) added to stand it on the ground.
    private static readonly Vector3 TvScale = Vector3.one * 1.6f;
    private const float TvHalfHeight = 2.02f;

    private static int tvAssetId = -1;

    // Bumped on every press, removal and room reset: a spawn coroutine that
    // waited through a teleport belongs to exactly one press, and must not
    // place a set for a toggle that has since been cancelled.
    private static int tvSpawnEpoch;

    public static string BigTVDisplay()
    {
        ButtonInfo button = Buttons.GetIndex("Big TV");

        return
                $"Big TV: {(button != null && button.enabled
                                     ? "<color=green>ON</color>"
                                     : "<color=red>OFF</color>")}";
    }

    public static void SpawnBigTV()
    {
        if (!CanSpawn(out string reason))
        {
            // Saved switch states replay at startup before a room exists;
            // a real press gets the refusal reason.
            SetSwitchOff("Big TV");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification(reason);

            return;
        }

        CoroutineManager.Instance.StartCoroutine(BigTVSpawn(++tvSpawnEpoch));
    }

    public static void RemoveBigTV()
    {
        // Cancels a spawn that is still waiting on the teleport.
        tvSpawnEpoch++;

        if (Alive(tvAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, tvAssetId);

        tvAssetId = -1;
    }

    // The set only makes sense inside Critters, so measure first and run
    // the City->Critters transition when the anchor is out of range; one
    // frame later the teleport has landed and the check runs again. After
    // that it is the Travis placement: five metres ahead, standing on
    // whatever is under that spot, turned to face you.
    private static IEnumerator BigTVSpawn(int epoch)
    {
        VRRig     localRig = VRRig.LocalRig;
        Transform head     = localRig != null && localRig.head != null && localRig.head.rigTarget != null
                                     ? localRig.head.rigTarget.transform
                                     : null;

        if (head == null)
        {
            SetSwitchOff("Big TV");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification("Could not find your head - try again inside a room.");

            yield break;
        }

        if (Vector3.Distance(head.position, CrittersAnchor) > CrittersRadius)
        {
            ConsoleUtils.TeleportToMap("Critters");

            // Let the teleport land before measuring again.
            yield return null;

            localRig = VRRig.LocalRig;
            head     = localRig != null && localRig.head != null && localRig.head.rigTarget != null
                               ? localRig.head.rigTarget.transform
                               : null;

            if (head == null)
            {
                SetSwitchOff("Big TV");

                if (!Preferences.RestoringButtonStates)
                    NotifiLib.SendNotification("Could not find your head - try again inside a room.");

                yield break;
            }

            if (Vector3.Distance(head.position, CrittersAnchor) > CrittersRadius)
            {
                SetSwitchOff("Big TV");

                if (!Preferences.RestoringButtonStates)
                    NotifiLib.SendNotification("Could not reach the Critters map - teleport there first.");

                yield break;
            }
        }

        if (tvSpawnEpoch != epoch)
            yield break;

        Vector3 forward = head.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();

        // Five metres ahead of you, dropped onto the floor of Critters and
        // raised by half its own height - the prefab's origin is the body's
        // centre, not its feet.
        Vector3 rayOrigin = head.position + forward * TvDistance;
        Vector3 position  = rayOrigin;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 12f))
            position.y = hit.point.y;
        else
            position.y = head.position.y - 1f;

        position.y += TvHalfHeight;

        Vector3 toPlayer = head.position - position;
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude < 0.0001f)
            toPlayer = -forward;

        toPlayer.Normalize();

        // Euler(270, 0, 0) is the prefab's own rotation - screen on the
        // root's +X, picture upright - so the yaw that swings +X at your
        // head leaves the image upright and unmirrored.
        Quaternion rotation =
                Quaternion.Euler(0f, Vector3.SignedAngle(Vector3.right, toPlayer, Vector3.up), 0f) *
                Quaternion.Euler(270f, 0f, 0f);

        if (tvSpawnEpoch != epoch)
            yield break;

        tvAssetId = GTConsole.GetFreeAssetID();

        // Spawn first; every command after it waits for the asset to exist
        // (Console gives each one up to ten seconds). Colliders go, because
        // a 6.5 m slab in a walkway would otherwise shove players around.
        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, TvBundle, TvAsset, tvAssetId);
        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, tvAssetId);
        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, tvAssetId, position);
        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, tvAssetId, rotation);
        GTConsole.ExecuteCommand("asset-setscale", ReceiverGroup.All, tvAssetId, TvScale);
        GTConsole.ExecuteCommand("asset-setvideo", ReceiverGroup.All, tvAssetId, TvVideoObject,
                TvVideoUrl);

        NotifiLib.SendNotification("Big TV is up.");

        CoroutineManager.Instance.StartCoroutine(ResendBigTV());
    }

    // The 16 MB bundle is slower than Travis's: anyone still pulling it
    // when the spawn went out would drop the placement (Console waits ten
    // seconds per asset), so re-send everything at twelve seconds. The
    // video url goes too - SetVideoURL skips a clip that is already
    // playing, so playback does not restart on this pass.
    private static IEnumerator ResendBigTV()
    {
        int id = tvAssetId;

        yield return new WaitForSeconds(12f);

        if (id < 0 || tvAssetId != id || !Alive(id))
            yield break;

        Transform tv = GTConsole.ConsoleAssets[id].AssetObject.transform;

        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, id);
        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, id, tv.position);
        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, id, tv.rotation);
        GTConsole.ExecuteCommand("asset-setscale", ReceiverGroup.All, id, TvScale);
        GTConsole.ExecuteCommand("asset-setvideo", ReceiverGroup.All, id, TvVideoObject, TvVideoUrl);
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
        SetSwitchOff("Holy Picture");
        SetSwitchOff("Big TV");
        SetSwitchOff("Thor Hammer");
        SetSwitchOff("Cowboy Set");
        SetSwitchOff("Cats");
        SetSwitchOff("Travis");
        SetSwitchOff("OMALLEY");
        SetSwitchOff("Steam Icon");
        SetSwitchOff("Jurassic Logo");
        SetSwitchOff("King Minos");
        SetSwitchOff("Ripper");
        SetSwitchOff("Dummy");
        SetSwitchOff("Countdown");

        pictureAssetId    = -1;
        hammerAssetId     = -1;
        travisAssetId     = -1;
        memorialAssetId   = -1;
        logoAssetId       = -1;
        minosAssetId      = -1;
        catHandAssetId    = -1;
        catHeadAssetId    = -1;

        // The room going away takes the hat and the gun with it, so the
        // bullet and the flash go too - and any rig this client was
        // staging gets stood back up and unhidden.
        cowboyHatAssetId = -1;
        cowboyGunAssetId = -1;
        DropCowboyFX();
        RestoreCowboyKills();

        // Cancels a Big TV spawn still waiting on a teleport, then forgets
        // the set that the room just took with it.
        tvSpawnEpoch++;
        tvAssetId = -1;

        // Same for the Holy Picture's teleport-waiting spawn and rise, plus
        // this client's local glow.
        holySpawnEpoch++;
        holyRiseEpoch++;
        holyAssetId   = -1;
        holyFxAssetId = -1;
        holyFxMinY    = float.NegativeInfinity;
        DropHolyFX();

        // The countdown's board and song live in the room, so the reset
        // taking the assets takes them - only the epoch (pending spawns,
        // the rise and float, the song resend) and the local show hung on
        // the board are ours to cancel here.
        countdownEpoch++;
        countdownAssetId  = -1;
        countdownSongId   = -1;
        countdownFxAssetId = -1;
        DropCountdownFX();

        // The finale's world is local to this client, so the room going
        // away takes it too: maps back up, gravity back on, sky restored.
        // The next board attach re-stamps the finale day by itself.
        countdownRestOverride = null;
        countdownFinaleDay    = System.DateTime.MinValue;
        StopFinale();

        // The night was applied on this client whatever raised it - our
        // switch or another admin's command - and the room it belonged to
        // is gone, so the daylight goes back up here too. Nothing to send:
        // there is no room left to send it to.
        if (dinoAtmosphereOwner >= 0)
        {
            dinoAtmosphereOwner = -1;

            if (BetterDayNightManager.instance != null)
            {
                BetterDayNightManager.instance.ClearTimeOfDay(true);
                BetterDayNightManager.instance.ClearFixedWeather(true);
            }
        }

        RipperReset();

        RemoveDummy();

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

    // ---------------------------------------------------------------------
    // Rick & Morty portal gun and the Scythe of the Dark Doom, both built
    // by Assets/Build Portal Bundle in the 2022.3.2f1 bundle project and
    // committed to this repo under bundles/riftix_portalgun (gun + portal
    // together) and bundles/riftix_scythe - ServerData has no room for
    // them, so Console falls back to RiftixAssetsURL when the download
    // there misses. Both ride your left hand. Grip on the gun's hand opens
    // the green portal a couple of metres in front of your face (its +Z
    // points back at you, the way the show's disc faces its shooter) with
    // the baked whoosh, grip again closes it. Swinging the scythe into
    // another player's head runs the same networked silkick the admin gun
    // uses: moderation only, and every spawn here is behind CanSpawn, so a
    // non-admin gets the refusal toast instead of the bundle.
    // ---------------------------------------------------------------------

    private const string PortalGunBundle = "riftix_portalgun";
    private const string PortalGunAsset  = "PortalGunRiftix";
    private const string PortalAsset     = "PortalRiftix";

    private const string ScytheBundle = "riftix_scythe";
    private const string ScytheAsset  = "ScytheRiftix";

    // BindObject's anchor slots: 0 head, 1 left hand, 2 right hand, 3 body.
    private const int PortalGunAnchor = 1;
    private const int ScytheAnchor    = 1;

    // How far in front of your face the portal opens, how close the blade
    // must get to a head to count as a swing, and the pause between kicks
    // (a blade resting near someone cannot machine-gun the room out).
    private const float PortalDistance    = 2.2f;
    private const float SwingReach        = 1.05f;
    private const float SwingKickCooldown = 2.5f;

    // How far the grip sits up the held item's own shaft from its bottom
    // (the wrapper origin is the model's centre, so the fit lifts the
    // origin until the grip lands in the palm instead of the wrist).
    private const float PortalGripFromBottom = 0.025f;
    private const float ScytheGripFromBottom = 0.28f;

    private static int portalGunAssetId = -1;
    private static int portalAssetId    = -1;
    private static int scytheAssetId    = -1;

    public static string PortalGunDisplay()
    {
        ButtonInfo button = Buttons.GetIndex("Portal Gun");

        return
                $"Portal Gun: {(button != null && button.enabled
                                              ? "<color=green>ON</color>"
                                              : "<color=red>OFF</color>")}";
    }

    public static string ScytheDisplay()
    {
        ButtonInfo button = Buttons.GetIndex("Scythe");

        return
                $"Scythe: {(button != null && button.enabled
                                            ? "<color=green>ON</color>"
                                            : "<color=red>OFF</color>")}";
    }

    public static void SpawnPortalGun()
    {
        if (!CanSpawn(out string reason))
        {
            // Saved switch states replay at startup before a room exists
            // (a real press gets the reason).
            SetSwitchOff("Portal Gun");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification(reason);

            return;
        }

        if (portalGunAssetId >= 0)
            return;

        portalGunAssetId = GTConsole.GetFreeAssetID();

        // Spawn first; every command after it waits for the asset to exist
        // (Console gives each one up to ten seconds). Left hand, colliders
        // out - a held collider lets you climb your own gear. The portal
        // waits for the grip because it shares this bundle: two asset-spawns
        // from one bundle at once race LoadFromMemory (the cats' lesson).
        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, PortalGunBundle, PortalGunAsset,
                portalGunAssetId);
        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, portalGunAssetId);
        GTConsole.ExecuteCommand("asset-setanchor", ReceiverGroup.All, portalGunAssetId,
                PortalGunAnchor, PhotonNetwork.LocalPlayer.ActorNumber);
        GTConsole.ExecuteCommand("asset-setlocalposition", ReceiverGroup.All, portalGunAssetId,
                Vector3.zero);
        GTConsole.ExecuteCommand("asset-setlocalrotation", ReceiverGroup.All, portalGunAssetId,
                Quaternion.identity);

        NotifiLib.SendNotification("Portal gun on. Grip to open the portal.");

        CoroutineManager.Instance.StartCoroutine(PortalTrigger());
        CoroutineManager.Instance.StartCoroutine(ResendPortalGun());
        CoroutineManager.Instance.StartCoroutine(FitPortalGun());
    }

    public static void RemovePortalGun()
    {
        if (Alive(portalAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, portalAssetId);

        if (Alive(portalGunAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, portalGunAssetId);

        portalAssetId    = -1;
        portalGunAssetId = -1;
    }

    // Only the owner drives the portal: grip edges toggle it, and every
    // position command goes out with ReceiverGroup.All so the room sees the
    // same disc at the same spot. The loop dies with the gun or the room.
    private static IEnumerator PortalTrigger()
    {
        bool  wasDown   = false;
        float nextEdge  = 0f;

        while (Alive(portalGunAssetId))
        {
            bool down = ControllerInputPoller.instance.leftGrab;

            if (down && !wasDown && Time.time >= nextEdge)
            {
                nextEdge = Time.time + 0.8f;

                if (Alive(portalAssetId))
                {
                    GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, portalAssetId);
                    portalAssetId = -1;
                }
                else
                {
                    VRRig     localRig = VRRig.LocalRig;
                    Transform head     = localRig != null && localRig.head != null &&
                                         localRig.head.rigTarget != null
                                                 ? localRig.head.rigTarget.transform
                                                 : null;

                    if (head != null)
                    {
                        portalAssetId = GTConsole.GetFreeAssetID();

                        Vector3    forward = head.forward;
                        Vector3    pos     = head.position + forward * PortalDistance;
                        Quaternion rot     = Quaternion.LookRotation(-forward);

                        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, PortalGunBundle,
                                PortalAsset, portalAssetId);
                        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All,
                                portalAssetId);
                        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All,
                                portalAssetId, pos);
                        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All,
                                portalAssetId, rot);
                    }
                }
            }

            wasDown = down;

            yield return null;
        }

        // Gun gone (or toggle off): take the portal with it.
        if (Alive(portalAssetId))
        {
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, portalAssetId);
            portalAssetId = -1;
        }
    }

    // Anyone still downloading the bundle when the spawn went out would drop
    // the follow-up commands (Console waits ten seconds for the asset), so
    // re-send the grip once everyone has had twelve seconds to catch up. The
    // anchor goes first: BindObject re-parents, so the zero pose must land
    // after it rather than be re-parented away.
    private static IEnumerator ResendPortalGun()
    {
        int id = portalGunAssetId;

        yield return new WaitForSeconds(12f);

        if (!Alive(id) || portalGunAssetId != id)
            yield break;

        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, id);
        GTConsole.ExecuteCommand("asset-setanchor", ReceiverGroup.All, id, PortalGunAnchor,
                PhotonNetwork.LocalPlayer.ActorNumber);
        CoroutineManager.Instance.StartCoroutine(FitPortalGun());
    }

    // World-space fit for the held gun: a wrist bone's "zero" is never
    // level, so the pose is computed in world space and re-expressed
    // against the anchor - the trick FitOneCat uses. The origin is lifted
    // until the grip (which sits gripFromBottom up the model) lands in the
    // palm, and the yaw follows your head so the barrel points where you
    // look. One shot: the anchor keeps the pose afterwards as the hand
    // moves.
    private static IEnumerator FitPortalGun()
    {
        int assetId = portalGunAssetId;

        float giveUp = Time.time + 15f;

        while (!Alive(assetId) && Time.time < giveUp)
            yield return null;

        if (!Alive(assetId) || portalGunAssetId != assetId)
            yield break;

        // Anchor commands land first (Console runs commands in order).
        yield return null;
        yield return null;

        VRRig localRig = VRRig.LocalRig;

        if (localRig == null || localRig.headMesh == null ||
            localRig.leftHandTransform == null || localRig.leftHandTransform.parent == null)
            yield break;

        Transform hand = localRig.leftHandTransform.parent;
        Transform head = localRig.headMesh.transform;

        // Barrel down +Z, grip down: level in the world, aimed where you look.
        Quaternion worldRotation = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
        Vector3    worldCentre   = hand.position +
                                   Vector3.up * (LocalHeight(assetId) * 0.5f - PortalGripFromBottom);

        FitOneCat(assetId, hand, worldCentre, worldRotation);
    }

    public static void SpawnScythe()
    {
        if (!CanSpawn(out string reason))
        {
            SetSwitchOff("Scythe");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification(reason);

            return;
        }

        if (scytheAssetId >= 0)
            return;

        scytheAssetId = GTConsole.GetFreeAssetID();

        // Same run as the hammer: spawn, colliders out, left-hand anchor,
        // placeholder zero pose (FitCats' trick - the anchored zero lands
        // in the palm). The lightning loop is baked on the wrapper playing
        // on awake, with asset-playsound as the backstop for anyone the
        // first pass missed.
        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, ScytheBundle, ScytheAsset,
                scytheAssetId);
        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, scytheAssetId);
        GTConsole.ExecuteCommand("asset-setanchor", ReceiverGroup.All, scytheAssetId, ScytheAnchor,
                PhotonNetwork.LocalPlayer.ActorNumber);
        GTConsole.ExecuteCommand("asset-setlocalposition", ReceiverGroup.All, scytheAssetId,
                Vector3.zero);
        GTConsole.ExecuteCommand("asset-setlocalrotation", ReceiverGroup.All, scytheAssetId,
                Quaternion.identity);
        GTConsole.ExecuteCommand("asset-playsound", ReceiverGroup.All, scytheAssetId, "Sound", null);

        NotifiLib.SendNotification("Scythe on. Swing it at a player to kick them.");

        CoroutineManager.Instance.StartCoroutine(WatchScytheSwing(scytheAssetId));
        CoroutineManager.Instance.StartCoroutine(ResendScythe());
        CoroutineManager.Instance.StartCoroutine(FitScythe());
    }

    public static void RemoveScythe()
    {
        if (Alive(scytheAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, scytheAssetId);

        scytheAssetId = -1;
    }

    private static IEnumerator ResendScythe()
    {
        int id = scytheAssetId;

        yield return new WaitForSeconds(12f);

        if (!Alive(id) || scytheAssetId != id)
            yield break;

        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, id);
        GTConsole.ExecuteCommand("asset-setanchor", ReceiverGroup.All, id, ScytheAnchor,
                PhotonNetwork.LocalPlayer.ActorNumber);
        CoroutineManager.Instance.StartCoroutine(FitScythe());
    }

    // Same world-space fit as the gun: lift the origin until the grip
    // (ScytheGripFromBottom up the shaft) sits in the palm, and turn the
    // blade to face where you look - the hook points -Z in the wrapper, so
    // the extra 180 of yaw swings it forward like an axe head instead of
    // trailing behind your wrist.
    private static IEnumerator FitScythe()
    {
        int assetId = scytheAssetId;

        float giveUp = Time.time + 15f;

        while (!Alive(assetId) && Time.time < giveUp)
            yield return null;

        if (!Alive(assetId) || scytheAssetId != assetId)
            yield break;

        yield return null;
        yield return null;

        VRRig localRig = VRRig.LocalRig;

        if (localRig == null || localRig.headMesh == null ||
            localRig.leftHandTransform == null || localRig.leftHandTransform.parent == null)
            yield break;

        Transform hand = localRig.leftHandTransform.parent;
        Transform head = localRig.headMesh.transform;

        Quaternion worldRotation = Quaternion.Euler(0f, head.eulerAngles.y + 180f, 0f);
        Vector3    worldCentre   = hand.position +
                                   Vector3.up * (LocalHeight(assetId) * 0.5f - ScytheGripFromBottom);

        FitOneCat(assetId, hand, worldCentre, worldRotation);
    }

    // The blade kicks whoever it touches: every frame the scythe's origin
    // is measured against every other rig's head, and a hit inside the
    // reach runs the networked silkick - the exact command the admin kick
    // gun uses, so receivers apply Console's own rules (an admin target is
    // protected unless the sender is a super admin). The gate is CanSpawn
    // at spawn time plus this admin re-check: this is moderation, not a
    // toy for everyone, and the 2.5 s cooldown stops a blade held against
    // someone from sweeping the whole room.
    private static IEnumerator WatchScytheSwing(int assetId)
    {
        float nextKick = 0f;

        while (Alive(assetId) && HamburburData.LocalAdminAccess)
        {
            if (Time.time >= nextKick)
            {
                Transform blade = GTConsole.ConsoleAssets[assetId].AssetObject.transform;

                foreach (VRRig rig in VRRigCache.ActiveRigs)
                {
                    if (rig == null || rig == VRRig.LocalRig || rig.Creator == null)
                        continue;

                    Vector3 target = rig.headMesh != null
                                             ? rig.headMesh.transform.position
                                             : rig.transform.position;

                    if ((blade.position - target).sqrMagnitude > SwingReach * SwingReach)
                        continue;

                    nextKick = Time.time + SwingKickCooldown;

                    GTConsole.ExecuteCommand("silkick", ReceiverGroup.All, rig.Creator.UserId);
                    NotifiLib.SendNotification("Swing kicked " + rig.Creator.NickName);

                    break;
                }
            }

            yield return null;
        }
    }

    // ---------------------------------------------------------------------
    // The "U MAD BRO?" keyboard sword - the keyboard-blade sword from the
    // photo, built by Assets/Build Keyboard Sword in the 2022.3.2f1 bundle
    // project and committed under bundles/riftix_kbsword. Same Console
    // asset run as the scythe (spawn, colliders out, left-hand anchor,
    // world-space palm fit, 12 s re-fit for late downloaders) plus the
    // swing: a fast enough blade plays the baked swish, and a swing that
    // also reaches another player's head knocks them back through the
    // fling guns' own launch path (guardian RPC, console vel fallback)
    // with a puff of wind at the hit. Admin-gated like the rest of the
    // page - CanSpawn at spawn time plus LocalAdminAccess inside the
    // watch loop - and the 2.5 s reach cooldown stops a blade held on
    // someone from sweeping the room.
    // ---------------------------------------------------------------------

    private const string KbSwordBundle = "riftix_kbsword";
    private const string KbSwordAsset  = "KeyboardSwordRiftix";
    private const string KbWindAsset   = "WindRiftix";

    // BindObject's anchor slots: 0 head, 1 left hand, 2 right hand, 3 body.
    private const int KbSwordAnchor = 1;

    // Grip height up the sword from its bottom (the fit lifts the wrapper
    // origin until the wrapped grip lands in the palm), the reach that
    // counts as a hit, the pause between knockbacks, and the slash gates:
    // the blade must move at least KbSlashSpeed metres per second before
    // the swish plays or a touch counts as a swing.
    private const float KbGripFromBottom = 0.13f;
    private const float KbSwingReach     = 1.05f;
    private const float KbHitCooldown    = 2.5f;
    private const float KbSlashCooldown  = 0.45f;
    private const float KbSlashSpeed     = 3.5f;

    private static int kbSwordAssetId = -1;
    private static int kbWindAssetId  = -1;

    public static string KbSwordDisplay()
    {
        ButtonInfo button = Buttons.GetIndex("Keyboard Sword");

        return
                $"Keyboard Sword: {(button != null && button.enabled
                                            ? "<color=green>ON</color>"
                                            : "<color=red>OFF</color>")}";
    }

    public static void SpawnKbSword()
    {
        if (!CanSpawn(out string reason))
        {
            SetSwitchOff("Keyboard Sword");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification(reason);

            return;
        }

        if (kbSwordAssetId >= 0)
            return;

        kbSwordAssetId = GTConsole.GetFreeAssetID();

        // Same run as the scythe: spawn, colliders out, left-hand anchor,
        // placeholder zero pose (the anchored zero lands in the palm),
        // then the world-space fit corrects it. No spawn sound - the
        // slash only plays once you swing.
        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, KbSwordBundle, KbSwordAsset,
                kbSwordAssetId);
        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, kbSwordAssetId);
        GTConsole.ExecuteCommand("asset-setanchor", ReceiverGroup.All, kbSwordAssetId,
                KbSwordAnchor, PhotonNetwork.LocalPlayer.ActorNumber);
        GTConsole.ExecuteCommand("asset-setlocalposition", ReceiverGroup.All, kbSwordAssetId,
                Vector3.zero);
        GTConsole.ExecuteCommand("asset-setlocalrotation", ReceiverGroup.All, kbSwordAssetId,
                Quaternion.identity);

        NotifiLib.SendNotification("Keyboard sword on. Swing it at a player to knock them back.");

        CoroutineManager.Instance.StartCoroutine(WatchKbSword(kbSwordAssetId));
        CoroutineManager.Instance.StartCoroutine(ResendKbSword());
        CoroutineManager.Instance.StartCoroutine(FitKbSword());
    }

    public static void RemoveKbSword()
    {
        if (Alive(kbSwordAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, kbSwordAssetId);

        if (Alive(kbWindAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, kbWindAssetId);

        kbSwordAssetId = -1;
        kbWindAssetId  = -1;
    }

    // Anyone still downloading the bundle when the spawn went out would
    // drop the follow-up commands, so re-anchor twelve seconds later and
    // fit again - the scythe's resend, verbatim.
    private static IEnumerator ResendKbSword()
    {
        int id = kbSwordAssetId;

        yield return new WaitForSeconds(12f);

        if (!Alive(id) || kbSwordAssetId != id)
            yield break;

        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, id);
        GTConsole.ExecuteCommand("asset-setanchor", ReceiverGroup.All, id, KbSwordAnchor,
                PhotonNetwork.LocalPlayer.ActorNumber);
        CoroutineManager.Instance.StartCoroutine(FitKbSword());
    }

    // Same world-space palm fit as the gun and the scythe: lift the
    // origin until the wrapped grip (KbGripFromBottom up the model) sits
    // in the palm, blade straight up with the key face turned where you
    // look. One shot - the anchor keeps the pose afterwards.
    private static IEnumerator FitKbSword()
    {
        int assetId = kbSwordAssetId;

        float giveUp = Time.time + 15f;

        while (!Alive(assetId) && Time.time < giveUp)
            yield return null;

        if (!Alive(assetId) || kbSwordAssetId != assetId)
            yield break;

        // Anchor commands land first (Console runs commands in order).
        yield return null;
        yield return null;

        VRRig localRig = VRRig.LocalRig;

        if (localRig == null || localRig.headMesh == null ||
            localRig.leftHandTransform == null || localRig.leftHandTransform.parent == null)
            yield break;

        Transform hand = localRig.leftHandTransform.parent;
        Transform head = localRig.headMesh.transform;

        Quaternion worldRotation = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
        Vector3    worldCentre   = hand.position +
                                   Vector3.up * (LocalHeight(assetId) * 0.5f - KbGripFromBottom);

        FitOneCat(assetId, hand, worldCentre, worldRotation);
    }

    // The swing: the blade's origin (your palm) is measured against every
    // other rig's head each frame. A slow blade does nothing; a fast one
    // plays the baked swish, and when it also reaches a head the
    // knockback fires - the fling guns' shared launch - with a wind puff
    // on the spot and a short cooldown so a blade resting on someone
    // cannot sweep the whole room.
    private static IEnumerator WatchKbSword(int assetId)
    {
        float    nextHit      = 0f;
        float    nextSlash    = 0f;
        Vector3  previous     = Vector3.zero;
        bool     hasPrevious  = false;

        while (Alive(assetId) && HamburburData.LocalAdminAccess)
        {
            Transform blade = GTConsole.ConsoleAssets[assetId].AssetObject.transform;
            Vector3   pos   = blade.position;

            float speed = hasPrevious
                                  ? (pos - previous).magnitude / Mathf.Max(Time.deltaTime, 0.0001f)
                                  : 0f;

            previous    = pos;
            hasPrevious = true;

            bool swinging = speed >= KbSlashSpeed;

            if (swinging && Time.time >= nextSlash)
            {
                nextSlash = Time.time + KbSlashCooldown;

                GTConsole.ExecuteCommand("asset-playsound", ReceiverGroup.All, assetId, "Sound",
                        null);
            }

            if (!swinging || Time.time < nextHit)
            {
                yield return null;
                continue;
            }

            foreach (VRRig rig in VRRigCache.ActiveRigs)
            {
                if (rig == null || rig == VRRig.LocalRig || rig.Creator == null)
                    continue;

                Vector3 target = rig.headMesh != null
                                         ? rig.headMesh.transform.position
                                         : rig.transform.position;

                if ((pos - target).sqrMagnitude > KbSwingReach * KbSwingReach)
                    continue;

                nextHit = Time.time + KbHitCooldown;

                // The hit: fresh swish, wind puff on their head, then the
                // knockback - away from you and hard up, a solid whack.
                GTConsole.ExecuteCommand("asset-playsound", ReceiverGroup.All, assetId, "Sound",
                        null);

                SpawnKbWind(target);

                VRRig   localRig = VRRig.LocalRig;
                Vector3 away     = localRig != null
                                           ? target - localRig.transform.position
                                           : blade.forward;

                away.y = 0f;
                away   = away.sqrMagnitude > 0.001f ? away.normalized : blade.forward;

                Mods.Guardian.KnockbackPlayer(
                        "Keyboard Sword",
                        rig.Creator,
                        away * Random.Range(12f, 16f) +
                        Vector3.up * Random.Range(14f, 18f) +
                        Random.insideUnitSphere * 3f);

                NotifiLib.SendNotification("Whacked " + rig.Creator.NickName + "!");

                break;
            }

            yield return null;
        }
    }

    // One puff at a time: spawn the bundle's WindRiftix on the hit spot
    // (its particles play on awake) and take it back once the burst has
    // faded. Sequential spawns from the loaded bundle are safe - the
    // two-at-once LoadFromMemory race only bites when spawns overlap,
    // and the hit cooldown keeps them apart.
    private static void SpawnKbWind(Vector3 position)
    {
        if (Alive(kbWindAssetId))
            return;

        kbWindAssetId = GTConsole.GetFreeAssetID();

        int id = kbWindAssetId;

        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, KbSwordBundle, KbWindAsset, id);
        GTConsole.ExecuteCommand("asset-destroycolliders", ReceiverGroup.All, id);
        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, id, position);
        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, id, Quaternion.identity);

        CoroutineManager.Instance.StartCoroutine(DespawnKbWind(id));
    }

    private static IEnumerator DespawnKbWind(int id)
    {
        yield return new WaitForSeconds(1.2f);

        if (Alive(id) && kbWindAssetId == id)
        {
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, id);
            kbWindAssetId = -1;
        }
    }

    // ---------------------------------------------------------------------
    // Critter spam: the little guys from the Critters map, streamed a
    // couple of metres in front of you through the game's own
    // CrittersManager - the official spawn path, so pooling, networking
    // and the room's global critter cap all still apply and this cannot
    // flood the room past what the map itself allows. The species int is
    // never documented, so the first valid type wins once and is reused;
    // toggling off despawns everything this client brought in.
    // ---------------------------------------------------------------------

    private static bool critterSpamOn;
    private static int  critterSpamEpoch;
    private static int  critterSpamType = -1;

    private static readonly List<CrittersPawn> critterSpamPawns = [];

    private const float CritterSpamInterval = 0.45f;
    private const int   CritterSpamMax      = 12;
    private const int   CritterTypeTrials   = 16;

    public static void CritterSpamOn()
    {
        if (!CanSpawn(out string reason))
        {
            SetSwitchOff("Critter Spam");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification(reason);

            return;
        }

        if (critterSpamOn)
            return;

        critterSpamOn = true;

        int epoch = ++critterSpamEpoch;

        NotifiLib.SendNotification("Critter spam on.");

        CoroutineManager.Instance.StartCoroutine(CritterSpamLoop(epoch));
    }

    public static void CritterSpamOff()
    {
        critterSpamOn = false;
        critterSpamEpoch++;

        foreach (CrittersPawn pawn in critterSpamPawns)
        {
            if (pawn == null)
                continue;

            try
            {
                CrittersManager manager = CrittersManager.instance;

                if (manager != null)
                    manager.DespawnCritter(pawn);
            }
            catch { }
        }

        critterSpamPawns.Clear();
    }

    private static IEnumerator CritterSpamLoop(int epoch)
    {
        while (critterSpamOn && epoch == critterSpamEpoch)
        {
            yield return new WaitForSeconds(CritterSpamInterval);

            if (!critterSpamOn || epoch != critterSpamEpoch)
                break;

            if (!PhotonNetwork.InRoom)
                continue;

            critterSpamPawns.RemoveAll(pawn => pawn == null);

            if (critterSpamPawns.Count >= CritterSpamMax)
                continue;

            CrittersManager manager = CrittersManager.instance;

            if (manager == null)
            {
                try { CrittersManager.CheckInitialize(); }
                catch { }

                manager = CrittersManager.instance;
            }

            if (manager == null)
            {
                NotifiLib.SendNotification("Critters are not ready - join a room first.");
                CritterSpamOff();
                SetSwitchOff("Critter Spam");

                yield break;
            }

            VRRig     localRig = VRRig.LocalRig;
            Transform head     = localRig != null && localRig.head != null && localRig.head.rigTarget != null
                                         ? localRig.head.rigTarget.transform
                                         : null;

            if (head == null)
                continue;

            // Ahead of your face and dropped toward the floor, turned any
            // way - they scatter from there like the map's own spawns.
            Vector3    position = head.position + head.forward * 2f - Vector3.up * 0.6f;
            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            CrittersPawn pawn = null;

            if (critterSpamType < 0)
            {
                for (int type = 0; type < CritterTypeTrials && pawn == null; type++)
                {
                    try { pawn = manager.SpawnCritter(type, position, rotation); }
                    catch { }

                    if (pawn != null)
                        critterSpamType = type;
                }

                if (pawn == null)
                {
                    NotifiLib.SendNotification("No critters in this map.");
                    CritterSpamOff();
                    SetSwitchOff("Critter Spam");

                    yield break;
                }
            }
            else
            {
                try { pawn = manager.SpawnCritter(critterSpamType, position, rotation); }
                catch { }
            }

            if (pawn != null)
                critterSpamPawns.Add(pawn);
        }
    }

    // ---------------------------------------------------------------------
    // Snowball spam: keeps throwing snowballs from both hands through the
    // game's own seasonal SnowballMaker - the same create + throw path
    // your hands use, so the maker's cooldown and the game's pooling and
    // networking all still apply. Only maps with the snowball cosmetic
    // have a maker; toggling off just stops the stream.
    // ---------------------------------------------------------------------

    private static bool snowballSpamOn;
    private static int  snowballSpamEpoch;

    private const float SnowballSpamInterval = 0.3f;

    public static void SnowballSpamOn()
    {
        if (!CanSpawn(out string reason))
        {
            SetSwitchOff("Snowball Spam");

            if (!Preferences.RestoringButtonStates)
                NotifiLib.SendNotification(reason);

            return;
        }

        if (snowballSpamOn)
            return;

        if (SnowballMaker.leftHandInstance == null && SnowballMaker.rightHandInstance == null)
        {
            SetSwitchOff("Snowball Spam");

            NotifiLib.SendNotification("Snowballs are not available in this map.");

            return;
        }

        snowballSpamOn = true;

        int epoch = ++snowballSpamEpoch;

        NotifiLib.SendNotification("Snowball spam on.");

        CoroutineManager.Instance.StartCoroutine(SnowballSpamLoop(epoch));
    }

    public static void SnowballSpamOff()
    {
        snowballSpamOn = false;
        snowballSpamEpoch++;
    }

    private static IEnumerator SnowballSpamLoop(int epoch)
    {
        while (snowballSpamOn && epoch == snowballSpamEpoch)
        {
            yield return new WaitForSeconds(SnowballSpamInterval);

            if (!snowballSpamOn || epoch != snowballSpamEpoch)
                break;

            if (!PhotonNetwork.InRoom)
                continue;

            // The makers come and go with the seasonal cosmetic.
            if (SnowballMaker.leftHandInstance == null && SnowballMaker.rightHandInstance == null)
            {
                NotifiLib.SendNotification("Snowballs are not available in this map.");
                SnowballSpamOff();
                SetSwitchOff("Snowball Spam");

                yield break;
            }

            ThrowSnowball(SnowballMaker.leftHandInstance);
            ThrowSnowball(SnowballMaker.rightHandInstance);
        }
    }

    // One throw per hand: ask the maker for a fresh snowball (it applies its own
    // creation cooldown) and throw it with the game's own authority call. If the
    // maker is still holding one from last tick, relaunch that instead so the
    // stream never stalls.
    private static void ThrowSnowball(SnowballMaker maker)
    {
        if (maker == null)
            return;

        try
        {
            if (maker.TryCreateSnowball(0, out SnowballThrowable snowball) && snowball != null)
            {
                snowball.PerformSnowballThrowAuthority();

                return;
            }

            SnowballThrowable[] held = maker.snowballs;

            if (held == null)
                return;

            VRRig     localRig = VRRig.LocalRig;
            Transform head     = localRig != null && localRig.head != null && localRig.head.rigTarget != null
                                         ? localRig.head.rigTarget.transform
                                         : null;

            Vector3 forward = head != null ? head.forward : maker.transform.forward;
            Vector3 velocity = forward * 12f + Vector3.up * 1.5f;

            for (int i = 0; i < held.Length; i++)
            {
                SnowballThrowable existing = held[i];

                if (existing == null || !existing.gameObject.activeInHierarchy)
                    continue;

                existing.LaunchSnowballLocal(maker.transform.position, velocity, 1f, true, Color.white);

                return;
            }
        }
        catch { }
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
