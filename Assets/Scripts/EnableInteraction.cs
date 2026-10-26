using Unity.VisualScripting;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Grab;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.Input;

public class EnableInteraction : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        //transform.GetComponent<Collider>().isTrigger = true;

        if (transform.GetComponent<Rigidbody>() == null)
        {
            transform.AddComponent<Rigidbody>();
        }
        Rigidbody rigidbody = GetComponent<Rigidbody>();
        rigidbody.useGravity = false;
        rigidbody.isKinematic = true;


        if (transform.GetComponent<ObservableGrabbable>() == null)
        {
            transform.AddComponent<ObservableGrabbable>();
        }
        ObservableGrabbable observableGrabbable = GetComponent<ObservableGrabbable>();
        observableGrabbable.InjectOptionalRigidbody(rigidbody);
        observableGrabbable.InjectOptionalTargetTransform(transform);

        if (transform.GetComponent<HandGrabInteractable>() == null)
        {
            transform.AddComponent<HandGrabInteractable>();
        }
        var handGrabInteractable = GetComponent<HandGrabInteractable>();

        handGrabInteractable.HandAlignment = HandAlignType.None;
        PoseMeasureParameters scoreModifier = new PoseMeasureParameters(0.5f);
        handGrabInteractable.InjectOptionalScoreModifier(scoreModifier);
        handGrabInteractable.InjectOptionalPointableElement(observableGrabbable);
        handGrabInteractable.InjectRigidbody(rigidbody);
        var pinchRule = handGrabInteractable.PinchGrabRules;
        pinchRule[HandFinger.Thumb] = FingerRequirement.Ignored;
        handGrabInteractable.InjectPinchGrabRules(pinchRule); 
        var palmRule = handGrabInteractable.PalmGrabRules; 
        palmRule[HandFinger.Index] = FingerRequirement.Ignored;
        handGrabInteractable.InjectPalmGrabRules(palmRule);


        if (transform.GetComponent<GrabInteractable>() == null)
        {
            transform.AddComponent<GrabInteractable>();
        }
        GetComponent<GrabInteractable>().InjectOptionalPointableElement(observableGrabbable);
        GetComponent<GrabInteractable>().InjectRigidbody(rigidbody); 


    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void IsSelected()
    {
        Debug.Log("is selected"); 
    }

    public void IsUnselected()
    {
        Debug.Log("is unselected");
    }

    public void IsMoved()
    {
        Debug.Log("is moved");
    }
}
