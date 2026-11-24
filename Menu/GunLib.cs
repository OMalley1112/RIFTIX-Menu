using GorillaLocomotion;
using StupidTemplate.Classes;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace StupidTemplate.Menu;

// GunLib: everything gun related lives here.
//
//   GunLib.Render()          draws the line, pointer and ring, and returns (Ray, Pointer).
//   GunLib.GetInput(false)   is the gun equipped? (grip in VR, right mouse on PC)
//   GunLib.GetInput(true)    is the gun firing?   (trigger in VR, left mouse on PC)
//   GunLib.HoveredRig        the player the gun is currently pointing at (or null).
//   GunLib.LockedRig         the player the gun is locked on to (or null). Grip + trigger over a
//                            player locks on; the lock then follows that player wherever they go
//                            until BOTH grip and trigger are let go. A purple tracer runs from
//                            your hand to the locked body while locked.
//   GunLib.ClearLock()       drops the lock right away.
//   GunLib.BodyPoint(rig)    chest position of a rig, used for the lock end and the tracer.
//   GunLib.EndFrame()        called once per frame by Main, hides the gun when nobody rendered it.
//
// To add a new line style:
//   1. Add a value to GunLineStyle (at the END of the enum so saved configs stay valid).
//   2. Add a case in BuildLine() that fills the `points` array and returns how many points it used.
//   3. (Optional) pick a width shape for it in GetWidthCurve().
public static class GunLib
{
    private const int MaxPoints     = 96;
    private const int RopePoints    = 40;
    private const int RingPoints    = 32;
    private const int GradientKeys  = 8;
    private const int RaycastBuffer = 64;

    // ---------------------------------------------------------------- public state

    public static VRRig   HoveredRig { get; private set; }
    public static VRRig   LockedRig  { get; private set; }
    public static Vector3 AimOrigin  { get; private set; }
    public static Vector3 AimDirection { get; private set; }
    public static Vector3 LastEnd    { get; private set; }

    // ---------------------------------------------------------------- objects

    private static GameObject   pointer;
    private static Renderer     pointerRenderer;
    private static LineRenderer line;
    private static LineRenderer ring;
    private static LineRenderer tracer;

    // Purple lock-on tracer.
    private static readonly Color TracerColor = new(0.62f, 0.16f, 1f, 1f);
    private const float TracerWidth = 0.02f;

    private static readonly Vector3[]   points    = new Vector3[MaxPoints];
    private static readonly RaycastHit[] hitBuffer = new RaycastHit[RaycastBuffer];

    // ---------------------------------------------------------------- state

    private static int     lastRenderFrame = -10;
    private static bool    hasState;
    private static Vector3 smoothMid;
    private static Vector3 smoothPointer;
    private static bool    previousFire;
    private static float   clickPulse;
    private static int     appliedWidthStyle = -1;

    // Lock-on (ported from Zeno's GunLib): the player we are locked onto. Stays set until
    // grip AND trigger are both released, so the gun keeps tracking them while you aim away.
    private static VRRig   lockRig;
    private static bool    wasLocked;

    // rope
    private static readonly Vector3[] ropePos  = new Vector3[RopePoints];
    private static readonly Vector3[] ropePrev = new Vector3[RopePoints];
    private static bool               ropeReady;

    // lightning
    private static readonly Vector2[] lightning     = new Vector2[MaxPoints];
    private static float              nextLightning;

    // colour
    private static readonly Gradient         gradient   = new();
    private static readonly GradientColorKey[] colorKeys = new GradientColorKey[GradientKeys];
    private static readonly GradientAlphaKey[] alphaKeys = { new(1f, 0f), new(1f, 1f), };

    // width shapes
    private static readonly AnimationCurve flatWidth  = AnimationCurve.Constant(0f, 1f, 1f);
    private static readonly AnimationCurve flareWidth = AnimationCurve.Linear(0f, 0.4f, 1f, 3f);
    private static readonly AnimationCurve taperWidth = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f);

    private static GameObject shoulderCamera;

    // ---------------------------------------------------------------- input

    public static bool GetInput(bool fire, GunSettings settings = null)
    {
        settings ??= Settings.gunSettings;

        if (XRSettings.isDeviceActive)
        {
            if (fire)
                return ControllerInputPoller.TriggerFloat(settings.hand) > 0.5f;

            return settings.hand == XRNode.LeftHand
                           ? ControllerInputPoller.instance.leftGrab
                           : ControllerInputPoller.instance.rightGrab;
        }

        Mouse mouse = Mouse.current;

        if (mouse == null)
            return false;

        return fire ? mouse.leftButton.isPressed : mouse.rightButton.isPressed;
    }

    // ---------------------------------------------------------------- render

    public static (RaycastHit Ray, GameObject NewPointer) Render(
            int?        overrideLayerMask = null,
            GunSettings settings          = null)
    {
        settings ??= Settings.gunSettings;

        // If the gun wasn't drawn last frame it was put away, so start fresh (no smoothing from the old position).
        if (Time.frameCount - lastRenderFrame > 1)
            ResetState();

        lastRenderFrame = Time.frameCount;

        EnsureObjects();

        GetAim(settings, out Vector3 lineStart, out Vector3 rayOrigin, out Vector3 direction, out Vector3 handUp);

        AimOrigin    = lineStart;
        AimDirection = direction;

        bool didHit = Raycast(rayOrigin, direction, settings.maxDistance, overrideLayerMask, out RaycastHit ray);

        // What are we pointing at?
        VRRig hoveredRig = null;

        if (didHit && ray.collider != null)
        {
            hoveredRig = ray.collider.GetComponentInParent<VRRig>();

            if (hoveredRig != null && hoveredRig.isLocal)
                hoveredRig = null;
        }

        HoveredRig = hoveredRig;

        bool fireHeld = GetInput(true, settings);
        bool gripHeld = GetInput(false, settings);

        // Lock-on: grip + trigger over a player locks onto them. The lock then follows that
        // player no matter how they move or what you aim at, and only ends once grip AND
        // trigger are both released (also checked every frame in EndFrame).
        if (lockRig != null && (!lockRig.gameObject.activeInHierarchy || lockRig.isLocal))
            ClearLock();

        if (!gripHeld && !fireHeld)
            ClearLock();
        else if (lockRig == null && gripHeld && fireHeld && hoveredRig != null)
            lockRig = hoveredRig;

        VRRig lockedRig = lockRig;

        LockedRig = lockedRig;

        bool locked = lockedRig != null;

        // A short buzz the moment the lock catches.
        if (locked && !wasLocked && GorillaTagger.Instance != null)
        {
            GorillaTagger.Instance.StartVibration(
                    settings.hand == XRNode.LeftHand,
                    GorillaTagger.Instance.tapHapticStrength,
                    GorillaTagger.Instance.tapHapticDuration);
        }

        wasLocked = locked;

        Vector3 end;

        if (lockedRig != null)
            end = BodyPoint(lockedRig);
        else if (didHit)
            end = ray.point;
        else
            end = rayOrigin + direction * settings.maxDistance;

        // Pointer position (optionally smoothed).
        if (!hasState)
        {
            smoothPointer = end;
            smoothMid     = (lineStart + end) * 0.5f;
            ropeReady     = false;
            hasState      = true;
        }

        if (settings.pointerSmoothing > 0f)
        {
            float k = 1f - Mathf.Exp(-settings.pointerSmoothing * Time.deltaTime);
            smoothPointer = Vector3.Lerp(smoothPointer, end, k);
        }
        else
            smoothPointer = end;

        Vector3 pointerPos = smoothPointer;

        LastEnd = pointerPos;

        // Trigger feedback.
        bool fire    = locked || fireHeld;
        bool clicked = fire && !previousFire;

        previousFire = fire;

        if (clicked)
            clickPulse = 1f;

        clickPulse = Mathf.MoveTowards(clickPulse, 0f, Time.deltaTime * 3f);

        // Colours.
        ExtGradient idleColors   = settings.pointerIdleColor   ?? Settings.buttonColors[0];
        ExtGradient activeColors = settings.pointerActiveColor ?? Settings.buttonColors[1];

        Color idle   = idleColors.GetCurrentColor();
        Color active = activeColors.GetCurrentColor();

        Color pointerColor = fire
                                     ? active
                                     : hoveredRig != null
                                             ? Color.Lerp(idle, active, 0.5f)
                                             : idle;

        // Pointer.
        pointer.SetActive(true);
        pointer.transform.position = pointerPos;

        float size = settings.pointerSize;

        size *= 1f + Mathf.Sin(Time.time * 6f) * settings.pointerPulse;
        size *= fire ? 0.8f : hoveredRig != null ? 1.15f : 1f;

        pointer.transform.localScale = Vector3.one * Mathf.Max(size, 0.001f);

        pointerRenderer.enabled        = settings.showPointer;
        pointerRenderer.material.color = pointerColor;

        // Line.
        line.gameObject.SetActive(true);

        ApplyWidthStyle(settings.lineStyle);

        line.widthMultiplier = settings.lineWidth * (1f + clickPulse * 0.8f);

        int count = BuildLine(settings, lineStart, pointerPos, handUp);

        line.positionCount = count;
        line.SetPositions(points);

        ApplyLineColor(settings);

        // Ring.
        UpdateRing(settings, pointerPos, locked ? -direction : didHit ? ray.normal : -direction, pointerColor, size);

        // Purple tracer only while the gun is actually locked on (trigger held).
        // Hovering over a player does not draw it.
        if (lockedRig != null)
            DrawTracer(lockedRig, lineStart);
        else
            HideTracer();

        return (ray, pointer);
    }

    // Chest position of a rig. Used for the lock end and the tracer so both land on the body
    // instead of the player's feet.
    public static Vector3 BodyPoint(VRRig rig)
    {
        if (rig != null && rig.headMesh != null)
            return rig.headMesh.transform.position + Vector3.down * 0.3f;

        return rig != null ? rig.transform.position + Vector3.up * 1.2f : Vector3.zero;
    }

    // Purple line from the right hand to the body of the locked player.
    private static void DrawTracer(VRRig rig, Vector3 fallbackStart)
    {
        Transform hand = GorillaTagger.Instance != null ? GorillaTagger.Instance.rightHandTransform : null;

        if (rig == null || hand == null)
        {
            HideTracer();

            return;
        }

        EnsureTracer();

        tracer.gameObject.SetActive(true);
        tracer.SetPosition(0, hand.position);
        tracer.SetPosition(1, BodyPoint(rig));
    }

    private static void HideTracer()
    {
        if (tracer != null && tracer.gameObject.activeSelf)
            tracer.gameObject.SetActive(false);
    }

    private static void EnsureTracer()
    {
        if (tracer != null)
            return;

        tracer = CreateLine($"{Constants.Name}_GunTracer");

        Material tracerMaterial = new(Shader.Find("UI/Default") ?? Shader.Find("Sprites/Default"))
        {
                color       = TracerColor,
                mainTexture = Texture2D.whiteTexture,
                renderQueue = 4000,
        };

        tracerMaterial.SetInt("unity_GUIZTestMode", 8);

        tracer.material          = tracerMaterial;
        tracer.numCapVertices    = 2;
        tracer.numCornerVertices = 2;
        tracer.alignment         = LineAlignment.View;
        tracer.startWidth        = TracerWidth;
        tracer.endWidth          = TracerWidth;
        tracer.positionCount     = 2;
        tracer.startColor        = TracerColor;
        tracer.endColor          = TracerColor;

        tracer.gameObject.SetActive(false);
    }

    // Called once per frame by Main, before the mods run. Everything is switched off here and
    // switched back on by Render(), so the gun disappears the moment no mod is drawing it.
    public static void EndFrame()
    {
        if (pointer != null && pointer.activeSelf) pointer.SetActive(false);
        if (line    != null && line.gameObject.activeSelf) line.gameObject.SetActive(false);
        if (ring    != null && ring.gameObject.activeSelf) ring.gameObject.SetActive(false);
        if (tracer  != null && tracer.gameObject.activeSelf) tracer.gameObject.SetActive(false);

        // Runs every frame: once grip AND trigger are both let go, the lock is dropped.
        if (lockRig != null && !GetInput(false) && !GetInput(true))
            ClearLock();
    }

    // Drops the lock-on immediately (target gone, or the gun put away).
    public static void ClearLock()
    {
        lockRig   = null;
        LockedRig = null;
        wasLocked = false;
    }

    public static void Hide()
    {
        EndFrame();
        ResetState();
    }

    private static void ResetState()
    {
        hasState     = false;
        ropeReady    = false;
        previousFire = false;
        clickPulse   = 0f;
        HoveredRig   = null;
        LockedRig    = null;
        lockRig      = null;
        wasLocked    = false;

        HideTracer();
    }

    // ---------------------------------------------------------------- setup

    private static void EnsureObjects()
    {
        if (pointer == null)
        {
            pointer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pointer.name = $"{Constants.Name}_GunPointer";

            Object.Destroy(pointer.GetComponent<Collider>());

            pointerRenderer          = pointer.GetComponent<Renderer>();
            pointerRenderer.material = new Material(Shader.Find("GUI/Text Shader"));
        }

        if (line == null)
        {
            line = CreateLine($"{Constants.Name}_GunLine");
        }

        if (ring == null)
        {
            ring                      = CreateLine($"{Constants.Name}_GunRing");
            ring.positionCount        = RingPoints;
            ring.numCapVertices       = 2;
        }
    }

    private static LineRenderer CreateLine(string objectName)
    {
        GameObject lineObject = new(objectName);

        LineRenderer renderer = lineObject.AddComponent<LineRenderer>();

        renderer.useWorldSpace     = true;
        renderer.numCapVertices    = 4;
        renderer.numCornerVertices = 4;
        renderer.material          = new Material(Shader.Find("GUI/Text Shader"));
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows    = false;

        return renderer;
    }

    // ---------------------------------------------------------------- aiming

    private static void GetAim(
            GunSettings settings,
            out Vector3 lineStart,
            out Vector3 rayOrigin,
            out Vector3 direction,
            out Vector3 handUp)
    {
        Transform hand = settings.hand == XRNode.LeftHand
                                 ? GorillaTagger.Instance.leftHandTransform
                                 : GorillaTagger.Instance.rightHandTransform;

        lineStart = hand.position;
        handUp    = hand.up;

        if (XRSettings.isDeviceActive || Mouse.current == null)
        {
            direction = hand.forward;
            rayOrigin = lineStart + direction / 4f;

            return;
        }

        // Desktop: shoot from the camera towards the mouse.
        Camera cam = GetDesktopCamera();

        if (cam == null)
        {
            direction = hand.forward;
            rayOrigin = lineStart + direction / 4f;

            return;
        }

        Ray mouseRay = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

        rayOrigin = mouseRay.origin;
        direction = mouseRay.direction;
        handUp    = Vector3.up;
    }

    private static Camera GetDesktopCamera()
    {
        if (shoulderCamera == null)
            shoulderCamera = GameObject.Find("Shoulder Camera");

        if (shoulderCamera != null && shoulderCamera.activeInHierarchy)
        {
            Camera shoulder = shoulderCamera.GetComponent<Camera>();

            if (shoulder != null)
                return shoulder;
        }

        if (GorillaTagger.Instance.mainCamera != null)
        {
            Camera main = GorillaTagger.Instance.mainCamera.GetComponent<Camera>();

            if (main != null)
                return main;
        }

        return Camera.main;
    }

    private static bool Raycast(
            Vector3        origin,
            Vector3        direction,
            float          maxDistance,
            int?           overrideLayerMask,
            out RaycastHit hit)
    {
        if (overrideLayerMask.HasValue)
        {
            return Physics.Raycast(
                    origin,
                    direction,
                    out hit,
                    maxDistance,
                    overrideLayerMask.Value,
                    QueryTriggerInteraction.Ignore);
        }

        // Default: hit anything you can stand on, plus other players.
        int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                hitBuffer,
                maxDistance,
                ~0,
                QueryTriggerInteraction.Ignore);

        hit = default;

        float closest = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = hitBuffer[i];
            Collider   collider  = candidate.collider;

            if (collider == null || candidate.distance >= closest)
                continue;

            bool interactable =
                    ((1 << collider.gameObject.layer) & GTPlayer.Instance.locomotionEnabledLayers) != 0;

            VRRig rig = collider.GetComponentInParent<VRRig>();

            bool isOtherRig = rig != null && rig != VRRig.LocalRig;

            if (!interactable && !isOtherRig)
                continue;

            closest = candidate.distance;
            hit     = candidate;
        }

        return hit.collider != null;
    }

    // ---------------------------------------------------------------- line styles

    // Fills `points` and returns how many were used.
    private static int BuildLine(GunSettings settings, Vector3 start, Vector3 end, Vector3 handUp)
    {
        Vector3 delta  = end - start;
        float   length = delta.magnitude;

        if (length < 0.001f)
        {
            points[0] = start;
            points[1] = end;

            return 2;
        }

        Vector3 dir = delta / length;

        Basis(dir, handUp, out Vector3 right, out Vector3 up);

        int quality = Mathf.Clamp(settings.lineQuality, 4, MaxPoints);

        switch (settings.lineStyle)
        {
            case GunLineStyle.Curve:
            {
                // The middle of the curve trails behind your hand, so the line feels springy.
                Vector3 target = (start + end) * 0.5f + up * settings.curveAmount;
                float   k      = 1f - Mathf.Exp(-Mathf.Max(settings.lineSmoothing, 0.1f) * Time.deltaTime);

                smoothMid = Vector3.Lerp(smoothMid, target, k);

                for (int i = 0; i < quality; i++)
                {
                    float t  = i / (float)(quality - 1);
                    float it = 1f - t;

                    points[i] = it * it * start + 2f * it * t * smoothMid + t * t * end;
                }

                return quality;
            }

            case GunLineStyle.Wave:
            {
                for (int i = 0; i < quality; i++)
                {
                    float t    = i / (float)(quality - 1);
                    float fade = Mathf.Sin(t * Mathf.PI);
                    float wave = Mathf.Sin(t * Mathf.PI * 2f * settings.waveFrequency + Time.time * 8f)
                               * settings.waveAmount * fade;

                    points[i] = Vector3.Lerp(start, end, t) + right * wave;
                }

                return quality;
            }

            case GunLineStyle.Rope:
            {
                UpdateRope(start, end);

                for (int i = 0; i < RopePoints; i++)
                    points[i] = ropePos[i];

                return RopePoints;
            }

            case GunLineStyle.Flare:
            {
                points[0] = start;
                points[1] = end;

                return 2;
            }

            case GunLineStyle.Tentacle:
            {
                for (int i = 0; i < quality; i++)
                {
                    float t = i / (float)(quality - 1);

                    float sine    = Mathf.Sin(t * Mathf.PI * 3f   + Time.time * 3f);
                    float cosine  = Mathf.Cos(t * Mathf.PI * 2.5f + Time.time * 2.5f);
                    float shimmer = Mathf.Sin(t * Mathf.PI * 5f   + Time.time * 4f) * 0.5f;
                    float depth   = Mathf.Sin(t * Mathf.PI * 4f   + Time.time * 5f) * 0.04f;
                    float bow     = Mathf.Sin(t * Mathf.PI) * 0.1f * Mathf.Min(length, 20f);

                    Vector3 offset =
                            up    * (sine   * 0.1f  * (1f - t) + shimmer * 0.06f * t) +
                            right * (cosine * 0.08f * (1f - t) + bow + depth);

                    points[i] = Vector3.Lerp(start, end, t) + offset;
                }

                return quality;
            }

            case GunLineStyle.Lightning:
            {
                int n = Mathf.Clamp(quality, 8, MaxPoints);

                if (Time.time >= nextLightning)
                {
                    nextLightning = Time.time + 0.05f;

                    for (int i = 0; i < n; i++)
                        lightning[i] = Random.insideUnitCircle;
                }

                float strength = Mathf.Clamp(length * 0.012f, 0.03f, 0.25f);

                for (int i = 0; i < n; i++)
                {
                    float t    = i / (float)(n - 1);
                    float fade = Mathf.Sin(t * Mathf.PI);

                    points[i] = Vector3.Lerp(start, end, t)
                              + (right * lightning[i].x + up * lightning[i].y) * (strength * fade);
                }

                return n;
            }

            case GunLineStyle.Helix:
            {
                int   n     = Mathf.Clamp(quality * 2, 16, MaxPoints);
                float turns = Mathf.Clamp(length * 2f, 2f, 24f);

                for (int i = 0; i < n; i++)
                {
                    float t     = i / (float)(n - 1);
                    float fade  = Mathf.Sin(t * Mathf.PI);
                    float angle = t * turns * Mathf.PI * 2f + Time.time * 6f;

                    points[i] = Vector3.Lerp(start, end, t)
                              + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * (0.05f * fade);
                }

                return n;
            }

            default: // Straight
            {
                points[0] = start;
                points[1] = end;

                return 2;
            }
        }
    }

    private static void Basis(Vector3 dir, Vector3 upHint, out Vector3 right, out Vector3 up)
    {
        right = Vector3.Cross(dir, upHint);

        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.Cross(dir, Vector3.right);

        right.Normalize();

        up = Vector3.Cross(right, dir).normalized;
    }

    // Verlet rope with both ends pinned. Slack makes it sway and sag as you move.
    private static void UpdateRope(Vector3 start, Vector3 end)
    {
        int n = RopePoints;

        if (!ropeReady)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 p = Vector3.Lerp(start, end, i / (float)(n - 1));

                ropePos[i]  = p;
                ropePrev[i] = p;
            }

            ropeReady = true;
        }

        float dt      = Mathf.Min(Time.deltaTime, 0.033f);
        float segment = Vector3.Distance(start, end) * 1.06f / (n - 1);

        Vector3 gravity = Vector3.down * 2f;

        for (int i = 1; i < n - 1; i++)
        {
            Vector3 velocity = (ropePos[i] - ropePrev[i]) * 0.96f;

            ropePrev[i] =  ropePos[i];
            ropePos[i]  += velocity + gravity * (dt * dt);
        }

        ropePos[0]     = start;
        ropePos[n - 1] = end;

        for (int iteration = 0; iteration < 8; iteration++)
        {
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 delta    = ropePos[i + 1] - ropePos[i];
                float   distance = delta.magnitude;

                if (distance < 0.00001f)
                    continue;

                Vector3 correction = delta * ((distance - segment) / distance);

                bool pinnedA = i == 0;
                bool pinnedB = i + 1 == n - 1;

                if (pinnedA && pinnedB)
                    continue;

                if (pinnedA)
                    ropePos[i + 1] -= correction;
                else if (pinnedB)
                    ropePos[i] += correction;
                else
                {
                    ropePos[i]     += correction * 0.5f;
                    ropePos[i + 1] -= correction * 0.5f;
                }
            }
        }
    }

    private static void ApplyWidthStyle(GunLineStyle style)
    {
        if (appliedWidthStyle == (int)style)
            return;

        appliedWidthStyle = (int)style;

        line.widthCurve = style switch
                          {
                                  GunLineStyle.Flare    => flareWidth,
                                  GunLineStyle.Tentacle => taperWidth,
                                  var _                 => flatWidth,
                          };
    }

    // ---------------------------------------------------------------- colour

    private static void ApplyLineColor(GunSettings settings)
    {
        ExtGradient colors = settings.lineColor ?? Settings.backgroundColor;

        if (settings.flowingGradient)
        {
            // Sample the menu gradient along the line so the colours flow towards the pointer.
            for (int i = 0; i < GradientKeys; i++)
            {
                float t = i / (float)(GradientKeys - 1);

                colorKeys[i] = new GradientColorKey(
                        colors.GetCurrentColor(t * 0.5f),
                        t);
            }
        }
        else
        {
            Color flat = colors.GetCurrentColor();

            for (int i = 0; i < GradientKeys; i++)
                colorKeys[i] = new GradientColorKey(flat, i / (float)(GradientKeys - 1));
        }

        gradient.SetKeys(colorKeys, alphaKeys);

        line.colorGradient = gradient;
    }

    // ---------------------------------------------------------------- ring

    // A spinning arc around the pointer that flares out when you pull the trigger.
    private static void UpdateRing(
            GunSettings settings,
            Vector3     center,
            Vector3     normal,
            Color       color,
            float       pointerSize)
    {
        bool visible = settings.showRing && settings.showPointer;

        ring.gameObject.SetActive(visible);

        if (!visible)
            return;

        if (normal.sqrMagnitude < 0.0001f)
            normal = Vector3.up;

        Basis(normal.normalized, Vector3.up, out Vector3 right, out Vector3 up);

        float radius = pointerSize * 0.5f * 1.8f + clickPulse * pointerSize * 1.5f;

        float arc   = Mathf.PI * 2f * 0.7f;
        float start = Time.time * 3f;

        for (int i = 0; i < RingPoints; i++)
        {
            float angle = start + arc * (i / (float)(RingPoints - 1));

            ring.SetPosition(i, center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius);
        }

        ring.startColor      = color;
        ring.endColor        = new Color(color.r, color.g, color.b, 0f);
        ring.widthMultiplier = Mathf.Max(settings.lineWidth * 0.6f, 0.004f);
    }

    // ---------------------------------------------------------------- settings helpers (used by the menu buttons)

    private static readonly float[] LineWidths   = { 0.005f, 0.01f, 0.015f, 0.025f, 0.04f, 0.06f, };
    private static readonly float[] PointerSizes = { 0.05f, 0.1f, 0.15f, 0.2f, 0.3f, 0.4f, };

    public static string LineStyleName  => Settings.gunSettings.lineStyle.ToString();
    public static string LineWidthName  => Settings.gunSettings.lineWidth.ToString("0.###");
    public static string PointerSizeName => Settings.gunSettings.pointerSize.ToString("0.###");

    public static void ChangeLineStyle(bool increment)
    {
        int count = System.Enum.GetValues(typeof(GunLineStyle)).Length;
        int index = (int)Settings.gunSettings.lineStyle + (increment ? 1 : -1);

        if (index >= count)
            index = 0;
        else if (index < 0)
            index = count - 1;

        Settings.gunSettings.lineStyle = (GunLineStyle)index;

        ropeReady = false;
    }

    public static void ChangeLineWidth(bool increment) =>
            Settings.gunSettings.lineWidth = Step(LineWidths, Settings.gunSettings.lineWidth, increment);

    public static void ChangePointerSize(bool increment) =>
            Settings.gunSettings.pointerSize = Step(PointerSizes, Settings.gunSettings.pointerSize, increment);

    public static void SetLeftHanded(bool left) =>
            Settings.gunSettings.hand = left ? XRNode.LeftHand : XRNode.RightHand;

    public static bool IsLeftHanded => Settings.gunSettings.hand == XRNode.LeftHand;

    // Moves to the next/previous preset, starting from whichever preset is closest to the current value.
    private static float Step(float[] table, float current, bool increment)
    {
        int nearest = 0;

        for (int i = 1; i < table.Length; i++)
        {
            if (Mathf.Abs(table[i] - current) < Mathf.Abs(table[nearest] - current))
                nearest = i;
        }

        nearest += increment ? 1 : -1;

        if (nearest >= table.Length)
            nearest = 0;
        else if (nearest < 0)
            nearest = table.Length - 1;

        return table[nearest];
    }
}
