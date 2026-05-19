# UR5e Hand-E VR Teleoperation

This repository contains a Unity + Meta Quest interface for teleoperating a simulated UR5e robot with a Robotiq Hand-E gripper through ROS 2, MoveIt Servo, and Gazebo. Unity is used for headset interaction, scene visualization, controller input, synchronized object visualization, and wrist-camera recording. ROS/Gazebo is the source of robot and object physics.

## Current Status

The current canonical backend is `ros_backend1.0`.

Older local experiment folders such as `ros_backend0.9`, `Archive`, and `Ros_archive` are intentionally ignored by `.gitignore` so the GitHub repo stays clean.

## Repository Layout

```text
.
├── README.md
├── docs/
│   ├── ARCHITECTURE.md
│   ├── CONTROLLER_BUTTONS.md
│   ├── GRIPPER_CAMERA.md
│   ├── MAPPING_TUNING.md
│   ├── RECORDING.md
│   ├── SYSTEM_BRINGUP.md
│   ├── TROUBLESHOOTING.md
│   ├── WIRED_WIRELESS_SETUP.md
│   └── WORLD_FRAME_MAPPING.md
├── UnityApp/
│   ├── Assets/
│   ├── Packages/
│   └── ProjectSettings/
└── ros_backend1.0/
    ├── Dockerfile
    ├── docker-compose.yaml
    ├── .env.example
    ├── scripts/
    ├── simulation/
    └── src/
```

## Main Components

- `UnityApp/`: Unity project for Quest/headset input, robot visualization, synchronized scene objects, floating control panel, and wrist-camera recording.
- `ros_backend1.0/`: Dockerized ROS 2 backend with UR5e + Hand-E descriptions, Gazebo simulation setup, MoveIt Servo configuration, TCP receiver, mapper, reset manager, and object pose sync.
- `docs/`: User-facing setup, tuning, operation, and troubleshooting notes.

## Requirements

- Unity `6000.2.10f1`.
- Docker Desktop on macOS, or Docker Engine / Docker Compose on Linux.
- Meta Quest headset and Meta XR / OpenXR Unity setup.
- `adb` for wired Quest mode.
- Git LFS for large Unity and mesh assets.

## Quick Start

From the repository root:

```bash
cd ros_backend1.0
cp .env.example .env
./scripts/backend10_lifecycle.sh up_container
./scripts/backend10_lifecycle.sh build_ws
./scripts/backend10_lifecycle.sh bringup_wired
```

For detailed bringup and checkpoints, see [docs/SYSTEM_BRINGUP.md](docs/SYSTEM_BRINGUP.md).

## Unity App

Open `UnityApp/` in Unity `6000.2.10f1`.

The active scene is:

```text
UnityApp/Assets/Scenes/Ur5e_Working 1.unity
```

For controller bindings, see [docs/CONTROLLER_BUTTONS.md](docs/CONTROLLER_BUTTONS.md).

## Networking

Preferred local development mode is wired Quest TCP over USB with `adb reverse`.

See [docs/WIRED_WIRELESS_SETUP.md](docs/WIRED_WIRELESS_SETUP.md).

## Recording

The Unity wrist camera can preview in the floating control panel and record frames from the headset session.

See [docs/RECORDING.md](docs/RECORDING.md) and [docs/GRIPPER_CAMERA.md](docs/GRIPPER_CAMERA.md).

## Tuning

Mapping and controller tuning is documented in:

- [docs/MAPPING_TUNING.md](docs/MAPPING_TUNING.md)
- [docs/WORLD_FRAME_MAPPING.md](docs/WORLD_FRAME_MAPPING.md)

## Troubleshooting

Common issues are collected in [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md).

## GitHub Setup

Repository setup and first-push instructions are in [docs/GITHUB_SETUP.md](docs/GITHUB_SETUP.md).

## Versioning Recommendation

Going forward, prefer Git tags/releases instead of copying backend folders repeatedly:

```text
v1.0
v1.1
v1.2
```

For now, `ros_backend1.0/` remains the canonical backend folder to avoid breaking existing paths and scripts.
