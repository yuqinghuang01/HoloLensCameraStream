namespace HoloLensCameraStream
{
    // Refer https://github.com/EnoxSoftware/HoloLensWithOpenCVForUnityExample/blob/master/Assets/HoloLensWithOpenCVForUnityExample/HoloLensArUcoExample/HoloLensArUcoExample.cs
    public struct CameraIntrinsics
    {
        public double FocalLengthX;         //focal length x.
        public double FocalLengthY;         //focal length y.
        public double PrincipalPointX;      //principal point x.
        public double PrincipalPointY;      //principal point y.
        public double RadialDistK1;         //radial distortion coefficient k1.
        public double RadialDistK2;         //radial distortion coefficient k2.
        public double TangentialDistP1;     //tangential distortion coefficient p1.
        public double TangentialDistP2;     //tangential distortion coefficient p2.
        public double RadialDistK3;         //radial distortion coefficient k3.

        public CameraIntrinsics(Windows.Media.Devices.Core.CameraIntrinsics intrinsics)
        {
            FocalLengthX = intrinsics.FocalLength.X;
            FocalLengthY = intrinsics.FocalLength.Y;
            PrincipalPointX = intrinsics.PrincipalPoint.X;
            PrincipalPointY = intrinsics.PrincipalPoint.Y;
            RadialDistK1 = intrinsics.RadialDistortion.X;
            RadialDistK2 = intrinsics.RadialDistortion.Y;
            RadialDistK3 = intrinsics.RadialDistortion.Z;
            TangentialDistP1 = intrinsics.TangentialDistortion.X;
            TangentialDistP2 = intrinsics.TangentialDistortion.Y;
        }
    }
}