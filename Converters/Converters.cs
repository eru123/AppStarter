using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AppStarter.Models;
using Binding = System.Windows.Data.Binding;
using Color = System.Windows.Media.Color;

namespace AppStarter.Converters;

/// <summary>
/// Converts CommandStatus to color for status indicator
/// </summary>
public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is CommandStatus status)
        {
            return status switch
            {
                CommandStatus.Running => new SolidColorBrush(Color.FromRgb(16, 185, 129)),    // Green
                CommandStatus.Starting => new SolidColorBrush(Color.FromRgb(245, 158, 11)),   // Yellow
                CommandStatus.Stopping => new SolidColorBrush(Color.FromRgb(245, 158, 11)),   // Yellow
                CommandStatus.Failed => new SolidColorBrush(Color.FromRgb(239, 68, 68)),      // Red
                CommandStatus.Scheduled => new SolidColorBrush(Color.FromRgb(99, 102, 241)),  // Purple
                _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))                        // Gray
            };
        }
        return new SolidColorBrush(Color.FromRgb(100, 116, 139));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts bool/object to Visibility with optional inversion and status checking
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var paramString = parameter as string ?? "";
        
        // Check for IsNull parameter
        if (paramString == "IsNull")
        {
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }
        
        // Check for Invert parameter
        bool invert = paramString == "Invert";
        
        // Check for status-specific visibility
        if (Enum.TryParse<CommandStatus>(paramString, out var targetStatus))
        {
            if (value is CommandStatus currentStatus)
            {
                return currentStatus == targetStatus ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        
        bool isVisible = false;
        
        if (value is bool boolValue)
        {
            isVisible = boolValue;
        }
        else if (value != null)
        {
            isVisible = true;
        }
        
        if (invert)
        {
            isVisible = !isVisible;
        }
        
        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            bool result = visibility == Visibility.Visible;
            
            if ((parameter as string) == "Invert")
            {
                result = !result;
            }
            
            return result;
        }
        return false;
    }
}

/// <summary>
/// Converts between enum values and bool for checkbox/radio binding
/// Also provides enum values for ComboBox ItemsSource
/// </summary>
public class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // If no parameter and value is an enum, return all values for ComboBox ItemsSource
        if (parameter == null && value != null && value.GetType().IsEnum)
        {
            return Enum.GetValues(value.GetType());
        }
        
        // For flags enum checkbox binding
        if (value is Enum enumValue && parameter is string paramString)
        {
            if (Enum.TryParse(value.GetType(), paramString, out var targetValue))
            {
                var intValue = System.Convert.ToInt64(enumValue);
                var intTarget = System.Convert.ToInt64(targetValue);
                return (intValue & intTarget) == intTarget && intTarget != 0;
            }
        }
        
        // For regular enum comparison
        if (value != null && parameter != null)
        {
            return value.Equals(parameter) || value.ToString() == parameter.ToString();
        }
        
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // For flags enum checkbox binding
        if (value is bool isChecked && parameter is string paramString && targetType.IsEnum)
        {
            // This is tricky for flags - we'd need the current value
            // For simplicity, we return the flag value if checked
            if (Enum.TryParse(targetType, paramString, out var targetValue))
            {
                return isChecked ? targetValue : Enum.ToObject(targetType, 0);
            }
        }
        
        return Binding.DoNothing;
    }
}

/// <summary>
/// Converts TimeSpan to human-readable uptime string
/// </summary>
public class TimeSpanToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeSpan ts)
        {
            if (ts.TotalDays >= 1)
            {
                return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
            }
            if (ts.TotalHours >= 1)
            {
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            }
            if (ts.TotalMinutes >= 1)
            {
                return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
            }
            return $"{ts.Seconds}s";
        }
        return "-";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
