#include "glm/glm.hpp"

#include <embree4/rtcore.h>
#include <embree4/rtcore_builder.h>
#include <embree4/rtcore_geometry.h>
#include <iostream>
#include <vector>

void errorCallback(void* userPtr, RTCError code, const char* str) {
    if (code != RTC_ERROR_NONE) {
        std::cerr << "Embree Error: " << str << std::endl;
    }
}

typedef struct {
    glm::vec3 lower;
    glm::vec3 upper;
} AABB;

inline AABB AABB_union(const AABB& a, const AABB& b) {
	AABB u;
	u.lower = glm::min(a.lower, b.lower);
	u.upper = glm::max(a.upper, b.upper);
	return u;
}
inline glm::vec3 AABB_center(const AABB& aabb) {
	return glm::mix(aabb.lower, aabb.upper, 0.5f);
}
inline float AABB_center(const AABB& aabb, int axis) {
	return glm::mix(aabb.lower[axis], aabb.upper[axis], 0.5f);
}

inline void EmbreeErorrHandler(void* userPtr, RTCError code, const char* str) {
	printf("Embree Error [%d] %s\n", code, str);
}

class BVHBranch;
class BVHLeaf;

class BVHNode {
public:
	virtual ~BVHNode() {}

	virtual BVHBranch* branch() { return nullptr; }
	virtual BVHLeaf* leaf() { return nullptr; }

	BVHBranch* parent = nullptr;
	uint32_t index_for_array_storage = 0;
};

class BVHBranch : public BVHNode {
public:
	BVHBranch* branch() { return this; }
	BVHNode* L = nullptr;
	BVHNode* R = nullptr;
	AABB L_bounds;
	AABB R_bounds;
};
class BVHLeaf : public BVHNode {
public:
	BVHLeaf* leaf() { return this; }
	uint32_t primitive_ids[5];
	uint32_t primitive_count = 0;
};

static void* create_branch(RTCThreadLocalAllocator alloc, unsigned int numChildren, void* userPtr)
{
	//RT_ASSERT(numChildren == 2);
	void* ptr = rtcThreadLocalAlloc(alloc, sizeof(BVHBranch), 16);

	// direct "BVHBranch *" to "void *" cast maybe cause undefined behavior when "void *" to "BVHNode *"
	// so "BVHBranch *" to "BVHNode *"
	BVHNode* node = new (ptr) BVHBranch;
	return (void*)node;
}
static void set_children_to_branch(void* nodePtr, void** childPtr, unsigned int numChildren, void* userPtr)
{
	//RT_ASSERT(numChildren == 2);
	BVHBranch* node = static_cast<BVHBranch*>((BVHNode*)nodePtr);
	node->L = static_cast<BVHNode*>(childPtr[0]);
	node->R = static_cast<BVHNode*>(childPtr[1]);
	node->L->parent = node;
	node->R->parent = node;
}
static void set_branch_bounds(void* nodePtr, const RTCBounds** bounds, unsigned int numChildren, void* userPtr)
{
	//RT_ASSERT(numChildren == 2);
	BVHBranch* node = static_cast<BVHBranch*>((BVHNode*)nodePtr);
	node->L_bounds = *(const AABB*)bounds[0];
	node->R_bounds = *(const AABB*)bounds[1];
}
static void* create_leaf(RTCThreadLocalAllocator alloc, const RTCBuildPrimitive* prims, size_t numPrims, void* userPtr)
{
	//RT_ASSERT(numPrims <= 5);
	void* ptr = rtcThreadLocalAlloc(alloc, sizeof(BVHLeaf), 16);
	BVHLeaf* l = new (ptr) BVHLeaf();
	l->primitive_count = numPrims;
	for (int i = 0; i < numPrims; ++i) {
		l->primitive_ids[i] = prims[i].primID;
	}
	return ptr;
}

void print_bvh(RTCScene scene)
{
    
    //BVH4* bvh4 = nullptr;

    ///* if the scene contains only triangles, the BVH4 acceleration structure can be obtained this way */
    //AccelData* accel = ((Accel*)scene)->intersectors.ptr;
    //if (accel->type == AccelData::TY_BVH4)
    //    bvh4 = (BVH4*)accel;

    ///* if there are also other geometry types, one has to iterate over the toplevel AccelN structure */
    //else if (accel->type == AccelData::TY_ACCELN)
    //{
    //    AccelN* accelN = (AccelN*)(accel);
    //    for (size_t i = 0; i < accelN->accels.size(); i++) {
    //        if (accelN->accels[i]->intersectors.ptr->type == AccelData::TY_BVH4) {
    //            bvh4 = (BVH4*)accelN->accels[i]->intersectors.ptr;
    //            if (std::string(bvh4->primTy->name()) == "triangle4v") break;
    //            bvh4 = nullptr;
    //        }
    //    }
    //}
    //if (bvh4 == nullptr)
    //    throw std::runtime_error("cannot access BVH4 acceleration structure"); // will not happen if you use this Embree version

    ///* now lets print the entire hierarchy */
    //print_bvh4_triangle4v(bvh4->root, 0);
}

//void print_bvh4_triangle4v(BVH4::NodeRef node, size_t depth)
//{
//    if (node.isAABBNode())
//    {
//        BVH4::AABBNode* n = node.getAABBNode();
//
//        std::cout << "AABBNode {" << std::endl;
//        for (size_t i = 0; i < 4; i++)
//        {
//            for (size_t k = 0; k < depth; k++) std::cout << "  ";
//            std::cout << "  bounds" << i << " = " << n->bounds(i) << std::endl;
//        }
//
//        for (size_t i = 0; i < 4; i++)
//        {
//            if (n->child(i) == BVH4::emptyNode)
//                continue;
//
//            for (size_t k = 0; k < depth; k++) std::cout << "  ";
//            std::cout << "  child" << i << " = ";
//            print_bvh4_triangle4v(n->child(i), depth + 1);
//        }
//        for (size_t k = 0; k < depth; k++) std::cout << "  ";
//        std::cout << "}" << std::endl;
//    }
//    else
//    {
//        size_t num;
//        const Triangle4v* tri = (const Triangle4v*)node.leaf(num);
//
//        std::cout << "Leaf {" << std::endl;
//        for (size_t i = 0; i < num; i++) {
//            for (size_t j = 0; j < tri[i].size(); j++) {
//                for (size_t k = 0; k < depth; k++) std::cout << "  ";
//                std::cout << "  Triangle { v0 = (" << tri[i].v0.x[j] << ", " << tri[i].v0.y[j] << ", " << tri[i].v0.z[j] << "),  "
//                    "v1 = (" << tri[i].v1.x[j] << ", " << tri[i].v1.y[j] << ", " << tri[i].v1.z[j] << "), "
//                    "v2 = (" << tri[i].v2.x[j] << ", " << tri[i].v2.y[j] << ", " << tri[i].v2.z[j] << "), "
//                    "geomID = " << tri[i].geomID(j) << ", primID = " << tri[i].primID(j) << " }" << std::endl;
//            }
//        }
//        for (size_t k = 0; k < depth; k++) std::cout << "  ";
//        std::cout << "}" << std::endl;
//    }
//}

void print_node(BVHNode* node) 
{
    if (node == nullptr || (node->branch() == nullptr && node->leaf() == nullptr))
        return;

    if (node->branch() != nullptr) 
    {
        BVHBranch* nodeBranch = node->branch();
        std::cout << "nodeBranch index is: " << nodeBranch->index_for_array_storage << std::endl;
        print_node(nodeBranch->L);
        print_node(nodeBranch->R);
    }

    if (node->leaf() != nullptr)
    {
        BVHLeaf* nodeLeaf = node->leaf();
        std::cout << "nodeLeaf index is: " << nodeLeaf->index_for_array_storage << std::endl;
    }
}

int main() {
    // 初始化 Embree 设备
    RTCDevice device = rtcNewDevice(nullptr);
    if (!device) {
        std::cerr << "Error: Unable to create Embree device." << std::endl;
        return -1;
    }
    rtcSetDeviceErrorFunction(device, errorCallback, nullptr);

    //
    RTCBVH bvh = rtcNewBVH(device);

    // 准备自定义几何数据
    std::vector<RTCBuildPrimitive> prims(12);
    prims[0].lower_x = -0.5f;
    prims[0].lower_y = -0.5f;
    prims[0].lower_z = 0.5f;
    prims[0].geomID = 0;
    prims[0].primID = 0;
    prims[0].upper_x = 0.5f;
    prims[0].upper_y = 0.5f;
    prims[0].upper_z = 0.5f;

    prims[1].lower_x = -0.5f;
    prims[1].lower_y = -0.5f;
    prims[1].lower_z = 0.5f;
    prims[1].geomID = 0;
    prims[1].primID = 0;
    prims[1].upper_x = 0.5f;
    prims[1].upper_y = 0.5f;
    prims[1].upper_z = 0.5f;

    prims[2].lower_x = -0.5f;
    prims[2].lower_y = 0.5f;
    prims[2].lower_z = -0.5f;
    prims[2].geomID = 0;
    prims[2].primID = 0;
    prims[2].upper_x = 0.5f;
    prims[2].upper_y = 0.5f;
    prims[2].upper_z = 0.5f;

    prims[3].lower_x = -0.5f; 
    prims[3].lower_y = 0.5f;
    prims[3].lower_z = -0.5f;
    prims[3].geomID = 0;
    prims[3].primID = 0;
    prims[3].upper_x = 0.5f;
    prims[3].upper_y = 0.5f;
    prims[3].upper_z = 0.5f;

    prims[4].lower_x = -0.5f;
    prims[4].lower_y = -0.5f;
    prims[4].lower_z = -0.5f;
    prims[4].geomID = 0;
    prims[4].primID = 0;
    prims[4].upper_x = 0.5f;
    prims[4].upper_y = 0.5f;
    prims[4].upper_z = -0.5f;

    prims[5].lower_x = -0.5f;
    prims[5].lower_y = -0.5f;
    prims[5].lower_z = -0.5f;
    prims[5].geomID = 0;
    prims[5].primID = 0;
    prims[5].upper_x = 0.5f;
    prims[5].upper_y = 0.5f;
    prims[5].upper_z = -0.5f;

    prims[6].lower_x = -0.5f;
    prims[6].lower_y = -0.5f;
    prims[6].lower_z = -0.5f;
    prims[6].geomID = 0;
    prims[6].primID = 0;
    prims[6].upper_x = 0.5f;
    prims[6].upper_y = -0.5f;
    prims[6].upper_z = 0.5f;

    prims[7].lower_x = -0.5f;
    prims[7].lower_y = -0.5f;
    prims[7].lower_z = -0.5f;
    prims[7].geomID = 0;
    prims[7].primID = 0;
    prims[7].upper_x = 0.5f;
    prims[7].upper_y = -0.5f;
    prims[7].upper_z = 0.5f;

    prims[8].lower_x = -0.5f;
    prims[8].lower_y = -0.5f;
    prims[8].lower_z = -0.5f;
    prims[8].geomID = 0;
    prims[8].primID = 0;
    prims[8].upper_x = -0.5f;
    prims[8].upper_y = 0.5f;
    prims[8].upper_z = 0.5f;

    prims[9].lower_x = -0.5f;
    prims[9].lower_y = -0.5f;
    prims[9].lower_z = -0.5f;
    prims[9].geomID = 0;
    prims[9].primID = 0;
    prims[9].upper_x = -0.5f;
    prims[9].upper_y = 0.5f;
    prims[9].upper_z = 0.5f;

    prims[10].lower_x = 0.5f;
    prims[10].lower_y = -0.5f;
    prims[10].lower_z = -0.5f;
    prims[10].geomID = 0;
    prims[10].primID = 0;
    prims[10].upper_x = 0.5f;
    prims[10].upper_y = 0.5f;
    prims[10].upper_z = 0.5f;

    prims[11].lower_x = 0.5f;
    prims[11].lower_y = -0.5f;
    prims[11].lower_z = -0.5f;
    prims[11].geomID = 0;
    prims[11].primID = 0;
    prims[11].upper_x = 0.5f;
    prims[11].upper_y = 0.5f;
    prims[11].upper_z = 0.5f;

    // 配置自定义 BVH 构建参数
    RTCBuildArguments arguments = rtcDefaultBuildArguments();
    arguments.byteSize = sizeof(arguments);
    arguments.buildFlags = RTC_BUILD_FLAG_NONE;
    arguments.buildQuality = RTCBuildQuality::RTC_BUILD_QUALITY_LOW;
    arguments.maxBranchingFactor = 2;
    arguments.maxDepth = 1024;
    arguments.sahBlockSize = 1;
    arguments.minLeafSize = 1;
    arguments.maxLeafSize = 1;
    arguments.traversalCost = 1.0f;
    arguments.intersectionCost = 1.0f;
    arguments.bvh = bvh;
    arguments.primitives = prims.data();
    arguments.primitiveCount = prims.size();
    arguments.primitiveArrayCapacity = prims.capacity();
    arguments.createNode = create_branch;
    arguments.setNodeChildren = set_children_to_branch;
    arguments.setNodeBounds = set_branch_bounds;
    arguments.createLeaf = create_leaf;
    arguments.splitPrimitive = nullptr;
    arguments.userPtr = nullptr;

    // 构建 BLAS
    BVHNode* blasBVH = (BVHNode*) rtcBuildBVH(&arguments);
    if (!blasBVH) {
        std::cerr << "Error: Failed to build BLAS." << std::endl;
        rtcReleaseDevice(device);
        return -1;
    }

    BVHNode* blasBVHTest = blasBVH;
    while (blasBVHTest->branch() != nullptr || blasBVHTest->leaf() != nullptr)
    {
        BVHBranch* nodeBranch;
        BVHLeaf* nodeLeaf;
        if (blasBVHTest->branch() != nullptr)
            nodeBranch = blasBVHTest->branch();
        std::cout << "blasBVHTest branch is: " << blasBVHTest->index_for_array_storage << std::endl;
        blasBVHTest = (BVHBranch*) blasBVHTest->branch();
    }

    // 创建 TLAS
    //RTCScene tlasScene = rtcNewScene(device);

    //float* transformMatrix = new float[16] 
    //    {   1.0f, 0.0f, 0.0f, 0.0f,
    //        0.0f, 1.0f, 0.0f, 0.0f,
    //        0.0f, 0.0f, 1.0f, 0.0f,
    //        0.0f, 0.0f, 0.0f, 1.0f
    //    };

    // 为 BLAS 创建实例几何体
    //RTCGeometry instance = rtcNewGeometry(device, RTC_GEOMETRY_TYPE_INSTANCE);
    //rtcSetGeometryInstancedScene(instance, (RTCScene)blasBVH); // 将 BLAS BVH 转换为场景
    //rtcSetGeometryTransform(instance, 0, RTC_FORMAT_FLOAT4X4_COLUMN_MAJOR, transformMatrix);
    //rtcCommitGeometry(instance);
    //rtcAttachGeometry(tlasScene, instance);

    // 提交 TLAS
    //rtcCommitScene(tlasScene);

    //if (!(RTCScene)blasBVH) {
    //    std::cerr << "Failed to create Embree scene." << std::endl;
    //}

    

    //RTCBVH blasBVH = (RTCBVH) rtcGetSampleBvh();
    //rtcGetAccelerationStructureFromScene((RTCScene)blasBVH);
    //rtcGetAccelerationStructureFromScene(tlasScene);
    //rtcReleaseBVH(blasBVH); // 释放 BLAS BVH

     //清理资源
    //rtcReleaseGeometry(instance);
    rtcReleaseBVH((RTCBVH) blasBVH); // 释放 BLAS BVH
    //rtcReleaseScene(tlasScene);
    rtcReleaseDevice(device);

    //rtcPrintSampleScene();

    std::cout << "Custom BLAS and TLAS constructed successfully!" << std::endl;
    return 0;
}
