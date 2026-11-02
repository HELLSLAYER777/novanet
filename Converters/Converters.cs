using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace NovaNet.Converters;

/// <summary>Converts bool to Visibility (Visible/Collapsed).</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }
    public bool UseHidden { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool b = value is true;
        if (Invert) b = !b;
        return b ? Visibility.Visible : (UseHidden ? Visibility.Hidden : Visibility.Collapsed);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

/// <summary>Converts bool to Visibility (Visible/Hidden).</summary>
public sealed class BoolToVisibilityHiddenConverter : BoolToVisibilityConverter
{
    public BoolToVisibilityHiddenConverter() => UseHidden = true;
}

/// <summary>Converts null/empty to Visibility.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }
    public bool UseHidden { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool hasValue = value switch
        {
            string s => !string.IsNullOrWhiteSpace(s),
            System.Collections.IEnumerable e => e.GetEnumerator().MoveNext(),
            _ => value != null
        };
        if (Invert) hasValue = !hasValue;
        return hasValue ? Visibility.Visible : (UseHidden ? Visibility.Hidden : Visibility.Collapsed);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Converts enum to bool for RadioButton binding.</summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.Equals(parameter) == true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? parameter : Binding.DoNothing;
}

/// <summary>Converts a double to a color brush for latency visualization.</summary>
public sealed class LatencyToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double ms) return Brushes.Gray;
        return ms switch
        {
            < 20 => new SolidColorBrush(Color.FromRgb(91, 227, 176)),   // Mint
            < 60 => new SolidColorBrush(Color.FromRgb(255, 181, 71)),   // Amber
            _ => new SolidColorBrush(Color.FromRgb(255, 107, 107))      // Coral
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Multi-converter for combining multiple boolean values (AND logic).</summary>
public sealed class AndMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        => values.All(v => v is true);

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Converts a progress percentage to a formatted string.</summary>
public sealed class ProgressToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is double d ? $"{d:0}%" : "0%";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Converts bytes to human-readable string.</summary>
public sealed class BytesToStringConverter : IValueConverter
{
    static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long bytes) return "0 B";
        double d = bytes;
        int i = 0;
        while (d >= 1024 && i < Units.Length - 1) { d /= 1024; i++; }
        return $"{d:0.##} {Units[i]}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Inverts a boolean value.</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;
}