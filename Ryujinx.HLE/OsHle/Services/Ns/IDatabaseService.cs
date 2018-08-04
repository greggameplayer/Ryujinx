using Ryujinx.HLE.OsHle.Ipc;
using System.Collections.Generic;

namespace Ryujinx.HLE.OsHle.Services.Ns
{
    class IDatabaseService : IpcService
    {
        private Dictionary<int, ServiceProcessRequest> m_Commands;

        public override IReadOnlyDictionary<int, ServiceProcessRequest> Commands => m_Commands;

        public IDatabaseService()
        {
            m_Commands = new Dictionary<int, ServiceProcessRequest>()
            {
                { 3, Get }
            };
        }
		
		public long Get(ServiceCtx Context)
		{
			int Id = Context.RequestData.ReadInt32();
			
			Context.ResponseData.Write(1);
			
			
			return 0;
		}
    }
}