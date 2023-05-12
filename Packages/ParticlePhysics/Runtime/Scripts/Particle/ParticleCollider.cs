using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.VFX.SDF;

using MarchingCubesProject;
using ParticlePhysics;
using ParticlePhysics.Type;
using ParticlePhysics.Utils.NearestNeighbour;

[RequireComponent(typeof(MeshFilter))]
public class ParticleCollider : MonoBehaviour
{
    public float particleRadius = 0.1f;
    public int maxResolution = 64;

    private Texture3D _sdfData;
    private Bounds _boundingBox;
    private GranularParticle _particle;
    private GridSearch<ParticleState> _objectGS;

    #region Accessor
    public Bounds BoundingBox { get => _boundingBox; private set => _boundingBox = value; }
    public Texture3D MeshSdf { get => _sdfData; private set => _sdfData = value; }
    public GranularParticle Particle { get => _particle; private set => _particle = value; }
    internal GridSearch<ParticleState> ObjectGS { get => _objectGS; private set => _objectGS = value; }
    #endregion


    // Update is called once per frame
    void Start()
    {
        MeshFilter mf = this.GetComponent<MeshFilter>();
        SetBoundingBox(mf.mesh);
        SetMeshSDF(mf.mesh);
        _particle = GranularParticle.SetAsSimpleParticle(
            particles: ParticleState.GenerateFromVertices(GetVertsOnMeshSurface(mf.mesh)),
            radius: 0.1f);
        _objectGS = new GridSearch<ParticleState>(_particle.num, _boundingBox.size, particleRadius*3);
    }

    private void OnDestroy()
    {
        _particle.Release();
        _objectGS.Release();
    }

    private void SetBoundingBox(Mesh mesh)
    {
        _boundingBox = mesh.bounds;
        _boundingBox.size += GetAbsolutePadding(_boundingBox.size, maxResolution);
    }
    
    private void SetMeshSDF(Mesh mesh)
    {
        var baker = new MeshToSDFBaker(
            sizeBox: SnapBoxToVoxels(_boundingBox.size, maxResolution),
            center: mesh.bounds.center,
            maxRes: maxResolution,
            mesh: mesh
            );

        baker.BakeSDF();
        RenderTexture sdf = baker.SdfTexture;
        _sdfData = RenderTextureUtils.ConvertToTexture3D(sdf);

        baker.Dispose();
    }

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

    
    private Vector3[] GetVertsOnMeshSurface(Mesh mesh)
    {
        Marching marching = new MarchingCubes{Surface = 0.005f};
        VoxelArray voxels = new VoxelArray(_sdfData.width, _sdfData.height, _sdfData.depth);

        //Fill voxels with values. Im using perlin noise but any method to create voxels will work.
        for (int x = 0; x < _sdfData.width; x++)
            for (int y = 0; y < _sdfData.height; y++)
                for (int z = 0; z < _sdfData.depth; z++)
                {
                    voxels[x, y, z] = _sdfData.GetPixel(x, y, z).r;
                }

        List<Vector3> verts = new();
        List<int> indices = new();

        //The mesh produced is not optimal. There is one vert for each index.
        //Would need to weld vertices for better quality mesh.
        marching.Generate(voxels.Voxels, verts, indices);

        var ratio = new Vector3(
            _boundingBox.size.x / _sdfData.width,
            _boundingBox.size.y / _sdfData.height,
            _boundingBox.size.z / _sdfData.depth);
        var move = _boundingBox.size * 0.5f - _boundingBox.center;
        return verts.Select(data => Vector3.Scale(data, ratio) - move).ToArray();
    }

    public static List<Vector3> GetVertsOnMeshSurface(Mesh mesh, int resolution)
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
