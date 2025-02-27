# 以下部分均为可更改部分
'''
references:
https://www.theorangeduck.com/page/code-vs-data-driven-displacement
https://github.com/orangeduck/Motion-Matching
https://www.gdcvault.com/play/1023280/Motion-Matching-and-The-Road
https://www.bilibili.com/video/BV1GK4y1S7Zw
Holden D, Kanoun O, Perepichka M, et al. Learned motion matching
'''

from scipy.spatial import KDTree
from answer_task1 import *
from smooth_utils import *
   
class Inertializer():
    @staticmethod
    def compute_velocity(joint_translation, joint_orientation, dt):
        diff_vel = (joint_translation[1:] - joint_translation[:-1]) / dt
        diff_vel = np.insert(diff_vel, 0, np.zeros_like(diff_vel[0]), axis=0)
        for i in range(len(joint_translation)):
            cur_orientation = R.from_quat(joint_orientation[i, 0, :])
            # except root joint
            diff_vel[i, 1:, :] = cur_orientation.inv().apply(diff_vel[i, 1:, :])
        return diff_vel


    @staticmethod
    def compute_angular_velocity(joint_orientation, dt):
        diff_avel = quat_to_avel(joint_orientation, dt)
        diff_avel = np.insert(diff_avel, 0, np.zeros_like(diff_avel[0]), axis=0)
        return diff_avel

    @staticmethod
    def inertialize_transition_pos(
        off_x,
        off_v,
        src_x,
        src_v,
        dst_x,
        dst_v
    ):
        off_x += src_x - dst_x
        off_v += src_v - dst_v
        return off_x, off_v

    @staticmethod
    def inertialize_transition_quat(
        off_x,
        off_v,
        src_x,
        src_v,
        dst_x,
        dst_v
    ):
        # why (off_x * src_x * dst_x.inv) but (src_x * dst_x.inv * off_x)
        off_x = (R.from_quat(off_x) * R.from_quat(src_x) * R.from_quat(dst_x).inv()).as_quat()
        off_v += src_v - dst_v
        return off_x, off_v

    @staticmethod
    def inertialize_update_pos(
        out_x,
        out_v,
        off_x,
        off_v,
        in_x,
        in_v,
        half_life,
        dt
    ):
        off_x, off_v = decay_spring_implicit_damping_pos(off_x, off_v, half_life, dt)
        out_x = in_x + off_x
        out_v = in_v + off_v
        return out_x, out_v, off_x, off_v
        # return in_x, in_v, [0,0,0], [0,0,0]


    @staticmethod
    def inertialize_update_quat(
        out_x,
        out_v,
        off_x,
        off_v,
        in_x,
        in_v,
        half_life,
        dt
    ):
        off_x, off_v = decay_spring_implicit_damping_rot(R.from_quat(off_x).as_rotvec(), off_v, half_life, dt)
        off_x = R.from_rotvec(off_x).as_quat()

        out_x = (R.from_quat(off_x) * R.from_quat(in_x)).as_quat()
        out_v = R.from_quat(off_x).apply(in_v) + off_v
        return out_x, out_v, off_x, off_v
        # return in_x, in_v, [0,0,0,1], [0,0,0]

        

    @staticmethod
    def inertialize_pose_transition(
        bone_offset_positions,
        bone_offset_vels,
        bone_offset_rotations,
        bone_offset_avels,
        # set src and dst for inertialize
        transition_src_position,
        transition_src_rotation,
        transition_dst_position,
        transition_dst_rotation,
        # cur root state
        root_translation,
        root_vel,
        root_rotation,
        root_avel,
        # cur frame
        bone_src_positions,
        bone_src_vels,
        bone_src_rotations,
        bone_src_avels,
        # best frame
        bone_dst_positions,
        bone_dst_vels,
        bone_dst_rotations,
        bone_dst_avels,
    ):
        # set src and dst
        transition_dst_position[:] = root_translation
        transition_dst_rotation[:] = root_rotation
        transition_src_position[:] = bone_dst_positions[0]
        transition_src_rotation[:] = bone_dst_rotations[0]

        # R.from_quat(bone_dst_rotations[0]).inv().apply(bone_dst_vels[0])): 是局部向量在骨骼原始姿势坐标中的表达（因为这里是根节点的速度，所以等价于根节点空间）
        #         world_dst_velocity = R.from_quat(root_rotation).apply(R.from_quat(bone_dst_rotations[0]).inv().apply(bone_dst_vels[0]))： 局部向量在世界坐标下的表达（因为是根节点的速度，就只算了根节点相对于世界空间的偏移）
        world_dst_velocity = R.from_quat(root_rotation).apply(\
            R.from_quat(bone_dst_rotations[0]).inv().apply(bone_dst_vels[0]))
        world_dst_avelocity = R.from_quat(root_rotation).apply(\
            R.from_quat(bone_dst_rotations[0]).inv().apply(bone_dst_avels[0]))      

        bone_offset_positions[0], bone_offset_vels[0] = Inertializer.inertialize_transition_pos(
            bone_offset_positions[0],
            bone_offset_vels[0],
            root_translation,
            root_vel,
            root_translation,
            world_dst_velocity,
        )

        bone_offset_rotations[0], bone_offset_avels[0] = Inertializer.inertialize_transition_quat(
            bone_offset_rotations[0],
            bone_offset_avels[0],
            root_rotation,
            root_avel,
            root_rotation,
            world_dst_avelocity
        )

        for i in range(1, len(bone_offset_positions)):
            bone_offset_positions[i], bone_offset_vels[i] = Inertializer.inertialize_transition_pos(
                bone_offset_positions[i],
                bone_offset_vels[i],
                bone_src_positions[i],
                bone_src_vels[i],
                bone_dst_positions[i],
                bone_dst_vels[i]
            )
            bone_offset_rotations[i], bone_offset_avels[i] = Inertializer.inertialize_transition_quat(
                bone_offset_rotations[i],
                bone_offset_avels[i],
                bone_src_rotations[i],
                bone_src_avels[i],
                bone_dst_rotations[i],
                bone_dst_avels[i]
            )
        pass


    @staticmethod
    def inertialize_pose_update(
        bone_positions,
        bone_vels,
        bone_rotations,
        bone_avels,
        offset_positions,
        offset_vels,
        offset_rotations,
        offset_avels,
        # next frame
        bone_input_positions,
        bone_input_vels,
        bone_input_rotations,
        bone_input_avels,            
        # pred src frame and dst frame
        transition_src_position,
        transition_src_rotation,
        transition_dst_position,
        transition_dst_rotation,
        # inertialize args
        halflife,
        dt
    ):
        #R.from_quat(transition_src_rotation).inv().apply(bone_input_positions[0] - transition_src_position)): 表示局部向量在
        world_position = R.from_quat(transition_dst_rotation).apply(\
            R.from_quat(transition_src_rotation).inv().apply(bone_input_positions[0] - transition_src_position))\
            + transition_dst_position
        world_velocity = R.from_quat(transition_dst_rotation).apply(\
            R.from_quat(transition_src_rotation).inv().apply(bone_input_vels[0]))
        world_rotation = (R.from_quat(transition_dst_rotation) * R.from_quat(transition_src_rotation).inv() * R.from_quat(bone_input_rotations[0])).as_quat()
        world_avelocity = R.from_quat(transition_dst_rotation).apply(\
            R.from_quat(transition_src_rotation).inv().apply(bone_input_avels[0]))
        
        # trick
        world_position[1] = bone_input_positions[0][1]
        world_rotation, _ = BVHMotion.decompose_rotation_with_yaxis(world_rotation)

        # inertialize root pos and rot
        bone_positions[0], bone_vels[0], offset_positions[0], offset_vels[0] = Inertializer.inertialize_update_pos(
            bone_positions[0], 
            bone_vels[0], 
            offset_positions[0], 
            offset_vels[0],
            # target
            world_position,
            world_velocity,
            halflife,
            dt
        )


        bone_rotations[0], bone_avels[0], offset_rotations[0], offset_avels[0] = Inertializer.inertialize_update_quat(
            bone_rotations[0], 
            bone_avels[0], 
            offset_rotations[0], 
            offset_avels[0],
            # target
            world_rotation,
            world_avelocity,
            halflife,
            dt
        )

        for i in range(1, len(bone_positions)):
            bone_positions[i], bone_vels[i], offset_positions[i], offset_vels[i] = Inertializer.inertialize_update_pos(
                bone_positions[i], 
                bone_vels[i], 
                offset_positions[i], 
                offset_vels[i],
                # target
                bone_input_positions[i],
                bone_input_vels[i],
                halflife,
                dt
            )
            
            bone_rotations[i], bone_avels[i], offset_rotations[i], offset_avels[i] = Inertializer.inertialize_update_quat(
                bone_rotations[i], 
                bone_avels[i], 
                offset_rotations[i], 
                offset_avels[i],
                # target
                bone_input_rotations[i],
                bone_input_avels[i],
                halflife,
                dt
            )            

        pass


class CharacterController():
    def __init__(self, controller) -> None:
        self.motions = []
        self.motions.append(BVHMotion('motion_material/kinematic_motion/long_walk.bvh'))
        # self.motions.append(BVHMotion('motion_material/kinematic_motion/long_run.bvh'))
        self.motion = self.motions[0]
        self.controller = controller
        self.cur_frame = 1
        self.deltaTime = 1. / 60.

        self.counter = 2
        self.cur_count = self.counter
        self.first_frame = True
        

        # idle
        self.idle_motion = BVHMotion('motion_material/idle.bvh')
        self.idle = True

        # dataset
        self.poseData = self.CreatePoseDatasets()
        self.featureData = self.CreateFeatureDatasets()
        
        self.feature_weight = np.ones(self.featureData[0].shape)
        self.features_offset, self.features_scale = self.normalize_features()
        self.feature_kd_tree = KDTree(self.featureData[:])


        # inertialize args
        self.inertailze_blending_halflife = 0.1

        # 当前pose
        self.bone_positions = self.poseData['joint_position'][self.cur_frame].copy()
        self.bone_rotations = self.poseData['joint_rotation'][self.cur_frame].copy()
        self.bone_vels = self.poseData['joint_velocity'][self.cur_frame].copy()
        self.bone_avels = self.poseData['joint_avelocity'][self.cur_frame].copy()

        # cur_frame
        self.cur_bone_positions = self.poseData['joint_position'][self.cur_frame].copy()
        self.cur_bone_rotations = self.poseData['joint_rotation'][self.cur_frame].copy()
        self.cur_bone_vels = self.poseData['joint_velocity'][self.cur_frame].copy()
        self.cur_bone_avels = self.poseData['joint_avelocity'][self.cur_frame].copy()

        # next_frame
        self.trans_bone_positions = self.poseData['joint_position'][self.cur_frame].copy()
        self.trans_bone_rotations = self.poseData['joint_rotation'][self.cur_frame].copy()
        self.trans_bone_vels = self.poseData['joint_velocity'][self.cur_frame].copy()
        self.trans_bone_avels = self.poseData['joint_avelocity'][self.cur_frame].copy()


        # offset
        self.offset_positions = np.zeros(self.poseData['joint_position'][0].shape)
        self.offset_rotations = np.zeros(self.poseData['joint_rotation'][0].shape)
        self.offset_rotations[:,3] = 1.
        self.offset_vels = np.zeros(self.poseData['joint_velocity'][0].shape)
        self.offset_avels = np.zeros(self.poseData['joint_avelocity'][0].shape)

        # transition tmp var
        self.transition_src_position = np.zeros(3)
        self.transition_src_rotation = np.array([0.,0.,0.,1.])
        self.transition_dst_position = np.zeros(3)
        self.transition_dst_rotation = np.array([0.,0.,0.,1.])

        # adjust pose



        # final pose
        self.global_bone_positions = np.zeros(self.poseData['joint_position'][0].shape)
        self.global_bone_rotations = np.zeros(self.poseData['joint_rotation'][0].shape)
        self.global_bone_rotations[:,3] = 1.
        # self.global_vels = np.zeros(self.poseData['joint_velocity'][0].shape)
        # self.global_avels = np.zeros(self.poseData['joint_avelocity'][0].shape)

        self.initialize_pose()
        pass


    #-----------------------------------------------------------------------------------------------
    # datset functions

    # 标准化(Normalization):是通过特征的平均值和标准差，将特征缩放成一个标准的正态分布，缩放后均值为0，方差为1。但即使数据不服从正态分布，也可以用此法。特别适用于数据的最大值和最小值未知，或存在孤立点。 https://ssjcoding.github.io/2019/03/27/normalization-and-standardization/
    def normalize_features(self):
        # compute mean and std
        features_offset = np.mean(self.featureData, axis=0)
        features_scale = np.std(self.featureData, axis=0)

        # weight
        features_scale = features_scale / self.feature_weight

        # normalize features
        self.featureData = (self.featureData - features_offset) / features_scale

        return features_offset, features_scale
    

    def CreateFeatureDatasets(self):
        features = []
        for i in range(len(self.motions)):
            motion = self.motions[i]
            temp_motion_features = np.zeros([motion.motion_length, 27])
            joint_name = motion.joint_name
            joint_translation, joint_orientation = motion.batch_forward_kinematics()
            # 每段Motion的最后一部分因为预期位移为0，相当于废弃的数据？
            for j in range(motion.motion_length):
                # 用于左乘后得到，世界坐标下的vector在第0帧的root空间坐标下的表达。
                cur_orientation_inv = R.from_quat(joint_orientation[j, 0, :]).inv()
                # cur_orientation_inv.apply(joint_translation[i + 20, 0, :] - joint_translation[i, 0, :]): 计算二者位移（向量）在root的local空间下的表达。
                if j < motion.motion_length - 60:
                    temp_motion_features[j, 0:2] = cur_orientation_inv.apply(joint_translation[j + 20, 0, :] - joint_translation[j, 0, :])[[0, 2]]
                    temp_motion_features[j, 2:4] = cur_orientation_inv.apply(joint_translation[j + 40, 0, :] - joint_translation[j, 0, :])[[0, 2]]
                    temp_motion_features[j, 4:6] = cur_orientation_inv.apply(joint_translation[j + 60, 0, :] - joint_translation[j, 0, :])[[0, 2]]
                    # joint_orientation[i + 20, 0, :]).apply(np.array([0.,0.,1.]))： 20帧后的root坐标中Z轴在世界坐标的表达。
                    # cur_orientation_inv.apply(R.from_quat(joint_orientation[i + 20, 0, :]).apply(np.array([0.,0.,1.])))： 20帧后的root坐标中Z轴在0帧时的root坐标中的表达。
                    temp_motion_features[j, 6:  8] = cur_orientation_inv.apply(R.from_quat(joint_orientation[j + 20, 0, :]).apply(np.array([0.,0.,1.])))[[0,2]]
                    temp_motion_features[j, 8: 10] = cur_orientation_inv.apply(R.from_quat(joint_orientation[j + 40, 0, :]).apply(np.array([0.,0.,1.])))[[0,2]]
                    temp_motion_features[j, 10:12] = cur_orientation_inv.apply(R.from_quat(joint_orientation[j + 60, 0, :]).apply(np.array([0.,0.,1.])))[[0,2]]

                temp_motion_features[j, 12:15] = cur_orientation_inv.apply(joint_translation[j, joint_name.index('rAnkle'), :] - joint_translation[j, 0, :])
                temp_motion_features[j, 15:18] = cur_orientation_inv.apply(joint_translation[j, joint_name.index('lAnkle'), :] - joint_translation[j, 0, :])

                if j != 0:
                    dt = self.deltaTime
                    temp_motion_features[j, 18:21] = cur_orientation_inv.apply((joint_translation[j, joint_name.index('RootJoint'), :] - joint_translation[j - 1, joint_name.index('RootJoint'), :]) / dt)
                    temp_motion_features[j, 21:24] = cur_orientation_inv.apply((joint_translation[j, joint_name.index('rAnkle'), :] - joint_translation[j - 1, joint_name.index('rAnkle'), :]) / dt) - temp_motion_features[j, 18:21]
                    temp_motion_features[j, 24:27] = cur_orientation_inv.apply((joint_translation[j, joint_name.index('lAnkle'), :] - joint_translation[j - 1, joint_name.index('lAnkle'), :]) / dt) - temp_motion_features[j, 18:21]
            features.append(temp_motion_features)

        features = np.concatenate(features, axis=0)
        return features
    

    def get_simulation_bone_pos(self):
        simulation_pos = self.motion.joint_position[:,0,:].copy()
        simulation_pos[:, 1] = 0
        return simulation_pos
    

    def get_simulation_bone_rot(self):
        pass


    def CreatePoseDatasets(self):
        joint_positions = []
        joint_rotations = []
        joint_velocities = []
        joint_avelocities = []
        for i in range(len(self.motions)):
            motion = self.motions[i]
            joint_translation, joint_orientation = motion.batch_forward_kinematics()
            joint_positions.append(motion.joint_position)
            joint_rotations.append(motion.joint_rotation)
            joint_velocities.append(Inertializer.compute_velocity(joint_translation, joint_orientation, self.deltaTime))
            joint_avelocities.append(Inertializer.compute_angular_velocity(motion.joint_rotation, self.deltaTime))

        joint_positions = np.concatenate(joint_positions, axis=0)
        joint_rotations = np.concatenate(joint_rotations, axis=0)
        joint_velocities = np.concatenate(joint_velocities, axis=0)
        joint_avelocities = np.concatenate(joint_avelocities, axis=0)

        features = {
                'joint_position': joint_positions,
                'joint_rotation': joint_rotations,
                'joint_velocity': joint_velocities,
                'joint_avelocity': joint_avelocities,
            }
        return features


    def query(self, cur_feature):
        pass



    def search_best_frame(self, cur_feature):
        pass


    def initialize_pose(self):
        self.bone_positions[0][0] = 0.
        self.bone_positions[0][2] = 0.
        self.bone_rotations[0] = np.array([0,0,0,1.])


    #-----------------------------------------------------------------------------------------------
    # tick functions


    def update_state(self, 
                     desired_pos_list, 
                     desired_rot_list,
                     desired_vel_list,
                     desired_avel_list,
                     current_gait,
                     ):
        '''
        此接口会被用于获取新的期望状态
        Input: 平滑过的手柄输入,包含了现在(第0帧)和未来20,40,60,80,100帧的期望状态,以及一个额外输入的步态
        简单起见你可以先忽略步态输入,它是用来控制走路还是跑步的
            desired_pos_list: 期望位置, 6x3的矩阵, 每一行对应0，20，40...帧的期望位置(水平)， 期望位置可以用来拟合根节点位置也可以是质心位置或其他
            desired_rot_list: 期望旋转, 6x4的矩阵, 每一行对应0，20，40...帧的期望旋转(水平), 期望旋转可以用来拟合根节点旋转也可以是其他
            desired_vel_list: 期望速度, 6x3的矩阵, 每一行对应0，20，40...帧的期望速度(水平), 期望速度可以用来拟合根节点速度也可以是其他
            desired_avel_list: 期望角速度, 6x3的矩阵, 每一行对应0，20，40...帧的期望角速度(水平), 期望角速度可以用来拟合根节点角速度也可以是其他
        
        Output: 同作业一,输出下一帧的关节名字,关节位置,关节旋转
            joint_name: List[str], 代表了所有关节的名字
            joint_translation: np.ndarray，形状为(M, 3)的numpy数组，包含着所有关节的全局位置
            joint_orientation: np.ndarray，形状为(M, 4)的numpy数组，包含着所有关节的全局旋转(四元数)
        Tips:
            输出三者顺序需要对应
            controller 本身有一个move_speed属性,是形状(3,)的ndarray,
            分别对应着面朝向移动速度,侧向移动速度和向后移动速度.目前根据LAFAN的统计数据设为(1.75,1.5,1.25)
            如果和你的角色动作速度对不上,你可以在init或这里对属性进行修改
        '''
        
        if self.first_frame:
            self.first_frame = False
            self.bone_positions[0][0] = desired_pos_list[0][0]
            self.bone_positions[0][2] = desired_pos_list[0][2]
            self.transition_dst_position[:] = self.bone_positions[0]
            self.transition_dst_rotation[:] = self.bone_rotations[0]
            self.transition_src_position[:] = self.poseData['joint_position'][self.cur_frame][0]
            self.transition_src_rotation[:] = self.poseData['joint_rotation'][self.cur_frame][0]

        # 计算当前的feature vector
        cur_feature_vector = np.zeros([27])
        root_orientation = self.bone_rotations[0]
        root_translation = self.bone_positions[0]

        # print(desired_pos_list)
        # print(desired_rot_list)
        # 计算trajectory
            ## 用于左乘后得到，世界坐标下的vector在当前帧的root空间坐标下的表达。
        root_orientation_inv = R.from_quat(root_orientation).inv()
            ## root空间的预期位移
        cur_feature_vector[0:2] = root_orientation_inv.apply(desired_pos_list[1] - root_translation)[[0, 2]]
        cur_feature_vector[2:4] = root_orientation_inv.apply(desired_pos_list[2] - root_translation)[[0, 2]]
        cur_feature_vector[4:6] = root_orientation_inv.apply(desired_pos_list[3] - root_translation)[[0, 2]]
            ## root空间的预期旋转
        cur_feature_vector[6:  8] = root_orientation_inv.apply(R.from_quat(desired_rot_list[1]).apply(np.array([0.,0.,1.])))[[0,2]]
        cur_feature_vector[8: 10] = root_orientation_inv.apply(R.from_quat(desired_rot_list[2]).apply(np.array([0.,0.,1.])))[[0,2]]
        cur_feature_vector[10:12] = root_orientation_inv.apply(R.from_quat(desired_rot_list[3]).apply(np.array([0.,0.,1.])))[[0,2]]
        # normalize tarjectory feature
        cur_feature_vector[:12] = np.divide(cur_feature_vector[:12] - self.features_offset[:12], self.features_scale[:12])

        # mocap feature
        # 12~17： 左右脚踝与根节点的位移差在root空间坐标下的表达
        # 18~26： root，左右脚踝的在root空间坐标下的表达
        cur_feature_vector[12:27] = self.featureData[self.cur_frame][12:27]

        # search best match next frame
        best_cost, best_frame = self.feature_kd_tree.query(cur_feature_vector)            

        # 每两帧找一次？
        search = True
        self.cur_count -= 1
        if self.cur_count == 0: 
            self.cur_count = self.counter
        else:
            search = False

        print(best_frame)

        # swtich to idle
        if np.linalg.norm(desired_vel_list[0]) < 0.01:
            pass

        # inertialize
        if best_frame != self.cur_frame and search:
            self.trans_bone_positions[:] = self.poseData['joint_position'][best_frame]
            self.trans_bone_rotations[:] = self.poseData['joint_rotation'][best_frame]
            self.trans_bone_vels[:] = self.poseData['joint_velocity'][best_frame]
            self.trans_bone_avels[:] = self.poseData['joint_avelocity'][best_frame]
            # update offset
            Inertializer.inertialize_pose_transition(
                self.offset_positions,
                self.offset_vels,
                self.offset_rotations,
                self.offset_avels,
                # set src and dst for inertialize
                self.transition_src_position,
                self.transition_src_rotation,
                self.transition_dst_position,
                self.transition_dst_rotation,
                # cur root state
                self.bone_positions[0],
                self.bone_vels[0],
                self.bone_rotations[0],
                self.bone_avels[0],
                # cur frame
                self.cur_bone_positions,
                self.cur_bone_vels,
                self.cur_bone_rotations,
                self.cur_bone_avels,
                # best frame
                self.trans_bone_positions,
                self.trans_bone_vels,
                self.trans_bone_rotations,
                self.trans_bone_avels,
            )
            self.cur_frame = best_frame

        # tick frame
        self.cur_frame += 1
        # print(self.cur_frame)
        # get next pose
        self.cur_bone_positions[:] = self.poseData['joint_position'][self.cur_frame]
        self.cur_bone_rotations[:] = self.poseData['joint_rotation'][self.cur_frame]
        self.cur_bone_vels[:] = self.poseData['joint_velocity'][self.cur_frame]
        self.cur_bone_avels[:] = self.poseData['joint_avelocity'][self.cur_frame]

        # update pose with inerializer
        Inertializer.inertialize_pose_update(
            self.bone_positions,
            self.bone_vels,
            self.bone_rotations,
            self.bone_avels,
            self.offset_positions,
            self.offset_vels,
            self.offset_rotations,
            self.offset_avels,
            # next frame
            self.cur_bone_positions,
            self.cur_bone_vels,
            self.cur_bone_rotations,
            self.cur_bone_avels,            
            # cur src frame and dst frame
            self.transition_src_position,
            self.transition_src_rotation,
            self.transition_dst_position,
            self.transition_dst_rotation,
            # inertialize args
            self.inertailze_blending_halflife,
            self.deltaTime
        )


        # self.bone_positions = self.motion.joint_position[self.cur_frame]
        # self.bone_rotations = self.motion.joint_rotation[self.cur_frame]


    
    
    def sync_controller_and_character(self, controller):
        '''
        这一部分用于同步你的角色和手柄的状态
        更新后很有可能会出现手柄和角色的位置不一致，这里可以用于修正
        让手柄位置服从你的角色? 让角色位置服从手柄? 或者插值折中一下?
        需要你进行取舍
        Input: 手柄对象，角色状态
        手柄对象我们提供了set_pos和set_rot接口,输入分别是3维向量和四元数,会提取水平分量来设置手柄的位置和旋转
        角色状态实际上是一个tuple, (joint_name, joint_translation, joint_orientation),为你在update_state中返回的三个值
        你可以更新他们,并返回一个新的角色状态
        '''
        
        # simulation object跟随角色
        # controller.set_pos(np.array([self.bone_positions[0][0], 0., self.bone_positions[0][2]]))
        # controller.set_rot(self.bone_rotations[0])
        
        # 角色跟随simulation object
        # self.bone_positions[0][0] = controller.position[0]
        # self.bone_positions[0][2] = controller.position[2]
        # self.bone_rotations[0] = controller.rotation
        


    def full_forward_kinematics(self):
        joint_parent = self.motion.joint_parent

        self.global_bone_positions[0] = self.bone_positions[0]
        self.global_bone_rotations[0] = self.bone_rotations[0]
        for i in range(1, len(joint_parent)):
            pi = joint_parent[i]
            self.global_bone_positions[i] = self.global_bone_positions[pi] + \
                R.from_quat(self.global_bone_rotations[pi]).apply(self.bone_positions[i])
            self.global_bone_rotations[i] = (R.from_quat(self.global_bone_rotations[pi]) * R.from_quat(self.bone_rotations[i])).as_quat()

        return (self.motion.joint_name, self.global_bone_positions, self.global_bone_rotations)