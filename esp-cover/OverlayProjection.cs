using EFT.CameraControl;
using UnityEngine;
namespace LocalSpt5Esp;

internal static class OverlayProjection {
 internal static Camera? MainCamera {
  get { var camera=CameraManager.Instance?.Camera;return camera!=null&&camera.isActiveAndEnabled?camera:Camera.main; }
 }
 // Camera pixels belong to its render texture (FSR/DLSS/SSAA), not the IMGUI
 // backbuffer. Normalized viewport coordinates remain valid at every scale.
 internal static Vector3 ScreenPoint(Camera camera,Vector3 position){
  var point=camera.WorldToViewportPoint(position);var rect=camera.rect;
  var pixel=OverlayProjectionRules.Pixels(point.x,point.y,rect.x,rect.y,rect.width,rect.height,Screen.width,Screen.height);
  return new(pixel.X,pixel.Y,point.z);
 }
}
