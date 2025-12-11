using System;
using System.Collections.Generic;

namespace FluentZip
{
    internal sealed class AddFilesDialogResult
    {
        public IReadOnlyList<AddFileCandidate> Files { get; init; } = Array.Empty<AddFileCandidate>();
        public int CompressionLevel { get; init; } = 2;
        public bool ShouldTestArchive { get; init; }
        public bool DeleteSourceAfterAdd { get; init; }
    }

    internal sealed class AddFileCandidate
    {
        public AddFileCandidate(string displayName, string sourcePath, long size)
        {
            DisplayName = displayName;
            SourcePath = sourcePath;
            Size = size;
            SizeDisplay = FormatSize(size);
        }

        public string DisplayName { get; }
        public string SourcePath { get; }
        public long Size { get; }
        public string SizeDisplay { get; }

        private static string FormatSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            double number = bytes;
            while (Math.Abs(number) >= 1024 && counter < suffixes.Length - 1)
            {
                counter++;
                number /= 1024d;
            }
            return $"{number:0.##} {suffixes[counter]}";
        }
    }
}
