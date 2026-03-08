using UnityEngine;

namespace Maptifier.Warping
{
    public enum WarpMode { FourCorner, MeshGrid }

    public interface IWarpService
    {
        WarpMode CurrentMode { get; }
        void SetMode(WarpMode mode);
        Mesh GenerateWarpMesh(int subdivisions);
        void UpdateFourCornerWarp(Mesh mesh, Vector2[] corners);
        void UpdateMeshWarp(Mesh mesh, Vector2[] controlPoints, int gridWidth, int gridHeight);
        Vector2[] GetDefaultCorners();
        Vector2[] GetDefaultGrid(int width, int height);
    }
}
