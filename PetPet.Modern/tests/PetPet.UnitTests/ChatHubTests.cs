using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;
using PetPet.Web.Hubs;
using Xunit;

namespace PetPet.UnitTests
{
    public class ChatHubTests
    {
        [Fact]
        public async Task SendMessage_ShouldSaveToDb_AndInvokeClients()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<PetPetDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new PetPetDbContext(options);
            var hub = new ChatHub(context);

            var mockClients = new Mock<IHubCallerClients>();
            var mockClientProxy = new Mock<ISingleClientProxy>();
            
            // Setup Clients.User(...) returns a proxy
            mockClients.Setup(c => c.User(It.IsAny<string>())).Returns(mockClientProxy.Object);
            // Fix: Mock Caller as well
            mockClients.Setup(c => c.Caller).Returns(mockClientProxy.Object);

            // Mock Context.UserIdentifier
            var mockContext = new Mock<HubCallerContext>();
            mockContext.Setup(c => c.UserIdentifier).Returns("sender@test.com");

            hub.Context = mockContext.Object;
            hub.Clients = mockClients.Object;

            // Act
            await hub.SendMessage("receiver@test.com", "Hello World");

            // Assert
            // 1. DB Save
            var msg = await context.Messages.FirstOrDefaultAsync();
            Assert.NotNull(msg);
            Assert.Equal("sender@test.com", msg.SenderEmail);
            Assert.Equal("receiver@test.com", msg.ReceiverEmail);
            Assert.Equal("Hello World", msg.Content);

            // 2. Clients.User(...).SendAsync("ReceiveMessage", ...)
            mockClientProxy.Verify(
                client => client.SendCoreAsync(
                    "ReceiveMessage",
                    It.Is<object[]>(args => 
                        args.Length == 2 && 
                        (string)args[0] == "sender@test.com" && 
                        (string)args[1] == "Hello World"),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));

            // 3. Clients.User(...).SendAsync("ReceiveNotification", ...)
            mockClientProxy.Verify(
                client => client.SendCoreAsync(
                    "ReceiveNotification",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        ((string)args[0]).Contains("新訊息")),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
