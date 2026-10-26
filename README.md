# MR SE(3) Path Interface for Meta Quest 3

This Unity project implements the Meta Quest 3 mixed-reality interface used to demonstrate, edit, visualize, and transmit six-degree-of-freedom robot paths. It contains the headset-side implementation only; the ROS 2 and robot-controller repositories are separate components of the full system.

## Core features

- controller/hand-driven pose sampling using both translation and rotation thresholds;
- centripetal Catmull–Rom interpolation in SE(3);
- interactive waypoint selection and position/orientation editing;
- Laplacian shape-preserving path deformation;
- QR-based virtual-to-robot frame calibration;
- visualization of the robot, sampled waypoints, and fitted path; and
- ROS-TCP publication of fitted poses in the robot base frame.

The main scene is `Assets/Scenes/SampleScene.unity`.

## Tested project configuration

| Component | Version / value |
| --- | --- |
| Unity | 6000.3.13f1 |
| Render pipeline | Universal RP 17.3.0 |
| Meta XR SDK / MRUK | 201.0.0 |
| OpenXR | 1.16.1 |
| Target | Android, Meta Quest 3 |
| ROS-TCP port | 10000 |

Package versions are locked in `Packages/packages-lock.json`. The MRTK preview packages required by the scene are vendored as `.tgz` files under `Packages/MixedReality` so that their exact versions remain reproducible.

## Quick start

1. Install Unity 6000.3.13f1 with Android Build Support, the Android SDK/NDK tools, and OpenJDK.
2. Open this directory from Unity Hub and allow Package Manager to restore dependencies.
3. Open `Assets/Scenes/SampleScene.unity`.
4. In `Assets/Resources/ROSConnectionPrefab.prefab`, replace the example ROS host `192.168.0.2` with the address of the computer running the ROS-TCP endpoint.
5. Put the Quest 3 and ROS host on the same network, then enter Play Mode for editor checks or build an APK with **Tools > Release > Build Quest APK**.

See [Documentation/SETUP.md](Documentation/SETUP.md) for device, ROS, calibration, and deployment details.

## Path-processing defaults

| Parameter | Default | Meaning |
| --- | ---: | --- |
| Translation sampling threshold | 0.015 m | Record when translation exceeds this value |
| Rotation sampling threshold | 0.15 rad | Record when angular change exceeds this value |
| Fitted-path spacing | 0.004 m | Approximate output sample spacing |
| SE(3) characteristic length | 0.1 m | Translation/rotation metric balance |
| Laplacian constraint weight | 500 | Weight of the manipulated handle pose |
| Laplacian regularization | 0.5 | Weight of the distance falloff term |
| Falloff sigma | 3 waypoints | Geodesic editing influence |

These values are serialized in the scene and can be changed on `PathRecorder` and `WaypointMarkerManager` components.

## ROS interface

| Direction | Topic | Message |
| --- | --- | --- |
| Unity → ROS | `/waypoint` | `flexiv_msgs/TrajectoryWaypoint` |
| ROS → Unity | `/robot_states` | `flexiv_msgs/RobotStates` |
| ROS → Unity (optional visualization) | `/curve` | `geometry_msgs/PoseArray` |
| ROS → Unity (optional visualization) | `/target_pose` | `geometry_msgs/Pose` |
| Unity → ROS (optional text-path tool) | `/waypoint2` | `flexiv_msgs/TrajectoryWaypoint` |

Unity world poses are converted to the robot base frame and then to ROS FLU coordinates before publication. The generated C# message classes are under `Assets/RosMessages`.

## Repository layout

- `Assets/Scripts`: interaction, sampling, interpolation, editing, calibration, and ROS bridge code;
- `Assets/Scenes`: the deployable MR scene;
- `Assets/Prefabs`: interaction and robot visualization prefabs;
- `Assets/URDF`: Flexiv Rizon model resources;
- `Assets/RosMessages`: generated ROS message classes;
- `Assets/Tests/Editor`: focused tests for SE(3) fitting and Laplacian editing;
- `Packages`: locked Unity dependencies and vendored MRTK packages; and
- `Documentation`: architecture and deployment notes.

## Validation

Use **Tools > Release > Validate Project** to check that required assets exist and the main scene contains no missing scripts. Run the Edit Mode tests from **Window > General > Test Runner** before publishing a release. Generated folders (`Library`, `Temp`, `Logs`, `Build`, and `UserSettings`) are intentionally excluded from Git.

## Safety

This is research software. Validate coordinate frames, units, workspace limits, network behavior, and emergency-stop handling in simulation and at reduced speed before commanding physical hardware. The Unity interface is not a safety-rated controller.

## License

The original project code is released under the [MIT License](LICENSE). Third-party components retain their own licenses; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
