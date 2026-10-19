using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetPet.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "City",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_City", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Member",
                columns: table => new
                {
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    PublicId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Password = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Birthday = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Gender = table.Column<bool>(type: "INTEGER", nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    CityId = table.Column<int>(type: "INTEGER", nullable: true),
                    Bio = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Photo = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsMatchEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsAdmin = table.Column<bool>(type: "INTEGER", nullable: false),
                    SecurityStamp = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Member", x => x.Email);
                });

            migrationBuilder.CreateTable(
                name: "News",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    PhotoUrl = table.Column<string>(type: "TEXT", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_News", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SeedRecord",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GeneratorVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    Seed = table.Column<long>(type: "INTEGER", nullable: false),
                    BaseTimeUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CountsJson = table.Column<string>(type: "TEXT", nullable: false),
                    BusinessFingerprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeedRecord", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Variety",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    KindCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Variety", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminAudit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActorEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    TargetId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    BeforeJson = table.Column<string>(type: "TEXT", nullable: true),
                    AfterJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TraceId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAudit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminAudit_Member_ActorEmail",
                        column: x => x.ActorEmail,
                        principalTable: "Member",
                        principalColumn: "Email",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Block",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BlockerEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    BlockedEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Block", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Block_Member_BlockedEmail",
                        column: x => x.BlockedEmail,
                        principalTable: "Member",
                        principalColumn: "Email",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Block_Member_BlockerEmail",
                        column: x => x.BlockerEmail,
                        principalTable: "Member",
                        principalColumn: "Email",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Friend",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RequesterEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    AddresseeEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    MemberAEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    MemberBEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    IsAccepted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Friend", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Friend_Member_AddresseeEmail",
                        column: x => x.AddresseeEmail,
                        principalTable: "Member",
                        principalColumn: "Email",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Friend_Member_RequesterEmail",
                        column: x => x.RequesterEmail,
                        principalTable: "Member",
                        principalColumn: "Email",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Media",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    StorageKey = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ByteLength = table.Column<long>(type: "INTEGER", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OrphanedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Media", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Media_Member_OwnerEmail",
                        column: x => x.OwnerEmail,
                        principalTable: "Member",
                        principalColumn: "Email",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Message",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SenderEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    ReceiverEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    ConversationPair = table.Column<string>(type: "TEXT", maxLength: 510, nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ClientMessageId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Message", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Message_Member_ReceiverEmail",
                        column: x => x.ReceiverEmail,
                        principalTable: "Member",
                        principalColumn: "Email",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Message_Member_SenderEmail",
                        column: x => x.SenderEmail,
                        principalTable: "Member",
                        principalColumn: "Email",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecipientEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    ActorEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EventKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TargetType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TargetPublicId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TargetId = table.Column<int>(type: "INTEGER", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notification", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notification_Member_ActorEmail",
                        column: x => x.ActorEmail,
                        principalTable: "Member",
                        principalColumn: "Email",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notification_Member_RecipientEmail",
                        column: x => x.RecipientEmail,
                        principalTable: "Member",
                        principalColumn: "Email",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationReceipt",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActorEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    Operation = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CommandId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PayloadDigest = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ResultCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TargetId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationReceipt", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationReceipt_Member_ActorEmail",
                        column: x => x.ActorEmail,
                        principalTable: "Member",
                        principalColumn: "Email",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pet",
                columns: table => new
                {
                    Pet_no = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    PetVariety_no = table.Column<int>(type: "INTEGER", nullable: true),
                    KindCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Pet_name = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Pet_gender = table.Column<bool>(type: "INTEGER", nullable: true),
                    BirthDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EstimatedAgeMonths = table.Column<int>(type: "INTEGER", nullable: true),
                    Bio = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    Pet_photo = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pet", x => x.Pet_no);
                    table.ForeignKey(
                        name: "FK_Pet_Member_Email",
                        column: x => x.Email,
                        principalTable: "Member",
                        principalColumn: "Email",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Post",
                columns: table => new
                {
                    Post_no = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Post_Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    Post_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Post_title = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Post_content = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Post_img = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Post_Enable = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Post", x => x.Post_no);
                    table.ForeignKey(
                        name: "FK_Post_Member_Post_Email",
                        column: x => x.Post_Email,
                        principalTable: "Member",
                        principalColumn: "Email",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Comment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PostId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comment_Member_UserEmail",
                        column: x => x.UserEmail,
                        principalTable: "Member",
                        principalColumn: "Email",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Comment_Post_PostId",
                        column: x => x.PostId,
                        principalTable: "Post",
                        principalColumn: "Post_no",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Like",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PostId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Like", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Like_Member_UserEmail",
                        column: x => x.UserEmail,
                        principalTable: "Member",
                        principalColumn: "Email",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Like_Post_PostId",
                        column: x => x.PostId,
                        principalTable: "Post",
                        principalColumn: "Post_no",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Report",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PostId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReporterEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    HandledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HandledByEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    HandlingNote = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Report", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Report_Member_HandledByEmail",
                        column: x => x.HandledByEmail,
                        principalTable: "Member",
                        principalColumn: "Email",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Report_Member_ReporterEmail",
                        column: x => x.ReporterEmail,
                        principalTable: "Member",
                        principalColumn: "Email",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Report_Post_PostId",
                        column: x => x.PostId,
                        principalTable: "Post",
                        principalColumn: "Post_no",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAudit_ActorEmail",
                table: "AdminAudit",
                column: "ActorEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Block_BlockedEmail",
                table: "Block",
                column: "BlockedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Block_BlockerEmail_BlockedEmail",
                table: "Block",
                columns: new[] { "BlockerEmail", "BlockedEmail" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Comment_PostId_IsDeleted_CreatedAt_Id",
                table: "Comment",
                columns: new[] { "PostId", "IsDeleted", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Comment_UserEmail",
                table: "Comment",
                column: "UserEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Friend_AddresseeEmail_IsAccepted",
                table: "Friend",
                columns: new[] { "AddresseeEmail", "IsAccepted" });

            migrationBuilder.CreateIndex(
                name: "IX_Friend_MemberAEmail_MemberBEmail",
                table: "Friend",
                columns: new[] { "MemberAEmail", "MemberBEmail" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Friend_RequesterEmail_IsAccepted",
                table: "Friend",
                columns: new[] { "RequesterEmail", "IsAccepted" });

            migrationBuilder.CreateIndex(
                name: "IX_Like_PostId_UserEmail",
                table: "Like",
                columns: new[] { "PostId", "UserEmail" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Like_UserEmail",
                table: "Like",
                column: "UserEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Media_OwnerEmail",
                table: "Media",
                column: "OwnerEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Member_NormalizedEmail",
                table: "Member",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Member_PublicId",
                table: "Member",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Message_ConversationPair_SentAt_Id",
                table: "Message",
                columns: new[] { "ConversationPair", "SentAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Message_ReceiverEmail",
                table: "Message",
                column: "ReceiverEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Message_SenderEmail_ClientMessageId",
                table: "Message",
                columns: new[] { "SenderEmail", "ClientMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notification_ActorEmail",
                table: "Notification",
                column: "ActorEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_RecipientEmail_EventKey",
                table: "Notification",
                columns: new[] { "RecipientEmail", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notification_RecipientEmail_ReadAt_CreatedAt_Id",
                table: "Notification",
                columns: new[] { "RecipientEmail", "ReadAt", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationReceipt_ActorEmail_Operation_CommandId",
                table: "OperationReceipt",
                columns: new[] { "ActorEmail", "Operation", "CommandId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pet_Email_Pet_no",
                table: "Pet",
                columns: new[] { "Email", "Pet_no" });

            migrationBuilder.CreateIndex(
                name: "IX_Post_Post_Email",
                table: "Post",
                column: "Post_Email");

            migrationBuilder.CreateIndex(
                name: "IX_Post_Post_Enable_IsDeleted_Post_time_Post_no",
                table: "Post",
                columns: new[] { "Post_Enable", "IsDeleted", "Post_time", "Post_no" });

            migrationBuilder.CreateIndex(
                name: "IX_Report_HandledByEmail",
                table: "Report",
                column: "HandledByEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Report_PostId",
                table: "Report",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_Report_ReporterEmail_PostId",
                table: "Report",
                columns: new[] { "ReporterEmail", "PostId" });

            migrationBuilder.CreateIndex(
                name: "IX_Report_Status_CreatedAt_Id",
                table: "Report",
                columns: new[] { "Status", "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAudit");

            migrationBuilder.DropTable(
                name: "Block");

            migrationBuilder.DropTable(
                name: "City");

            migrationBuilder.DropTable(
                name: "Comment");

            migrationBuilder.DropTable(
                name: "Friend");

            migrationBuilder.DropTable(
                name: "Like");

            migrationBuilder.DropTable(
                name: "Media");

            migrationBuilder.DropTable(
                name: "Message");

            migrationBuilder.DropTable(
                name: "News");

            migrationBuilder.DropTable(
                name: "Notification");

            migrationBuilder.DropTable(
                name: "OperationReceipt");

            migrationBuilder.DropTable(
                name: "Pet");

            migrationBuilder.DropTable(
                name: "Report");

            migrationBuilder.DropTable(
                name: "SeedRecord");

            migrationBuilder.DropTable(
                name: "Variety");

            migrationBuilder.DropTable(
                name: "Post");

            migrationBuilder.DropTable(
                name: "Member");
        }
    }
}
