using Ryujinx.Common.DSU;
using Ryujinx.Configuration;
using System.Numerics;

namespace Ryujinx.Common.Configuration.Hid
{
    public class MotionDevice
    {
        public const float GyroCoeff = 0.00122187695f;

        public Vector3 Gyroscope     { get; private set; }
        public Vector3 Accelerometer { get; private set; }
        public float[] Orientation   { get; private set; }

        private Client _motionSource;

        public MotionDevice(Client motionSource){
            _motionSource = motionSource;
        }

        public void RegisterController(PlayerIndex player)
        {
            InputConfig config = ConfigurationState.Instance.Hid.InputConfig.Value.Find(x => x.PlayerIndex == player);

            if(config != null && config.EnableMotion)
            {
                string host = config.UseAltServer ? config.DsuServerHost : ConfigurationState.Instance.Hid.DsuServerHost;
                int    port = config.UseAltServer ? config.DsuServerPort : ConfigurationState.Instance.Hid.DsuServerPort;

                _motionSource.RegisterClient((int)player, host, port);
                _motionSource.RequestData((int)player, config.Slot);

                if(config.ControllerType == ControllerType.JoyconPair && !config.MirrorInput)
                {
                    _motionSource.RequestData((int)player, config.AltSlot);
                }
            }
        }

        public void Poll(PlayerIndex player, int slot)
        {
            if (!ConfigurationState.Instance.Hid.EnableDsuClient)
            {
                Accelerometer = new Vector3();
                Gyroscope     = new Vector3();

                Orientation = new float[9];

                return;
            }

            var input = _motionSource.GetData((int)player, slot);

            Gyroscope     = Truncate(input.Gyroscrope * GyroCoeff);
            Accelerometer = Truncate(input.Accelerometer);

            Vector3 zNormal = Vector3.Normalize(Accelerometer * -1);
            Vector3 yNormal = Vector3.Normalize(new Vector3(0, zNormal.Z, -zNormal.Y));

            Vector3 xCross = Vector3.Cross(yNormal, zNormal);

            Orientation = new float[9];

            Orientation[0] = xCross.X;
            Orientation[1] = yNormal.X;
            Orientation[2] = zNormal.X;
            Orientation[3] = xCross.Y;
            Orientation[4] = yNormal.Y;
            Orientation[5] = zNormal.Y;
            Orientation[6] = xCross.Z;
            Orientation[7] = yNormal.Z;
            Orientation[8] = zNormal.Z;
        }

        private Vector3 Truncate(Vector3 value)
        {
            value.X = (int)(value.X * 1000) * 0.001f;
            value.Y = (int)(value.Y * 1000) * 0.001f;
            value.Z = (int)(value.Z * 1000) * 0.001f;
            return value;
        }
    }
}
