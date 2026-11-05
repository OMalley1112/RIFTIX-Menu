using System.Collections.Generic;
using UnityEngine;
using static StupidTemplate.Menu.Main;
using static StupidTemplate.Menu.Settings;

namespace StupidTemplate.Classes
{
    public class Button : MonoBehaviour
    {

        public static float      buttonCooldown;
        public        bool       incremental;
        public        bool       positive;
        public        ButtonInfo relatedButton;

        // Squash-and-pop-back animation played on the pressed button.
        private const float PopDuration = 0.18f;
        private const float PopAmount   = 0.10f;

        private sealed class PendingPop
        {
            public GameObject obj;
            public Vector3    baseScale;
            public float      start;
        }

        private static readonly List<PendingPop> pops = new();

        public void OnTriggerEnter(Collider collider)
        {
            if (Time.time     <= buttonCooldown ||
                menu          == null           ||
                relatedButton == null)
                return;

            if (!TryGetReferenceHand(
                        collider,
                        out bool pressingRightHand))
                return;

            buttonCooldown =
                    Time.time +
                    0.2f;

            GorillaTagger.Instance.StartVibration(
                    pressingRightHand,
                    GorillaTagger.Instance.tagHapticStrength / 2f,
                    GorillaTagger.Instance.tagHapticDuration / 2f);

            VRRig.LocalRig.PlayHandTapLocal(
                    8,
                    pressingRightHand,
                    0.4f);

            bool? incrementDirection =
                    incremental
                            ? positive
                            : (bool?)null;

            Toggle(
                    relatedButton,
                    incrementDirection);

            StartPop(gameObject);
        }

        // Records the press so TickPops can animate the squash and return.
        public static void StartPop(GameObject target)
        {
            if (target == null || !buttonPop)
                return;

            for (int i = 0; i < pops.Count; i++)
            {
                if (pops[i].obj == target)
                {
                    // Already popping - restart the return without re-capturing
                    // the (already squashed) scale.
                    pops[i].start = Time.unscaledTime;
                    return;
                }
            }

            pops.Add(
                    new PendingPop
                    {
                            obj       = target,
                            baseScale = target.transform.localScale,
                            start     = Time.unscaledTime,
                    });
        }

        // Called from the menu update loop: shrinks the pressed button on
        // contact, then eases it back to its original scale.
        public static void TickPops()
        {
            if (pops.Count == 0)
                return;

            float now = Time.unscaledTime;

            for (int i = pops.Count - 1; i >= 0; i--)
            {
                PendingPop pop = pops[i];

                if (pop.obj == null)
                {
                    pops.RemoveAt(i);
                    continue;
                }

                float t =
                        (now - pop.start) /
                        PopDuration;

                if (!buttonPop || t >= 1f)
                {
                    pop.obj.transform.localScale = pop.baseScale;
                    pops.RemoveAt(i);
                    continue;
                }

                // 90% scale at contact, eased back up to 100%.
                float squash =
                        1f -
                        PopAmount * (1f - t) * (1f - t);

                pop.obj.transform.localScale =
                        pop.baseScale * squash;
            }
        }
    }
}
