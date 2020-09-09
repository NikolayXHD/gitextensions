using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using GitExtUtils.GitUI.Theming;

namespace GitUI.Theming
{
    public interface IThemePersistence
    {
        Theme Load(string fileName, ThemeId id, IReadOnlyList<string> variations);
        void Save(Theme theme, string fileName);
    }

    public class ThemePersistence : IThemePersistence
    {
        private const string Format = ".{0} {{ color: #{1:x6} }}";
        private readonly IThemeCssUrlResolver _themeCssUrlResolver;

        public ThemePersistence(IThemeCssUrlResolver themeCssUrlResolver)
        {
            _themeCssUrlResolver = themeCssUrlResolver;
        }

        public Theme Load(string fileName, ThemeId id, IReadOnlyList<string> variations)
        {
            var themeLoader = new ThemeCssLoader(_themeCssUrlResolver, allowedClasses: variations);
            themeLoader.LoadCss(fileName);
            return new Theme(themeLoader.AppColors, themeLoader.SysColors, id);
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
