using System.Globalization;

namespace BlazorUI.Shared;

public static class DateTimeExtentions
{
    public static string ToRfc3339String(this DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", DateTimeFormatInfo.InvariantInfo);
    }
}