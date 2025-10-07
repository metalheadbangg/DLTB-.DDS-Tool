using System;
using System.IO;

public static class DdsHelper
{
    private const uint DDSD_CAPS = 0x1;
    private const uint DDSD_HEIGHT = 0x2;
    private const uint DDSD_WIDTH = 0x4;
    private const uint DDSD_PITCH = 0x8;
    private const uint DDSD_PIXELFORMAT = 0x1000;
    private const uint DDSD_MIPMAPCOUNT = 0x20000;
    private const uint DDSD_LINEARSIZE = 0x80000;
    private const uint DDSD_DEPTH = 0x800000;

    private const uint DDPF_ALPHAPIXELS = 0x1;
    private const uint DDPF_FOURCC = 0x4;
    private const uint DDPF_RGB = 0x40;

    private const uint DDSCAPS_COMPLEX = 0x8;
    private const uint DDSCAPS_TEXTURE = 0x1000;
    private const uint DDSCAPS_MIPMAP = 0x400000;

    private const uint DDSCAPS2_CUBEMAP = 0x200;
    private const uint DDSCAPS2_VOLUME = 0x200000;

    // FourCC kodları
    private static readonly uint FOURCC_DXT1 = MakeFourCC('D', 'X', 'T', '1');
    private static readonly uint FOURCC_DX10 = MakeFourCC('D', 'X', '1', '0');

    private static uint MakeFourCC(char c1, char c2, char c3, char c4)
    {
        return (uint)c1 | ((uint)c2 << 8) | ((uint)c3 << 16) | ((uint)c4 << 24);
    }

    public static byte[] GenerateDdsHeader(uint width, uint height, uint mipCount, uint gameFormat, uint texType, uint depth)
    {
        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            uint bpp = 0;
            bool isCompressed = false;
            uint pitchOrLinearSizeFlag = 0;

            uint formatForDx10 = gameFormat;
            bool useDx10Header = true;

            switch (gameFormat)
            {
                case 0: case 1: case 5: bpp = 8; pitchOrLinearSizeFlag = DDSD_PITCH; break;
                case 7: case 8: case 10: case 15: case 16: case 17: bpp = 16; pitchOrLinearSizeFlag = DDSD_PITCH; break;
                case 20: case 21: case 22: case 32: case 38: case 39: case 40: bpp = 32; pitchOrLinearSizeFlag = DDSD_PITCH; break;
                case 46: case 47: case 48: bpp = 64; pitchOrLinearSizeFlag = DDSD_PITCH; break;
                case 59: // BC1_UNORM (DXT1)
                    bpp = 4; isCompressed = true; pitchOrLinearSizeFlag = DDSD_LINEARSIZE; useDx10Header = false; break;
                case 62: case 63: bpp = 4; isCompressed = true; pitchOrLinearSizeFlag = DDSD_LINEARSIZE; break; // BC4
                case 64: case 65: case 66: case 68: bpp = 8; isCompressed = true; pitchOrLinearSizeFlag = DDSD_LINEARSIZE; break; // BC5, BC6, BC7
                default: throw new NotSupportedException($"Unsupported texture format: {gameFormat}");
            }

            if (useDx10Header)
            {
                switch (gameFormat)
                {
                    case 0: case 5: formatForDx10 = 61; break; // R8_UNORM
                    case 1: formatForDx10 = 63; break; // R8_SNORM
                    case 7: formatForDx10 = 56; break; // R16_UNORM
                    case 32: case 38: formatForDx10 = 28; break; // R8G8B8A8_UNORM
                    case 59: formatForDx10 = 71; break; // BC1_UNORM
                    case 63: formatForDx10 = 80; break; // BC4_UNORM
                    case 65: formatForDx10 = 83; break; // BC5_UNORM
                    case 68: formatForDx10 = 98; break; // BC7_UNORM
                }
            }

            // Main Header
            writer.Write(MakeFourCC('D', 'D', 'S', ' '));
            writer.Write(124); // Size

            uint flags = DDSD_CAPS | DDSD_PIXELFORMAT | DDSD_WIDTH | DDSD_HEIGHT | pitchOrLinearSizeFlag;
            if (mipCount > 1) flags |= DDSD_MIPMAPCOUNT;
            if (depth > 1) flags |= DDSD_DEPTH;
            writer.Write(flags);

            writer.Write(height);
            writer.Write(width);

            uint pitch = isCompressed
                ? Math.Max(1, ((width + 3) / 4)) * (bpp * 2)
                : (width * bpp + 7) / 8;
            writer.Write(isCompressed ? (width * height * bpp / 8) : pitch);

            writer.Write(depth > 1 ? depth : 0);
            writer.Write(mipCount > 1 ? mipCount : 0);
            writer.Write(new byte[44]);

            writer.Write(32);
            writer.Write(DDPF_FOURCC);
            writer.Write(useDx10Header ? FOURCC_DX10 : FOURCC_DXT1);
            writer.Write(new byte[20]);

            // Caps
            uint caps = DDSCAPS_TEXTURE;
            if (mipCount > 1) caps |= DDSCAPS_COMPLEX | DDSCAPS_MIPMAP;
            if (depth > 1 || texType != 0) caps |= DDSCAPS_COMPLEX;
            writer.Write(caps);

            uint caps2 = 0;
            if (texType == 1) caps2 |= DDSCAPS2_CUBEMAP;
            if (texType == 2) caps2 |= DDSCAPS2_VOLUME;
            writer.Write(caps2);
            writer.Write(new byte[12]);

            if (useDx10Header)
            {
                writer.Write(formatForDx10); // dxgiFormat
                writer.Write(3); // resourceDimension (D3D10_RESOURCE_DIMENSION_TEXTURE2D)
                writer.Write(0); // miscFlag
                writer.Write(1); // arraySize
                writer.Write(0); // miscFlags2
            }

            return ms.ToArray();
        }
    }
}