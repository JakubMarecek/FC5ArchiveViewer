namespace FC5ArchiveViewer
{
    public enum CompressionScheme : byte
    {
        None = 0,
        LZO1x = 1,
        Zlib = 2,
        Unknown3 = 3,
    }

    public struct FatEntry
    {
        public ulong NameHash;
        public uint UncompressedSize;
        public uint CompressedSize;
        public long Offset;
        public CompressionScheme CompressionScheme;
        public long AvailableSpace;
    }
}
