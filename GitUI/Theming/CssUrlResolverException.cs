using System;

namespace GitUI.Theming
{
    public class CssUrlResolverException : Exception
    {
        public CssUrlResolverException(string message)
            : base(message)
        {
        }
    }
}
