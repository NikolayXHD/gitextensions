using System;

namespace GitUI.Theming
{
    public class ThemeException : Exception
    {
        public ThemeException(string message, string path, Exception innerException = null)
            : base($"Failed to load {path}: {message}", innerException)
        {
        }
    }
}
