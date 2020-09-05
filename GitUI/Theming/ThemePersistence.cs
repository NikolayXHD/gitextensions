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
    public class ThemePersistence
    {
        private const string ClassSelector = ".";
        private const string ColorProperty = "color";

        private const string Format = ".{0} {{ color: #{1:x6} }}";

        private readonly TranslationString _failedToLoadThemeFrom =
            new TranslationString("Failed to read theme from {0}");
        private readonly TranslationString _fileNotFound =
            new TranslationString("File not found");
        private readonly TranslationString _fileTooLarge =
            new TranslationString("File too large");
        private readonly TranslationString _invalidRule =
            new TranslationString("Invalid rule: {0}");

        public ICssUrlResolver CssUrlResolver { get; set; }

        public Theme Load(string fileName, ThemeId id, string[] variations)
        {
            if (!TryReadFile(fileName, out string serialized))
            {
                return null;
            }

            if (!TryGetColors(fileName, serialized, variations, out var appColors, out var sysColors))
            {
                return null;
            }

            return new Theme(appColors, sysColors, id);
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

        private bool TryGetColors(
            string fileName,
            string input,
            string[] allowedClasses,
            out IReadOnlyDictionary<AppColor, Color> applicationColors,
            out IReadOnlyDictionary<KnownColor, Color> systemColors)
        {
            var appColors = new Dictionary<AppColor, Color>();
            var sysColors = new Dictionary<KnownColor, Color>();
            var specificityByColor = new Dictionary<string, int>();
            var classSet = new HashSet<string>(allowedClasses, StringComparer.OrdinalIgnoreCase);

            applicationColors = null;
            systemColors = null;

            var parser = new Parser();

            var isSuccess = TryParseCss(fileName, input, cssImportChain: Array.Empty<string>());

            applicationColors = appColors;
            systemColors = sysColors;
            return isSuccess;

            bool TryParseCss(string filePath, string inputContent, string[] cssImportChain)
            {
                var stylesheet = parser.Parse(inputContent);
                if (stylesheet.ImportDirectives.Count != 0)
                {
                    foreach (var import in stylesheet.ImportDirectives)
                    {
                        var importFilePath = CssUrlResolver.ResolveCssUrl(import.Href);
                        if (importFilePath == null)
                        {
                            PrintTraceWarning(filePath, $"Failed to resolve import: {import.Href}");
                            return false;
                        }

                        if (cssImportChain.Any(_ => StringComparer.OrdinalIgnoreCase.Equals(_, importFilePath)))
                        {
                            PrintTraceWarning(filePath, $"Cycling css imports: {string.Join(",", cssImportChain.Append(importFilePath))}");
                            return false;
                        }

                        if (!TryReadFile(importFilePath, out string importContent))
                        {
                            return false;
                        }

                        if (!TryParseCss(importFilePath, importContent, cssImportChain.Append(importFilePath)))
                        {
                            return false;
                        }
                    }
                }

                foreach (StyleRule rule in stylesheet.StyleRules)
                {
                    if (rule.Declarations == null || rule.Declarations.Count != 1)
                    {
                        PrintTraceWarning(filePath, string.Format(_invalidRule.Text, rule.Value));
                        return false;
                    }

                    var style = rule.Declarations[0];
                    if (style.Name != ColorProperty || !(style.Term is HtmlColor htmlColor))
                    {
                        PrintTraceWarning(filePath, string.Format(_invalidRule.Text, rule.Value));
                        return false;
                    }

                    var color = Color.FromArgb(htmlColor.A, htmlColor.R, htmlColor.G, htmlColor.B);

                    var classNames = TryGetClassNames(rule);

                    var colorName = classNames[0];
                    if (!classNames.Skip(1).All(classSet.Contains))
                    {
                        continue;
                    }

                    specificityByColor.TryGetValue(colorName, out int previousSpecificity);
                    int specificity = classNames.Length;
                    if (specificity < previousSpecificity)
                    {
                        continue;
                    }

                    specificityByColor[colorName] = specificity;

                    if (Enum.TryParse(colorName, out AppColor appColorName))
                    {
                        appColors[appColorName] = color;
                    }
                    else if (Enum.TryParse(colorName, out KnownColor sysColorName))
                    {
                        sysColors[sysColorName] = color;
                    }
                    else
                    {
                        PrintTraceWarning(filePath, string.Format(_invalidRule.Text, rule.Value));
                        return false;
                    }
                }

                return true;
            }
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

        [Conditional("DEBUG")]
        private void PrintTraceWarning(string fileName, string message) =>
            Trace.WriteLine(string.Format(_failedToLoadThemeFrom.Text, fileName) + Environment.NewLine + message);
    }
}
