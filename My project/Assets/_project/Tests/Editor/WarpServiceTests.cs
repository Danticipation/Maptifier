using NUnit.Framework;
using UnityEngine;
using Maptifier.Warping;

namespace Maptifier.Tests
{
    public class WarpServiceTests
    {
        private WarpService _warpService;

        [SetUp]
        public void SetUp()
        {
            _warpService = new WarpService();
        }

        [Test]
        public void GenerateWarpMesh_CreatesCorrectVertexCount()
        {
            int subdivisions = 4;
            Mesh mesh = _warpService.GenerateWarpMesh(subdivisions);

            int expectedVertices = (subdivisions + 1) * (subdivisions + 1);
            Assert.AreEqual(expectedVertices, mesh.vertexCount);
        }

        [Test]
        public void GenerateWarpMesh_InitializesUVsCorrectly()
        {
            int subdivisions = 2;
            Mesh mesh = _warpService.GenerateWarpMesh(subdivisions);
            var uvs = mesh.uv;

            Assert.AreEqual(new Vector2(0, 0), uvs[0]);
            Assert.AreEqual(new Vector2(1, 1), uvs[uvs.Length - 1]);
        }

        [Test]
        public void UpdateFourCornerWarp_MovesVerticesToCorners()
        {
            int subdivisions = 1; // 4 vertices
            Mesh mesh = _warpService.GenerateWarpMesh(subdivisions);

            Vector2[] corners = new Vector2[]
            {
                new Vector2(-10, -10),
                new Vector2(10, -10),
                new Vector2(10, 10),
                new Vector2(-10, 10)
            };

            _warpService.UpdateFourCornerWarp(mesh, corners);
            var vertices = mesh.vertices;

            Assert.AreEqual(new Vector3(-10, -10, 0), vertices[0]);
            Assert.AreEqual(new Vector3(10, -10, 0), vertices[1]);
            Assert.AreEqual(new Vector3(-10, 10, 0), vertices[2]); // Note: order in GenerateWarpMesh is row-major
            Assert.AreEqual(new Vector3(10, 10, 0), vertices[3]);
        }
    }
}
