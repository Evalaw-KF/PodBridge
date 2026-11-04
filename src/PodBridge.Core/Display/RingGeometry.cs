namespace PodBridge.Core.Display;

public readonly record struct RingPoint(double X, double Y);

/// <summary>
/// The shape of a progress arc: where it starts and ends on the circle and which way an ArcSegment must go.
/// <see cref="IsEmpty"/> means "draw nothing"; <see cref="IsLargeArc"/> maps to ArcSegment.IsLargeArc.
/// The arc is always drawn clockwise.
/// </summary>
public readonly record struct RingArc(RingPoint Start, RingPoint End, double SweepDegrees, bool IsLargeArc, bool IsEmpty);

/// <summary>Arc maths for the circular battery rings. Angles are in degrees, 0 = 12 o'clock, growing clockwise.</summary>
public static class RingGeometry
{
    /// <summary>A full circle cannot be one arc segment (start and end coincide), so 100% is drawn as just under 360 degrees.</summary>
    public const double MaxSweep = 359.9;

    /// <summary>Point on the circle at the given angle (0 = top, 90 = right, 180 = bottom, 270 = left).</summary>
    public static RingPoint PointAt(double centreX, double centreY, double radius, double angleDegrees)
    {
        double rad = angleDegrees * Math.PI / 180.0;
        return new RingPoint(centreX + radius * Math.Sin(rad), centreY - radius * Math.Cos(rad));
    }

    /// <summary>The arc that shows <paramref name="percent"/> (clamped to 0..100) of a circle, starting at the top.</summary>
    public static RingArc ForPercent(double centreX, double centreY, double radius, double percent)
    {
        if (double.IsNaN(percent))
        {
            percent = 0;
        }
        percent = Math.Clamp(percent, 0, 100);
        double sweep = Math.Min(percent * 3.6, MaxSweep);
        var start = PointAt(centreX, centreY, radius, 0);
        var end = PointAt(centreX, centreY, radius, sweep);
        return new RingArc(start, end, sweep, sweep > 180.0, percent <= 0);
    }
}
