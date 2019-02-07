using System.Collections.Generic;
using System.Windows.Forms;

namespace ColorScheme
{
    public static class ControlExtension
    {
        public static void SetTag<TValue>(this Control control, string key, TValue value)
        {
            if (control.Tag == null)
                control.Tag = new Dictionary<string, object>();

            var dict = (Dictionary<string, object>)control.Tag;
            dict[key] = value;
        }

        public static void SetTag<TValue>(this Control control, TValue value) =>
            SetTag(control, typeof(TValue).FullName, value);

        public static TValue GetTag<TValue>(this Control control, string key)
        {
            if (control.Tag == null)
                control.Tag = new Dictionary<string, object>();

            var dict = (Dictionary<string, object>)control.Tag;

            if (!dict.TryGetValue(key, out var result))
                return default;

            return (TValue)result;
        }

        public static TValue GetTag<TValue>(this Control control) =>
            GetTag<TValue>(control, typeof(TValue).FullName);
    }
}