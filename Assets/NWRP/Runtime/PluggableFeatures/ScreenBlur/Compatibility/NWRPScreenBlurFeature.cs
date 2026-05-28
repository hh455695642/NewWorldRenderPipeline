using System;

namespace NWRP
{
    [Obsolete("Use ScreenBlurFeature. This shim is kept for serialized assets and editor tooling that reflect the old type name.")]
    [NWRPFeatureMetadata(
        "Screen Blur",
        MenuPath = "Post Processing/Screen Blur",
        ShowInAddMenu = false,
        VolumeDriven = true,
        SortOrder = 300)]
    public sealed class NWRPScreenBlurFeature : ScreenBlurFeature
    {
    }
}
