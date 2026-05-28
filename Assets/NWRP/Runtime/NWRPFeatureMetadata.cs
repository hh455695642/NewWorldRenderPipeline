using System;
using System.Collections.Generic;
using System.Reflection;

namespace NWRP
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class NWRPFeatureMetadataAttribute : Attribute
    {
        public NWRPFeatureMetadataAttribute(string displayName)
        {
            DisplayName = string.IsNullOrEmpty(displayName)
                ? string.Empty
                : displayName;
            MenuPath = DisplayName;
            ShowInAddMenu = true;
        }

        public string DisplayName { get; }
        public string MenuPath { get; set; }
        public bool AllowMultiple { get; set; }
        public bool VolumeDriven { get; set; }
        public bool ShowInAddMenu { get; set; }
        public int SortOrder { get; set; }
    }

    public readonly struct NWRPFeatureMetadataInfo
    {
        public readonly string displayName;
        public readonly string menuPath;
        public readonly bool allowMultiple;
        public readonly bool volumeDriven;
        public readonly bool showInAddMenu;
        public readonly int sortOrder;

        public NWRPFeatureMetadataInfo(
            string displayName,
            string menuPath,
            bool allowMultiple,
            bool volumeDriven,
            bool showInAddMenu,
            int sortOrder)
        {
            this.displayName = displayName;
            this.menuPath = menuPath;
            this.allowMultiple = allowMultiple;
            this.volumeDriven = volumeDriven;
            this.showInAddMenu = showInAddMenu;
            this.sortOrder = sortOrder;
        }
    }

    public static class NWRPFeatureMetadataUtility
    {
        private static readonly Dictionary<Type, NWRPFeatureMetadataInfo> s_MetadataCache =
            new Dictionary<Type, NWRPFeatureMetadataInfo>();

        public static NWRPFeatureMetadataInfo Get(Type featureType)
        {
            if (featureType == null)
            {
                return default;
            }

            if (s_MetadataCache.TryGetValue(
                    featureType,
                    out NWRPFeatureMetadataInfo metadata))
            {
                return metadata;
            }

            metadata = CreateMetadata(featureType);
            s_MetadataCache[featureType] = metadata;
            return metadata;
        }

        private static NWRPFeatureMetadataInfo CreateMetadata(Type featureType)
        {
            NWRPFeatureMetadataAttribute attribute =
                featureType.GetCustomAttribute<NWRPFeatureMetadataAttribute>();
            string typeName = featureType.Name;
            if (attribute == null)
            {
                return new NWRPFeatureMetadataInfo(
                    typeName,
                    typeName,
                    allowMultiple: true,
                    volumeDriven: false,
                    showInAddMenu: false,
                    sortOrder: 0);
            }

            string displayName = string.IsNullOrEmpty(attribute.DisplayName)
                ? typeName
                : attribute.DisplayName;
            string menuPath = string.IsNullOrEmpty(attribute.MenuPath)
                ? displayName
                : attribute.MenuPath;

            return new NWRPFeatureMetadataInfo(
                displayName,
                menuPath,
                attribute.AllowMultiple,
                attribute.VolumeDriven,
                attribute.ShowInAddMenu,
                attribute.SortOrder);
        }

        public static bool AllowsMultiple(NWRPFeature feature)
        {
            return feature == null || Get(feature.GetType()).allowMultiple;
        }
    }
}
