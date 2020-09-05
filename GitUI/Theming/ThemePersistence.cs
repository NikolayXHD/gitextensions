using System;
using System.Drawing;
using System.IO;
using System.Linq;
using GitExtUtils.GitUI.Theming;

namespace GitUI.Theming
{
    public class ThemePersistence
    {
        private const string Format = ".{0} {{ color: #{1:x6} }}";

        public ICssUrlResolver CssUrlResolver { get; set; }

        public Theme Load(string fileName, ThemeId id, string[] variations)
        {
            var themeParser = new ThemeCssLoader(CssUrlResolver, allowedClasses: variations);
            if (themeParser.TryLoadCss(fileName))
            {
                return new Theme(themeParser.AppColors, themeParser.SysColors, id);
            }

            return null;
        }

        public void Save(Theme theme, string fileName)
        {
            string serialized = string.Join(
                Environment.NewLine,
                theme.SysColorValues.Select(_ => string.Format(Format, _.Key, ToRbgInt(_.Value))).Concat(
                    theme.AppColorValues.Select(_ => string.Format(Format, _.Key, ToRbgInt(_.Value)))));

            File.WriteAllText(fileName, serialized);

            static int ToRbgInt(Color с) => с.ToArgb() & 0x00ffffff;
        }
    }
}
