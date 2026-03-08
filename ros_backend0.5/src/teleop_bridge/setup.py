from setuptools import setup
import os
from glob import glob

package_name = 'teleop_bridge'

setup(
    name=package_name,
    version='0.1.0',
    packages=[package_name],
    data_files=[
        ('share/ament_index/resource_index/packages',
            ['resource/' + package_name]),
        ('share/' + package_name, ['package.xml']),
        (os.path.join('share', package_name, 'launch'), glob('launch/*.launch.py')),
        (os.path.join('share', package_name, 'config'), glob('config/*.yaml')),
    ],
    install_requires=['setuptools'],
    zip_safe=True,
    maintainer='User',
    maintainer_email='user@example.com',
    description='VR Hand Tracking to ROS 2 Bridge',
    license='TODO',
    tests_require=['pytest'],
    entry_points={
        'console_scripts': [
            'hand_to_servo = teleop_bridge.hand_to_servo:main',
            'test_joints = teleop_bridge.test_joints:main',
            'fake_hand_publisher = teleop_bridge.fake_hand_publisher:main',
            'pose_to_servo_twist = teleop_bridge.pose_to_servo_twist:main',
            'gripper_hold_to_position = teleop_bridge.gripper_hold_to_position:main',
            'quest_controller_receiver = teleop_bridge.quest_controller_receiver:main',
            'received_pose_to_target_twist = teleop_bridge.received_pose_to_target_twist:main',
            'target_twist_to_servo_cmd = teleop_bridge.target_twist_to_servo_cmd:main',
            'target_twist_to_gripper_cmd = teleop_bridge.target_twist_to_gripper_cmd:main',
            'target_twist_reset_manager = teleop_bridge.target_twist_reset_manager:main',
            'cube_pose_sync_publisher = teleop_bridge.cube_pose_sync_publisher:main',
        ],
    },
)
