namespace IconRenderGuard;
internal static class IconRenderRules {
 // Preserve every valid caller dimension, including native multi-cell weapons.
 // A malformed axis uses the item's native cell-pixel size, then one full cell.
 internal static (int Width,int Height) Size(int width,int height,int nativeWidth,int nativeHeight)=>(width>0?width:nativeWidth>0?nativeWidth:64,height>0?height:nativeHeight>0?nativeHeight:64);
 // The icon camera activates its entire hierarchy immediately before rendering.
 // A currently inactive sibling must therefore be isolated as well.
 internal static bool Hide(bool enabled,bool forcedOff,bool owned,bool inMask)=>enabled&&!forcedOff&&!owned&&inMask;
}
