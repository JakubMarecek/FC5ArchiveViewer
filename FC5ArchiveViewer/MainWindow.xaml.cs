/* 
 * FC5 Archive Viewer
 * Copyright (C) 2020  Jakub Mareček (info@jakubmarecek.cz)
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with FC5 Archive Viewer.  If not, see <https://www.gnu.org/licenses/>.
 */

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace FC5ArchiveViewer
{
    public class MyTreeViewItem
    {
        public int Level { get; set; }

        public string Name { get; set; }

        public string FileName { get; set; }

        public List<MyTreeViewItem> SubItems { get; set; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string m_Path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        string m_File = @"\FCBConverterFileNames.list";
        Dictionary<ulong, string> m_HashList = new Dictionary<ulong, string>();

        SortedDictionary<ulong, FatEntry> entries;

        string currentFile;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Title = "FC5 Archive Viewer - Opening archive, please wait...";

            string[] args = Environment.GetCommandLineArgs();
            if (args.Length < 2)
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Title = "FC5 Archive Viewer - Open archive...";
                ofd.Filter = "*.fat|*.fat";
                if (ofd.ShowDialog() == true)
                {
                    currentFile = ofd.FileName;
                }
                else
                    Environment.Exit(0);
            }
            else
                currentFile = args[1];

            if (!currentFile.EndsWith(".fat"))
            {
                MessageBox.Show("The file is not valid FAT file.", "FC5 Archive Viewer", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(0);
            }

            entries = GetFatEntries(currentFile);

            LoadFile();

            List<string> files = new List<string>();

            foreach (KeyValuePair<ulong, FatEntry> entry in entries)
            {
                string m_Hash = entry.Value.NameHash.ToString("X16");
                string m_FileName = null;
                if (m_HashList.ContainsKey(entry.Value.NameHash))
                {
                    m_HashList.TryGetValue(entry.Value.NameHash, out m_FileName);
                }
                else
                {
                    m_FileName = @"__Unknown\" + m_Hash;
                }

                files.Add(m_FileName);
            }

            files = files.OrderBy(q => q).ToList();

            PopulateTreeView(foldersItem, files, '\\');

            Title = "FC5 Archive Viewer - " + currentFile;
        }

        private void PopulateTreeView(TreeView treeView, IEnumerable<string> paths, char pathSeparator)
        {
            List<MyTreeViewItem> sourceCollection = new List<MyTreeViewItem>();
            foreach (string path in paths)
            {
                string[] fileItems = path.Split(pathSeparator);
                if (fileItems.Any())
                {
                    MyTreeViewItem root = sourceCollection.FirstOrDefault(x => x.Name.Equals(fileItems[0]) && x.Level.Equals(1));
                    if (root == null)
                    {
                        root = new MyTreeViewItem()
                        {
                            Level = 1,
                            Name = fileItems[0],
                            FileName = path,
                            SubItems = new List<MyTreeViewItem>()
                        };
                        sourceCollection.Add(root);
                    }

                    if (fileItems.Length > 1)
                    {
                        MyTreeViewItem parentItem = root;
                        int level = 2;
                        for (int i = 1; i < fileItems.Length; ++i)
                        {
                            MyTreeViewItem subItem = parentItem.SubItems.FirstOrDefault(x => x.Name.Equals(fileItems[i]) && x.Level.Equals(level));
                            if (subItem == null)
                            {
                                subItem = new MyTreeViewItem()
                                {
                                    Name = fileItems[i],
                                    Level = level,
                                    FileName = path,
                                    SubItems = new List<MyTreeViewItem>()
                                };
                                parentItem.SubItems.Add(subItem);
                            }

                            parentItem = subItem;
                            level++;
                        }
                    }
                }
            }

            treeView.ItemsSource = sourceCollection;
        }

        private void foldersItem_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeView tree = (TreeView)sender;
            MyTreeViewItem temp = ((MyTreeViewItem)tree.SelectedItem);

            if (Path.GetFileName(temp.FileName) == temp.Name)
                if (m_HashList.ContainsValue(temp.FileName))
                {
                    SaveFileDialog sfd = new SaveFileDialog();
                    sfd.Title = "FC5 Archive Viewer - Extract file...";
                    sfd.Filter = "*.*|*.*";
                    sfd.FileName = Path.GetFileName(temp.FileName);
                    if (sfd.ShowDialog() == true)
                    {
                        SaveFile(sfd.FileName, temp.FileName);
                    }
                }
        }

        private void SaveFile(string filename, string filenamefat)
        {
            ulong hash = m_HashList.FirstOrDefault(x => x.Value == filenamefat).Key;

            byte[] result = null;
            FatEntry entry = entries[hash];

            FileStream fileStream = File.Open(currentFile.Replace(".fat", ".dat"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fileStream.Seek(entry.Offset, SeekOrigin.Begin);
            result = new byte[entry.UncompressedSize];

            switch (entry.CompressionScheme)
            {
                case CompressionScheme.None:
                    new BinaryReader(fileStream).Read(result, 0, (int)entry.UncompressedSize);

                    Console.WriteLine("Getting file completed.");

                    fileStream.Dispose();
                    fileStream.Close();
                    break;
                case CompressionScheme.Zlib:
                    BinaryReader binaryReader = new BinaryReader(fileStream);
                    byte[] array = new byte[entry.CompressedSize];
                    binaryReader.Read(array, 0, (int)entry.CompressedSize);

                    new LZ4Sharp.LZ4Decompressor64().Decompress(array, result);

                    Console.WriteLine("Getting file completed.");

                    fileStream.Dispose();
                    fileStream.Close();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            File.WriteAllBytes(filename, result);
            MessageBox.Show("The file was successfully extracted!", "FC5 Archive Viewer", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadFile()
        {
            if (!File.Exists(m_Path + m_File))
            {
                Console.WriteLine(m_Path + m_File + " doesn't exist!");
                return;
            }

            string[] ss = File.ReadAllLines(m_Path + m_File);
            for (int i = 0; i < ss.Length; i++)
            {
                ulong a = Gibbed.Dunia2.FileFormats.CRC64.Hash(ss[i]);
                if (!m_HashList.ContainsKey(a))
                    m_HashList.Add(a, ss[i]);
            }

            Console.WriteLine("Files loaded: " + m_HashList.Count);
            Console.WriteLine("");
        }

        private SortedDictionary<ulong, FatEntry> GetFatEntries(string fatFile)
        {
            SortedDictionary<ulong, FatEntry> Entries = new SortedDictionary<ulong, FatEntry>();

            FileStream TFATStream = new FileStream(fatFile, FileMode.Open);
            BinaryReader TFATReader = new BinaryReader(TFATStream);

            int dwMagic = TFATReader.ReadInt32();
            int dwVersion = TFATReader.ReadInt32();
            int dwUnknown = TFATReader.ReadInt32();
            int dwZero1 = TFATReader.ReadInt32();
            int dwZero2 = TFATReader.ReadInt32();
            int dwTotalFiles = TFATReader.ReadInt32();

            if (dwMagic != 0x46415432)
            {
                Console.WriteLine("Invalid FAT Index file!");
                TFATReader.Dispose();
                TFATStream.Dispose();
                TFATReader.Close();
                TFATStream.Close();
                return null;
            }

            if (dwVersion != 10)
            {
                Console.WriteLine("Invalid version of FAT Index file!");
                TFATReader.Dispose();
                TFATStream.Dispose();
                TFATReader.Close();
                TFATStream.Close();
                return null;
            }

            for (int i = 0; i < dwTotalFiles; i++)
            {
                ulong dwHash = TFATReader.ReadUInt64();
                uint dwUncompressedSize = TFATReader.ReadUInt32();
                uint dwUnresolvedOffset = TFATReader.ReadUInt32();
                uint dwCompressedSize = TFATReader.ReadUInt32();

                uint dwFlag = dwUncompressedSize & 3;
                ulong dwOffset = dwCompressedSize >> 29 | 8ul * dwUnresolvedOffset;
                dwHash = (dwHash << 32) + (dwHash >> 32);
                dwCompressedSize = (dwCompressedSize & 0x1FFFFFFF);
                dwUncompressedSize = (dwUncompressedSize >> 2);

                var entry = new FatEntry();
                entry.NameHash = dwHash;
                entry.UncompressedSize = dwUncompressedSize;
                entry.Offset = (long)dwOffset;
                entry.CompressedSize = dwCompressedSize;
                entry.CompressionScheme = dwFlag == 0 ? CompressionScheme.None : CompressionScheme.Zlib;

                Entries[entry.NameHash] = entry;
            }

            TFATReader.Dispose();
            TFATStream.Dispose();
            TFATReader.Close();
            TFATStream.Close();

            return Entries;
        }
    }
}
