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
using System.Text;
using Gibbed.IO;
using System.Diagnostics;

namespace Gibbed.TombRaider.FileFormats
{
    public class DRMFile
    {
        public uint Version;
        public bool LittleEndian;
        public List<string> Unknown04s = new List<string>();
        public List<string> Unknown08s = new List<string>();

        public List<DRM.Section> Sections
            = new List<DRM.Section>();

        public Stream Serialize()
        {
            MemoryStream data = new MemoryStream();
            uint sectionCount = (uint)this.Sections.Count;
            Stream[] resolvers = new MemoryStream[sectionCount]; // for serialized resolvers

            uint unknown04_size = 0;
            uint unknown08_size = 0;

            // Write DRM Header
            data.WriteValueU32(this.Version, this.LittleEndian);
            data.WriteValueU32(0); // skip for now
            data.WriteValueU32(0); // skip for now
            data.WriteValueU32(0); // unknown0C
            data.WriteValueU32(0); // unknown10
            data.WriteValueU32(sectionCount, this.LittleEndian);

            // Write DRM Section Headers
            for (int i = 0; i < sectionCount; i++)
            {
                DRM.Section section = this.Sections[i];

                // Serialize resolvers to get length, data will be used later
                uint resolverLen;
                if (section.Resolver != null)
                {
                    resolvers[i] = section.Resolver.Serialize(this.LittleEndian);
                    resolverLen = (uint)resolvers[i].Length;
                }
                else
                {
                    resolvers[i] = null;
                    resolverLen = 0;
                }

                data.WriteValueU32((uint)section.Data.Length, this.LittleEndian);
                data.WriteValueU8((byte)section.Type);
                data.WriteValueU8(section.Unknown05);
                data.WriteValueU16(section.Unknown06, this.LittleEndian);
                data.WriteValueU32((uint)section.Flags | (resolverLen << 8), this.LittleEndian);
                data.WriteValueU32(section.Id, this.LittleEndian);
                data.WriteValueU32(section.Unknown10, this.LittleEndian);
            }

            // Write Unknown08s
            for (int i = 0; i < Unknown08s.Count; i++)
            {
                unknown08_size += ((uint)Unknown08s[i].Length + 1);
                data.WriteStringZ(Unknown08s[i]);
            }

            // Write Unknown04s
            for (int i = 0; i < Unknown04s.Count; i++)
            {
                unknown04_size += ((uint)Unknown04s[i].Length + 1);
                data.WriteStringZ(Unknown04s[i]);
            }

            // Write DRM Section Data
            for (int i = 0; i < sectionCount; i++)
            {
                if (resolvers[i] != null)
                {
                    data.WriteFromStream(resolvers[i], resolvers[i].Length);
                }
                data.WriteFromStream(this.Sections[i].Data, this.Sections[i].Data.Length);
                this.Sections[i].Data.Position = 0;
            }

            // Go back and write unknowns length
            data.Seek(4, SeekOrigin.Begin);
            data.WriteValueU32(unknown04_size);
            data.WriteValueU32(unknown08_size);

            data.Position = 0;
            return data;
        }

        public void Deserialize(Stream input)
        {
            var magic = input.ReadValueU32(false);
            input.Seek(-4, SeekOrigin.Current);

            if (magic == CDRMFile.Magic)
            {
                input = CDRMFile.Decompress(input);
            }

            var version = input.ReadValueU32();
            if (version != 14 && version.Swap() != 14 &&
                version != 19 && version.Swap() != 19 &&
                version != 21 && version.Swap() != 21)
            {
                throw new FormatException();
            }

            this.LittleEndian =
                version == 14 ||
                version == 19 ||
                version == 21;
            this.Version = this.LittleEndian == true ? version : version.Swap();

            if (this.Version == 14)
            {
                throw new NotSupportedException("TRL/TRA not supported");
            }

            if (this.Version == 21)
            {
                throw new NotSupportedException("DX3 not supported");
            }

            if (input.Length < 20)
            {
                throw new FormatException("not enough data for header");
            }

            var unknown04_Size = input.ReadValueU32(this.LittleEndian);
            var unknown08_Size = input.ReadValueU32(this.LittleEndian);
            var unknown0C = input.ReadValueU32(this.LittleEndian); // extra data after first block?
            var unknown10 = input.ReadValueU32(this.LittleEndian);
            var sectionCount = input.ReadValueU32(this.LittleEndian);

            Debug.Assert((unknown0C + unknown10) == 0, "unk hdr val not 0");

            if (unknown0C != 0)
            {
                throw new FormatException(); //why?
            }

            var sectionHeaders = new DRM.SectionHeader[sectionCount];
            for (uint i = 0; i < sectionCount; i++)
            {
                sectionHeaders[i] = new DRM.SectionHeader();
                sectionHeaders[i].Deserialize(input, this.LittleEndian);
            }

            this.Unknown08s.Clear();
            using (var unknown08_Data = input.ReadToMemoryStream(unknown08_Size))
            {
                while (unknown08_Data.Position < unknown08_Data.Length)
                {
                    this.Unknown08s.Add(unknown08_Data.ReadStringZ(Encoding.ASCII));
                }
            }

            this.Unknown04s.Clear();
            using (var unknown04_Data = input.ReadToMemoryStream(unknown04_Size))
            {
                while (unknown04_Data.Position < unknown04_Data.Length)
                {
                    this.Unknown04s.Add(unknown04_Data.ReadStringZ(Encoding.ASCII));
                }
            }

            var sections = new DRM.Section[sectionCount];
            for (int i = 0; i < sectionCount; i++)
            {
                var sectionHeader = sectionHeaders[i];

                var section = new DRM.Section();
                section.Id = sectionHeader.Id;
                section.Type = sectionHeader.Type;
                section.Flags = (byte)(sectionHeader.Flags & 0xFF);
                section.Unknown05 = sectionHeader.Unknown05;
                section.Unknown06 = sectionHeader.Unknown06;
                section.Unknown10 = sectionHeader.Unknown10;

                if ((sectionHeader.Unknown05 & 1) != 0)
                {
                    throw new NotImplementedException();
                }

                if (sectionHeader.HeaderSize > 0)
                {
                    using (var buffer = input.ReadToMemoryStream(sectionHeader.HeaderSize))
                    {
                        var resolver = new DRM.Resolver();
                        resolver.Deserialize(buffer, this.LittleEndian);
                        section.Resolver = resolver;
                    }
                }

                if (sectionHeader.DataSize > 0)
                {
                    section.Data = input.ReadToMemoryStream(sectionHeader.DataSize);
                }
                else
                {
                    section.Data = null;
                }

                sections[i] = section;
            }

            this.Sections.Clear();
            this.Sections.AddRange(sections);
        }
    }
}
