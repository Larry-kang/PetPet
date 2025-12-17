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
            
            // 3. Echo back to Sender (so their UI updates)
            await Clients.Caller.SendAsync("ReceiveMessage", senderEmail, message);

            // 4. Send Notification Signal (For global toast/badge)
            // Note: In a real app, we might check if user is online or active in chat, but here we just blast it.
            // Client side logic can filter duplicates if needed, or we rely on different event names.
            await Clients.User(receiverEmail).SendAsync("ReceiveNotification", $"來自 {senderEmail} 的新訊息: {message.Substring(0, Math.Min(message.Length, 20))}...");

            // 5. AI Auto-Reply Handler
            if (receiverEmail == "ai@petpet.com")
            {
                await HandleAIReply(senderEmail, message);
            }
        }

        private async Task HandleAIReply(string userEmail, string userMessage)
        {
            await Task.Delay(1500); // Simulate typing delay

            string reply = "汪汪！我是 PetPet AI 助手。";
            string lowerMsg = userMessage.ToLower();

            if (lowerMsg.Contains("hi") || lowerMsg.Contains("hello") || lowerMsg.Contains("你好"))
            {
                reply = "嗨！很高興見到你！有什麼我可以幫你的嗎？🐶";
            }
            else if (lowerMsg.Contains("教學") || lowerMsg.Contains("guide") || lowerMsg.Contains("help"))
            {
                reply = "📚 **PetPet 使用教學**：\n1. 到「緣分匹配」滑動卡片尋找對象。\n2. 配對成功後到「聊天室」開始對話。\n3. 不需要的功能可以忽略，專注於找到你的靈魂伴侶！";
            }
            else if (lowerMsg.Contains("配對") || lowerMsg.Contains("match") || lowerMsg.Contains("tips") || lowerMsg.Contains("建議"))
            {
                reply = "💡 **增加配對成功率的小撇步**：\n1. 上傳清晰、明亮的毛孩照片。\n2. 填寫有趣的自我介紹。\n3. 使用我們最新的 **紫微斗數** 功能查看命定對象！\n4. 多多滑動卡片，緣分就在下一張！";
            }
            else if (lowerMsg.Contains("紫微") || lowerMsg.Contains("算命"))
            {
                 reply = "🔮 我們的「紫微斗數配對」是根據您的生日計算主星。\n趕快去匹配頁面看看誰是您的命定之人吧！";
            }
            else
            {
                reply = "汪！我還在學習人類的語言，不過你可以問我關於「教學」、「配對建議」或「紫微」的問題喔！🐾";
            }

            // Save AI Reply to DB
            var aiMsg = new Message
            {
                SenderEmail = "ai@petpet.com",
                ReceiverEmail = userEmail,
                Content = reply,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };
            _context.Messages.Add(aiMsg);
            await _context.SaveChangesAsync();

            // Send to User
            await Clients.User(userEmail).SendAsync("ReceiveMessage", "ai@petpet.com", reply);
            await Clients.User(userEmail).SendAsync("ReceiveNotification", $"來自 PetPet AI 助手的回覆");
        }
    }
}
