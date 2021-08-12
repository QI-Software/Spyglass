using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Spyglass.Database.Moderation
{
    [Table("Moderation.Ongoing")]
    public class OngoingModeration
    {
        [Key]
        public long Id { get; private set; }
        
        [Required] 
        public ulong UserId { get; private set; }

        [Required]
        [Column(TypeName = "timestamp with time zone")]
        public DateTimeOffset EndTime { get; private set; }

        [Required]
        public InfractionType Type { get; private set; }

        [Required]
        public long LinkedInfractionId { get; private set; }
        
        [Required]
        [ForeignKey("LinkedInfractionId")]
        public Infraction LinkedInfraction { get; private set; }

        public OngoingModeration(ulong userId, DateTimeOffset endTime, InfractionType type, long linkedInfractionId)
        {
            UserId = userId;
            EndTime = endTime;
            Type = type;
            LinkedInfractionId = linkedInfractionId;
        }
    }
}