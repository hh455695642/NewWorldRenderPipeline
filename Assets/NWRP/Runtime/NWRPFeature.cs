using UnityEngine;

namespace NWRP
{
    /// <summary>
    /// Base ScriptableObject for custom renderer features.
    /// A feature can enqueue one or more passes.
    /// </summary>
    public abstract class NWRPFeature : ScriptableObject
    {
        [SerializeField]
        private bool isEnabled = true;

        [System.NonSerialized]
        private bool _isCreated;

        public bool IsEnabled => isEnabled;

        internal void EnsureCreated()
        {
            if (_isCreated)
            {
                return;
            }

            Create();
            _isCreated = true;
        }

        protected abstract void Create();

        public abstract void AddPasses(NWRPRenderer renderer, ref NWRPFrameData frameData);

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            _isCreated = false;
        }
#endif
    }
}
