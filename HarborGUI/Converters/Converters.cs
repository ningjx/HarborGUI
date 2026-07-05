using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using HarborGUI.Models;

namespace HarborGUI.Converters;

/// <summary>
/// TaskStatus → 状态颜色转换
/// </summary>
public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Models.VerifyTaskStatus status) { return status switch { Models.VerifyTaskStatus.Queued => new SolidColorBrush(Color.FromRgb(0x00, 0xB8, 0xD4)), Models.VerifyTaskStatus.Verifying or Models.VerifyTaskStatus.Extracting => new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)), Models.VerifyTaskStatus.Passed => new SolidColorBrush(Color.FromRgb(0x1E, 0xBA, 0x1E)), Models.VerifyTaskStatus.Failed or Models.VerifyTaskStatus.Error => new SolidColorBrush(Color.FromRgb(0xE0, 0x30, 0x30)), _ => new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00)) }; }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// bool → Visibility 转换
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var boolValue = value is true;
        var invert = parameter is string s && s == "Invert";
        return (boolValue ^ invert) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// double (0~100) → 百分比字符串
/// </summary>
public class ProgressToPercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
            return $"{d:F0}%";
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Dictionary → 指定规则的结果文本（ConverterParameter = 规则名）
/// 绑定 Path="RuleResults"，WPF 自动响应 PropertyChanged
/// </summary>
public class RuleResultConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Dictionary<string, string> results && parameter is string ruleName)
            return results.TryGetValue(ruleName, out var v) ? v : "";
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// bool → 👁/🔒 图标
/// </summary>
public class BoolToEyeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "👁" : "🔒";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>double 0~100 → 0.0~1.0（任务栏进度条）</summary>
public class ProgressToTaskbarConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double d ? d / 100.0 : 0.0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>质检规则结果状态 → 前景色（✅ 绿 / ❌ 红 / 其他 橙）</summary>
public class RuleResultToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Dictionary<string, string> results && parameter is string ruleName
            && results.TryGetValue(ruleName, out var result))
        {
            return result switch
            {
                "✅" => new SolidColorBrush(Color.FromRgb(0x1E, 0xBA, 0x1E)),
                "❌" => new SolidColorBrush(Color.FromRgb(0xE0, 0x30, 0x30)),
                _ => new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00))
            };
        }
        return new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
