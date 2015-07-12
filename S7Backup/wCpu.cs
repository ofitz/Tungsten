﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Snap7;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace S7Backup
{
    public enum wLanguage
    {
        STL = 0x01,
        FBD = 0x02,
        LAD = 0x03,
        DB = 0x04,
        SCL = 0x05,
        Graph = 0x06,
        SDB = 0x11
    }
    public enum wBlockType
    {
        OB = 0x38,
        FC = 0x43,
        FB = 0x45,
        DB = 0x41,
        SFC = 0x44,
        SFB = 0x46,
        SDB = 0x42
    }
    public enum wSubBlockType
    {
        OB = 0x08,
        FC = 0x0C,
        FB = 0x0E,
        DB = 0x0A,
        SFC = 0x0D,
        SFB = 0x0F,
        SDB = 0x0B
    }
   
    [Serializable]
    public class wCpu
    {
        public wCpu()
        {
            blocks = new List<wCpuBlock>();
        }
        //TODO: Deconstruct WLD file in to CPU
        public wCpu(wldFile w)
        {
            blocks = new List<wCpuBlock>();
        }

        public void connect(string ipAddress)
        {
            this.connect(ipAddress, 0, 2);
        }

        public void connect(string ipAddress, int rack, int slot)
        {
            MyClient = new S7Client();
            int result = MyClient.ConnectTo(ipAddress, rack, slot);

            if (result == 0)
            {
                Console.WriteLine("Connected to CPU at IP Address " + ipAddress);
                S7Client.S7OrderCode oc = new S7Client.S7OrderCode();
                result = MyClient.GetOrderCode(ref oc);

                if (result == 0)
                {
                    Console.WriteLine("CPU Order Code:\t" + this.orderCode);
                    S7Client.S7BlocksList bl = new S7Client.S7BlocksList();
                    result = MyClient.ListBlocks(ref bl);

                    if (result == 0)
                    {
                        Console.WriteLine("OB Count:\t" + bl.OBCount);
                        Console.WriteLine("FC Count:\t" + bl.FCCount);
                        Console.WriteLine("FB Count:\t" + bl.FBCount);
                        Console.WriteLine("DB Count:\t" + bl.DBCount);
                        Console.WriteLine("SFC Count:\t" + bl.SFCCount);
                        Console.WriteLine("SFB Count:\t" + bl.SFBCount);
                        Console.WriteLine("SDB Count:\t" + bl.SDBCount);
                    }
                    else //Failed to List Blocks
                    {
                        Console.WriteLine("Failed to list blocks. " + result.ToString("X4"));
                    }
                }
                else //Failed to get Order Code
                {
                    Console.WriteLine("Failed to get Order Code. " + result.ToString("X4"));
                }
            }
            else //Failed to connect to CPU
            {
                Console.WriteLine("Failed to connect to CPU. " + result.ToString("X4"));
            }
        }
        
        public void upload()
        {
            Console.WriteLine("Getting CPU information...");

            S7Client.S7OrderCode oc = new S7Client.S7OrderCode();
            int result = MyClient.GetOrderCode(ref oc);

            if (result == 0)
            {
                this.setOrderCode(oc);
            }
            else
            {
                Console.WriteLine("Failed to get Order Code");
                Console.WriteLine("Error: " + result.ToString("X4"));
            }

            S7Client.S7CpuInfo ci = new S7Client.S7CpuInfo();
            result = MyClient.GetCpuInfo(ref ci);

            if (result == 0)
            {
                this.setCpuInfo(ci);
            }
            else
            {
                Console.WriteLine("Failed to get CPU info");
                Console.WriteLine("Error: " + result.ToString("X4"));
            }

            Console.WriteLine("Uploading program blocks... ");
            foreach (wBlockType blockType in Enum.GetValues(typeof(wBlockType)))
            {
                ushort[] blockList = new ushort[MAX_BLOCK];
                int blockCount = blockList.Length;
                MyClient.ListBlocksOfType((int)blockType, blockList, ref blockCount);
                for (int i = 0; i < blockCount; i++)
                {
                    S7Client.S7BlockInfo blockInfo = new S7Client.S7BlockInfo();
                    MyClient.GetAgBlockInfo((int)blockType, blockList[i], ref blockInfo);

                    byte[] buffer = new byte[4096];
                    int bufferSize = buffer.Length;

                    if (blockType != wBlockType.SFC && blockType != wBlockType.SFB)
                        MyClient.FullUpload((int)blockType, blockList[i], buffer, ref bufferSize);
                    else
                        bufferSize = 0;

                    byte[] data = new byte[bufferSize];
                    Array.Copy(buffer, data, data.Length);
                    this.addCpuBlock(blockInfo, data);
                    Console.WriteLine(this.blocks.Last().ToString() + " loaded. Size: " + this.blocks.Last().loadSize + " bytes.");
                }
            }
            Console.WriteLine("Done!");
        }

        public void download(string ipAddress)
        {
            download(ipAddress, 0, 2);
        }

        public void download(string ipAddress, int rack, int slot)
        {
            int result;
            result = MyClient.ConnectTo(ipAddress, rack, slot);
            foreach (wCpuBlock b in this.blocks)
            {
                if (b.blockType != wBlockType.SFC && b.blockType != wBlockType.SFB)
                {
                    result = MyClient.Download(b.blockNumber, b.data, b.data.Length);
                    if (result == 0)
                    {
                        Console.WriteLine("Downloaded " + b.ToString());
                    }   
                    else
                    {
                        Console.WriteLine("Error downloading " + b.ToString());
                        Console.WriteLine("Error: " + result.ToString("X8"));
                    }
                }
            }

            Console.Write("Download Complete");
        }

        public void erase()
        {
            int result;
            Console.WriteLine("Erasing CPU... ");
            foreach (wBlockType blockType in Enum.GetValues(typeof(wBlockType)))
            {
                ushort[] blockList = new ushort[MAX_BLOCK];
                int blockCount = blockList.Length;
                MyClient.ListBlocksOfType((int)blockType, blockList, ref blockCount);
                for (int i = 0; i < blockCount; i++)
                {
                    if (blockType != wBlockType.SFC && blockType != wBlockType.SFB)
                    {
                        result = MyClient.Delete((int)blockType, blockList[i]);

                        if (result == 0)
                        {
                            Console.WriteLine("Deleted " + blockType + blockList[i]);
                        }
                        else
                        {
                            Console.WriteLine("Error deleting " + blockType + blockList[i]);
                            Console.WriteLine("Error: " + result.ToString("X4"));
                        }
                    }

                    
                }
            }
            Console.WriteLine("Done!");
        }

        public static string cleanString(string s)
        {
            s = System.Text.RegularExpressions.Regex.Replace(s, @"[^\u0020-\u007F]", string.Empty);
            return s;
        }

        public List<wCpuBlock> blocks;
        private S7Client MyClient;
        
        public string moduleTypeName,
                      serialNumber,
                      ASName,
                      copyright,
                      moduleName,
                      orderCode;
        private const int MAX_BLOCK = 0x2000;

        public string version
        {
            get { return V1.ToString() + "." + V2.ToString() + "." + V3.ToString(); }
        }

        private byte V1, V2, V3;

        public void addCpuBlock(S7Client.S7BlockInfo block, byte[] data)
        {
            this.blocks.Add(new wCpuBlock(block, data));
        }

        public void setOrderCode(S7Client.S7OrderCode oc)
        {
            this.orderCode = cleanString(oc.Code);
            this.V1 = oc.V1;
            this.V2 = oc.V2;
            this.V3 = oc.V3;
        }

        public void setCpuInfo(S7Client.S7CpuInfo ci)
        {
            this.moduleTypeName = cleanString(ci.ModuleTypeName);
            this.serialNumber = cleanString(ci.SerialNumber);
            this.ASName = cleanString(ci.ASName);
            this.copyright = cleanString(ci.Copyright);
            this.moduleName = cleanString(ci.ModuleName);
        }
        
    }
    [Serializable]
    public class wCpuBlock : IComparable<wCpuBlock>
    {
        public wCpuBlock() { }
        public wCpuBlock(S7Client.S7BlockInfo info, byte[] data)
        {
            if (info.BlkType == (int)wSubBlockType.OB)
                this.blockType = wBlockType.OB;
            else if (info.BlkType == (int)wSubBlockType.FC)
                this.blockType = wBlockType.FC;
            else if (info.BlkType == (int)wSubBlockType.FB)
                this.blockType = wBlockType.FB;
            else if (info.BlkType == (int)wSubBlockType.DB)
                this.blockType = wBlockType.DB;
            else if (info.BlkType == (int)wSubBlockType.SFC)
                this.blockType = wBlockType.SFC;
            else if (info.BlkType == (int)wSubBlockType.SFB)
                this.blockType = wBlockType.SFB;
            else if (info.BlkType == (int)wSubBlockType.SDB)
                this.blockType = wBlockType.SDB;
            this.language = (wLanguage) info.BlkLang;
            this.name = wCpu.cleanString(info.Header);
            this.family = wCpu.cleanString(info.Family);
            this.author = wCpu.cleanString(info.Author);
            this.codeDate = wCpu.cleanString(info.CodeDate);
            this.interfaceDate = wCpu.cleanString(info.IntfDate);
            this.loadSize = info.LoadSize;
            this.MC7Size = info.MC7Size;
            this.blockNumber = info.BlkNumber;
            this.blockFlags = info.BlkFlags;
            this.localData = info.LocalData;
            this.SBBLength = info.SBBLength;
            this.checksum = info.CheckSum;
            this.data = data;
        }

        public int CompareTo(wCpuBlock b)
        {
            if (this.blockType == b.blockType)
            {
                return this.blockNumber.CompareTo(b.blockNumber);
            }
            else
            {
                return this.blockType.CompareTo(b.blockType);
            }
        }

        public override string ToString()
        {
            return blockType.ToString() + blockNumber.ToString();
        }

        public wLanguage language;
        public wBlockType blockType;
        public string name,
                       family,
                       author,
                       codeDate,
                       interfaceDate;
        public int  loadSize, 
                    MC7Size,
                    blockNumber,
                    blockFlags,
                    localData,
                    SBBLength,
                    checksum,
                    version;
        public byte[] data;
    }
    public class wldFile
    {
        public wldFile(wCpu cpu)
        {
            List<wCpuBlock> blocks = cpu.blocks;
            blocks.Sort();
            data = new byte[0];

            foreach (wCpuBlock block in blocks)
            {
                if ((block.data != null) && (block.blockType != wBlockType.SFC) && (block.blockType != wBlockType.SFB))
                    data = data.Concat(block.data).ToArray();
            }
        }

         public byte[] data {get; private set;}
    }
}