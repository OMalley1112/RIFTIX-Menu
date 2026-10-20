using UnityEngine;
using UnityEngine.XR;
using static StupidTemplate.Menu.Main;

namespace StupidTemplate.Mods;

// Shared gun helpers, RIFTIX's RenderGun replaces Gemstone's GunLib.
public abstract class GunHelper
{
    public static bool Trigger =>
            ControllerInputPoller.TriggerFloat(XRNode.RightHand) > 0.5f;

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
