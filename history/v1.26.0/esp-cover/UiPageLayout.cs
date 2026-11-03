using System;
using System.Linq;
namespace LocalSpt5Esp;
internal static class UiPageLayout {
 internal static float CardStack(float[] heights,float gap=18,float top=4,float tail=0){if(heights.Any(height=>!float.IsFinite(height)||height<0)||!float.IsFinite(gap)||gap<0)throw new ArgumentException("布局尺寸无效。");return top+heights.Sum()+heights.Length*gap+tail;}
}
