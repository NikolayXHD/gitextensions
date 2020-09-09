using System;
using System.IO;
using GitCommands;
using GitExtUtils.GitUI.Theming;

namespace GitUI.Theming
{
    public interface IThemePathProvider
    {
        string GetThemePath(ThemeId id);
    }

    public class ThemePathProvider : IThemePathProvider
    {
        private const string Subdirectory = "Themes";
        internal const string ThemeExtension = ".css";

        static ThemePathProvider()
        {
            string appDirectory = AppSettings.GetGitExtensionsDirectory() ??
                throw new DirectoryNotFoundException("Application directory not found");
            AppThemesDirectory = Path.Combine(appDirectory, Subdirectory);

            string userDirectory = AppSettings.ApplicationDataPath.Value;

            // in portable version appDirectory and userDirectory are same,
            // hence we don't have a separate directory for user themes
            UserThemesDirectory = string.Equals(appDirectory, userDirectory, StringComparison.OrdinalIgnoreCase)
                ? null
                : Path.Combine(userDirectory, Subdirectory);
        }

        public static string AppThemesDirectory { get; }

        public static string UserThemesDirectory { get; }

        ///// <summary>
        /////
        ///// </summary>
        ///// <param name="id"></param>
        ///// <returns></returns>

        /// <exception cref="InvalidOperationException">Attempt to resolve a custom theme from a %UserAppData% folder in a portable version.</exception>
        /// <exception cref="FileNotFoundException">Theme does not exist.</exception>
        public string GetThemePath(ThemeId id)
        {
            string path;
            if (id.IsBuiltin)
            {
                path = Path.Combine(AppThemesDirectory, id.Name + ThemeExtension);
            }
            else
            {
                if (UserThemesDirectory is null)
                {
                    throw new InvalidOperationException("Portable mode only supports local themes");
                }

                path = Path.Combine(UserThemesDirectory, id.Name + ThemeExtension);
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Theme not found", path);
            }

            return path;
        }
    }
}
