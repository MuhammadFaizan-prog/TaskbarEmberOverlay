using System;
using System.Windows;
using TaskbarEmberOverlay.Interop;
using TaskbarEmberOverlay.Models;
using Xunit;

namespace TaskbarEmberOverlay.Tests
{
    public class DpiConversionTests
    {
        [Theory]
        [InlineData(96, 1.0)]
        [InlineData(120, 1.25)]
        [InlineData(144, 1.5)]
        [InlineData(192, 2.0)]
        public void DpiToScale_CalculatesCorrectScaleFactor(uint dpi, double expectedScale)
        {
            double scale = TaskbarBounds.DpiToScale(dpi);
            Assert.Equal(expectedScale, scale, precision: 3);
        }

        [Fact]
        public void CalculateDipRect_Standard1080p_100PercentScale()
        {
            var phys = new NativeMethods.RECT
            {
                Left = 0,
                Top = 1040,
                Right = 1920,
                Bottom = 1080
            };

            var dip = TaskbarBounds.CalculateDipRect(phys, 1.0);

            Assert.Equal(0, dip.Left);
            Assert.Equal(1040, dip.Top);
            Assert.Equal(1920, dip.Width);
            Assert.Equal(40, dip.Height);
        }

        [Fact]
        public void CalculateDipRect_4K_150PercentScale()
        {
            var phys = new NativeMethods.RECT
            {
                Left = 0,
                Top = 2088,
                Right = 3840,
                Bottom = 2160
            };

            var dip = TaskbarBounds.CalculateDipRect(phys, 1.5);

            Assert.Equal(0, dip.Left);
            Assert.Equal(1392, dip.Top, precision: 1);
            Assert.Equal(2560, dip.Width, precision: 1);
            Assert.Equal(48, dip.Height, precision: 1);
        }

        [Fact]
        public void CalculateDipRect_SecondaryMonitor_NegativeCoordinates()
        {
            var phys = new NativeMethods.RECT
            {
                Left = -1920,
                Top = 1040,
                Right = 0,
                Bottom = 1080
            };

            var dip = TaskbarBounds.CalculateDipRect(phys, 1.0);

            Assert.Equal(-1920, dip.Left);
            Assert.Equal(1040, dip.Top);
            Assert.Equal(1920, dip.Width);
            Assert.Equal(40, dip.Height);
        }
    }
}
