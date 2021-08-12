using System;
using Spyglass.Database.Moderation;
using Spyglass.Providers;

namespace Spyglass
{
    public static class Format
    {
        public static string GetInfractionTypeString(InfractionType type, int count = 1)
        {
            switch (type)
            {
                case InfractionType.Note:
                    return String.Format(new PluralFormatProvider(), "{0:Note;Notes}", count);
                case InfractionType.Warn:
                    return String.Format(new PluralFormatProvider(), "{0:Warn;Warns}", count);
                case InfractionType.Mute:
                    return String.Format(new PluralFormatProvider(), "{0:Mute;Mutes}", count);
                case InfractionType.Kick:
                    return String.Format(new PluralFormatProvider(), "{0:Kick;Kicks}", count);
                case InfractionType.Ban:
                    return String.Format(new PluralFormatProvider(), "{0:Ban;Bans}", count);
                case InfractionType.Unmute:
                    return String.Format(new PluralFormatProvider(), "{0:Unmute;Unmutes}", count);
                case InfractionType.Unban:
                    return String.Format(new PluralFormatProvider(), "{0:Unban;Unbans}", count);
                case InfractionType.Undeafen:
                    return String.Format(new PluralFormatProvider(), "{0:Undeafen;Undeafens}", count);
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
        
        public static string GetTimespanString(TimeSpan span)
        {
            string formatted = string.Format("{0}{1}{2}{3}",
                span.Duration().Days > 0 ? $"{span.Days:0} day{(span.Days == 1 ? string.Empty : "s")}, " : string.Empty,
                span.Duration().Hours > 0 ? $"{span.Hours:0} hour{(span.Hours == 1 ? string.Empty : "s")}, " : string.Empty,
                span.Duration().Minutes > 0 ? $"{span.Minutes:0} minute{(span.Minutes == 1 ? string.Empty : "s")}, " : string.Empty,
                span.Duration().Seconds > 0 ? $"{span.Seconds:0} second{(span.Seconds == 1 ? string.Empty : "s")}" : string.Empty);

            if (formatted.EndsWith(", ")) formatted = formatted.Substring(0, formatted.Length - 2);

            if (string.IsNullOrEmpty(formatted)) formatted = "0 seconds";

            return formatted;
        }
    }
}