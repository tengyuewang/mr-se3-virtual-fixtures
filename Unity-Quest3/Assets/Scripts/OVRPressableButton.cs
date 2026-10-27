using MixedReality.Toolkit;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class OVRPressableButton : StatefulInteractable
{
    public enum HandednessMode
    {
        Left,
        Right,
        Both
    }

    public enum FingerType
    {
        IndexTip,
        MiddleTip,
        ThumbTip
    }

    [Header("Skeleton References")]
    [SerializeField] private OVRSkeleton leftHandSkeleton;
    [SerializeField] private OVRSkeleton rightHandSkeleton;

    [Header("Press Source")]
    [SerializeField] private HandednessMode handedness = HandednessMode.Right;
    [SerializeField] private FingerType fingerType = FingerType.IndexTip;
    [SerializeField] private float fingertipRadius = 0.01f;
    [SerializeField] private float hoverDistance = 0.02f;

    [Header("Press Planes")]
    [SerializeField] private float startPushPlane = 0.0f;
    [SerializeField] private float endPushPlane = 7.0f;

    [Header("Selection")]
    [SerializeField] private bool smoothSelectionProgress = true;
    [SerializeField] private float returnSpeed = 12f;
    [SerializeField] private float extendSpeed = 20f;
    [SerializeField, Range(0f, 1f)] private float pressThreshold = 0.7f;
    [SerializeField, Range(0f, 1f)] private float releaseThreshold = 0.2f;

    [Header("Constraints")]
    [SerializeField] private bool enforceFrontPush = true;
    [SerializeField] private bool rejectXYRollOff = true;
    [SerializeField] private bool rejectZRollOff = false;
    [SerializeField] private float xyRollOffTolerance = 0.002f;

    [Header("Visuals")]
    [SerializeField] private Transform movingVisuals;
    [SerializeField] private bool driveVisualsLocally = false;
    [SerializeField] private bool pressAlongPositiveLocalZ = true;

    [Header("Extra Events")]
    public UnityEvent OnHoverEnteredOVR;
    public UnityEvent OnHoverExitedOVR;
    public UnityEvent OnPressedOVR;
    public UnityEvent OnReleasedOVR;
    public UnityEvent<float> OnSelectionProgressChangedOVR;

    public float SelectionProgress => selectionProgress;
    public bool OVRIsHovered => ovrIsHovered;
    public bool OVRIsPressed => ovrIsPressed;

    private Collider cachedCollider;
    private Vector3 movingVisualsInitialLocalPosition;
    private float selectionProgress = 0f;
    private bool ovrIsHovered = false;
    private bool ovrIsPressed = false;
    private bool clickTriggeredThisPress = false;

    protected override void Awake()
    {
        base.Awake();

        cachedCollider = GetComponent<Collider>();

        if (movingVisuals == null)
        {
            movingVisuals = transform;
        }

        movingVisualsInitialLocalPosition = movingVisuals.localPosition;

        // Keep StatefulInteractable thresholds aligned with our local logic
        SelectThreshold = pressThreshold;
        DeselectThreshold = releaseThreshold;
    }

    protected override void OnDisable()
    {
        selectionProgress = 0f;
        ovrIsHovered = false;
        ovrIsPressed = false;
        clickTriggeredThisPress = false;

        if (movingVisuals != null)
        {
            movingVisuals.localPosition = movingVisualsInitialLocalPosition;
        }

        base.OnDisable();
    }

    private void Update()
    {
        if (leftHandSkeleton == null || rightHandSkeleton == null)
        {
            AutoAssignReferences();
        }

        bool hasFinger = TryGetBestFingerState(
            out Vector3 fingerWorldPos,
            out float pressDistance,
            out float pressProgress,
            out bool isNear);

        SetHoveredInternal(isNear);

        if (hasFinger)
        {
            bool invalid = false;

            if (enforceFrontPush && pressDistance < startPushPlane - fingertipRadius)
            {
                invalid = true;
            }

            if (rejectXYRollOff && IsOutsideFootprint(fingerWorldPos, xyRollOffTolerance))
            {
                invalid = true;
            }

            if (rejectZRollOff && pressDistance > endPushPlane + fingertipRadius)
            {
                invalid = true;
            }

            if (invalid)
            {
                hasFinger = false;
                pressProgress = 0f;
            }
        }

        UpdateSelectionProgress(hasFinger, pressProgress);
        UpdatePressState();

        if (driveVisualsLocally)
        {
            UpdateVisuals();
        }
    }

    private void AutoAssignReferences()
    {
        OVRSkeleton[] allSkeletons = FindObjectsByType<OVRSkeleton>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (leftHandSkeleton == null)
        {
            foreach (var skeleton in allSkeletons)
            {
                if (skeleton == null || !skeleton.enabled) continue;

                if (skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.HandLeft ||
                    skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.XRHandLeft)
                {
                    leftHandSkeleton = skeleton;
                    break;
                }
            }
        }

        if (rightHandSkeleton == null)
        {
            foreach (var skeleton in allSkeletons)
            {
                if (skeleton == null || !skeleton.enabled) continue;

                if (skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.HandRight ||
                    skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.XRHandRight)
                {
                    rightHandSkeleton = skeleton;
                    break;
                }
            }
        }
    }

    public override float GetSelectionProgress()
    {
        return selectionProgress;
    }

    private void SetHoveredInternal(bool value)
    {
        if (ovrIsHovered == value) return;

        ovrIsHovered = value;

        if (ovrIsHovered)
        {
            OnHoverEnteredOVR?.Invoke();
        }
        else
        {
            OnHoverExitedOVR?.Invoke();
        }
    }

    private void SetPressedInternal(bool value)
    {
        if (ovrIsPressed == value) return;

        ovrIsPressed = value;

        if (ovrIsPressed)
        {
            OnPressedOVR?.Invoke();
        }
        else
        {
            OnReleasedOVR?.Invoke();
        }
    }

    private void UpdateSelectionProgress(bool hasTouch, float targetPressProgress)
    {
        targetPressProgress = Mathf.Clamp01(targetPressProgress);

        if (hasTouch)
        {
            selectionProgress = smoothSelectionProgress
                ? Mathf.Lerp(selectionProgress, targetPressProgress, Time.deltaTime * extendSpeed)
                : targetPressProgress;
        }
        else
        {
            selectionProgress = smoothSelectionProgress
                ? Mathf.Lerp(selectionProgress, 0f, Time.deltaTime * returnSpeed)
                : 0f;
        }

        if (selectionProgress < 0.0001f) selectionProgress = 0f;
        if (selectionProgress > 0.9999f) selectionProgress = 1f;

        OnSelectionProgressChangedOVR?.Invoke(selectionProgress);
    }

    private void UpdatePressState()
    {
        if (!ovrIsPressed && selectionProgress >= pressThreshold)
        {
            SetPressedInternal(true);
            clickTriggeredThisPress = false;
        }

        if (ovrIsPressed && !clickTriggeredThisPress && selectionProgress >= pressThreshold)
        {
            clickTriggeredThisPress = true;

            if (!TriggerOnRelease)
            {
                FireStatefulClickAndToggle();
            }
        }

        if (ovrIsPressed && selectionProgress <= releaseThreshold)
        {
            if (TriggerOnRelease && clickTriggeredThisPress)
            {
                FireStatefulClickAndToggle();
            }

            SetPressedInternal(false);
            clickTriggeredThisPress = false;
        }
    }

    private void FireStatefulClickAndToggle()
    {
        if (ToggleMode == ToggleType.Toggle)
        {
            ForceSetToggled(!IsToggled.Active);
        }
        else if (ToggleMode == ToggleType.OneWayToggle && !IsToggled.Active)
        {
            ForceSetToggled(true);
        }

        OnClicked.Invoke();
    }

    private void UpdateVisuals()
    {
        if (movingVisuals == null) return;

        float distance = Mathf.Lerp(startPushPlane, endPushPlane, selectionProgress);
        float sign = pressAlongPositiveLocalZ ? 1f : -1f;

        movingVisuals.localPosition =
            movingVisualsInitialLocalPosition + new Vector3(0f, 0f, sign * distance);
    }

    private bool TryGetBestFingerState(
        out Vector3 fingerWorldPos,
        out float pressDistance,
        out float pressProgress,
        out bool isNear)
    {
        fingerWorldPos = Vector3.zero;
        pressDistance = startPushPlane;
        pressProgress = 0f;
        isNear = false;

        bool foundPress = false;
        float bestDistance = float.NegativeInfinity;
        float nearestSurfaceDistance = float.PositiveInfinity;
        Vector3 bestPos = Vector3.zero;

        TryEvaluateHand(leftHandSkeleton, true, ref foundPress, ref bestDistance, ref bestPos, ref nearestSurfaceDistance);
        TryEvaluateHand(rightHandSkeleton, false, ref foundPress, ref bestDistance, ref bestPos, ref nearestSurfaceDistance);

        isNear = nearestSurfaceDistance <= hoverDistance;

        if (!foundPress)
        {
            return false;
        }

        fingerWorldPos = bestPos;
        pressDistance = Mathf.Max(bestDistance, startPushPlane);
        pressProgress = Mathf.InverseLerp(startPushPlane, endPushPlane, pressDistance);
        return true;
    }

    private void TryEvaluateHand(
        OVRSkeleton skeleton,
        bool isLeftHand,
        ref bool foundPress,
        ref float bestDistance,
        ref Vector3 bestPos,
        ref float nearestSurfaceDistance)
    {
        if (!ShouldUseHand(isLeftHand)) return;

        if (!TryGetFingerWorldPosition(skeleton, fingerType, out Vector3 fingerPos))
        {
            return;
        }

        Vector3 closest = cachedCollider.ClosestPoint(fingerPos);
        float surfaceDistance = Vector3.Distance(fingerPos, closest); 

        if (surfaceDistance < nearestSurfaceDistance)
        {
            nearestSurfaceDistance = surfaceDistance;
        }

        if (surfaceDistance > fingertipRadius)
        {
            return;
        }

        float dist = GetDistanceAlongPushDirection(fingerPos) + fingertipRadius;
        if (dist > bestDistance)
        {
            bestDistance = dist;
            bestPos = fingerPos;
            foundPress = true;
        }
    }

    private bool ShouldUseHand(bool isLeft)
    {
        return handedness switch
        {
            HandednessMode.Left => isLeft,
            HandednessMode.Right => !isLeft,
            HandednessMode.Both => true,
            _ => false
        };
    }

    private float GetDistanceAlongPushDirection(Vector3 worldPos)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        return localPos.z;
    }

    private bool IsOutsideFootprint(Vector3 fingerWorldPos, float tolerance)
    {
        Vector3 closestPoint = cachedCollider.ClosestPoint(fingerWorldPos);
        Vector3 localDelta = cachedCollider.transform.InverseTransformDirection(closestPoint - fingerWorldPos);

        return Mathf.Abs(localDelta.x) > tolerance || Mathf.Abs(localDelta.y) > tolerance;
    }

    private bool TryGetFingerWorldPosition(OVRSkeleton skeleton, FingerType finger, out Vector3 worldPos)
    {
        worldPos = Vector3.zero;

        if (skeleton == null || !skeleton.IsInitialized || !skeleton.IsDataValid || skeleton.Bones == null)
        {
            return false;
        }

        OVRSkeleton.BoneId targetBone = MapFingerToBoneId(skeleton, finger);

        foreach (var bone in skeleton.Bones)
        {
            if (bone != null && bone.Transform != null && bone.Id == targetBone)
            {
                worldPos = bone.Transform.position;
                return true;
            }
        }

        return false;
    }

    private OVRSkeleton.BoneId MapFingerToBoneId(OVRSkeleton skeleton, FingerType finger)
    {
        bool isOpenXR =
            skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.XRHandLeft ||
            skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.XRHandRight;

        if (isOpenXR)
        {
            return finger switch
            {
                FingerType.IndexTip => OVRSkeleton.BoneId.XRHand_IndexTip,
                FingerType.MiddleTip => OVRSkeleton.BoneId.XRHand_MiddleTip,
                FingerType.ThumbTip => OVRSkeleton.BoneId.XRHand_ThumbTip,
                _ => OVRSkeleton.BoneId.XRHand_IndexTip
            };
        }
        else
        {
            return finger switch
            {
                FingerType.IndexTip => OVRSkeleton.BoneId.Hand_IndexTip,
                FingerType.MiddleTip => OVRSkeleton.BoneId.Hand_MiddleTip,
                FingerType.ThumbTip => OVRSkeleton.BoneId.Hand_ThumbTip,
                _ => OVRSkeleton.BoneId.Hand_IndexTip
            };
        }
    }
}