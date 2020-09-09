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
    public interface IThemeLoader
    {
        Theme LoadTheme(string themeFileName, ThemeId themeId, in IReadOnlyList<string> allowedClasses);
    }

    public class ThemeLoader : IThemeLoader
    {
        private const string ClassSelector = ".";
        private const string ColorProperty = "color";
        private const int MaxFileSize = 1024 * 1024;

        private readonly Parser _parser;
        private readonly IThemeCssUrlResolver _urlResolver;

        public ThemeLoader(IThemeCssUrlResolver urlResolver)
        {
            _parser = new Parser();
            _urlResolver = urlResolver;
        }

        public Theme LoadTheme(string themeFileName, ThemeId themeId, in IReadOnlyList<string> allowedClasses)
        {
            var themeColors = new ThemeColors();
            LoadThemeColors(themeFileName, cssImportChain: new[] { themeFileName }, allowedClasses, themeColors);
            return new Theme(themeColors.AppColors, themeColors.SysColors, themeId);
        }

        private void LoadThemeColors(string themeFileName, string[] cssImportChain, in IReadOnlyList<string> allowedClasses, ThemeColors themeColors)
        {
            string content = ReadThemeFile(themeFileName);
            var stylesheet = _parser.Parse(content);
            if (stylesheet.Errors.Count > 0)
            {
                throw new ThemeException(
                    $"Error parsing CSS:{Environment.NewLine}{string.Join(Environment.NewLine, stylesheet.Errors)}", themeFileName);
            }

            foreach (var importDirective in stylesheet.ImportDirectives)
            {
                ImportTheme(themeFileName, importDirective, allowedClasses, cssImportChain, themeColors);
            }

            foreach (StyleRule rule in stylesheet.StyleRules)
            {
                ParseRule(themeFileName, rule, allowedClasses, themeColors);
            }
        }

        private static string ReadThemeFile(string themeFileName)
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

        private void ImportTheme(string themeFileName, ImportRule importRule, in IReadOnlyList<string> allowedClasses, string[] cssImportChain, ThemeColors themeColors)
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

            if (cssImportChain.Any(cssImport => StringComparer.OrdinalIgnoreCase.Equals(cssImport, importFilePath)))
            {
                string importChainText = string.Join("->", cssImportChain.Append(importFilePath));
                throw new ThemeException($"Cycling CSS import {importRule.Href} {importChainText}", themeFileName);
            }

            LoadThemeColors(importFilePath, cssImportChain.Append(importFilePath), allowedClasses, themeColors);
        }

        private static void ParseRule(string themeFileName, StyleRule rule, in IReadOnlyList<string> allowedClasses, ThemeColors themeColors)
        {
            var color = GetColor(themeFileName, rule);

            var classNames = GetClassNames(themeFileName, rule);

            var colorName = classNames[0];
            if (!classNames.Skip(1).All(allowedClasses.Contains))
            {
                return;
            }

            themeColors.SpecificityByColor.TryGetValue(colorName, out int previousSpecificity);
            int specificity = classNames.Length;
            if (specificity < previousSpecificity)
            {
                return;
            }

            themeColors.SpecificityByColor[colorName] = specificity;
            if (Enum.TryParse(colorName, out AppColor appColorName))
            {
                themeColors.AppColors[appColorName] = color;
                return;
            }

            if (Enum.TryParse(colorName, out KnownColor sysColorName))
            {
                themeColors.SysColors[sysColorName] = color;
                return;
            }

            throw StyleRuleThemeException(rule, themeFileName);
        }

        private static string[] GetClassNames(string themeFileName, StyleRule rule)
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

        private static Color GetColor(string themeFileName, StyleRule rule)
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

        private class ThemeColors
        {
            public readonly Dictionary<AppColor, Color> AppColors = new Dictionary<AppColor, Color>();
            public readonly Dictionary<KnownColor, Color> SysColors = new Dictionary<KnownColor, Color>();
            public readonly Dictionary<string, int> SpecificityByColor = new Dictionary<string, int>();
        }
    }
}
