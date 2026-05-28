namespace NWRP
{
    /// <summary>
    /// Global pass order contract for NWRP.
    /// Keep new passes inside this order unless a hard dependency requires otherwise.
    /// </summary>
    public enum NWRPPassEvent
    {
        BeforeShadowMap = 50,
        ShadowMap = 100,
        BeforeDepthPrepass = 150,
        DepthPrepass = 200,
        BeforeOpaque = 250,
        Opaque = 300,
        Skybox = 400,
        BeforeTransparent = 450,
        Transparent = 500,
        AfterTransparent = 550,
        AfterValleyHeightFog = 575,
        BeforePostProcess = 590,
        PostProcess = 600,
        AfterPostProcess = 650,
        DebugOverlay = 700
    }
}
