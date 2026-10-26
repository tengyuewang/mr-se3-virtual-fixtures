using UnityEngine;
using Oculus.Interaction;

public class DampedMoveTowardsTargetProvider : MonoBehaviour, IMovementProvider
{
    [SerializeField] private float positionDamping = 18f;
    [SerializeField] private float rotationDamping = 18f;

    public IMovement CreateMovement()
    {
        return new DampedMoveTowardsTarget(positionDamping, rotationDamping);
    }
}