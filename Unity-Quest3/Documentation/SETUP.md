# Setup and deployment

[Unity project guide](../README.md) | [Repository overview](../../README.md)

This guide covers both the published Quest 3 APK and the `Unity-Quest3` source project. Asset paths and source-build commands are relative to `Unity-Quest3`; run downloaded-APK commands from the directory containing the download.

## 1. Choose a prebuilt APK or a source build

### Prebuilt APK

Download [mr-se3-virtual-fixtures.apk](https://github.com/tengyuewang/mr-se3-virtual-fixtures/releases/download/quest3-apk-2026-11-11/mr-se3-virtual-fixtures.apk) from the [November 11, 2026 release](https://github.com/tengyuewang/mr-se3-virtual-fixtures/releases/tag/quest3-apk-2026-11-11). You do not need Unity Editor to install this APK; you need Android SDK Platform-Tools (`adb`) and a Quest 3 with Developer Mode enabled.

| APK property | Value |
| --- | --- |
| Application | MR SE3 Path Interface |
| Package ID | `org.research.mrse3path` |
| Version name / code | `1.1.0` / `1` |
| CPU architecture | `arm64-v8a` |
| Minimum / target Android API | `32` / `34` |
| File size | 109,102,756 bytes (104.05 MiB) |
| Build date | November 11, 2026 |

The release also includes [SHA256SUMS.txt](https://github.com/tengyuewang/mr-se3-virtual-fixtures/releases/download/quest3-apk-2026-11-11/SHA256SUMS.txt). The expected APK SHA-256 is:

```text
72ed430f7df9382f10182bc0f1e94e07f056e931d28b1668eaa3dd8649b35449
```

On Windows, compute the downloaded file's checksum with:

```powershell
Get-FileHash .\mr-se3-virtual-fixtures.apk -Algorithm SHA256
```

The prebuilt APK's package metadata and file checksum have been inspected; a reproducible match to the repository source has not been independently established. Integration with physical hardware requires separately supplied ROS 2 and robot-control software.

### Build from source

Install Unity 6000.3.13f1 through Unity Hub with:

- Android Build Support;
- Android SDK and NDK Tools; and
- OpenJDK.

Open the repository's `Unity-Quest3` folder as a Unity project in Unity Hub. Do not select the repository root. The first import can take several minutes because registry and Git packages must be resolved. Keep `Packages/packages-lock.json` under version control.

Select Android as the active build target. The checked-in scene list already contains `Assets/Scenes/SampleScene.unity`. The Android application identifier is `org.research.mrse3path`.

## 2. Install on Quest 3

Enable Developer Mode for the headset, connect it by USB, accept the debugging prompt, and verify that it is visible to `adb`. The project requests hand tracking, passthrough, anchors, and scene permissions in `Assets/Plugins/Android/AndroidManifest.xml`.

For the downloaded prebuilt APK, run the following from its download directory:

```text
adb devices
adb install -r mr-se3-virtual-fixtures.apk
```

For a local source build, use **Tools > Release > Build Quest APK** in Unity. The APK is written to `Build/MR-SE3-Path-Interface.apk`; `Build` is ignored by Git. From the `Unity-Quest3` directory, install with:

```text
adb install -r Build/MR-SE3-Path-Interface.apk
```

Launch **MR SE3 Path Interface** on the headset. On first launch, grant the requested spatial and hand-tracking permissions.

## 3. ROS-TCP connection

For integration with independently supplied robot software, run a compatible ROS-TCP endpoint on the ROS 2 computer and expose TCP port 10000 to the headset network. Edit `Assets/Resources/ROSConnectionPrefab.prefab` and set `m_RosIPAddress` to that computer's reachable IPv4 address. The checked-in `192.168.0.2` value is an example only.

Editing the prefab changes a subsequent source build, not the already downloaded APK. For the prebuilt application, the ROS endpoint must match the network configuration built into the application. This guide covers Unity-side configuration; project-specific ROS 2 packages and end-to-end robot deployment instructions are outside the repository's public scope.

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
- Confirm the configured path topic (`/waypoint2` in the sample scene; `/waypoint` is the script default) uses positions in metres and normalized orientations.
- Test the entire path in simulation or without motor power.
- Verify workspace constraints, collision checking, speed limits, and emergency stop outside Unity.
