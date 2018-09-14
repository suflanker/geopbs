//****************************************
//Copyright@diligentpig, https://geopbs.codeplex.com
//Please using source code under LGPL license.
//****************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using PBS.Util;
using System.Collections.Concurrent;

namespace PBS.DataSource
{
    public class DataSourceArcGISTilePackage:DataSourceBase,IDisposable
    {
        private TilePackageReader _tpkReader;
        public DataSourceArcGISTilePackage(string path)
        {
            Initialize(path);
            _tpkReader = new TilePackageReader(path);
        }

        void IDisposable.Dispose()
        {
            if (_tpkReader != null)
                _tpkReader.Dispose();
        }

        protected override void Initialize(string path)
        {
            this.Type = DataSourceTypePredefined.ArcGISTilePackage.ToString();
            base.Initialize(path);
        }

        protected override void ReadTilingScheme(out TilingScheme tilingScheme)
        {
            ReadArcGISTilePackageTilingSchemeFile(this.Path, out tilingScheme);
            this.TilingScheme = TilingSchemePostProcess(tilingScheme);
        }

        public override byte[] GetTileBytes(int level, int row, int col)
        {
            #region Exploded
            if (this.TilingScheme.StorageFormat == StorageFormat.esriMapCacheStorageModeExploded)
            {
                string baseentry = "v101/Layers/";//zip file name   
                string suffix = this.TilingScheme.CacheTileFormat.ToString().ToUpper().Contains("PNG") ? ".png" : ".jpg";

                baseentry += "_alllayers";
                string l = "L";
                l = level.ToString().PadLeft(2, '0');
                string r = "R";
                r = String.Format("{0:X}", row).PadLeft(8, '0');
                string c = "C";
                c = String.Format("{0:X}", col).PadLeft(8, '0');
                string str = baseentry
                    + "/L" + l
                    + "/R" + r
                    + "/C" + c + suffix;
                byte[] bytes;
                using (Stream fs = new MemoryStream(Utility.GetEntryBytesFromZIPFile(this.Path, str)))
                {
                    bytes = new byte[fs.Length];
                    fs.Read(bytes, 0, (int)fs.Length);
                }
                return bytes;
            }
            #endregion
            #region Compact
            else
            {
                //int packetSize = this.TilingScheme.PacketSize;
                //int RECORD_SIZE = 5;
                //long rowIndex = (row / packetSize) * packetSize;
                //long colIndex = (col / packetSize) * packetSize;
                //string filepath = string.Format("v101/Layers/_alllayers/L{0:d2}/R{1:x4}C{2:x4}", level, rowIndex, colIndex);
                //string bundlxentryname = string.Format("{0}.bundlx", filepath);
                //string bundleentryname = string.Format("{0}.bundle", filepath);

                //try
                //{
                //    #region retrieve the tile offset from bundlx file
                //    long bundleOffset;
                //    using (Stream fs = new MemoryStream(Utility.GetEntryBytesFromZIPFile(this.Path, bundlxentryname)))
                //    {
                //        long tileStartRow = (row / packetSize) * packetSize;
                //        long tileStartCol = (col / packetSize) * packetSize;
                //        long recordNumber = (((packetSize * (col - tileStartCol)) + (row - tileStartRow)));
                //        if (recordNumber < 0)
                //            throw new ArgumentException("Invalid level / row / col");
                //        long offset = 16 + (recordNumber * RECORD_SIZE);
                //        byte[] idxData = new byte[5];
                //        fs.Seek(offset, SeekOrigin.Begin);
                //        fs.Read(idxData, 0, 5);
                //        bundleOffset = ((idxData[4] & 0xFF) << 32) | ((idxData[3] & 0xFF) << 24) |
                //            ((idxData[2] & 0xFF) << 16) | ((idxData[1] & 0xFF) << 8) | ((idxData[0] & 0xFF));
                //    }
                //    #endregion
                //    #region read tile length and tile data from bundle file
                //    byte[] imageBytes;
                //    using (Stream fs = new MemoryStream(Utility.GetEntryBytesFromZIPFile(this.Path, bundleentryname)))
                //    {
                //        //fs.Seek(bundleOffset, SeekOrigin.Begin);
                //        //byte[] tileLengthBytes = new byte[4];//4个byte存储该tile的长度
                //        //fs.Read(tileLengthBytes, 0, 4);
                //        //long tileLength = Utility.GetLongFromBytes(tileLengthBytes, true);
                //        //imageBytes = new byte[tileLength];//紧接着后面是该tile内容
                //        //fs.Read(imageBytes, 0, Convert.ToInt32(tileLength));




                //        fs.Seek(bundleOffset, SeekOrigin.Begin);
                //        byte[] buffer = new byte[4];
                //        fs.Read(buffer, 0, 4);
                //        int recordLen = ((buffer[3] & 0xFF) << 24) | ((buffer[2] & 0xFF) << 16) | ((buffer[1] & 0xFF) << 8) | ((buffer[0] & 0xFF));
                //        imageBytes = new byte[recordLen];
                //        fs.Read(imageBytes, 0, recordLen);
                //    }
                //    return imageBytes;
                //    #endregion
                //}
                //catch (Exception)
                //{
                //    return null;
                //}

                try
                {
                    return CompactCacheGetTile(level, row, col, TilingScheme.PacketSize);
                }
                catch (Exception)
                {
                    return null;
                }
            }
            #endregion
        }
        
        private byte[] CompactCacheGetTile(int level, int row, int column, int packetSize)
        {
            string filename = _tpkReader.Filenames.FirstOrDefault<string>(entry => entry.EndsWith("conf.xml"));
            string tilePackageRootCacheFolder = filename.Substring(0, filename.Length - "conf.xml".Length);
            string path = string.Empty;
            string str2 = string.Empty;
            long num = (row / packetSize) * packetSize;
            long num2 = (column / packetSize) * packetSize;
            try
            {
                long num4;
                    string str4 = string.Format("{0}_alllayers/L{1:d2}/R{2:x4}C{3:x4}", new object[] { tilePackageRootCacheFolder, level, num, num2 });
                    path = string.Format("{0}.bundlx", str4);
                    str2 = string.Format("{0}.bundle", str4);
                    if (!_tpkReader.FileExists(path) || !_tpkReader.FileExists(str2))
                    {
                        return null;
                    }
                long offset = _tpkReader.GetBundlxOffset(level, (long)row, (long)column, packetSize);
                if (offset == -1L)
                {
                    return null;
                }
                byte[] buffer = new byte[8];
                Array.Copy(_tpkReader.Read(path, offset, 5), buffer, 5);
                num4 = BitConverter.ToInt64(buffer, 0);
                if (_tpkReader == null)
                {
                    using (Stream stream = File.OpenRead(path))
                    {
                        stream.Seek(offset, SeekOrigin.Begin);
                        stream.Read(buffer, 0, 5);
                        using (Stream stream2 = File.OpenRead(str2))
                        {
                            stream2.Seek(num4, SeekOrigin.Begin);
                            byte[] buffer3 = new byte[4];
                            stream2.Read(buffer3, 0, 4);
                            int num5 = BitConverter.ToInt32(buffer3, 0);
                            byte[] buffer4 = new byte[num5];
                            stream2.Read(buffer4, 0, num5);
                            return buffer4;
                        }
                    }
                }
                int count = BitConverter.ToInt32(_tpkReader.Read(str2, num4, 4), 0);
                return _tpkReader.Read(str2, num4 + 4L, count);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    internal class TilePackageReader : IDisposable
    {
        private static string _constInvalidTPK = "Invalid Tile Package File!";
        private string _zipFilename;
        private bool _isOpen;
        private ConcurrentStack<FileStream> _streams = new ConcurrentStack<FileStream>();
        private Dictionary<string, FileEntry> _files = new Dictionary<string, FileEntry>();
        public IEnumerable<string> Filenames
        {
            get
            {
                if (!this._isOpen)
                {
                    return null;
                }
                return this._files.Keys;
            }
        }
        public TilePackageReader(string path)
        {
            Open(path);
        }
        private void Open(string path)
        {
            if (!this._isOpen)
            {
                FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                Dictionary<string, FileEntry> files = new Dictionary<string, FileEntry>();
                try
                {
                    ReadCentralDirectory(stream, files);
                }
                catch
                {
                    stream.Dispose();
                    throw;
                }
                this._streams.Push(stream);
                this._files = files;
                this._zipFilename = path;
                this._isOpen = true;
            }
        }

        private static void ReadCentralDirectory(FileStream stream, Dictionary<string, FileEntry> files)
        {
            long position = stream.Length - 0x16L;
            if (stream.Length <= 0x16L)
            {
                throw new Exception(_constInvalidTPK);
            }
            byte[] bytes = ReadBytes(stream, position, 0x16);
            if (!SignatureValid(bytes, 0x6054b50))
            {
                throw new Exception(_constInvalidTPK);
            }
            long num2 = BitConverter.ToUInt32(bytes, 0x10);
            long num3 = 0L;
            if (num2 != 0xffffffffL)
            {
                num3 = BitConverter.ToUInt16(bytes, 10);
            }
            else
            {
                byte[] buffer2 = ReadBytes(stream, position - 20L, 20);
                //if (!SignatureValid(buffer2, 0x7064b50))
                //{
                //    throw new TileCacheException(Resources.ArcGISLocalTiledLayer_InvalidTilePackage);
                //}
                long num4 = BitConverter.ToInt64(buffer2, 8);
                byte[] buffer3 = ReadBytes(stream, num4, 0x38);
                //if (!SignatureValid(buffer3, 0x6064b50))
                //{
                //    throw new TileCacheException(Resources.ArcGISLocalTiledLayer_InvalidTilePackage);
                //}
                num3 = BitConverter.ToInt64(buffer3, 0x20);
                num2 = BitConverter.ToInt64(buffer3, 0x30);
            }
            for (int i = 0; i < num3; i++)
            {
                byte[] buffer4 = ReadBytes(stream, num2, 0x2e);
                if (!SignatureValid(buffer4, 0x2014b50))
                {
                    throw new Exception(_constInvalidTPK);
                }
                uint num6 = BitConverter.ToUInt32(buffer4, 0x2a);
                ushort count = BitConverter.ToUInt16(buffer4, 0x1c);
                ushort num8 = BitConverter.ToUInt16(buffer4, 30);
                ushort num9 = BitConverter.ToUInt16(buffer4, 0x20);
                ushort num10 = BitConverter.ToUInt16(buffer4, 10);
                if (count != 0)
                {
                    //if (num10 != 0)
                    //{
                    //    throw new TileCacheException(Resources.ArcGISLocalTiledLayer_InvalidTilePackage);
                    //}
                    byte[] buffer5 = ReadBytes(stream, num2 + 0x2eL, count);
                    string filename = Encoding.UTF8.GetString(buffer5, 0, count);
                    long localFileHeaderPos = 0x7fffffffffffffffL;
                    if (num6 != uint.MaxValue)
                    {
                        localFileHeaderPos = num6;
                    }
                    else
                    {
                        //if (num8 == 0)
                        //{
                        //    throw new TileCacheException(Resources.ArcGISLocalTiledLayer_InvalidTilePackage);
                        //}
                        byte[] buffer6 = ReadBytes(stream, (num2 + 0x2eL) + count, num8);
                        ushort num12 = BitConverter.ToUInt16(buffer6, 0);
                        ushort num13 = BitConverter.ToUInt16(buffer6, 2);
                        //if ((num12 != 1) || (num13 != 8))
                        //{
                        //    throw new TileCacheException(Resources.ArcGISLocalTiledLayer_InvalidTilePackage);
                        //}
                        localFileHeaderPos = BitConverter.ToInt64(buffer6, 4);
                    }
                    FileEntry entry = new FileEntry(filename, localFileHeaderPos);
                    files[StandardiseFilename(filename)] = entry;
                    num2 += ((0x2e + count) + num8) + num9;
                }
            }
        }

        private static string StandardiseFilename(string filename)
        {
            return filename.Replace('\\', '/').ToLower();
        }        

        internal long GetBundlxOffset(int level, long row, long column, int packetSize)
        {
            long num = (row / ((long)packetSize)) * packetSize;
            long num2 = (column / ((long)packetSize)) * packetSize;
            long num3 = (packetSize * (column - num2)) + (row - num);
            if (num3 < 0L)
            {
                return -1L;
            }
            return (0x10L + (num3 * 5L));
        }

        internal byte[] Read(string filename, long offset, int count)
        {
            FileEntry entry;
            FileStream stream;
            byte[] buffer;
            if (!this._files.TryGetValue(filename.ToLower(), out entry))
            {
                throw new Exception(_constInvalidTPK);
            }
            if (!this._streams.TryPop(out stream))
            {
                stream = new FileStream(_zipFilename, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            try
            {
                this.UpdateFileDataPos(stream, entry);
                int num = count;
                if (num == -1)
                {
                    num = ((int)entry.FileDataSize) - ((int)offset);
                }
                long position = entry.FileDataPos + offset;
                if ((position < entry.FileDataPos) || ((position + num) > (entry.FileDataPos + entry.FileDataSize)))
                {
                    throw new Exception(_constInvalidTPK);
                }
                buffer = ReadBytes(stream, position, num);
            }
            catch (IOException)
            {
                if (stream != null)
                {
                    stream.Dispose();
                }
                stream = null;
                throw;
            }
            finally
            {
                if (stream != null)
                {
                    this._streams.Push(stream);
                }
            }
            return buffer;
        }

        private void UpdateFileDataPos(FileStream stream, FileEntry fileEntry)
        {
            if (!fileEntry.HasFileData)
            {
                lock (fileEntry.LockThis)
                {
                    if (!fileEntry.HasFileData)
                    {
                        byte[] bytes = ReadBytes(stream, fileEntry.LocalFileHeaderPos, 30);
                        if (!SignatureValid(bytes, 0x4034b50))
                        {
                            throw new Exception(_constInvalidTPK);
                        }
                        ushort num = BitConverter.ToUInt16(bytes, 0x1a);
                        ushort num2 = BitConverter.ToUInt16(bytes, 0x1c);
                        uint num3 = BitConverter.ToUInt32(bytes, 0x12);
                        uint num4 = BitConverter.ToUInt32(bytes, 0x16);
                        if (num3 != num4)
                        {
                            throw new Exception(_constInvalidTPK);
                        }
                        if (num4 == uint.MaxValue)
                        {
                            throw new Exception(_constInvalidTPK);
                        }
                        fileEntry.FileDataPos = ((fileEntry.LocalFileHeaderPos + 30L) + num) + num2;
                        fileEntry.FileDataSize = num4;
                        fileEntry.HasFileData = true;
                    }
                }
            }
        }

        private static byte[] ReadBytes(Stream stream, long position, int count)
        {
            byte[] buffer = new byte[count];
            stream.Seek(position, SeekOrigin.Begin);
            stream.Read(buffer, 0, count);
            return buffer;
        }

        private static bool SignatureValid(byte[] bytes, uint signature)
        {
            uint num = BitConverter.ToUInt32(bytes, 0);
            return (signature == num);
        }

        internal bool FileExists(string filename)
        {
            if (!this._isOpen)
            {
                return false;
            }
            return this._files.ContainsKey(StandardiseFilename(filename));
        }

        private class FileEntry
        {
            public long FileDataPos;
            public uint FileDataSize;
            public string Filename;
            public volatile bool HasFileData;
            public long LocalFileHeaderPos;
            public object LockThis;

            public FileEntry(string filename, long localFileHeaderPos)
            {
                this.Filename = filename;
                this.LocalFileHeaderPos = localFileHeaderPos;
                this.FileDataPos = 0x7fffffffffffffffL;
                this.FileDataSize = uint.MaxValue;
                this.HasFileData = false;
                this.LockThis = new object();
            }
        }

        public void Dispose()
        {
            if (this._isOpen)
            {
                FileStream stream;
                while (this._streams.TryPop(out stream))
                {
                    stream.Dispose();
                }
                this._files = null;
                this._zipFilename = null;
                this._isOpen = false;
            }
        }
    }
}
