using System.Runtime.InteropServices;

namespace Ryujinx.Common.DSU
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct SharedResponse
    {
        public MessageType Type;
        public byte Slot;
        public SlotState State;
        public DeviceModelType ModelType;
        public ConnectionType ConnectionType;
        public fixed byte MacAddress[6];
        public BatteryStatus BatteryStatus;
    }

    public enum SlotState : byte
    {
        Disconnected = 0,
        Reserved,
        Connected
    }

    public enum DeviceModelType : byte
    {
        None = 0,
        PartialGyro,
        FullGyro
    }

    public enum ConnectionType : byte
    {
        None = 0,
        USB,
        Bluetooth
    }

    public enum BatteryStatus : byte
    {
        NA = 0,
        Dying,
        Low,
        Medium,
        High,
        Full,
        Charging,
        Charged
    }
}
