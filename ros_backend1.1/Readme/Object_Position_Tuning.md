# Manually Changing Workspace Object Positions

The current system separates object placement into a reusable workspace anchor and a task profile.

## Files To Edit

Primary files:

1. Workspace/task-group anchor:
   `ros_backend1.1/profiles/workspaces/dual_arm_tabletop/workspace.yaml`

2. Current task objects:
   `ros_backend1.1/profiles/tasks/rubik_2x2/task.yaml`

3. Scene selector:
   `ros_backend1.1/profiles/scenes/dual_arm_tabletop/scene.yaml`

Generated file:

```text
ros_backend1.1/simulation/worlds/ur_hande_dual_arm_tabletop.sdf
UnityApp/Assets/Resources/TaskProfiles/active_task.json
```

Do not hand-edit generated files unless you are doing a temporary test. Regenerate them from profiles instead.

## Workspace / Task Group / Object

The workspace profile defines the task group position:

```yaml
task_groups:
  - name: TaskGroup_Main
    unity_local_position_xyz: [0.0, 0.0, 0.25]
```

The task profile defines each object relative to that task group:

```yaml
objects:
  - id: Sync_RedCube
    local_position_xyz: [-0.055, 0.020, -0.060]
```

Final Unity workspace-local object pose:

```text
object_workspace_position = task_group_position + object_local_position
```

Example:

```text
TaskGroup_Main: [0.0, 0.0, 0.25]
Sync_RedCube local: [-0.055, 0.020, -0.060]
Sync_RedCube workspace: [-0.055, 0.020, 0.580]
```

## Coordinate Conversion

Task profile positions use Unity workspace axes:

```text
x = right/left in MR
y = height
z = forward/back on the table
```

Generated Gazebo positions use:

```text
Gazebo x = Unity z
Gazebo y = -Unity x
Gazebo z = Unity y
```

So this Unity workspace position:

```text
Unity: [-0.055, 0.020, 0.580]
```

becomes:

```text
Gazebo: [0.580, 0.055, 0.020]
```

## Regenerate And Run

From the host:

```bash
cd /Users/noahli/ros_unity_project/ros_backend1.1
./scripts/backend11_lifecycle.sh generate_dual_world
```

`start_dual_sim` and `bringup_dual` do this automatically.

To restart after changing task positions:

```bash
./scripts/backend11_lifecycle.sh start_dual_sim
./scripts/backend11_lifecycle.sh start_dual_servo
./scripts/backend11_lifecycle.sh start_dual_part23
./scripts/backend11_lifecycle.sh start_dual_part4
```

In Unity, run:

```text
Tools > Gazebo Replica > Rebuild Dual Arm Workspace In Active Scene
```

## Current Task Group Layout

```text
TaskGroup_Main position: [0.0, 0.0, 0.25]

Rubik2x2 local:          [ 0.000, 0.020, -0.020]
RubikGoalPlate_Left:     [-0.115, 0.0025, 0.075]
RubikGoalPlate_Right:    [ 0.115, 0.0025, 0.075]
```
