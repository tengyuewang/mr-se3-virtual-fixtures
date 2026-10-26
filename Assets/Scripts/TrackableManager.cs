using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using MixedReality.Toolkit.UX;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Aligns the virtual robot base with a tracked QR marker.</summary>
public class TrackableManager : MonoBehaviour
{
    public enum CalibrationMode
    {
        LegacyYawOffset,
        FullPose
    }

    [Header("Tracking")]
    [SerializeField] private GameObject theCamera;
    [SerializeField] private GameObject trackableObjectPrefab;
    [SerializeField] private string markerPayload = "ROBOT";
    [SerializeField] private bool keepCameraAtOrigin = true;

    [Header("Robot")]
    [SerializeField] private Transform baseLink;
    [SerializeField] private CalibrationMode calibrationMode = CalibrationMode.LegacyYawOffset;

    [Header("Legacy yaw-only calibration")]
    [SerializeField] private float legacyHorizontalOffset = 0.23f;
    [SerializeField] private float legacyVerticalOffset = 0.01f;

    [Header("Full-pose calibration (robot base to QR marker)")]
    [Tooltip("Marker position expressed in the robot-base frame, in metres.")]
    [SerializeField] private Vector3 robotBaseToMarkerPosition;
    [Tooltip("Marker orientation expressed in the robot-base frame.")]
    [SerializeField] private Vector3 robotBaseToMarkerEulerDegrees;

    private MRUKTrackable currentTrackable;
    private GameObject qrBase;
    private ArticulationBody articulationBody;
    private bool manipulationState = true;
    private bool isCalibrating;
    private readonly List<Grabbable> frozenRingGrabbables = new List<Grabbable>();
    private readonly List<HandGrabInteractable> frozenRingInteractables = new List<HandGrabInteractable>();

    private static readonly Quaternion LegacyFrameCorrection =
        Quaternion.Inverse(new Quaternion(0.707f, 0f, 0f, -0.707f));

    private void Start()
    {
        if (baseLink != null)
            articulationBody = baseLink.GetComponent<ArticulationBody>();
    }

    private void Update()
    {
        if (keepCameraAtOrigin && theCamera != null && theCamera.transform.position != Vector3.zero)
            theCamera.transform.position = Vector3.zero;

        if (!isCalibrating || qrBase == null || baseLink == null)
            return;

        if (calibrationMode == CalibrationMode.FullPose)
            ApplyFullPoseCalibration();
        else
            ApplyLegacyCalibration();
    }

    public void OnTrackableAdded(MRUKTrackable trackable)
    {
        //if (trackable.TrackableType != OVRAnchor.TrackableType.QRCode ||
        //    !string.Equals(trackable.MarkerPayloadString, markerPayload, System.StringComparison.Ordinal))
        //    return;

        if (trackable.TrackableType != OVRAnchor.TrackableType.QRCode)
            return;

        currentTrackable = trackable;
        if (qrBase != null)
            Destroy(qrBase);

        if (trackableObjectPrefab != null)
        {
            qrBase = Instantiate(trackableObjectPrefab, trackable.transform);
            qrBase.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 0f, 180f));
        }
        else
        {
            qrBase = new GameObject("TrackedRobotQR");
            qrBase.transform.SetParent(trackable.transform, false);
            qrBase.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        }
    }

    public void OnTrackableRemoved(MRUKTrackable trackable)
    {
        if (currentTrackable != trackable)
            return;

        currentTrackable = null;
        if (qrBase != null)
            Destroy(qrBase);
        qrBase = null;
    }

    public void SwitchManipulationState(FontIconSelector fontIconSelector)
    {
        manipulationState = !manipulationState;
        SetMenuButtonActive("Generation", manipulationState);
        SetMenuButtonActive("Edit", manipulationState);
        SetMenuButtonActive("Calibration", manipulationState);
        SetMenuButtonActive("Loading", manipulationState);
        SetMenuButtonActive("Reset", manipulationState);
        SetMenuColumnCount(manipulationState ? 2 : 1);

        bool interactionsFrozen = !manipulationState;
        SetRingInteractionsFrozen(interactionsFrozen);
        SetWaypointInteractionsFrozen(interactionsFrozen);

        if (fontIconSelector != null)
            fontIconSelector.CurrentIconName = manipulationState ? "Icon 18" : "Icon 17";
    }

    public void SwitchIsCalibrating(FontIconSelector fontIconSelector)
    {
        isCalibrating = !isCalibrating;
        if (fontIconSelector != null)
            fontIconSelector.CurrentIconName = isCalibrating ? "Icon 9" : "Icon 10";
    }

    private void ApplyLegacyCalibration()
    {
        float yaw = (qrBase.transform.rotation * LegacyFrameCorrection).eulerAngles.y;
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 position = qrBase.transform.position
                           - legacyHorizontalOffset * (rotation * Vector3.right)
                           + legacyVerticalOffset * (rotation * Vector3.up);
        SetRobotBasePose(position, rotation);
    }

    private void ApplyFullPoseCalibration()
    {
        Quaternion baseToMarkerRotation = Quaternion.Euler(robotBaseToMarkerEulerDegrees);
        Quaternion baseRotation = qrBase.transform.rotation * Quaternion.Inverse(baseToMarkerRotation);
        Vector3 basePosition = qrBase.transform.position - baseRotation * robotBaseToMarkerPosition;
        SetRobotBasePose(basePosition, baseRotation);
    }

    private void SetRobotBasePose(Vector3 position, Quaternion rotation)
    {
        if (articulationBody != null)
        {
            articulationBody.TeleportRoot(position, rotation);
            articulationBody.linearVelocity = Vector3.zero;
            articulationBody.angularVelocity = Vector3.zero;
        }
        else
        {
            baseLink.SetPositionAndRotation(position, rotation);
        }
    }

    private static void SetMenuButtonActive(string buttonName, bool active)
    {
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != "HandMenuBase")
                continue;

            Transform button = root.transform.Find($"MenuContent-Canvas/Buttons-GridLayout/{buttonName}");
            if (button != null)
                button.gameObject.SetActive(active);
            return;
        }
    }

    private static void SetMenuColumnCount(int columnCount)
    {
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != "HandMenuBase")
                continue;

            Transform gridTransform = root.transform.Find("MenuContent-Canvas/Buttons-GridLayout");
            if (gridTransform == null)
                return;

            GridLayoutGroup gridLayout = gridTransform.GetComponent<GridLayoutGroup>();
            if (gridLayout == null)
                return;

            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = Mathf.Max(1, columnCount);

            if (gridTransform is RectTransform gridRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(gridRect);
            return;
        }
    }

    private void SetRingInteractionsFrozen(bool frozen)
    {
        GameObject ringRoot = FindSceneRoot("Ring");
        if (ringRoot == null)
            return;

        if (frozen)
        {
            frozenRingInteractables.Clear();
            foreach (HandGrabInteractable interactable in
                     ringRoot.GetComponentsInChildren<HandGrabInteractable>(true))
            {
                if (!interactable.enabled)
                    continue;

                frozenRingInteractables.Add(interactable);
                interactable.enabled = false;
            }

            frozenRingGrabbables.Clear();
            foreach (Grabbable grabbable in ringRoot.GetComponentsInChildren<Grabbable>(true))
            {
                if (!grabbable.enabled)
                    continue;

                frozenRingGrabbables.Add(grabbable);
                grabbable.enabled = false;
            }
        }
        else
        {
            foreach (Grabbable grabbable in frozenRingGrabbables)
            {
                if (grabbable != null)
                    grabbable.enabled = true;
            }

            foreach (HandGrabInteractable interactable in frozenRingInteractables)
            {
                if (interactable != null)
                    interactable.enabled = true;
            }

            frozenRingGrabbables.Clear();
            frozenRingInteractables.Clear();
        }
    }

    private static void SetWaypointInteractionsFrozen(bool frozen)
    {
        WaypointMarkerManager manager = Object.FindFirstObjectByType<WaypointMarkerManager>(
            FindObjectsInactive.Include);
        if (manager != null)
            manager.SetInteractionFrozen(frozen);
    }

    private static GameObject FindSceneRoot(string rootName)
    {
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == rootName)
                return root;
        }

        return null;
    }
}
