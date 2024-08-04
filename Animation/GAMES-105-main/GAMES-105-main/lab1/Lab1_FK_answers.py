import numpy as np
from scipy.spatial.transform import Rotation as R

def load_motion_data(bvh_file_path):
    """part2 辅助函数，读取bvh文件"""
    with open(bvh_file_path, 'r') as f:
        lines = f.readlines()
        for i in range(len(lines)):
            if lines[i].startswith('Frame Time'):
                break
        motion_data = []
        for line in lines[i+1:]:
            data = [float(x) for x in line.split()]
            if len(data) == 0:
                break
            motion_data.append(np.array(data).reshape(1,-1))
        motion_data = np.concatenate(motion_data, axis=0)
    return motion_data



def part1_calculate_T_pose(bvh_file_path):
    """请填写以下内容
    输入： bvh 文件路径
    输出:
        joint_name: List[str]，字符串列表，包含着所有关节的名字
        joint_parent: List[int]，整数列表，包含着所有关节的父关节的索引,根节点的父关节索引为-1
        joint_offset: np.ndarray，形状为(M, 3)的numpy数组，包含着所有关节的偏移量

    Tips:
        joint_name顺序应该和bvh一致
    """

    joint_name = []
    joint_parent = []
    joint_offset = []

    with open(bvh_file_path, 'r') as f:
        
        lines = f.readlines()
        jointList = [-1]
        parentId = -1
        i = 0
        while i < len(lines):
            if lines[i].strip().startswith('MOTION'):
                break

            if lines[i].strip().startswith('{'):
                parentId += 1
                i += 1
                continue

            if lines[i].strip().startswith('}'):
                jointList.pop()
                i += 1
                continue

            if lines[i].strip().startswith('HIERARCHY'):
                i += 1
                continue

            if lines[i].strip().startswith('ROOT'):
                joint_name.append('RootJoint')
                joint_parent.append(jointList[-1])
                parentId += 1
                jointList.append(parentId)
                joint_offset = np.empty(shape=[0, 3])

                i += 1
                continue

            if lines[i].strip().startswith('JOINT'):
                data = lines[i].split()
                joint_name.append(data[1])
                joint_parent.append(jointList[-1])
                jointList.append(parentId)

                i += 1
                continue

            if lines[i].strip().startswith('OFFSET'):
                data = lines[i].split()
                joint_offset = np.append(joint_offset, [[float(data[1]), float(data[2]), float(data[3])]], axis= 0)

                i += 1
                continue

            if lines[i].strip().startswith('End'):
                joint_name.append(joint_name[-1] + "_end")
                joint_parent.append(jointList[-1])
                jointList.append(parentId)

                i += 1
                continue

            i += 1

    return joint_name, joint_parent, joint_offset


def part2_forward_kinematics(joint_name, joint_parent, joint_offset, motion_data, frame_id):
    joint_positions = []
    joint_orientations = []

    offsetCount = 0
    channelCount = 0
    i = 0

    frame_data = motion_data[frame_id].reshape(-1, 3)
    currentFrameMotionData = R.from_euler('XYZ', frame_data[1:], degrees=True).as_quat()
    while i < len(joint_name):
        if "_end" in joint_name[i]:
            currentFrameMotionData = np.insert(currentFrameMotionData, i, [0,0,0,1], axis=0)
        i += 1

    i = 0
    while i < len(joint_name):
        if joint_name[i] == 'RootJoint':
            joint_positions.append(frame_data[0])
            joint_orientations.append(currentFrameMotionData[channelCount])
            channelCount += 1
        else:
            r_p =  R.from_quat(currentFrameMotionData[joint_parent[i]])
            r_i =  R.from_quat(currentFrameMotionData[i])
            rotation = R.as_quat(r_i * r_p)
            joint_orientations.append(rotation)
            channelCount += 1
            joint_orientations_EA = R.from_quat(joint_orientations)

            position = joint_orientations_EA[joint_parent[i]].apply(joint_offset[i])
            joint_positions.append(joint_positions[joint_parent[i]] + position)

        i += 1

    joint_positions = np.array(joint_positions)
    joint_orientations = np.array(joint_orientations)
    return joint_positions, joint_orientations


def part3_retarget_func(T_pose_bvh_path, A_pose_bvh_path):
    """
    将 A-pose的bvh重定向到T-pose上
    输入: 两个bvh文件的路径
    输出: 
        motion_data: np.ndarray，形状为(N,X)的numpy数组，其中N为帧数，X为Channel数。retarget后的运动数据
    Tips:
        两个bvh的joint name顺序可能不一致哦(
        as_euler时也需要大写的XYZ
    """

    joint_name_T, joint_parent_T, joint_offset_T = part1_calculate_T_pose(T_pose_bvh_path)
    joint_name_A, joint_parent_A, joint_offset_A = part1_calculate_T_pose(A_pose_bvh_path)
    motion_data_A = load_motion_data(A_pose_bvh_path)
    motion_data_T = []
    
    rootMotionData_A = motion_data_A[:, :3]
    jointMotionData_A = motion_data_A[:, 3:]
    motion_data_T = np.zeros(jointMotionData_A.shape)

    jointDataDic_A = {}
    jointDataDic_T = {}
    i = 0
    for name in joint_name_A:
        if "_end" not in name:
            jointDataDic_A[name] = i
            i += 1

    i = 0
    for name in joint_name_T:
        if "_end" not in name:
            jointDataDic_T[name] = i
            i += 1

    for name in joint_name_T:
        if "_end" not in name: 
            index_A = jointDataDic_A[name]
            index_T = jointDataDic_T[name]
            if name == "lShoulder":
                jointMotionData_A[:, index_A*3 + 2] -= 45
            elif name == "rShoulder":
                jointMotionData_A[:, index_A*3 + 2] += 45
            motion_data_T[:, 3*index_T:3*(index_T+1)] = jointMotionData_A[:, index_A*3: (index_A+1)*3]

    motion_data_T = np.concatenate([rootMotionData_A, motion_data_T], axis=1)
    return motion_data_T
