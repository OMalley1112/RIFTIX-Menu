using GorillaLocomotion;
using UnityEngine;

namespace StupidTemplate.Mods;

// Overpowered mods: big local power trips that only touch your own player.
// Nothing in here sends a network command - other players just see you
// moving like a maniac.
public abstract class Overpowered
{
    // ---- Mega Jump -------------------------------------------------------

    public static void MegaJump()
    {
        GTPlayer.Instance.maxJumpSpeed   = 16f;
        GTPlayer.Instance.jumpMultiplier = 6.5f;
    }

    // ---- Titan Speed -----------------------------------------------------

    // Speed Boost's big brother: the boost values plus a constant shove
    // in the direction you are already travelling.
    public static void TitanSpeed()
    {
        GTPlayer.Instance.maxJumpSpeed   = 12f;
        GTPlayer.Instance.jumpMultiplier = 6f;

        Rigidbody rb  = GorillaTagger.Instance.rigidbody;
        Vector3   vel = rb.linearVelocity;

        if (vel.sqrMagnitude > 0.01f)
            rb.MovePosition(rb.position + vel.normalized * (7.5f * Time.deltaTime * GTPlayer.Instance.scale));
    }

    // ---- Giant Arms ------------------------------------------------------

    public static void GiantArms()
    {
        if (VRRig.LocalRig != null)
            GTPlayer.Instance.transform.localScale = Vector3.one * (VRRig.LocalRig.NativeScale * 1.6f);
    }

    public static void DisableGiantArms()
    {
        if (VRRig.LocalRig != null)
            GTPlayer.Instance.transform.localScale = Vector3.one * VRRig.LocalRig.NativeScale;
    }

    // ---- Moon Gravity ----------------------------------------------------

    // Cancels most of the fall so every hop floats for seconds.
    private static readonly Vector3 moonLift = new(0f, 7.4f, 0f);

    public static void MoonGravity()
    {
        GorillaTagger.Instance.rigidbody.AddForce(moonLift, ForceMode.Acceleration);
    }

    // ---- Super Fly -------------------------------------------------------

    // Joystick Fly with the limiter ripped off.
    public static void SuperFly()
    {
        GorillaTagger tagger = GorillaTagger.Instance;
        Rigidbody     rb     = tagger.rigidbody;

        rb.linearVelocity = Vector3.zero;
        rb.AddForce(-Physics.gravity, ForceMode.Acceleration);

        Vector2 joyl            = ControllerInputPoller.instance.leftControllerPrimary2DAxis;
        Vector2 joyr            = ControllerInputPoller.instance.rightControllerPrimary2DAxis;
        float   speedMultiplier = Time.deltaTime * 34f;

        if (joyl.magnitude > 0.4f)
        {
            Transform bodyTransform = tagger.bodyCollider.transform;
            GTPlayer.Instance.transform.position += bodyTransform.forward * (joyl.y * speedMultiplier) +
                                                    bodyTransform.right   * (joyl.x * speedMultiplier);
        }

        if (joyr.magnitude > 0.4f)
            GTPlayer.Instance.transform.position += tagger.bodyCollider.transform.up * (joyr.y * speedMultiplier);
    }

    // ---- Rocket Rise -----------------------------------------------------

    // Hold A and you rocket straight up through the map.
    public static void RocketRise()
    {
        bool a = ControllerInputPoller.instance.rightControllerPrimaryButton;

        if (a)
        {
            Rigidbody rb = GorillaTagger.Instance.rigidbody;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 14f, rb.linearVelocity.z);
        }
    }

    // ---- Air Boost -------------------------------------------------------

    // Hold grip and you blast forward along your hand's line at full tilt.
    public static void AirBoost()
    {
        if (!ControllerInputPoller.instance.rightGrab)
            return;

        Transform hand = GorillaTagger.Instance.rightHandTransform;

        Rigidbody rb = GorillaTagger.Instance.rigidbody;
        rb.linearVelocity = hand.forward * 22f;
    }
}
