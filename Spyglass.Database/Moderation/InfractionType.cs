using NpgsqlTypes;

namespace Spyglass.Database.Moderation
{
    public enum InfractionType
    {
        [PgName("Note")]
        Note,
        [PgName("Warn")]
        Warn,
        [PgName("Mute")]
        Mute,
        [PgName("Kick")]
        Kick,
        [PgName("Ban")]
        Ban,
        [PgName("Unmute")]
        Unmute,
        [PgName("Unban")]
        Unban,
        [PgName("Undeafen")]
        Undeafen,
    }
}