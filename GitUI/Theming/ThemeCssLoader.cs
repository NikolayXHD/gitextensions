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
    internal class ThemeCssLoader
    {
        private const string ClassSelector = ".";
        private const string ColorProperty = "color";
        private const int MaxFileSize = 1024 * 1024;

        private readonly Parser _parser;
        private readonly ICssUrlResolver _urlResolver;
        private readonly string[] _allowedClasses;

        private readonly Dictionary<AppColor, Color> _appColors = new Dictionary<AppColor, Color>();
        private readonly Dictionary<KnownColor, Color> _sysColors = new Dictionary<KnownColor, Color>();
        private readonly Dictionary<string, int> _specificityByColor = new Dictionary<string, int>();

        private bool _parseCalled;

        public ThemeCssLoader(ICssUrlResolver urlResolver, string[] allowedClasses)
        {
            _parser = new Parser();
            _urlResolver = urlResolver;
            _allowedClasses = allowedClasses;
        }

        public IReadOnlyDictionary<AppColor, Color> AppColors => _appColors;

        public IReadOnlyDictionary<KnownColor, Color> SysColors => _sysColors;

        public void LoadCss(string path)
        {
            if (_parseCalled)
            {
                throw new InvalidOperationException($"{nameof(ThemeCssLoader)} only supports 1 call to {nameof(LoadCss)}");
            }

            _parseCalled = true;

            LoadCssImpl(path, cssImportChain: new[] { path });
        }

        private void LoadCssImpl(string path, string[] cssImportChain)
        {
            string content = ReadFile(path);
            var stylesheet = _parser.Parse(content);
            if (stylesheet.Errors.Count > 0)
            {
                throw new ThemeRepositoryException(
                    $"Error parsing css:{Environment.NewLine}{string.Join(Environment.NewLine, stylesheet.Errors)}", path);
            }

            foreach (var importDirective in stylesheet.ImportDirectives)
            {
                Import(importDirective, cssImportChain, path);
            }

            foreach (StyleRule rule in stylesheet.StyleRules)
            {
                ParseRule(rule, path);
            }
        }

        private string ReadFile(string path)
        {
            var fileInfo = new FileInfo(path);
            if (fileInfo.Exists && fileInfo.Length > MaxFileSize)
            {
                throw new ThemeRepositoryException($"File too large, maximum is {MaxFileSize} bytes", path);
            }

            try
            {
                return File.ReadAllText(path);
            }
            catch (SystemException ex)
            {
                throw new ThemeRepositoryException(ex.Message, path, ex);
            }
        }

        private void Import(ImportRule importRule, string[] cssImportChain, string path)
        {
            string importFilePath;
            try
            {
                importFilePath = _urlResolver.ResolveCssUrl(importRule.Href);
            }
            catch (CssUrlResolverException ex)
            {
                throw new ThemeRepositoryException($"Failed to resolve css import {importRule.Href}: {ex.Message}", path, ex);
            }

            if (cssImportChain.Any(_ => StringComparer.OrdinalIgnoreCase.Equals((string)_, importFilePath)))
            {
                string importChainText = string.Join("->", cssImportChain.Append(importFilePath));
                throw new ThemeRepositoryException($"Cycling css import {importRule.Href} {importChainText}", path);
            }

            LoadCssImpl(importFilePath, cssImportChain.Append(importFilePath));
        }

        private void ParseRule(StyleRule rule, string path)
        {
            var color = GetColor(rule, path);

            var classNames = GetClassNames(rule, path);

            var colorName = classNames[0];
            if (!classNames.Skip(1).All(_allowedClasses.Contains))
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

            throw new ThemeRepositoryException(rule, path);
        }

        private string[] GetClassNames(StyleRule rule, string path)
        {
            var selector = rule.Selector;
            if (!(selector is SimpleSelector simpleSelector))
            {
                throw new ThemeRepositoryException(rule, path);
            }

            var selectorText = simpleSelector.ToString();
            if (!selectorText.StartsWith(ClassSelector))
            {
                throw new ThemeRepositoryException(rule, path);
            }

            return selectorText
                .Substring(ClassSelector.Length)
                .Split(new[] { ClassSelector }, StringSplitOptions.RemoveEmptyEntries);
        }

        private Color GetColor(StyleRule rule, string path)
        {
            if (rule.Declarations == null || rule.Declarations.Count != 1)
            {
                throw new ThemeRepositoryException(rule, path);
            }

            var style = rule.Declarations[0];
            if (style.Name != ColorProperty || !(style.Term is HtmlColor htmlColor))
            {
                throw new ThemeRepositoryException(rule, path);
            }

            return Color.FromArgb(htmlColor.A, htmlColor.R, htmlColor.G, htmlColor.B);
        }
    }
}
