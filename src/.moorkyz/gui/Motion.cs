using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace Moorkyz
{
    public sealed class InertialPoint
    {
        public Point Position { get; private set; }
        public Vector Velocity { get; private set; }

        public InertialPoint(Point start) { Position = start; }

        public void Step(Point target, double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0 ||
                double.IsNaN(target.X) || double.IsInfinity(target.X) ||
                double.IsNaN(target.Y) || double.IsInfinity(target.Y))
                throw new ArgumentOutOfRangeException("Spring inputs must be finite and time must be nonnegative.");
            double remaining = Math.Min(seconds, 0.05);
            while (remaining > 0)
            {
                double step = Math.Min(remaining, 1.0 / 120);
                Vector acceleration = (target - Position) * 170 - Velocity * 23;
                Velocity += acceleration * step;
                Position += Velocity * step;
                remaining -= step;
            }
        }
    }

    internal sealed class DragPreview : Adorner, IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct CursorPoint { public int X; public int Y; }
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out CursorPoint point);
        private readonly AdornerLayer layer;
        private readonly ImageSource image;
        private readonly Size size;
        private readonly Point anchor;
        private readonly InertialPoint spring;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private double previousTime;
        private bool disposed;

        public DragPreview(UIElement host, FrameworkElement card, Point anchor, AdornerLayer layer)
            : base(host)
        {
            this.layer = layer;
            this.anchor = anchor;
            size = new Size(card.ActualWidth, card.ActualHeight);
            var bitmap = new RenderTargetBitmap(Math.Max(1, (int)size.Width), Math.Max(1, (int)size.Height), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(card);
            bitmap.Freeze();
            image = bitmap;
            spring = new InertialPoint(card.TranslatePoint(new Point(), host));
            IsHitTestVisible = false;
            layer.Add(this);
            CompositionTarget.Rendering += OnFrame;
        }

        private void OnFrame(object sender, EventArgs e)
        {
            CursorPoint cursor;
            if (!GetCursorPos(out cursor)) return;
            Point pointer = AdornedElement.PointFromScreen(new Point(cursor.X, cursor.Y));
            double now = clock.Elapsed.TotalSeconds;
            spring.Step(new Point(pointer.X - anchor.X, pointer.Y - anchor.Y), now - previousTime);
            previousTime = now;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext context)
        {
            context.PushTransform(new TranslateTransform(spring.Position.X, spring.Position.Y));
            context.PushTransform(new RotateTransform(Math.Max(-5, Math.Min(5, spring.Velocity.X * 0.008)), size.Width / 2, size.Height / 2));
            context.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)), null, new Rect(8, 12, size.Width, size.Height), 10, 10);
            context.DrawImage(image, new Rect(new Point(), size));
            context.Pop();
            context.Pop();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CompositionTarget.Rendering -= OnFrame;
            clock.Stop();
            layer.Remove(this);
        }
    }

    public sealed class MaterialCard : Button
    {
        private readonly AxisAngleRotation3D rotateX = new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0);
        private readonly AxisAngleRotation3D rotateY = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);
        private readonly RadialGradientBrush sunlight;
        private readonly TimeSpan duration;
        private bool motionEnabled;
        public double TiltX { get { return rotateX.Angle; } }
        public double TiltY { get { return rotateY.Angle; } }

        public MaterialCard(string label, Color accent, TimeSpan duration)
        {
            this.duration = duration;
            Width = 370;
            Height = 228;
            Padding = new Thickness(0);
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
            Margin = new Thickness(0, 0, 14, 14);
            var face = new Grid { Width = 440, Height = 250 };
            face.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = new LinearGradientBrush(Color.FromRgb(14, 27, 55), accent, 25),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 184, 167, 255)),
                BorderThickness = new Thickness(1)
            });
            var pattern = new DrawingGroup();
            pattern.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 6, 6))));
            pattern.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromArgb(12, 255, 255, 255)), null, new EllipseGeometry(new Point(2, 2), 0.65, 0.65)));
            var texture = new DrawingBrush(pattern)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 6, 6),
                ViewportUnits = BrushMappingMode.Absolute
            };
            face.Children.Add(new Border { Background = texture, CornerRadius = new CornerRadius(14), IsHitTestVisible = false });
            var imprint = new Grid { Margin = new Thickness(0, 0, 22, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            imprint.Children.Add(new TextBlock { Text = "M", FontSize = 140, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)), Margin = new Thickness(0, 2, 0, 0) });
            imprint.Children.Add(new TextBlock { Text = "M", FontSize = 140, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(Color.FromArgb(24, 242, 244, 255)) });
            face.Children.Add(imprint);
            var text = new StackPanel { Margin = new Thickness(30) };
            text.Children.Add(new TextBlock { Text = "MOORKYZ / MATERIAL STUDY", Foreground = Brushes.White, FontSize = 12 });
            text.Children.Add(new TextBlock { Text = "Local.\nOn your terms.", Foreground = Brushes.White, FontSize = 32, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 22, 0, 18) });
            text.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(210, 218, 243)), FontSize = 12 });
            face.Children.Add(text);
            sunlight = new RadialGradientBrush
            {
                Center = new Point(0.25, 0.2),
                GradientOrigin = new Point(0.25, 0.2),
                RadiusX = 0.85,
                RadiusY = 1.2
            };
            sunlight.GradientStops.Add(new GradientStop(Color.FromArgb(75, 255, 243, 217), 0));
            sunlight.GradientStops.Add(new GradientStop(Color.FromArgb(18, 184, 167, 255), 0.5));
            sunlight.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            face.Children.Add(new Border { Background = sunlight, CornerRadius = new CornerRadius(14), IsHitTestVisible = false });
            var material = new DiffuseMaterial(Brushes.White);
            Viewport2DVisual3D.SetIsVisualHostMaterial(material, true);
            var transforms = new Transform3DGroup();
            transforms.Children.Add(new RotateTransform3D(rotateX));
            transforms.Children.Add(new RotateTransform3D(rotateY));
            var viewport = new Viewport3D { ClipToBounds = false, IsHitTestVisible = false };
            viewport.Camera = new PerspectiveCamera(new Point3D(0, 0, 4.2), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), 45);
            viewport.Children.Add(new ModelVisual3D { Content = new AmbientLight(Colors.White) });
            viewport.Children.Add(new Viewport2DVisual3D
            {
                Geometry = new MeshGeometry3D
                {
                    Positions = new Point3DCollection { new Point3D(-1.75, -1, 0), new Point3D(1.75, -1, 0), new Point3D(1.75, 1, 0), new Point3D(-1.75, 1, 0) },
                    TextureCoordinates = new PointCollection { new Point(0, 1), new Point(1, 1), new Point(1, 0), new Point(0, 0) },
                    TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 }
                },
                Material = material,
                Visual = face,
                Transform = transforms
            });
            Content = viewport;
            MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (!motionEnabled) return;
                Point point = e.GetPosition(this);
                SetPointer(point.X / ActualWidth, point.Y / ActualHeight);
            };
            MouseLeave += delegate { SetPointer(0.5, 0.5); };
        }

        public void SetMotionEnabled(bool value)
        {
            motionEnabled = value;
            if (!value)
            {
                rotateX.BeginAnimation(AxisAngleRotation3D.AngleProperty, null);
                rotateY.BeginAnimation(AxisAngleRotation3D.AngleProperty, null);
                rotateX.Angle = rotateY.Angle = 0;
                sunlight.Center = sunlight.GradientOrigin = new Point(0.25, 0.2);
            }
        }

        public void SetPointer(double x, double y)
        {
            if (!motionEnabled) return;
            x = Math.Max(0, Math.Min(1, x));
            y = Math.Max(0, Math.Min(1, y));
            rotateX.BeginAnimation(AxisAngleRotation3D.AngleProperty, new DoubleAnimation((0.5 - y) * 12, duration));
            rotateY.BeginAnimation(AxisAngleRotation3D.AngleProperty, new DoubleAnimation((x - 0.5) * 16, duration));
            sunlight.Center = sunlight.GradientOrigin = new Point(x, y);
        }
    }
}
