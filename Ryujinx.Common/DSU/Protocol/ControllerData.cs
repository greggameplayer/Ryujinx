using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Ryujinx.Common.DSU
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct ControllerDataRequest
    {
        public MessageType Type;
        public SubscriberType SubscriberType;
        public byte Slot;
        public fixed byte Mac[6];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct ControllerDataResponse
    {
        public SharedResponse Shared;
        public byte Connected;
        public uint PacketID;
        public byte ExtraButtons;
        public byte MainButtons;
        public ushort PSExtraInput;
        public ushort LeftStickXY;
        public ushort RightStickXY;
        public uint DPadAnalog;
        public ulong MainButtonsAnalog;
        public fixed byte Touch1[6];
        public fixed byte Touch2[6];
        public ulong MotionTimestamp;
        public float AccelerometerX;
        public float AccelerometerY;
        public float AccelerometerZ;
        public float GyroscopePitch;
        public float GyroscopeYaw;
        public float GyroscopeRoll;
    }

    enum SubscriberType : byte
    {
        All = 0,
        Slot,
        Mac
    }
}
