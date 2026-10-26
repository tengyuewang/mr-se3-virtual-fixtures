using System.Collections;
using System.Collections.Generic;
using MixedReality.Toolkit;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Samples the pose of this object while it is selected and maintains a smooth
/// SE(3) path for visualization and ROS publication.
/// </summary>
public class PathRecorder : MonoBehaviour
{
    [Header("References")]
    public ObservableGrabbable observableGrabbable;
    public InteractableGroupView interactableGroupView;
    public LineRenderer pathLine;
    [SerializeField] private Material lineMaterial;

    [Header("Pose sampling")]
    [FormerlySerializedAs("minDistance")]
    [Min(0f)] public float minPositionDistance = 0.015f;
    [Tooltip("Minimum angular change, in radians, that records a new pose.")]
    [Min(0f)] public float minRotationAngleRadians = 0.15f;
    [Min(2)] public int maxPoints = 20000;
    public bool useFixedUpdate;

    [Header("SE(3) path fitting")]
    [Min(0.0001f)] public float curveSpacing = 0.004f;
    [Tooltip("Balances translation and rotation in the SE(3) metric, in metres.")]
    [Min(0.0001f)] public float characteristicLength = 0.1f;
    [Min(0.005f)] public float refitInterval = 1f / 60f;
    [Min(0f)] public float lineWidth = 0.01f;

    [Header("Waypoint markers")]
    public GameObject waypointPrefab;
    [Min(0.0001f)] public float waypointScale = 0.01f;
    [Min(1)] public int waypointEveryN = 6;
    public Transform waypointParent;

    // Retained for existing UnityEvent bindings.
    public Material transparentMaterial;
    public Material solidMaterial;
    public bool tapToStart;
    public bool goPublish;

    private readonly List<Transform> transforms = new List<Transform>();
    private readonly List<Pose> sampledPoses = new List<Pose>(1024);
    private readonly List<Pose> fittedPoses = new List<Pose>(1024);
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private bool hasLastPose;
    private bool refitRequested;
    private Coroutine refitRoutine;

    public List<Transform> Transforms => transforms;
    public IReadOnlyList<Pose> FittedPoses => fittedPoses;

    private void Start()
    {
        EnsureLineRenderer();
        EnsureWaypointParent();
        ResetRecording();
        refitRoutine = StartCoroutine(RefitThrottler());
    }

    private void OnDestroy()
    {
        if (refitRoutine != null)
            StopCoroutine(refitRoutine);
    }

    private void Update()
    {
        if (tapToStart && !useFixedUpdate && IsSelected())
            Tick();
    }

    private void FixedUpdate()
    {
        if (tapToStart && useFixedUpdate && IsSelected())
            Tick();
    }

    public void ResetRecording()
    {
        tapToStart = false;
        for (int i = 0; i < transforms.Count; i++)
        {
            if (transforms[i] != null)
                Destroy(transforms[i].gameObject);
        }

        transforms.Clear();
        sampledPoses.Clear();
        fittedPoses.Clear();
        hasLastPose = false;
        refitRequested = false;
        goPublish = false;
        if (pathLine != null)
            pathLine.positionCount = 0;
    }

    public void MarkDirty()
    {
        refitRequested = true;
    }

    public void FitPathLine()
    {
        sampledPoses.Clear();
        for (int i = 0; i < transforms.Count; i++)
        {
            Transform waypoint = transforms[i];
            if (waypoint != null)
                sampledPoses.Add(new Pose(waypoint.position, waypoint.rotation));
        }

        fittedPoses.Clear();
        if (sampledPoses.Count > 0)
        {
            fittedPoses.AddRange(CentripetalCatmullRomSE3.FitTrajectory(
                sampledPoses,
                Mathf.Max(curveSpacing, 0.0001f),
                Mathf.Max(characteristicLength, 0.0001f),
                true));
        }

        if (pathLine != null)
        {
            pathLine.positionCount = fittedPoses.Count;
            for (int i = 0; i < fittedPoses.Count; i++)
                pathLine.SetPosition(i, fittedPoses[i].position);
        }

        goPublish = true;
    }

    public void MaterialToSolid()
    {
        SetOwnMaterial(solidMaterial);
    }

    public void MaterialToTransparent()
    {
        SetOwnMaterial(transparentMaterial);
    }

    private void Tick()
    {
        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;
        float angularChange = hasLastPose
            ? Quaternion.Angle(lastRotation, rotation) * Mathf.Deg2Rad
            : float.PositiveInfinity;

        if (!hasLastPose ||
            (position - lastPosition).sqrMagnitude >= minPositionDistance * minPositionDistance ||
            angularChange >= minRotationAngleRadians)
        {
            AddSample(position, rotation);
            lastPosition = position;
            lastRotation = rotation;
            hasLastPose = true;
        }
    }

    private void AddSample(Vector3 position, Quaternion rotation)
    {
        if (transforms.Count >= maxPoints)
            return;

        GameObject marker;
        if (waypointPrefab != null)
        {
            marker = Instantiate(waypointPrefab, waypointParent);
        }
        else
        {
            marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = $"Waypoint_{transforms.Count:D4}";
            marker.transform.SetParent(waypointParent, true);
            Collider markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null)
                Destroy(markerCollider);
        }

        marker.transform.SetPositionAndRotation(position, rotation);
        marker.transform.localScale = Vector3.one * waypointScale;

        int index = transforms.Count;
        ScaleWhenHovering hover = marker.GetComponent<ScaleWhenHovering>();
        if (hover != null)
        {
            hover.id = index;
            if (index % Mathf.Max(1, waypointEveryN) != 0)
                hover.DisableVisualization();
        }

        SwitchableGrabTransformer grabTransformer = marker.GetComponent<SwitchableGrabTransformer>();
        if (grabTransformer != null)
            grabTransformer.SetTranslationAndRotation();

        ObservableGrabbable grabbable = marker.GetComponent<ObservableGrabbable>();
        if (grabbable != null)
            grabbable.enabled = false;

        transforms.Add(marker.transform);
        MarkDirty();
    }

    private bool IsSelected()
    {
        return interactableGroupView == null || interactableGroupView.State == InteractableState.Select;
    }

    private IEnumerator RefitThrottler()
    {
        while (true)
        {
            if (refitRequested)
            {
                refitRequested = false;
                FitPathLine();
            }
            yield return new WaitForSeconds(Mathf.Max(refitInterval, 0.005f));
        }
    }

    private void EnsureLineRenderer()
    {
        if (pathLine == null)
            pathLine = GetComponent<LineRenderer>();
        if (pathLine == null)
            pathLine = gameObject.AddComponent<LineRenderer>();

        pathLine.positionCount = 0;
        pathLine.startWidth = lineWidth;
        pathLine.endWidth = lineWidth;
        pathLine.useWorldSpace = true;
        if (lineMaterial != null)
            pathLine.sharedMaterial = lineMaterial;
    }

    private void EnsureWaypointParent()
    {
        if (waypointParent != null)
            return;

        GameObject parent = new GameObject($"{name}_Waypoints");
        waypointParent = parent.transform;
    }

    private void SetOwnMaterial(Material material)
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null && material != null)
            meshRenderer.material = material;
    }
}
