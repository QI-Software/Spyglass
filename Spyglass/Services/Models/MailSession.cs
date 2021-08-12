using System;
using DSharpPlus.Entities;

namespace Spyglass.Services.Models
{
    public class MailSession : IEquatable<MailSession>
    {
        public MailSession(ulong mailChannelId, ulong mailUserId, DiscordWebhook webhook)
        {
            MailChannelId = mailChannelId;
            MailUserId = mailUserId;
            Webhook = webhook;
        }

        public ulong MailChannelId { get; private set; }
        
        public ulong MailUserId { get; private set; }
        
        public DiscordWebhook Webhook { get; private set; }

        public bool Equals(MailSession other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return MailUserId == other.MailUserId;
        }
    }
}