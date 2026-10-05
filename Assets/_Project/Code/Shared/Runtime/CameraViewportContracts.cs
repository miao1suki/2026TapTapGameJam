using UnityEngine;

namespace Project.Contracts
{
    public readonly struct CameraViewportInfo
    {
        public CameraViewportInfo(
            Rect pixelRect,
            bool orthographic,
            float orthographicSize,
            float fieldOfView)
        {
            PixelRect = pixelRect;
            Orthographic = orthographic;
            OrthographicSize = orthographicSize;
            FieldOfView = fieldOfView;
        }

        public Rect PixelRect { get; }
        public bool Orthographic { get; }
        public float OrthographicSize { get; }
        public float FieldOfView { get; }
    }

    public interface ICameraViewportSource
    {
        bool TryGetViewport(out CameraViewportInfo info);
    }
}
