using System;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Data.Converters;

namespace CodeGenerator.Converters;

public class VisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            Debug.Assert(parameter != null, nameof(parameter) + " != null");
            if (parameter.Equals("ProgressBar"))
            {
                if (value == null)
                {
                    return true;
                }

                return int.Parse(value.ToString()!) != 100;
            }
            else
            {
                if (value == null)
                {
                    return false;
                }

                return int.Parse(value.ToString()!) == 100;
            }
        }
        catch
        {
            return false;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}