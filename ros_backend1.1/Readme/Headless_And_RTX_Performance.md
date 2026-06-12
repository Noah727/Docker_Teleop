# Headless Simulation And RTX GPU Plan

This backend should run Gazebo headless by default for Quest MR teleop. Unity is the visual frontend, so Gazebo GUI/noVNC should only be enabled when debugging simulation visuals.

## Current Mac Development Mode

Default backend behavior:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh restart_container
./scripts/backend11_lifecycle.sh bringup_dual
```

This starts:

- container desktop/noVNC: disabled by default
- Gazebo: headless server mode by default
- Unity/Quest communication: unchanged
- ROS/Gazebo physics: still CPU-bound on Mac

## Enable Gazebo GUI And noVNC Only For Debugging

Use this when you need to look at Gazebo in the browser:

```bash
cd ros_backend1.1
ENABLE_DESKTOP=1 ENABLE_NOVNC=1 ./scripts/backend11_lifecycle.sh restart_container
SIM_HEADLESS=0 ./scripts/backend11_lifecycle.sh start_dual_sim
```

Then open:

```text
http://localhost:6080/vnc.html
```

When finished, go back to headless:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh restart_container
./scripts/backend11_lifecycle.sh bringup_dual
```

## Check Real-Time Factor

After the sim is running:

```bash
CONTAINER=motion_planner_11
docker exec "$CONTAINER" bash -lc 'ign topic -l | grep stats'
docker exec "$CONTAINER" bash -lc 'timeout 12 ign topic -e -t /stats 2>/dev/null | awk '\''/real_time_factor:/ {v=$2+0; n++; sum+=v; if(n==1 || v<min) min=v; if(n==1 || v>max) max=v} END {if(n>0) printf("samples=%d avg=%.3f min=%.3f max=%.3f\n", n, sum/n, min, max); else print "no samples"}'\'''
```

If `/stats` is not available, check the world-specific stats topic:

```bash
docker exec "$CONTAINER" bash -lc 'ign topic -l | grep -E "stats|world"'
```

## What Actually Reduces CPU Load

Most helpful:

- Run Gazebo headless: `SIM_HEADLESS=1`.
- Keep desktop/noVNC off: `ENABLE_DESKTOP=0`.
- Do not run Gazebo camera sensors unless needed.
- Keep Unity as the visualizer and Gazebo as physics only.
- Avoid leaving debug generators, samplers, or extra bridges running.

Optional performance-test knobs:

```bash
# Lower Unity object sync rate from 60 Hz to 30 Hz.
DUAL_SYNC_RATE_HZ=30 ./scripts/backend11_lifecycle.sh start_dual_part4

# Lower haptic polling rate.
DUAL_HAPTIC_RATE_HZ=30 ./scripts/backend11_lifecycle.sh start_dual_part4

# Disable haptics completely for CPU/RTF testing.
ENABLE_HAPTICS=0 ./scripts/backend11_lifecycle.sh start_dual_part4

# Disable the older EE-gap haptic source while keeping Gazebo contact haptics.
DUAL_HAPTIC_ENABLE_SERVO_GAP=false ./scripts/backend11_lifecycle.sh start_dual_part4
```

Less helpful:

- GPU acceleration on Mac Docker. Docker Desktop on macOS does not expose an NVIDIA GPU path, and Gazebo physics is still mostly CPU-bound.

## Ubuntu RTX Acceleration Plan

This is for a lab Ubuntu machine with an NVIDIA RTX GPU.

### 1. Host Setup

Install and verify:

```bash
nvidia-smi
docker --version
docker compose version
```

Install NVIDIA Container Toolkit on the host, then test:

```bash
docker run --rm --gpus all nvidia/cuda:12.4.1-base-ubuntu22.04 nvidia-smi
```

### 2. Use An amd64 Image

The Mac backend Dockerfile currently starts from:

```Dockerfile
FROM arm64v8/ros:humble-ros-base
```

For an Ubuntu RTX PC, use an amd64 ROS base instead:

```Dockerfile
FROM ros:humble-ros-base
```

Recommended long-term layout:

```text
Dockerfile              # Apple Silicon / current Mac workflow
Dockerfile.ubuntu_rtx   # Ubuntu amd64 + optional NVIDIA graphics deps
docker-compose.yaml
docker-compose.rtx.yaml
```

### 3. Add GPU Access In Compose

Example override file:

```yaml
services:
  motion_planner:
    gpus: all
    environment:
      - NVIDIA_VISIBLE_DEVICES=all
      - NVIDIA_DRIVER_CAPABILITIES=compute,utility,graphics,display
```

Run:

```bash
docker compose -f docker-compose.yaml -f docker-compose.rtx.yaml up -d --build
docker exec motion_planner_11 nvidia-smi
```

### 4. What GPU Will And Will Not Improve

GPU can help with:

- Gazebo GUI rendering
- camera/image sensors
- RViz-style visualization if used in-container

GPU will not magically solve:

- Gazebo rigid-body physics CPU cost
- MoveIt Servo collision checking CPU cost
- ROS message scheduling/load

For the current Quest teleop system, the first performance target is still headless Gazebo plus no desktop/noVNC. GPU becomes useful when camera sensors or GPU-rendered Gazebo/RViz views are needed.
