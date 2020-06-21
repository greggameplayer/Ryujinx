using System.Numerics;

namespace Ryujinx.Common.Configuration.Hid
{
    public struct MotionInput
    {
        public Vector3 Accelerometer { get; set; }
        public Vector3 Gyroscrope    { get; set; }
    }
}
