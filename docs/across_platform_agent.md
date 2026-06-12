# Across-Platform Agent Note: Ubuntu Bringup Test

This note is for a future coding agent running on a Linux/Ubuntu machine. The goal is to clone this project, bring up the ROS/Gazebo backend, install/run the Quest app if available, and report exactly where the setup succeeds or fails.

Current canonical project state:

| Item | Value |
| --- | --- |
| Backend | `ros_backend1.1` |
| Lifecycle script | `ros_backend1.1/scripts/backend11_lifecycle.sh` |
| Docker container | `motion_planner_11` |
| Unity project | `UnityApp/` |
| Main Unity scene | `UnityApp/Assets/Scenes/GazeboReplica_DualArm_MR.unity` |
| Quest TCP wired port | `127.0.0.1:5026` |
| Unity ROS-TCP wired port | `127.0.0.1:10001` |

## Expected Ubuntu Environment

Preferred test OS:

```text
Ubuntu 22.04 LTS or Ubuntu 24.04 LTS
```

Expected hardware/software:

- Docker Engine and Docker Compose plugin.
- Git and Git LFS.
- Android platform tools / `adb`.
- Optional NVIDIA GPU with working NVIDIA container runtime.
- Optional Unity Editor if rebuilding the Quest app on Linux.

## What The Linux Agent Should Report Back

At the end of the test, report:

- Ubuntu version.
- CPU/GPU/RAM summary.
- Docker version.
- Whether Docker can build/start `motion_planner_11`.
- Whether `build_ws` succeeds.
- Whether `bringup_dual` succeeds.
- Whether Quest wired mode works through `adb reverse`.
- RTF / CPU observations if Gazebo starts.
- Any exact command that failed and its last 50-100 log lines.

## 1. Install Base Tools

Run these in the Ubuntu terminal:

```bash
sudo apt update
sudo apt install -y \
  git \
  git-lfs \
  curl \
  ca-certificates \
  gnupg \
  lsb-release \
  android-tools-adb

git lfs install
adb version
```

Expected:

```text
adb version
```

prints a valid Android Debug Bridge version.

## 2. Install Docker Engine

If Docker is not installed, use Docker's official apt repository setup:

```bash
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg

echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
```

Allow the current user to run Docker without `sudo`:

```bash
sudo usermod -aG docker "$USER"
newgrp docker
```

Check Docker:

```bash
docker version
docker compose version
docker run --rm hello-world
```

Expected:

- `docker version` prints client/server versions.
- `docker run --rm hello-world` completes successfully.

## 3. Optional NVIDIA GPU Container Setup

Only do this on an Ubuntu machine with an NVIDIA GPU and installed host NVIDIA driver.

Check GPU first:

```bash
nvidia-smi
```

If that works, install NVIDIA container toolkit:

```bash
curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | \
  sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg

curl -s -L https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list | \
  sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' | \
  sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list

sudo apt update
sudo apt install -y nvidia-container-toolkit
sudo nvidia-ctk runtime configure --runtime=docker
sudo systemctl restart docker
```

Check container GPU access:

```bash
docker run --rm --gpus all nvidia/cuda:12.4.1-base-ubuntu22.04 nvidia-smi
```

Expected:

- The container prints `nvidia-smi` output.

If this fails, continue CPU-only first and record the failure.

## 4. Clone The Project

Use SSH if keys are configured:

```bash
git clone git@github.com:su-idr-lab/ros_unity_project.git
cd ros_unity_project
git lfs pull
```

If SSH is not configured, use HTTPS for a read-only setup:

```bash
git clone https://github.com/su-idr-lab/ros_unity_project.git
cd ros_unity_project
git lfs pull
```

Check repo state:

```bash
git status --short
ls
ls ros_backend1.1
```

Expected:

- `ros_backend1.1/` exists.
- `UnityApp/` exists.

## 5. Configure Backend Network Mode

Use wired Quest mode first because it is the most repeatable across networks:

```bash
cd ros_backend1.1
cp .env.example .env 2>/dev/null || true
./scripts/backend11_lifecycle.sh mode_wired
```

Expected `.env` behavior:

```text
ROS_TCP_HOST_BIND=127.0.0.1
QUEST_TCP_HOST_BIND=127.0.0.1
```

## 6. Start Container And Build Workspace

From `ros_backend1.1`:

```bash
./scripts/backend11_lifecycle.sh up_container
./scripts/backend11_lifecycle.sh build_ws
./scripts/backend11_lifecycle.sh build_receiver
./scripts/backend11_lifecycle.sh status
```

Expected:

- Docker container `motion_planner_11` is running.
- `build_ws` completes without compile errors.
- `status` shows backend processes or at least a healthy container.

If build fails, collect:

```bash
docker ps -a
docker logs motion_planner_11 --tail 100
```

## 7. Connect Quest Over USB

Connect Quest 3 by USB and accept the headset authorization prompt.

```bash
adb devices
```

Expected:

```text
<device_id>    device
```

If it says `unauthorized`, put on the headset and accept USB debugging.

Enable wired tunnels:

```bash
./scripts/backend11_lifecycle.sh wired_on
./scripts/backend11_lifecycle.sh wired_status
adb reverse --list
```

Expected reverse tunnels:

```text
tcp:5026 tcp:5026
tcp:10001 tcp:10001
```

## 8. Install Prebuilt Quest APK If Available

If the APK was downloaded from a GitHub Release or copied locally:

```bash
ls UnityApp/App_Build/*.apk
adb install -r -d 'UnityApp/App_Build/R&U_7.0.1.apk'
```

If the file is not present, this is not a backend failure. Record that the prebuilt app is missing and either download it from the release artifact or rebuild from Unity.

Expected app package:

```bash
adb shell pm list packages | grep -i noahli
```

Expected package ID:

```text
com.noahli.ROSUNITY
```

## 9. Bring Up The Full Backend

From `ros_backend1.1`:

```bash
./scripts/backend11_lifecycle.sh bringup_dual
./scripts/backend11_lifecycle.sh status
```

Expected:

- Container remains running.
- Gazebo starts headless by default.
- MoveIt Servo starts for both arms.
- Quest TCP receiver starts.
- Dual Part 2/3 control nodes start.
- Part 4 sync/ROS-TCP/haptics/task-manager nodes start.

Check logs:

```bash
docker exec motion_planner_11 bash -lc 'tail -n 80 /tmp/run_dual_arm_tabletop_sim.log'
docker exec motion_planner_11 bash -lc 'tail -n 80 /tmp/servo_dual_gz.log'
docker exec motion_planner_11 bash -lc 'tail -n 80 /tmp/qcr.log'
docker exec motion_planner_11 bash -lc 'tail -n 80 /tmp/dual_part4_tcp_endpoint.log'
```

## 10. Basic ROS Health Checks

```bash
CONTAINER=motion_planner_11
ROS_ENV='source /opt/ros/humble/setup.bash && source /home/noah/ws_moveit/install/setup.bash'

docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic list | sort | sed -n '1,120p'"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic hz /joint_states"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic hz /left_arm/received_pose_states"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 topic hz /right_arm/received_pose_states"
```

Expected:

- `/joint_states` publishes when simulation and controllers are alive.
- `/left_arm/received_pose_states` and `/right_arm/received_pose_states` publish only when the Quest app is running and sending controller packets.

## 11. Optional Headed Gazebo Debug

Default is headless. If visual inspection is required:

```bash
SIM_HEADLESS=0 ./scripts/backend11_lifecycle.sh start_dual_sim
```

Then use noVNC or the configured desktop path if available in this container. Record whether headed mode reduces RTF.

## 12. Useful Failure Report Template

When reporting back, include:

```text
OS:
Kernel:
CPU:
GPU:
RAM:
Docker version:
Docker compose version:
NVIDIA container runtime: yes/no/not tested
Repo commit:
Backend command run:
Quest connected by adb: yes/no
APK installed: yes/no
bringup_dual result: pass/fail
Primary failure:
Relevant logs:
```
