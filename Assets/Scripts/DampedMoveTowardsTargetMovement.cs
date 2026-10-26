using UnityEngine;
using Oculus.Interaction;

public class DampedMoveTowardsTarget : IMovement
{
    private readonly float _positionDamping;
    private readonly float _rotationDamping;

    private Pose _pose;
    private Pose _target;
    private bool _stopped = true;

    public Pose Pose => _pose;
    public bool Stopped => _stopped;

    public DampedMoveTowardsTarget(float positionDamping, float rotationDamping)
    {
        _positionDamping = Mathf.Max(0.0001f, positionDamping);
        _rotationDamping = Mathf.Max(0.0001f, rotationDamping);
    }

    public void MoveTo(Pose target)
    {
        _target = target;
        _stopped = false;
    }

    public void UpdateTarget(Pose target)
    {
        if (_target != target)
        {
            _target = target;
            _stopped = false;
        }
    }

    public void StopAndSetPose(Pose pose)
    {
        _pose = pose;
        _target = pose;
        _stopped = true;
    }

    public void Tick()
    {
        if (_stopped)
        {
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        float posT = 1f - Mathf.Exp(-_positionDamping * dt);
        float rotT = 1f - Mathf.Exp(-_rotationDamping * dt);

        Vector3 newPosition = Vector3.Lerp(_pose.position, _target.position, posT);
        Quaternion newRotation = Quaternion.Slerp(_pose.rotation, _target.rotation, rotT);

        _pose = new Pose(newPosition, newRotation);

        // 内部固定停止条件，不暴露到 Inspector
        bool positionDone = (_pose.position - _target.position).sqrMagnitude < 1e-8f;
        bool rotationDone = Quaternion.Angle(_pose.rotation, _target.rotation) < 0.1f;

        if (positionDone && rotationDone)
        {
            _pose = _target;
            _stopped = true;
        }
    }
}