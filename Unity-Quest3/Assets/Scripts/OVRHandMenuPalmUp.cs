using System.Collections.Generic;
using UnityEngine;

public class OVRHandMenuPalmUp : MonoBehaviour
{
    public enum SafeZone
    {
        UlnarSide,
        RadialSide,
        AboveFingerTips,
        BelowWrist,
        AtopPalm
    }
    public enum FingerDirectionType
    {
        Index,
        Middle,
        Ring
    }

    [Header("References")]
    [SerializeField] private OVRSkeleton leftHandSkeleton;
    [SerializeField] private OVRSkeleton rightHandSkeleton;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform menuRoot;

    [Header("Hand Selection")]
    [SerializeField] private bool useLeftHand = true;
    [SerializeField] private bool useRightHand = false;

    [Header("Palm Up")]
    [SerializeField, Range(0f, 90f)]
    private float facingCameraTrackingThreshold = 80f;

    [Header("Finger Up Requirement")]
    [SerializeField]
    private bool requireFingerUp = false;

    [SerializeField]
    private FingerDirectionType fingerDirectionType = FingerDirectionType.Index;

    [SerializeField, Range(0f, 90f)]
    private float fingerUpThreshold = 30f;

    [SerializeField]
    private bool requireFlatHand = false;

    [SerializeField, Range(0f, 90f)]
    private float flatHandThreshold = 45f;

    [Header("Gaze Activation")]
    [SerializeField]
    private bool useGazeActivation = false;

    [SerializeField, Range(0f, 0.2f)]
    private float headGazeProximityThreshold = 0.04f;

    [Header("Menu Placement")]
    [SerializeField]
    private SafeZone safeZone = SafeZone.UlnarSide;

    [SerializeField]
    private Vector3 worldOffset = Vector3.zero;

    [SerializeField]
    private bool faceCamera = true;

    [SerializeField]
    private bool showWhenValid = true;

    private bool gazeActivationAlreadyTriggered = false;

    private void Reset()
    {
        if (mainCamera == null)
        {
            AutoAssignReferences();
        }
    }

    private void Update()
    {
        if (mainCamera == null || leftHandSkeleton == null || rightHandSkeleton == null)
        {
            AutoAssignReferences();
        }

        if (mainCamera == null || menuRoot == null)
        {
            return;
        }

        bool isValid = false;

        if (useLeftHand)
        {
            isValid |= EvaluateHand(leftHandSkeleton, true);
        }

        if (!isValid && useRightHand)
        {
            isValid |= EvaluateHand(rightHandSkeleton, false);
        }

        if (!isValid)
        {
            gazeActivationAlreadyTriggered = false;
            if (showWhenValid)
            {
                menuRoot.gameObject.SetActive(false);
            }
        }
    }

    private void AutoAssignReferences()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

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

    private bool EvaluateHand(OVRSkeleton skeleton, bool isLeftHand)
    {
        if (skeleton == null || !skeleton.IsInitialized || !skeleton.IsDataValid)
        {
            return false;
        }

        if (!TryGetPalmPose(skeleton, isLeftHand, out Vector3 palmPos, out Vector3 palmUp, out Quaternion palmRot))
        {
            return false;
        }

        float dotProduct = Vector3.Dot(palmUp.normalized, mainCamera.transform.forward.normalized);
        if (dotProduct < 0f)
        {
            return false;
        }

        float palmCameraAngle = Mathf.Acos(Mathf.Clamp(dotProduct, -1f, 1f)) * Mathf.Rad2Deg;

        if (!IsPalmMeetingThresholdRequirements(skeleton, isLeftHand, palmPos, palmUp, palmCameraAngle))
        {
            return false;
        }

        if (useGazeActivation)
        {
            if (!gazeActivationAlreadyTriggered)
            {
                if (!IsUserGazeMeetingThresholdRequirements(skeleton, isLeftHand))
                {
                    if (showWhenValid)
                    {
                        menuRoot.gameObject.SetActive(false);
                    }
                    return false;
                }
            }
        }

        if (TryGenerateActivationPoint(skeleton, isLeftHand, out Vector3 activationPoint))
        {
            //menuRoot.position = activationPoint + worldOffset;
            menuRoot.position = activationPoint;

            if (faceCamera)
            {
                Vector3 forward = menuRoot.position - mainCamera.transform.position;
                if (forward.sqrMagnitude > 1e-6f)
                {
                    menuRoot.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
                }
            }
            menuRoot.Translate(worldOffset, Space.Self); 
        }

        if (showWhenValid)
        {
            menuRoot.gameObject.SetActive(true);
        }

        return true;
    }

    private bool IsPalmMeetingThresholdRequirements(
        OVRSkeleton skeleton,
        bool isLeftHand,
        Vector3 palmPos,
        Vector3 palmUp,
        float palmCameraAngle)
    {
        if (requireFlatHand)
        {
            if (TryGetJointPosition(skeleton, isLeftHand, JointKind.IndexTip, out Vector3 indexTipPos) &&
                TryGetJointPosition(skeleton, isLeftHand, JointKind.RingTip, out Vector3 ringTipPos))
            {
                Vector3 handNormal = Vector3.Cross(
                    indexTipPos - palmPos,
                    ringTipPos - indexTipPos).normalized;

                handNormal *= isLeftHand ? -1.0f : 1.0f;

                if (Vector3.Angle(palmUp, handNormal) > flatHandThreshold)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        if (requireFingerUp)
        {
            if (!IsFingerPointingUp(skeleton, isLeftHand))
            {
                return false;
            }
        }

        return palmCameraAngle < facingCameraTrackingThreshold;
    }

    private bool IsFingerPointingUp(OVRSkeleton skeleton, bool isLeftHand)
    {
        JointKind proximalJoint;
        JointKind tipJoint;

        switch (fingerDirectionType)
        {
            case FingerDirectionType.Middle:
                proximalJoint = JointKind.MiddleProximal;
                tipJoint = JointKind.MiddleTip;
                break;
            case FingerDirectionType.Ring:
                proximalJoint = JointKind.RingProximal;
                tipJoint = JointKind.RingTip;
                break;
            case FingerDirectionType.Index:
            default:
                proximalJoint = JointKind.IndexProximal;
                tipJoint = JointKind.IndexTip;
                break;
        }

        if (!TryGetJointPosition(skeleton, isLeftHand, proximalJoint, out Vector3 proximalPos) ||
            !TryGetJointPosition(skeleton, isLeftHand, tipJoint, out Vector3 tipPos))
        {
            return false;
        }

        Vector3 fingerDir = (tipPos - proximalPos).normalized;
        float angleToUp = Vector3.Angle(fingerDir, Vector3.up);

        return angleToUp <= fingerUpThreshold;
    }

    private bool IsUserGazeMeetingThresholdRequirements(OVRSkeleton skeleton, bool isLeftHand)
    {
        Ray gazeRay = new Ray(mainCamera.transform.position, mainCamera.transform.forward);

        if (TryGenerateHandPlaneAndActivationPoint(skeleton, isLeftHand, out Plane handPlane, out Vector3 activationPoint) &&
            handPlane.Raycast(gazeRay, out float distanceToHandPlane))
        {
            Vector3 gazePosOnPlane = gazeRay.origin + gazeRay.direction.normalized * distanceToHandPlane;
            Vector3 planePos = handPlane.ClosestPointOnPlane(gazePosOnPlane);

            float gazePosDistToActivationPosition = (activationPoint - planePos).sqrMagnitude;
            float gazeActivationThreshold = headGazeProximityThreshold;

            gazeActivationAlreadyTriggered = (gazePosDistToActivationPosition < gazeActivationThreshold);
            return gazeActivationAlreadyTriggered;
        }

        return false;
    }

    private bool TryGenerateHandPlaneAndActivationPoint(
        OVRSkeleton skeleton,
        bool isLeftHand,
        out Plane handPlane,
        out Vector3 activationPoint)
    {
        if (TryGetJointPosition(skeleton, isLeftHand, JointKind.IndexProximal, out Vector3 indexKnuckle) &&
            TryGetJointPosition(skeleton, isLeftHand, JointKind.LittleProximal, out Vector3 pinkyKnuckle) &&
            TryGetJointPosition(skeleton, isLeftHand, JointKind.Wrist, out Vector3 wrist))
        {
            handPlane = new Plane(indexKnuckle, pinkyKnuckle, wrist);

            if (TryGenerateActivationPoint(skeleton, isLeftHand, out Vector3 generatedActivationPoint))
            {
                activationPoint = handPlane.ClosestPointOnPlane(generatedActivationPoint);
                return true;
            }
        }

        handPlane = default;
        activationPoint = Vector3.zero;
        return false;
    }

    private bool TryGenerateActivationPoint(
        OVRSkeleton skeleton,
        bool isLeftHand,
        out Vector3 activationPoint)
    {
        activationPoint = Vector3.zero;

        switch (safeZone)
        {
            case SafeZone.AboveFingerTips:
                {
                    if (TryGetJointPosition(skeleton, isLeftHand, JointKind.MiddleTip, out Vector3 p1) &&
                        TryGetJointPosition(skeleton, isLeftHand, JointKind.RingTip, out Vector3 p2))
                    {
                        activationPoint = Vector3.Lerp(p1, p2, 0.5f);
                        return true;
                    }
                    return false;
                }

            case SafeZone.BelowWrist:
                {
                    return TryGetJointPosition(skeleton, isLeftHand, JointKind.Wrist, out activationPoint);
                }

            case SafeZone.AtopPalm:
                {
                    return TryGetJointPosition(skeleton, isLeftHand, JointKind.Palm, out activationPoint);
                }

            case SafeZone.RadialSide:
                {
                    if (TryGetJointPosition(skeleton, isLeftHand, JointKind.IndexProximal, out Vector3 p1) &&
                        TryGetJointPosition(skeleton, isLeftHand, JointKind.ThumbProximal, out Vector3 p2))
                    {
                        activationPoint = Vector3.Lerp(p1, p2, 0.5f);
                        return true;
                    }
                    return false;
                }

            case SafeZone.UlnarSide:
            default:
                {
                    if (TryGetJointPosition(skeleton, isLeftHand, JointKind.LittleProximal, out Vector3 p1) &&
                        TryGetJointPosition(skeleton, isLeftHand, JointKind.Wrist, out Vector3 p2))
                    {
                        activationPoint = Vector3.Lerp(p1, p2, 0.5f);
                        return true;
                    }
                    return false;
                }
        }
    }

    private bool TryGetPalmPose(
        OVRSkeleton skeleton,
        bool isLeftHand,
        out Vector3 position,
        out Vector3 up,
        out Quaternion rotation)
    {
        position = Vector3.zero;
        up = Vector3.up;
        rotation = Quaternion.identity;

        if (TryGetJointTransform(skeleton, isLeftHand, JointKind.Palm, out Transform t))
        {
            position = t.position;
            rotation = t.rotation;
            up = t.up;
            return true;
        }

        // 旧 OVR 手骨架没有明确 Palm 时，退化到 WristRoot
        if (TryGetJointTransform(skeleton, isLeftHand, JointKind.Wrist, out t))
        {
            position = t.position;
            rotation = t.rotation;
            up = t.up;
            return true;
        }

        return false;
    }

    private enum JointKind
    {
        Palm,
        Wrist,
        ThumbProximal,
        IndexProximal,
        MiddleProximal,
        RingProximal,
        LittleProximal,
        MiddleTip,
        IndexTip,
        RingTip
    }

    private bool TryGetJointPosition(OVRSkeleton skeleton, bool isLeftHand, JointKind joint, out Vector3 position)
    {
        position = Vector3.zero;
        if (TryGetJointTransform(skeleton, isLeftHand, joint, out Transform t))
        {
            position = t.position;
            return true;
        }
        return false;
    }

    private bool TryGetJointTransform(OVRSkeleton skeleton, bool isLeftHand, JointKind joint, out Transform result)
    {
        result = null;
        if (skeleton == null || skeleton.Bones == null) return false;

        OVRSkeleton.BoneId targetBoneId = MapJointToBoneId(skeleton, joint);

        foreach (var bone in skeleton.Bones)
        {
            if (bone != null && bone.Id == targetBoneId && bone.Transform != null)
            {
                result = bone.Transform;
                return true;
            }
        }

        return false;
    }

    private OVRSkeleton.BoneId MapJointToBoneId(OVRSkeleton skeleton, JointKind joint)
    {
        bool isOpenXR =
            skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.XRHandLeft ||
            skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.XRHandRight;

        if (isOpenXR)
        {
            switch (joint)
            {
                case JointKind.Palm: return OVRSkeleton.BoneId.XRHand_Palm;
                case JointKind.Wrist: return OVRSkeleton.BoneId.XRHand_Wrist;
                case JointKind.ThumbProximal: return OVRSkeleton.BoneId.XRHand_ThumbProximal;
                case JointKind.IndexProximal: return OVRSkeleton.BoneId.XRHand_IndexProximal;
                case JointKind.LittleProximal: return OVRSkeleton.BoneId.XRHand_LittleProximal;
                case JointKind.MiddleTip: return OVRSkeleton.BoneId.XRHand_MiddleTip;
                case JointKind.IndexTip: return OVRSkeleton.BoneId.XRHand_IndexTip;
                case JointKind.RingTip: return OVRSkeleton.BoneId.XRHand_RingTip;
                case JointKind.MiddleProximal: return OVRSkeleton.BoneId.XRHand_MiddleProximal;
                case JointKind.RingProximal: return OVRSkeleton.BoneId.XRHand_RingProximal;
                default: return OVRSkeleton.BoneId.Invalid;
            }
        }
        else
        {
            switch (joint)
            {
                case JointKind.Palm:
                    return OVRSkeleton.BoneId.Hand_WristRoot; // 旧 OVR 没有独立 Palm，近似用 WristRoot
                case JointKind.Wrist:
                    return OVRSkeleton.BoneId.Hand_WristRoot;
                case JointKind.ThumbProximal:
                    return OVRSkeleton.BoneId.Hand_Thumb2;
                case JointKind.IndexProximal:
                    return OVRSkeleton.BoneId.Hand_Index1;
                case JointKind.LittleProximal:
                    return OVRSkeleton.BoneId.Hand_Pinky1;
                case JointKind.MiddleTip:
                    return OVRSkeleton.BoneId.Hand_MiddleTip;
                case JointKind.IndexTip:
                    return OVRSkeleton.BoneId.Hand_IndexTip;
                case JointKind.RingTip:
                    return OVRSkeleton.BoneId.Hand_RingTip;
                case JointKind.MiddleProximal:
                    return OVRSkeleton.BoneId.Hand_Middle1;
                case JointKind.RingProximal:
                    return OVRSkeleton.BoneId.Hand_Ring1;
                default:
                    return OVRSkeleton.BoneId.Invalid;
            }
        }
    }
}
