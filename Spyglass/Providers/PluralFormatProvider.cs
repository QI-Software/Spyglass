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
            long value = Convert.ToInt64(arg);
            long form = value == 1 ? 0 : 1;
            return value + " " + forms[form];
        }
    }
}