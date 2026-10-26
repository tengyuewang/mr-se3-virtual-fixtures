# Toward In-Situ SE(3) Virtual Fixture Generation and Editing: A Mixed Reality-Driven Approach

**Tengyue Wang, Zhefan Lin, Leixin Chang, Songjie Xiao, Kaiwen Hong, Xianhao Chen, and Liangjing Yang**

*IEEE Robotics and Automation Letters*, accepted October 25, 2026.

[Project repository](https://github.com/tengyuewang/mr-se3-virtual-fixtures) | [Setup and deployment](Documentation/SETUP.md) | [Unity architecture](Documentation/ARCHITECTURE.md)

This repository accompanies our work on mixed reality (MR)-driven, in-situ generation and editing of full SE(3) virtual fixtures (VFs) for physical human-robot interaction. Users specify a six-degree-of-freedom motion path by manipulating a virtual proxy in the shared workspace, refine its position and orientation through interactive editing, and deploy the resulting path as a virtual fixture for translational and rotational haptic guidance.

> **Release status:** This is an incremental release. The Meta Quest 3 Unity project is available now. The additional HoloLens 2 project, prebuilt Quest 3 APK, robot-side C++ implementation, ROS 2 packages, and user-study materials will be added in subsequent updates. The current repository alone does not yet provide the complete robot-side system required to reproduce the physical experiments.

## Overview

The framework combines four stages:

1. **MR-driven motion specification:** manipulate a virtual proxy using hands or controllers and sample poses according to translational and rotational thresholds.
2. **SE(3) path generation:** map sampled poses into Lie algebra coordinates relative to a reference pose, interpolate them with a centripetal Catmull-Rom spline, and map the interpolated path back to SE(3).
3. **Interactive path editing:** manipulate virtual handles to refine position, orientation, or both. Laplacian editing preserves local path shape while limiting the extent of edit propagation.
4. **Physical VF enforcement:** deploy the edited path to the robot and provide compliant translational and rotational guidance through path-parameterized VF enforcement and task-space impedance control. **The robot-side implementation will be uploaded in a subsequent update.**

The experiments reported in the paper use a **Meta Quest 3** and a **7-DoF Flexiv Rizon 4** manipulator. The forthcoming **HoloLens 2** implementation is an additional MR interface; it is not the headset used in the reported user study.

## Available and forthcoming materials

The following table tracks the implementation and supporting materials planned for this public release. **To be added** means that the material is not yet included and will be uploaded in a subsequent update; no release dates have been set.

| Material | Status | Contents / location |
| --- | --- | --- |
| Meta Quest 3 Unity project | **Available** | This repository: C# interaction code, pose sampling, SE(3) path generation and visualization, interactive path editing, scenes, prefabs, and project settings. |
| Additional authoring operations described in Section III-B | **Partially available; to be completed** | Path reset and Inspector-based parameter adjustment are included. Dedicated pose-insertion/removal controls are not exposed in the current public scripts and will be added with their usage instructions in a subsequent update. |
| Additional HoloLens 2 Unity project | **To be added** | HoloLens 2 MR interface, including the corresponding interaction, sampling, path generation, visualization, and editing code. |
| Prebuilt Meta Quest 3 APK | **To be added** | An installable APK for direct deployment. The current release provides source and a build command, but no downloadable prebuilt APK. |
| Robot-side C++ implementation | **To be added** | Path deployment, virtual fixture enforcement, task-space impedance control, and the software interface for the Flexiv Rizon 4 used in the study. |
| ROS 2 communication packages | **To be added** | Packages for exchanging generated paths, control parameters, and robot states between Unity and the robot-side modules. Unity-side generated C# message classes are already included; the corresponding ROS 2 packages are not. |
| System setup and deployment documentation | **Partially available; to be extended** | [Unity/Quest setup](Documentation/SETUP.md) and [Unity architecture](Documentation/ARCHITECTURE.md) are available. Complete ROS 2 and robot-side build, launch, configuration, and end-to-end deployment instructions will accompany the remaining components. |
| Informed consent form | **To be added** | The participant-facing consent form used in the study. |
| Participant study protocol | **To be added** | Familiarization, path-authoring and task-execution procedures, and condition-ordering instructions. |
| Study questionnaires | **To be added** | The questionnaires administered to participants, including NASA-TLX, the System Usability Scale (SUS), and the open-ended feedback prompts. |

This status table will be updated as the remaining materials become available. Participant-level recordings, raw questionnaire responses, and analysis scripts are not included in the current release; the planned study materials above refer to the forms, protocol, and questionnaire instruments.

## Results reported in the paper

The study involved **14 participants** performing a steady-hand task, guiding a torus-shaped end-effector along a spatial wire while avoiding contact. Path authoring compared screen-based specification (SBS), OptiTrack-based demonstration (OTD), MR generation without editing (MRG), and MR generation with editing (MRGE). Task execution additionally included unassisted operation (NO-VF) and translation-only guidance using the MRGE-authored path (MRGE-T).

For the complete MRGE framework, the paper reports:

- **Path authoring:** mean translational error of **2.33 +/- 0.53 mm** and mean rotational error of **0.13 +/- 0.02 rad**; total authoring time was **27.6% lower than SBS**.
- **VF-assisted task execution:** **62.7% lower mean rotational error** and **89.2% lower contact percentage** compared with NO-VF.
- **User experience:** lower mean perceived workload and the highest mean SUS score among the evaluated conditions, **74.3 +/- 12.5**.

These are results from the paper's complete experimental system, not measurements from a fresh build of this source release. The evaluation covers one steady-hand task in a controlled, largely static environment.

## Getting started: Meta Quest 3

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
   ```

2. Install Unity **6000.3.13f1** through Unity Hub with **Android Build Support**, **Android SDK and NDK Tools**, and **OpenJDK**.
3. Add the cloned repository root to Unity Hub, open it, and allow Package Manager to restore the dependencies.
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

## Scope and future research

The current Laplacian formulation operates in logarithmic coordinates relative to a fixed reference pose. As discussed in the paper, it is a local approximation rather than an intrinsic Laplacian on SE(3).

Beyond releasing the remaining materials listed above, the paper identifies future research on cross-task evaluation, adaptive and deformable VFs using HMD perception for online constraint updates, and contact-aware/hybrid motion-force guidance. These are research directions, not implemented features of the current release.

## Citation

If you use this work, please cite the accompanying paper. This citation reflects the accepted manuscript; final DOI, volume, issue, and page information will be updated when available.

```bibtex
@article{wang2026mrse3vf,
  author  = {Wang, Tengyue and Lin, Zhefan and Chang, Leixin and Xiao, Songjie and Hong, Kaiwen and Chen, Xianhao and Yang, Liangjing},
  title   = {Toward In-Situ {SE(3)} Virtual Fixture Generation and Editing: A Mixed Reality-Driven Approach},
  journal = {IEEE Robotics and Automation Letters},
  year    = {2026},
  note    = {Accepted manuscript}
}
```

## Safety

This is research software. Validate coordinate frames, units, workspace limits, network behavior, and emergency-stop handling in simulation and at reduced speed before commanding physical hardware. The Unity interface is not a safety-rated controller.

## License

The original project code is released under the [MIT License](LICENSE). Third-party components retain their own licenses; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
