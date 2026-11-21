using UnityEngine.XR;

namespace StupidTemplate.Classes
{
    // New styles are appended so older saved configs (stored by number) still load correctly.
    public enum GunLineStyle
    {
        Straight,
        Curve,
        Wave,
        Rope,
        Flare,
        Tentacle,
        Lightning,
        Helix,
    }

    public class GunSettings
    {
        public XRNode hand = XRNode.RightHand;

        public GunLineStyle lineStyle = GunLineStyle.Straight;

        public float maxDistance = 512f;
        public float lineWidth   = 0.025f;
        public float pointerSize = 0.2f;

        public float curveAmount   = 0.15f;
        public float waveAmount    = 0.05f;
        public float waveFrequency = 3f;

        // How fast the curve's middle point trails behind your hand (higher = tighter).
        public float lineSmoothing = 6f;

        // 0 = pointer snaps to the hit point, higher = pointer glides to it.
        public float pointerSmoothing = 0f;

        // How much the pointer breathes in and out (0 = off).
        public float pointerPulse = 0.1f;

        public int lineQuality = 24;

        public bool showPointer     = true;
        public bool showRing        = true;
        public bool flowingGradient = true;

        public ExtGradient lineColor;
        public ExtGradient pointerIdleColor;
        public ExtGradient pointerActiveColor;
    }
}
