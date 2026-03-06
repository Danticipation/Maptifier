using NUnit.Framework;
using UnityEngine;
using Maptifier.Masking;

namespace Maptifier.Tests
{
    public class EarClipTriangulatorTests
    {
        [Test]
        public void Triangulate_Triangle_ReturnsCorrectIndices()
        {
            Vector2[] vertices = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1)
            };

            int[] indices = EarClipTriangulator.Triangulate(vertices);

            Assert.AreEqual(3, indices.Length);
            // Since it's already a triangle, it should return 0, 1, 2 (or some permutation)
            Assert.Contains(0, indices);
            Assert.Contains(1, indices);
            Assert.Contains(2, indices);
        }

        [Test]
        public void Triangulate_Square_ReturnsTwoTriangles()
        {
            Vector2[] vertices = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };

            int[] indices = EarClipTriangulator.Triangulate(vertices);

            // A square (4 vertices) should result in 2 triangles (6 indices)
            Assert.AreEqual(6, indices.Length);
        }

        [Test]
        public void IsPointInTriangle_PointInside_ReturnsTrue()
        {
            Vector2 a = new Vector2(0, 0);
            Vector2 b = new Vector2(2, 0);
            Vector2 c = new Vector2(1, 2);
            Vector2 p = new Vector2(1, 1);

            Assert.IsTrue(EarClipTriangulator.IsPointInTriangle(p, a, b, c));
        }

        [Test]
        public void IsPointInTriangle_PointOutside_ReturnsFalse()
        {
            Vector2 a = new Vector2(0, 0);
            Vector2 b = new Vector2(2, 0);
            Vector2 c = new Vector2(1, 2);
            Vector2 p = new Vector2(3, 3);

            Assert.IsFalse(EarClipTriangulator.IsPointInTriangle(p, a, b, c));
        }

        [Test]
        public void IsConvex_ConvexCorner_ReturnsTrue()
        {
            Vector2 a = new Vector2(0, 0);
            Vector2 b = new Vector2(1, 0);
            Vector2 c = new Vector2(1, 1);

            Assert.IsTrue(EarClipTriangulator.IsConvex(a, b, c));
        }
    }
}
