# ROS Unity MR Dual-Arm Teleoperation

This repository contains a Meta Quest 3 mixed-reality teleoperation platform for simulated robot manipulation. Unity provides the MR passthrough interface, controller input, robot/object visualization, movable workspace UI, central control panel, and camera recording. The Dockerized ROS 2 Humble backend provides Gazebo simulation, MoveIt Servo control, Robotiq Hand-E gripper control, task-object synchronization, reset logic, and haptic/contact feedback.

Gazebo is the physics authority. Unity is the headset-side control, visualization, and data-collection interface.

<img src="docs/assets/demo/quest_mr_workspace_overview_20260614_184136.jpg" alt="Mixed-reality dual-arm teleoperation workspace overview" width="900">

## System Overview

### Main Docs

- [docs/System_Setup.md](docs/System_Setup.md): full replication/setup guide for a new computer or new developer.
- [docs/Getting_Started.md](docs/Getting_Started.md): day-to-day run guide and operational checks.
- [docs/Linux_Windows_setup.md](docs/Linux_Windows_setup.md): Linux and Windows/WSL backend setup notes.
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
│   ├── Linux_Windows_setup.md
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

Local recordings, APKs, generated Unity folders, ROS build folders, raw thesis/evaluation material, and private developer notes are not part of the public repository. Keep those artifacts in local-only folders or external storage.

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

### 3. Install The Prebuilt Quest App

The current prebuilt Quest APK is published as a GitHub Release asset:

- [Unity Quest App 7.0.7](https://github.com/Noah727/Docker_Teleop/releases/tag/unity-app-7.0.7)
- APK asset: [`R.U_7.0.7.apk`](https://github.com/Noah727/Docker_Teleop/releases/download/unity-app-7.0.7/R.U_7.0.7.apk)

Download it with GitHub CLI:

```bash
mkdir -p UnityApp/App_Build
gh release download unity-app-7.0.7 \
  --repo Noah727/Docker_Teleop \
  --pattern 'R.U_7.0.7.apk' \
  --dir UnityApp/App_Build
```

Install it on the headset:

```bash
adb devices
adb install -r -d 'UnityApp/App_Build/R.U_7.0.7.apk'
```

APK files are release artifacts rather than normal Git-tracked files. To rebuild the app instead of using the published APK, see [Build Quest App From Source](docs/Getting_Started.md#build-quest-app-from-source).

### 4. Confirm Unity/Quest Connection Settings

The published APK is configured for the wired development path. If rebuilding from source, keep these settings:

| Unity Setting | Value |
| --- | --- |
| Hand/controller TCP target IP | `127.0.0.1` |
| Hand/controller TCP target port | `5026` |
| ROS Settings IP Address | `127.0.0.1` |
| ROS Settings Port | `10001` |

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
- The legacy in-headset hand-pose/thumbstick mode switch is disabled by default. Optional keyboard/thumbstick modes are terminal-driven right-arm overrides; see [docs/Getting_Started.md](docs/Getting_Started.md).

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

## Demo Media

The hosted demo media documents the current Unity/Quest user-facing workflow. Raw Quest recordings and source video clips are stored outside the repository; the repository keeps only lightweight preview images and links to hosted videos.

<img src="docs/assets/demo/demo_contact_sheet.png" alt="Labeled v2 demo contact sheet" width="900">

[Full demo video](https://youtu.be/xJCyWlMuZTQ)

| Feature | Preview | Source Clip | Demonstrated Capability | Hosted Video |
| --- | --- | --- | --- | --- |
| Project intro / MR workspace | <img src="docs/assets/demo/v2_01_intro.jpg" alt="Project intro and MR workspace overview" width="280"> | `01_intro_Project_intro.mp4` | Quest passthrough with the simulated dual-arm workspace, task objects, and control panel placed in the room. | [Watch](https://youtu.be/5J4qUSEHJ8Q) |
| Central control panel | <img src="docs/assets/demo/v2_02_control_panel.jpg" alt="Central control panel" width="280"> | `02_control_panel_Central_control_panel_buttons.mp4` | Page switching, reset buttons, swap hands, camera controls, task controls, and debug controls. | [Watch](https://youtu.be/Pfm06HfjQYs) |
| Workspace drag/reset/rotation | <img src="docs/assets/demo/v2_03_workspace_drag.jpg" alt="Workspace drag, reset, and rotation" width="280"> | `03_workspace_drag_Workspace_reset_drag_and_rotation.mp4` | Move and rotate the shared workspace while robot and task visuals stay grouped. | [Watch](https://youtu.be/7B3oCiTcR_Q) |
| Floating camera placement | <img src="docs/assets/demo/v2_04_floating_camera.jpg" alt="Floating camera placement and rotation" width="280"> | `04_floating_camera_Floating_camera_placement_and_rotation.mp4` | Place the floating camera and adjust its rotation handles in MR. | [Watch](https://youtu.be/Oz-em5T_6e8) |
| Wrist camera recording | <img src="docs/assets/demo/v2_05_wrist_camera.jpg" alt="Wrist camera recording and capture" width="280"> | `05_wrist_camera_Wrist_camera_recording_and_capture.mp4` | Select wrist/floating camera views and start/stop recording or capture from the panel. | [Watch](https://youtu.be/D6pof3wCspg) |
| Attachment mode | <img src="docs/assets/demo/v2_06_attachment.jpg" alt="Attachment mode and offset calibration" width="280"> | `06_attachment_Attachment_mode_and_offset_calibration.mp4` | Align the controller and gripper using attachment mode and offset calibration. | [Watch](https://youtu.be/YCunphtiU4U) |
| Haptic feedback | <img src="docs/assets/demo/v2_07_haptics.jpg" alt="Haptic feedback modes" width="280"> | `07_haptics_Haptic_feedback_modes.mp4` | Show haptic/contact feedback controls and feedback-mode behavior. | [Watch](https://youtu.be/SlRmVWTV-x4) |
| Task/debug pages | <img src="docs/assets/demo/v2_08_task_page.jpg" alt="Task and debug pages" width="280"> | `08_task_page_Task_and_debug_pages.mp4` | Show task selection, status, and debug controls on the central panel. | [Watch](https://youtu.be/uySzKrMUrYk) |
| Pick/place task | <img src="docs/assets/demo/v2_09_pick_place.jpg" alt="Pick and place task" width="280"> | `09_pick_place_Pick_and_place_task.mp4` | Pick and place task objects on target plates. | [Watch](https://youtu.be/3RRWzHUXET4) |
| Dual-arm handoff | <img src="docs/assets/demo/v2_10_dual_handoff.jpg" alt="Dual-arm handoff" width="280"> | `10_dual_handoff_Dual_arm_handoff.mp4` | Use both arms together for a coordinated handoff. | [Watch](https://youtu.be/W380J-VKKL8) |
| Cable task layout | <img src="docs/assets/demo/v2_11_cable_intro.jpg" alt="Cable insertion task layout" width="280"> | `11_cable_intro_Cable_insertion_task_intro.mp4` | Introduce the cable rods, receiver boxes, and multiple port clearances. | [Watch](https://youtu.be/hWEKjaqdx6k) |
| Loose port insertion | <img src="docs/assets/demo/v2_13_loose_ports.jpg" alt="Loose port insertion" width="280"> | `13_loose_ports_Loose_port_insertions.mp4` | Demonstrate easier cable insertion ports. | [Watch](https://youtu.be/Nkdtu-we2gI) |
| Tighter port insertion | <img src="docs/assets/demo/v2_14_tighter_ports.jpg" alt="Tighter port insertion" width="280"> | `14_tighter_ports_Tighter_port_insertion_behavior.mp4` | Show insertion behavior as port tolerance becomes tighter. | [Watch](https://youtu.be/m0dovinv2Hs) |
| Fixed receiver box | <img src="docs/assets/demo/v2_15_fixed_box.jpg" alt="Fixed receiver box insertion" width="280"> | `15_fixed_box_Fixed_insertion_box.mp4` | Demonstrate the fixed receiver-box insertion setup. | [Watch](https://youtu.be/mO-z67oC6no) |
| Tightest ports / conclusion | <img src="docs/assets/demo/v2_16_tightest_ports.jpg" alt="Tightest ports and conclusion" width="280"> | `16_tightest_ports_Tightest_ports_and_conclusion.mp4` | Show the most difficult port condition and closing demo frame. | [Watch](https://youtu.be/RCicppveXlI) |
| Control panel drag/resize | <img src="docs/assets/demo/v2_17_control_panel_drag_resize.jpg" alt="Control panel drag and resize" width="280"> | `17_control_panel_drag_resize_Control_panel_drag_and_resize.mp4` | Drag the central panel and resize it with the corner handles. | [Watch](https://youtu.be/g5qecVFjjOM) |

## Evaluation Summary

Evaluation focused on whether the MR interface can support repeatable simulated manipulation, dual-arm coordination, Gazebo-to-Unity synchronization, camera recording, and backend runtime operation. Raw videos, trace logs, and full CSV/plot outputs are kept outside the public repository with the thesis/evaluation archive; this README includes the headline results and caveats.

### Manipulation Trials

| Evaluation Task | Result | Notes |
| --- | --- | --- |
| Single-arm pick/place with MR hand-pose control | 46 successful placements across two 3 min trials, 7.67 successes/min average | Operator-counted Quest trials. |
| Single-arm comparison controls | Keyboard: 2.67 successes/min; SpaceMouse: 4.67 successes/min; gamepad: 3.33 successes/min | Same 3 min pick/place task; baseline controls are single-arm only. |
| Dual-arm simultaneous pick/place | 26 successful placements in 3 min, 8.67 successes/min | Both arms operated together in the shared MR workspace. |
| Dual-arm airborne handoff | 9 successful handoffs in 3 min, 3.00 successes/min | One arm passes an object to the other without placing it down first. |
| Fixed-box cable insertion | 15, 13, 9, 4, and 2 insertions/min at 2.0, 1.5, 1.0, 0.5, and 0.2 mm/side clearance | Receiver box remains fixed in the workspace. |
| Airborne-box cable insertion | 11, 12, 11, 2, and 2 insertions/min at 2.0, 1.5, 1.0, 0.5, and 0.2 mm/side clearance | One arm holds the receiver box while the other inserts the cable. |

### System Measurements

| Measurement | Headline Result | Caveat |
| --- | --- | --- |
| Headset-to-backend command path | Receiver-to-joint-command mean delay ranged from 1.32-7.25 ms across traced arm/run rows; Servo-to-joint-command mean delay was about 13.0 ms | Measured inside ROS/container timing after receiver publication, not optical controller-to-display latency. |
| Gazebo-to-Unity object sync | RedCube static alignment error was effectively zero; moving-object mean position error was 0.11 mm, with transient outliers during grasp/object motion | Object sync rows are usable; old end-effector absolute alignment rows are excluded from the headline result. |
| Quest recording performance | Recording trace averaged 40.3 FPS with a 42.0 FPS median; preferred object visual-latency sequence had 54.5 ms median latency | FPS and object-sync evidence are usable; raw end-effector event detection remains debug-only unless manually filtered. |
| Backend runtime performance | The current macOS dynamic backend case study measured 0.63 mean real-time factor headless and 0.52 with headed Gazebo/noVNC | Runtime numbers are hardware and Docker-stack dependent, so cross-platform values should be treated as case studies rather than strict OS benchmarks. |

Evaluation helper scripts live under `ros_backend1.1/scripts/test_tools/eval_scripts/`. The repository intentionally excludes raw recordings, large trace outputs, and generated thesis material.

## Operational Boundaries

This repository is a simulation-first research platform rather than a validated physical robot deployment.

| Area | Current Boundary |
| --- | --- |
| Robot platform | The maintained configuration targets dual UR5e arms with Robotiq Hand-E grippers. Supporting another robot requires coordinated changes to ROS descriptions, MoveIt configuration, controller topics, Unity visualization, and mapping parameters. |
| Physics authority | Gazebo is the authoritative source for robot and object physics. Unity objects are visual/control representations and should not run a competing physics simulation for synchronized task objects. |
| Host performance | Gazebo and MoveIt Servo are CPU-sensitive in the current Docker workflow. Headless Gazebo is the default because macOS Docker rendering can reduce real-time factor compared with a native Linux workstation. |
| Quest connection | Wired Quest mode through USB and `adb reverse` is the recommended operational path. It depends on ADB authorization and active reverse tunnels for both controller TCP and ROS-TCP traffic. |
| Task switching | Profile-based task generation is supported. Live runtime task switching exists as an experimental path and should be validated per task before use in formal studies. |
| Haptics | Haptic feedback is intended to be contact/proximity-driven. End-effector error signals are useful for diagnostics but can be misleading as user-facing feedback during fast or constrained motion. |
| License | License pending. Reuse, redistribution, or derivative use of this project requires permission from the author/lab until a project license is finalized. |
