# Setup and deployment

## 1. Unity and Android

Install Unity 6000.3.13f1 through Unity Hub with:

- Android Build Support;
- Android SDK and NDK Tools; and
- OpenJDK.

Open the repository root as a Unity project. The first import can take several minutes because registry and Git packages must be resolved. Keep `Packages/packages-lock.json` under version control.

Select Android as the active build target. The checked-in scene list already contains `Assets/Scenes/SampleScene.unity`. The Android application identifier is `org.research.mrse3path`.

## 2. Quest 3

Enable Developer Mode for the headset, connect it by USB, accept the debugging prompt, and verify that it is visible to `adb`. The project requests hand tracking, passthrough, anchors, and scene permissions in `Assets/Plugins/Android/AndroidManifest.xml`.

Build from **Tools > Release > Build Quest APK**. The APK is written to `Build/MR-SE3-Path-Interface.apk`; `Build` is ignored by Git. Install with:

```text
adb install -r Build/MR-SE3-Path-Interface.apk
```

On first launch, grant the requested spatial and hand-tracking permissions.

## 3. ROS-TCP connection

Run a ROS-TCP endpoint on the ROS 2 computer and expose TCP port 10000 to the headset network. Edit `Assets/Resources/ROSConnectionPrefab.prefab` and set `m_RosIPAddress` to that computer's reachable IPv4 address. The checked-in `192.168.0.2` value is an example only.

The Unity and robot-side packages must use compatible `flexiv_msgs` definitions. If the ROS definitions change, regenerate `Assets/RosMessages` with the ROS-TCP Connector message-generation tool.

## 4. Robot and QR calibration

Use a QR code whose decoded payload is exactly `ROBOT`. Mount it rigidly relative to the robot base.

Two calibration modes are available on `TrackableManager`:

- **LegacyYawOffset** (default) reproduces the existing study setup using yaw plus a 0.23 m horizontal and 0.01 m vertical offset.
- **FullPose** uses a measured six-degree-of-freedom transform from the robot base to the QR marker. Enter that transform in `robotBaseToMarkerPosition` and `robotBaseToMarkerEulerDegrees`.

The calibration control in the hand menu enables `TrackableManager` while calibration is active. Confirm the virtual base alignment visually before enabling robot motion.

## 5. Pre-deployment checklist

- Run **Tools > Release > Validate Project**.
- Run all Edit Mode tests in Unity Test Runner.
- Confirm the ROS host and port.
- Confirm `/robot_states` is received and joint ordering matches the visual model.
- Confirm `/waypoint` positions are expressed in metres and orientations are normalized.
- Test the entire path in simulation or without motor power.
- Verify workspace constraints, collision checking, speed limits, and emergency stop outside Unity.
