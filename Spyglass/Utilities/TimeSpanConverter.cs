using System;
using System.Globalization;
using System.Text.RegularExpressions;
using DSharpPlus.Entities;

namespace Spyglass.Utilities
{
    public class TimeSpanConverter
    {
        private static Regex TimeSpanRegex { get; set; }

        static TimeSpanConverter()
        {
#if NETSTANDARD1_3
            TimeSpanRegex = new Regex(@"^(?<years>\d+y\s*)?(?<weeks>\d+w\s*)?(?<days>\d+d\s*)?(?<hours>\d{1,2}h\s*)?(?<minutes>\d{1,2}m\s*)?(?<seconds>\d{1,2}s\s*)?$", RegexOptions.ECMAScript);
#else
            TimeSpanRegex = new Regex(@"^(?<years>\d+y\s*)?(?<weeks>\d+w\s*)?(?<days>\d+d\s*)?(?<hours>\d{1,2}h\s*)?(?<minutes>\d{1,2}m\s*)?(?<seconds>\d{1,2}s\s*)?$",RegexOptions.ECMAScript | RegexOptions.Compiled);
#endif
        }

        public Optional<TimeSpan> ConvertFromString(string value)
        {
            if (value == null)
            {
                return Optional.FromNoValue<TimeSpan>();
            }

            if (value == "0")
            {
                return Optional.FromValue(TimeSpan.Zero);
            }

            if (int.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            {
                return Optional.FromNoValue<TimeSpan>();
            }

            value = value.ToLowerInvariant();


            if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var result))
            {
                return Optional.FromValue(result);
            }

            var gps = new [] {"years", "weeks", "days", "hours", "minutes", "seconds"};
            var mtc = TimeSpanRegex.Match(value);
            if (!mtc.Success)
            {
                return Optional.FromNoValue<TimeSpan>();
            }

            var y = 0;
            var w = 0;
            var d = 0;
            var h = 0;
            var m = 0;
            var s = 0;
            foreach (var gp in gps)
            {
                var gpc = mtc.Groups[gp].Value;
                if (string.IsNullOrWhiteSpace(gpc))
                {
                    continue;
                }

                var gpt = gpc[gpc.Length - 1];
                int.TryParse(gpc.Substring(0, gpc.Length - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var val);
                switch (gpt)
                {
                    case 'y':
                        y = val;
                        break;
                    
                    case 'w':
                        w = val;
                        break;
                    
                    case 'd':
                        d = val;
                        break;

                    case 'h':
                        h = val;
                        break;

                    case 'm':
                        m = val;
                        break;

                    case 's':
                        s = val;
                        break;
                }
            }

            result = new TimeSpan(d, h, m, s);
            result += TimeSpan.FromDays(w * 7);
            result += TimeSpan.FromDays(y * 365);
            
            return Optional.FromValue(result);
        }
    }
}