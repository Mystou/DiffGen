﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace xDiffPatcher
{
    public enum ExeType
    {
        None = 0,
        Rag,
        Sak,
        RagRE,
        RagRE9
    }

    public enum DiffType
    {
        None = 0,
        Diff,
        xDiff
    }

    public enum PatchType
    {
        None = 0,
        UI,
        Fix,
        Data,
        Auto,
        Color
    }

    public enum ChangeType
    {
        None = 0,
        Byte,
        Word,
        Dword,
        String
    }

    public class DiffFile
    {
        private int m_exeBuildDate = 0;
        private string m_exeName = "";
        private int m_exeCRC = 0;
        private int m_exeType = 0;

        private string m_name = "";
        private string m_author = "";
        private string m_version = "";
        private string m_releaseDate = "";

        private FileInfo m_fileInfo;

        private DiffType m_type = 0;

        private Dictionary<int, DiffPatchBase> m_xpatches; //for xDiff
        private Dictionary<string, DiffPatch> m_patches; //for diff

        public int ExeBuildDate
        {
            get { return m_exeBuildDate; }
            set { m_exeBuildDate = value; }
        }
        public string ExeName
        {
            get { return m_exeName; }
            set { m_exeName = value; }
        }
        public int ExeCRC
        {
            get { return m_exeCRC; }
            set { m_exeCRC = value; }
        }
        public int ExeType
        {
            get { return m_exeType; }
            set { m_exeType = value; }
        }

        public FileInfo FileInfo
        {
            get { return m_fileInfo; }
            set { m_fileInfo = value; }
        }
        public DiffType Type
        {
            get { return m_type; }
            set { m_type = value; }
        }

        public string Name
        {
            get { return m_name; }
            set { m_name = value; }
        }
        public string Author
        {
            get { return m_author; }
            set { m_author = value; }
        }
        public string Version
        {
            get { return m_version; }
            set { m_version = value; }
        }
        public string ReleaseDate
        {
            get { return m_releaseDate; }
            set { m_releaseDate = value; }
        }
        public Dictionary<string, DiffPatch> Patches
        {
            get { return m_patches; }
            set { m_patches = value; }
        }
        public Dictionary<int, DiffPatchBase> xPatches
        {
            get { return m_xpatches; }
            set { m_xpatches = value; }
        }

        public DiffFile()
        {
            m_xpatches = new Dictionary<int, DiffPatchBase>();
            m_patches = new Dictionary<string, DiffPatch>();
        }

        public DiffFile(string fileName, DiffType type)
        {
            m_xpatches = new Dictionary<int, DiffPatchBase>();
            m_patches = new Dictionary<string, DiffPatch>();

            this.Load(fileName, type);
        }

        public int PatchCount()
        {
            int count = 0;

            foreach (KeyValuePair<int, DiffPatchBase> p in this.xPatches)
            {
                if (p.Value is DiffPatch)
                    count++;
                else if (p.Value is DiffPatchGroup)
                    count += ((DiffPatchGroup)p.Value).Patches.Count;
            }

            return count;
        }

        public int Load(string fileName, DiffType type)
        {
            if (!File.Exists(fileName))
                return 1;

            m_fileInfo = new FileInfo(fileName);

            if (m_patches != null)
                m_patches.Clear();
            if (m_xpatches != null)
                m_xpatches.Clear();

            m_type = type;

            if (type == DiffType.xDiff)
            {
                XmlDocument XDoc = null;
                try
                {
                    XDoc = new XmlDocument();
                    XDoc.Load(fileName);

                    this.ExeBuildDate = int.Parse(XDoc.SelectSingleNode("//diff/exe/builddate").InnerText);
                    this.ExeName = XDoc.SelectSingleNode("//diff/exe/filename").InnerText;
                    this.ExeCRC = int.Parse(XDoc.SelectSingleNode("//diff/exe/crc").InnerText);
                    string xtype = XDoc.SelectSingleNode("//diff/exe/type").InnerText;
                    this.ExeType = 0;

                    this.Name = XDoc.SelectSingleNode("//diff/info/name").InnerText;
                    this.Author = XDoc.SelectSingleNode("//diff/info/author").InnerText;
                    this.Version = XDoc.SelectSingleNode("//diff/info/version").InnerText;
                    this.ReleaseDate = XDoc.SelectSingleNode("//diff/info/releasedate").InnerText;

                    XmlNode patches = XDoc.SelectSingleNode("//diff/patches");
                    foreach (XmlNode patch in patches.ChildNodes)
                    {
                        if (patch.Name == "patchgroup")
                        {
                            //XmlNode tmpNode = null;
                            DiffPatchGroup g = new DiffPatchGroup();

                            g.ID = int.Parse(patch.Attributes["id"].InnerText);
                            g.Name = patch.Attributes["name"].InnerText;

                            foreach (XmlNode node in patch.ChildNodes)
                            {
                                if (node.Name == "patch")
                                {
                                    DiffPatch p = new DiffPatch();
                                    p.LoadFromXML(node);
                                    this.xPatches.Add(p.ID, p);
                                    g.Patches.Add(p);
                                }
                            }

                            this.xPatches.Add(g.ID, g);
                        }
                        else if (patch.Name == "patch")
                        {
                            DiffPatch p = new DiffPatch();
                            p.LoadFromXML(patch);

                            this.xPatches.Add(p.ID, p);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to parse xDiff file: \n"+ex.ToString());
                    return 2;
                }
            } else if (type == DiffType.Diff)
            {
                bool hex = false;

                using (StreamReader r = new StreamReader(fileName))
                {
                    string line;
                    while (!r.EndOfStream && (line = r.ReadLine()) != null )
                    {
                        line = line.Trim();
                        if (line.Length < 5) continue;

                        if (line.StartsWith("OCRC:")) 
                        {
                            this.ExeCRC = int.Parse(line.Substring(5));
                        }
                        else if (line.StartsWith("BLURB:"))
                        {
                            this.Name = line.Substring(6);
                        }
                        else if (line.StartsWith("READHEX"))
                        {
                            hex = true;
                        }
                        else if (line.StartsWith("byte_"))
                        {
                            string pType, pName;
                            string pGroup;
                            DiffChange change = new DiffChange();
                            DiffPatch patch = new DiffPatch();
                            string[] split = line.Split(':');

                            Regex regex = new Regex("(.+)_\\[(.+)\\]_(.+)");
                            Match match = regex.Match(split[0]);

                            pName = "";
                            pType = "";
                            if (match.Success)
                            {
                                change.Type = ChangeType.Byte;
                                pType = match.Groups[1].Captures[0].Value;
                                pName = split[0].Substring(5); //match.Captures[2].Value.Replace('_', ' ');
                            } else 
                            {
                                regex = new Regex("(.+)_\\[(.+)\\]\\((.+)\\)_(.+)");
                                match = regex.Match(split[0]);

                                if (match.Success)
                                {
                                    change.Type = ChangeType.Byte;
                                    pType = match.Groups[1].Captures[0].Value;
                                    pGroup = match.Groups[3].Captures[0].Value;
                                    pName = split[0].Substring(5); //match.Groups[3].Captures[0].Value.Replace('_', ' ');
                                }
                                else
                                {
                                    continue;
                                }
                            }

                            change.Offset = uint.Parse(split[1], System.Globalization.NumberStyles.HexNumber);
                            change.Old = (byte) ( (!hex) ? byte.Parse(split[2]) : byte.Parse(split[2], System.Globalization.NumberStyles.HexNumber) );
                            change.New_ = (byte) ( (!hex) ? byte.Parse(split[3]) : byte.Parse(split[3], System.Globalization.NumberStyles.HexNumber) );

                            if (m_patches.ContainsKey(pName))
                                m_patches[pName].Changes.Add(change);
                            else
                            {
                                patch.Changes.Add(change);
                                patch.Name = pName;
                                patch.Type = pType;
                                m_patches.Add(pName, patch);
                            }
                        }
                    }
                }
            }
            else
            {
                return 2;
            }



            return 0;
        }

        public int Patch(string inputFile, string fileName)
        {
            if (!File.Exists(inputFile))
                return 1;

            int start = Environment.TickCount;

            if (this.Type == DiffType.xDiff)
            {
                BinaryReader r = new BinaryReader(new StreamReader(inputFile).BaseStream);
                BinaryWriter w = new BinaryWriter(new StreamWriter(fileName, false).BaseStream);

                int changed = 0;

                byte[] buf = new byte[r.BaseStream.Length];
                r.Read(buf, 0, buf.Length);
                //w.Write(buf);

                foreach (DiffPatchBase p in xPatches.Values)
                {
                    if (!(p is DiffPatch))
                        continue;

                    if (!((DiffPatch)p).Apply)
                        continue;

                    foreach (DiffChange c in ((DiffPatch)p).Changes)
                    {
                        switch (c.Type)
                        {
                            case ChangeType.Byte:
                                {
                                    byte old;

                                    r.BaseStream.Seek(c.Offset, SeekOrigin.Begin);
                                    old = r.ReadByte();
                                    if (old != (byte)c.Old)
                                    {
                                        //hm....
                                        MessageBox.Show("Data mismatch at " + c.Offset + "(" + old + " != " + (byte)c.Old + ")!");
                                    }

                                    buf[c.Offset] = (byte)c.New_;
                                    changed++;
                                }
                                break;

                            case ChangeType.Word:
                                {
                                    UInt16 old;

                                    r.BaseStream.Seek(c.Offset, SeekOrigin.Begin);
                                    old = r.ReadUInt16();
                                    if (old != (UInt16)c.Old)
                                    {
                                        MessageBox.Show("Data mismatch at " + c.Offset + "(" + old + " != " + (ushort)c.Old + ")!");
                                    }
                                }

                                buf[c.Offset] =   (byte)((ushort)c.New_ & 0xFF00);
                                buf[c.Offset+1] = (byte)((ushort)c.New_ & 0x00FF);
                                break;

                            default:
                                break;
                        }
                    }
                }

                w.Write(buf);

                int stop = Environment.TickCount;

                MessageBox.Show("Finished patching " + changed + "bytes in " + (stop - start) + "ms!");

                r.Close();
                w.Close();
            }
            else if (this.Type == DiffType.Diff)
            {
                BinaryReader r = new BinaryReader(new StreamReader(inputFile).BaseStream);
                BinaryWriter w = new BinaryWriter(new StreamWriter(fileName, false).BaseStream);

                int changed = 0;

                byte[] buf = new byte[r.BaseStream.Length];
                r.Read(buf, 0, buf.Length);
                //w.Write(buf);

                foreach (DiffPatch p in Patches.Values)
                {
                    if (!p.Apply)
                        continue;

                    foreach (DiffChange c in p.Changes)
                    {
                        switch (c.Type)
                        {
                            case ChangeType.Byte:
                                byte old;

                                r.BaseStream.Seek(c.Offset, SeekOrigin.Begin);
                                //w.Seek((int)c.Offset, SeekOrigin.Begin);
                                old = r.ReadByte();
                                if (old != (byte)c.Old)
                                {
                                    // nich so gut.
                                    if (true) { }
                                }

                                //w.Write((byte)c.New_);
                                buf[c.Offset] = (byte) c.New_;
                                changed++;
                                
                                break;

                            default:
                                break;
                        }
                    }
                }

                w.Write(buf);

                int stop = Environment.TickCount;

                MessageBox.Show("Finished patching " + changed + "bytes in " + (stop - start) + "ms!");

                r.Close();
                w.Close();
            }


            return 0;
        }
    }

    public class DiffChange
    {
        ChangeType m_type;
        uint m_offset;
        object m_old;
        object m_new;

        public ChangeType Type
        {
            get { return m_type; }
            set { m_type = value; }
        }
        public uint Offset
        {
            get { return m_offset; }
            set { m_offset = value; }
        }
        public object Old
        {
            get { return m_old; }
            set { m_old = value; }
        }
        public object New_
        {
            get { return m_new; }
            set { m_new = value; }
        }
    }

    public class DiffPatchBase
    {
        string m_name;
        int m_id = 0;

        public string Name
        {
            get { return m_name; }
            set { m_name = value; }
        }

        public int ID
        {
            get { return m_id; }
            set { m_id = value; }
        }
    }

    public class DiffPatchGroup : DiffPatchBase
    {
        

        List<DiffPatch> m_patches = new List<DiffPatch>();

        public List<DiffPatch> Patches
        {
            get { return m_patches; }
            set { m_patches = value; }
        }

    }

    public class DiffPatch : DiffPatchBase
    {
        string m_type = "";
        bool m_recommended = false;
        string m_desc = "";
        int m_groupID = 0;
        bool m_apply = false; // for diffpatcher

        List<DiffChange> m_changes = new List<DiffChange>();

        public int GroupID
        {
            get { return m_groupID; }
            set { m_groupID = value; }
        }
        public string Type
        {
            get { return m_type; }
            set { m_type = value; }
        }
        public bool Recommended
        {
            get { return m_recommended; }
            set { m_recommended = value; }
        }
        public string Desc
        {
            get { return m_desc; }
            set { m_desc = value; }
        }
        public bool Apply
        {
            get { return m_apply; }
            set { m_apply = value; }
        }
        public List<DiffChange> Changes
        {
            get { return m_changes; }
            set { m_changes = value; }
        }

        public void LoadFromXML(XmlNode patch)
        {
            XmlNode tmpNode = null;
            //DiffPatch p = new DiffPatch();
            this.ID = int.Parse(patch.Attributes["id"].InnerText);
            this.Name = patch.Attributes["name"].InnerText;
            this.Type = patch.Attributes["type"].InnerText;

            tmpNode = patch.ParentNode;
            if (tmpNode != null && tmpNode.Name == "patchgroup")
                this.GroupID = int.Parse(tmpNode.Attributes["id"].InnerText);

            if (patch.Attributes["recommended"] != null)
                this.Recommended = true;

            tmpNode = patch.SelectSingleNode("desc");
            if (tmpNode != null)
                this.Desc = tmpNode.InnerText;

            tmpNode = patch.SelectSingleNode("changes");
            if (tmpNode != null)
            {
                foreach (XmlNode change in tmpNode.ChildNodes)
                {
                    DiffChange c = new DiffChange();

                    if (change.Name == "byte")
                        c.Type = ChangeType.Byte;
                    else if (change.Name == "word")
                        c.Type = ChangeType.Word;
                    else if (change.Name == "dword")
                        c.Type = ChangeType.Dword;
                    else if (change.Name == "string")
                        c.Type = ChangeType.String;
                    else
                        c.Type = ChangeType.None;

                    if (change.Attributes["new"].InnerText.StartsWith("$"))
                        throw new Exception("This xDiff Patcher version does not support input variables, sorry :(");

                    c.Offset = uint.Parse(change.Attributes["offset"].InnerText, System.Globalization.NumberStyles.HexNumber);
                    if (c.Type == ChangeType.String)
                    {
                        c.New_ = change.Attributes["new"].InnerText;
                        c.Old = change.Attributes["old"].InnerText;
                    }
                    else if (c.Type == ChangeType.Byte)
                    {
                        c.New_ = byte.Parse(change.Attributes["new"].InnerText, System.Globalization.NumberStyles.HexNumber);
                        c.Old = byte.Parse(change.Attributes["old"].InnerText, System.Globalization.NumberStyles.HexNumber);
                    }
                    else if (c.Type == ChangeType.Word)
                    {
                        c.New_ = uint.Parse(change.Attributes["new"].InnerText, System.Globalization.NumberStyles.HexNumber);
                        c.Old = uint.Parse(change.Attributes["old"].InnerText, System.Globalization.NumberStyles.HexNumber);
                    }

                    this.Changes.Add(c);
                }
            }
        }
    }
}