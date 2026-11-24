using StupidTemplate.Menu;
using UnityEngine;
using UnityEngine.XR;
using static StupidTemplate.Menu.Main;
using static StupidTemplate.Menu.Settings;

namespace StupidTemplate.Mods;

// Shared gun helpers, built on the GunLib aim line.
public abstract class GunHelper
{
    // Gemstone-style: the gun fires while the grip on the aiming hand is held.
    public static bool Trigger =>
            gunSettings.hand == XRNode.RightHand
                    ? ControllerInputPoller.instance.rightGrab
                    : ControllerInputPoller.instance.leftGrab;

    // The precision guns (teleport, grapple, paint) fire on the trigger so
    // you can aim without setting them off - same as Teleport Gun.
    public static bool AimTrigger =>
            ControllerInputPoller.TriggerFloat(gunSettings.hand) > 0.5f;

    public static VRRig Rig
    {
        get
        {
            RaycastHit ray = GunLib.Render().Ray;

            if (ray.collider == null)
                return null;

            VRRig rig = ray.collider.GetComponentInParent<VRRig>();

            if (rig == null || rig.isLocal)
                return null;

            return rig;
        }
    }

    // The lock target: once grip AND trigger were held over a player the gun stays locked
    // onto them until both are released (Zeno-style lock-on), so aim drift never breaks
    // the shot. Falls back to the plain aim ray when nothing is locked.
    public static VRRig AimRig
    {
        get
        {
            // Refreshes HoveredRig/LockedRig for this frame first.
            GunLib.Render();

            VRRig locked = GunLib.LockedRig;

            if (locked != null && !locked.isLocal && locked.gameObject.activeInHierarchy)
                return locked;

            return Rig;
        }
    }
}
