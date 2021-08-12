using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Spyglass.Database.ReactionRoles
{
    [Table("Utilities.ReactionRole")]
    public class ReactionRole : IEquatable<ReactionRole>
    {
        [Key] 
        public long Id { get; private set; }
        
        [Required]
        [Column(TypeName = "bigint")]
        public ulong MessageId { get; private set; }
        
        [Required]
        [Column(TypeName = "bigint")]
        public ulong RoleId { get; private set; }
        
        public string ReactionName { get; private set; }
        
        public ulong? ReactionId { get; private set; }

        public ReactionRole(ulong messageId, ulong roleId, ulong? reactionId = null, string reactionName = null)
        {
            MessageId = messageId;
            RoleId = roleId;
            ReactionId = reactionId;
            ReactionName = reactionName;
        }

        public bool Equals(ReactionRole other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return MessageId == other.MessageId && RoleId == other.RoleId && ReactionName == other.ReactionName && ReactionId == other.ReactionId;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ReactionRole) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(MessageId, RoleId, ReactionName, ReactionId);
        }
    }
}