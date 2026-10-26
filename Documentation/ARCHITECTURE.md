# Architecture

## Runtime data flow

1. `PathRecorder` samples the pose of the manipulated path-generation object when either translation or rotation crosses its configured threshold.
2. `CentripetalCatmullRomSE3` maps poses into one SE(3) tangent space, applies non-uniform cubic Hermite interpolation with centripetal knots, and maps the samples back to poses.
3. `WaypointMarkerManager` finds the closest visible waypoint to each hand and enables the configured translation and/or rotation manipulation.
4. `LaplacianEditing` propagates handle changes while preserving the path's discrete Laplacian coordinates. Endpoint and distance-falloff constraints limit unintended global deformation.
5. `WaypointPublish` converts the fitted world-space path into the Flexiv base frame, converts Unity coordinates to ROS FLU, and publishes a `TrajectoryWaypoint` message.
6. `SubscribeRobotJoint` updates the visual robot model from `/robot_states`.

## Coordinate frames

- `V`: Unity virtual-world frame;
- `B`: Flexiv robot-base frame;
- `Q`: tracked QR-marker frame; and
- ROS messages use FLU coordinates after conversion by the Unity Robotics package.

The legacy calibration mode preserves the study setup's yaw-only alignment and measured offsets. Full-pose mode uses the measured transform from robot base to marker, `T_B_Q`, and computes:

`T_V_B = T_V_Q * inverse(T_B_Q)`

Configure the marker translation and Euler rotation on `TrackableManager`. The QR payload must exactly match `ROBOT` unless `markerPayload` is changed.

## Core source files

- `PathRecorder.cs`: pose acquisition, marker lifecycle, refit scheduling;
- `CurvedLineFitting.cs`: SE(3) logarithm/exponential and spline fitting;
- `WaypointMarkerManager.cs`: hand proximity, handle selection, editing loop;
- `LaplacianEditing.cs`: SE(3), position-only, and rotation-only deformation;
- `WaypointPublish.cs`: robot-frame conversion and path publication;
- `TrackableManager.cs`: QR calibration; and
- `SubscribeRobotJoint.cs`: robot-state visualization.
