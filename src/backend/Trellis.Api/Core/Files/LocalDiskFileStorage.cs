using Microsoft.Extensions.Options;

namespace Trellis.Api.Core.Files;

/// <summary>The real, working Phase 3 <see cref="IFileStorage"/> — see its own doc comment for why a real cloud backend isn't wired here yet.</summary>
public sealed class LocalDiskFileStorage(IOptions<FileStorageOptions> options) : IFileStorage
{
    private readonly string _root = options.Value.RootPath;

    public async Task<string> SaveAsync(Stream content, string suggestedKey)
    {
        Directory.CreateDirectory(_root);

        // suggestedKey may collide (two uploads of files with the same name) — prefixing with a
        // fresh Guid keeps every stored key unique without the caller having to guarantee that.
        var storageKey = $"{Guid.NewGuid()}-{suggestedKey}";
        var path = Path.Combine(_root, storageKey);

        await using var destination = File.Create(path);
        await content.CopyToAsync(destination);

        return storageKey;
    }

    public Task<Stream> OpenReadAsync(string storageKey) =>
        Task.FromResult<Stream>(File.OpenRead(Path.Combine(_root, storageKey)));
}
