using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static StupidTemplate.Menu.Settings;

namespace StupidTemplate.Classes
{
    // Text that paints its glyphs with an animated horizontal gradient instead
    // of a flat colour - the "TEXT GRADIENT" button look. The gradient sweeps
    // across the actual glyph bounds so short titles still show the full range.
    public class GradientText : Text
    {
        public Color colorA = Color.white;
        public Color colorB = Color.white;

        private static readonly List<GradientText> active = new();

        private static float nextTick;
        private static float phase;

        // Called from the menu update loop. Repaints all live gradient texts at
        // a throttled rate so the sweep stays cheap.
        public static void Tick()
        {
            if (!textGradient)
                return;

            if (Time.unscaledTime < nextTick)
                return;

            nextTick = Time.unscaledTime + 0.1f;

            phase += 0.1f * gradientSpeed;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                GradientText text = active[i];

                if (text == null)
                {
                    active.RemoveAt(i);
                    continue;
                }

                text.SetVerticesDirty();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (!active.Contains(this))
                active.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            active.Remove(this);
        }

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            base.OnPopulateMesh(toFill);

            if (!textGradient)
                return;

            int count = toFill.currentVertCount;

            if (count == 0)
                return;

            UIVertex vertex = new();

            // First pass: find the glyph bounds so the gradient spans the text
            // itself rather than the whole rect.
            float minX = float.MaxValue;
            float maxX = float.MinValue;

            for (int i = 0; i < count; i++)
            {
                toFill.PopulateUIVertex(ref vertex, i);

                float x = vertex.position.x;

                if (x < minX)
                    minX = x;

                if (x > maxX)
                    maxX = x;
            }

            float span = maxX - minX;

            if (span < 0.0001f)
                span = 0.0001f;

            for (int i = 0; i < count; i++)
            {
                toFill.PopulateUIVertex(ref vertex, i);

                float u = (vertex.position.x - minX) / span;

                // Ping-pong so the highlight sweeps right, then back again.
                float t = Mathf.PingPong(phase + u, 1f);

                Color color = Color.Lerp(colorA, colorB, t);

                // Keep per-vertex alpha (rich text tags, shadow fades, ...).
                color.a = vertex.color.a;

                vertex.color = color;

                toFill.SetUIVertex(vertex, i);
            }
        }
    }
}
