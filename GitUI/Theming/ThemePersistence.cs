using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using GitExtUtils.GitUI.Theming;
using ResourceManager;
using Color = System.Drawing.Color;

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

            var parser = new ExCSS.StylesheetParser();
            var stylesheet = parser.Parse(input);
            foreach ((ExCSS.ISelector selector, ExCSS.Color cssColor) in EnumerateColors(stylesheet))
            {
                var color = Color.FromArgb(cssColor.A, cssColor.R, cssColor.G, cssColor.B);

                var classNames = TryGetClassNames(selector);

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
                    PrintTraceWarning(fileName, string.Format(_invalidRule.Text, selector.Text));
                    return false;
                }
            }

            applicationColors = appColors;
            systemColors = sysColors;
            return true;
        }

        private IEnumerable<(ExCSS.ISelector selector, ExCSS.Color color)> EnumerateColors(ExCSS.IStylesheetNode stylesheet)
        {
            foreach (ExCSS.IStylesheetNode node in EnumerateNodes(stylesheet))
            {
                if (!(node is ExCSS.IRule rule) || rule.Type != ExCSS.RuleType.Style)
                {
                    continue;
                }

                var selector = rule.Children.OfType<ExCSS.ISelector>().FirstOrDefault();
                if (selector == null)
                {
                    continue;
                }

                var style = rule.Children.OfType<ExCSS.StyleDeclaration>().FirstOrDefault();
                if (style == null)
                {
                    continue;
                }

                var colorProperty = style.Children.OfType<ExCSS.Property>().FirstOrDefault(_ => _.Name == ExCSS.PropertyNames.Color);
                if (colorProperty == null)
                {
                    continue;
                }

                ExCSS.Color? color = GetColor(colorProperty);
                if (!color.HasValue)
                {
                    continue;
                }

                yield return (selector, color.Value);
            }
        }

        private IEnumerable<ExCSS.IStylesheetNode> EnumerateNodes(ExCSS.IStylesheetNode stylesheet)
        {
            foreach (ExCSS.IStylesheetNode child in stylesheet.Children)
            {
                yield return child;
                foreach (var descendant in EnumerateNodes(child))
                {
                    yield return descendant;
                }
            }
        }

        private string[] TryGetClassNames(ExCSS.ISelector selector)
        {
            var selectorText = selector.Text;
            if (!selectorText.StartsWith(ClassSelector))
            {
                return null;
            }

            return selectorText
                .Substring(ClassSelector.Length)
                .Split(new[] { ClassSelector }, StringSplitOptions.RemoveEmptyEntries);
        }

        private ExCSS.Color? GetColor(ExCSS.Property colorProperty)
        {
            var value = DeclaredValueProperty.GetValue(colorProperty);

            var valueType = value.GetType();
            if (!_valueFields.TryGetValue(valueType, out var valueField))
            {
                valueField = valueType.GetField("_value", BindingFlags.Instance | BindingFlags.NonPublic);
                _valueFields.Add(valueType, valueField);
            }

            var color = valueField.GetValue(value) as ExCSS.Color?;
            return color;
        }

        [Conditional("DEBUG")]
        private void PrintTraceWarning(string fileName, string message) =>
            Trace.WriteLine(string.Format(_failedToLoadThemeFrom.Text, fileName) + Environment.NewLine + message);

        private static readonly PropertyInfo DeclaredValueProperty =
            typeof(ExCSS.Property).GetProperty("DeclaredValue", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Dictionary<Type, FieldInfo> _valueFields = new Dictionary<Type, FieldInfo>();
    }
}
