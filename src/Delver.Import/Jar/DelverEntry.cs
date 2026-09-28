namespace Delver.Import.Jar;

/// <summary>One archive entry: inner path, classification, and uncompressed size.</summary>
public sealed record DelverEntry(string Path, DelverEntryKind Kind, long Length);
