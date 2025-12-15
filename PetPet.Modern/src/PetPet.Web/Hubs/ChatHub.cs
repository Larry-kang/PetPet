using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;
using System.Security.Claims;

namespace PetPet.Web.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly PetPetDbContext _context;

        public ChatHub(PetPetDbContext context)
        {
            _context = context;
        }

        public async Task SendMessage(string receiverEmail, string message)
        {
            var senderEmail = Context.UserIdentifier; // Should match ClaimTypes.NameIdentifier (Email)
            if (string.IsNullOrEmpty(senderEmail)) return;

            // 1. Save to DB
            var msg = new Message
            {
                SenderEmail = senderEmail,
                ReceiverEmail = receiverEmail,
                Content = message,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Messages.Add(msg);
            await _context.SaveChangesAsync();

            // 2. Send to Receiver directly (Assuming UserIdentifier maps to Email)
            await Clients.User(receiverEmail).SendAsync("ReceiveMessage", senderEmail, message);
        }
    }
}
