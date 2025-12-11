using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Storage.Streams;

namespace FluentZip
{
    internal static class ShellIconService
    {
        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const uint SHGFI_SMALLICON = 0x000000001;

        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            out SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static readonly ConcurrentDictionary<string, BitmapImage> s_smallCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, BitmapImage> s_largeCache = new(StringComparer.OrdinalIgnoreCase);

        public static BitmapImage GetFolderIcon(bool small = true) =>
            GetIconInternal("folder", FILE_ATTRIBUTE_DIRECTORY, small);

        public static BitmapImage GetFileIconByExtension(string? extension, bool small = true)
        {
            var ext = string.IsNullOrWhiteSpace(extension) ? ".unknown" : (extension.StartsWith('.') ? extension : "." + extension);
            return GetIconInternal(ext, FILE_ATTRIBUTE_NORMAL, small);
        }

        private static BitmapImage GetIconInternal(string key, uint fileAttr, bool small)
        {
            var cache = small ? s_smallCache : s_largeCache;
            if (cache.TryGetValue(key, out var cached)) return cached;

            var flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES | (small ? SHGFI_SMALLICON : SHGFI_LARGEICON);
            if (SHGetFileInfo(key, fileAttr, out var info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags) == IntPtr.Zero || info.hIcon == IntPtr.Zero)
            {
                if (key != ".unknown")
                    return GetIconInternal(".unknown", FILE_ATTRIBUTE_NORMAL, small);
                return new BitmapImage();
            }

            try
            {
                using var icon = System.Drawing.Icon.FromHandle(info.hIcon);
                using var bmp = icon.ToBitmap();
                using var ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;

                var image = new BitmapImage();
                image.SetSource(ms.AsRandomAccessStream());
                cache[key] = image;
                return image;
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }
    }
}