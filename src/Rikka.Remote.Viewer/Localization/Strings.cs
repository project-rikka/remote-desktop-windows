using System.Text;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Rikka.Remote.Viewer.Localization;

internal static class Strings
{
    private static readonly ResourceLoader Loader = new();

    public static string Get(string key) => Loader.GetString(key.Replace('.', '/'));

    public static string Format(string key, params ReadOnlySpan<(string Name, object Value)> arguments)
    {
        var text = new StringBuilder(Get(key));
        foreach (var (name, value) in arguments)
        {
            text.Replace("{" + name + "}", Convert.ToString(value, System.Globalization.CultureInfo.CurrentCulture));
        }

        return text.ToString();
    }
}
