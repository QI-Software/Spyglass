using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Spyglass.Database.Moderation
{
    [Table("Moderation.BlacklistedUsers")]
    public class BlacklistedUser
    {
        [Key]
        public long Id { get; private set; }
        
        [Required]
        public BlacklistType Type { get; private set; }
        
        [Required]
        [Column(TypeName = "bigint")]
        public ulong StaffId { get; set; }
        
        [Required]
        [Column(TypeName = "bigint")]
        public ulong UserId { get; set; }
        
        public string Reason { get; private set; }

        public BlacklistedUser(BlacklistType type, ulong userId, ulong staffId, string reason)
        {
            Type = type;
            UserId = userId;
            StaffId = staffId;
            Reason = reason;
        }
    }
}