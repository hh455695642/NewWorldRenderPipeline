using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NWRP
{
    internal static class NWRPVolumeDefaults
    {
        // Extension point: append new NWRP VolumeComponent types here so player
        // builds can construct a complete VolumeStack without editor reflection.
        private static readonly Type[] s_ComponentTypes =
        {
            typeof(NWRPTonemapping),
            typeof(NWRPBloom),
            typeof(NWRPColorAdjustments),
            typeof(NWRPVignette),
            typeof(NWRPAntiAliasing),
            typeof(NWRPScreenBlur),
            typeof(NWRPValleyHeightFog),
            typeof(NWRPCloudShadowProjector),
            typeof(NWRPFog)
        };

        public static VolumeProfile CreateProfile()
        {
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "NWRP Runtime Default Volume Profile";
            profile.hideFlags = HideFlags.HideAndDontSave;

            for (int i = 0; i < s_ComponentTypes.Length; i++)
            {
                VolumeComponent component = profile.Add(s_ComponentTypes[i], overrides: false);
                component.name = s_ComponentTypes[i].Name;
                component.hideFlags = HideFlags.HideAndDontSave;
            }

            return profile;
        }

        public static void DestroyProfile(VolumeProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            if (profile.components != null)
            {
                for (int i = profile.components.Count - 1; i >= 0; i--)
                {
                    CoreUtils.Destroy(profile.components[i]);
                }

                profile.components.Clear();
            }

            CoreUtils.Destroy(profile);
        }
    }
}
