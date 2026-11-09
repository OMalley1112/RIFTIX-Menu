using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using StupidTemplate.Classes;
using StupidTemplate.Notifications;
using UnityEngine;
using GTConsole = StupidTemplate.Classes.Console.Console;
using HamburburData = StupidTemplate.Classes.Console.HamburburData;

namespace StupidTemplate.Menu;

// The small Console Assets page: one button, one picture.
//
// Press it and the TikTok picture - a still photo with its song baked into a
// single mp4 - appears on a bare screen floating in front of you: no TV
// furniture, just the picture turned to face you. Every command goes out with
// ReceiverGroup.All, so the whole room spawns the same screen at the same spot
// showing the same picture; players who join later get it through Console's
// asset sync (which re-sends the texture and the video url).
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

    public static string PictureDisplay()
    {
        ButtonInfo button = Buttons.GetIndex("TikTok Picture");

        return
                $"TikTok Picture: {(button != null && button.enabled
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
            SetSwitchOff();

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
            SetSwitchOff();

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

    private static bool Alive(int assetId) =>
            assetId >= 0 &&
            GTConsole.ConsoleAssets.TryGetValue(assetId, out GTConsole.ConsoleAsset asset) &&
            asset.AssetObject != null;

    // Console clears every asset when the room is left (ClearConsoleAssets):
    // the picture goes with them, so the switch must read OFF again instead of
    // claiming a picture that no longer exists.
    public static void ResetPictureSwitch() => SetSwitchOff();

    private static void SetSwitchOff()
    {
        ButtonInfo button = Buttons.GetIndex("TikTok Picture");

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
