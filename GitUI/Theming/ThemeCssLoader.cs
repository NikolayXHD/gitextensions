using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using ExCSS;
using GitCommands;
using GitExtUtils.GitUI.Theming;
using ResourceManager;

namespace GitUI.Theming
{
    internal class ThemeCssLoader
    {
        private const string ClassSelector = ".";
        private const string ColorProperty = "color";

        private readonly TranslationString _invalidRule = new TranslationString("Invalid rule: {0}");
        private readonly TranslationString _failedToLoadThemeFrom = new TranslationString("Failed to load theme from {0}");
        private readonly TranslationString _fileNotFound = new TranslationString("File not found");
        private readonly TranslationString _fileTooLarge = new TranslationString("File too large");

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

        public bool TryLoadCss(string filePath)
        {
            if (_parseCalled)
            {
                throw new InvalidOperationException($"{nameof(ThemeCssLoader)} only supports 1 call to {nameof(TryLoadCss)}");
            }

            _parseCalled = true;

            return TryLoadCssImpl(filePath, cssImportChain: Array.Empty<string>());
        }

        private bool TryLoadCssImpl(string filePath, string[] cssImportChain)
        {
            if (!TryReadFile(filePath, out string inputContent))
            {
                return false;
            }

            var stylesheet = _parser.Parse(inputContent);
            foreach (var importDirective in stylesheet.ImportDirectives)
            {
                if (!TryImport(filePath, importDirective, cssImportChain))
                {
                    return false;
                }
            }

            foreach (StyleRule rule in stylesheet.StyleRules)
            {
                if (!TryParseRule(filePath, rule))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryReadFile(string fileName, out string result)
        {
            result = null;
            var fileInfo = new FileInfo(fileName);

            if (!fileInfo.Exists)
            {
                PrintTraceWarning(fileName, _fileNotFound.Text);
                return false;
            }

            if (fileInfo.Length > 1024 * 1024)
            {
                PrintTraceWarning(fileName, _fileTooLarge.Text);
                return false;
            }

            try
            {
                result = File.ReadAllText(fileName);
                return true;
            }
            catch (Exception ex)
            {
                PrintTraceWarning(fileName, ex.Message);
                return false;
            }
        }

        private bool TryImport(string filePath, ImportRule importDirective, string[] cssImportChain)
        {
            var importFilePath = _urlResolver.ResolveCssUrl(importDirective.Href);
            if (importFilePath == null)
            {
                PrintTraceWarning(filePath, $"Failed to resolve import: {importDirective.Href}");
                return false;
            }

            if (cssImportChain.Any(_ => StringComparer.OrdinalIgnoreCase.Equals((string)_, importFilePath)))
            {
                PrintTraceWarning(filePath, $"Cycling css imports: {string.Join(",", cssImportChain.Append(importFilePath))}");
                return false;
            }

            return TryLoadCssImpl(importFilePath, cssImportChain.Append(importFilePath));
        }

        private bool TryParseRule(string filePath, StyleRule rule)
        {
            var color = TryGetColor(rule);
            if (!color.HasValue)
            {
                PrintTraceWarning(filePath, string.Format(_invalidRule.Text, rule.Value));
                return false;
            }

            var classNames = TryGetClassNames(rule);
            if (classNames == null)
            {
                PrintTraceWarning(filePath, string.Format(_invalidRule.Text, rule.Value));
                return false;
            }

            var colorName = classNames[0];
            if (!classNames.Skip(1).All(_allowedClasses.Contains))
            {
                return true;
            }

            _specificityByColor.TryGetValue(colorName, out int previousSpecificity);
            int specificity = classNames.Length;
            if (specificity < previousSpecificity)
            {
                return true;
            }

            _specificityByColor[colorName] = specificity;
            if (Enum.TryParse(colorName, out AppColor appColorName))
            {
                _appColors[appColorName] = color.Value;
                return true;
            }

            if (Enum.TryParse(colorName, out KnownColor sysColorName))
            {
                _sysColors[sysColorName] = color.Value;
                return true;
            }

            PrintTraceWarning(filePath, string.Format(_invalidRule.Text, rule.Value));
            return false;
        }

        private string[] TryGetClassNames(StyleRule rule)
        {
            var selector = rule.Selector;
            if (!(selector is SimpleSelector simpleSelector))
            {
                return null;
            }

            var selectorText = simpleSelector.ToString();
            if (!selectorText.StartsWith(ClassSelector))
            {
                return null;
            }

            return selectorText
                .Substring(ClassSelector.Length)
                .Split(new[] { ClassSelector }, StringSplitOptions.RemoveEmptyEntries);
        }

        private Color? TryGetColor(StyleRule rule)
        {
            if (rule.Declarations == null || rule.Declarations.Count != 1)
            {
                return null;
            }

            var style = rule.Declarations[0];
            if (style.Name != ColorProperty || !(style.Term is HtmlColor htmlColor))
            {
                return null;
            }

            return Color.FromArgb(htmlColor.A, htmlColor.R, htmlColor.G, htmlColor.B);
        }

        [Conditional("DEBUG")]
        private void PrintTraceWarning(string fileName, string message) =>
            Trace.WriteLine(string.Format(_failedToLoadThemeFrom.Text, fileName) + Environment.NewLine + message);
    }
}
