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
// Press it and the TikTok picture - still image plus its song, baked into a
// single mp4 - appears on the Gemstone TV in front of you. Every command goes
// out with ReceiverGroup.All, so the whole room spawns the same TV at the same
// spot playing the same clip; anyone who joins late gets the spawn too.
public abstract class ConsoleAssetPages
{
    private const string TvBundle = "consolehamburburassets";

    // "The Park is Closed" (Jurassic World OST) under the TikTok picture,
    // encoded as park.mp4 and committed to this menu's public repo. Served
    // straight off raw.githubusercontent.com: no redirect hop for the player
    // to follow, and Range requests are answered, which is what Unity's
    // VideoPlayer needs to stream a file.
    private const string PictureVideoUrl =
            "https://raw.githubusercontent.com/OMalley1112/RIFTIX-Menu/main/media/park.mp4";

    // How far in front of your face the TV stands.
    private const float SpawnDistance = 3f;

    private static int tvAssetId = -1;

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
            // Saved switch states are restored before a room is joined;
            // flipping back off silently beats a notification at startup.
            SetSwitchOff();

            if (PhotonNetwork.InRoom)
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
            NotifiLib.SendNotification("Could not find your head - try again inside a room.");

            return;
        }

        // Stand it SpawnDistance metres ahead of you, at eye height, turned to
        // face you. The TV's screen points along +X when its rotation is
        // Euler(270, 0, 0) - the pitch every other menu spawns it with - so the
        // yaw is solved from the direction back to your head.
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

        float yaw = Mathf.Atan2(-toPlayer.z, toPlayer.x) * Mathf.Rad2Deg;

        tvAssetId = GTConsole.GetFreeAssetID();

        // Spawn first; every command after it waits for the asset to exist
        // (Console gives each one up to ten seconds), so the TV arrives in
        // place with the picture already on it.
        GTConsole.ExecuteCommand("asset-spawn", ReceiverGroup.All, TvBundle, "TV", tvAssetId);
        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, tvAssetId, position);
        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, tvAssetId,
                Quaternion.Euler(270f, yaw, 0f));
        GTConsole.ExecuteCommand("asset-setvideo", ReceiverGroup.All, tvAssetId, "VideoPlayer",
                PictureVideoUrl);

        NotifiLib.SendNotification("Picture is up in front of you - the whole room sees it.");

        CoroutineManager.Instance.StartCoroutine(ResendPicture());
    }

    public static void RemovePicture()
    {
        if (Alive(tvAssetId))
            GTConsole.ExecuteCommand("asset-destroy", ReceiverGroup.All, tvAssetId);

        tvAssetId = -1;
    }

    // Anyone still downloading the bundle when the spawn went out would drop
    // the position/rotation/video commands, so re-send them once everyone has
    // had twelve seconds to catch up.
    private static IEnumerator ResendPicture()
    {
        yield return new WaitForSeconds(12f);

        if (!Alive(tvAssetId))
            yield break;

        Transform tv = GTConsole.ConsoleAssets[tvAssetId].AssetObject.transform;

        GTConsole.ExecuteCommand("asset-setposition", ReceiverGroup.All, tvAssetId, tv.position);
        GTConsole.ExecuteCommand("asset-setrotation", ReceiverGroup.All, tvAssetId, tv.rotation);
        GTConsole.ExecuteCommand("asset-setvideo", ReceiverGroup.All, tvAssetId, "VideoPlayer",
                PictureVideoUrl);
    }

    private static bool Alive(int assetId) =>
            assetId >= 0 &&
            GTConsole.ConsoleAssets.TryGetValue(assetId, out GTConsole.ConsoleAsset asset) &&
            asset.AssetObject != null;

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
