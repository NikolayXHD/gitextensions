using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using ExCSS;
using GitCommands;
using GitExtUtils.GitUI.Theming;

namespace GitUI.Theming
{
    public class ThemeCssLoader
    {
        private const string ClassSelector = ".";
        private const string ColorProperty = "color";
        private const int MaxFileSize = 1024 * 1024;

        private readonly Parser _parser;
        private readonly IThemeCssUrlResolver _urlResolver;

        private readonly Dictionary<AppColor, Color> _appColors = new Dictionary<AppColor, Color>();
        private readonly Dictionary<KnownColor, Color> _sysColors = new Dictionary<KnownColor, Color>();
        private readonly Dictionary<string, int> _specificityByColor = new Dictionary<string, int>();

        private bool _parseCalled;

        public ThemeCssLoader(IThemeCssUrlResolver urlResolver)
        {
            _parser = new Parser();
            _urlResolver = urlResolver;
        }

        public IReadOnlyDictionary<AppColor, Color> AppColors => _appColors;

        public IReadOnlyDictionary<KnownColor, Color> SysColors => _sysColors;

        public void LoadCss(string themeFileName, in IReadOnlyList<string> allowedClasses)
        {
            if (_parseCalled)
            {
                throw new InvalidOperationException($"{nameof(ThemeCssLoader)} only supports 1 call to {nameof(LoadCss)}");
            }

            _parseCalled = true;

            LoadCssImpl(themeFileName, cssImportChain: new[] { themeFileName }, allowedClasses);
        }

        private void LoadCssImpl(string themeFileName, string[] cssImportChain, in IReadOnlyList<string> allowedClasses)
        {
            string content = ReadFile(themeFileName);
            var stylesheet = _parser.Parse(content);
            if (stylesheet.Errors.Count > 0)
            {
                throw new ThemeException(
                    $"Error parsing CSS:{Environment.NewLine}{string.Join(Environment.NewLine, stylesheet.Errors)}", themeFileName);
            }

            foreach (var importDirective in stylesheet.ImportDirectives)
            {
                Import(themeFileName, importDirective, allowedClasses, cssImportChain);
            }

            foreach (StyleRule rule in stylesheet.StyleRules)
            {
                ParseRule(themeFileName, rule, allowedClasses);
            }
        }

        private string ReadFile(string themeFileName)
        {
            var fileInfo = new FileInfo(themeFileName);
            if (fileInfo.Exists && fileInfo.Length > MaxFileSize)
            {
                throw new ThemeException($"Theme file size exceeds {MaxFileSize:#,##0} bytes", themeFileName);
            }

            try
            {
                return File.ReadAllText(themeFileName);
            }
            catch (SystemException ex)
            {
                throw new ThemeException(ex.Message, themeFileName, ex);
            }
        }

        private void Import(string themeFileName, ImportRule importRule, in IReadOnlyList<string> allowedClasses, string[] cssImportChain)
        {
            string importFilePath;
            try
            {
                importFilePath = _urlResolver.ResolveCssUrl(importRule.Href);
            }
            catch (ThemeCssUrlResolverException ex)
            {
                throw new ThemeException($"Failed to resolve CSS import {importRule.Href}: {ex.Message}", themeFileName, ex);
            }

            if (cssImportChain.Any(_ => StringComparer.OrdinalIgnoreCase.Equals((string)_, importFilePath)))
            {
                string importChainText = string.Join("->", cssImportChain.Append(importFilePath));
                throw new ThemeException($"Cycling CSS import {importRule.Href} {importChainText}", themeFileName);
            }

            LoadCssImpl(importFilePath, cssImportChain.Append(importFilePath), allowedClasses);
        }

        private void ParseRule(string themeFileName, StyleRule rule, in IReadOnlyList<string> allowedClasses)
        {
            var color = GetColor(themeFileName, rule);

            var classNames = GetClassNames(themeFileName, rule);

            var colorName = classNames[0];
            if (!classNames.Skip(1).All(allowedClasses.Contains))
            {
                return;
            }

            _specificityByColor.TryGetValue(colorName, out int previousSpecificity);
            int specificity = classNames.Length;
            if (specificity < previousSpecificity)
            {
                return;
            }

            _specificityByColor[colorName] = specificity;
            if (Enum.TryParse(colorName, out AppColor appColorName))
            {
                _appColors[appColorName] = color;
                return;
            }

            if (Enum.TryParse(colorName, out KnownColor sysColorName))
            {
                _sysColors[sysColorName] = color;
                return;
            }

            throw StyleRuleThemeException(rule, themeFileName);
        }

        private string[] GetClassNames(string themeFileName, StyleRule rule)
        {
            var selector = rule.Selector;
            if (!(selector is SimpleSelector simpleSelector))
            {
                throw StyleRuleThemeException(rule, themeFileName);
            }

            var selectorText = simpleSelector.ToString();
            if (!selectorText.StartsWith(ClassSelector))
            {
                throw StyleRuleThemeException(rule, themeFileName);
            }

            return selectorText
                .Substring(ClassSelector.Length)
                .Split(new[] { ClassSelector }, StringSplitOptions.RemoveEmptyEntries);
        }

        private Color GetColor(string themeFileName, StyleRule rule)
        {
            if (rule.Declarations is null || rule.Declarations.Count != 1)
            {
                throw StyleRuleThemeException(rule, themeFileName);
            }

            var style = rule.Declarations[0];
            if (style.Name != ColorProperty || !(style.Term is HtmlColor htmlColor))
            {
                throw StyleRuleThemeException(rule, themeFileName);
            }

            return Color.FromArgb(htmlColor.A, htmlColor.R, htmlColor.G, htmlColor.B);
        }

        private static ThemeException StyleRuleThemeException(StyleRule styleRule, string themePath)
            => new ThemeException($"Invalid CSS rule '{styleRule.Value}'", themePath);
    }
}
