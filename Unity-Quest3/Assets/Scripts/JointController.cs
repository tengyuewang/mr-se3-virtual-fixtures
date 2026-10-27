using UnityEngine;

public class JointController : MonoBehaviour
{
    public const int LinkNumbers = 7;
    public string[] _linkNames = new string[] { "link_1", "link_2", "link_3", "link_4", "link_5", "link_6", "link_7" };
    private float[,] _rotateMatrix = { { 0, -1, 0 }, { 1, 0, 0 }, { 0, -1, 0 }, { 1, 0, 0 }, { 0, -1, 0 }, { 1, 0, 0 }, { 0, 0, 1 } };
    private Transform[] _transforms;
    private int[] _links_index = new int[LinkNumbers];
    public float[] _jointState;
    [HideInInspector]
    public string _jointSource = "rizon_states";


    // Start is called before the first frame update
    void Start()
    {
        _transforms = GetComponentsInChildren<Transform>();


        // Find and check components we are interested in
        if (FindLinkIndexByName(_linkNames) != LinkNumbers)
        {
            Debug.Log("ERROR: Found link numbers is not equal to" + LinkNumbers);
        }

        if (_jointSource == "rizon_states")
        {
            // _jointState = GetComponent<RizonStatesSubscriber>()._jointState;
        }
        else if (_jointSource == "rizon_visual")
        {
            ;
        }


    }

    // Update is called once per frame
    void Update()
    {
        if (_jointState.Length != 0)
        {
            UpdateJointStates(_jointState);
        }
        
    }

    /* local functions -- see function headers for details */

    /*
     * FindLinkIndexByName
     *   DESCRIPTION: Find links' transform according to link names
     *   INPUTS: string[] links -- Name list of links 
     *   OUTPUTS: private Transform[] _links -- Assign links' tranform to local value
     *   RETURN VALUE: int count -- Found #s of link transform
     *   SIDE EFFECT: none
     */
    private int FindLinkIndexByName(string[] links)
    {
        int count = 0;
        foreach (string link in links)
        {
            for (int i = 0; i < _transforms.Length; i++)
            {
                if (link == _transforms[i].name)
                {
                    _links_index[count] = i;
                    count++;
                }
            }
        }
        return count;
    }

    /*
     * UpdateJointStates
     *   DESCRIPTION: Update transform values according to subscribed joint states
     *   INPUTS: double[] position -- Published jont state 's position message
     *   OUTPUTS: localEulerAngles -- Eular angle in degrees relative to the parent's transform
     *   RETURN VALUE: ignored
     *   SIDE EFFECT: none
     */
    public void UpdateJointStates(float[] position)
    {
        for (int i = 0; i < _links_index.Length; i++)
        {
            int index = _links_index[i];

            _transforms[index].localEulerAngles = GetLocalEularAngle(i, position[i]);
        }
    }

    /*
    * GetLocalEularAngle
    *   DESCRIPTION: Transfer the radian position to eular angle in degrees relative to the parent's transform
    *   INPUTS: int index -- Index of joint
    *           float position -- Rotation of the joint in radians
    *   OUTPUTS: none
    *   RETURN VALUE: Vector3 eulars -- Local eular angle in degrees
    *   SIDE EFFECT: none
    */
    private Vector3 GetLocalEularAngle(int index, float position)
    {
        Vector3 eulars = new Vector3(_rotateMatrix[index, 0], _rotateMatrix[index, 1], _rotateMatrix[index, 2]) * position * Mathf.Rad2Deg;

        if (index == 0) { eulars.y = -180 + eulars.y; }
        else if (index == 1) { }
        else if (index == 2) { }
        else if (index == 3) { eulars.y = -180; }
        else if (index == 4) { eulars.y = -180 + eulars.y; }
        else if (index == 5) { }
        else if (index == 6) { }
        return eulars;
    }
}

