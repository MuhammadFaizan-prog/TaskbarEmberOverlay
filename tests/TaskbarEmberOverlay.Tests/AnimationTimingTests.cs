using System.Linq;
using TaskbarEmberOverlay.Models;
using TaskbarEmberOverlay.Services;
using Xunit;

namespace TaskbarEmberOverlay.Tests
{
    public class AnimationTimingTests
    {
        [Fact]
        public void EmberEngine_InitializesNeedlesOnAllEdges()
        {
            var engine = new EmberEngine(seed: 123);
            engine.InitializeDimensions(1920, 48, 8.0);

            Assert.True(engine.Needles.Count > 20, "Should generate multiple molten needles");
            Assert.Contains(engine.Needles, c => c.Side == EdgeSide.Top);
            Assert.Contains(engine.Needles, c => c.Side == EdgeSide.Bottom);
            Assert.Contains(engine.Needles, c => c.Side == EdgeSide.Left);
            Assert.Contains(engine.Needles, c => c.Side == EdgeSide.Right);
        }

        [Fact]
        public void EmberEngine_SparksStayConfinedToPerimeter()
        {
            var engine = new EmberEngine(seed: 456);
            double width = 1920;
            double height = 48;
            double thickness = 8.0;
            engine.InitializeDimensions(width, height, thickness);

            var settings = new AppSettings { ShowSparks = true };

            for (int i = 0; i < 50; i++)
            {
                engine.Update(0.033, i * 0.033, settings);

                foreach (var spark in engine.Sparks)
                {
                    Assert.False(double.IsNaN(spark.Position.X));
                    Assert.False(double.IsNaN(spark.Position.Y));

                    bool onTop = spark.Position.Y <= thickness + 1.0;
                    bool onBottom = spark.Position.Y >= height - thickness - 1.0;
                    bool onLeft = spark.Position.X <= thickness * 1.5 + 1.0;
                    bool onRight = spark.Position.X >= width - thickness * 1.5 - 1.0;

                    Assert.True(onTop || onBottom || onLeft || onRight,
                        $"Spark at ({spark.Position.X}, {spark.Position.Y}) leaked outside perimeter band into interior!");
                }
            }
        }

        [Fact]
        public void EmberEngine_SparksExpireGracefullyAfterLifespan()
        {
            var engine = new EmberEngine(seed: 789);
            engine.InitializeDimensions(1000, 48, 8.0);

            var settings = new AppSettings { ShowSparks = true };

            for (int i = 0; i < 10; i++) engine.Update(0.033, i * 0.033, settings);

            settings.ShowSparks = false;
            for (int i = 0; i < 150; i++)
            {
                engine.Update(0.033, (10 + i) * 0.033, settings);
            }

            Assert.Empty(engine.Sparks);
        }
    }
}
