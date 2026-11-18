namespace KleinGeld.Contracts;

/// <summary>
/// A short-lived link the browser PUTs one file to, straight to blob storage (with an <c>x-ms-blob-type: BlockBlob</c>
/// header). It then tells the API the upload is complete, naming <paramref name="UploadId"/>.
/// </summary>
public sealed record UploadLink(Guid UploadId, Uri Url, DateTimeOffset ExpiresOn);
