namespace PetPet.Application.Media;

public record MediaUploadResult(
    Guid Id,
    string Url,
    string ContentType,
    long ByteLength,
    int Width,
    int Height
);

public record MediaFileResult(
    Stream Stream,
    string ContentType,
    long ByteLength
);
