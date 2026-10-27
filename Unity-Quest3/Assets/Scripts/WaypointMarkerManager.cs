using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class WaypointMarkerManager : MonoBehaviour
{
    [Header("OVR Hand References")]
    [SerializeField] private OVRSkeleton leftHandSkeleton;
    [SerializeField] private OVRSkeleton rightHandSkeleton;

    [Header("Hover Highlight")]
    public float threshold = 0.05f;            // 最近点距离阈值(米)
    public float highlightFactor = 2f;         // 高亮放大倍数
    public float probeInterval = 0.01f;        // 检测间隔

    private float waypointScale;
    private int waypointEveryN;
    public GameObject waypointPrefab;
    private readonly List<Material[]> cachedMaterials = new List<Material[]>();
    private bool isMarkerPrefabTransparencyHigh = true;
    private IReadOnlyList<Transform> transforms;

    private PathRecorder recorder;
    private Coroutine checkRoutine;
    private Coroutine fittingRoutine;
    private bool initialized;
    private bool interactionFrozen;

    private bool isLeftPuase = false;
    private bool isRightPuase = false;

    private Transform leftClosest = null;
    private Transform rightClosest = null;

    private readonly List<ObservableGrabbable> manipulatingGrabbables = new List<ObservableGrabbable>();

    public bool enableMove = false;
    public bool enableRotate = false;

    private bool _isKeepFittingRunning = false;

    private readonly Dictionary<ObservableGrabbable, UnityAction> _onEnter = new();
    private readonly Dictionary<ObservableGrabbable, UnityAction> _onExit = new();

    public bool isUsingLaplacian = true;

    [Header("Laplacian editing")]
    [Min(0f)] public float constraintWeight = 500f;
    [Min(0f)] public float regularizationWeight = 0.5f;
    [Min(0.01f)] public float falloffSigma = 3f;
    public bool pinEndpoints = true;
    [Min(0f)] public float endpointWeight = 1e4f;
    [Min(0.01f)] public float editingInterval = 1f/60f;

    private enum HandSide
    {
        Left,
        Right
    }

    //private void Reset()
    //{
    //    AutoAssignHandSkeletons();
    //}

    //private void Awake()
    //{
    //    AutoAssignHandSkeletons();
    //}

    //private void OnEnable()
    //{
    //    AutoAssignHandSkeletons();
    //}

    private void Start()
    {
        AutoAssignHandSkeletons();

        recorder = GetComponent<PathRecorder>();
        if (!recorder)
        {
            Debug.LogError("[WaypointMarkerManager] Missing PathRecorder on the same GameObject.");
            enabled = false;
            return;
        }

        transforms = recorder.Transforms;
        waypointScale = recorder.waypointScale;
        waypointEveryN = Mathf.Max(1, recorder.waypointEveryN);
        waypointPrefab = recorder.waypointPrefab;

        if (waypointPrefab == null)
        {
            Debug.LogError("[WaypointMarkerManager] waypointPrefab is null.");
            enabled = false;
            return;
        }

        GameObject obj = Instantiate(waypointPrefab);
        foreach (var mr in obj.GetComponentsInChildren<MeshRenderer>())
        {
            cachedMaterials.Add(mr.materials);
        }
        Destroy(obj);

        initialized = true;
        if (!interactionFrozen)
            StartWaypointProbe();
    }

    private void OnEnable()
    {
        if (initialized && !interactionFrozen)
            StartWaypointProbe();
    }

    private void Update()
    {
        if (!interactionFrozen && manipulatingGrabbables.Count > 0 && !_isKeepFittingRunning)
        {
            fittingRoutine = StartCoroutine(KeepFitting());
        }
    }

    private void OnDisable()
    {
        StopInteractionRoutines();
        DeactivateAllWaypointInteractions();
    }

    public void SetInteractionFrozen(bool frozen)
    {
        if (interactionFrozen == frozen)
            return;

        interactionFrozen = frozen;
        if (frozen)
        {
            StopInteractionRoutines();
            DeactivateAllWaypointInteractions();
            recorder?.MarkDirty();
        }
        else if (initialized && isActiveAndEnabled)
        {
            StartWaypointProbe();
        }
    }

    private void StartWaypointProbe()
    {
        if (checkRoutine == null)
            checkRoutine = StartCoroutine(IndexCheckRoutineBothHands());
    }

    private void StopInteractionRoutines()
    {
        if (checkRoutine != null)
        {
            StopCoroutine(checkRoutine);
            checkRoutine = null;
        }

        if (fittingRoutine != null)
        {
            StopCoroutine(fittingRoutine);
            fittingRoutine = null;
        }

        _isKeepFittingRunning = false;
    }

    private void DeactivateAllWaypointInteractions()
    {
        if (transforms != null)
        {
            for (int i = 0; i < transforms.Count; i++)
            {
                Transform waypoint = transforms[i];
                if (waypoint == null)
                    continue;

                waypoint.localScale = Vector3.one * waypointScale;

                Transform origin = waypoint.Find("XYZCoordinate/Origin");
                if (origin != null && origin.TryGetComponent(out SphereCollider sphereCollider))
                    sphereCollider.radius = 0.5f;

                ObservableGrabbable grabbable = waypoint.GetComponent<ObservableGrabbable>();
                if (grabbable != null)
                {
                    grabbable.selectEntered.RemoveListener(LeftPause);
                    grabbable.selectExited.RemoveListener(LeftResume);
                    grabbable.selectEntered.RemoveListener(RightPause);
                    grabbable.selectExited.RemoveListener(RightResume);
                    DetachListeners(grabbable);
                    grabbable.enabled = false;
                }

                ScaleWhenHovering visualization = waypoint.GetComponent<ScaleWhenHovering>();
                if (visualization == null)
                    continue;

                if (visualization.id % Mathf.Max(1, waypointEveryN) == 0)
                    visualization.EnableVisualization();
                else
                    visualization.DisableVisualization();
            }
        }

        manipulatingGrabbables.Clear();
        leftClosest = null;
        rightClosest = null;
        isLeftPuase = false;
        isRightPuase = false;
        ChangeMarkerPrefabTransparency(255f);
    }

    private void AutoAssignHandSkeletons()
    {
        if (leftHandSkeleton != null && rightHandSkeleton != null)
            return;

        OVRSkeleton[] allSkeletons = Object.FindObjectsByType<OVRSkeleton>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (leftHandSkeleton == null)
        {
            foreach (var skeleton in allSkeletons)
            {
                if (skeleton == null) continue;

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
                if (skeleton == null) continue;

                if (skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.HandRight ||
                    skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.XRHandRight)
                {
                    rightHandSkeleton = skeleton;
                    break;
                }
            }
        }
    }

    private bool TryGetIndexTipPosition(OVRSkeleton skeleton, out Vector3 position)
    {
        position = Vector3.zero;

        if (skeleton == null || !skeleton.IsInitialized || !skeleton.IsDataValid || skeleton.Bones == null)
        {
            return false;
        }

        bool isOpenXR =
            skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.XRHandLeft ||
            skeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.XRHandRight;

        OVRSkeleton.BoneId targetBoneId = isOpenXR
            ? OVRSkeleton.BoneId.XRHand_IndexTip
            : OVRSkeleton.BoneId.Hand_IndexTip;

        foreach (var bone in skeleton.Bones)
        {
            if (bone != null && bone.Id == targetBoneId && bone.Transform != null)
            {
                position = bone.Transform.position;
                return true;
            }
        }

        return false;
    }

    private void AttachListeners(ObservableGrabbable og)
    {
        if (og == null) return;

        if (!_onEnter.ContainsKey(og))
            _onEnter[og] = () => AddToManipulatingGrabbables(og);

        if (!_onExit.ContainsKey(og))
            _onExit[og] = () => RemoveFromManipulatingGrabbables(og);

        og.selectEntered.RemoveListener(_onEnter[og]);
        og.selectExited.RemoveListener(_onExit[og]);

        og.selectEntered.AddListener(_onEnter[og]);
        og.selectExited.AddListener(_onExit[og]);
    }

    private void DetachListeners(ObservableGrabbable og)
    {
        if (og == null) return;

        if (_onEnter.TryGetValue(og, out var a)) og.selectEntered.RemoveListener(a);
        if (_onExit.TryGetValue(og, out var b)) og.selectExited.RemoveListener(b);
    }

    private IEnumerator IndexCheckRoutineBothHands()
    {
        var wait = new WaitForSeconds(probeInterval);
        float thresholdSq = threshold * threshold;

        Transform prevLeft = null;
        Transform prevRight = null;

        while (true)
        {
            if (leftHandSkeleton == null || rightHandSkeleton == null)
            {
                AutoAssignHandSkeletons();
            }

            if (isLeftPuase && isRightPuase)
            {
                yield return null;
                continue;
            }

            if (!isLeftPuase) { leftClosest = null; }
            if (!isRightPuase) { rightClosest = null; }

            float leftMinSqr = float.MaxValue;
            float rightMinSqr = float.MaxValue;

            if (!isLeftPuase && TryGetIndexTipPosition(leftHandSkeleton, out Vector3 leftIndexTipPos))
            {
                for (int i = 0; i < transforms.Count; ++i)
                {
                    var tf = transforms[i];
                    if (!tf) continue;

                    float sqr = (tf.position - leftIndexTipPos).sqrMagnitude;
                    if (sqr < leftMinSqr)
                    {
                        leftMinSqr = sqr;
                        leftClosest = tf;
                    }
                }

                if (leftMinSqr > thresholdSq) leftClosest = null;
            }

            if (!isRightPuase && TryGetIndexTipPosition(rightHandSkeleton, out Vector3 rightIndexTipPos))
            {
                for (int i = 0; i < transforms.Count; ++i)
                {
                    var tf = transforms[i];
                    if (!tf) continue;

                    float sqr = (tf.position - rightIndexTipPos).sqrMagnitude;
                    if (sqr < rightMinSqr)
                    {
                        rightMinSqr = sqr;
                        rightClosest = tf;
                    }
                }

                if (rightMinSqr > thresholdSq) rightClosest = null;
            }

            bool anyActive = (leftClosest != null) || (rightClosest != null);
            ChangeMarkerPrefabTransparency(anyActive ? 60 : 255);

            if (prevLeft != leftClosest)
            {
                if (prevLeft != null && prevLeft != rightClosest)
                    RestoreWaypoint(prevLeft, HandSide.Left);

                if (leftClosest != null)
                    HighlightWaypoint(leftClosest, HandSide.Left);
            }

            if (prevRight != rightClosest)
            {
                if (prevRight != null && prevRight != leftClosest)
                    RestoreWaypoint(prevRight, HandSide.Right);

                if (rightClosest != null)
                    HighlightWaypoint(rightClosest, HandSide.Right);
            }

            prevLeft = leftClosest;
            prevRight = rightClosest;

            yield return wait;
        }
    }

    private void RestoreWaypoint(Transform tf, HandSide hand)
    {
        if (!tf) return;

        tf.localScale = Vector3.one * waypointScale;

        Transform origin = tf.Find("XYZCoordinate/Origin");
        SphereCollider sphereCollider = origin.GetComponent<SphereCollider>();
        sphereCollider.radius = 0.5f;

        ObservableGrabbable og = tf.GetComponent<ObservableGrabbable>();
        if (og)
        {
            if (hand == HandSide.Left)
            {
                og.selectEntered.RemoveListener(LeftPause);
                og.selectExited.RemoveListener(LeftResume);
            }
            else if (hand == HandSide.Right)
            {
                og.selectEntered.RemoveListener(RightPause);
                og.selectExited.RemoveListener(RightResume);
            }

            DetachListeners(og);
            og.enabled = false;
        }

        ScaleWhenHovering hover = tf.GetComponent<ScaleWhenHovering>();
        if (hover)
        {
            if (hover.id % waypointEveryN != 0) hover.DisableVisualization();
            else hover.EnableVisualization(60);

            HideNeighbors(hover.id);
        }
    }

    private void HighlightWaypoint(Transform tf, HandSide hand)
    {
        if (!tf) return;

        if (Vector3.Distance(tf.position, transform.position) < 0.02f) return;

        tf.localScale = Vector3.one * waypointScale * highlightFactor;

        Debug.Log(tf.name);

        Transform origin = tf.Find("XYZCoordinate/Origin");
        SphereCollider sphereCollider = origin.GetComponent<SphereCollider>();
        sphereCollider.radius = 3.0f;

        ObservableGrabbable og = tf.GetComponent<ObservableGrabbable>();
        if (og)
        {
            if (hand == HandSide.Left)
            {
                og.selectEntered.RemoveListener(LeftPause);
                og.selectExited.RemoveListener(LeftResume);

                og.selectEntered.AddListener(LeftPause);
                og.selectExited.AddListener(LeftResume);
            }
            else if (hand == HandSide.Right)
            {
                og.selectEntered.RemoveListener(RightPause);
                og.selectExited.RemoveListener(RightResume);

                og.selectEntered.AddListener(RightPause);
                og.selectExited.AddListener(RightResume);
            }

            DetachListeners(og);
            AttachListeners(og);

            og.enabled = true; 
            SwitchableGrabTransformer switchableGrabTransformer = tf.GetComponent<SwitchableGrabTransformer>();
            if (enableMove && enableRotate)
                switchableGrabTransformer.SetTranslationAndRotation();
            else if (enableMove && !enableRotate)
                switchableGrabTransformer.SetTranslationOnly();
            else if (!enableMove && enableRotate)
                switchableGrabTransformer.SetRotationOnly();
            else
                switchableGrabTransformer.SetNone();
        }

        ScaleWhenHovering hover = tf.GetComponent<ScaleWhenHovering>();
        if (hover)
        {
            hover.EnableVisualization();
            ShowNeighbors(hover.id);
        }
    }

    private void AddToManipulatingGrabbables(ObservableGrabbable og)
    {
        if (og != null && !manipulatingGrabbables.Contains(og))
        {
            manipulatingGrabbables.Add(og);
        }
    }

    private void RemoveFromManipulatingGrabbables(ObservableGrabbable og)
    {
        if (og != null && manipulatingGrabbables.Contains(og))
        {
            manipulatingGrabbables.Remove(og);
        }
    }

    private void ShowNeighbors(int id)
    {
        ToggleNeighbor(id - 1, true);
        ToggleNeighbor(id - 2, true);
        ToggleNeighbor(id + 1, true);
        ToggleNeighbor(id + 2, true);
    }

    private void HideNeighbors(int id)
    {
        ToggleNeighbor(id - 1, false);
        ToggleNeighbor(id - 2, false);
        ToggleNeighbor(id + 1, false);
        ToggleNeighbor(id + 2, false);
    }

    private void ToggleNeighbor(int id, bool enable)
    {
        if (id < 0 || id >= transforms.Count) return;
        var t = transforms[id];
        if (!t) return;

        var hover = t.GetComponent<ScaleWhenHovering>();
        if (!hover) return;

        if (id % waypointEveryN != 0)
        {
            if (enable) hover.EnableVisualization(60);
            else hover.DisableVisualization();
        }
    }

    private void LeftResume()
    {
        isLeftPuase = false;
    }

    private void LeftPause()
    {
        isLeftPuase = true;
    }

    private void RightResume()
    {
        isRightPuase = false;
    }

    private void RightPause()
    {
        isRightPuase = true;
    }

    private void ChangeMarkerPrefabTransparency(float transparency)
    {
        if ((isMarkerPrefabTransparencyHigh == false && transparency > 250) ||
            (isMarkerPrefabTransparencyHigh == true && transparency < 250))
        {
            isMarkerPrefabTransparencyHigh = !isMarkerPrefabTransparencyHigh;
            for (int i = 0; i < transforms.Count; i++)
            {
                if (i % waypointEveryN == 0)
                {
                    transforms[i].GetComponent<ScaleWhenHovering>()?.EnableVisualization(transparency);
                }
            }
        }
    }

    IEnumerator KeepFitting()
    {
        _isKeepFittingRunning = true;
        int n = transforms.Count;

        int handle_num = manipulatingGrabbables.Count;
        if (handle_num == 0)
        {
            _isKeepFittingRunning = false;
            fittingRoutine = null;
            yield break;
        }

        List<Pose> allPose = new List<Pose>(n);
        for (int i = 0; i < n; i++)
            allPose.Add(new Pose(transforms[i].position, transforms[i].rotation));

        List<int> handles = new List<int>();
        List<Pose> targets = new List<Pose>();
        bool isSelected = false;

        for (int i = 0; i < handle_num; i++)
        {
            handles.Add(manipulatingGrabbables[i].GetComponent<ScaleWhenHovering>().id);
            targets.Add(new Pose(manipulatingGrabbables[i].transform.position, manipulatingGrabbables[i].transform.rotation));
            if (manipulatingGrabbables[i].IsSelected) isSelected = true;
        }

        Vector3[] prevOmegaAll = new Vector3[n];
        Vector3[] prevOmegaAtHandles = new Vector3[handles.Count];

        var wait = new WaitForSeconds(Mathf.Max(0.01f, editingInterval));

        while (!interactionFrozen && isSelected && manipulatingGrabbables.Count > 0)
        {
            handle_num = manipulatingGrabbables.Count;
            handles = new List<int>();
            targets = new List<Pose>();
            isSelected = false;

            for (int i = 0; i < handle_num; i++)
            {
                if (manipulatingGrabbables[i] == null) continue;

                handles.Add(manipulatingGrabbables[i].GetComponent<ScaleWhenHovering>().id);
                targets.Add(new Pose(manipulatingGrabbables[i].transform.position, manipulatingGrabbables[i].transform.rotation));
                if (manipulatingGrabbables[i].IsSelected) isSelected = true;
            }

            Vector3[] newPrevOmegaAll, newPrevOmegaAtHandles;

            if (!isUsingLaplacian)
            {
                for (int h = 0; h < handles.Count; h++)
                {
                    int id = handles[h];

                    if (id < 0 || id >= transforms.Count)
                        continue;

                    if (enableMove)
                        transforms[id].position = targets[h].position;

                    if (enableRotate)
                        transforms[id].rotation = targets[h].rotation;
                }

                recorder.MarkDirty();
                yield return wait;
                continue;
            }


            if (enableMove && enableRotate)
            {
                Pose[] updatePoses = LaplacianEditing.SolveShapePreservingSE3(
                    allPose, handles, targets,
                    prevOmegaAll, prevOmegaAtHandles,
                    out newPrevOmegaAll, out newPrevOmegaAtHandles,
                    lam: constraintWeight,
                    mu: regularizationWeight,
                    pinEnds: pinEndpoints,
                    endWeight: endpointWeight,
                    falloffSigma: falloffSigma
                );

                for (int i = 0; i < n; i++)
                {
                    transforms[i].position = updatePoses[i].position;
                    transforms[i].rotation = updatePoses[i].rotation;
                }

                prevOmegaAll = newPrevOmegaAll;
                prevOmegaAtHandles = newPrevOmegaAtHandles;
            }
            else if (enableMove && !enableRotate)
            {
                Vector3[] updatePositions = LaplacianEditing.SolveShapePreservingPosition(
                    allPose,
                    handles,
                    targets,
                    lam: constraintWeight,
                    mu: regularizationWeight,
                    pinEnds: pinEndpoints,
                    endWeight: endpointWeight,
                    falloffSigma: falloffSigma
                );

                for (int i = 0; i < n; i++)
                {
                    transforms[i].position = updatePositions[i];
                }
            }
            else if (!enableMove && enableRotate)
            {
                Quaternion[] updateRotations = LaplacianEditing.SolveShapePreservingRotation(
                    allPose, handles, targets,
                    prevOmegaAll, prevOmegaAtHandles,
                    out newPrevOmegaAll, out newPrevOmegaAtHandles,
                    lam: constraintWeight,
                    mu: regularizationWeight,
                    pinEnds: pinEndpoints,
                    endWeight: endpointWeight,
                    falloffSigma: falloffSigma
                );

                for (int i = 0; i < n; i++)
                {
                    transforms[i].rotation = updateRotations[i];
                }

                prevOmegaAll = newPrevOmegaAll;
                prevOmegaAtHandles = newPrevOmegaAtHandles;
            }
            else
            {
                yield return null;
            }

            recorder.MarkDirty();
            yield return wait;
        }

        _isKeepFittingRunning = false;
        fittingRoutine = null;
    }

    public void ChangeIsUsingLaplacian()
    {
        isUsingLaplacian = !isUsingLaplacian; 
    }
}
