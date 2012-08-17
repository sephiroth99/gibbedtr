/* Copyright (c) 2011 Rick (rick 'at' gibbed 'dot' us)
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 * 
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 * 
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

//-----------------------------------------------------------------------------
// Additional modifications by sephiroth99
//-----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using Gibbed.IO;

namespace Gibbed.TombRaider.FileFormats
{
    // PCD9 = PC D3D9 Texture?
    public class PCD9File
    {
        public PCD9.Format Format;
        public ushort Width;
        public ushort Height;
        public byte BPP;
        public List<PCD9.Mipmap> Mipmaps = new List<PCD9.Mipmap>();

        public uint Unknown0C;
        public ushort Unknown16;

        public Stream Serialize()
        {
            MemoryStream data = new MemoryStream();
            uint dataSize = 0;

            // Header stuff
            data.WriteValueU32(0x39444350);
            data.WriteValueEnum<PCD9.Format>(this.Format);
            data.Seek(4, SeekOrigin.Current); //skip dataSize for now
            data.WriteValueU32(this.Unknown0C);
            data.WriteValueU16(this.Width);
            data.WriteValueU16(this.Height);
            data.WriteValueU8(this.BPP);
            data.WriteValueU8((byte)(this.Mipmaps.Count - 1));
            data.WriteValueU16(this.Unknown16);

            // Data stuff
            //TODO sort to make sure the biggest is first? will the order ever change?
            for (int i = 0; i < this.Mipmaps.Count; i++)
            {
                // Write image data
                data.WriteBytes(this.Mipmaps[i].Data);

                // Add length to total
                dataSize += (uint)this.Mipmaps[i].Data.Length;
            }

            // Write dataSize
            data.Seek(8, SeekOrigin.Begin);
            data.WriteValueU32(dataSize);

            data.Position = 0;
            return data;
        }

        public void Deserialize(Stream input)
        {
            if (input.ReadValueU32() != 0x39444350)
            {
                throw new FormatException();
            }

            this.Format = input.ReadValueEnum<PCD9.Format>();
            var dataSize = input.ReadValueU32();
            this.Unknown0C = input.ReadValueU32();
            this.Width = input.ReadValueU16();
            this.Height = input.ReadValueU16();
            this.BPP = input.ReadValueU8();
            var mipMapCount = 1 + input.ReadValueU8();
            this.Unknown16 = input.ReadValueU16();

            this.Mipmaps.Clear();
            using (var data = input.ReadToMemoryStream(dataSize))
            {
                var mipWidth = this.Width;
                var mipHeight = this.Height;

                for (int i = 0; i < mipMapCount; i++)
                {
                    if (mipWidth == 0)
                    {
                        mipWidth = 1;
                    }

                    if (mipHeight == 0)
                    {
                        mipHeight = 1;
                    }

                    int size;
                    switch (this.Format)
                    {
                        case PCD9.Format.A8R8G8B8:
                        {
                            size = mipWidth * mipHeight * 4;
                            break;
                        }

                        case PCD9.Format.DXT1:
                        case PCD9.Format.DXT3:
                        case PCD9.Format.DXT5:
                        {
                            int blockCount = ((mipWidth + 3) / 4) * ((mipHeight + 3) / 4);
                            int blockSize = this.Format == PCD9.Format.DXT1 ? 8 : 16;
                            size = blockCount * blockSize;
                            break;
                        }

                        default:
                        {
                            throw new NotSupportedException();
                        }
                    }

                    var buffer = new byte[size];
                    if (data.Read(buffer, 0, buffer.Length) != buffer.Length)
                    {
                        throw new EndOfStreamException();
                    }

                    this.Mipmaps.Add(new PCD9.Mipmap()
                        {
                            Width = mipWidth,
                            Height = mipHeight,
                            Data = buffer,
                        });

                    mipWidth >>= 1;
                    mipHeight >>= 1;
                }

                if (data.Position != data.Length)
                {
                    throw new InvalidOperationException();
                }
            }
        }
    }
}
