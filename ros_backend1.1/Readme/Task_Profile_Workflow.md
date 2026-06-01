# Task Profile Workflow

This backend now separates the reusable workspace from the swappable task layout.

## Layout

```text
profiles/
  workspaces/
    dual_arm_tabletop/workspace.yaml
  robots/
    ur5e_hande_dual/robot.yaml
  tasks/
    pick_place_basic/task.yaml
  scenes/
    dual_arm_tabletop/scene.yaml
```

`scene.yaml` chooses one workspace, one robot profile, and one active task:

```yaml
workspace_profile: ../../workspaces/dual_arm_tabletop/workspace.yaml
robot_profile: ../../robots/ur5e_hande_dual/robot.yaml
task_profile: ../../tasks/rubik_2x2/task.yaml
active_task_group: TaskGroup_Main
```

Available task profiles:

```text
profiles/tasks/pick_place_basic/task.yaml
profiles/tasks/rubik_2x2/task.yaml
profiles/tasks/cable_insertion/task.yaml
```

The `rubik_2x2` task expands into eight Gazebo-owned cubie models named `Sync_Rubik2x2_cubie_*`. The Unity scene only visualizes/syncs those cubies; Rubik twisting is controlled by the backend `/rubik2x2/*` services.

The `cable_insertion` task creates a Rubik-sized receiver box with a visual port and a thin cable rod with a rectangular plug end.

## Workspace vs Task

The workspace profile owns the reusable table/workspace and task group anchors:

```yaml
task_groups:
  - name: TaskGroup_Main
    unity_local_position_xyz: [0.0, 0.0, 0.25]
    unity_local_euler_xyz: [0.0, 0.0, 0.0]
```

The task profile owns only the task objects relative to that task group:

```yaml
objects:
  - id: Sync_RedCube
    type: box
    local_position_xyz: [-0.055, 0.020, -0.060]
```

So changing the task group's position moves the whole task. Changing a single object's `local_position_xyz` moves only that object inside the task group.

## Coordinate Convention

Task profile object poses use Unity workspace axes:

```text
x = right/left in MR
y = up/down
z = forward/back on the table
```

The Gazebo world generator converts Unity workspace vectors into Gazebo/ROS world vectors:

```text
Gazebo x = Unity z
Gazebo y = -Unity x
Gazebo z = Unity y
```

## Generate Gazebo World

From the host machine:

```bash
cd ros_backend1.1
./scripts/backend11_lifecycle.sh generate_dual_world
```

To generate a different task without editing the default scene profile:

```bash
DUAL_SCENE_PROFILE=profiles/scenes/dual_arm_cable_insertion/scene.yaml ./scripts/backend11_lifecycle.sh generate_dual_world
```

`start_dual_sim` and `bringup_dual` run this automatically before starting Gazebo.

Generated output:

```text
simulation/worlds/ur_hande_dual_arm_tabletop.sdf
../UnityApp/Assets/Resources/TaskProfiles/active_task.json
```

Gazebo still receives task objects as top-level models. The task group is a logical config transform, not a Gazebo parent model. This keeps physics, reset, and object pose sync simple.

For the Rubik task, each cubie is a top-level Gazebo model so Part 4 can synchronize each cubie pose to Unity independently.

## Unity Hierarchy

The Unity scene builder now creates this hierarchy:

```text
GazeboWorkspace
  TaskGroups
    TaskGroup_Main
      GameObjects_sync
        Sync_RedCube
        Sync_GreenCube
        Sync_RedCylinder
        Sync_GreenCylinder
        Sync_Plate_A
        Sync_Plate_B
```

The MR workspace drag moves `GazeboWorkspace`, so the robot visuals, task group, and task objects move together.

Unity reads the generated `active_task.json` through `Resources.Load("TaskProfiles/active_task")`, so the Unity task visuals and the Gazebo SDF come from the same workspace/task profiles.

## Creating a New Task

1. Copy `profiles/tasks/pick_place_basic/task.yaml` to a new folder.
2. Edit object IDs, shapes, sizes, colors, and `local_position_xyz` values.
3. Either point `profiles/scenes/dual_arm_tabletop/scene.yaml` at the new task profile, or create a new scene profile under `profiles/scenes/`.
4. Run `./scripts/backend11_lifecycle.sh generate_dual_world`, optionally with `DUAL_SCENE_PROFILE=profiles/scenes/<your_scene>/scene.yaml`.
5. In Unity, run `Tools > Gazebo Replica > Rebuild Dual Arm Workspace In Active Scene`.

## Rubik 2x2 Backend Services

After `start_dual_part4`, the Rubik controller exposes:

```bash
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 service call /rubik2x2/twist_x std_srvs/srv/Trigger {}"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 service call /rubik2x2/twist_y std_srvs/srv/Trigger {}"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 service call /rubik2x2/twist_z std_srvs/srv/Trigger {}"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 service call /rubik2x2/shuffle std_srvs/srv/Trigger {}"
docker exec "$CONTAINER" bash -lc "$ROS_ENV && ros2 service call /rubik2x2/reset std_srvs/srv/Trigger {}"
```

This is a Gazebo-side kinematic mechanism. A fully passive Rubik cube that twists only from gripper contact would require a dedicated Gazebo plugin with constraint switching/reparenting logic.
