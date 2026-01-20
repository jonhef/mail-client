namespace MailClient.Server.Models;

public record FolderDto(string Id, string Name, int Unread, string Role);

public record MessageHeaderDto(
    string Id,
    string FolderId,
    string Subject,
    string FromName,
    string FromEmail,
    string DateIso,
    bool IsUnread,
    bool HasAttachments,
    long Size
);

public record AttachmentDto(string Id, string FileName, string ContentType, long Size);

public record MessageDto(
    string Id,
    string FolderId,
    string Subject,
    string FromName,
    string FromEmail,
    string[] To,
    string[] Cc,
    string DateIso,
    bool IsUnread,
    bool HasAttachments,
    string BodyHtml,
    string BodyText,
    AttachmentDto[] Attachments
);

public record ListMessagesResponse(MessageHeaderDto[] Items, string? NextCursor);

public record DiscoverRequest(
    string Email,
    string? ProviderHint
);

public record DiscoverResponse(
    ServerEndpoint Imap,
    ServerEndpoint Smtp,
    string ProviderHint
);

public record ValidateSettingsRequest(
    string Email,
    string? Password,
    ServerEndpoint Imap,
    ServerEndpoint Smtp
);

public record ValidateSettingsResponse(
    bool Ok,
    string? Message
);

public record CreateAccountRequest(
    string Email,
    string DisplayName,
    string? Password,
    string? ProviderHint,
    ServerEndpoint? Imap,
    ServerEndpoint? Smtp,
    ServerEndpoint? Pop3
);

public record CreateAccountResponse(AccountConfig Config);

public record SendEmailRequest(
    string FolderIdSent,
    string To,
    string? Cc,
    string? Bcc,
    string Subject,
    string BodyText,
    string? BodyHtml
);

public record UpdateMessageRequest(
    bool? MarkRead,
    bool? MarkUnread,
    bool? Delete,
    string? MoveToFolderId
);
