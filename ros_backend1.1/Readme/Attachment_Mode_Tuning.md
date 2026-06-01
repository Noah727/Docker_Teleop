# Attachment Mode Tool Offset Tuning

Attachment mode tries to make the robot EE follow the controller pose in the movable Unity workspace frame.

## Backend Default Parameters

Left arm:

```text
ros_backend1.1/src/teleop_bridge/config/teleop_tuning_dual_left.yaml
```

Right arm:

```text
ros_backend1.1/src/teleop_bridge/config/teleop_tuning_dual_right.yaml
```

Important parameters:

```yaml
attachment_enable_rotation: true
attachment_use_absolute_rotation: true
attachment_tool_position_offset_xyz: [0.0, 0.0, 0.0]
attachment_tool_rotation_offset_xyzw: [0.0, 0.0, 0.0, 1.0]
attachment_kp_angular: 1.4
attachment_max_angular_speed: 0.65
attachment_angular_deadband: 0.03
```

`attachment_tool_position_offset_xyz` is the static translation from the controller/hand pose to the desired EE/tool origin, in meters. It is expressed in the Gazebo/ROS controller frame after Unity-to-Gazebo conversion. This means it moves with the controller orientation.

`attachment_tool_rotation_offset_xyzw` is the static quaternion offset applied after the controller pose is converted into Gazebo/ROS coordinates. If the EE points 90 degrees away from the controller ray, this is the backend-side default place to encode a fixed tool offset.

The mapper code that consumes this is:

```text
ros_backend1.1/src/teleop_bridge/teleop_bridge/received_pose_to_target_twist.py
```

Look for:

```python
_compute_attachment_position_target()
_compute_attachment_rotation_target()
attachment_tool_position_offset_xyz
attachment_tool_rotation_offset_xyzw
```

## Unity Live Offset

The Quest app now sends an additional per-arm live offset in the TCP packet. This is useful for calibration without rebuilding ROS.

Unity source:

```text
UnityApp/Assets/Scripts/HandPoseSender.cs
UnityApp/Assets/Scripts/MRCentralControlPanel.cs
```

Panel workflow:

1. Open the control panel.
2. Go to `Attach`.
3. Turn `Adjust ON`.
4. Toggle attachment mode for the arm:
   - left arm: `Y`
   - right arm: `B`
5. Hold the arm's rotation button and rotate the controller:
   - left arm: hold `X`
   - right arm: hold `A`
6. Release the button when the EE points correctly.

The adjusted offset is sent continuously to ROS and is saved locally in Unity `PlayerPrefs`. Use `Reset L` or `Reset R` on the Attach page to return that arm to zero live offset.

## Manual Numeric Offset

The Attach page has fields for each arm:

```text
position offset: x,y,z meters in Unity workspace axes
rotation offset: x,y,z degrees
```

Unity workspace axes:

```text
x = right/left
y = up/down
z = forward/back
```

The live Unity rotation offset is applied as:

```text
effective_controller_rotation = controller_rotation * live_rotation_offset
```

Then ROS converts that effective controller rotation into Gazebo/world and applies the backend `attachment_tool_rotation_offset_xyzw`.

The live Unity position offset is different from `attachment_tool_position_offset_xyz`: the live field is in Unity workspace axes and is useful for quick MR calibration. The backend tool position offset is a fixed hand-to-tool translation that rotates with the controller.

## Practical Tuning Tip

If the EE direction is consistently 90 degrees off, first try the live Attach page adjustment. Once the value feels correct, copy the equivalent fixed quaternion into `attachment_tool_rotation_offset_xyzw` later if you want the same offset to be the backend default.

If the EE follows the controller with the correct orientation but its origin is consistently too far forward/back/up/down from the user's hand, tune `attachment_tool_position_offset_xyz` in the relevant dual-arm YAML file.
