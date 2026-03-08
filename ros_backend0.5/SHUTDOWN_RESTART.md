# ros_backend0.5 Safe Shutdown / Restart

Last updated: March 5, 2026

Use this file when you want to stop everything cleanly and avoid restart issues.

## One file to run everything

Use this script:

- `./backend05_lifecycle.sh`

It already includes safe stop/start commands for nodes + container.

## Most important commands

### 1) Clean shutdown (recommended before closing laptop / Docker)

```bash
cd /Users/noahli/ros_unity_project/ros_backend0.5
./backend05_lifecycle.sh safe_down
```

What it does:
- Stops all teleop/sim/sync ROS nodes inside `motion_planner_05`
- Runs `docker compose down --remove-orphans`
- Cleans stale same-name containers

### 2) Clean container restart

```bash
cd /Users/noahli/ros_unity_project/ros_backend0.5
./backend05_lifecycle.sh restart_container
```

### 3) Full bringup (Parts 1-4 + sim + servo)

```bash
cd /Users/noahli/ros_unity_project/ros_backend0.5
./backend05_lifecycle.sh bringup_all
```

### 4) Quick status check

```bash
cd /Users/noahli/ros_unity_project/ros_backend0.5
./backend05_lifecycle.sh status
```

## If RX 0.0 Hz appears again

1. Run:

```bash
cd /Users/noahli/ros_unity_project/ros_backend0.5
./backend05_lifecycle.sh restart_container
./backend05_lifecycle.sh start_receiver
./backend05_lifecycle.sh status
```

2. Confirm Quest sender target is:
- `192.168.1.199:5016`

3. Confirm port mapping:

```bash
docker port motion_planner_05
```

Expected:
- `5005/udp -> 0.0.0.0:5016`

## Optional fine-grained commands

```bash
./backend05_lifecycle.sh stop_nodes
./backend05_lifecycle.sh up_container
./backend05_lifecycle.sh start_sim
./backend05_lifecycle.sh start_servo
./backend05_lifecycle.sh start_receiver
./backend05_lifecycle.sh start_part23
./backend05_lifecycle.sh start_part4
```

