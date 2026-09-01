using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;

using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;
using Cursors = System.Windows.Input.Cursors;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace Catalyst.Windows;

public partial class ColorPickerWindow : FluentWindow
{
    private const int WheelSize = 220;
    private static BitmapSource? _cachedWheelBitmap;

    private readonly string _initialHex;
    private readonly Action<string>? _livePreviewCallback;

    private double _hue = 207.0; // 0..360
    private double _saturation = 0.85; // 0..1
    private double _value = 0.9; // 0..1
    private bool _isInitialized = false;
    private bool _isUpdatingUi = false;
    private bool _isDraggingWheel = false;

    private static readonly string[] Presets =
    {
        "#1E88E4", "#3B82F6", "#0284C7", "#06B6D4", "#10B981", "#22C55E",
        "#84CC16", "#EAB308", "#F59E0B", "#F97316", "#EF4444", "#EC4899",
        "#D946EF", "#8B5CF6", "#6366F1", "#64748B", "#1E293B", "#FFFFFF"
    };

    public string SelectedHex { get; private set; } = "#1E88E4";

    public ColorPickerWindow(string initialHex = "#1E88E4", Action<string>? livePreviewCallback = null)
    {
        InitializeComponent();

        _initialHex = NormalizeHex(initialHex);
        _livePreviewCallback = livePreviewCallback;

        if (_cachedWheelBitmap == null)
        {
            _cachedWheelBitmap = GenerateWheelBitmap(WheelSize);
        }
        if (ImgColorWheel != null)
        {
            ImgColorWheel.Source = _cachedWheelBitmap;
        }

        PopulatePresets();
        _isInitialized = true;
        SetColorFromHex(_initialHex);
    }

    public static string? ShowDialog(Window? owner, string initialHex, Action<string>? livePreviewCallback = null)
    {
        var window = new ColorPickerWindow(initialHex, livePreviewCallback);
        if (owner != null && owner.IsVisible)
        {
            window.Owner = owner;
        }

        if (window.ShowDialog() == true)
        {
            return window.SelectedHex;
        }
        else if (livePreviewCallback != null)
        {
            // Revert live preview if cancelled
            livePreviewCallback(initialHex);
        }

        return null;
    }

    private void PopulatePresets()
    {
        PresetSwatchesPanel.Children.Clear();
        foreach (var hex in Presets)
        {
            var border = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(2),
                Cursor = Cursors.Hand,
                ToolTip = hex,
                BorderThickness = new Thickness(1)
            };

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                border.Background = new SolidColorBrush(color);
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
            }
            catch
            {
                border.Background = Brushes.Gray;
            }

            border.MouseLeftButtonUp += (s, e) =>
            {
                SetColorFromHex(hex);
            };

            PresetSwatchesPanel.Children.Add(border);
        }
    }

    public static string NormalizeHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return "#1E88E4";
        hex = hex.Trim();
        if (!hex.StartsWith('#')) hex = "#" + hex;
        return hex;
    }

    public static bool TryParseHex(string? hex, out Color color)
    {
        color = Colors.Transparent;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        hex = hex.Trim();
        if (!hex.StartsWith('#')) hex = "#" + hex;

        try
        {
            var converted = ColorConverter.ConvertFromString(hex);
            if (converted is Color c)
            {
                color = c;
                return true;
            }
        }
        catch { }

        return false;
    }

    private void SetColorFromHex(string hex)
    {
        if (TryParseHex(hex, out var color))
        {
            var (h, s, v) = RgbToHsv(color.R, color.G, color.B);
            _hue = h;
            _saturation = s;
            _value = v;

            UpdateUi(color, updateHexText: true, updateRgbText: true, updateSlider: true, updateWheelPos: true);
        }
    }

    private void UpdateFromHsv(bool updateHexText = true, bool updateRgbText = true, bool updateSlider = true, bool updateWheelPos = true)
    {
        var (r, g, b) = HsvToRgb(_hue, _saturation, _value);
        var color = Color.FromRgb(r, g, b);
        UpdateUi(color, updateHexText, updateRgbText, updateSlider, updateWheelPos);
    }

    private void UpdateUi(Color color, bool updateHexText, bool updateRgbText, bool updateSlider, bool updateWheelPos)
    {
        if (!_isInitialized || _isUpdatingUi) return;
        _isUpdatingUi = true;

        try
        {
            SelectedHex = string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
            if (CurrentColorBox != null)
            {
                CurrentColorBox.Background = new SolidColorBrush(color);
            }

            if (updateHexText && TxtHex != null)
            {
                TxtHex.Text = SelectedHex;
            }

            if (updateRgbText)
            {
                if (TxtR != null) TxtR.Text = color.R.ToString();
                if (TxtG != null) TxtG.Text = color.G.ToString();
                if (TxtB != null) TxtB.Text = color.B.ToString();
            }

            if (updateSlider && SliderBrightness != null)
            {
                SliderBrightness.Value = _value;
            }

            // Update brightness track gradient brush (from black to pure hue/saturation color)
            if (BrightnessTrack != null)
            {
                var (pureR, pureG, pureB) = HsvToRgb(_hue, _saturation, 1.0);
                BrightnessTrack.Background = new LinearGradientBrush(
                    Colors.Black,
                    Color.FromRgb(pureR, pureG, pureB),
                    new Point(0, 0.5),
                    new Point(1, 0.5));
            }

            if (updateWheelPos)
            {
                UpdateWheelSelectorPosition();
            }

            _livePreviewCallback?.Invoke(SelectedHex);
        }
        finally
        {
            _isUpdatingUi = false;
        }
    }

    private void UpdateWheelSelectorPosition()
    {
        if (WheelSelector == null) return;
        double center = WheelSize / 2.0;
        double maxRadius = center - 4.0;

        double angleRad = _hue * Math.PI / 180.0;
        double r = _saturation * maxRadius;

        double x = center + r * Math.Cos(angleRad);
        double y = center + r * Math.Sin(angleRad);

        Canvas.SetLeft(WheelSelector, x - (WheelSelector.Width / 2.0));
        Canvas.SetTop(WheelSelector, y - (WheelSelector.Height / 2.0));
    }

    private void HandleWheelInput(Point pos)
    {
        if (!_isInitialized) return;
        double center = WheelSize / 2.0;
        double maxRadius = center - 4.0;

        double dx = pos.X - center;
        double dy = pos.Y - center;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        _hue = (angle + 360.0) % 360.0;
        _saturation = Math.Clamp(dist / maxRadius, 0.0, 1.0);

        UpdateFromHsv(updateHexText: true, updateRgbText: true, updateSlider: false, updateWheelPos: true);
    }

    private void WheelCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isInitialized || WheelCanvas == null) return;
        _isDraggingWheel = true;
        WheelCanvas.CaptureMouse();
        HandleWheelInput(e.GetPosition(WheelCanvas));
    }

    private void WheelCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingWheel && WheelCanvas != null)
        {
            HandleWheelInput(e.GetPosition(WheelCanvas));
        }
    }

    private void WheelCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingWheel)
        {
            _isDraggingWheel = false;
            WheelCanvas?.ReleaseMouseCapture();
        }
    }

    private void SliderBrightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized || _isUpdatingUi || SliderBrightness == null) return;
        _value = SliderBrightness.Value;
        UpdateFromHsv(updateHexText: true, updateRgbText: true, updateSlider: false, updateWheelPos: false);
    }

    private void TxtHex_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitialized || _isUpdatingUi || TxtHex == null) return;
        string hex = TxtHex.Text?.Trim() ?? "";
        if (TryParseHex(hex, out var color))
        {
            var (h, s, v) = RgbToHsv(color.R, color.G, color.B);
            _hue = h;
            _saturation = s;
            _value = v;
            UpdateUi(color, updateHexText: false, updateRgbText: true, updateSlider: true, updateWheelPos: true);
        }
    }

    private void Rgb_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitialized || _isUpdatingUi || TxtR == null || TxtG == null || TxtB == null) return;
        if (byte.TryParse(TxtR.Text, out byte r) &&
            byte.TryParse(TxtG.Text, out byte g) &&
            byte.TryParse(TxtB.Text, out byte b))
        {
            var (h, s, v) = RgbToHsv(r, g, b);
            _hue = h;
            _saturation = s;
            _value = v;
            var color = Color.FromRgb(r, g, b);
            UpdateUi(color, updateHexText: true, updateRgbText: false, updateSlider: true, updateWheelPos: true);
        }
    }

    private void BtnSelect_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public static BitmapSource GenerateWheelBitmap(int size)
    {
        var wb = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
        int stride = size * 4;
        byte[] pixels = new byte[size * stride];

        double center = size / 2.0;
        double maxRadius = center - 3.0;

        for (int y = 0; y < size; y++)
        {
            double dy = y - center;
            for (int x = 0; x < size; x++)
            {
                double dx = x - center;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                int idx = y * stride + x * 4;

                if (dist <= maxRadius)
                {
                    double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                    double hue = (angle + 360.0) % 360.0;
                    double sat = dist / maxRadius;

                    var (r, g, b) = HsvToRgb(hue, sat, 1.0);

                    byte alpha = 255;
                    if (dist > maxRadius - 1.5)
                    {
                        alpha = (byte)(255 * Math.Clamp((maxRadius - dist) / 1.5, 0.0, 1.0));
                    }

                    pixels[idx + 0] = b;
                    pixels[idx + 1] = g;
                    pixels[idx + 2] = r;
                    pixels[idx + 3] = alpha;
                }
                else
                {
                    pixels[idx + 0] = 0;
                    pixels[idx + 1] = 0;
                    pixels[idx + 2] = 0;
                    pixels[idx + 3] = 0;
                }
            }
        }

        wb.WritePixels(new Int32Rect(0, 0, size, size), pixels, stride, 0);
        wb.Freeze();
        return wb;
    }

    public static (double H, double S, double V) RgbToHsv(byte r, byte g, byte b)
    {
        double rd = r / 255.0;
        double gd = g / 255.0;
        double bd = b / 255.0;

        double max = Math.Max(rd, Math.Max(gd, bd));
        double min = Math.Min(rd, Math.Min(gd, bd));
        double delta = max - min;

        double h = 0;
        if (delta > 1e-6)
        {
            if (Math.Abs(max - rd) < 1e-6)
                h = 60.0 * (((gd - bd) / delta) % 6);
            else if (Math.Abs(max - gd) < 1e-6)
                h = 60.0 * (((bd - rd) / delta) + 2);
            else
                h = 60.0 * (((rd - gd) / delta) + 4);

            if (h < 0) h += 360.0;
        }

        double s = max < 1e-6 ? 0 : delta / max;
        double v = max;

        return (h, s, v);
    }

    public static (byte R, byte G, byte B) HsvToRgb(double h, double s, double v)
    {
        h = (h % 360.0 + 360.0) % 360.0;
        s = Math.Clamp(s, 0.0, 1.0);
        v = Math.Clamp(v, 0.0, 1.0);

        double c = v * s;
        double x = c * (1.0 - Math.Abs((h / 60.0) % 2 - 1.0));
        double m = v - c;

        double rPrime = 0, gPrime = 0, bPrime = 0;
        if (h < 60) { rPrime = c; gPrime = x; bPrime = 0; }
        else if (h < 120) { rPrime = x; gPrime = c; bPrime = 0; }
        else if (h < 180) { rPrime = 0; gPrime = c; bPrime = x; }
        else if (h < 240) { rPrime = 0; gPrime = x; bPrime = c; }
        else if (h < 300) { rPrime = x; gPrime = 0; bPrime = c; }
        else { rPrime = c; gPrime = 0; bPrime = x; }

        byte r = (byte)Math.Clamp((int)Math.Round((rPrime + m) * 255.0), 0, 255);
        byte g = (byte)Math.Clamp((int)Math.Round((gPrime + m) * 255.0), 0, 255);
        byte b = (byte)Math.Clamp((int)Math.Round((bPrime + m) * 255.0), 0, 255);

        return (r, g, b);
    }
}
