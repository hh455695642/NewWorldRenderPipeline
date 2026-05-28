using System;
using System.Collections.Generic;
using UnityEngine;

namespace NWRP
{
    public sealed class NWRPRuntimeFeatureStore : IDisposable
    {
        private readonly Dictionary<Type, NWRPFeature> _features =
            new Dictionary<Type, NWRPFeature>();
        private readonly string _ownerName;

        public NWRPRuntimeFeatureStore(string ownerName)
        {
            _ownerName = string.IsNullOrEmpty(ownerName)
                ? "NWRP"
                : ownerName;
        }

        public T GetOrCreate<T>()
            where T : NWRPFeature
        {
            return (T)GetOrCreate(typeof(T));
        }

        public NWRPFeature GetOrCreate(Type featureType)
        {
            if (featureType == null
                || featureType.IsAbstract
                || !typeof(NWRPFeature).IsAssignableFrom(featureType))
            {
                return null;
            }

            if (_features.TryGetValue(featureType, out NWRPFeature feature)
                && feature != null)
            {
                return feature;
            }

            feature = ScriptableObject.CreateInstance(featureType) as NWRPFeature;
            if (feature == null)
            {
                return null;
            }

            feature.hideFlags = HideFlags.HideAndDontSave;
            feature.name = $"{_ownerName} Runtime {featureType.Name}";
            _features[featureType] = feature;
            return feature;
        }

        public void Dispose()
        {
            DisposeAll();
        }

        public void DisposeAll()
        {
            foreach (NWRPFeature feature in _features.Values)
            {
                if (feature == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(feature);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(feature);
                }
            }

            _features.Clear();
        }
    }
}
