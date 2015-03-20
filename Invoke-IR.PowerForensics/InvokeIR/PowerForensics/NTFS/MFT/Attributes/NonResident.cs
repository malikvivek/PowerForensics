﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using InvokeIR.Win32;

namespace InvokeIR.PowerForensics.NTFS.MFT.Attributes
{

    public class NonResident : Attr
    {

        struct ATTR_HEADER_NON_RESIDENT
        {
            internal AttrHeader.ATTR_HEADER_COMMON commonHeader;	// Common data structure
            internal ulong StartVCN;		                        // Starting VCN
            internal ulong LastVCN;		                            // Last VCN
            internal ushort DataRunOffset;	                        // Offset to the Data Runs
            internal ushort CompUnitSize;	                        // Compression unit size
            internal uint Padding;		                            // Padding
            internal ulong AllocSize;		                        // Allocated size of the attribute
            internal ulong RealSize;		                        // Real size of the attribute
            internal ulong IniSize;		                            // Initialized data size of the stream 

            internal ATTR_HEADER_NON_RESIDENT(byte[] bytes)
            {
                commonHeader = new AttrHeader.ATTR_HEADER_COMMON(bytes.Take(16).ToArray());
                StartVCN = BitConverter.ToUInt64(bytes, 16);
                LastVCN = BitConverter.ToUInt64(bytes, 24);
                DataRunOffset = BitConverter.ToUInt16(bytes, 32);
                CompUnitSize = BitConverter.ToUInt16(bytes, 34);
                Padding = BitConverter.ToUInt32(bytes, 36);
                AllocSize = BitConverter.ToUInt64(bytes, 40);
                RealSize = BitConverter.ToUInt64(bytes, 48);
                IniSize = BitConverter.ToUInt64(bytes, 56);
            }
        }

        public ulong AllocatedSize;
        public ulong RealSize;
        public ulong InitializedSize;
        public ulong[] StartCluster;
        public ulong[] EndCluster;

        internal NonResident(uint AttrType, string name, bool nonResident, ulong allocatedSize, ulong realSize, ulong iniSize, ulong[] startCluster, ulong[] endCluster)
        {
            Name = Enum.GetName(typeof(ATTR_TYPE), AttrType);
            NameString = name;
            NonResident = nonResident;
            AllocatedSize = allocatedSize;
            RealSize = realSize;
            InitializedSize = iniSize;
            StartCluster = startCluster;
            EndCluster = endCluster;
        }

        internal static List<byte> GetContent(FileStream streamToRead, NonResident nonResAttr)
        {

            List<byte> DataBytes = new List<byte>();

            for (int i = 0; i < nonResAttr.StartCluster.Length; i++)
            {
                long offset = (long)nonResAttr.StartCluster[i] * 4096;
                long length = ((long)nonResAttr.EndCluster[i] - (long)nonResAttr.StartCluster[i]) * 4096;
                DataBytes.AddRange(NativeMethods.readDrive(streamToRead, offset, length));
            }

            DataBytes.Take((int)nonResAttr.RealSize);
            return DataBytes;

        }


        internal static List<byte> GetContent(string volume, NonResident nonResAttr)
        {

            List<byte> DataBytes = new List<byte>();

            IntPtr hVolume = NativeMethods.getHandle(volume);
            FileStream streamToRead = NativeMethods.getFileStream(hVolume);

            for (int i = 0; i < nonResAttr.StartCluster.Length; i++)
            {
                long offset = (long)nonResAttr.StartCluster[i] * 4096;
                long length = ((long)nonResAttr.EndCluster[i] - (long)nonResAttr.StartCluster[i]) * 4096;
                DataBytes.AddRange(NativeMethods.readDrive(streamToRead, offset, length));
            }

            DataBytes.Take((int)nonResAttr.RealSize);
            return DataBytes;

        }

        internal static NonResident Get(byte[] AttrBytes, string AttrName)
        {

            ATTR_HEADER_NON_RESIDENT nonResAttrHeader = new ATTR_HEADER_NON_RESIDENT(AttrBytes);

            int offset = 0;
            int DataRunStart = nonResAttrHeader.DataRunOffset;
            int DataRunSize = (int)nonResAttrHeader.commonHeader.TotalSize - nonResAttrHeader.DataRunOffset;
            byte[] DataRunBytes = AttrBytes.Skip(DataRunStart).Take(DataRunSize).ToArray();

            int DataRunLengthByteCount = DataRunBytes[offset] & 0x0F;
            int DataRunOffsetByteCount = ((DataRunBytes[offset] & 0xF0) >> 4);

            ulong startCluster = 0;
            List<ulong> startClusterList = new List<ulong>();
            List<ulong> endClusterList = new List<ulong>();


            //Console.WriteLine("Size: {0}", DataRunSize);
            do
            {
                //Console.WriteLine("Offset: {0}", offset);
                //Console.WriteLine("Count1: {0}", DataRunLengthByteCount);
                //Console.WriteLine("Count2: {0}", DataRunOffsetByteCount);

                byte[] DataRunLengthBytes = DataRunBytes.Skip(offset + 1).Take(DataRunLengthByteCount).ToArray();
                Array.Resize(ref DataRunLengthBytes, 8);
                ulong DataRunLength = BitConverter.ToUInt64(DataRunLengthBytes, 0);

                byte[] DataRunOffsetBytes = DataRunBytes.Skip((offset + 1 + DataRunLengthByteCount)).Take(DataRunOffsetByteCount).ToArray();
                Array.Resize(ref DataRunOffsetBytes, 8);
                ulong DataRunOffset = BitConverter.ToUInt64(DataRunOffsetBytes, 0);

                startCluster += DataRunOffset;
                startClusterList.Add(startCluster);
                endClusterList.Add(startCluster + DataRunLength);

                offset = offset + 1 + DataRunLengthByteCount + DataRunOffsetByteCount;

                DataRunLengthByteCount = DataRunBytes[offset] & 0x0F;
                DataRunOffsetByteCount = ((DataRunBytes[offset] & 0xF0) >> 4);

            }
            while (((offset + DataRunLengthByteCount + DataRunOffsetByteCount + 1) < DataRunSize) && (DataRunLengthByteCount != 0));




            return new NonResident(
                nonResAttrHeader.commonHeader.ATTRType,
                AttrName,
                nonResAttrHeader.commonHeader.NonResident,
                nonResAttrHeader.AllocSize,
                nonResAttrHeader.RealSize,
                nonResAttrHeader.IniSize,
                startClusterList.ToArray(),
                endClusterList.ToArray());

        }

    }

}
