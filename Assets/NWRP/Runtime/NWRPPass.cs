namespace NWRP
{
    /// <summary>
    /// Base class for all passes managed by NWRPRenderer.
    /// Pass instances are expected to be long-lived and reused.
    /// </summary>
    public abstract class NWRPPass
    {
        public NWRPPassEvent passEvent { get; protected set; }

        protected NWRPPass(NWRPPassEvent passEvent)
        {
            this.passEvent = passEvent;
        }

        public abstract void Execute(ref NWRPFrameData frameData);
    }
}
