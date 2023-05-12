using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX.SDF;

using MarchingCubesProject;

internal class MeshUtils
{
    private static Vector3 GetAbsolutePadding(Vector3 boxSize, int maxResolution)
    {
        float maxExtent = Mathf.Max(boxSize.x, Mathf.Max(boxSize.y, boxSize.z));
        float voxelSize = maxExtent / maxResolution;
        Vector3 absolutePadding = 2 * voxelSize * Vector3.one;
        return absolutePadding;
    }

    private static Vector3 SnapBoxToVoxels(Vector3 boxSize, int resolution, int refAxis = 0)
    {
        float maxExtent = Mathf.Max(boxSize.x, Mathf.Max(boxSize.y, boxSize.z));
        int dimX, dimY, dimZ;

        if (refAxis == 0 || refAxis > 3) // Default behavior, choose largest dimension
        {
            if (maxExtent == boxSize.x)
            {
                refAxis = 1;
            }

            if (maxExtent == boxSize.y)
            {
                refAxis = 2;
            }

            if (maxExtent == boxSize.z)
            {
                refAxis = 3;
            }
        }

        if (refAxis == 1)
        {
            dimX = Mathf.Max(Mathf.RoundToInt(resolution * boxSize.x / maxExtent), 1);
            dimY = Mathf.Max(Mathf.CeilToInt(resolution * boxSize.y / maxExtent), 1);
            dimZ = Mathf.Max(Mathf.CeilToInt(resolution * boxSize.z / maxExtent), 1);
            float voxelSize = boxSize.x / dimX;
            var tmpBoxSize = boxSize;
            tmpBoxSize.x = dimX * voxelSize;
            tmpBoxSize.y = dimY * voxelSize;
            tmpBoxSize.z = dimZ * voxelSize;
            return tmpBoxSize;
        }
        else if (refAxis == 2)
        {
            dimY = Mathf.Max(Mathf.RoundToInt(resolution * boxSize.y / maxExtent), 1);
            dimX = Mathf.Max(Mathf.CeilToInt(resolution * boxSize.x / maxExtent), 1);
            dimZ = Mathf.Max(Mathf.CeilToInt(resolution * boxSize.z / maxExtent), 1);
            float voxelSize = boxSize.y / dimY;
            var tmpBoxSize = boxSize;
            tmpBoxSize.x = dimX * voxelSize;
            tmpBoxSize.y = dimY * voxelSize;
            tmpBoxSize.z = dimZ * voxelSize;
            return tmpBoxSize;
        }
        else
        {
            dimZ = Mathf.Max(Mathf.RoundToInt(resolution * boxSize.z / maxExtent), 1);
            dimY = Mathf.Max(Mathf.CeilToInt(resolution * boxSize.y / maxExtent), 1);
            dimX = Mathf.Max(Mathf.CeilToInt(resolution * boxSize.x / maxExtent), 1);
            float voxelSize = boxSize.z / dimZ;
            var tmpBoxSize = boxSize;
            tmpBoxSize.x = dimX * voxelSize;
            tmpBoxSize.y = dimY * voxelSize;
            tmpBoxSize.z = dimZ * voxelSize;
            return tmpBoxSize;
        }
    }

    public static List<Vector3> GetRandomVertsOnMeshSurface(Mesh mesh, int resolution)
    {
        Vector3 boxSize = mesh.bounds.extents * 2.0f;
        boxSize += GetAbsolutePadding(boxSize, resolution);
        boxSize = SnapBoxToVoxels(boxSize, resolution);

        var baker = new MeshToSDFBaker(
            sizeBox: boxSize,
            center: mesh.bounds.center,
            maxRes: resolution,
            mesh: mesh
            );

        baker.BakeSDF();
        var sdf = RenderTextureUtils.ConvertToTexture3D(baker.SdfTexture);

        baker.Dispose();

        Marching marching = new MarchingCubes { Surface = 0.005f };
        VoxelArray voxels = new VoxelArray(sdf.width, sdf.height, sdf.depth);

        //Fill voxels with values. Im using perlin noise but any method to create voxels will work.
        for (int x = 0; x < sdf.width; x++)
            for (int y = 0; y < sdf.height; y++)
                for (int z = 0; z < sdf.depth; z++)
                {
                    voxels[x, y, z] = sdf.GetPixel(x, y, z).r;
                }

        List<Vector3> verts = new();
        List<int> indices = new();

        //The mesh produced is not optimal. There is one vert for each index.
        //Would need to weld vertices for better quality mesh.
        marching.Generate(voxels.Voxels, verts, indices);

        var ratio = new Vector3(
            boxSize.x / sdf.width,
            boxSize.y / sdf.height,
            boxSize.z / sdf.depth);
        var move = boxSize * 0.5f - mesh.bounds.center;
        verts = verts.Select(data => Vector3.Scale(data, ratio) - move).ToList();

        return verts;
    }
}
