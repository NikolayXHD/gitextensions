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
        Theme Load(string themeFileName, ThemeId id, IReadOnlyList<string> variations);
        void Save(Theme theme, string themeFileName);
    }

    public class ThemePersistence : IThemePersistence
    {
        private const string Format = ".{0} {{ color: #{1:x6} }}";
        private readonly ThemeCssLoader _themeLoader;

        public ThemePersistence(ThemeCssLoader themeLoader)
        {
            _themeLoader = themeLoader;
        }

        public Theme Load(string themeFileName, ThemeId themeId, IReadOnlyList<string> variations)
        {
            _themeLoader.LoadCss(themeFileName, allowedClasses: variations);
            return new Theme(_themeLoader.AppColors, _themeLoader.SysColors, themeId);
        }

        public void Save(Theme theme, string themeFileName)
        {
            string serialized = string.Join(
                Environment.NewLine,
                theme.SysColorValues.Select(_ => string.Format(Format, _.Key, ToRbgInt(_.Value))).Concat(
                    theme.AppColorValues.Select(_ => string.Format(Format, _.Key, ToRbgInt(_.Value)))));

            File.WriteAllText(themeFileName, serialized);

            static int ToRbgInt(Color с) => с.ToArgb() & 0x00ffffff;
        }
    }
}
