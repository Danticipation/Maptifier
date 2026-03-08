using UnityEngine;
using Maptifier.Core;

namespace Maptifier.Warping
{
    /// <summary>
    /// MonoBehaviour that renders a warp mesh with projective texture mapping.
    /// Uses MaterialPropertyBlock for per-instance texture assignment.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class WarpMeshRenderer : MonoBehaviour
    {
        [SerializeField] private Material _warpMaterial;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private IWarpService _warpService;
        private Mesh _mesh;

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _propertyBlock = new MaterialPropertyBlock();
            _warpService = ServiceLocator.Get<IWarpService>();
            if (_warpMaterial != null)
                _meshRenderer.sharedMaterial = _warpMaterial;
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }

        /// <summary>
        /// Sets the source RenderTexture to display. Uses MaterialPropertyBlock for per-instance assignment.
        /// </summary>
        public void SetSourceTexture(RenderTexture rt)
        {
            if (_meshRenderer == null || _warpMaterial == null) return;

            _meshRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetTexture("_MainTex", rt != null ? rt : Texture2D.blackTexture);
            _meshRenderer.SetPropertyBlock(_propertyBlock);
        }

        /// <summary>
        /// Updates the warp mesh with the given corner/control points.
        /// For FourCorner mode: pass 4 corners. For MeshGrid: pass gridWidth * gridHeight points.
        /// </summary>
        public void UpdateWarp(Vector2[] points, int gridWidth = 0, int gridHeight = 0)
        {
            if (_warpService == null || points == null) return;

            if (_mesh == null)
            {
                var subdivisions = gridWidth > 0 && gridHeight > 0
                    ? Mathf.Max(gridWidth, gridHeight)
                    : 16;
                _mesh = _warpService.GenerateWarpMesh(subdivisions);
                if (_meshFilter != null)
                    _meshFilter.sharedMesh = _mesh;
            }

            if (_warpService.CurrentMode == WarpMode.FourCorner && points.Length >= 4)
            {
                _warpService.UpdateFourCornerWarp(_mesh, points);
            }
            else if (gridWidth > 0 && gridHeight > 0 && points.Length >= gridWidth * gridHeight)
            {
                _warpService.UpdateMeshWarp(_mesh, points, gridWidth, gridHeight);
            }
        }

        /// <summary>
        /// Sets the warp material. Uses the projective texture mapping shader.
        /// </summary>
        public void SetMaterial(Material mat)
        {
            _warpMaterial = mat;
            if (_meshRenderer != null)
                _meshRenderer.sharedMaterial = mat;
        }
    }
}
