using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace NWRP
{
    public readonly struct NWRPFrameResourceDesc : IEquatable<NWRPFrameResourceDesc>
    {
        public readonly int width;
        public readonly int height;
        public readonly GraphicsFormat graphicsFormat;
        public readonly int depthBufferBits;
        public readonly FilterMode filterMode;
        public readonly bool isDepth;

        private NWRPFrameResourceDesc(
            int width,
            int height,
            GraphicsFormat graphicsFormat,
            int depthBufferBits,
            FilterMode filterMode,
            bool isDepth)
        {
            this.width = Mathf.Max(width, 1);
            this.height = Mathf.Max(height, 1);
            this.graphicsFormat = graphicsFormat;
            this.depthBufferBits = Mathf.Max(depthBufferBits, 0);
            this.filterMode = filterMode;
            this.isDepth = isDepth;
        }

        public static NWRPFrameResourceDesc Color(
            int width,
            int height,
            GraphicsFormat graphicsFormat,
            FilterMode filterMode)
        {
            return new NWRPFrameResourceDesc(
                width,
                height,
                graphicsFormat,
                0,
                filterMode,
                false);
        }

        public bool Equals(NWRPFrameResourceDesc other)
        {
            return width == other.width
                && height == other.height
                && graphicsFormat == other.graphicsFormat
                && depthBufferBits == other.depthBufferBits
                && filterMode == other.filterMode
                && isDepth == other.isDepth;
        }

        public override bool Equals(object obj)
        {
            return obj is NWRPFrameResourceDesc other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = width;
                hash = (hash * 397) ^ height;
                hash = (hash * 397) ^ (int)graphicsFormat;
                hash = (hash * 397) ^ depthBufferBits;
                hash = (hash * 397) ^ (int)filterMode;
                hash = (hash * 397) ^ isDepth.GetHashCode();
                return hash;
            }
        }
    }

    public readonly struct NWRPFrameResourceHandle
    {
        public readonly int logicalId;
        public readonly int physicalId;
        public readonly int firstUsePassIndex;
        public readonly int lastUsePassIndex;
        public readonly NWRPFrameResourceDesc desc;

        internal NWRPFrameResourceHandle(
            int logicalId,
            int physicalId,
            int firstUsePassIndex,
            int lastUsePassIndex,
            NWRPFrameResourceDesc desc)
        {
            this.logicalId = logicalId;
            this.physicalId = physicalId;
            this.firstUsePassIndex = firstUsePassIndex;
            this.lastUsePassIndex = lastUsePassIndex;
            this.desc = desc;
        }
    }

    public sealed class NWRPTransientResourceAllocator
    {
        private struct PhysicalResource
        {
            public NWRPFrameResourceDesc desc;
            public int lastUsePassIndex;
        }

        private readonly List<PhysicalResource> _physicalResources =
            new List<PhysicalResource>(8);

        public int LogicalResourceCount { get; private set; }
        public int PhysicalResourceCount => _physicalResources.Count;

        public void Reset()
        {
            LogicalResourceCount = 0;
            _physicalResources.Clear();
        }

        public NWRPFrameResourceHandle Allocate(
            NWRPFrameResourceDesc desc,
            int firstUsePassIndex,
            int lastUsePassIndex)
        {
            int safeFirstUse = Mathf.Max(firstUsePassIndex, 0);
            int safeLastUse = Mathf.Max(lastUsePassIndex, safeFirstUse);
            int logicalId = LogicalResourceCount++;
            int physicalId = FindReusablePhysicalResource(desc, safeFirstUse);
            if (physicalId < 0)
            {
                physicalId = _physicalResources.Count;
                _physicalResources.Add(new PhysicalResource
                {
                    desc = desc,
                    lastUsePassIndex = safeLastUse
                });
            }
            else
            {
                PhysicalResource resource = _physicalResources[physicalId];
                resource.lastUsePassIndex = safeLastUse;
                _physicalResources[physicalId] = resource;
            }

            return new NWRPFrameResourceHandle(
                logicalId,
                physicalId,
                safeFirstUse,
                safeLastUse,
                desc);
        }

        private int FindReusablePhysicalResource(
            NWRPFrameResourceDesc desc,
            int firstUsePassIndex)
        {
            for (int i = 0; i < _physicalResources.Count; i++)
            {
                PhysicalResource resource = _physicalResources[i];
                if (resource.lastUsePassIndex < firstUsePassIndex
                    && resource.desc.Equals(desc))
                {
                    return i;
                }
            }

            return -1;
        }
    }

    public static class NWRPFrameGraphAnalyzer
    {
        public static NWRPFrameGraphData Analyze(
            IReadOnlyList<NWRPFramePassResourceUsage> usages)
        {
            NWRPFrameGraphData graph = default;
            graph.cameraColorFinalPresentPassIndex = -1;
            graph.cameraDepthLastUsePassIndex = -1;

            bool inCameraAttachmentCluster = false;
            if (usages == null)
            {
                return graph;
            }

            for (int i = 0; i < usages.Count; i++)
            {
                NWRPFramePassResourceUsage usage = usages[i];
                graph.RecordPassUsage(usage);

                if (usage.canPresentCameraColorToBackBuffer)
                {
                    graph.cameraColorFinalPresentPassIndex = i;
                }

                if (usage.UsesCameraDepth)
                {
                    graph.cameraDepthLastUsePassIndex = i;
                }

                bool usesCameraAttachments =
                    usage.UsesCameraColor || usage.UsesCameraDepth;
                if (usesCameraAttachments && !inCameraAttachmentCluster)
                {
                    graph.renderPassClusterCount++;
                    inCameraAttachmentCluster = true;
                }
                else if (!usesCameraAttachments)
                {
                    inCameraAttachmentCluster = false;
                }
            }

            graph.canDiscardCameraDepthAfterLastUse =
                graph.cameraDepthLastUsePassIndex >= 0;
            return graph;
        }
    }
}
