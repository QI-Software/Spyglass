using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using Spyglass.Utilities;
using Serilog.Core;
using Spyglass.Database;
using Spyglass.Database.Moderation;
using Spyglass.Providers;

namespace Spyglass.Services
{
    public class BlacklistService
    {
        private readonly Logger _log;

        private Dictionary<BlacklistType, List<ulong>> _blacklistCache = new();

        public BlacklistService(Logger log)
        {
            _log = log;
        }

        public void InvalidateCache(SpyglassContext dbContext = null)
        {
            _log.Information("Blacklist: Invalidating cache.");
            
            _blacklistCache = new();
            try
            {
                dbContext ??= new SpyglassContext();
                var count = 0;
                
                foreach (var blacklistedUser in dbContext.BlacklistedUsers)
                {
                    if (!_blacklistCache.ContainsKey(blacklistedUser.Type))
                    {
                        _blacklistCache.Add(blacklistedUser.Type, new List<ulong>());
                    }
                    
                    _blacklistCache[blacklistedUser.Type].Add(blacklistedUser.UserId);
                    count++;
                }
                
                _log.Information(string.Format(new PluralFormatProvider(), "BlacklistService: Cached {0:blacklisted user;blacklisted users}.", count));
            }
            catch (Exception e)
            {
                _log.Warning(e, "Blacklist: Failed to cache currently blacklisted users.");
            }
            finally
            {
                dbContext?.Dispose();
            }
        }
        
        /// <summary>
        /// Check if the user is blacklisted for the given type.
        /// </summary>
        /// <param name="type"> The type of blacklist. </param>
        /// <param name="user"> The user to check for. </param>
        /// <returns> True if blacklisted. </returns>
        public QueryResult<bool> IsBlacklisted(BlacklistType type, DiscordUser user) => IsBlacklisted(type, user.Id);

        /// <summary>
        /// Check if the user is blacklisted for the given type.
        /// </summary>
        /// <param name="type"> The type of blacklist. </param>
        /// <param name="userId"> The user Id to check for. </param>
        /// <returns> True if blacklisted. </returns>
        public QueryResult<bool> IsBlacklisted(BlacklistType type, ulong userId)
        {
            if (!_blacklistCache.ContainsKey(type))
            {
                return QueryResult<bool>.FromError(result: false, message: "Unknown blacklist type.");
            }

            if (_blacklistCache[type].Contains(userId))
            {
                return QueryResult<bool>.FromSuccess(result: true);
            }
            
            return QueryResult<bool>.FromSuccess(result: false);
        }

        /// <summary>
        /// Add a user to a given blacklist.
        /// </summary>
        /// <param name="type"> The type of blacklist. </param>
        /// <param name="user"> The user to blacklist. </param>
        /// <param name="staff"> The staff member who added the blacklist. </param>
        /// <param name="reason"> The reason for the blacklisting. </param>
        /// <returns> True if succeeded. </returns>
        public async Task<QueryResult<bool>> AddUserToBlacklistAsync(BlacklistType type, DiscordUser user, DiscordUser staff, string reason) =>
            await AddUserToBlacklistAsync(type, user.Id, staff.Id, reason);

        /// <summary>
        /// Add a user to a given blacklist.
        /// </summary>
        /// <param name="type"> The type of blacklist. </param>
        /// <param name="userId"> The user id to blacklist. </param>
        /// <param name="staffId"> The staff member id who added the blacklist. </param>
        /// <param name="reason"> The reason for the blacklisting. </param>
        /// <returns> True if succeeded. </returns>
        public async Task<QueryResult<bool>> AddUserToBlacklistAsync(BlacklistType type, ulong userId, ulong staffId, string reason)
        {
            SpyglassContext dbContext = null;
            
            try
            {
                // Create the db context.
                dbContext = new SpyglassContext();

                if (dbContext.BlacklistedUsers.Any(b => b.Type == type && b.UserId == userId))
                {
                    return QueryResult<bool>.FromError("The specified user is already blacklisted.");
                }

                var blacklist = new BlacklistedUser(type, userId, staffId, reason);
                dbContext.BlacklistedUsers.Add(blacklist);
                
                await dbContext.SaveChangesAsync();
                InvalidateCache(dbContext);

                return QueryResult<bool>.FromSuccess("User successfully added to blacklist.");
            }
            catch (Exception e)
            {
                _log.Error(e, "Blacklist: an error has occured while blacklisting a user.");
                return QueryResult<bool>.FromError(e.Message);
            }
            finally
            {
                // Dispose of the context.
                dbContext?.Dispose();
            }
        }
        
        /// <summary>
        /// Remove a user from a given blacklist.
        /// </summary>
        /// <param name="type"> The type of blacklist. </param>
        /// <param name="user"> The user to remove from the blacklist. </param>
        /// <returns> True if succeeded. </returns>
        public async Task<QueryResult<bool>> RemoveUserFromBlacklistAsync(BlacklistType type, DiscordUser user) =>
            await RemoveUserFromBlacklistAsync(type, user.Id);

        /// <summary>
        /// Remove a user from a given blacklist.
        /// </summary>
        /// <param name="type"> The type of blacklist. </param>
        /// <param name="userId"> The user Id to remove from the blacklist. </param>
        /// <returns> True if succeeded. </returns>
        public async Task<QueryResult<bool>> RemoveUserFromBlacklistAsync(BlacklistType type, ulong userId)
        {
            SpyglassContext dbContext = null;
            
            try
            {
                // Create the db context.
                dbContext = new SpyglassContext();

                var blacklistedUser = dbContext.BlacklistedUsers.FirstOrDefault(b => b.Type == type && b.UserId == userId);
                
                if (blacklistedUser == null)
                {
                    return QueryResult<bool>.FromError("The specified user isn't blacklisted.");
                }

                dbContext.BlacklistedUsers.Remove(blacklistedUser);

                // Save it to the database.
                await dbContext.SaveChangesAsync();
                InvalidateCache(dbContext);

                return QueryResult<bool>.FromSuccess("User successfully removed from blacklist.");
            }
            catch (Exception e)
            {
                _log.Error(e, "Blacklist: an error has occured while removing a user from a blacklist.");
                return QueryResult<bool>.FromError(e.Message);
            }
            finally
            {
                // Dispose of the context.
                dbContext?.Dispose();
            }
        }
        
        public QueryResult<List<BlacklistedUser>> GetUserBlacklistInformation(DiscordUser user)
        {
            SpyglassContext dbContext = null;
            
            try
            {
                // Create the db context.
                dbContext = new SpyglassContext();

                var blacklists = dbContext.BlacklistedUsers
                    .Where(b => b.UserId == user.Id)
                    .ToList();
                
                return QueryResult<List<BlacklistedUser>>.FromSuccess($"Successfully retrieve blacklists for {user.Username}#{user.Discriminator}", blacklists);
            }
            catch (Exception e)
            {
                _log.Error(e, "Blacklist: an error has occured while removing a user from a blacklist.");
                return QueryResult<List<BlacklistedUser>>.FromError($"Failed to retrieve blacklists for {user.Username}#{user.Discriminator}: {e.Message}");
            }
            finally
            {
                // Dispose of the context.
                dbContext?.Dispose();
            }
        }
    }
}