using System;
using ExCSS;

namespace GitUI.Theming
{
    public class ThemeRepositoryException : Exception
    {
        public ThemeRepositoryException(string message, string path, Exception innerException = null)
            : base($"Failed to load {path}: {message}", innerException)
        {
        }

        public ThemeRepositoryException(StyleRule styleRule, string path)
            : base($"Failed to load {path}: invalid css rule {styleRule.Value}")
        {
        }
    }
}
