#if UNITY_EDITOR
        void DrawCube(ScriptableRenderContext context, Camera camera)
        {
            if (material == null)
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            if (m_BoxMesh == null)
                m_BoxMesh = CreateBoundingBoxMesh();
            var cmd = CommandBufferPool.Get();
            cmd.DrawMesh(m_BoxMesh, Matrix4x4.identity, material, 0);
            context.ExecuteCommandBuffer(cmd);
            context.Submit();
        }

        Mesh CreateBoundingBoxMesh()
        {
            Vector3[] positions =
            {
                bounds.min, new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.min.z), new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.min.y, bounds.max.z), new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.max.z), bounds.max,
            };

            int[] indices =
            {
                0, 1, 2, 
                0, 2, 3,
                0, 3, 4,
                4, 3, 5,
                6, 2, 1,
                7, 2, 6,
                5, 4, 7,
                7, 4, 6,
            };

            Mesh mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt16;
            mesh.vertices = positions;
            mesh.triangles = indices;
            mesh.SetIndices(indices, MeshTopology.LineStrip, 0);

            return mesh;
        }
#endif