using System.Collections.Generic;
using BepInEx;
using GorillaLocomotion;
using Photon.Pun;
using StupidTemplate.Classes;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using static StupidTemplate.Menu.Main;

namespace StupidTemplate.Mods;

public abstract class Movement
{

    private static GameObject platl;
    private static GameObject platr;

    public static bool previousTeleportTrigger;
    public static void Fly()
    {
        if (!ControllerInputPoller.instance.rightControllerPrimaryButton)
            return;

        GTPlayer.Instance.transform.position            += GorillaTagger.Instance.headCollider.transform.forward * Time.deltaTime * Settings.Movement.flySpeed;
        GorillaTagger.Instance.rigidbody.linearVelocity =  Vector3.zero;
    }

    public static void Platforms()
    {
        bool leftGrip =
                ControllerInputPoller.instance.leftGrab;

        bool rightGrip =
                ControllerInputPoller.instance.rightGrab;

        if (leftGrip)
        {
            if (platl == null)
            {
                platl =
                        GameObject.CreatePrimitive(
                                PrimitiveType.Cube);

                platl.transform.localScale =
                        new Vector3(
                                0.025f,
                                0.3f,
                                0.4f);

                platl.transform.position =
                        TrueLeftHand().position + TrueLeftHand().right * 0.05f;

                platl.transform.rotation =
                        TrueLeftHand().rotation;

                FixStickyColliders(
                        platl);

                ColorChanger colorChanger =
                        platl.AddComponent<ColorChanger>();

                colorChanger.colors =
                        Menu.Settings.backgroundColor;
            }
        }
        else if (platl != null)
        {
            Object.Destroy(
                    platl);

            platl =
                    null;
        }

        if (rightGrip)
        {
            if (platr == null)
            {
                platr =
                        GameObject.CreatePrimitive(
                                PrimitiveType.Cube);

                platr.transform.localScale =
                        new Vector3(
                                0.025f,
                                0.3f,
                                0.4f);

                platr.transform.position =
                        TrueRightHand().position - TrueRightHand().right * 0.05f;

                platr.transform.rotation =
                        TrueRightHand().rotation;

                FixStickyColliders(
                        platr);

                ColorChanger colorChanger =
                        platr.AddComponent<ColorChanger>();

                colorChanger.colors =
                        Menu.Settings.backgroundColor;
            }
        }
        else if (platr != null)
        {
            Object.Destroy(
                    platr);

            platr =
                    null;
        }
    }
    public static void TeleportGun()
    {
        if (!ControllerInputPoller.instance.rightGrab)
            return;

        (RaycastHit Ray, GameObject NewPointer) GunData    = RenderGun();
        GameObject                              NewPointer = GunData.NewPointer;

        if (ControllerInputPoller.TriggerFloat(XRNode.RightHand) > 0.5f && !previousTeleportTrigger)
        {
            GTPlayer.Instance.TeleportTo(NewPointer.transform.position + Vector3.up, GTPlayer.Instance.transform.rotation);
            GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;
        }

        previousTeleportTrigger = ControllerInputPoller.TriggerFloat(XRNode.RightHand) > 0.5f;
    }

    public static void SpeedBoost()
    {
        GTPlayer.Instance.maxJumpSpeed   = 8f;
        GTPlayer.Instance.jumpMultiplier = 5.3f;
    }

    public static void LongArms()
    {
        if (VRRig.LocalRig != null)
            GTPlayer.Instance.transform.localScale = Vector3.one * (VRRig.LocalRig.NativeScale * 1.15f);
    }

    public static void UnLongArms()
    {
        if (VRRig.LocalRig != null)
            GTPlayer.Instance.transform.localScale = Vector3.one * VRRig.LocalRig.NativeScale;
    }

    public static void AmplifiedMonke()
    {
        Rigidbody rb  = GTPlayer.Instance.GetComponent<Rigidbody>();
        Vector3   vel = rb.linearVelocity;

        if (vel.sqrMagnitude > 0.001f)
            rb.MovePosition(rb.position + vel.normalized * (4.2f * Time.deltaTime * GTPlayer.Instance.scale));
    }

    private static readonly Vector3 gravityForce = new(0f, -8f, 0f);

    public static void GroundHelper()
    {
        if (!ControllerInputPoller.instance.rightGrab)
            return;

        GorillaTagger.Instance.rigidbody.AddForce(gravityForce, ForceMode.Acceleration);
        SpeedBoost();

        if (ControllerInputPoller.instance.rightControllerPrimaryButton)
        {
            Rigidbody rb  = GTPlayer.Instance.GetComponent<Rigidbody>();
            Vector3   vel = rb.linearVelocity;
            vel.y = 0f;

            if (vel.sqrMagnitude > 0.001f)
                rb.MovePosition(rb.position + vel.normalized * (3.9f * Time.deltaTime * GTPlayer.Instance.scale));
        }
    }

    public static void JoystickFly()
    {
        GorillaTagger tagger = GorillaTagger.Instance;
        Rigidbody     rb     = tagger.rigidbody;

        rb.linearVelocity = Vector3.zero;
        rb.AddForce(-Physics.gravity, ForceMode.Acceleration);

        Vector2 joyl            = ControllerInputPoller.instance.leftControllerPrimary2DAxis;
        Vector2 joyr            = ControllerInputPoller.instance.rightControllerPrimary2DAxis;
        float   speedMultiplier = Time.deltaTime * Settings.Movement.flySpeed;

        if (joyl.magnitude > 0.6f)
        {
            Transform bodyTransform = tagger.bodyCollider.transform;
            GTPlayer.Instance.transform.position += bodyTransform.forward * (joyl.y * speedMultiplier) +
                                                    bodyTransform.right   * (joyl.x * speedMultiplier);
        }

        if (joyr.magnitude > 0.6f)
            GTPlayer.Instance.transform.position += tagger.bodyCollider.transform.up * (joyr.y * speedMultiplier);
    }

    private static bool          noclipActive;
    private static bool          noclipFirstPass;
    private static float         noclipNextRefresh;
    private static readonly List<MeshCollider> noclipDisabled = [];

    public static void Noclip()
    {
        if (!noclipActive)
        {
            noclipActive     = true;
            noclipFirstPass  = false;
            noclipDisabled.Clear();
        }

        if (noclipFirstPass && Time.time < noclipNextRefresh)
            return;

        noclipFirstPass   = true;
        noclipNextRefresh = Time.time + 1f;

        foreach (MeshCollider collider in Resources.FindObjectsOfTypeAll<MeshCollider>())
        {
            if (collider == null || !collider.enabled)
                continue;

            if (!noclipDisabled.Contains(collider))
                noclipDisabled.Add(collider);

            collider.enabled = false;
        }
    }

    public static void DisableNoclip()
    {
        foreach (MeshCollider collider in noclipDisabled)
            if (collider != null)
                collider.enabled = true;

        noclipDisabled.Clear();
        noclipActive    = false;
        noclipFirstPass = false;
    }

    private static bool dashPressedUp;
    private static bool dashPressedForward;

    public static void Dash()
    {
        Vector3 forward = GorillaTagger.Instance.headCollider.transform.forward;

        if (ControllerInputPoller.instance.rightControllerPrimaryButton && !dashPressedUp)
        {
            GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;
            GorillaTagger.Instance.rigidbody.AddForce(Vector3.up * 18f, ForceMode.VelocityChange);
            dashPressedUp = true;
        }

        if (!ControllerInputPoller.instance.rightControllerPrimaryButton && dashPressedUp)
            dashPressedUp = false;

        if (ControllerInputPoller.instance.rightGrab && !dashPressedForward)
        {
            GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;
            GorillaTagger.Instance.rigidbody.AddForce(forward * 25f, ForceMode.VelocityChange);
            dashPressedForward = true;
        }

        if (!ControllerInputPoller.instance.rightGrab && dashPressedForward)
            dashPressedForward = false;
    }

    private static readonly string[] slingIgnoreLayers =
    {
            "Gorilla Trigger", "Gorilla Boundary", "GorillaHand", "GorillaObject", "Zone", "Water", "GorillaCosmetics",
            "GorillaParticle",
    };

    private static LineRenderer webLineLeft;
    private static LineRenderer webLineRight;
    private static Vector3      lastLeftPos;
    private static Vector3      lastRightPos;
    private static Vector3      leftHandVel;
    private static Vector3      rightHandVel;
    private static bool         leftLocked;
    private static bool         rightLocked;
    private static bool         leftActive;
    private static bool         rightActive;
    private static Vector3      leftAnchor;
    private static Vector3      rightAnchor;
    private static float        leftLength;
    private static float        rightLength;
    private static int          cachedSlingMask = -1;

    private static void InitializeSlingMask()
    {
        int mask = ~0;

        foreach (string layerName in slingIgnoreLayers)
        {
            int layer = LayerMask.NameToLayer(layerName);

            if (layer != -1)
                mask &= ~(1 << layer);
        }

        cachedSlingMask = mask;
    }

    public static void WebSlingers()
    {
        Transform left  = GTPlayer.Instance.LeftHand.controllerTransform;
        Transform right = GTPlayer.Instance.RightHand.controllerTransform;

        bool leftGrab  = ControllerInputPoller.instance.leftGrab;
        bool rightGrab = ControllerInputPoller.instance.rightGrab;

        Rigidbody rb = GTPlayer.Instance.GetComponent<Rigidbody>();

        if (rb == null)
            return;

        if (cachedSlingMask == -1)
            InitializeSlingMask();

        float dt = Time.deltaTime;

        if (dt > 0f)
        {
            leftHandVel  = (left.position  - lastLeftPos)  / dt;
            rightHandVel = (right.position - lastRightPos) / dt;
        }

        lastLeftPos  = left.position;
        lastRightPos = right.position;

        if (webLineLeft == null)
        {
            GameObject obj = new("RIFTIX_WebLeftHand");
            webLineLeft               = obj.AddComponent<LineRenderer>();
            webLineLeft.positionCount = 2;
            webLineLeft.startWidth    = 0.02f;
            webLineLeft.endWidth      = 0.02f;
            webLineLeft.material      = new Material(Shader.Find("Sprites/Default"));
        }

        if (webLineRight == null)
        {
            GameObject obj = new("RIFTIX_WebRightHand");
            webLineRight               = obj.AddComponent<LineRenderer>();
            webLineRight.positionCount = 2;
            webLineRight.startWidth    = 0.02f;
            webLineRight.endWidth      = 0.02f;
            webLineRight.material      = new Material(Shader.Find("Sprites/Default"));
        }

        Vector3 playerPos = GTPlayer.Instance.transform.position;

        if (leftGrab && !leftLocked)
            if (Physics.Raycast(left.position, left.forward, out RaycastHit hitL, Mathf.Infinity, cachedSlingMask))
            {
                leftLocked = true;
                leftActive = true;
                leftAnchor = hitL.point;
                leftLength = Vector3.Distance(playerPos, leftAnchor);
            }

        if (!leftGrab)
        {
            leftLocked          = false;
            leftActive          = false;
            webLineLeft.enabled = false;
        }

        if (rightGrab && !rightLocked)
            if (Physics.Raycast(right.position, right.forward, out RaycastHit hitR, Mathf.Infinity, cachedSlingMask))
            {
                rightLocked = true;
                rightActive = true;
                rightAnchor = hitR.point;
                rightLength = Vector3.Distance(playerPos, rightAnchor);
            }

        if (!rightGrab)
        {
            rightLocked          = false;
            rightActive          = false;
            webLineRight.enabled = false;
        }

        if (leftActive)
        {
            Vector3 toAnchor = leftAnchor - playerPos;
            float   dist     = toAnchor.magnitude;
            Vector3 dir      = toAnchor.normalized;

            if (dist > leftLength)
            {
                playerPos                            = leftAnchor - dir * leftLength;
                GTPlayer.Instance.transform.position = playerPos;

                Vector3 projected = Vector3.Project(rb.linearVelocity, dir);

                if (Vector3.Dot(projected, dir) > 0)
                    rb.linearVelocity -= projected;
            }

            rb.AddForce(Vector3.Cross(dir, Vector3.Cross(rb.linearVelocity, dir)), ForceMode.Acceleration);

            float pull = Vector3.Dot(leftHandVel, -dir);

            if (pull > 0f)
                rb.AddForce(dir * (pull * 50f), ForceMode.Acceleration);

            webLineLeft.enabled = true;
            webLineLeft.SetPosition(0, left.position);
            webLineLeft.SetPosition(1, leftAnchor);
        }

        if (rightActive)
        {
            Vector3 toAnchor = rightAnchor - playerPos;
            float   dist     = toAnchor.magnitude;
            Vector3 dir      = toAnchor.normalized;

            if (dist > rightLength)
            {
                GTPlayer.Instance.transform.position = rightAnchor - dir * rightLength;

                Vector3 projected = Vector3.Project(rb.linearVelocity, dir);

                if (Vector3.Dot(projected, dir) > 0)
                    rb.linearVelocity -= projected;
            }

            rb.AddForce(Vector3.Cross(dir, Vector3.Cross(rb.linearVelocity, dir)), ForceMode.Acceleration);

            float pull = Vector3.Dot(rightHandVel, -dir);

            if (pull > 0f)
                rb.AddForce(dir * (pull * 50f), ForceMode.Acceleration);

            webLineRight.enabled = true;
            webLineRight.SetPosition(0, right.position);
            webLineRight.SetPosition(1, rightAnchor);
        }

        rb.linearVelocity *= 0.995f;
        rb.linearVelocity =  Vector3.ClampMagnitude(rb.linearVelocity, 60f);
    }

    public static void DisableWebSlingers()
    {
        if (webLineLeft != null)
            webLineLeft.enabled = false;

        if (webLineRight != null)
            webLineRight.enabled = false;

        leftLocked  = false;
        rightLocked = false;
        leftActive  = false;
        rightActive = false;
    }

    private static GameObject   handTurnLineObject;
    private static LineRenderer handTurnLine;
    private static bool         handTurnGrabbing;
    private static float        handTurnInitialHandAngle;
    private static float        handTurnInitialPlayerAngle;

    public static void HandTurn()
    {
        if (!ControllerInputPoller.instance.rightGrab)
        {
            handTurnGrabbing = false;

            if (handTurnLine != null)
                handTurnLine.enabled = false;

            return;
        }

        const float downwardAngle  = 50f;
        Transform  handTransform   = GTPlayer.Instance.RightHand.controllerTransform;
        Transform  playerTransform = GTPlayer.Instance.transform;

        Vector3 rayDirection = Quaternion.AngleAxis(downwardAngle, handTransform.right) * handTransform.forward;

        if (handTurnLineObject == null)
        {
            handTurnLineObject         = new GameObject("RIFTIX_HandTurn_LineIndicator");
            handTurnLine               = handTurnLineObject.AddComponent<LineRenderer>();
            handTurnLine.startWidth    = 0.02f;
            handTurnLine.endWidth      = 0.02f;
            handTurnLine.positionCount = 2;
            handTurnLine.useWorldSpace = true;
            handTurnLine.material      = new Material(Shader.Find("Sprites/Default"));
            handTurnLine.startColor    = Color.red;
            handTurnLine.endColor      = Color.red;
        }

        handTurnLine.enabled = true;
        handTurnLine.SetPosition(0, handTransform.position);

        if (Physics.Raycast(handTransform.position, rayDirection, out RaycastHit hit, 100f))
        {
            handTurnLine.SetPosition(1, hit.point);

            Vector3 directionToHit = hit.point - playerTransform.position;
            directionToHit.y = 0f;

            float currentHandAngle = Mathf.Atan2(directionToHit.x, directionToHit.z) * Mathf.Rad2Deg;

            if (!handTurnGrabbing)
            {
                handTurnGrabbing           = true;
                handTurnInitialHandAngle   = currentHandAngle;
                handTurnInitialPlayerAngle = playerTransform.eulerAngles.y;
            }

            float angleDelta        = currentHandAngle   - handTurnInitialHandAngle;
            float targetPlayerAngle = handTurnInitialPlayerAngle + angleDelta;

            Quaternion targetRotation = Quaternion.Euler(0f, targetPlayerAngle, 0f);

            playerTransform.rotation =
                    Quaternion.Slerp(playerTransform.rotation, targetRotation, 15f * Time.deltaTime);
        }
        else
        {
            handTurnLine.SetPosition(1, handTransform.position + rayDirection * 100f);
            handTurnGrabbing = false;
        }
    }

    public static void TagGun()
    {
        VRRig target = GunHelper.Rig;

        if (!GunHelper.Trigger || target == null || target.Creator == null ||
            GorillaGameModes.GameMode.LocalIsTagged(target.Creator))
        {
            VRRig.LocalRig.enabled = true;
            return;
        }

        float distance = Vector3.Distance(VRRig.LocalRig.transform.position, target.transform.position);

        if (distance <= 5f)
        {
            VRRig.LocalRig.enabled                      = false;
            VRRig.LocalRig.transform.position           = target.syncPos;
            VRRig.LocalRig.leftHand.rigTarget.position  = target.syncPos;
            VRRig.LocalRig.rightHand.rigTarget.position = target.syncPos;
            GorillaGameModes.GameMode.ReportTag(target.Creator);
        }
        else
        {
            VRRig.LocalRig.enabled = true;
        }
    }

    private static float lastTagAllTime;

    public static void TagAll()
    {
        IReadOnlyList<VRRig> rigs = VRRigCache.ActiveRigs;

        if (!GorillaGameModes.GameMode.LocalIsTagged(PhotonNetwork.LocalPlayer))
        {
            VRRig hunterRig = null;

            for (int i = 0; i < rigs.Count; i++)
            {
                if (!rigs[i].isLocal && GorillaGameModes.GameMode.LocalIsTagged(rigs[i].Creator))
                {
                    hunterRig = rigs[i];

                    break;
                }
            }

            if (hunterRig != null)
            {
                VRRig.LocalRig.enabled            = false;
                VRRig.LocalRig.transform.position = hunterRig.leftHand.rigTarget.position;
            }
            else if (!VRRig.LocalRig.enabled)
            {
                VRRig.LocalRig.enabled = true;
            }

            return;
        }

        VRRig targetRig = null;

        for (int i = 0; i < rigs.Count; i++)
        {
            if (!rigs[i].isLocal && !GorillaGameModes.GameMode.LocalIsTagged(rigs[i].Creator))
            {
                targetRig = rigs[i];

                break;
            }
        }

        if (targetRig != null)
        {
            VRRig.LocalRig.enabled            = false;
            VRRig.LocalRig.transform.position = targetRig.transform.position - Vector3.up;

            if (Vector3.Distance(VRRig.LocalRig.transform.position, targetRig.transform.position) <= 1f &&
                Time.time > lastTagAllTime + 1f)
            {
                GorillaGameModes.GameMode.ReportTag(targetRig.Creator);
                lastTagAllTime = Time.time;
            }
        }
        else if (!VRRig.LocalRig.enabled)
        {
            VRRig.LocalRig.enabled = true;
        }
    }

    private const float WASD_MOUSE_SENSITIVITY = 0.08f;

    private static readonly Dictionary<Transform, Vector3> wasdHandPositions = new();
    private static          SphereCollider                wasdHandProbe;

    public static void WasdFly()
    {
        GorillaTagger tagger = GorillaTagger.Instance;
        var           input  = UnityInput.Current;

        if (tagger == null || input == null || GTPlayer.Instance == null ||
            tagger.rigidbody == null || tagger.headCollider == null ||
            tagger.leftHandTransform == null || tagger.rightHandTransform == null)
            return;

        Rigidbody rigidbody = tagger.rigidbody;
        Transform body      = rigidbody.transform;
        Transform head      = tagger.headCollider.transform;

        Transform leftHand  = tagger.leftHandTransform;
        Transform rightHand = tagger.rightHandTransform;

        leftHand.localPosition  += Vector3.down * 0.5f;
        rightHand.localPosition += Vector3.down * 0.5f;

        leftHand.localPosition  -= Vector3.right * 0.2f;
        rightHand.localPosition -= Vector3.left  * 0.2f;

        leftHand.localRotation  = Quaternion.Euler(40f, 0f, 0f);
        rightHand.localRotation = Quaternion.Euler(40f, 0f, 0f);

        if (input.GetKey(KeyCode.Q))
        {
            leftHand.localPosition += Vector3.forward * 0.2f;
            leftHand.localPosition += Vector3.up      * 0.4f;
        }

        if (input.GetKey(KeyCode.E))
        {
            rightHand.localPosition += Vector3.forward * 0.2f;
            rightHand.localPosition += Vector3.up      * 0.4f;
        }

        Camera     thirdPersonCam = null;
        GameObject cameraObj      = GameObject.Find("Player Objects/Third Person Camera/Shoulder Camera");

        if (cameraObj != null)
            thirdPersonCam = cameraObj.GetComponent<Camera>();

        Mouse mouse = Mouse.current;

        if (mouse != null && mouse.rightButton.isPressed)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();

            body.Rotate(Vector3.up,         mouseDelta.x * WASD_MOUSE_SENSITIVITY, Space.World);
            head.Rotate(Vector3.right, -mouseDelta.y * WASD_MOUSE_SENSITIVITY, Space.Self);

            Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
        }

        if (mouse != null && mouse.leftButton.isPressed)
        {
            Camera raycastCamera = Camera.main;

            if (thirdPersonCam != null)
                raycastCamera = thirdPersonCam;

            if (raycastCamera != null)
            {
                Ray ray = raycastCamera.ScreenPointToRay(mouse.position.ReadValue());

                if (Physics.Raycast(
                            ray,
                            out RaycastHit hit,
                            100f,
                            GTPlayer.Instance.locomotionEnabledLayers,
                            QueryTriggerInteraction.Ignore))
                    rightHand.position = hit.point;
            }
        }

        Vector3 movementDirection = Vector3.zero;

        Vector3 forwardDirection = head.forward;
        Vector3 rightDirection   = head.right;

        if (input.GetKey(KeyCode.W))
            movementDirection += forwardDirection;

        if (input.GetKey(KeyCode.S))
            movementDirection -= forwardDirection;

        if (input.GetKey(KeyCode.A))
            movementDirection -= rightDirection;

        if (input.GetKey(KeyCode.D))
            movementDirection += rightDirection;

        if (input.GetKey(KeyCode.Space))
            movementDirection += head.up;

        float speed = input.GetKey(KeyCode.LeftShift) ? 40f : 10f;

        if (movementDirection != Vector3.zero)
            body.position += movementDirection.normalized * (Time.deltaTime * speed);

        rigidbody.linearVelocity        = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;

        ResolveWasdHandCollision(leftHand);
        ResolveWasdHandCollision(rightHand);
    }

    private static void ResolveWasdHandCollision(Transform hand)
    {
        const float handRadius = 0.075f;
        const float skinWidth  = 0.0025f;

        bool shouldIgnore = Vector3.Distance(hand.position, GorillaTagger.Instance.rigidbody.transform.position) > 2f;

        if (shouldIgnore)
        {
            wasdHandPositions[hand] = hand.position;

            return;
        }

        if (!wasdHandPositions.TryGetValue(hand, out Vector3 previousPosition))
            previousPosition = hand.position;

        Vector3 currentPosition = hand.position;
        Vector3 moveDelta       = currentPosition - previousPosition;

        float distance = moveDelta.magnitude;

        if (distance > 0.0001f)
        {
            Vector3 direction = moveDelta.normalized;

            if (Physics.SphereCast(
                        previousPosition,
                        handRadius,
                        direction,
                        out RaycastHit hit,
                        distance + skinWidth,
                        GTPlayer.Instance.locomotionEnabledLayers,
                        QueryTriggerInteraction.Ignore))
            {
                hand.position = hit.point + hit.normal * (handRadius + skinWidth);

                Vector3 remainingMovement = currentPosition - hand.position;
                Vector3 surfaceSlide      = Vector3.ProjectOnPlane(remainingMovement, hit.normal);

                if (!Physics.SphereCast(
                            hand.position,
                            handRadius,
                            surfaceSlide.normalized,
                            out RaycastHit slideHit,
                            surfaceSlide.magnitude,
                            GTPlayer.Instance.locomotionEnabledLayers,
                            QueryTriggerInteraction.Ignore))
                    hand.position += surfaceSlide;
            }
        }

        Collider[] overlaps = Physics.OverlapSphere(
                hand.position,
                handRadius,
                GTPlayer.Instance.locomotionEnabledLayers,
                QueryTriggerInteraction.Ignore);

        foreach (Collider col in overlaps)
            if (Physics.ComputePenetration(
                        GetWasdHandProbe(handRadius, hand.position),
                        hand.position,
                        Quaternion.identity,
                        col,
                        col.transform.position,
                        col.transform.rotation,
                        out Vector3 direction,
                        out float penetration))
                hand.position += direction * (penetration + skinWidth);

        wasdHandPositions[hand] = hand.position;
    }

    private static SphereCollider GetWasdHandProbe(float radius, Vector3 position)
    {
        if (wasdHandProbe == null)
        {
            GameObject obj = new("RIFTIX_WasdHandProbe");
            obj.hideFlags = HideFlags.HideAndDontSave;

            wasdHandProbe           = obj.AddComponent<SphereCollider>();
            wasdHandProbe.isTrigger = true;
        }

        wasdHandProbe.radius             = radius;
        wasdHandProbe.transform.position = position;

        return wasdHandProbe;
    }
}