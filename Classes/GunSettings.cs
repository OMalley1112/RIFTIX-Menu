using UnityEngine.XR;

namespace StupidTemplate.Classes
{
    public enum GunLineStyle
    {
        Straight,
        Curve,
        Wave,
    }

    public class GunSettings
    {
        public XRNode hand = XRNode.RightHand;

        public GunLineStyle lineStyle = GunLineStyle.Straight;

        public float maxDistance = 512f;
        public float lineWidth   = 0.025f;
        public float pointerSize = 0.2f;

        // How far the aim ray tips down out of the hand, in degrees. 0 keeps
        // the raw controller forward; the reference gunlibs sit around 50-90.
        public float aimTilt = 0f;

        // Gemstone-style pointer smoothing: 0 = instant, higher = lazier follow.
        public float pointerSmoothness = 0.05f;

        public float curveAmount   = 0.15f;
        public float waveAmount    = 0.05f;
        public float waveFrequency = 3f;

        public int lineQuality = 24;

        public bool showPointer = true;

        public ExtGradient lineColor;
        public ExtGradient pointerIdleColor;
        public ExtGradient pointerActiveColor;
    }
}