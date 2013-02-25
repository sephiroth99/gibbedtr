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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gibbed.IO;
using System.Text;

namespace Gibbed.CrystalDynamics.FileFormats
{
    public class BigFileV3
    {
        public bool LittleEndian = true;
        public uint FileAlignment = 0x7FF00000;
        public List<Big.EntryV2> Entries
            = new List<Big.EntryV2>();

        public uint Unknown04;
        public uint Unknown08;
        public uint Unknown10;
        public string Unknown14;

        public static int EstimateHeaderSize(int count)
        {
            return
                (52 + // header
                (16 * count)) // entries
                .Align(2048); // aligned to 2048 bytes
        }

        public void Serialize(Stream output)
        {
            throw new NotImplementedException();
        }

        public void Deserialize(Stream input)
        {
            var magic = input.ReadValueU32(this.LittleEndian);

            if (magic != 0x54414653)
                throw new NotSupportedException("Bad magic number");

            Unknown04 = input.ReadValueU32(this.LittleEndian);
            Unknown08 = input.ReadValueU32(this.LittleEndian);

            var count = input.ReadValueU32(this.LittleEndian);

            Unknown10 = input.ReadValueU32(this.LittleEndian);

            Unknown14 = input.ReadString(32, true, Encoding.ASCII);

            this.Entries.Clear();
            for (uint i = 0; i < count; i++)
            {
                var entry = new Big.EntryV2();
                entry.NameHash = input.ReadValueU32(this.LittleEndian);
                entry.Locale = input.ReadValueU32(this.LittleEndian);
                entry.Size = input.ReadValueU32(this.LittleEndian);
                entry.Offset = (uint)(input.ReadValueU16(this.LittleEndian) << 8) + input.ReadValueU8();
                entry.File = input.ReadValueU8();
                this.Entries.Add(entry);
            }
        }
    }
}
