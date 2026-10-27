using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class OVRQuickThumbIndexTap : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OVRSkeleton leftHandSkeleton;

    [Header("Detection")]
    [SerializeField] private float touchDistance = 0.02f;
    [SerializeField] private float releaseDistance = 0.025f;
    [SerializeField] private float quickReleaseWindow = 0.2f;
    [SerializeField] private float pollingInterval = 0.05f;
    [SerializeField] private float cooldownTime = 0.3f;

    [Header("Events")]
    public UnityEvent OnQuickThumbIndexTap;

    private Coroutine _watchCoroutine;
    private Coroutine _checkReleaseCoroutine;
    private bool _isInCooldown = false;
    private bool _isLastTouching = false;

    private PathRecorder pathRecorder; 

    private void Start()
    {
        pathRecorder = GetComponent<PathRecorder>(); 
        _watchCoroutine = StartCoroutine(LeftThumbIndexCheckRoutine());
    }

    private IEnumerator LeftThumbIndexCheckRoutine()
    {
        while (true)
        {
            if (CanEvaluate() &&
                TryGetFingerTipPosition(leftHandSkeleton, OVRSkeleton.BoneId.Hand_ThumbTip, OVRSkeleton.BoneId.XRHand_ThumbTip, out Vector3 thumbTip) &&
                TryGetFingerTipPosition(leftHandSkeleton, OVRSkeleton.BoneId.Hand_IndexTip, OVRSkeleton.BoneId.XRHand_IndexTip, out Vector3 indexTip))
            {
                float distance = Vector3.Distance(thumbTip, indexTip);

                if (distance < touchDistance && _checkReleaseCoroutine == null && !_isInCooldown)
                {
                    _checkReleaseCoroutine = StartCoroutine(CheckFingerSeparation());
                }
                else if (distance >= touchDistance)
                {
                    _isLastTouching = false;
                }
            }

            yield return new WaitForSeconds(pollingInterval);
        }
    }

    private IEnumerator CheckFingerSeparation()
    {
        float startTime = Time.time;

        while (Time.time - startTime < quickReleaseWindow)
        {
            if (CanEvaluate() &&
                TryGetFingerTipPosition(leftHandSkeleton, OVRSkeleton.BoneId.Hand_ThumbTip, OVRSkeleton.BoneId.XRHand_ThumbTip, out Vector3 thumbTip) &&
                TryGetFingerTipPosition(leftHandSkeleton, OVRSkeleton.BoneId.Hand_IndexTip, OVRSkeleton.BoneId.XRHand_IndexTip, out Vector3 indexTip))
            {
                float distance = Vector3.Distance(thumbTip, indexTip);

                if (distance >= releaseDistance && !_isLastTouching)
                {
                    Debug.Log("Detected a quick thumb-index touch and release.");
                    pathRecorder.tapToStart = !pathRecorder.tapToStart; 

                    OnQuickThumbIndexTap?.Invoke();

                    _checkReleaseCoroutine = null;
                    _isInCooldown = true;
                    yield return new WaitForSeconds(cooldownTime);
                    _isInCooldown = false;
                    yield break;
                }
            }

            yield return null;
        }

        _isLastTouching = true;
        _checkReleaseCoroutine = null;
    }

    private bool CanEvaluate()
    {
        if (leftHandSkeleton == null)
            return false;

        if ((!leftHandSkeleton.IsInitialized || !leftHandSkeleton.IsDataValid || leftHandSkeleton.Bones == null))
            return false; 
        
        if (!pathRecorder.enabled)
        {
            return false;
        }

        return true;
    }

    private bool TryGetFingerTipPosition(
        OVRSkeleton skeleton,
        OVRSkeleton.BoneId ovrBoneId,
        OVRSkeleton.BoneId openXrBoneId,
        out Vector3 position)
    {
        position = Vector3.zero;

        if (skeleton == null || skeleton.Bones == null)
            return false;

        bool isOpenXR =
            skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.XRHandLeft ||
            skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.XRHandRight;

        OVRSkeleton.BoneId targetBone = isOpenXR ? openXrBoneId : ovrBoneId;

        foreach (var bone in skeleton.Bones)
        {
            if (bone != null && bone.Transform != null && bone.Id == targetBone)
            {
                position = bone.Transform.position;
                return true;
            }
        }

        return false;
    }
}