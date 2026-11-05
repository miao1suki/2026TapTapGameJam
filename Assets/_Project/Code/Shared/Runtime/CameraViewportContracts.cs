using UnityEngine;

namespace Project.Contracts
{
    public readonly struct CameraViewportInfo
    {
        public CameraViewportInfo(
            Rect pixelRect,
            bool orthographic,
            float orthographicSize)
        {
            PixelRect = pixelRect;
            Orthographic = orthographic;
            OrthographicSize = orthographicSize;
        }

        public Rect PixelRect { get; }
        public bool Orthographic { get; }
        public float OrthographicSize { get; }
    }

    public interface ICameraViewportSource
    {
        bool TryGetViewport(out CameraViewportInfo info);
    }
}
