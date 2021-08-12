using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Spyglass.Database.Moderation
{
    [Table("Moderation.Infractions")]
    public class Infraction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; private set; }

        [Required]
        public InfractionType Type { get; set; }
        
        [Required]
        [Column(TypeName = "timestamp with time zone")]
        public DateTimeOffset Timestamp { get; set; }
        
        [Column(TypeName = "timestamp with time zone")]
        public DateTimeOffset? LastUpdatedAt { get; set; }
        
        [Required]
        [Column(TypeName = "bigint")]
        public ulong StaffId { get; set; }
        
        [Required]
        [Column(TypeName = "bigint")]
        public ulong UserId { get; set; }
        
        public string Reason { get; set; }
        
        public string StaffName { get; set; }
        
        public string UserName { get; set; }
    }
}