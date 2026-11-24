using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TaskbarEmberOverlay.Models;

namespace TaskbarEmberOverlay.Services
{
    public class EmberEngine
    {
        private static readonly List<BitmapSource> _sideAnimationFrames = new();
        private static readonly List<BitmapSource> _sideAnimationFramesMirrored = new();
        private static bool _framesLoaded;
        private static readonly object _framesLock = new();

        private readonly List<MoltenNeedle> _needles = new();
        private readonly List<EmberSpark> _sparks = new();
        private readonly List<Rect> _freeSpaces = new();
        private readonly Random _rand;

        public static int SideAnimationFrameCount => _sideAnimationFrames.Count;
        public static string LastError { get; set; } = "";

        public double Width { get; private set; }
        public double Height { get; private set; }
        public double Thickness { get; private set; }

        public IReadOnlyList<MoltenNeedle> Needles => _needles;
        public IReadOnlyList<EmberSpark> Sparks => _sparks;
        public IReadOnlyList<Rect> FreeSpaces => _freeSpaces;

        public EmberEngine(int seed = 42)
        {
            _rand = new Random(seed);
            EnsureFramesLoaded();
        }

        private static BitmapSource ApplyLumaKey(BitmapSource source)
        {
            var formatted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int width = formatted.PixelWidth;
            int height = formatted.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            formatted.CopyPixels(pixels, stride, 0);

            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte b = pixels[i];
                byte g = pixels[i + 1];
                byte r = pixels[i + 2];
                int maxVal = Math.Max(r, Math.Max(g, b));

                // Smooth luma key: below 18 is 100% transparent. Above 120 is fully opaque.
                byte alpha = (byte)Math.Clamp((maxVal - 18) * 255 / (120 - 18), 0, 255);
                pixels[i + 3] = alpha;
            }

            // BitmapSource.Create works on any thread (including MTA test runner threads)
            var result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            result.Freeze();
            return result;
        }

        private static BitmapSource CreateHorizontallyFlipped(BitmapSource source)
        {
            var tb = new TransformedBitmap(source, new ScaleTransform(-1.0, 1.0, source.PixelWidth / 2.0, 0));
            tb.Freeze();
            return tb;
        }

        private static void EnsureFramesLoaded()
        {
            if (_framesLoaded) return;
            lock (_framesLock)
            {
                if (_framesLoaded) return;
                _framesLoaded = true;

                // 1. Try Pack URI embedded resources
                try
                {
                    // Ensure pack URI scheme is registered
                    if (!UriParser.IsKnownScheme("pack"))
                    {
                        System.IO.Packaging.PackUriHelper.Create(new Uri("http://localhost"));
                    }
                }
                catch { }

                for (int i = 1; i <= 60; i++)
                {
                    try
                    {
                        var uri = new Uri($"pack://application:,,,/TaskbarEmberOverlay;component/Assets/SideAnimation/frame_{i:D3}.png", UriKind.Absolute);
                        var sri = Application.GetResourceStream(uri);
                        if (sri == null)
                        {
                            uri = new Uri($"pack://application:,,,/Assets/SideAnimation/frame_{i:D3}.png", UriKind.Absolute);
                            sri = Application.GetResourceStream(uri);
                        }

                        if (sri != null)
                        {
                            using var stream = sri.Stream;
                            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                            if (decoder.Frames.Count > 0)
                            {
                                var keyed = ApplyLumaKey(decoder.Frames[0]);
                                _sideAnimationFrames.Add(keyed);
                                _sideAnimationFramesMirrored.Add(CreateHorizontallyFlipped(keyed));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LastError = "Pack: " + ex.Message;
                    }
                }

                // 2. Fallback to candidate disk directories if pack URI stream wasn't found
                if (_sideAnimationFrames.Count == 0)
                {
                    try
                    {
                        string[] candidateDirs = new[]
                        {
                            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "SideAnimation"),
                            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "TaskbarEmberOverlay", "Assets", "SideAnimation"),
                            @"C:\Users\DELL\Downloads\custom taskbar\TaskbarEmberOverlay\src\TaskbarEmberOverlay\Assets\SideAnimation",
                            @"C:\Users\DELL\Downloads\custom taskbar\TaskbarEmberOverlay\artifacts\publish\win-x64\Assets\SideAnimation"
                        };

                        foreach (var dir in candidateDirs)
                        {
                            if (System.IO.Directory.Exists(dir))
                            {
                                var files = System.IO.Directory.GetFiles(dir, "frame_*.png");
                                Array.Sort(files);
                                if (files.Length > 0)
                                {
                                    foreach (var f in files)
                                    {
                                        using var fs = System.IO.File.OpenRead(f);
                                        var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                                        if (decoder.Frames.Count > 0)
                                        {
                                            var keyed = ApplyLumaKey(decoder.Frames[0]);
                                            _sideAnimationFrames.Add(keyed);
                                            _sideAnimationFramesMirrored.Add(CreateHorizontallyFlipped(keyed));
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LastError = "Disk: " + ex.ToString();
                    }
                }
            }
        }

        public void InitializeDimensions(double width, double height, double thickness, IEnumerable<Rect>? freeSpaces = null)
        {
            Width = Math.Max(10, width);
            Height = Math.Max(10, height);
            Thickness = Math.Max(2.0, thickness);

            _freeSpaces.Clear();
            if (freeSpaces != null)
            {
                _freeSpaces.AddRange(freeSpaces);
            }

            _needles.Clear();
            _sparks.Clear();

            // 1. Perimeter Needles
            int perimeterCount = CalculateTargetNeedleCount(Width, Height);
            for (int i = 0; i < perimeterCount; i++)
            {
                var needle = CreatePerimeterNeedle();
                needle.Age = _rand.NextDouble() * needle.MaxLife;
                _needles.Add(needle);
            }

            // 2. Free Space Needles (Fallback only if video frames could not be loaded)
            if (_sideAnimationFrames.Count == 0)
            {
                foreach (var fs in _freeSpaces)
                {
                    int fsNeedleCount = (int)(fs.Width / 6.0);
                    for (int i = 0; i < fsNeedleCount; i++)
                    {
                        var needle = CreateFreeSpaceNeedle(fs);
                        needle.Age = _rand.NextDouble() * needle.MaxLife;
                        _needles.Add(needle);
                    }
                }
            }
        }

        private static int CalculateTargetNeedleCount(double width, double height)
        {
            double perimeter = (width + height) * 2;
            int count = (int)(perimeter / 7.0);
            return Math.Clamp(count, 40, 600);
        }

        private MoltenNeedle CreatePerimeterNeedle()
        {
            double r = _rand.NextDouble();
            EdgeSide side;
            Point center;
            double angle = GetRandomCrackleAngle();

            if (r < 0.45) // Top Edge
            {
                side = EdgeSide.Top;
                double x = _rand.NextDouble() * Width;
                double y = _rand.NextDouble() * (Thickness - 1.0) + 0.5;
                center = new Point(x, y);
            }
            else if (r < 0.80) // Bottom Edge
            {
                side = EdgeSide.Bottom;
                double x = _rand.NextDouble() * Width;
                double y = Height - Thickness + _rand.NextDouble() * (Thickness - 1.0) + 0.5;
                center = new Point(x, y);
            }
            else if (r < 0.90) // Left End
            {
                side = EdgeSide.Left;
                double x = _rand.NextDouble() * (Thickness * 1.5) + 0.5;
                double y = _rand.NextDouble() * Height;
                center = new Point(x, y);
            }
            else // Right End
            {
                side = EdgeSide.Right;
                double x = Width - (Thickness * 1.5) + _rand.NextDouble() * (Thickness * 1.5) - 0.5;
                double y = _rand.NextDouble() * Height;
                center = new Point(x, y);
            }

            double length = _rand.NextDouble() * 22.0 + 14.0;
            double maxLife = _rand.NextDouble() * 0.7 + 0.4;
            double peakHeat = _rand.NextDouble() * 0.35 + 0.65;
            double phase = _rand.NextDouble() * Math.PI * 2;

            return new MoltenNeedle
            {
                Center = center,
                Angle = angle,
                Length = length,
                Age = 0,
                MaxLife = maxLife,
                PeakHeat = peakHeat,
                Phase = phase,
                Side = side
            };
        }

        public void UpdateFreeSpaces(IEnumerable<Rect> freeSpaces)
        {
            _freeSpaces.Clear();
            _freeSpaces.AddRange(freeSpaces);

            // Clean up free-space needles that fall outside updated free space rectangles
            _needles.RemoveAll(n =>
            {
                if (n.Side == EdgeSide.FreeSpace)
                {
                    foreach (var fs in _freeSpaces)
                    {
                        if (fs.Contains(n.Center)) return false;
                    }
                    return true;
                }
                return false;
            });

            // Only generate procedural needles in free spaces if video frames could not be loaded
            if (_sideAnimationFrames.Count == 0)
            {
                foreach (var fs in _freeSpaces)
                {
                    int currentCount = 0;
                    foreach (var n in _needles)
                    {
                        if (n.Side == EdgeSide.FreeSpace && fs.Contains(n.Center)) currentCount++;
                    }

                    int targetCount = (int)(fs.Width / 6.0);
                    for (int i = currentCount; i < targetCount; i++)
                    {
                        var needle = CreateFreeSpaceNeedle(fs);
                        needle.Age = _rand.NextDouble() * needle.MaxLife;
                        _needles.Add(needle);
                    }
                }
            }
        }

        private MoltenNeedle CreateFreeSpaceNeedle(Rect fs)
        {
            double padX = Math.Min(6.0, fs.Width * 0.08);
            double padY = 3.0;
            double minX = fs.Left + padX;
            double maxX = Math.Max(minX, fs.Right - padX);
            double minY = fs.Top + padY;
            double maxY = Math.Max(minY, fs.Bottom - padY);

            double x = minX + _rand.NextDouble() * (maxX - minX);
            double y = minY + _rand.NextDouble() * (maxY - minY);
            double angle = GetRandomCrackleAngle();
            double length = _rand.NextDouble() * 26.0 + 14.0;
            double maxLife = _rand.NextDouble() * 0.7 + 0.4;
            double peakHeat = _rand.NextDouble() * 0.35 + 0.65;
            double phase = _rand.NextDouble() * Math.PI * 2;

            return new MoltenNeedle
            {
                Center = new Point(x, y),
                Angle = angle,
                Length = length,
                Age = 0,
                MaxLife = maxLife,
                PeakHeat = peakHeat,
                Phase = phase,
                Side = EdgeSide.FreeSpace
            };
        }

        private double GetRandomCrackleAngle()
        {
            double deg = _rand.Next(6) switch
            {
                0 => _rand.NextDouble() * 45.0 + 20.0,
                1 => -(_rand.NextDouble() * 45.0 + 20.0),
                2 => _rand.NextDouble() * 20.0 + 65.0,
                3 => -(_rand.NextDouble() * 20.0 + 65.0),
                4 => _rand.NextDouble() * 15.0 + 10.0,
                _ => -(_rand.NextDouble() * 15.0 + 10.0)
            };
            return deg * (Math.PI / 180.0);
        }

        public void Update(double dt, double timeSeconds, AppSettings settings)
        {
            if (dt <= 0) return;

            for (int i = 0; i < _needles.Count; i++)
            {
                var n = _needles[i];
                n.Age += dt;
                if (n.Age >= n.MaxLife)
                {
                    if (n.Side == EdgeSide.FreeSpace)
                    {
                        Rect matchingFs = Rect.Empty;
                        foreach (var fs in _freeSpaces)
                        {
                            if (fs.Contains(n.Center))
                            {
                                matchingFs = fs;
                                break;
                            }
                        }

                        _needles[i] = !matchingFs.IsEmpty ? CreateFreeSpaceNeedle(matchingFs) : CreatePerimeterNeedle();
                    }
                    else
                    {
                        _needles[i] = CreatePerimeterNeedle();
                    }
                }
                else if (settings.ShowSparks && _sparks.Count < 40 && _rand.NextDouble() < 0.05)
                {
                    double heat = n.GetCurrentHeat(timeSeconds);
                    if (heat > 0.75)
                    {
                        SpawnSparkNearNeedle(n);
                    }
                }
            }

            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                var s = _sparks[i];
                s.Life += dt;
                if (s.Life >= s.MaxLife)
                {
                    _sparks.RemoveAt(i);
                    continue;
                }

                s.Position += s.Velocity * dt;

                // Ensure perimeter sparks stay strictly within the perimeter band unless in free space
                bool inFreeSpace = false;
                for (int fsIdx = 0; fsIdx < _freeSpaces.Count; fsIdx++)
                {
                    if (_freeSpaces[fsIdx].Contains(s.Position))
                    {
                        inFreeSpace = true;
                        break;
                    }
                }

                if (!inFreeSpace)
                {
                    if (s.Side == EdgeSide.Top && s.Position.Y > Thickness)
                    {
                        _sparks.RemoveAt(i);
                        continue;
                    }
                    if (s.Side == EdgeSide.Bottom && s.Position.Y < Height - Thickness)
                    {
                        _sparks.RemoveAt(i);
                        continue;
                    }
                    if (s.Side == EdgeSide.Left && s.Position.X > Thickness * 1.5)
                    {
                        _sparks.RemoveAt(i);
                        continue;
                    }
                    if (s.Side == EdgeSide.Right && s.Position.X < Width - Thickness * 1.5)
                    {
                        _sparks.RemoveAt(i);
                        continue;
                    }
                }

                double lifeRatio = s.Life / s.MaxLife;
                s.Alpha = Math.Max(0.0, 1.0 - lifeRatio);
            }
        }

        private void SpawnSparkNearNeedle(MoltenNeedle n)
        {
            var endpts = n.GetEndpoints();
            Point pos = _rand.NextDouble() < 0.5 ? endpts.P1 : endpts.P2;
            double speed = _rand.NextDouble() * 12.0 + 4.0;
            
            double vx = Math.Cos(n.Angle) * speed;
            double vy = Math.Sin(n.Angle) * speed;

            if (n.Side == EdgeSide.FreeSpace)
            {
                // Free space sparks drift gently upward
                vy = -Math.Abs(vy) - 3.0;
                vx *= 0.4;
            }
            else if (n.Side == EdgeSide.Top)
            {
                pos.Y = Math.Clamp(pos.Y, 0.5, Thickness);
                if (vy > 0) vy = -vy * 0.2;
            }
            else if (n.Side == EdgeSide.Bottom)
            {
                pos.Y = Math.Clamp(pos.Y, Height - Thickness, Height - 0.5);
                if (vy < 0) vy = -vy * 0.2;
            }
            else if (n.Side == EdgeSide.Left)
            {
                pos.X = Math.Clamp(pos.X, 0.5, Thickness * 1.5);
                if (vx > 0) vx = -vx * 0.2;
            }
            else if (n.Side == EdgeSide.Right)
            {
                pos.X = Math.Clamp(pos.X, Width - Thickness * 1.5, Width - 0.5);
                if (vx < 0) vx = -vx * 0.2;
            }

            _sparks.Add(new EmberSpark
            {
                Position = pos,
                Velocity = new Vector(vx, vy),
                Life = 0,
                MaxLife = _rand.NextDouble() * 0.6 + 0.3,
                Radius = _rand.NextDouble() * 1.2 + 0.7,
                Alpha = 1.0,
                Side = n.Side
            });
        }

        public void Render(DrawingContext dc, double timeSeconds, AppSettings settings, bool isStatic)
        {
            double intensity = settings.Intensity;
            Color primaryColor = settings.GetPrimaryMediaColor();
            Color hotColor = settings.GetHotMediaColor();
            Color baseRedColor = settings.GetBaseRedMediaColor();

            // 1. Free Space Side Animation (Luma-keyed video frames from reference with alpha transparency)
            if (_freeSpaces.Count > 0)
            {
                int frameIdx = _sideAnimationFrames.Count > 0 
                    ? (isStatic ? 0 : (int)((timeSeconds * 20.0) % _sideAnimationFrames.Count)) 
                    : -1;

                for (int fsi = 0; fsi < _freeSpaces.Count; fsi++)
                {
                    var fs = _freeSpaces[fsi];
                    if (frameIdx >= 0)
                    {
                        // Use mirrored frames for right-wing space to add natural symmetry
                        var frameList = (fsi % 2 == 1 && _sideAnimationFramesMirrored.Count > 0)
                            ? _sideAnimationFramesMirrored : _sideAnimationFrames;
                        var frameBmp = frameList[frameIdx];

                        // Soft horizontal feathering at outer and inner ends of free space (16-24 DIPs)
                        // This eliminates any harsh vertical lines or cut black strips
                        double fadeDist = Math.Min(22.0, fs.Width * 0.25);
                        double fadeFrac = fs.Width > 0 ? Math.Clamp(fadeDist / fs.Width, 0.04, 0.35) : 0.1;

                        var fadeMask = new LinearGradientBrush
                        {
                            StartPoint = new Point(0, 0.5),
                            EndPoint = new Point(1, 0.5),
                            GradientStops = new GradientStopCollection
                            {
                                new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.0),
                                new GradientStop(Color.FromArgb((byte)Math.Clamp((int)(255 * intensity), 40, 255), 0, 0, 0), fadeFrac),
                                new GradientStop(Color.FromArgb((byte)Math.Clamp((int)(255 * intensity), 40, 255), 0, 0, 0), 1.0 - fadeFrac),
                                new GradientStop(Color.FromArgb(0, 0, 0, 0), 1.0)
                            }
                        };
                        fadeMask.Freeze();

                        dc.PushOpacityMask(fadeMask);
                        dc.DrawImage(frameBmp, fs);
                        dc.Pop();
                    }
                    else
                    {
                        byte smokeAlpha = (byte)Math.Clamp((int)(110 * intensity), 30, 210);
                        byte coreGlowAlpha = (byte)Math.Clamp((int)(140 * intensity), 40, 230);
                        var smokeBrush = new LinearGradientBrush
                        {
                            StartPoint = new Point(0, 0.5),
                            EndPoint = new Point(1, 0.5),
                            GradientStops = new GradientStopCollection
                            {
                                new GradientStop(Color.FromArgb(0, baseRedColor.R, baseRedColor.G, baseRedColor.B), 0.0),
                                new GradientStop(Color.FromArgb(smokeAlpha, (byte)(baseRedColor.R * 0.75), 0, 0), 0.12),
                                new GradientStop(Color.FromArgb(coreGlowAlpha, (byte)(baseRedColor.R * 0.9), (byte)(primaryColor.G * 0.25), 0), 0.5),
                                new GradientStop(Color.FromArgb(smokeAlpha, (byte)(baseRedColor.R * 0.75), 0, 0), 0.88),
                                new GradientStop(Color.FromArgb(0, baseRedColor.R, baseRedColor.G, baseRedColor.B), 1.0)
                            }
                        };
                        smokeBrush.Freeze();
                        dc.DrawRoundedRectangle(smokeBrush, null, fs, 6.0, 6.0);
                    }
                }
            }

            // 2. Ambient perimeter base glow
            byte ambientAlpha = (byte)Math.Clamp((int)(110 * intensity), 20, 220);
            var ambientPen = new Pen(new SolidColorBrush(Color.FromArgb(ambientAlpha, baseRedColor.R, baseRedColor.G, baseRedColor.B)), Thickness);
            ambientPen.Freeze();

            double halfThick = Thickness / 2.0;
            dc.DrawRectangle(null, ambientPen, new Rect(halfThick, halfThick, Math.Max(0, Width - Thickness), Math.Max(0, Height - Thickness)));

            // 3. Dense Molten Needles Lattice (Perimeter + Free Spaces)
            if (settings.ShowCracks)
            {
                foreach (var needle in _needles)
                {
                    double heat = isStatic ? needle.PeakHeat : needle.GetCurrentHeat(timeSeconds);
                    heat *= intensity;
                    if (heat <= 0.05) continue;

                    var (p1, p2) = needle.GetEndpoints();

                    // Layer 1: Crimson glowing halo
                    byte haloAlpha = (byte)Math.Clamp((int)(120 * heat), 0, 255);
                    var haloPen = new Pen(new SolidColorBrush(Color.FromArgb(haloAlpha, baseRedColor.R, baseRedColor.G, baseRedColor.B)), 4.0);
                    haloPen.Freeze();
                    dc.DrawLine(haloPen, p1, p2);

                    // Layer 2: Fiery molten orange body
                    byte bodyAlpha = (byte)Math.Clamp((int)(230 * heat), 0, 255);
                    var bodyPen = new Pen(new SolidColorBrush(Color.FromArgb(bodyAlpha, primaryColor.R, primaryColor.G, primaryColor.B)), 2.0);
                    bodyPen.Freeze();
                    dc.DrawLine(bodyPen, p1, p2);

                    // Layer 3: Hot molten white/yellow core (when heat > 0.45)
                    if (heat > 0.45)
                    {
                        double coreFraction = (heat - 0.45) / 0.55;
                        byte coreAlpha = (byte)Math.Clamp((int)(255 * coreFraction), 0, 255);
                        var corePen = new Pen(new SolidColorBrush(Color.FromArgb(coreAlpha, hotColor.R, hotColor.G, hotColor.B)), 1.0);
                        corePen.Freeze();
                        dc.DrawLine(corePen, p1, p2);
                    }
                }
            }

            // 4. Drifting sparks
            if (!isStatic && settings.ShowSparks)
            {
                foreach (var spark in _sparks)
                {
                    byte a = (byte)Math.Clamp((int)(255 * spark.Alpha * intensity), 0, 255);
                    if (a == 0) continue;

                    Color c = (spark.Life / spark.MaxLife < 0.4) ? hotColor : primaryColor;
                    var brush = new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B));
                    brush.Freeze();

                    dc.DrawEllipse(brush, null, spark.Position, spark.Radius, spark.Radius);
                }
            }
        }
    }
}
