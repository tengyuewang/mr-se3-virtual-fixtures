# Meta Quest 3 Unity Project

[Back to the repository overview](../README.md) | [Setup and deployment](Documentation/SETUP.md) | [Architecture](Documentation/ARCHITECTURE.md)

This folder contains the Meta Quest 3 MR interface for the paper *Toward In-Situ SE(3) Virtual Fixture Generation and Editing: A Mixed Reality-Driven Approach*. It provides the headset-side implementation for specifying, visualizing, editing, and transmitting six-degree-of-freedom robot paths.

**Open this `Unity-Quest3` folder in Unity Hub.** Unless stated otherwise, all asset paths and local build commands below are relative to this folder.

## Included features

- Virtual-proxy interaction using hands or controllers.
- Pose sampling with translation and rotation thresholds.
- Lie algebra-based centripetal Catmull-Rom path interpolation in SE(3).
- Interactive handle selection and Laplacian editing of position, orientation, or both.
- QR-based alignment and visualization of the virtual robot and authored path.
- ROS-TCP publication of fitted paths and reception of robot states.

The main scene is [Assets/Scenes/SampleScene.unity](Assets/Scenes/SampleScene.unity). Robot-side VF enforcement, impedance control, and the matching ROS 2 packages are forthcoming components; see the [repository release roadmap](../README.md#release-roadmap).

## Setup and use

### Project configuration

The checked-in project specifies the following versions:

| Component | Version / value |
| --- | --- |
| Unity Editor | 6000.3.13f1 |
| Universal Render Pipeline | 17.3.0 |
| Meta XR SDK / MR Utility Kit | 201.0.0 |
| OpenXR | 1.16.1 |
| Target platform | Android, Meta Quest 3 |
| ROS-TCP port | 10000 |

Dependency versions are recorded in [Packages/manifest.json](Packages/manifest.json) and [Packages/packages-lock.json](Packages/packages-lock.json). Referenced MRTK package archives are included under `Packages/MixedReality`.

### Open and build the Unity project

1. Clone this repository:

   ```bash
   git clone https://github.com/tengyuewang/mr-se3-virtual-fixtures.git
   cd mr-se3-virtual-fixtures/Unity-Quest3
   ```

2. Install Unity **6000.3.13f1** through Unity Hub with **Android Build Support**, **Android SDK and NDK Tools**, and **OpenJDK**.
3. In Unity Hub, add the **`Unity-Quest3` subfolder**, open it, and allow Package Manager to restore the dependencies. This folder contains `Assets`, `Packages`, and `ProjectSettings`; the repository root is not a Unity project.
4. Open [Assets/Scenes/SampleScene.unity](Assets/Scenes/SampleScene.unity) and select Android as the build target.
5. Run **Tools > Release > Validate Project** to check required assets and missing scene scripts. Run the included Edit Mode tests from **Window > General > Test Runner**.
6. Enable Developer Mode on the Quest 3, connect it by USB, and accept the device's debugging prompt.
7. Select **Tools > Release > Build Quest APK**. The output is `Build/MR-SE3-Path-Interface.apk`. Install your locally built APK with:

   ```bash
   adb install -r Build/MR-SE3-Path-Interface.apk
   ```

A prebuilt APK will be uploaded later. Building the Unity application does not install or start the pending ROS 2 and robot-controller components. See [Documentation/SETUP.md](Documentation/SETUP.md) for the current device and deployment instructions.

### ROS connection and coordinate frames

The current Unity code publishes fitted paths and receives robot states through the ROS-TCP Connector:

| Direction | Topic | Message |
| --- | --- | --- |
| Unity to ROS | `/waypoint` | `flexiv_msgs/TrajectoryWaypoint` |
| ROS to Unity | `/robot_states` | `flexiv_msgs/RobotStates` |

Set the ROS host in [Assets/Resources/ROSConnectionPrefab.prefab](Assets/Resources/ROSConnectionPrefab.prefab). Its checked-in address, `192.168.0.2`, is an example and must be replaced with the reachable address of your ROS computer. The headset and ROS host must be able to communicate over TCP port `10000`.

Fitted poses are transformed from the Unity world frame to the robot-base frame and then converted to ROS FLU coordinates before publication. The default published frame ID is `rizon_base`. [Assets/RosMessages/Flexiv/msg](Assets/RosMessages/Flexiv/msg) contains the generated Unity C# message classes, not a standalone ROS 2 package. The matching ROS 2 message definitions, communication packages, and launch instructions will be provided with the ROS 2 release.

QR-based alignment is implemented in `TrackableManager`. The current code supports `LegacyYawOffset` (the default, with fixed offsets) and `FullPose` (using a measured base-to-marker transform). The paper describes full rigid-transform registration; configure and validate the transform for your own setup rather than assuming the default offsets apply to it.

### Path-processing parameters

The checked-in scene uses the following authoring parameters. The sampling thresholds, characteristic length, and Laplacian parameters correspond to Table I of the paper; output spacing and refit interval are Unity implementation settings.

| Parameter | Value | Unity field |
| --- | ---: | --- |
| Translation sampling threshold | 0.015 m | `PathRecorder.minPositionDistance` |
| Rotation sampling threshold | 0.15 rad | `PathRecorder.minRotationAngleRadians` |
| SE(3) characteristic length | 0.1 m/rad | `PathRecorder.characteristicLength` |
| Fitted-path spacing setting | 0.004 m | `PathRecorder.curveSpacing` |
| Refit interval | 1/60 s | `PathRecorder.refitInterval` |
| Handle constraint weight, lambda | 500 | `WaypointMarkerManager.constraintWeight` |
| Regularization weight, mu | 0.5 | `WaypointMarkerManager.regularizationWeight` |
| Edit falloff width, sigma | 3 waypoint indices | `WaypointMarkerManager.falloffSigma` |

The paper's robot control and VF update rates are **1000 Hz** and **100 Hz**, respectively. These belong to the forthcoming robot-side implementation and are separate from the Unity refit interval.

## Code guide

| Location | Purpose |
| --- | --- |
| [Assets/Scripts/PathRecorder.cs](Assets/Scripts/PathRecorder.cs) | Pose sampling and path-refit scheduling. |
| [Assets/Scripts/CurvedLineFitting.cs](Assets/Scripts/CurvedLineFitting.cs) | SE(3) logarithm/exponential maps and centripetal Catmull-Rom interpolation. |
| [Assets/Scripts/LaplacianEditing.cs](Assets/Scripts/LaplacianEditing.cs) | Shape-preserving SE(3), position-only, and rotation-only path editing. |
| [Assets/Scripts/WaypointMarkerManager.cs](Assets/Scripts/WaypointMarkerManager.cs) | Handle selection, proximity feedback, and interactive editing. |
| [Assets/Scripts/TrackableManager.cs](Assets/Scripts/TrackableManager.cs) | QR-based virtual-to-robot alignment. |
| [Assets/Scripts/WaypointPublish.cs](Assets/Scripts/WaypointPublish.cs) | Robot-frame conversion and path publication. |
| [Assets/Scripts/SubscribeRobotJoint.cs](Assets/Scripts/SubscribeRobotJoint.cs) | Robot-state reception for visualization. |
| [Assets/Scenes](Assets/Scenes) and [Assets/Prefabs](Assets/Prefabs) | MR scene and interaction/visualization assets. |
| [Assets/URDF](Assets/URDF) | Robot description and mesh resources; not the robot controller. |
| [Assets/Tests/Editor](Assets/Tests/Editor) | Focused spline and Laplacian editing tests. |
| [Assets/Editor/ReleaseAudit.cs](Assets/Editor/ReleaseAudit.cs) | Project validation and Quest APK build commands. |
| [Packages](Packages) and [ProjectSettings](ProjectSettings) | Unity dependencies and project configuration. |

Generated folders such as `Library`, `Temp`, `Logs`, `Build`, and `UserSettings` are excluded from version control.

## Scope and pending utilities

The current Laplacian formulation operates in logarithmic coordinates relative to a fixed reference pose. As discussed in the paper, it is a local approximation rather than an intrinsic Laplacian on SE(3).

Path reset and Inspector-based parameter adjustment are included. Dedicated pose-insertion/removal controls described in Section III-B of the paper are not exposed in the current public scripts and will be added with their usage instructions in a subsequent update.

A prebuilt Quest 3 APK is also planned. The complete release status, additional platform support, robot software, and user-study materials are tracked in the [root README](../README.md#release-roadmap).

## Safety

This is research software. Validate coordinate frames, units, workspace limits, network behavior, and emergency-stop handling in simulation and at reduced speed before commanding physical hardware. The Unity interface is not a safety-rated controller.

## License

The original project code is covered by the repository's [MIT License](../LICENSE). Third-party components retain their own licenses; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
