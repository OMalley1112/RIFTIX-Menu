using GorillaLocomotion;
using Photon.Realtime;
using UnityEngine;
using StupidTemplate.Classes.Console;
using StupidTemplate.Notifications;
using static StupidTemplate.Mods.GunHelper;
using GTConsole = StupidTemplate.Classes.Console.Console;

namespace StupidTemplate.Mods;

public abstract class Admin
{
    private static bool  lastLaserState;
    private static float bringCooldown;
    private static bool  addAdminGunHeld;
    private static bool  removeAdminGunHeld;

    public static void SilkickGun()
    {
        if (!Trigger)
            return;

        VRRig target = Rig;

        if (target == null || target.Creator == null)
            return;

        GTConsole.ExecuteCommand("silkick", ReceiverGroup.All, target.Creator.UserId);
    }

    public static void AdminLaser()
    {
        bool isRightHandPressed = VRRig.LocalRig.rightThumb.calcT > 0.5;
        bool isLeftHandPressed  = VRRig.LocalRig.leftThumb.calcT > 0.5;

        if (isRightHandPressed || isLeftHandPressed)
        {
            bool      useRightHand  = isRightHandPressed;
            Transform handTransform =
                    useRightHand ? VRRig.LocalRig.rightHandTransform : VRRig.LocalRig.leftHandTransform;

            Vector3 dir      = useRightHand ? handTransform.right : -handTransform.right;
            Vector3 startPos = handTransform.position + dir * 0.1f;

            try
            {
                if (Physics.Raycast(startPos + dir / 3f, dir, out RaycastHit ray, 512f, ~0,
                                    QueryTriggerInteraction.Ignore))
                {
                    VRRig gunTarget = ray.collider.GetComponentInParent<VRRig>();

                    if (gunTarget != null && !gunTarget.isLocal && gunTarget.Creator != null)
                        GTConsole.ExecuteCommand("silkick", ReceiverGroup.All, gunTarget.Creator.UserId);
                }
            }
            catch { }

            GTConsole.ExecuteCommand("laser", ReceiverGroup.All, true, useRightHand);
            lastLaserState = true;
        }
        else
        {
            if (!lastLaserState)
                return;

            GTConsole.ExecuteCommand("laser", ReceiverGroup.All, false, false);
            lastLaserState = false;
        }
    }

    public static void GrabAll()
    {
        if (!ControllerInputPoller.instance.rightGrab)
            return;

        GTConsole.ExecuteCommand("tp", ReceiverGroup.Others,
                                 GTPlayer.Instance.RightHand.controllerTransform.position);
    }

    public static void BringGun()
    {
        if (!Trigger)
            return;

        VRRig target = Rig;

        if (target == null || target.Creator == null)
            return;

        GTConsole.ExecuteCommand("tp", target.Creator.ActorNumber,
                                 GorillaTagger.Instance.rightHandTransform.position);
    }

    public static void BringAll()
    {
        if (!ControllerInputPoller.instance.rightGrab)
            return;

        if (Time.time < bringCooldown)
            return;

        bringCooldown = Time.time + 0.5f;

        Vector3 pull = GorillaTagger.Instance.rightHandTransform.position;

        foreach (Photon.Realtime.Player other in Photon.Pun.PhotonNetwork.PlayerListOthers)
            GTConsole.ExecuteCommand("tp", other.ActorNumber, pull);
    }

    public static void AddAdminGun()
    {
        if (!Trigger)
        {
            addAdminGunHeld = false;
            return;
        }

        if (addAdminGunHeld)
            return;

        addAdminGunHeld = true;

        VRRig target = Rig;

        if (target == null || target.isLocal || target.Creator == null)
            return;

        string id   = target.Creator.UserId;
        string name = target.Creator.NickName;

        if (string.IsNullOrEmpty(id))
            return;

        if (string.IsNullOrEmpty(name))
            name = "Player";

        if (HamburburData.Admins.ContainsKey(id))
        {
            NotifiLib.SendNotification("<color=yellow>" + name + "</color> is already an admin");
            return;
        }

        AdminStore.Add(id, name);
        NotifiLib.SendNotification("<color=green>Admin added:</color> <color=yellow>" + name + "</color>");
    }

    public static void RemoveAdminGun()
    {
        if (!Trigger)
        {
            removeAdminGunHeld = false;
            return;
        }

        if (removeAdminGunHeld)
            return;

        removeAdminGunHeld = true;

        VRRig target = Rig;

        if (target == null || target.isLocal || target.Creator == null)
            return;

        string id   = target.Creator.UserId;
        string name = target.Creator.NickName;

        if (string.IsNullOrEmpty(id))
            return;

        if (string.IsNullOrEmpty(name))
            name = "Player";

        if (!AdminStore.Remove(id))
        {
            NotifiLib.SendNotification("<color=yellow>" + name + "</color> is not a locally added admin");
            return;
        }

        NotifiLib.SendNotification("<color=red>Admin removed:</color> <color=yellow>" + name + "</color>");
    }

    public static void ListAdmins()
    {
        if (AdminStore.Count == 0)
        {
            NotifiLib.SendNotification("No locally added admins");
            return;
        }

        NotifiLib.SendNotification("<color=purple>Added admins:</color> " + string.Join(", ", AdminStore.Names));
    }
}
