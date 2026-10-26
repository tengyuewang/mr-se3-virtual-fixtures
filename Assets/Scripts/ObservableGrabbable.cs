using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Events;

public class ObservableGrabbable : Grabbable
{
    [System.Serializable]
    public class PointerEventUnityEvent : UnityEvent<PointerEvent> { }

    [Header("Events")]
    public UnityEvent selectEntered;
    public UnityEvent selectExited;
    public UnityEvent moved;
    public UnityEvent cancelled;

    [Header("State")]
    [SerializeField]
    private bool isSelected = false;

    public bool IsSelected => isSelected;

    public override void ProcessPointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Select:
                isSelected = true;
                selectEntered?.Invoke();
                break;

            case PointerEventType.Unselect:
                isSelected = false;
                selectExited?.Invoke();
                break;

            case PointerEventType.Move:
                moved?.Invoke();
                break;

            case PointerEventType.Cancel:
                isSelected = false;
                cancelled?.Invoke();
                break;
        }

        base.ProcessPointerEvent(evt);
    }

    protected override void OnDisable()
    {
        isSelected = false;
        base.OnDisable();
    }
}