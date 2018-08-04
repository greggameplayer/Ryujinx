using Ryujinx.HLE.OsHle.Ipc;
using System.Collections.Generic;

namespace Ryujinx.HLE.OsHle.Services.Ns
{
    class IStaticService : IpcService
    {
        private Dictionary<int, ServiceProcessRequest> m_Commands;

        public override IReadOnlyDictionary<int, ServiceProcessRequest> Commands => m_Commands;

        public IStaticService()
        {
            m_Commands = new Dictionary<int, ServiceProcessRequest>()
            {
                { 0, GetDatabaseService }
            };
        }
		
		public long GetDatabaseService(ServiceCtx Context)
		{
			int Id = Context.RequestData.ReadInt32();
			
			MakeObject(Context, new IDatabaseService());
			
			return 0;
		}
    }
}