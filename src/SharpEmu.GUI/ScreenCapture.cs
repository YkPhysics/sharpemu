// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Runtime.InteropServices;

namespace SharpEmu.GUI;

/// <summary>
/// Windows screen capture for the in-game screenshot action. The emulated
/// frame lives in a native child window whose swapchain the launcher cannot
/// read, so the capture goes through GDI (BitBlt of the screen region) and
/// is encoded to PNG with Avalonia's bitmap encoder — no extra dependencies.
/// </summary>
internal static class ScreenCapture
{
    private const uint SrcCopy = 0x00CC0020;
    private const uint CaptureBlt = 0x40000000;

    /// <summary>
    /// Captures the given physical-pixel screen rectangle into a PNG file.
    /// Windows only; throws on failure so the caller can surface the error.
    /// </summary>
    public static void CaptureToPng(int x, int y, int width, int height, string filePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Screenshots require Windows.");
        }

        var screenDc = GetDC(0);
        if (screenDc == 0)
        {
            throw new InvalidOperationException("Could not access the screen for capture.");
        }

        nint memoryDc = 0;
        nint dibSection = 0;
        nint previousObject = 0;
        try
        {
            memoryDc = CreateCompatibleDC(screenDc);
            var info = new BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                Width = width,
                Height = -height, // top-down rows
                Planes = 1,
                BitCount = 32,
            };
            dibSection = CreateDIBSection(memoryDc, ref info, 0, out var bits, 0, 0);
            if (dibSection == 0 || bits == 0)
            {
                throw new InvalidOperationException("Could not allocate the capture bitmap.");
            }

            previousObject = SelectObject(memoryDc, dibSection);
            if (!BitBlt(memoryDc, 0, 0, width, height, screenDc, x, y, SrcCopy | CaptureBlt))
            {
                throw new InvalidOperationException("Screen copy failed.");
            }

            // GDI leaves the alpha channel undefined; force opaque while
            // pulling the pixels into managed memory for the encoder.
            var pixels = new byte[width * height * 4];
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            for (var index = 3; index < pixels.Length; index += 4)
            {
                pixels[index] = 0xFF;
            }

            using var bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);
            using (var frameBuffer = bitmap.Lock())
            {
                var sourceStride = width * 4;
                for (var row = 0; row < height; row++)
                {
                    Marshal.Copy(
                        pixels,
                        row * sourceStride,
                        frameBuffer.Address + (row * frameBuffer.RowBytes),
                        sourceStride);
                }
            }

            using var stream = File.Create(filePath);
            bitmap.Save(stream);
        }
        finally
        {
            if (previousObject != 0)
            {
                _ = SelectObject(memoryDc, previousObject);
            }

            if (dibSection != 0)
            {
                _ = DeleteObject(dibSection);
            }

            if (memoryDc != 0)
            {
                _ = DeleteDC(memoryDc);
            }

            _ = ReleaseDC(0, screenDc);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int PixelsPerMeterX;
        public int PixelsPerMeterY;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

    [DllImport("user32.dll", EntryPoint = "GetDC")]
    private static extern nint GetDC(nint window);

    [DllImport("user32.dll", EntryPoint = "ReleaseDC")]
    private static extern int ReleaseDC(nint window, nint deviceContext);

    [DllImport("gdi32.dll", EntryPoint = "CreateCompatibleDC")]
    private static extern nint CreateCompatibleDC(nint deviceContext);

    [DllImport("gdi32.dll", EntryPoint = "DeleteDC")]
    private static extern bool DeleteDC(nint deviceContext);

    [DllImport("gdi32.dll", EntryPoint = "CreateDIBSection")]
    private static extern nint CreateDIBSection(
        nint deviceContext,
        ref BitmapInfoHeader info,
        uint usage,
        out nint bits,
        nint fileMapping,
        uint offset);

    [DllImport("gdi32.dll", EntryPoint = "SelectObject")]
    private static extern nint SelectObject(nint deviceContext, nint gdiObject);

    [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
    private static extern bool DeleteObject(nint gdiObject);

    [DllImport("gdi32.dll", EntryPoint = "BitBlt")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(
        nint destination,
        int destinationX,
        int destinationY,
        int width,
        int height,
        nint source,
        int sourceX,
        int sourceY,
        uint operation);
}
