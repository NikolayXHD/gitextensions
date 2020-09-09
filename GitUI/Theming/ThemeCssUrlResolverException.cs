using System;

namespace GitUI.Theming
{
    public class ThemeCssUrlResolverException : Exception
    {
        public ThemeCssUrlResolverException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
