using System;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace TaskbarEmberOverlay.Models
{
    public class AppSettings
    {
        [JsonPropertyName("targetFps")]
        public int TargetFps { get; set; } = 30;

        [JsonPropertyName("edgeThickness")]
        public double EdgeThickness { get; set; } = 8.0;

        [JsonPropertyName("intensity")]
        public double Intensity { get; set; } = 1.0;

        [JsonPropertyName("reducedMotion")]
        public bool? ReducedMotion { get; set; } = null;

        [JsonPropertyName("showSparks")]
        public bool ShowSparks { get; set; } = true;

        [JsonPropertyName("showCracks")]
        public bool ShowCracks { get; set; } = true;

        [JsonPropertyName("glowRadius")]
        public double GlowRadius { get; set; } = 3.0;

        [JsonPropertyName("primaryColor")]
        public string PrimaryColor { get; set; } = "#FF5722";

        [JsonPropertyName("hotColor")]
        public string HotColor { get; set; } = "#FFE082";

        [JsonPropertyName("baseRedColor")]
        public string BaseRedColor { get; set; } = "#B71C1C";

        public void Sanitize()
        {
            TargetFps = Math.Clamp(TargetFps, 10, 60);
            EdgeThickness = Math.Clamp(EdgeThickness, 1.0, 15.0);
            Intensity = Math.Clamp(Intensity, 0.1, 2.5);
            GlowRadius = Math.Clamp(GlowRadius, 0.0, 10.0);

            if (!IsValidHex(PrimaryColor)) PrimaryColor = "#FF5722";
            if (!IsValidHex(HotColor)) HotColor = "#FFE082";
            if (!IsValidHex(BaseRedColor)) BaseRedColor = "#B71C1C";
        }

        private static bool IsValidHex(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return false;
            if (hex.Length != 7 && hex.Length != 9) return false;
            if (hex[0] != '#') return false;
            for (int i = 1; i < hex.Length; i++)
            {
                char c = hex[i];
                bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!isHex) return false;
            }
            return true;
        }

        public Color GetPrimaryMediaColor() => ParseHexToColor(PrimaryColor, Color.FromRgb(255, 87, 34));
        public Color GetHotMediaColor() => ParseHexToColor(HotColor, Color.FromRgb(255, 224, 130));
        public Color GetBaseRedMediaColor() => ParseHexToColor(BaseRedColor, Color.FromRgb(183, 28, 28));

        private static Color ParseHexToColor(string hex, Color fallback)
        {
            try
            {
                var converted = ColorConverter.ConvertFromString(hex);
                return converted is Color c ? c : fallback;
            }
            catch
            {
                return fallback;
            }
        }
    }
}
