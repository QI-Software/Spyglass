using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Spyglass.Database.Tags
{
    [Table("Utilities.Tags")]
    public class Tag
    {
        [Key]
        public long Id { get; private set; }
        
        [Required]
        public string Name { get; set; }
        
        [Required]
        [Column(TypeName = "bigint")]
        public ulong CreatedBy { get; set; }
        
        [Required]
        [Column(TypeName = "timestamp with time zone")]
        public DateTimeOffset CreatedAt { get; private set; }
        
        [Required]
        public uint Uses { get; set; }
        
        [Required]
        public string Value { get; set; }
        
        public ulong? LastUpdatedBy { get; set; }
        
        [Column(TypeName = "timestamp with time zone")]
        public DateTimeOffset? LastUpdatedAt { get; private set; }

        public Tag(string name, ulong createdBy, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Tag name cannot be null or empty", nameof(name));
            }
            
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Tag value cannot be null or empty", nameof(value));
            }

            Name = name;
            CreatedBy = createdBy;
            CreatedAt = DateTimeOffset.Now;
            Uses = 0;
            Value = value;
        }
    }
}