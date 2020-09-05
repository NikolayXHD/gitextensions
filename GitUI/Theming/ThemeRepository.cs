using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GitCommands;
using GitExtUtils.GitUI.Theming;
using JetBrains.Annotations;

namespace GitUI.Theming
{
    public class ThemeRepository : ICssUrlResolver
    {
        private const string Subdirectory = "Themes";
        private const string Extension = ".css";
        private const string CssVariableUserThemesDirectory = "$user-defined/";

        private readonly ThemePersistence _persistence;

        public ThemeRepository(ThemePersistence persistence)
        {
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _persistence.CssUrlResolver = this;

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

        private string AppThemesDirectory { get; }

        [CanBeNull]
        private string UserThemesDirectory { get; }
        public string InvariantThemeName { get; } = "invariant";

        public Theme GetTheme(ThemeId id, string[] variations)
        {
            string path = GetPath(id);
            return _persistence.Load(path, id, variations);
        }

        public void Save(Theme theme) =>
            _persistence.Save(theme, GetPath(theme.Id));

        public Theme GetInvariantTheme() =>
            GetTheme(new ThemeId(InvariantThemeName, isBuiltin: true), variations: Array.Empty<string>());

        public IEnumerable<ThemeId> GetThemeIds() =>
            GetBuiltinThemeIds().Concat(GetUserCustomizedThemeIds());

        private IEnumerable<ThemeId> GetBuiltinThemeIds() =>
            new DirectoryInfo(AppThemesDirectory)
                .EnumerateFiles("*" + Extension, SearchOption.TopDirectoryOnly)
                .Select(_ => Path.GetFileNameWithoutExtension(_.Name))
                .Where(name => !name.Equals(InvariantThemeName, StringComparison.OrdinalIgnoreCase))
                .Select(name => new ThemeId(name, true));

        public void Delete(ThemeId id)
        {
            if (id.IsBuiltin)
            {
                throw new InvalidOperationException("Only user-defined theme can be deleted");
            }

            var path = GetPath(id);
            File.Delete(path);
        }

        private IEnumerable<ThemeId> GetUserCustomizedThemeIds()
        {
            if (UserThemesDirectory == null)
            {
                return Enumerable.Empty<ThemeId>();
            }

            var directory = new DirectoryInfo(UserThemesDirectory);
            return directory.Exists
                ? directory
                    .EnumerateFiles("*" + Extension, SearchOption.TopDirectoryOnly)
                    .Select(_ => Path.GetFileNameWithoutExtension(_.Name))
                    .Select(name => new ThemeId(name, false))
                : Enumerable.Empty<ThemeId>();
        }

        string ICssUrlResolver.ResolveCssUrl(string url)
        {
            if (url.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            {
                url = url.Substring(0, url.Length - Extension.Length);
            }

            ThemeId id = url.StartsWith(CssVariableUserThemesDirectory)
                ? new ThemeId(url.Substring(CssVariableUserThemesDirectory.Length), isBuiltin: false)
                : new ThemeId(url, isBuiltin: true);

            try
            {
                return GetPath(id);
            }
            catch (ThemeNotFoundException)
            {
                return null;
            }
        }

        private string GetPath(ThemeId id)
        {
            string path;
            if (id.IsBuiltin)
            {
                path = Path.Combine(AppThemesDirectory, id.Name + Extension);
            }
            else
            {
                if (UserThemesDirectory == null)
                {
                    throw new ThemeNotFoundException("There is no directory for custom user themes in portable mode");
                }

                path = Path.Combine(UserThemesDirectory, id.Name + Extension);
            }

            if (!File.Exists(path))
            {
                throw new ThemeNotFoundException($"Theme file not found: {path}");
            }

            return path;
        }

        private class ThemeNotFoundException : Exception
        {
            public ThemeNotFoundException(string message)
                : base(message)
            {
            }
        }
    }
}
