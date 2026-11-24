using System.IO;
using System.Text.Json;
using TaskbarEmberOverlay.Models;
using TaskbarEmberOverlay.Services;
using Xunit;

namespace TaskbarEmberOverlay.Tests
{
    public class SettingsTests
    {
        [Fact]
        public void DefaultSettings_HaveValidSanitizedValues()
        {
            var settings = new AppSettings();
            settings.Sanitize();

            Assert.Equal(30, settings.TargetFps);
            Assert.Equal(8.0, settings.EdgeThickness);
            Assert.Equal(1.0, settings.Intensity);
            Assert.Null(settings.ReducedMotion);
            Assert.True(settings.ShowSparks);
            Assert.True(settings.ShowCracks);
            Assert.Equal("#FF5722", settings.PrimaryColor);
            Assert.Equal("#FFE082", settings.HotColor);
            Assert.Equal("#B71C1C", settings.BaseRedColor);
        }

        [Theory]
        [InlineData(5, 10)]    // Below min
        [InlineData(120, 60)]  // Above max
        [InlineData(30, 30)]   // Within range
        public void Sanitize_ClampsFpsToSafeRange(int inputFps, int expectedFps)
        {
            var settings = new AppSettings { TargetFps = inputFps };
            settings.Sanitize();
            Assert.Equal(expectedFps, settings.TargetFps);
        }

        [Theory]
        [InlineData(0.1, 1.0)]   // Below min thickness
        [InlineData(50.0, 15.0)] // Above max thickness
        public void Sanitize_ClampsEdgeThickness(double inputThickness, double expectedThickness)
        {
            var settings = new AppSettings { EdgeThickness = inputThickness };
            settings.Sanitize();
            Assert.Equal(expectedThickness, settings.EdgeThickness);
        }

        [Fact]
        public void Sanitize_ResetsInvalidHexColorsToDefaults()
        {
            var settings = new AppSettings
            {
                PrimaryColor = "invalid_color",
                HotColor = "#12",
                BaseRedColor = ""
            };
            settings.Sanitize();

            Assert.Equal("#FF5722", settings.PrimaryColor);
            Assert.Equal("#FFE082", settings.HotColor);
            Assert.Equal("#B71C1C", settings.BaseRedColor);
        }

        [Fact]
        public void SettingsService_SavesAndLoadsJsonCleanly()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "TaskbarEmberOverlay_Test_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string testFilePath = Path.Combine(tempDir, "settings.json");

            try
            {
                var service = new SettingsService(testFilePath);
                var original = new AppSettings
                {
                    TargetFps = 25,
                    EdgeThickness = 4.5,
                    Intensity = 1.2,
                    PrimaryColor = "#FFAA00"
                };

                service.Save(original);
                Assert.True(File.Exists(testFilePath));

                var loaded = service.LoadOrCreate();
                Assert.Equal(25, loaded.TargetFps);
                Assert.Equal(4.5, loaded.EdgeThickness);
                Assert.Equal(1.2, loaded.Intensity);
                Assert.Equal("#FFAA00", loaded.PrimaryColor);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }
    }
}
