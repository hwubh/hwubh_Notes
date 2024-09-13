import numpy as np
import copy
import math
from scipy.spatial.transform import Rotation as R
from scipy.spatial.transform import Slerp
# ------------- lab1里的代码 -------------#
def load_meta_data(bvh_path):
    with open(bvh_path, 'r') as f:
        channels = []
        joints = []
        joint_parents = []
        joint_offsets = []
        end_sites = []

        parent_stack = [None]
        for line in f:
            if 'ROOT' in line or 'JOINT' in line:
                joints.append(line.split()[-1])
                joint_parents.append(parent_stack[-1])
                channels.append('')
                joint_offsets.append([0, 0, 0])

            elif 'End Site' in line:
                end_sites.append(len(joints))
                joints.append(parent_stack[-1] + '_end')
                joint_parents.append(parent_stack[-1])
                channels.append('')
                joint_offsets.append([0, 0, 0])

            elif '{' in line:
                parent_stack.append(joints[-1])

            elif '}' in line:
                parent_stack.pop()

            elif 'OFFSET' in line:
                joint_offsets[-1] = np.array([float(x) for x in line.split()[-3:]]).reshape(1,3)

            elif 'CHANNELS' in line:
                trans_order = []
                rot_order = []
                for token in line.split():
                    if 'position' in token:
                        trans_order.append(token[0])

                    if 'rotation' in token:
                        rot_order.append(token[0])

                channels[-1] = ''.join(trans_order)+ ''.join(rot_order)

            elif 'Frame Time:' in line:
                break
        
    joint_parents = [-1]+ [joints.index(i) for i in joint_parents[1:]]
    channels = [len(i) for i in channels]
    return joints, joint_parents, channels, joint_offsets

def load_motion_data(bvh_path):
    with open(bvh_path, 'r') as f:
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

# ------------- 实现一个简易的BVH对象，进行数据处理 -------------#

'''
注释里统一N表示帧数，M表示关节数
position, rotation表示局部平移和旋转
translation, orientation表示全局平移和旋转
'''

class BVHMotion():
    def __init__(self, bvh_file_name = None) -> None:
        
        # 一些 meta data
        self.joint_name = []
        self.joint_channel = []
        self.joint_parent = []
        
        # 一些local数据, 对应bvh里的channel, XYZposition和 XYZrotation
        #! 这里我们把没有XYZ position的joint的position设置为offset, 从而进行统一
        self.joint_position = None # (N,M,3) 的ndarray, 局部平移
        self.joint_rotation = None # (N,M,4)的ndarray, 用四元数表示的局部旋转
        
        if bvh_file_name is not None:
            self.load_motion(bvh_file_name)
        pass
    
    #------------------- 一些辅助函数 ------------------- #
    def load_motion(self, bvh_file_path):
        '''
            读取bvh文件，初始化元数据和局部数据
        '''
        self.joint_name, self.joint_parent, self.joint_channel, joint_offset = \
            load_meta_data(bvh_file_path)
        
        motion_data = load_motion_data(bvh_file_path)

        # 把motion_data里的数据分配到joint_position和joint_rotation里
        self.joint_position = np.zeros((motion_data.shape[0], len(self.joint_name), 3))
        self.joint_rotation = np.zeros((motion_data.shape[0], len(self.joint_name), 4))
        self.joint_rotation[:,:,3] = 1.0 # 四元数的w分量默认为1
        
        cur_channel = 0
        for i in range(len(self.joint_name)):
            if self.joint_channel[i] == 0:
                self.joint_position[:,i,:] = joint_offset[i].reshape(1,3)
                continue   
            elif self.joint_channel[i] == 3:
                self.joint_position[:,i,:] = joint_offset[i].reshape(1,3)
                rotation = motion_data[:, cur_channel:cur_channel+3]
            elif self.joint_channel[i] == 6:
                self.joint_position[:, i, :] = motion_data[:, cur_channel:cur_channel+3]
                rotation = motion_data[:, cur_channel+3:cur_channel+6]
            self.joint_rotation[:, i, :] = R.from_euler('XYZ', rotation,degrees=True).as_quat()
            cur_channel += self.joint_channel[i]
        
        return

    def batch_forward_kinematics(self, joint_position = None, joint_rotation = None):
        '''
        利用自身的metadata进行批量前向运动学
        joint_position: (N,M,3)的ndarray, 局部平移
        joint_rotation: (N,M,4)的ndarray, 用四元数表示的局部旋转
        '''
        if joint_position is None:
            joint_position = self.joint_position
        if joint_rotation is None:
            joint_rotation = self.joint_rotation
        
        joint_translation = np.zeros_like(joint_position)
        joint_orientation = np.zeros_like(joint_rotation)
        joint_orientation[:,:,3] = 1.0 # 四元数的w分量默认为1
        
        # 一个小hack是root joint的parent是-1, 对应最后一个关节
        # 计算根节点时最后一个关节还未被计算，刚好是0偏移和单位朝向
        
        for i in range(len(self.joint_name)):
            pi = self.joint_parent[i]
            parent_orientation = R.from_quat(joint_orientation[:,pi,:]) 
            joint_translation[:, i, :] = joint_translation[:, pi, :] + \
                parent_orientation.apply(joint_position[:, i, :])
            joint_orientation[:, i, :] = (parent_orientation * R.from_quat(joint_rotation[:, i, :])).as_quat()
        return joint_translation, joint_orientation
    
    
    def adjust_joint_name(self, target_joint_name):
        '''
        调整关节顺序为target_joint_name
        '''
        idx = [self.joint_name.index(joint_name) for joint_name in target_joint_name]
        idx_inv = [target_joint_name.index(joint_name) for joint_name in self.joint_name]
        self.joint_name = [self.joint_name[i] for i in idx]
        self.joint_parent = [idx_inv[self.joint_parent[i]] for i in idx]
        self.joint_parent[0] = -1
        self.joint_channel = [self.joint_channel[i] for i in idx]
        self.joint_position = self.joint_position[:,idx,:]
        self.joint_rotation = self.joint_rotation[:,idx,:]
        pass
    
    def raw_copy(self):
        '''
        返回一个拷贝
        '''
        return copy.deepcopy(self)
    
    @property
    def motion_length(self):
        return self.joint_position.shape[0]
    
    
    def sub_sequence(self, start, end):
        '''
        返回一个子序列
        start: 开始帧
        end: 结束帧
        '''
        res = self.raw_copy()
        res.joint_position = res.joint_position[start:end,:,:]
        res.joint_rotation = res.joint_rotation[start:end,:,:]
        return res
    
    def append(self, other):
        '''
        在末尾添加另一个动作
        '''
        other = other.raw_copy()
        other.adjust_joint_name(self.joint_name)
        self.joint_position = np.concatenate((self.joint_position, other.joint_position), axis=0)
        self.joint_rotation = np.concatenate((self.joint_rotation, other.joint_rotation), axis=0)
        pass
    
    #--------------------- 你的任务 -------------------- #
    
    def decompose_rotation_with_yaxis(self, rotation):
        '''
        输入: rotation 形状为(4,)的ndarray, 四元数旋转
        输出: Ry, Rxz，分别为绕y轴的旋转和转轴在xz平面的旋转，并满足R = Ry * Rxz
        '''
        Ry = np.zeros_like(rotation)
        Rxz = np.zeros_like(rotation)
        # TODO: 你的代码
        rot_matrix = R.from_quat(rotation).as_matrix()
        y_axis = rot_matrix[:, 1]
        rot_axis = np.cross(y_axis, (0,1,0))
        y_angle = np.arccos(np.dot(y_axis, (0,1,0)) / np.linalg.norm(y_axis))
        Ry = (R.from_rotvec(rot_axis * y_angle / np.linalg.norm(rot_axis)) * R.from_quat(rotation)).as_quat()
        Ry_inv = R.from_quat(Ry).inv()
        Rxz = (Ry_inv * R.from_quat(rotation)).as_quat()

        return Ry, Rxz
    
    # part 1
    def translation_and_rotation(self, frame_num, target_translation_xz, target_facing_direction_xz):
        '''
        计算出新的joint_position和joint_rotation
        使第frame_num帧的根节点平移为target_translation_xz, 水平面朝向为target_facing_direction_xz
        frame_num: int
        target_translation_xz: (2,)的ndarray
        target_faceing_direction_xz: (2,)的ndarray，表示水平朝向。你可以理解为原本的z轴被旋转到这个方向。
        Tips:
            主要是调整root节点的joint_position和joint_rotation
            frame_num可能是负数，遵循python的索引规则
            你需要完成并使用decompose_rotation_with_yaxis
            输入的target_facing_direction_xz的norm不一定是1
        '''
        
        res = self.raw_copy() # 拷贝一份，不要修改原始数据
        
        # 比如说，你可以这样调整第frame_num帧的根节点平移
        offset = target_translation_xz - res.joint_position[frame_num, 0, [0,2]]
        res.joint_position[:, 0, [0,2]] += offset
        # TODO: 你的代码
        Ry, Rxz = self.decompose_rotation_with_yaxis(res.joint_rotation[frame_num, 0])
        r_matrix = R.from_quat(Ry).as_matrix()
        Local_Z_Axis = r_matrix[:, 2]
        Target_Z_Axis = np.array([target_facing_direction_xz[0], 0, target_facing_direction_xz[1]])

        y_axis = np.cross(Local_Z_Axis, Target_Z_Axis) / (np.linalg.norm(Local_Z_Axis) * np.linalg.norm(Target_Z_Axis))
        r_angle = np.arccos(np.dot(Local_Z_Axis, Target_Z_Axis) / (np.linalg.norm(Local_Z_Axis) * np.linalg.norm(Target_Z_Axis)))
        delta_rotation = R.from_rotvec(r_angle * y_axis)

        res.joint_rotation[:, 0, :] = np.apply_along_axis(lambda q: (delta_rotation * R.from_quat(q)).as_quat(), axis=1, arr= res.joint_rotation[:, 0, :])
        
        offset_center = res.joint_position[frame_num, 0, [0,2]]
        res.joint_position[:, 0, [0,2]] -= offset_center
        res.joint_position[:, 0, :] = np.apply_along_axis(delta_rotation.apply, axis=1, arr=res.joint_position[:, 0, :])
        res.joint_position[:, 0, [0,2]] += offset_center

        return res

def slerp_single_quat(q1, q2, alpha):
    slerp = Slerp([0, 1], R.from_quat([q1, q2]))
    return slerp([alpha]).as_quat()[0]

def Interpolation(position1, position2, rotation1, rotation2, lerp):
    position = np.empty_like(position1)
    rotation = np.empty_like(rotation1)
    
    rotation = np.array([slerp_single_quat(q1, q2, lerp) for q1, q2 in zip(rotation1, rotation2)])
    position = (1-lerp) * position1 + lerp * position2

    return position, rotation

# part2
def blend_two_motions(bvh_motion1, bvh_motion2, alpha):
    '''
    blend两个bvh动作
    假设两个动作的帧数分别为n1, n2
    alpha: 0~1之间的浮点数组，形状为(n3,)
    返回的动作应该有n3帧，第i帧由(1-alpha[i]) * bvh_motion1[j] + alpha[i] * bvh_motion2[k]得到
    i均匀地遍历0~n3-1的同时，j和k应该均匀地遍历0~n1-1和0~n2-1
    '''
    
    res = bvh_motion1.raw_copy()
    res.joint_position = np.zeros((len(alpha), res.joint_position.shape[1], res.joint_position.shape[2]))
    res.joint_rotation = np.zeros((len(alpha), res.joint_rotation.shape[1], res.joint_rotation.shape[2]))
    res.joint_rotation[...,3] = 1.0

    # TODO: 你的代码
    cur_delta_time = 1 / (len(alpha) - 1)
    bvh1_delta_time = 1 / (bvh_motion1.motion_length - 1)
    bvh2_delta_time = 1 / (bvh_motion2.motion_length - 1)

    for i in range(len(alpha)):
        bvh1_time = i * cur_delta_time / bvh1_delta_time
        bvh1_index = math.floor(bvh1_time)
        lerp_Value_1 = bvh1_time - bvh1_index

        position1, rotation1 = Interpolation(
            bvh_motion1.joint_position[bvh1_index, ...],
            bvh_motion1.joint_position[(bvh1_index + 1) % bvh_motion1.motion_length, ...],
            bvh_motion1.joint_rotation[bvh1_index, ...],
            bvh_motion1.joint_rotation[(bvh1_index + 1) % bvh_motion1.motion_length, ...],
            lerp_Value_1
        )

        bvh2_time = i * cur_delta_time / bvh2_delta_time
        bvh2_index = math.floor(bvh2_time)
        lerp_Value_2 = bvh2_time - bvh2_index

        position2, rotation2 = Interpolation(
            bvh_motion2.joint_position[bvh2_index, ...],
            bvh_motion2.joint_position[(bvh2_index + 1) % bvh_motion2.motion_length, ...],
            bvh_motion2.joint_rotation[bvh2_index, ...],
            bvh_motion2.joint_rotation[(bvh2_index + 1) % bvh_motion2.motion_length, ...],
            lerp_Value_2
        )

        res.joint_position[i, ...], res.joint_rotation[i, ...] = Interpolation(
            position1,
            position2,
            rotation1,
            rotation2,
            alpha[i]
        )

    
    return res

# part3
def build_loop_motion(bvh_motion):
    '''
    将bvh动作变为循环动作
    由于比较复杂,作为福利,不用自己实现
    (当然你也可以自己实现试一下)
    推荐阅读 https://theorangeduck.com/
    Creating Looping Animations from Motion Capture
    '''
    res = bvh_motion.raw_copy()
    
    from smooth_utils import build_loop_motion
    return build_loop_motion(res)

# part4
def nearest_frame(motion, target_pose):
    def pose_distance(pose1, pose2):
        total_dis = 0.
        for i in range(1, pose1.shape[0]):
            total_dis += np.linalg.norm(pose1[i] - pose2[i])
        return total_dis

    min_dis = float("inf")
    ret = -1
    for i in range(motion.motion_length):
        dis = pose_distance(motion.joint_rotation[i], target_pose)
        if dis < min_dis:
            ret = i
            min_dis = dis
    return ret

# part4 linear interpolation
def concatenate_two_motions(bvh_motion1, bvh_motion2, mix_frame1, mix_time):
    '''
    将两个bvh动作平滑地连接起来，mix_time表示用于混合的帧数
    混合开始时间是第一个动作的第mix_frame1帧
    虽然某些混合方法可能不需要mix_time，但是为了保证接口一致，我们还是保留这个参数
    Tips:
        你可能需要用到BVHMotion.sub_sequence 和 BVHMotion.append
    '''
    res = bvh_motion1.raw_copy()
    # TODO: 你的代码
    # 下面这种直接拼肯定是不行的(
    # res.joint_position = np.concatenate([res.joint_position[:mix_frame1], bvh_motion2.joint_position], axis=0)
    # res.joint_rotation = np.concatenate([res.joint_rotation[:mix_frame1], bvh_motion2.joint_rotation], axis=0)

    bvh_motion2 = build_loop_motion(bvh_motion2)
    mix_frame2 = nearest_frame(bvh_motion2, res.joint_rotation[mix_frame1])
    bvh_motion2_sub = bvh_motion2.raw_copy()
    target_translation_xz = bvh_motion2_sub.joint_position[-1, 0, [0, 2]]
    target_facing_direction_xz = R.from_quat(bvh_motion2.joint_rotation[-1, 0]).apply(np.array([0,0,1])).flatten()[[0, 2]]
    bvh_motion2_sub = bvh_motion2_sub.translation_and_rotation(0, target_translation_xz, target_facing_direction_xz)
    bvh_motion2_sub.joint_position = bvh_motion2_sub.joint_position[:mix_frame2]
    bvh_motion2_sub.joint_rotation = bvh_motion2_sub.joint_rotation[:mix_frame2]
    bvh_motion2.append(bvh_motion2_sub)

    target_translation_xz_1 = res.joint_position[mix_frame1, 0, [0, 2]]
    target_facing_direction_xz_1 = R.from_quat(res.joint_rotation[mix_frame1, 0]).apply(np.array([0,0,1])).flatten()[[0, 2]]
    bvh_motion2 = bvh_motion2.translation_and_rotation(mix_frame2, target_translation_xz_1, target_facing_direction_xz_1)

    cur_frame_time1 = mix_frame1
    cur_frame_time2 = mix_frame2
    for i in range(mix_time):
        res.joint_position[cur_frame_time1], res.joint_rotation[cur_frame_time1] = Interpolation(
            res.joint_position[cur_frame_time1],
            bvh_motion2.joint_position[cur_frame_time2],
            res.joint_rotation[cur_frame_time1],
            bvh_motion2.joint_rotation[cur_frame_time2],
            (i+0.) / mix_time)
        cur_frame_time1 += 1
        cur_frame_time2 += 1
   
    res.joint_position = np.concatenate([res.joint_position[:cur_frame_time1], bvh_motion2.joint_position[cur_frame_time2:]], axis=0)
    res.joint_rotation = np.concatenate([res.joint_rotation[:cur_frame_time1], bvh_motion2.joint_rotation[cur_frame_time2:]], axis=0)

    return res

def concatenate_two_motions_Inertailization(bvh_motion1, bvh_motion2, mix_frame1, mix_time):
    '''
    将两个bvh动作平滑地连接起来，mix_time表示用于混合的帧数
    混合开始时间是第一个动作的第mix_frame1帧
    虽然某些混合方法可能不需要mix_time，但是为了保证接口一致，我们还是保留这个参数
    Tips:
        你可能需要用到BVHMotion.sub_sequence 和 BVHMotion.append
    '''
    res = bvh_motion1.raw_copy()
    # TODO: 你的代码
    # 下面这种直接拼肯定是不行的(
    # res.joint_position = np.concatenate([res.joint_position[:mix_frame1], bvh_motion2.joint_position], axis=0)
    # res.joint_rotation = np.concatenate([res.joint_rotation[:mix_frame1], bvh_motion2.joint_rotation], axis=0)

    bvh_motion2 = build_loop_motion(bvh_motion2)
    mix_frame2 = nearest_frame(bvh_motion2, res.joint_rotation[mix_frame1])
    bvh_motion2_sub = bvh_motion2.raw_copy()
    target_translation_xz = bvh_motion2_sub.joint_position[-1, 0, [0, 2]]
    target_facing_direction_xz = R.from_quat(bvh_motion2.joint_rotation[-1, 0]).apply(np.array([0,0,1])).flatten()[[0, 2]]
    bvh_motion2_sub = bvh_motion2_sub.translation_and_rotation(0, target_translation_xz, target_facing_direction_xz)
    bvh_motion2_sub.joint_position = bvh_motion2_sub.joint_position[:mix_frame2]
    bvh_motion2_sub.joint_rotation = bvh_motion2_sub.joint_rotation[:mix_frame2]
    bvh_motion2.append(bvh_motion2_sub)

    target_translation_xz_1 = res.joint_position[mix_frame1, 0, [0, 2]]
    target_facing_direction_xz_1 = R.from_quat(res.joint_rotation[mix_frame1, 0]).apply(np.array([0,0,1])).flatten()[[0, 2]]
    bvh_motion2 = bvh_motion2.translation_and_rotation(mix_frame2, target_translation_xz_1, target_facing_direction_xz_1)

    cur_frame_time1 = mix_frame1
    cur_frame_time2 = mix_frame2
    offset_Position = bvh_motion2.joint_position[mix_frame2] - bvh_motion1.joint_position[mix_frame1]
    test1 = R.from_quat(bvh_motion2.joint_rotation[mix_frame2]).as_euler('xyz') 
    test2 = R.from_quat(bvh_motion1.joint_rotation[mix_frame1]).as_euler('xyz')
    offset_Rotation = test1 - test2

    for i in range(mix_time):
        res.joint_position[cur_frame_time1], res.joint_rotation[cur_frame_time1] = Inertailization(
        bvh_motion2.joint_position[cur_frame_time2],
        offset_Position,
        bvh_motion2.joint_rotation[cur_frame_time2],
        offset_Rotation,
        (mix_time - i + 0.) / mix_time)

        cur_frame_time1 += 1
        cur_frame_time2 += 1

    for i in range(len(res.joint_rotation[cur_frame_time1])):
        print(res.joint_position[cur_frame_time1][i] - bvh_motion1.joint_position[cur_frame_time1][i])
    
    res.joint_position = np.concatenate([res.joint_position[:cur_frame_time1], bvh_motion2.joint_position[cur_frame_time2:]], axis=0)
    res.joint_rotation = np.concatenate([res.joint_rotation[:cur_frame_time1], bvh_motion2.joint_rotation[cur_frame_time2:]], axis=0)

    return res

def Inertailization(position1, offset_position, rotation1, offset_rotation, lerp):
    position = np.empty_like(position1)
    rotation = np.empty_like(rotation1)
    
    position = position1 - offset_position * lerp
    rotation = np.array([Inertailization_single_rotation(q1, q2, lerp) for q1, q2 in zip(rotation1, offset_rotation)])

    return position, rotation

def Inertailization_single_rotation(q1, q2, alpha):
    e1 = R.from_quat(q1).as_euler('xyz')
    e2 = q2
    # e1 = normalize_euler_angles(e1)
    # e2 = normalize_euler_angles(e2)
    e = e1 - e2 * alpha
    ##print(alpha)
    return R.from_euler('xyz', e).as_quat()

def normalize_euler_angles(euler_angles):
    # 将欧拉角规范化到 [-pi, pi] 范围内
    return (euler_angles + np.pi) % (2 * np.pi) + np.pi


