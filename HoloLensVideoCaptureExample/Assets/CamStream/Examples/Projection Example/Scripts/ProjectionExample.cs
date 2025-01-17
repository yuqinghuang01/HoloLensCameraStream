//  
// Copyright (c) 2017 Vulcan, Inc. All rights reserved.  
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.
//

using UnityEngine;
using HoloLensCameraStream;
using System;

#if WINDOWS_UWP && XR_PLUGIN_OPENXR
using Windows.Perception.Spatial;
#endif

#if WINDOWS_UWP
using Windows.UI.Input.Spatial;
#endif

/// <summary>
/// In this example, we back-project to the 3D world 5 pixels, which are the principal point and the image corners,
/// using the extrinsic parameters and projection matrices.
/// Whereas the app is running, if you tap on the image, this set of points is reprojected into the world.
/// 
/// **Add Define Symbols:**
/// Open **File > Build Settings > Player Settings > Other Settings** and add the following to `Scripting Define Symbols` depending on the XR system used in your project;
/// - Legacy built-in XR: `BUILTIN_XR`';
/// - XR Plugin Management (Windows Mixed Reality): `XR_PLUGIN_WINDOWSMR`;
/// - XR Plugin Management (OpenXR):`XR_PLUGIN_OPENXR`.
/// </summary>
public class ProjectionExample : MonoBehaviour
{
    // "Injected materials"
    public Material _topLeftMaterial;
    public Material _topRightMaterial;
    public Material _botLeftMaterial;
    public Material _botRightMaterial;
    public Material _centerMaterial;

    public float _lineWidthMultiplier = 0.01f;

    private HoloLensCameraStream.Resolution _resolution;
    private VideoCapture _videoCapture;

    private IntPtr _spatialCoordinateSystemPtr;

#if WINDOWS_UWP && XR_PLUGIN_OPENXR
    SpatialCoordinateSystem _spatialCoordinateSystem;
#endif

    private byte[] _latestImageBytes;
    private bool stopVideo;

#if WINDOWS_UWP
    private SpatialInteractionManager _spatialInteraction;
#endif

    // Frame gameobject, renderer and texture
    private GameObject _picture;
    private Renderer _pictureRenderer;
    private Texture2D _pictureTexture;

    private RaycastLaser _laser;

    LineRenderer lr1;
    LineRenderer lr2;
    LineRenderer lr3;
    LineRenderer lr4;

    // This struct store frame related data
    private class SampleStruct
    {
        public float[] camera2WorldMatrix, projectionMatrix;
        public byte[] data;
    }


    void Awake()
    {
#if WINDOWS_UWP
        UnityEngine.WSA.Application.InvokeOnUIThread(() =>
        {
            _spatialInteraction = SpatialInteractionManager.GetForCurrentView();
        }, true);
        _spatialInteraction.SourcePressed += SpatialInteraction_SourcePressed;
#endif
    }

    void Start()
    {
        //Fetch a pointer to Unity's spatial coordinate system if you need pixel mapping
#if WINDOWS_UWP

#if XR_PLUGIN_WINDOWSMR

        _spatialCoordinateSystemPtr = UnityEngine.XR.WindowsMR.WindowsMREnvironment.OriginSpatialCoordinateSystem;

#elif XR_PLUGIN_OPENXR

        _spatialCoordinateSystem = Microsoft.MixedReality.OpenXR.PerceptionInterop.GetSceneCoordinateSystem(UnityEngine.Pose.identity) as SpatialCoordinateSystem;

#elif BUILTIN_XR

#if UNITY_2017_2_OR_NEWER
        _spatialCoordinateSystemPtr = UnityEngine.XR.WSA.WorldManager.GetNativeISpatialCoordinateSystemPtr();
#else
        _spatialCoordinateSystemPtr = UnityEngine.VR.WSA.WorldManager.GetNativeISpatialCoordinateSystemPtr();
#endif

#endif

#endif

        CameraStreamHelper.Instance.GetVideoCaptureAsync(OnVideoCaptureCreated);

        // Create the frame container and apply HolographicImageBlend shader
        _picture = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _picture.transform.localScale = new Vector3(1.5f, 1.5f, 1);
        _pictureRenderer = _picture.GetComponent<Renderer>() as Renderer;
        _pictureRenderer.material = new Material(Shader.Find("AR/HolographicImageBlend"));

        // Set the lines for bounding box
        lr1 = new GameObject().AddComponent<LineRenderer>();
        lr2 = new GameObject().AddComponent<LineRenderer>();
        lr3 = new GameObject().AddComponent<LineRenderer>();
        lr4 = new GameObject().AddComponent<LineRenderer>();
        lr1.widthMultiplier = _lineWidthMultiplier;
        lr2.widthMultiplier = _lineWidthMultiplier;
        lr3.widthMultiplier = _lineWidthMultiplier;
        lr4.widthMultiplier = _lineWidthMultiplier;
        lr1.material = _centerMaterial;
        lr2.material = _centerMaterial;
        lr3.material = _centerMaterial;
        lr4.material = _centerMaterial;
    }

    private void OnDestroy()
    {
        if (_videoCapture == null)
            return;

        _videoCapture.FrameSampleAcquired += null;
        _videoCapture.Dispose();

#if WINDOWS_UWP
        _spatialInteraction.SourcePressed -= SpatialInteraction_SourcePressed;
        _spatialInteraction = null;
#endif
    }

    private void OnVideoCaptureCreated(VideoCapture v)
    {
        if (v == null)
        {
            Debug.LogError("No VideoCapture found");
            return;
        }

        _videoCapture = v;

        //Request the spatial coordinate ptr if you want fetch the camera and set it if you need to 
#if WINDOWS_UWP

#if XR_PLUGIN_OPENXR
        CameraStreamHelper.Instance.SetNativeISpatialCoordinateSystem(_spatialCoordinateSystem);
#elif XR_PLUGIN_WINDOWSMR || BUILTIN_XR
        CameraStreamHelper.Instance.SetNativeISpatialCoordinateSystemPtr(_spatialCoordinateSystemPtr);
#endif

#endif

        _resolution = CameraStreamHelper.Instance.GetLowestResolution();
        float frameRate = CameraStreamHelper.Instance.GetHighestFrameRate(_resolution);

        _videoCapture.FrameSampleAcquired += OnFrameSampleAcquired;

        CameraParameters cameraParams = new CameraParameters();
        cameraParams.cameraResolutionHeight = _resolution.height;
        cameraParams.cameraResolutionWidth = _resolution.width;
        cameraParams.frameRate = Mathf.RoundToInt(frameRate);
        cameraParams.pixelFormat = CapturePixelFormat.BGRA32;

        UnityEngine.WSA.Application.InvokeOnAppThread(() => { _pictureTexture = new Texture2D(_resolution.width, _resolution.height, TextureFormat.BGRA32, false); }, false);

        _videoCapture.StartVideoModeAsync(cameraParams, OnVideoModeStarted);
    }

    private void OnVideoModeStarted(VideoCaptureResult result)
    {
        if (result.success == false)
        {
            Debug.LogWarning("Could not start video mode.");
            return;
        }

        Debug.Log("Video capture started.");
    }

    private void OnFrameSampleAcquired(VideoCaptureSample sample)
    {
        // Allocate byteBuffer
        if (_latestImageBytes == null || _latestImageBytes.Length < sample.dataLength)
            _latestImageBytes = new byte[sample.dataLength];

        // Fill frame struct 
        SampleStruct s = new SampleStruct();
        sample.CopyRawImageDataIntoBuffer(_latestImageBytes);
        s.data = _latestImageBytes;

        // Get the cameraToWorldMatrix and projectionMatrix
        if (!sample.TryGetCameraToWorldMatrix(out s.camera2WorldMatrix) || !sample.TryGetProjectionMatrix(out s.projectionMatrix))
            return;

        sample.Dispose();

        Matrix4x4 camera2WorldMatrix = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(s.camera2WorldMatrix);
        Matrix4x4 projectionMatrix = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(s.projectionMatrix);

        UnityEngine.WSA.Application.InvokeOnAppThread(() =>
        {
            if (!stopVideo)
            {
                // Upload bytes to texture
                _pictureTexture.LoadRawTextureData(s.data);
                _pictureTexture.wrapMode = TextureWrapMode.Clamp;
                _pictureTexture.Apply();

                // Set material parameters
                _pictureRenderer.sharedMaterial.SetTexture("_MainTex", _pictureTexture);
                _pictureRenderer.sharedMaterial.SetMatrix("_WorldToCameraMatrix", camera2WorldMatrix.inverse);
                _pictureRenderer.sharedMaterial.SetMatrix("_CameraProjectionMatrix", projectionMatrix);
                _pictureRenderer.sharedMaterial.SetInt("_FlipY", 1);
                _pictureRenderer.sharedMaterial.SetFloat("_VignetteScale", 0f);

                Vector3 inverseNormal = -camera2WorldMatrix.GetColumn(2);
                // Position the canvas object slightly in front of the real world web camera.
                Vector3 imagePosition = camera2WorldMatrix.GetColumn(3) - camera2WorldMatrix.GetColumn(2);

                _picture.transform.position = imagePosition;
                _picture.transform.rotation = Quaternion.LookRotation(inverseNormal, camera2WorldMatrix.GetColumn(1));

                // define box corners coordinates
                Vector2 topLeftImageCoord = new Vector2(_resolution.width / 4, _resolution.height / 4);
                Vector2 topRightImageCoord = new Vector2(3 * _resolution.width / 4, _resolution.height / 4);
                Vector2 botLeftImageCoord = new Vector2(_resolution.width / 4, 3 * _resolution.height / 4);
                Vector2 botRightImageCoord = new Vector2(3 * _resolution.width / 4, 3 * _resolution.height / 4);

                // get depth data
                float depth = 0.8f;

                // Get corner coordinates in world coordinate system
                Vector3 from = camera2WorldMatrix.GetColumn(3);
                Vector3 topLeftWorldCoord = from + depth * LocatableCameraUtils.PixelCoordToWorldCoord(camera2WorldMatrix, projectionMatrix, _resolution, topLeftImageCoord);
                Vector3 topRightWorldCoord = from + depth * LocatableCameraUtils.PixelCoordToWorldCoord(camera2WorldMatrix, projectionMatrix, _resolution, topRightImageCoord);
                Vector3 botLeftWorldCoord = from + depth * LocatableCameraUtils.PixelCoordToWorldCoord(camera2WorldMatrix, projectionMatrix, _resolution, botLeftImageCoord);
                Vector3 botRightWorldCoord = from + depth * LocatableCameraUtils.PixelCoordToWorldCoord(camera2WorldMatrix, projectionMatrix, _resolution, botRightImageCoord);

                // update line positions of bounding box
                lr1.SetPosition(0, topLeftWorldCoord);
                lr1.SetPosition(1, topRightWorldCoord);
                lr2.SetPosition(0, topRightWorldCoord);
                lr2.SetPosition(1, botRightWorldCoord);
                lr3.SetPosition(0, botRightWorldCoord);
                lr3.SetPosition(1, botLeftWorldCoord);
                lr4.SetPosition(0, botLeftWorldCoord);
                lr4.SetPosition(1, topLeftWorldCoord);
            }

#if XR_PLUGIN_WINDOWSMR || XR_PLUGIN_OPENXR
            // It appears that the Legacy built-in XR environment automatically applies the Holelens Head Pose to Unity camera transforms,
            // but not to the new XR system (XR plugin management) environment.
            // Here the cameraToWorldMatrix is applied to the camera transform as an alternative to Head Pose,
            // so the position of the displayed video panel is significantly misaligned. If you want to apply a more accurate Head Pose, use MRTK.

            Camera unityCamera = Camera.main;
            Matrix4x4 invertZScaleMatrix = Matrix4x4.Scale(new Vector3(1, 1, -1));
            Matrix4x4 localToWorldMatrix = camera2WorldMatrix * invertZScaleMatrix;
            unityCamera.transform.localPosition = localToWorldMatrix.GetColumn(3);
            unityCamera.transform.localRotation = Quaternion.LookRotation(localToWorldMatrix.GetColumn(2), localToWorldMatrix.GetColumn(1));
#endif
        }, false);
    }

#if WINDOWS_UWP
    private void SpatialInteraction_SourcePressed(SpatialInteractionManager sender, SpatialInteractionSourceEventArgs args)
    {
        var item = args.State;
        UnityEngine.WSA.Application.InvokeOnAppThread(() =>
        {
            Debug.Log("SourcePressed");

            for (int i = _laser.transform.childCount - 1; i >= 0; --i)
            {
                GameObject.DestroyImmediate(_laser.transform.GetChild(i).gameObject);
            }

            stopVideo = !stopVideo;

        }, false);
    }
#endif
}
