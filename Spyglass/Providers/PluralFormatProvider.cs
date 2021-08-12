using System;

namespace Spyglass.Providers
{
    public class PluralFormatProvider : IFormatProvider, ICustomFormatter
    {
        public object GetFormat(Type formatType)
        {
            return this;
        }

        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            string[] forms = format.Split(';');
            int value = (int)arg;
            int form = value == 1 ? 0 : 1;
            return value.ToString() + " " + forms[form];
        }
    }
}