using System;

namespace LocalSpt5Esp;

internal static class OpticZoomRules
{
    internal const float MinimumFov = .00001f, MaximumFov = 179f;

    internal static float SensitivityScale(float fov, float first, float second)
    {
        if (!float.IsFinite(fov) || !float.IsFinite(first) || !float.IsFinite(second) ||
            fov <= 0 || fov >= 180 || first <= 0 || first >= 180 || second <= 0 || second >= 180)
            return 1;
        float reference = Math.Clamp(fov, Math.Min(first, second), Math.Max(first, second));
        return (float)(Math.Tan(fov * Math.PI / 360) / Math.Tan(reference * Math.PI / 360));
    }

    internal static float Step(float fov, float wheel, float minimum = MinimumFov, float maximum = MaximumFov)
    {
        if (!float.IsFinite(fov) || fov <= 0 || fov >= 180 || !float.IsFinite(wheel) ||
            !float.IsFinite(minimum) || !float.IsFinite(maximum) || minimum <= 0 || maximum >= 180 || minimum > maximum)
            return fov;
        double tangent = Math.Tan(fov * Math.PI / 360);
        double scale = Math.Exp(-Math.Clamp(wheel, -128, 128) * Math.Log(1.1));
        return (float)Math.Clamp(Math.Atan(tangent * scale) * 360 / Math.PI, minimum, maximum);
    }
}
