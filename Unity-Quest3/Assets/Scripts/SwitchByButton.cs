using MixedReality.Toolkit;
using MixedReality.Toolkit.Subsystems;
using MixedReality.Toolkit.UX;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR;

public class SwitchByButton : MonoBehaviour
{
    [Header("OVR Hand Reference")]
    [SerializeField] private OVRSkeleton rightHandSkeleton; 

    private Vector3 enterPosition; 
    private int index = 0;

    public Transform moveIcon = null;
    public Transform rotateIcon = null;
    public Transform moveAndRotateIcon = null;

    public TextMeshProUGUI[] moveIconText = new TextMeshProUGUI[2];
    public TextMeshProUGUI[] rotateIconText = new TextMeshProUGUI[2];
    public TextMeshProUGUI[] moveAndRotateIconText = new TextMeshProUGUI[3]; 

    Color solidColor = Color.white; 
    Color transparentColor = Color.white;

    public WaypointMarkerManager waypointMarkerManager; 

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        transparentColor.a = 0;

        foreach (TextMeshProUGUI t in moveIconText)
            t.color = solidColor;

        foreach (TextMeshProUGUI t in rotateIconText)
            t.color = transparentColor;

        foreach (TextMeshProUGUI t in moveAndRotateIconText)
            t.color = transparentColor;

        waypointMarkerManager.enableMove = true;
        waypointMarkerManager.enableRotate = false;

        //moveIcon = transform.Find("Frontplate/AnimatedContent/MoveIcon");
        //moveIconText[0] = moveIcon.Find("UIButtonFontIcon").GetComponent<TextMeshProUGUI>();
        //moveIconText[1] = moveIcon.Find("Label").GetComponent<TextMeshProUGUI>();

        //rotateIcon = transform.Find("Frontplate/AnimatedContent/RotateIcon");
        //rotateIconText[0] = rotateIcon.Find("UIButtonFontIcon").GetComponent<TextMeshProUGUI>();
        //rotateIconText[1] = rotateIcon.Find("Label").GetComponent<TextMeshProUGUI>();

        //moveAndRotateIcon = transform.Find("Frontplate/AnimatedContent/MoveAndRotateIcon");
        //moveAndRotateIconText[0] = moveAndRotateIcon.Find("UIButtonFontIcon").GetComponent<TextMeshProUGUI>();
        //moveAndRotateIconText[1] = moveAndRotateIcon.Find("UIButtonFontIcon2").GetComponent<TextMeshProUGUI>();
        //moveAndRotateIconText[2] = moveAndRotateIcon.Find("Label").GetComponent<TextMeshProUGUI>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private bool TryGetRightIndexTipWorldPosition(out Vector3 position)
    {
        position = Vector3.zero;

        if (rightHandSkeleton == null ||
            !rightHandSkeleton.IsInitialized ||
            !rightHandSkeleton.IsDataValid ||
            rightHandSkeleton.Bones == null)
        {
            return false;
        }

        bool isOpenXR =
            rightHandSkeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.XRHandLeft ||
            rightHandSkeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.XRHandRight;

        OVRSkeleton.BoneId targetBoneId = isOpenXR
            ? OVRSkeleton.BoneId.XRHand_IndexTip
            : OVRSkeleton.BoneId.Hand_IndexTip;

        foreach (var bone in rightHandSkeleton.Bones)
        {
            if (bone != null && bone.Id == targetBoneId && bone.Transform != null)
            {
                position = bone.Transform.position;
                return true;
            }
        }

        return false;
    }

    public void EnterIndexPoisition()
    {
        if (TryGetRightIndexTipWorldPosition(out Vector3 indexTipPosition))
        {
            enterPosition = transform.InverseTransformPoint(indexTipPosition);
        }
    }
    public void ExitIndexPoisition()
    {
        if (!TryGetRightIndexTipWorldPosition(out Vector3 indexTipPosition))
        {
            return;
        }

        Vector3 exitPosition = transform.InverseTransformPoint(indexTipPosition);

        if ((exitPosition - enterPosition).x > 10f)
        {
            index = (index + 1) % 3;
        }
        else if ((exitPosition - enterPosition).x < -10f)
        {
            if (index == 0) index = 3;
            index = (index - 1) % 3;
        }

        if (index == 0)
        {
            foreach (TextMeshProUGUI t in moveIconText)
                t.color = solidColor;

            foreach (TextMeshProUGUI t in rotateIconText)
                t.color = transparentColor;

            foreach (TextMeshProUGUI t in moveAndRotateIconText)
                t.color = transparentColor;

            if (waypointMarkerManager != null)
            {
                waypointMarkerManager.enableMove = true;
                waypointMarkerManager.enableRotate = false;
            }
        }
        else if (index == 1)
        {
            foreach (TextMeshProUGUI t in moveIconText)
                t.color = transparentColor;

            foreach (TextMeshProUGUI t in rotateIconText)
                t.color = solidColor;

            foreach (TextMeshProUGUI t in moveAndRotateIconText)
                t.color = transparentColor;

            if (waypointMarkerManager != null)
            {
                waypointMarkerManager.enableMove = false;
                waypointMarkerManager.enableRotate = true;
            }
        }
        else if (index == 2)
        {
            foreach (TextMeshProUGUI t in moveIconText)
                t.color = transparentColor;

            foreach (TextMeshProUGUI t in rotateIconText)
                t.color = transparentColor;

            foreach (TextMeshProUGUI t in moveAndRotateIconText)
                t.color = solidColor;

            if (waypointMarkerManager != null)
            {
                waypointMarkerManager.enableMove = true;
                waypointMarkerManager.enableRotate = true;
            }
        }
    }
}
