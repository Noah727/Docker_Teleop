# Gripper / Wrist Camera

There are two camera-related paths in this project.

## Unity Wrist Camera

Unity has a scene camera named `GripperDataCamera` attached near the gripper. It is used for headset-side preview and data recording.

Main script:

```text
UnityApp/Assets/Scripts/GripperCameraRecorder.cs
```

Scene object:

```text
UnityApp/Assets/Scenes/Ur5e_Working 1.unity > GripperDataCamera
```

Important fields:

- `Width` / `Height`: recording and preview RenderTexture size.
- `Capture Every N Frames`: frame-save interval while recording.
- `Assign Runtime Render Texture`: should stay enabled.
- `Force Render Before Capture`: usually disabled for performance.
- `Create Floating Panel`: creates the camera preview/control panel.

## Recording Controls

- Left controller `X`: start / stop recording.
- Floating panel `Record` button: start / stop recording.
- Floating panel `Capture Frame`: save one frame.

See [RECORDING.md](RECORDING.md).

## Gazebo Gripper Camera

The Gazebo camera, when enabled in the robot description, publishes ROS topics such as:

```bash
/gripper_camera/image_raw
/gripper_camera/camera_info
```

Inspect topics:

```bash
cd ros_backend1.0
CONTAINER=motion_planner_10
ROS_ENV='source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash'
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic list | grep gripper_camera"
```

Check image rate:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic hz /gripper_camera/image_raw"
```

If `/gripper_camera/camera_info` is missing, inspect the Gazebo camera plugin and bridge configuration in `ros_backend1.0/simulation` and robot xacro files.
