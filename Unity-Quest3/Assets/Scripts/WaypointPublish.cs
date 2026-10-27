using System.Collections.Generic;
using RosMessageTypes.Flexiv;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;

/// <summary>Publishes the fitted path in the robot base frame.</summary>
public class WaypointPublish : MonoBehaviour
{
    [SerializeField] private string pubTopic = "/waypoint";
    [SerializeField] private Transform baseLink;
    [SerializeField] private PathRecorder pathRecorder;
    [SerializeField] private string frameId = "rizon_base";

    private ROSConnection ros;

    private void Start()
    {
        if (pathRecorder == null)
            pathRecorder = GetComponent<PathRecorder>();

        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<TrajectoryWaypointMsg>(pubTopic);
    }

    private void Update()
    {
        if (pathRecorder != null && pathRecorder.goPublish)
        {
            pathRecorder.goPublish = false;
            PublishWaypoint();
        }
    }

    public void PublishWaypoint()
    {
        if (pathRecorder == null || baseLink == null)
        {
            Debug.LogError("[WaypointPublish] PathRecorder and baseLink are required.", this);
            return;
        }

        IReadOnlyList<Pose> fitted = pathRecorder.FittedPoses;
        int count = fitted.Count > 0 ? fitted.Count : pathRecorder.Transforms.Count;
        PoseMsg[] messages = new PoseMsg[count];

        for (int i = 0; i < count; i++)
        {
            Pose worldPose = fitted.Count > 0
                ? fitted[i]
                : new Pose(pathRecorder.Transforms[i].position, pathRecorder.Transforms[i].rotation);

            Vector3 localPosition = baseLink.InverseTransformPoint(worldPose.position);
            Quaternion localRotation = Quaternion.Inverse(baseLink.rotation) * worldPose.rotation;
            messages[i] = new PoseMsg
            {
                position = localPosition.To<FLU>(),
                orientation = localRotation.To<FLU>()
            };
        }

        HeaderMsg header = new HeaderMsg { frame_id = frameId };
        TrajectoryWaypointMsg trajectory = new TrajectoryWaypointMsg
        {
            header = header,
            id_array = new Int64MultiArrayMsg
            {
                layout = new MultiArrayLayoutMsg(),
                data = new long[0]
            },
            pose_array = new PoseArrayMsg
            {
                header = header,
                poses = messages
            }
        };

        ros.Publish(pubTopic, trajectory);
    }
}
