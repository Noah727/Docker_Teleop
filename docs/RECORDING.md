# Headset Data Recording

This file documents how to record images from the Unity gripper-mounted camera while running the app in the headset.

## 1) What Is Being Recorded

The Unity scene has a camera attached to the gripper:

```text
Ur5e_Working 1.unity
UR5e/.../robotiq_hande_link/GripperDataCamera
```

The recording script is:

```text
UnityApp/Assets/Scripts/GripperCameraRecorder.cs
```

It records PNG frames from `GripperDataCamera`.

The floating preview panel and UI buttons are excluded from the gripper camera recording, so they should not appear in the saved frames.

The yellow camera marker is controlled by `GripperCameraRecorder`:

- `showSceneMarker`: draws the editor Gizmo marker.
- `createRuntimeSceneMarker`: creates a real editor/play-mode child object named `GripperDataCamera_VisibleMarker` under `GripperDataCamera`.
- `rebuildSceneMarkerFromSettings`: one-shot checkbox that deletes/recreates the marker visuals from the numeric settings below.
- `runtimeSceneMarkerLayer`: defaults to layer `0` (`Default`) so it is visible in Scene view and headset/Game view.
- `markerBoxSize`, `markerForwardLength`, `markerFrustumHalfSize`, and `markerLineWidth` control its size. `markerForwardLength` is the distance from the camera body to the frustum plane.

After the marker exists, you can manually edit `GripperDataCamera_VisibleMarker`, `CameraBody`, and `Frustum` in the hierarchy. Those manual edits are preserved across script reloads/builds unless you enable `Rebuild Scene Marker From Settings`.

The marker is temporarily hidden only while a frame is captured, so it should not appear in the saved PNG images.

If the marker does not appear immediately after Unity recompiles, select `GripperDataCamera` and toggle `Create Runtime Scene Marker` off/on, or reopen the scene. Unity should then create `GripperDataCamera_VisibleMarker` as a normal child object in the hierarchy.

## 2) Headset Controls

When running inside the Quest headset:

- Left controller `X`: start/stop recording
- Floating control panel: shows live gripper camera preview and `IDLE` / `REC` status
- Floating panel `Record` / `Stop` button: also toggles recording if XR UI pointer interaction is working
- Floating panel `Capture Frame` button: captures one frame if XR UI pointer interaction is working
- Floating panel drag: release right grip so teleop is disengaged, point the left controller at the panel, hold left trigger, move the panel, then release left trigger

In the Unity Editor:

- Keyboard `R`: start/stop recording
- Keyboard `P`: capture one frame

## 3) Recommended Recording Flow

1. Start the ROS/Gazebo backend as usual.

```bash
cd ros_backend1.0
./scripts/backend10_lifecycle.sh status
```

2. Build or run the Unity app on the Quest.

3. Put on the headset.

4. Confirm the floating panel appears on the left side of the view.

5. Optional: release right grip, point the left controller at the panel, hold left trigger, drag the panel to a comfortable place, then release left trigger.

6. Press left controller `X`.

7. Confirm the floating panel status changes from `IDLE` to `REC`.

8. Perform the robot/task motion.

9. Press left controller `X` again to stop.

10. Confirm the floating panel status returns to `IDLE`.

## 4) Where Files Are Saved

The script saves frames under Unity's `Application.persistentDataPath`.

For the Quest/Android build, expected path:

```text
/storage/emulated/0/Android/data/com.DefaultCompany.Myproject/files/GripperCameraRecordings
```

Inside that folder:

- Normal recordings are saved in timestamp folders, for example:

```text
20260416_143015/
```

- Single-frame captures are saved in:

```text
single_frame/
```

Each image filename looks like:

```text
gripper_camera_YYYYMMDD_HHMMSS_mmm_FRAME.png
```

Example:

```text
gripper_camera_20260416_143015_281_00012345.png
```

## 5) Pull Data From Quest To Mac

Connect the Quest to the Mac with USB.

Check ADB sees the headset:

```bash
adb devices
```

Expected:

```text
List of devices attached
<device_id>    device
```

Pull the recording folder:

```bash
cd /path/to/robot-teleop-project
adb pull "/storage/emulated/0/Android/data/com.DefaultCompany.Myproject/files/GripperCameraRecordings" ./GripperCameraRecordings
```

Open the pulled folder:

```bash
open ./GripperCameraRecordings
```

## 6) Check Files Before Pulling

List recording folders on the Quest:

```bash
adb shell ls -lah "/storage/emulated/0/Android/data/com.DefaultCompany.Myproject/files/GripperCameraRecordings"
```

List files inside one session:

```bash
adb shell ls -lah "/storage/emulated/0/Android/data/com.DefaultCompany.Myproject/files/GripperCameraRecordings/20260416_143015"
```

Replace `20260416_143015` with the actual session folder name.

## 7) If Left X Does Not Toggle Recording

Use these checks:

1. Confirm the app is running in the headset, not only in the Unity Editor.

2. Confirm the left controller is awake and tracked.

3. Select `GripperDataCamera` in Unity and check:

```text
GripperCameraRecorder -> Toggle Recording With Left X = true
GripperCameraRecorder -> Debug Left X Input = true
```

4. If you want recording to start automatically, enable:

```text
GripperCameraRecorder -> Record On Play = true
```

This starts recording as soon as the scene starts.

5. Check whether the headset is reporting the X press:

```bash
adb logcat -d Unity:I '*:S' | grep 'Left X toggle detected'
```

If the X button is detected, the log should include:

```text
[GripperCameraRecorder] Left X toggle detected. ...
```

The log also reports which OVRInput mapping fired, for example `RawButton.X/Any=True`.

If logs show this error:

```text
InvalidOperationException: You are trying to read Input using the UnityEngine.Input class
```

then the headset is running an old build of the recorder. Rebuild/reinstall the Unity app; the current recorder avoids legacy `UnityEngine.Input` on Quest and uses `OVRInput` for left-controller `X`.

## 8) If No Files Appear

Check the Unity log message. The recorder prints the exact root folder at scene start:

```text
[GripperCameraRecorder] Recording root: ...
```

If running on Quest, inspect logs:

```bash
adb logcat -d Unity:I '*:S' | grep GripperCameraRecorder
```

If recording started correctly, logs should include:

```text
[GripperCameraRecorder] Started recording to: ...
```

If recording stopped correctly, logs should include:

```text
[GripperCameraRecorder] Stopped recording. Last session: ...
```

## 9) Recording Rate And Resolution

Current default settings on `GripperDataCamera`:

```text
width: 1280
height: 720
captureEveryNFrames: 5
```

At 60 FPS, `captureEveryNFrames = 5` records about 12 images per second.

To record more frames:

```text
captureEveryNFrames = 1
```

To reduce load:

```text
captureEveryNFrames = 10
```

If the headset performance drops, increase `captureEveryNFrames` or reduce resolution.

## 10) Unity Editor Test Path

When testing in Unity Editor on Mac, files are saved here:

```bash
~/Library/Application Support/DefaultCompany/My project/GripperCameraRecordings
```

Open it:

```bash
open "$HOME/Library/Application Support/DefaultCompany/My project/GripperCameraRecordings"
```

## 11) Relevant Unity Files

Scene:

```text
UnityApp/Assets/Scenes/Ur5e_Working 1.unity
```

Recorder script:

```text
UnityApp/Assets/Scripts/GripperCameraRecorder.cs
```

Camera object:

```text
GripperDataCamera
```

Important inspector fields:

- `Record On Play`
- `Toggle Recording With Left X`
- `Capture Every N Frames`
- `Width`
- `Height`
- `Create Floating Panel`
- `Rebuild Floating Panel From Settings`
- `Floating Panel Fixed In Scene`
- `Floating Panel World Position`
- `Floating Panel World Euler`
- `Floating Panel Face Main Camera On Create`
- `Floating Panel Local Position`
- `Preview Size`
- `Enable Floating Panel Controller Drag`
- `Require Teleop Disengaged For Panel Drag`
- `Floating Panel Drag Controller`
- `Floating Panel Drag Button`
- `Floating Panel Drag Button Threshold`
- `Floating Panel Drag Ray Max Distance`
- `Right Grip Teleop Drag Block Threshold`
- `Persist Floating Panel Dragged Pose`

`GripperCameraFloatingPanel` is an editor-created scene object. It is now treated as the central headset control panel, not only a camera preview. After Unity recompiles, it should appear in the hierarchy and can be moved, rotated, scaled, or restyled directly. Manual edits are preserved unless `Rebuild Floating Panel From Settings` is enabled.

When `Floating Panel Fixed In Scene` is enabled, the recording window is placed at the world position/euler fields during creation/rebuild and stays fixed in the room. `Floating Panel Face Main Camera On Create` aims the panel at the main camera once when the panel is created/rebuilt, then leaves it fixed. When fixed mode is disabled, the window uses the older headset-following behavior and `Floating Panel Local Position` is relative to the headset/Main Camera.

## 12) Dragging The Floating Control Panel In The Headset

Default drag settings:

```text
Enable Floating Panel Controller Drag = true
Require Teleop Disengaged For Panel Drag = true
Floating Panel Drag Controller = Left
Floating Panel Drag Button = Trigger
Floating Panel Drag Button Threshold = 0.55
Floating Panel Drag Ray Max Distance = 3
Right Grip Teleop Drag Block Threshold = 0.55
Persist Floating Panel Dragged Pose = true
```

Headset workflow:

1. Release the right grip so robot teleop is disengaged.
2. Point the left controller ray toward `GripperCameraFloatingPanel`.
3. Hold the left trigger.
4. Move the left controller to reposition the panel.
5. Release the left trigger to drop the panel.

When `Persist Floating Panel Dragged Pose` is enabled, the dropped world pose is saved with Unity `PlayerPrefs` on the headset. The next app run loads that saved panel pose.

If you want a different drag control, change:

```text
Floating Panel Drag Controller = Left or Right
Floating Panel Drag Button = Trigger, Grip, or Thumbstick
```

Keep `Require Teleop Disengaged For Panel Drag` enabled for normal use. It prevents the panel from being dragged while right grip is held for robot teleop. `Right Grip Teleop Drag Block Threshold` should normally match `HandPoseSender -> Analog Press Threshold`.
