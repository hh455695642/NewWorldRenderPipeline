using UnityEngine.Rendering;

namespace NWRP
{
    /// <summary>
    /// Base class for all passes managed by NWRPRenderer.
    /// Pass instances are expected to be long-lived and reused.
    /// </summary>
    public abstract class NWRPPass
    {
        public NWRPPassEvent passEvent { get; protected set; }
        public string debugName { get; protected set; }
        public ProfilingSampler profilingSampler { get; protected set; }
        public ProfilingSampler profilingGroupSampler { get; protected set; }
        public bool usePassProfilingScope { get; protected set; }

        protected NWRPPass(
            NWRPPassEvent passEvent,
            string debugName = null,
            ProfilingSampler profilingGroupSampler = null,
            bool usePassProfilingScope = true)
        {
            this.passEvent = passEvent;
            this.debugName = string.IsNullOrEmpty(debugName) ? GetType().Name : debugName;
            this.profilingSampler = new ProfilingSampler(this.debugName);
            this.profilingGroupSampler = profilingGroupSampler;
            this.usePassProfilingScope = usePassProfilingScope;
        }

        public abstract void Execute(ref NWRPFrameData frameData);
    }
}
