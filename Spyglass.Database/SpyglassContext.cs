using System;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Spyglass.Database.Moderation;
using Spyglass.Database.ReactionRoles;

namespace Spyglass.Database
{
    public class SpyglassContext : DbContext
    {
        private readonly string _dbConnString;

        // Used to map enum types.
        static SpyglassContext()
        {
            NpgsqlConnection.GlobalTypeMapper.MapEnum<BlacklistType>("BlacklistType");
            NpgsqlConnection.GlobalTypeMapper.MapEnum<InfractionType>("InfractionType");
        }
        
        public SpyglassContext()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _dbConnString = Environment.GetEnvironmentVariable("SPYGLASS_DBCONNSTRING", EnvironmentVariableTarget.User)
                    ?? Environment.GetEnvironmentVariable("SPYGLASS_DBCONNSTRING", EnvironmentVariableTarget.Machine);
            }
            else
            {
                _dbConnString = Environment.GetEnvironmentVariable("SPYGLASS_DBCONNSTRING", EnvironmentVariableTarget.Process);
            }
        }
        
        public DbSet<BlacklistedUser> BlacklistedUsers { get; set; }

        public DbSet<Infraction> Infractions { get; private set; }
        
        public DbSet<OngoingModeration> OngoingModerations { get; private set; }
        
        public DbSet<ReactionRole> ReactionRoles { get; private set; }
        

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_dbConnString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasSequence<int>("Moderation.BlacklistedUsers_ID_seq")
                .StartsAt(0)
                .IncrementsBy(1)
                .HasMin(0);
            
            modelBuilder.HasSequence<int>("Moderation.Infractions_ID_seq")
                .StartsAt(0)
                .IncrementsBy(1)
                .HasMin(0);

            modelBuilder.HasSequence<int>("Moderation.Ongoing_ID_seq")
                .StartsAt(0)
                .IncrementsBy(1)
                .HasMin(0);
            
            modelBuilder.HasSequence<int>("Moderation.ReactionRole_ID_seq")
                .StartsAt(0)
                .IncrementsBy(1)
                .HasMin(0);

            modelBuilder.HasPostgresEnum<InfractionType>(name: "InfractionType")
                .HasPostgresEnum<BlacklistType>(name: "BlacklistType")
                .Entity<BlacklistedUser>(entity =>
                {
                    entity.Property(p => p.Id)
                        .HasDefaultValueSql("nextval('\"Moderation.BlacklistedUsers_ID_seq\"'::regclass)");
                })
                .Entity<Infraction>(entity =>
                {
                    entity.Property(e => e.Id)
                        .HasDefaultValueSql("nextval('\"Moderation.Infractions_ID_seq\"'::regclass)");
                })
                .Entity<OngoingModeration>(entity =>
                {
                    entity.Property(e => e.Id)
                        .HasDefaultValueSql("nextval('\"Moderation.Ongoing_ID_seq\"'::regclass)");
                })
                .Entity<ReactionRole>(entity =>
                {
                    entity.Property(e => e.Id)
                        .HasDefaultValueSql("nextval('\"Moderation.ReactionRole_ID_seq\"'::regclass)");

                    entity.Property(e => e.ReactionId).IsRequired(false);
                });
        }
    }
}