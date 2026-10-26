using UnityEngine;
using Oculus.Interaction;

public class SwitchableGrabTransformer : MonoBehaviour, ITransformer
{
    public enum TransformMode
    {
        None,
        TranslationOnly,
        RotationOnly,
        TranslationAndRotation
    }

    [Header("Mode")]
    [SerializeField] private TransformMode mode = TransformMode.TranslationAndRotation;

    [Header("Translation Axis Lock")]
    [SerializeField] private bool lockX = false;
    [SerializeField] private bool lockY = false;
    [SerializeField] private bool lockZ = false;

    [Header("Rotation Axis Lock")]
    [SerializeField] private bool lockRotX = false;
    [SerializeField] private bool lockRotY = false;
    [SerializeField] private bool lockRotZ = false;

    [Header("Space")]
    [SerializeField] private bool useLocalSpaceForTranslation = false;
    [SerializeField] private bool useLocalRotationConstraint = false;

    private IGrabbable _grabbable;

    private Pose _grabStartPose;
    private Vector3 _objectStartPosition;
    private Quaternion _objectStartRotation;

    public TransformMode Mode
    {
        get => mode;
        set => mode = value;
    }

    public void Initialize(IGrabbable grabbable)
    {
        _grabbable = grabbable;
    }

    public void BeginTransform()
    {
        if (_grabbable == null || _grabbable.GrabPoints.Count == 0)
        {
            return;
        }

        _grabStartPose = _grabbable.GrabPoints[0];
        _objectStartPosition = _grabbable.Transform.position;
        _objectStartRotation = _grabbable.Transform.rotation;
    }

    public void UpdateTransform()
    {
        if (_grabbable == null || _grabbable.GrabPoints.Count == 0)
        {
            return;
        }

        Pose currentGrabPose = _grabbable.GrabPoints[0];

        switch (mode)
        {
            case TransformMode.None:
                _grabbable.Transform.position = _objectStartPosition;
                _grabbable.Transform.rotation = _objectStartRotation;
                break;

            case TransformMode.TranslationOnly:
                ApplyTranslation(currentGrabPose);
                _grabbable.Transform.rotation = _objectStartRotation;
                break;

            case TransformMode.RotationOnly:
                _grabbable.Transform.position = _objectStartPosition;
                ApplyRotation(currentGrabPose);
                break;

            case TransformMode.TranslationAndRotation:
                ApplyTranslation(currentGrabPose);
                ApplyRotation(currentGrabPose);
                break;
        }
    }

    public void EndTransform()
    {
        // no-op
    }

    private void ApplyTranslation(Pose currentGrabPose)
    {
        Vector3 deltaWorld = currentGrabPose.position - _grabStartPose.position;
        Vector3 targetWorld = _objectStartPosition + deltaWorld;

        if (useLocalSpaceForTranslation && _grabbable.Transform.parent != null)
        {
            Transform parent = _grabbable.Transform.parent;

            Vector3 startLocal = parent.InverseTransformPoint(_objectStartPosition);
            Vector3 targetLocal = parent.InverseTransformPoint(targetWorld);
            Vector3 deltaLocal = targetLocal - startLocal;

            if (lockX) deltaLocal.x = 0f;
            if (lockY) deltaLocal.y = 0f;
            if (lockZ) deltaLocal.z = 0f;

            Vector3 finalLocal = startLocal + deltaLocal;
            _grabbable.Transform.position = parent.TransformPoint(finalLocal);
        }
        else
        {
            Vector3 delta = targetWorld - _objectStartPosition;

            if (lockX) delta.x = 0f;
            if (lockY) delta.y = 0f;
            if (lockZ) delta.z = 0f;

            _grabbable.Transform.position = _objectStartPosition + delta;
        }
    }

    private void ApplyRotation(Pose currentGrabPose)
    {
        Quaternion grabDelta = currentGrabPose.rotation * Quaternion.Inverse(_grabStartPose.rotation);
        Quaternion targetRotation = grabDelta * _objectStartRotation;

        if (!lockRotX && !lockRotY && !lockRotZ)
        {
            _grabbable.Transform.rotation = targetRotation;
            return;
        }

        Quaternion constrainedRotation = ConstrainRotation(_objectStartRotation, targetRotation);
        _grabbable.Transform.rotation = constrainedRotation;
    }

    private Quaternion ConstrainRotation(Quaternion baseRotation, Quaternion targetRotation)
    {
        if (useLocalRotationConstraint && _grabbable.Transform.parent != null)
        {
            Transform parent = _grabbable.Transform.parent;

            Quaternion baseLocal = Quaternion.Inverse(parent.rotation) * baseRotation;
            Quaternion targetLocal = Quaternion.Inverse(parent.rotation) * targetRotation;

            Vector3 euler = NormalizeEuler(targetLocal.eulerAngles);
            Vector3 baseEuler = NormalizeEuler(baseLocal.eulerAngles);

            if (lockRotX) euler.x = baseEuler.x;
            if (lockRotY) euler.y = baseEuler.y;
            if (lockRotZ) euler.z = baseEuler.z;

            Quaternion finalLocal = Quaternion.Euler(euler);
            return parent.rotation * finalLocal;
        }
        else
        {
            Vector3 euler = NormalizeEuler(targetRotation.eulerAngles);
            Vector3 baseEuler = NormalizeEuler(baseRotation.eulerAngles);

            if (lockRotX) euler.x = baseEuler.x;
            if (lockRotY) euler.y = baseEuler.y;
            if (lockRotZ) euler.z = baseEuler.z;

            return Quaternion.Euler(euler);
        }
    }

    private Vector3 NormalizeEuler(Vector3 euler)
    {
        return new Vector3(
            NormalizeAngle(euler.x),
            NormalizeAngle(euler.y),
            NormalizeAngle(euler.z)
        );
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    public void SetNone()
    {
        mode = TransformMode.None;
    }

    public void SetTranslationOnly()
    {
        mode = TransformMode.TranslationOnly;
    }

    public void SetRotationOnly()
    {
        mode = TransformMode.RotationOnly;
    }

    public void SetTranslationAndRotation()
    {
        mode = TransformMode.TranslationAndRotation;
    }
}