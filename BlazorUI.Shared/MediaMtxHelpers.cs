using System.Web;
using System.Xml;

namespace BlazorUI.Shared;

public static class MediaMtxHelpers
{
    // for things im to lazy to find a place for
    public static string MediaMtxPlaybackUrl(string ip, string path, DateTime start,
        int duration)
    {
        var s = HttpUtility.UrlEncode(XmlConvert.ToString(start.ToUniversalTime(), XmlDateTimeSerializationMode.Utc));
        var f = $"http://{ip}/get?path={path}&start={s}&duration={duration}";
        return f;
    }
}