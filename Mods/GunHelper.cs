using UnityEngine;
using UnityEngine.XR;
using static StupidTemplate.Menu.Main;
using static StupidTemplate.Menu.Settings;

namespace StupidTemplate.Mods;

// Shared gun helpers, RIFTIX's RenderGun replaces Gemstone's GunLib.
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
            RaycastHit ray = RenderGun().Ray;

            if (ray.collider == null)
                return null;

            VRRig rig = ray.collider.GetComponentInParent<VRRig>();

            if (rig == null || rig.isLocal)
                return null;

            return rig;
        }
    }
}
