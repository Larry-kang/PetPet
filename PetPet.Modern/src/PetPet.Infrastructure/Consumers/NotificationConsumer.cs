using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using PetPet.Domain.Entities;
using PetPet.Domain.Events;
using PetPet.Infrastructure.Data;

namespace PetPet.Infrastructure.Consumers;

public class NotificationConsumer : IConsumer<MatchSuccessEvent>
{
    private readonly ILogger<NotificationConsumer> _logger;
    private readonly PetPetDbContext _context;

    public NotificationConsumer(ILogger<NotificationConsumer> logger, PetPetDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task Consume(ConsumeContext<MatchSuccessEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("💘 Match Event Received: {Source} <-> {Target}", message.SourceEmail, message.TargetEmail);

        // Simulate Notification by sending a System Message to Chat
        var sysMsg1 = new Message
        {
            SenderEmail = "admin@petpet.com", // System Admin
            ReceiverEmail = message.SourceEmail,
            Content = $"恭喜！您與 {message.TargetEmail} 配對成功！快去打個招呼吧！",
            SentAt = DateTime.UtcNow
        };
        
        var sysMsg2 = new Message
        {
            SenderEmail = "admin@petpet.com",
            ReceiverEmail = message.TargetEmail,
            Content = $"恭喜！您與 {message.SourceEmail} 配對成功！快去打個招呼吧！",
            SentAt = DateTime.UtcNow
        };

        _context.Messages.AddRange(sysMsg1, sysMsg2);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("✅ System Notifications sent to DB.");
    }
}
