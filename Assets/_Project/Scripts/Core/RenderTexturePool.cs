using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maptifier.Core
{
    public interface IRenderTexturePool
    {
        RenderTexture Get(int width, int height, RenderTextureFormat format = RenderTextureFormat.ARGB32, int depth = 0);
        void Release(RenderTexture rt);
        void ReleaseAll();
    }

    public class RenderTexturePool : IRenderTexturePool, IDisposable
    {
        private readonly Dictionary<(int Width, int Height, RenderTextureFormat Format, int Depth), Stack<RenderTexture>> _pool = new();
        private bool _disposed;

        public RenderTexture Get(int width, int height, RenderTextureFormat format = RenderTextureFormat.ARGB32, int depth = 0)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RenderTexturePool));
            }

            var key = (width, height, format, depth);

            if (_pool.TryGetValue(key, out var stack) && stack.Count > 0)
            {
                var rt = stack.Pop();
                rt.DiscardContents(true, true);
                return rt;
            }

            var rt = new RenderTexture(width, height, depth, format);
            rt.Create();
            return rt;
        }

        public void Release(RenderTexture rt)
        {
            if (rt == null) return;
            if (_disposed)
            {
                rt.Release();
                return;
            }

            var key = (rt.width, rt.height, rt.format, rt.depth);

            if (!_pool.TryGetValue(key, out var stack))
            {
                stack = new Stack<RenderTexture>();
                _pool[key] = stack;
            }

            stack.Push(rt);
        }

        public void ReleaseAll()
        {
            foreach (var stack in _pool.Values)
            {
                while (stack.Count > 0)
                {
                    var rt = stack.Pop();
                    rt.Release();
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            ReleaseAll();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
