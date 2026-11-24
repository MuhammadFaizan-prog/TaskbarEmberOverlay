using System;
using System.Windows;

namespace TaskbarEmberOverlay.Models
{
    public enum EdgeSide
    {
        Top,
        Bottom,
        Left,
        Right,
        FreeSpace
    }

    public class EmberSpark
    {
        public Point Position;
        public Vector Velocity;
        public double Life;
        public double MaxLife;
        public double Radius;
        public double Alpha;
        public EdgeSide Side;
    }

    public class MoltenNeedle
    {
        public Point Center;
        public double Angle;
        public double Length;
        public double Age;
        public double MaxLife;
        public double PeakHeat;
        public double Phase;
        public EdgeSide Side;

        public double GetCurrentHeat(double timeSeconds)
        {
            if (Age >= MaxLife) return 0.0;
            double progress = Age / MaxLife;
            double baseHeat;
            if (progress < 0.25)
            {
                baseHeat = progress / 0.25;
            }
            else if (progress < 0.65)
            {
                baseHeat = 1.0;
            }
            else
            {
                baseHeat = (1.0 - progress) / 0.35;
            }

            double flicker = Math.Sin(timeSeconds * 28.0 + Phase) * 0.15;
            return Math.Clamp((baseHeat + flicker) * PeakHeat, 0.0, 1.0);
        }

        public (Point P1, Point P2) GetEndpoints()
        {
            double halfLen = Length / 2.0;
            double dx = Math.Cos(Angle) * halfLen;
            double dy = Math.Sin(Angle) * halfLen;
            return (new Point(Center.X - dx, Center.Y - dy), new Point(Center.X + dx, Center.Y + dy));
        }
    }
}
