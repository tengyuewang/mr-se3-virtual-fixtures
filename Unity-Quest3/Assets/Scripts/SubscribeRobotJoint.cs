using RosMessageTypes.Flexiv;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;

public class SubscribeRobotJoint : MonoBehaviour
{
    private ROSConnection ros;

    [SerializeField]
    private string subTopic = "/robot_states";

    public float[] jointState;

    public bool isStepOn = false;

    // Start is called before the first frame update
    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ROSConnection.GetOrCreateInstance().Subscribe<RobotStatesMsg>(subTopic, ReceiveCallback);
        jointState = new float[7];
        transform.GetComponent<JointController>()._jointState = jointState;
    }

    private void ReceiveCallback(RobotStatesMsg msg)
    {
        for (int i = 0; i < jointState.Length; i++) { jointState[i] = (float)msg.q[i]; }
        isStepOn = msg.digital_input_0;
    }


    // Update is called once per frame
    void Update()
    {

    }
}
