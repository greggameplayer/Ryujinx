using System.Runtime.InteropServices;

namespace Ryujinx.Common.DSU
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ControllerInfoResponse
    {
        public SharedResponse Shared;
        private byte _zero;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct ControllerInfoRequest
    {
        public MessageType Type;
        public int PortsCount;
        public fixed byte PortIndices[4];
    }
}
