# ROS Unity MR Dual-Arm Teleoperation

This repository contains a Meta Quest 3 mixed-reality teleoperation platform for simulated robot manipulation. Unity provides the MR passthrough interface, controller input, robot/object visualization, movable workspace UI, central control panel, and camera recording. The Dockerized ROS 2 Humble backend provides Gazebo simulation, MoveIt Servo control, Robotiq Hand-E gripper control, task-object synchronization, reset logic, and haptic/contact feedback.

Gazebo is the physics authority. Unity is the headset-side control, visualization, and data-collection interface.

## System Overview

### Main Docs

- [docs/System_Setup.md](docs/System_Setup.md): full replication/setup guide for a new computer or new developer.
- [docs/Getting_Started.md](docs/Getting_Started.md): day-to-day run guide and operational checks.
- [docs/Technical_Details.md](docs/Technical_Details.md): architecture, networking, mapping, recording, troubleshooting, and robot/task adaptation details.
- [ros_backend1.1/Readme/DualArm_Getting_Started.md](ros_backend1.1/Readme/DualArm_Getting_Started.md): current dual-arm backend command sheet.
- [ros_backend1.1/Readme/Task_Profile_Workflow.md](ros_backend1.1/Readme/Task_Profile_Workflow.md): task/workspace/profile workflow.

### System Diagram

```mermaid
flowchart LR
    Quest[Quest 3 Unity MR App]
    Receiver[Quest TCP Receiver]
    Mapper[Dual-Arm Teleop Mapping]
    Servo[MoveIt Servo]
    Gazebo[Gazebo Dual UR5e + Hand-E]
    Sync[Robot/Object Pose Sync]
    RosTcp[ROS-TCP Endpoint]
    Haptics[Contact/Haptic Feedback]
    Recorder[Unity Wrist/Floating Cameras]

    Quest -->|controller pose/buttons TCP| Receiver
    Receiver --> Mapper
    Mapper -->|twist + gripper commands| Servo
    Servo --> Gazebo
    Gazebo --> Sync
    Sync --> RosTcp
    RosTcp --> Quest
    Gazebo --> Haptics
    Haptics --> RosTcp
    Quest --> Recorder
```

### Current Status

| Item | Current Project Default |
| --- | --- |
| Backend | `ros_backend1.1` |
| Docker container | `motion_planner_11` |
| Lifecycle script | `ros_backend1.1/scripts/backend11_lifecycle.sh` |
| Unity active scene | `UnityApp/Assets/Scenes/GazeboReplica_DualArm_MR.unity` |
| Single-arm backup scene | `UnityApp/Assets/Scenes/GazeboReplica_MR.unity` |
| Robot setup | Dual UR5e arms with Robotiq Hand-E grippers |
| Preferred connection | Wired Quest USB using `adb reverse` |
| Quest controller TCP | Quest `127.0.0.1:5026` -> host `127.0.0.1:5026` -> container `5005` |
| Unity ROS-TCP | Quest `127.0.0.1:10001` -> host `127.0.0.1:10001` -> container `10000` |

### Tested Environment

| Component | Tested Version / Setup |
| --- | --- |
| Unity | `6000.2.10f1` |
| Headset | Meta Quest 3 |
| Unity target | Android / Quest |
| Unity package ID | `com.noahli.ROSUNITY` |
| Backend | Dockerized ROS 2 workspace |
| ROS | Humble inside container |
| Simulation | Gazebo |
| Motion control | MoveIt Servo |
| Robot | Dual UR5e + Robotiq Hand-E |
| Host development | macOS with Docker Desktop |
| Preferred Quest link | USB wired mode via `adb reverse` |

### Repository Layout

```text
.
├── README.md
├── docs/
│   ├── System_Setup.md
│   ├── Getting_Started.md
│   └── Technical_Details.md
├── UnityApp/
│   ├── Assets/
│   ├── Packages/
│   └── ProjectSettings/
└── ros_backend1.1/
    ├── Dockerfile
    ├── docker-compose.yaml
    ├── .env.example
    ├── profiles/
    ├── scripts/
    ├── simulation/
    └── src/
```

Local recordings, APKs, generated Unity folders, ROS build folders, and private developer notes are intentionally ignored by Git.

## Quick Start

### 1. Clone And Restore Assets

```bash
git lfs install
git clone git@github.com:su-idr-lab/ros_unity_project.git
cd ros_unity_project
git lfs pull
```

### 2. Start The Dual-Arm Backend

```bash
cd ros_backend1.1
cp .env.example .env
./scripts/backend11_lifecycle.sh mode_wired
./scripts/backend11_lifecycle.sh bringup_dual
./scripts/backend11_lifecycle.sh status
```

`bringup_dual` starts the container, applies wired ADB reverse tunnels if the Quest is connected, generates the current dual-arm Gazebo world from profiles, starts Gazebo, starts MoveIt Servo, starts the Quest TCP receiver, starts the dual mapping/control pipeline, and starts Unity synchronization.

If Gazebo was closed but the container is still running:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh start_dual_sim
sleep 2
./scripts/backend11_lifecycle.sh start_dual_servo
./scripts/backend11_lifecycle.sh start_dual_part23
./scripts/backend11_lifecycle.sh start_dual_part4
./scripts/backend11_lifecycle.sh status
```

For a clean restart:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh safe_down
./scripts/backend11_lifecycle.sh bringup_dual
```

### 3. Install Or Run The Quest App

If a prebuilt APK exists locally, install it directly on the headset:

```bash
adb devices
adb install -r -d 'UnityApp/App_Build/R&U_1.0.5.apk'
```

If the APK name is different:

```bash
ls UnityApp/App_Build/*.apk
adb install -r -d '<path-to-apk>'
```

APKs are ignored by Git, so a fresh clone may need a Unity build first.

### 4. Open The Unity Project If Rebuilding

```text
Unity Hub -> Add project from disk -> UnityApp
Unity version: 6000.2.10f1
Active scene: Assets/Scenes/GazeboReplica_DualArm_MR.unity
```

In wired mode, the Unity app should use:

| Unity Setting | Value |
| --- | --- |
| Hand/controller TCP target IP | `127.0.0.1` |
| Hand/controller TCP target port | `5026` |
| ROS Settings IP Address | `127.0.0.1` |
| ROS Settings Port | `10001` |

Build and run the Android/Quest profile from Unity only if you are rebuilding instead of installing a prebuilt APK.

## Control Summary

The current app is built around independent dual-arm teleoperation. Each physical controller drives one robot arm by default.

### Right Controller / Right Arm

- `Grip hold`: engage right-arm teleop.
- `Trigger tap`: toggle right gripper open/close.
- `A hold`: right-arm rotation mode.
- `B tap`: toggle right-arm attachment mode.

### Left Controller / Left Arm

- `Grip hold`: engage left-arm teleop.
- `Trigger tap`: toggle left gripper open/close.
- `X hold`: left-arm rotation mode.
- `Y tap`: toggle left-arm attachment mode.

### Shared Controls And Panel Actions

- Central control panel buttons handle reset, status, camera view selection, recording, haptic settings, and task controls.
- `Swap Hands` on the control panel swaps which physical controller drives each robot arm.
- Workspace drag/rotation is available when teleop grip is not engaged.
- Attachment mode attempts to make the robot end effector follow the controller pose more directly in the MR workspace frame.
- The coupled Hand-E gripper controller is the default backend gripper mode for dual-arm bringup.
- The old in-headset hand-pose/thumbstick mode switch is disabled by default. Optional keyboard/thumbstick modes are terminal-driven right-arm overrides; see [docs/Getting_Started.md](docs/Getting_Started.md).

### Recording

Recording is controlled from the Unity side.

- Left controller `X` can start/stop wrist-camera recording when enabled in the Unity recorder script.
- The central control panel can start/stop recording and switch preview source.
- Current preview sources are left wrist camera, right wrist camera, and floating camera.
- The floating camera is for movable scene preview; wrist cameras are the main data-recording path.

Recordings on Quest are saved under:

```text
/storage/emulated/0/Android/data/com.noahli.ROSUNITY/files/GripperCameraRecordings
```

Pull recordings to the host with:

```bash
adb pull "/storage/emulated/0/Android/data/com.noahli.ROSUNITY/files/GripperCameraRecordings" ./GripperCameraRecordings
```

Do not commit recordings, APKs, or demo videos to Git. Upload final clips to GitHub Releases, YouTube, Google Drive, or lab storage, then link the hosted URLs in the demo section.

## Demo Videos

This section should show Unity app features, not backend bringup. Backend startup is already covered in Quick Start.

Recommended format:

- Keep each feature clip around 5-8 seconds.
- Use muted MP4/H.264 for hosted clips.
- Use GIF only as a tiny fallback preview if needed.
- Keep raw Quest recordings outside Git.
- Replace local draft links with hosted URLs before publishing the repo broadly.

| Feature | Suggested Clip | What To Show | Link |
| --- | --- | --- | --- |
| MR passthrough workspace | `demo_01_mr_workspace.mp4` | Quest passthrough with the simulated workspace, robots, and task objects placed in the room. | TODO |
| Workspace drag/rotate | `demo_02_workspace_drag.mp4` | Use the controller ray to move/rotate the whole workspace while robots and task objects stay together. | TODO |
| Central control panel | `demo_03_control_panel.mp4` | Page switching, reset buttons, status display, camera page, and draggable panel behavior. | TODO |
| Dual-arm teleoperation | `demo_04_dual_arm_teleop.mp4` | Left and right controllers independently drive the two robot arms. | TODO |
| Coupled gripper grasp | `demo_05_coupled_gripper.mp4` | Stable two-finger Hand-E closing/opening and object grasp behavior. | TODO |
| Pick/place task | `demo_06_pick_place.mp4` | Pick up a cube/cylinder and place it on a target plate. | TODO |
| Cable insertion task objects | `demo_07_cable_insertion.mp4` | Show the cable/port task objects in the shared workspace. | TODO |
| Attachment mode | `demo_08_attachment_mode.mp4` | End-effector alignment with the controller and attachment offset behavior. | TODO |
| Haptic/contact feedback | `demo_09_haptics.mp4` | Contact or gripper pinch feedback behavior, if visible through the control panel/log cue. | TODO |
| Camera preview/recording | `demo_10_recording.mp4` | Select wrist/floating camera preview and start/stop recording from the control panel. | TODO |
| Full walkthrough | `full_demo.mp4` | Optional longer run showing the complete system in use. | TODO |

## Current Limitations

- The current setup is tuned for dual UR5e + Robotiq Hand-E in simulation; switching robot arms still requires coordinated backend profiles, MoveIt configuration, controller topics, Unity visualization, and mapping parameters.
- Gazebo is the physics authority; Unity should not be used as a competing physics simulation for synchronized objects.
- macOS Docker/Gazebo rendering can be CPU-heavy and may not use GPU acceleration like a native Linux workstation.
- Wired Quest mode is the recommended path, but it requires USB connection, ADB authorization, and working `adb reverse` tunnels.
- Runtime task switching is still evolving. The profile system exists, but polished end-user task switching should eventually be handled by a robust ROS task manager and Unity task-selection UI.
- Haptic feedback should stay contact-driven. EE-error vibration can be useful for debugging but can be misleading during normal fast motion.
- Demo clips, recordings, datasets, APKs, Unity generated folders, and ROS build artifacts are intentionally not stored in Git.
- License is not finalized yet. MIT is a good candidate if the lab/advisor wants broad reuse, but this should be confirmed before adding a `LICENSE` file because license choice affects legal reuse rights.
