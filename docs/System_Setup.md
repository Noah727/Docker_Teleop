# System Setup

This document defines the supported setup path for the current mixed-reality dual-arm teleoperation system. It is intended for a new developer or lab machine that must reproduce the Unity/Quest frontend and Dockerized ROS/Gazebo backend from a fresh clone.

For daily operation after setup, use [Getting_Started.md](Getting_Started.md). For implementation details, use [Technical_Details.md](Technical_Details.md).

## Project Scope

The platform connects a Meta Quest 3 Unity application to a ROS 2 Humble backend. Unity provides mixed-reality passthrough presentation, controller input, robot and task-object visualization, workspace manipulation, central panel controls, camera preview, and recording. ROS 2, MoveIt Servo, and Gazebo provide robot control, task-object physics, gripper control, reset behavior, haptic/contact events, and state synchronization.

Gazebo is the physics authority. Unity is the headset-side interface and visualizer.

## Current Canonical Configuration

| Item | Value |
| --- | --- |
| Backend folder | `ros_backend1.1` |
| Docker container | `motion_planner_11` |
| Lifecycle script | `ros_backend1.1/scripts/backend11_lifecycle.sh` |
| Unity active scene | `UnityApp/Assets/Scenes/GazeboReplica_DualArm_MR.unity` |
| Single-arm backup scene | `UnityApp/Assets/Scenes/GazeboReplica_MR.unity` |
| Generated Gazebo world | `ros_backend1.1/simulation/worlds/ur_hande_dual_arm_tabletop.sdf` |
| Generated Unity task profile | `UnityApp/Assets/Resources/TaskProfiles/active_task.json` |
| Preferred connection | Wired Quest USB with `adb reverse` |
| Quest controller TCP | Quest `127.0.0.1:5026` -> host `127.0.0.1:5026` -> container `5005` |
| Unity ROS-TCP | Quest `127.0.0.1:10001` -> host `127.0.0.1:10001` -> container `10000` |

## Demonstration Material

The repository tracks lightweight preview images and hosted video links in the root `README.md`. Raw Quest recordings, source `.mp4` clips, APK builds, and full evaluation archives are kept outside the public repository.

The public demo set covers:

- Mixed-reality workspace overview.
- Central control panel.
- Workspace drag, rotation, and reset.
- Floating and wrist camera workflows.
- Attachment mode.
- Haptic feedback controls.
- Task/debug pages.
- Pick/place, dual-arm handoff, and cable insertion tasks.

## Tested Environment

| Component | Tested Version / Setup |
| --- | --- |
| Unity | `6000.2.10f1` |
| Headset | Meta Quest 3 |
| Unity target platform | Android / Quest |
| Unity package ID | `com.noahli.ROSUNITY` |
| Backend | Dockerized ROS 2 workspace |
| ROS | Humble inside container |
| Simulation | Gazebo |
| Motion control | MoveIt Servo |
| Robot | Dual UR5e arms with Robotiq Hand-E grippers |
| Primary host development | macOS with Docker Desktop |
| Additional backend target | Native Ubuntu / Windows through WSL2 Ubuntu |
| Preferred Quest connection | USB wired mode via `adb reverse` |

## Repository Layout

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

The public repository intentionally excludes generated outputs and large local artifacts. Do not commit Unity `Library/`, Unity `Temp/`, APKs, Quest recordings, source demo videos, ROS build/install/log folders, local `.env` files, raw thesis/evaluation archives, or private development notes.

## Hardware Requirements

Required:

- Meta Quest 3 with Developer Mode enabled.
- USB-C data cable for wired development.
- Host computer capable of running Docker.
- Unity Editor with Android/Quest build support.
- Internet access for first clone, Git LFS asset restoration, package restore, and container builds.

Optional:

- Native Linux workstation for higher Gazebo real-time factor.
- External monitor for noVNC or Gazebo visual debugging.
- Lab storage, YouTube, GitHub Releases, or Google Drive for hosted demo media.

## Software Requirements

- Git.
- Git LFS.
- Unity Hub.
- Unity `6000.2.10f1`.
- Unity Android Build Support, Android SDK/NDK Tools, and OpenJDK.
- Docker Desktop on macOS/Windows, or Docker Engine plus Docker Compose on Linux.
- `adb` for Quest installation and wired reverse tunnels.

## Clone And Restore Assets

```bash
git lfs install
git clone git@github.com:su-idr-lab/ros_unity_project.git
cd ros_unity_project
git lfs pull
```

If Git LFS is skipped, Unity may import mesh or texture pointer files instead of the real assets.

## Backend Setup

Create the backend environment file and select wired mode:

```bash
cd ros_backend1.1
cp .env.example .env
./scripts/backend11_lifecycle.sh mode_wired
```

Build and start the backend:

```bash
./scripts/backend11_lifecycle.sh safe_down
./scripts/backend11_lifecycle.sh up_container_build
./scripts/backend11_lifecycle.sh build_ws
./scripts/backend11_lifecycle.sh bringup_dual
./scripts/backend11_lifecycle.sh status
```

`bringup_dual` starts the container, applies wired ADB reverse tunnels when the Quest is connected, generates the dual-arm Gazebo world from profiles, starts Gazebo, starts MoveIt Servo, starts the Quest TCP receiver, starts both arm mapping/control pipelines, and starts Unity synchronization services.

Expected runtime container:

```text
motion_planner_11
```

Expected wired port mappings:

```text
5005/tcp  -> 127.0.0.1:5026
10000/tcp -> 127.0.0.1:10001
```

## Quest Wired Mode

Connect the headset by USB and accept the USB debugging prompt. Then verify ADB:

```bash
adb devices
```

Expected state:

```text
<device_id>    device
```

Enable and inspect reverse tunnels:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh wired_on
./scripts/backend11_lifecycle.sh wired_status
adb reverse --list
```

Expected tunnel entries:

```text
tcp:5026 tcp:5026
tcp:10001 tcp:10001
```

## Unity Project Setup

Open the Unity project:

```text
Unity Hub -> Add project from disk -> UnityApp
Unity version: 6000.2.10f1
Active scene: Assets/Scenes/GazeboReplica_DualArm_MR.unity
```

The current package ID is:

```text
com.noahli.ROSUNITY
```

In wired mode, the deployed Quest app should use:

| Unity Setting | Value |
| --- | --- |
| Hand/controller TCP target IP | `127.0.0.1` |
| Hand/controller TCP target port | `5026` |
| ROS Settings IP Address | `127.0.0.1` |
| ROS Settings Port | `10001` |

The current prebuilt Quest APK is published as a GitHub Release asset:

- Release: [Unity Quest App 7.0.7](https://github.com/su-idr-lab/ros_unity_project/releases/tag/unity-app-7.0.7)
- APK asset: [`R.U_7.0.7.apk`](https://github.com/su-idr-lab/ros_unity_project/releases/download/unity-app-7.0.7/R.U_7.0.7.apk)

Download and install it:

```bash
mkdir -p UnityApp/App_Build
gh release download unity-app-7.0.7 \
  --repo su-idr-lab/ros_unity_project \
  --pattern 'R.U_7.0.7.apk' \
  --dir UnityApp/App_Build
adb install -r -d 'UnityApp/App_Build/R.U_7.0.7.apk'
```

Build through Unity only when a new APK is required. Source-build instructions are maintained in [Getting_Started.md](Getting_Started.md#build-quest-app-from-source).

## Expected Working Behavior

When setup is complete:

- The `motion_planner_11` container is running.
- Gazebo starts the dual UR5e + Hand-E tabletop world.
- MoveIt Servo controllers are active for both arms.
- The Quest app connects over `127.0.0.1` through ADB reverse tunnels.
- Holding either controller grip engages the corresponding robot arm.
- Trigger taps toggle the corresponding gripper open/closed.
- Unity robot visuals follow ROS/Gazebo joint state.
- Synchronized Unity task objects follow Gazebo object poses.
- The central control panel provides reset, camera, recording, haptics, and task controls.
- Wrist and floating camera preview/recording paths are available from the Unity side.

## Main Controls

Right controller:

- `Grip hold`: engage right-arm teleop.
- `Trigger tap`: toggle right gripper open/close.
- `A hold`: right-arm rotation mode.
- `B tap`: toggle right-arm attachment mode.

Left controller:

- `Grip hold`: engage left-arm teleop.
- `Trigger tap`: toggle left gripper open/close.
- `X hold`: left-arm rotation mode.
- `Y tap`: toggle left-arm attachment mode.

Central control panel:

- Reset left arm, right arm, both arms, objects, workspace view, or full state.
- Swap controller-to-arm assignment.
- Select camera preview source.
- Start and stop recording.
- Inspect task and debug status.
- Configure haptic output.

## Recording And Data Collection

Quest recordings are saved under:

```text
/storage/emulated/0/Android/data/com.noahli.ROSUNITY/files/GripperCameraRecordings
```

Pull recordings to the host:

```bash
adb pull "/storage/emulated/0/Android/data/com.noahli.ROSUNITY/files/GripperCameraRecordings" ./GripperCameraRecordings
```

Recordings and datasets are local artifacts. Store final public clips in external hosting and link them from the README.

## Troubleshooting Checks

| Symptom | Primary Check |
| --- | --- |
| Missing Unity assets | Run `git lfs pull` from the repository root. |
| Quest not visible | Run `adb devices`; accept the headset USB debugging prompt. |
| Wired mode not connecting | Run `./scripts/backend11_lifecycle.sh wired_on` and inspect `adb reverse --list`. |
| Backend not responding | Run `./scripts/backend11_lifecycle.sh status`. |
| Quest input not reaching ROS | Inspect `/tmp/qcr.log` inside `motion_planner_11`. |
| Gazebo moves but Unity does not sync | Inspect `/unity_sync/*` topics and `/tmp/dual_part4_tcp_endpoint.log`. |
| Recording folder is empty | Confirm the installed app package ID is `com.noahli.ROSUNITY` and inspect Unity logs through `adb logcat`. |

Useful status commands:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh status
docker exec motion_planner_11 bash -lc 'tail -n 80 /tmp/qcr.log'
docker exec motion_planner_11 bash -lc 'tail -n 80 /tmp/servo_dual_gz.log'
docker exec motion_planner_11 bash -lc 'tail -n 80 /tmp/dual_part4_tcp_endpoint.log'
```

## Development Workflow

Keep `main` stable. Use feature branches for changes that affect Unity scenes, backend launch behavior, robot descriptions, profiles, or documentation:

```bash
git checkout -b feature/my-change
```

Before merging, verify:

- Unity project opens without script errors.
- Quest build installs or the affected Unity path is manually tested.
- `ros_backend1.1/scripts/backend11_lifecycle.sh status` reports the expected runtime state.
- Generated or local-only artifacts are not staged.

## License And Credits

Add a root `LICENSE` before distributing the project for reuse outside the lab. Third-party components retain their upstream notices in source folders where available, including ROS-TCP Endpoint, Universal Robots assets, Robotiq Hand-E assets, Unity packages, ROS 2, MoveIt, and Gazebo-related components.
