# Unity Quest Debug Stream Setup

This note describes the recommended debugging stream for the Unity Quest 3 app. The goal is to make headset runtime behavior observable from the host computer instead of relying only on what the user sees inside the headset.

## Goal

When the Quest app is running, the host developer should be able to inspect:

- Unity runtime logs.
- Controller button/axis values.
- Teleop engagement state.
- Current control mode.
- Last packet sent to ROS.
- ROS connection state.
- Wrist-camera recording state.
- Runtime errors/exceptions.

The basic debug stream should work over USB with `adb`.

## Basic ADB Log Stream

Connect the Quest by USB and confirm it is visible:

```bash
adb devices
```

Clear old logs before a test:

```bash
adb logcat -c
```

Stream Unity logs live:

```bash
adb logcat Unity:I '*:S'
```

Capture logs after reproducing a bug:

```bash
adb logcat -d Unity:I '*:S' > quest_unity_log.txt
```

Useful filtered logs:

```bash
adb logcat -d Unity:I '*:S' | rg "HandPoseSender|GripperCameraRecorder|Teleop|ROS|Error|Exception"
```

## Recommended Unity Log Tags

Use consistent prefixes in `Debug.Log()` messages so logs are searchable:

```text
[HandPoseSender]
[TeleopInput]
[TeleopMode]
[GripperControl]
[GripperCameraRecorder]
[SceneObjectSync]
[ROSConnection]
[RuntimeDebugPanel]
```

Example log style:

```csharp
Debug.Log($"[TeleopInput] rightGrip={rightGrip:F2} trigger={trigger:F2} engaged={teleopEngaged}");
Debug.Log($"[HandPoseSender] sent pose packet to {targetIP}:{targetPort}");
Debug.Log($"[TeleopMode] mode changed: {oldMode} -> {newMode}");
```

Avoid vague logs like:

```text
button pressed
moving
error
```

## Runtime Debug Panel Plan

The floating control panel should eventually include a debug tab with these fields:

```text
Connection
- ROS IP
- ROS port
- TCP sender IP
- TCP sender port
- Last packet sent time
- Last packet age

Input
- Right grip value
- Right trigger value
- Right A/B state
- Right thumbstick x/y
- Right thumbstick press state
- Left X/Y state
- Left trigger/grip value
- Left thumbstick x/y

Teleop
- Teleop engaged true/false
- Current control mode
- Hand reference pose
- Current hand pose
- Position delta
- Rotation delta
- Commanded linear velocity
- Commanded angular velocity

Robot/Sync
- Last joint state receive time
- Last object sync receive time
- Active synchronized object count

Recording
- Wrist camera recording true/false
- Recording folder
- Captured frame count
- RenderTexture size
```

This lets us tell whether a bug is in Quest input, Unity logic, TCP transport, ROS receive, Gazebo execution, or Unity visualization.

## Debug Snapshot Export

Add a future button called `Save Debug Snapshot` that writes a JSON file under Unity `Application.persistentDataPath`.

Suggested snapshot fields:

```json
{
  "timestamp": "2026-05-19T12:00:00Z",
  "scene": "Ur5e_Working 1",
  "appVersion": "dev",
  "packageId": "com.noahli.handtrackingunity",
  "controlMode": "hand_pose",
  "teleopEngaged": true,
  "rosIp": "127.0.0.1",
  "rosPort": 10001,
  "tcpTargetIp": "127.0.0.1",
  "tcpTargetPort": 5026,
  "lastPacketAgeSec": 0.04,
  "rightGrip": 0.91,
  "rightTrigger": 0.02,
  "leftX": false,
  "leftY": true,
  "recording": false
}
```

Pull snapshots from Quest with:

```bash
adb shell run-as com.noahli.handtrackingunity ls files
adb pull /storage/emulated/0/Android/data/com.noahli.handtrackingunity/files/debug_snapshot.json .
```

## Screen Recording For Visual Bugs

For visual-only bugs, record the headset view:

```bash
adb shell screenrecord /sdcard/quest_debug.mp4
```

Stop recording with `Ctrl-C`, then pull the video:

```bash
adb pull /sdcard/quest_debug.mp4 .
```

Do not commit the video to Git. Upload it to external storage and link it from an issue or dev note.

## Input Trace Recording Plan

For difficult controller bugs, add an input trace recorder that writes per-frame controller state:

```text
time
right controller pose
left controller pose
buttons
triggers
grips
thumbsticks
teleop mode
hand reference pose
mapped target pose
```

Later, add an Editor replay mode so a Quest test can be reproduced in Unity Editor without wearing the headset.

Suggested flow:

```text
Quest test -> record input trace -> adb pull trace -> replay in Unity Editor -> debug normal C# code
```

## Bug Report Bundle

For a good Quest bug report, collect:

```text
1. Git branch and commit hash
2. Steps to reproduce
3. Expected behavior
4. Actual behavior
5. adb Unity log
6. Optional headset video
7. Optional debug snapshot JSON
8. Backend status output
```

Commands:

```bash
git log --oneline -1
adb logcat -d Unity:I '*:S' > quest_unity_log.txt
cd ros_backend1.0 && ./scripts/backend10_lifecycle.sh status
```

## Implementation Priority

Recommended order:

1. Add structured Unity log tags.
2. Add runtime debug panel fields.
3. Add debug snapshot export.
4. Add input trace recording.
5. Add input replay in Unity Editor.
