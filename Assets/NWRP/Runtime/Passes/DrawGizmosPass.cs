#if UNITY_EDITOR
using UnityEngine.Rendering;

namespace NWRP.Runtime.Passes
{
    internal sealed class DrawGizmosPass : NWRPPass
    {
        private readonly NWRPRenderer _renderer;
        private readonly GizmoSubset _gizmoSubset;

        public DrawGizmosPass(
            NWRPRenderer renderer,
            NWRPPassEvent passEvent,
            GizmoSubset gizmoSubset,
            string debugName)
            : base(passEvent, debugName)
        {
            _renderer = renderer;
            _gizmoSubset = gizmoSubset;
        }

        public override void Execute(ref NWRPFrameData frameData)
        {
            _renderer.ExecuteDrawGizmos(ref frameData, _gizmoSubset);
        }
    }
}
#endif
