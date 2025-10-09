using System;
using System.IO;

public static class DdsHelper
{
    private static readonly uint FOURCC_DXT1 = MakeFourCC('D', 'X', 'T', '1');
    private static readonly uint FOURCC_DXT3 = MakeFourCC('D', 'X', 'T', '3');
    private static readonly uint FOURCC_DXT5 = MakeFourCC('D', 'X', 'T', '5');
    private static readonly uint FOURCC_DX10 = MakeFourCC('D', 'X', '1', '0');

    public static byte[] GenerateDdsHeader(uint width, uint height, uint mipCount, string formatString, uint texType, uint depth)
    {
        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            uint pitchOrLinearSize = 0;
            uint pitchOrLinearSizeFlag = 0;
            uint fourCC = 0;
            uint formatForDx10 = 0;
            bool useDx10Header = false;
            uint bitsPerPixel = 0;
            bool isCompressed = false;
            uint blockSize = 0;

            switch (formatString.ToUpper())
            {
                case "BC1": fourCC = FOURCC_DXT1; isCompressed = true; blockSize = 8; break;
                case "BC2": fourCC = FOURCC_DXT3; isCompressed = true; blockSize = 16; break;
                case "BC3": fourCC = FOURCC_DXT5; isCompressed = true; blockSize = 16; break;
                case "BC4_UNORM": useDx10Header = true; formatForDx10 = 80; isCompressed = true; blockSize = 8; break;
                case "BC4": useDx10Header = true; formatForDx10 = 80; isCompressed = true; blockSize = 8; break;
                case "BC4_SNORM": useDx10Header = true; formatForDx10 = 81; isCompressed = true; blockSize = 8; break;
                case "BC5": useDx10Header = true; formatForDx10 = 83; isCompressed = true; blockSize = 16; break;
                case "BC5_SNORM": useDx10Header = true; formatForDx10 = 84; isCompressed = true; blockSize = 16; break;
                case "BC6H_UF16": useDx10Header = true; formatForDx10 = 95; isCompressed = true; blockSize = 16; break;
                case "BC6H_SF16": useDx10Header = true; formatForDx10 = 96; isCompressed = true; blockSize = 16; break;
                case "BC7": useDx10Header = true; formatForDx10 = 98; isCompressed = true; blockSize = 16; break;
                case "R8": useDx10Header = true; formatForDx10 = 61; bitsPerPixel = 8; break;
                case "R8_SNORM": useDx10Header = true; formatForDx10 = 62; bitsPerPixel = 8; break;
                case "RG8": useDx10Header = true; formatForDx10 = 49; bitsPerPixel = 16; break;
                case "RG8_SNORM": useDx10Header = true; formatForDx10 = 50; bitsPerPixel = 16; break;
                case "R16_UNORM": useDx10Header = true; formatForDx10 = 56; bitsPerPixel = 16; break;
                case "R16_SNORM": useDx10Header = true; formatForDx10 = 55; bitsPerPixel = 16; break;
                case "RG16": useDx10Header = true; formatForDx10 = 35; bitsPerPixel = 32; break;
                case "RG16_SNORM": useDx10Header = true; formatForDx10 = 36; bitsPerPixel = 32; break;
                case "RGBA8": useDx10Header = true; formatForDx10 = 28; bitsPerPixel = 32; break;
                case "RGBA8_SNORM": useDx10Header = true; formatForDx10 = 31; bitsPerPixel = 32; break;
                case "RGBA8_UINT": useDx10Header = true; formatForDx10 = 30; bitsPerPixel = 32; break;
                case "RGBA16": useDx10Header = true; formatForDx10 = 11; bitsPerPixel = 64; break;
                case "RGBA16_SNORM": useDx10Header = true; formatForDx10 = 13; bitsPerPixel = 64; break;
                case "RGBA16F": useDx10Header = true; formatForDx10 = 10; bitsPerPixel = 64; break;
                default:
                    throw new NotSupportedException($"Unknown format: {formatString}");
            }

            if (isCompressed) { pitchOrLinearSize = Math.Max(1, ((width + 3) / 4)) * Math.Max(1, ((height + 3) / 4)) * blockSize; pitchOrLinearSizeFlag = 0x80000; }
            else { pitchOrLinearSize = (width * bitsPerPixel + 7) / 8; pitchOrLinearSizeFlag = 0x8; }

            if (useDx10Header) { fourCC = FOURCC_DX10; }

            writer.Write(MakeFourCC('D', 'D', 'S', ' ')); writer.Write(124); uint flags = 0x1 | 0x1000 | 0x4 | 0x2 | pitchOrLinearSizeFlag; if (mipCount > 1) flags |= 0x20000; if (depth > 1) flags |= 0x800000; writer.Write(flags); writer.Write(height); writer.Write(width); writer.Write(pitchOrLinearSize); writer.Write(depth > 1 ? depth : 0); writer.Write(mipCount > 1 ? mipCount : 0); writer.Write(new byte[44]); writer.Write(32); writer.Write(0x4); writer.Write(fourCC); writer.Write(new byte[20]); uint caps = 0x1000; if (mipCount > 1) caps |= 0x8 | 0x400000; if (depth > 1 || texType != 0) caps |= 0x8; writer.Write(caps); uint caps2 = 0; if (texType == 1) caps2 |= 0x200; if (texType == 2) caps2 |= 0x200000; writer.Write(caps2); writer.Write(new byte[12]); if (useDx10Header) { writer.Write(formatForDx10); writer.Write(3); writer.Write(0); writer.Write(1); writer.Write(0); }
            return ms.ToArray();
        }
    }

    private static uint MakeFourCC(char c1, char c2, char c3, char c4)
    {
        return (uint)c1 | ((uint)c2 << 8) | ((uint)c3 << 16) | ((uint)c4 << 24);
    }
}