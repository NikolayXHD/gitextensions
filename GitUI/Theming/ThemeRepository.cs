using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GitCommands;
using GitExtUtils.GitUI.Theming;
using JetBrains.Annotations;

namespace GitUI.Theming
{
    public class ThemeRepository
    {
        private const string Subdirectory = "Themes";
        private const string Extension = ".css";

        private readonly IThemePersistence _persistence;
        private readonly IThemePathProvider _themePathProvider;

        public ThemeRepository(IThemePersistence persistence, IThemePathProvider themePathProvider)
        {
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _themePathProvider = themePathProvider ?? throw new ArgumentNullException(nameof(themePathProvider));

            string appDirectory = AppSettings.GetGitExtensionsDirectory() ??
                throw new InvalidOperationException("Missing application directory");
            AppThemesDirectory = Path.Combine(appDirectory, Subdirectory);

            string userDirectory = AppSettings.ApplicationDataPath.Value;

            // in portable version appDirectory and userDirectory are same,
            // hence we don't have a separate directory for user themes
            UserThemesDirectory = string.Equals(appDirectory, userDirectory, StringComparison.OrdinalIgnoreCase)
                ? null
                : Path.Combine(userDirectory, Subdirectory);
        }

        public ThemeRepository()
            : this(new ThemePersistence(new ThemeCssLoader(new ThemeCssUrlResolver(new ThemePathProvider()))), new ThemePathProvider())
        {
        }

        private string AppThemesDirectory { get; }

        [CanBeNull]
        private string UserThemesDirectory { get; }
        public string InvariantThemeName { get; } = "invariant";

        public Theme GetTheme(ThemeId themeId, IReadOnlyList<string> variations)
        {
            string themePath = _themePathProvider.GetThemePath(themeId);
            return _persistence.Load(themePath, themeId, variations);
        }

        public void Save(Theme theme) =>
            _persistence.Save(theme, _themePathProvider.GetThemePath(theme.Id));

        public Theme GetInvariantTheme() =>
            GetTheme(new ThemeId(InvariantThemeName, isBuiltin: true), variations: Array.Empty<string>());

        public IEnumerable<ThemeId> GetThemeIds() =>
            GetBuiltinThemeIds().Concat(GetUserCustomizedThemeIds());

        public void Delete(ThemeId themeId)
        {
            if (themeId.IsBuiltin)
            {
                throw new InvalidOperationException("Only user-defined theme can be deleted");
            }

            var themePath = _themePathProvider.GetThemePath(themeId);
            File.Delete(themePath);
        }

        private IEnumerable<ThemeId> GetBuiltinThemeIds() =>
            new DirectoryInfo(AppThemesDirectory)
                .EnumerateFiles("*" + Extension, SearchOption.TopDirectoryOnly)
                .Select(fileInfo => Path.GetFileNameWithoutExtension(fileInfo.Name))
                .Where(fileName => !fileName.Equals(InvariantThemeName, StringComparison.OrdinalIgnoreCase))
                .Select(fileName => new ThemeId(fileName, true));

        private IEnumerable<ThemeId> GetUserCustomizedThemeIds()
        {
            if (UserThemesDirectory is null)
            {
                return Enumerable.Empty<ThemeId>();
            }

            var directory = new DirectoryInfo(UserThemesDirectory);
            return directory.Exists
                ? directory
                    .EnumerateFiles("*" + Extension, SearchOption.TopDirectoryOnly)
                    .Select(fileInfo => Path.GetFileNameWithoutExtension(fileInfo.Name))
                    .Select(fileName => new ThemeId(fileName, false))
                : Enumerable.Empty<ThemeId>();
        }
    }
}
