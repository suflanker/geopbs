//****************************************
//Copyright@diligentpig, https://geopbs.codeplex.com
//Please using source code under LGPL license.
//****************************************
using System;
using System.Text;
using PBS.Service;
using System.ServiceModel.Web;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.IO;
using System.Windows.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using Memcached.ClientLibrary;
using System.ServiceProcess;
using System.Runtime.InteropServices;
using ICSharpCode.SharpZipLib.Zip;
using System.Windows.Threading;
using System.Security.Principal;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using log4net;
using System.Reflection;

namespace PBS.Util
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error,
        Fatal
    }
    public static class Utility
    {
        private static ILog _logger;
        
        /// <summary>
        /// call this as early as possible to init log4net.
        /// </summary>
        // ref:log4net In DLL Project http://www.cleancode.co.nz/blog/670/log4net-in-dll-project
        public static void InitLog4net()
        {
            log4net.Config.XmlConfigurator.ConfigureAndWatch(new FileInfo(
                Path.GetDirectoryName(Assembly.GetAssembly(typeof(Utility)).Location)
               + @"\" + "log4net.config"));//need to copy log4net.config file to final output directory
             _logger= LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="level">log level</param>
        /// <param name="e">if e is null, just log description.</param>
        /// <param name="desc">extra description at first to say.</param>
        public static void Log(LogLevel level, Exception e, string desc = null)
        {
            StackTrace st = new StackTrace(1, true);
            StackFrame sf = st.GetFrame(0);
            //when release without .pdb file, then fileName, lineNumber will be empty.
            string fileName = sf.GetFileName();
            string lineNumber = sf.GetFileLineNumber().ToString();
            string methondName = sf.GetMethod().Name;            
            if (e == null)
            {
                innerLog(level,desc+"\r\n"+
                    fileName+"\r\n"+
                    "Method:"+methondName+"\r\n"+
                    "LineNum:"+lineNumber);
                return;
            }
            string innerMessage = e.InnerException == null ? string.Empty : e.InnerException.Message;
            if (desc == null)
                innerLog(level,"Message:" + e.Message + "\r\n" +
                    "InnerMessage:" + innerMessage + "\r\n" +
                    "StackTrace:" + e.StackTrace +"\r\n"+
                    fileName + "\r\n" +
                    "Log@Method:" + methondName + "\r\n" +
                    "Log@LineNum:" + lineNumber);
            else
                innerLog(level,desc + "\r\n" +
                    "Message:" + e.Message + "\r\n" +
                    "InnerMessage:" + innerMessage + "\r\n" +
                    "StackTrace:" + e.StackTrace + "\r\n" +
                    fileName + "\r\n" +
                    "Log@Method:" + methondName + "\r\n" +
                    "Log@LineNum:" + lineNumber);
        }

        private static void innerLog(LogLevel l,string message)
        {
            switch (l)
            {
                case LogLevel.Debug:
                    if (_logger.IsDebugEnabled)
                    _logger.Debug(message);
                    break;
                case LogLevel.Info:
                    if (_logger.IsInfoEnabled)
                    _logger.Info(message);
                    break;
                case LogLevel.Warn:
                    if (_logger.IsWarnEnabled)
                        _logger.Warn(message);
                    break;
                case LogLevel.Error:
                    if (_logger.IsErrorEnabled)
                        _logger.Error(message);
                    break;
                case LogLevel.Fatal:
                    if (_logger.IsFatalEnabled)
                        _logger.Fatal(message);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path">full file name including path</param>
        /// <returns></returns>
        public static bool IsValidFilename(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            Regex regEx = new Regex("[\\*\\\\/:?<>|\"]");

            return !regEx.IsMatch(Path.GetFileName(path));
        }

        [DllImport("wininet.dll")]
        public extern static bool InternetGetConnectedState(out int Description, int ReservedValue);
        public static bool IsConnectedToInternet()
        {
            int Desc;
            return InternetGetConnectedState(out Desc, 0);
        }        

        public static Point GeographicToWebMercator(Point p)
        {
            double x = p.X;
            double y = p.Y;
            if ((y < -90.0) || (y > 90.0))
            {
                throw new ArgumentException("Point does not fall within a valid range of a geographic coordinate system.");
            }
            double num = x * 0.017453292519943295;
            double xx = 6378137.0 * num;
            double a = y * 0.017453292519943295;
            return new Point(xx, 3189068.5 * Math.Log((1.0 + Math.Sin(a)) / (1.0 - Math.Sin(a))));
        }

        public static Point WebMercatorToGeographic(Point p)
        {
            double originShift = 2 * Math.PI * 6378137 / 2.0;
            double lon = (p.X / originShift) * 180.0;
            double lat = (p.Y / originShift) * 180.0;

            lat = 180 / Math.PI * (2 * Math.Atan(Math.Exp(lat * Math.PI / 180.0)) - Math.PI / 2.0);
            return new Point(lon, lat);
        }

        public static int GetRequestPortNumber()
        {
            if (WebOperationContext.Current != null)
            {
                string host = WebOperationContext.Current.IncomingRequest.Headers["HOST"];//127.0.0.1:8000
                return host.Split(new char[] { ':' }).Length ==1 ?80: int.Parse(host.Split(new char[] { ':' })[1]);
            }
            return -1;
        }

        public static string GetRequestIPAddress()
        {
            OperationContext context = OperationContext.Current;
            MessageProperties messageProperties = context.IncomingMessageProperties;
            RemoteEndpointMessageProperty endpointProperty =
              messageProperties[RemoteEndpointMessageProperty.Name] as RemoteEndpointMessageProperty;
            return endpointProperty.Address;
        }

        /// <summary>
        /// MBTile implement Tiled Map Service Specification. http://wiki.osgeo.org/wiki/Tile_Map_Service_Specification
        /// Used to convert row/col number in TMS to row/col number in Google/ArcGIS.
        /// TMS starts (0,0) from left bottom corner, while Google/ArcGIS/etc. starts (0,0) from left top corner.
        /// </summary>
        /// <param name="level"></param>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="row"></param>
        /// <param name="col"></param>
        public static void ConvertGoogleTileToTMSTile(int level, int row, int col, out int outRow, out int outCol)
        {
            outCol = col;
            outRow = ((int)(Math.Pow(2.0, (double)level) - 1.0)) - row;
        }

        public static void ConvertTMSTileToGoogleTile(int level, int row, int col, out int outRow, out int outCol)
        {
            outCol = col;
            outRow = ((int)(Math.Pow(2.0, (double)level) - 1.0)) - row;
        }
        /// <summary>
        /// calculate the bounding box of a tile or a bundle.
        /// </summary>
        /// <param name="tileOrigin">tiling scheme origin</param>
        /// <param name="resolution">the resolution of the level which the tile blongs</param>
        /// <param name="tileRows">the pixel count of row in the tile</param>
        /// <param name="tileCols">the pixel count of column in the tile</param>
        /// <param name="row">the row number of the tile</param>
        /// <param name="col">the column number of the tile</param>
        /// <param name="xmin"></param>
        /// <param name="ymin"></param>
        /// <param name="xmax"></param>
        /// <param name="ymax"></param>
        public static void CalculateBBox(Point tileOrigin,double resolution,int tileRows,int tileCols, int row, int col, out double xmin, out double ymin, out double xmax, out double ymax)
        {
            //calculate the bbox
            xmin = tileOrigin.X + resolution * tileCols * col;
            ymin = tileOrigin.Y - resolution * tileRows * (row + 1);
            xmax = tileOrigin.X + resolution * tileCols * (col + 1);
            ymax = tileOrigin.Y - resolution * tileRows * row;
        }

        /// <summary>
        /// using for bing maps
        /// </summary>
        /// <param name="tileX"></param>
        /// <param name="tileY"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        public static string TileXYToQuadKey(int tileX, int tileY, int level)
        {
            StringBuilder quadKey = new StringBuilder();
            for (int i = level; i > 0; i--)
            {
                char digit = '0';
                int mask = 1 << (i - 1);//掩码，最高位设为1，其他位设为0
                if ((tileX & mask) != 0)//与运算取得tileX的最高位，若为1，则加1
                {
                    digit++;
                }
                if ((tileY & mask) != 0)//与运算取得tileY的最高位，若为1，则加2
                {
                    digit++;
                    digit++;
                }
                quadKey.Append(digit);//也即2*y+x
            }
            return quadKey.ToString();
        }

        public static byte[] MakeGrayUsingWPF(byte[] tileBytes)
        {            
            using (MemoryStream ms = new MemoryStream(tileBytes))
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                //bitmap.DecodePixelWidth = 200;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                FormatConvertedBitmap fcb = new FormatConvertedBitmap();
                fcb.BeginInit();
                fcb.Source = bitmap;
                fcb.DestinationFormat = PixelFormats.Gray8;
                fcb.DestinationPalette = BitmapPalettes.WebPaletteTransparent;
                fcb.AlphaThreshold = 0.5;
                fcb.EndInit();
                //ref: http://192.168.0.106:8000/PBS/rest/services/BingMapsRoad/MapServer/tile/14/6208/13491
                //gray4pngencoder=14.5kb
                //gray8pngencoder=21.5kb
                //gray16pngencoder=36.9kb
                //gray8jpegencoder=22.9kb
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                BitmapFrame frame = BitmapFrame.Create(fcb);
                encoder.Frames.Add(frame);
                using (MemoryStream stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    return stream.ToArray();
                }
            }
        }

        public static byte[] MakeGrayUsingGDI(byte[] tileBytes)
        {
            using (MemoryStream ms = new MemoryStream(tileBytes))
            {
                Bitmap gdiBitmap = new Bitmap(ms);//default is PixelFormat.Format8bppIndexed
                //create a blank bitmap the same size as original
                Bitmap newBitmap = new Bitmap(gdiBitmap.Width, gdiBitmap.Height);

                //get a graphics object from the new image
                Graphics g = Graphics.FromImage(newBitmap);

                //create the grayscale ColorMatrix
                //Invert matrix is found by googleing:invert colormatrix
                ColorMatrix colorMatrix = new ColorMatrix(
                   new float[][] 
      {
         new float[] {.3f, .3f, .3f, 0, 0},
         new float[] {.59f, .59f, .59f, 0, 0},
         new float[] {.11f, .11f, .11f, 0, 0},
         new float[] {0, 0, 0, 1, 0},
         new float[] {0, 0, 0, 0, 1}
      });

                //create some image attributes
                ImageAttributes attributes = new ImageAttributes();
                //set the color matrix attribute
                attributes.SetColorMatrix(colorMatrix);

                //draw the original image on the new image
                //using the grayscale color matrix
                g.DrawImage(gdiBitmap, new Rectangle(0, 0, gdiBitmap.Width, gdiBitmap.Height),
                   0, 0, gdiBitmap.Width, gdiBitmap.Height, GraphicsUnit.Pixel, attributes);

                //dispose the Graphics object
                g.Dispose();
                BitmapSource bitmapSource = ConvertToBitmapSource(newBitmap);
                
                //http://192.168.100.109:8000/PBS/rest/services/GoogleMapsRoad/MapServer/tile/11/775/1686
                //pngencoder 79.5kb
                //jpegencoder 32kb
                JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                encoder.QualityLevel = 75;
                BitmapFrame frame = BitmapFrame.Create(bitmapSource);
                encoder.Frames.Add(frame);
                using (MemoryStream stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    return stream.ToArray();
                }
            }
        }

        /// <summary>
        /// C# Tutorial - Convert a Color Image to Grayscale
        /// ref: http://www.switchonthecode.com/tutorials/csharp-tutorial-convert-a-color-image-to-grayscale
        /// </summary>
        /// <param name="original"></param>
        /// <returns></returns>
        public static byte[] MakeInvertUsingGDI(byte[] tileBytes)
        {
            using (MemoryStream ms = new MemoryStream(tileBytes))
            {
                Bitmap gdiBitmap = new Bitmap(ms);//default is PixelFormat.Format8bppIndexed
                //create a blank bitmap the same size as original
                Bitmap newBitmap = new Bitmap(gdiBitmap.Width, gdiBitmap.Height);
                //get a graphics object from the new image
                Graphics g = Graphics.FromImage(newBitmap);

                //create the grayscale ColorMatrix
                //Invert matrix is found by googleing:invert colormatrix
                ColorMatrix colorMatrix = new ColorMatrix(
                   new float[][]
{
   new float[] {-1, 0, 0, 0, 0},
   new float[] {0, -1, 0, 0, 0},
   new float[] {0, 0, -1, 0, 0},
   new float[] {0, 0, 0, 1, 0},
   new float[] {1, 1, 1, 0, 1}
});

                //create some image attributes
                ImageAttributes attributes = new ImageAttributes();
                //set the color matrix attribute
                attributes.SetColorMatrix(colorMatrix);

                //draw the original image on the new image
                //using the grayscale color matrix
                g.DrawImage(gdiBitmap, new Rectangle(0, 0, gdiBitmap.Width, gdiBitmap.Height),
                   0, 0, gdiBitmap.Width, gdiBitmap.Height, GraphicsUnit.Pixel, attributes);

                //dispose the Graphics object
                g.Dispose();                

                BitmapSource bitmapSource = ConvertToBitmapSource(newBitmap);
                gdiBitmap.Dispose();
                newBitmap.Dispose();
                //http://192.168.100.109:8000/PBS/rest/services/GoogleMapsRoad/MapServer/tile/11/775/1686
                //pngencoder 79.5kb
                //jpegencoder 32kb
                JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                encoder.QualityLevel = 75;
                BitmapFrame frame = BitmapFrame.Create(bitmapSource);
                encoder.Frames.Add(frame);
                using (MemoryStream stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    return stream.ToArray();
                }
            }
        }

        /// <summary>
        /// Getting Started with Shader Effects in WPF:http://www.codeproject.com/KB/WPF/WPF_shader_effects.aspx
        /// gpu enabled
        /// </summary>
        /// <param name="tileBytes"></param>
        /// <param name="style"></param>
        /// <returns></returns>
        public static byte[] MakeShaderEffect(byte[] tileBytes, VisualStyle style)
        {
            if (tileBytes == null)
                return null;
            using (MemoryStream ms = new MemoryStream(tileBytes))
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                //bitmap.DecodePixelWidth = 200;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                DrawingVisual drawingVisual = new DrawingVisual();
                using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawImage(bitmap, new Rect(new System.Windows.Size(bitmap.PixelWidth, bitmap.PixelHeight)));
                }
                switch (style)
                {
                    case VisualStyle.None:
                        break;
                    case VisualStyle.Gray:
                        drawingVisual.Effect = new PBS.Shaders.MonochromeEffect();
                        break;
                    case VisualStyle.Invert:
                        drawingVisual.Effect = new PBS.Shaders.InvertColorEffect();
                        break;
                    case VisualStyle.Tint:
                        drawingVisual.Effect = new PBS.Shaders.TintShaderEffect();
                        break;
                    //case VisualStyle.Saturation:
                    //    drawingVisual.Effect = new PBS.Shaders.SaturationEffect();
                    //    break;
                    case VisualStyle.Embossed:
                        drawingVisual.Effect = new PBS.Shaders.EmbossedEffect(3.5);
                        break;
                    default:
                        break;
                }

                RenderTargetBitmap rtb = new RenderTargetBitmap(bitmap.PixelWidth, bitmap.PixelHeight, 96, 96, System.Windows.Media.PixelFormats.Default);
                rtb.Render(drawingVisual);
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                BitmapFrame frame = BitmapFrame.Create(rtb);
                encoder.Frames.Add(frame);
                using (MemoryStream stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    return stream.ToArray();
                }
            }
        }

        /// <summary>
        /// http://www.netframeworkdev.com/windows-presentation-foundation-wpf/invert-background-brush-86966.shtml
        /// Invert Background Brush
        /// </summary>
        /// <param name="gdiPlusBitmap"></param>
        /// <returns></returns>
        public static BitmapSource ConvertToBitmapSource(Bitmap gdiPlusBitmap)
        {
            IntPtr hBitmap = gdiPlusBitmap.GetHbitmap();
            try
            {
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                //ref:Bitmap to BitmapSource http://www.codeproject.com/KB/WPF/BitmapToBitmapSource.aspx
                DeleteObject(hBitmap);//very important to avoid memory leak
            }
        }

        /// <summary>
        /// C#对Windows服务操作(注册安装服务,卸载服务,启动停止服务,判断服务存在)
        /// http://blog.csdn.net/hejialin666/article/details/5657695
        /// </summary>
        /// <param name="serviceName"></param>
        /// <returns></returns>
        public static bool IsWindowsServiceExisted(string serviceName)
        {
            ServiceController[] services = ServiceController.GetServices();
            foreach (ServiceController s in services)
            {
                if (s.ServiceName == serviceName)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// determine if a windows service is running.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static bool IsWindowsServiceStarted(string name)
        {
            ServiceController[] service = ServiceController.GetServices();
            bool isStart = false;
            for (int i = 0; i < service.Length; i++)
            {
                if (service[i].DisplayName.ToUpper().Contains(name.ToUpper()))
                {
                    if (service[i].Status == ServiceControllerStatus.Running)
                    {
                        isStart = true;
                        break;
                    }
                }
            }
            return isStart;
        }

        /// <summary>
        /// Run cmd command in code
        /// C#中一种执行命令行或DOS内部命令的方法:http://www.cppblog.com/andxie99/archive/2006/12/09/16200.html
        /// </summary>
        /// <param name="strIp">cmd line input</param>
        /// <param name="returnProcess">wether return the Cmd Process for holding it for longer using</param>
        /// <param name="sleepTime">main thread sleep time to execute the command</param>
        /// <returns></returns>
        public static Process Cmd(string strCmd,bool returnProcess,out string strResult,int sleepTime=250)
        {
            // 实例一个Process类,启动一个独立进程
            Process p = new Process();

            // 设定程序名
            p.StartInfo.FileName = "cmd.exe";
            // 关闭Shell的使用
            p.StartInfo.UseShellExecute = false;
            // 重定向标准输入
            p.StartInfo.RedirectStandardInput = true;
            // 重定向标准输出
            p.StartInfo.RedirectStandardOutput = true;
            //重定向错误输出
            p.StartInfo.RedirectStandardError = true;
            // 设置不显示窗口
            p.StartInfo.CreateNoWindow = true;

            p.Start();

            p.StandardInput.WriteLine(strCmd);
            System.Threading.Thread.Sleep(sleepTime);            
            if (returnProcess == false)
            {
                p.StandardInput.WriteLine("exit");
                System.Threading.Thread.Sleep(sleepTime);
                strResult = p.StandardOutput.ReadToEnd();//output information
                p.Close();
                return null;
            }
            else
            {
                strResult = "";//StandardOutput.ReadToEnd() must be after p.StandardInput.WriteLine("exit")
                return p;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="zipFilePath"></param>
        /// <param name="entryName">the specified filename in zip file to be retrieved, may contain directory components separated by slashes ('/'). http://www.icsharpcode.net/CodeReader/SharpZipLib/031/ZipZipFile.cs.html</param>
        /// <returns></returns>
        public static byte[] GetEntryBytesFromZIPFile(string zipFilePath, string entryName)
        {         
            //Using SharpZipLib to unzip specific files?http://stackoverflow.com/questions/328343/using-sharpziplib-to-unzip-specific-files
            if (!System.IO.File.Exists(zipFilePath))
            {
                return null;
            }
            using (FileStream fs = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read))
            {
                ZipFile zf = new ZipFile(fs);
                ZipEntry ze = zf.GetEntry(entryName);
                if (ze == null)
                    return null;
                //try
                //{
                using (Stream stream = zf.GetInputStream(ze))
                {
                    return StreamToBytes(stream);
                }
                //}
                //finally
                //{
                //    zf.Close();
                //}
            }          
        }

        public static byte[] StreamToBytes(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        public static byte[] SerializeObject(object data)
        {
            if (data == null)
                return null;
            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            MemoryStream rems = new MemoryStream();
            formatter.Serialize(rems, data);
            return rems.GetBuffer();
        }
        public static object DeserializeObject(byte[] data)
        {
            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            MemoryStream rems = new MemoryStream(data);
            data = null;
            return formatter.Deserialize(rems);
        }

        public static bool Is64bitOS()
        {
            //How to detect Windows 64 bit platform with .net? http://stackoverflow.com/questions/336633/how-to-detect-windows-64-bit-platform-with-net
            //How to check whether the system is 32 bit or 64 bit ?:http://social.msdn.microsoft.com/Forums/da-DK/csharpgeneral/thread/24792cdc-2d8e-454b-9c68-31a19892ca53
            return Environment.Is64BitOperatingSystem;
        }

        //应对32位程序在64位系统上访问注册表和文件自动转向问题:http://www.cnblogs.com/FlyingBread/archive/2007/01/21/624291.html
        /// <summary>
        /// Disable system32 directory redirect on 64bit system
        /// </summary>
        /// <param name="ptr"></param>
        /// <returns></returns>
        [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool Wow64DisableWow64FsRedirection(ref IntPtr ptr);
        /// <summary>
        /// Enable system32 directory redirect on 64bit system
        /// </summary>
        /// <param name="ptr"></param>
        /// <returns></returns>
        [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool Wow64RevertWow64FsRedirection(IntPtr ptr);
        /// <summary>
        /// look msdn: Bitmap.GetHbitmap()
        /// </summary>
        /// <param name="hObject"></param>
        /// <returns></returns>
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
        
        #region UserValidation
        //How to check if a given user is local admin or not:http://social.msdn.microsoft.com/Forums/en-US/netfxbcl/thread/e799c2f4-4ada-477b-8f98-05bafb0225f0
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LogonUser(String lpszUsername, String lpszDomain, String lpszPassword, int dwLogonType, int dwLogonProvider, ref IntPtr phToken);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private extern static bool CloseHandle(IntPtr handle);

        public static  bool IsUserAdmin(String user, String password, String domain)
        {
            IntPtr userToken = IntPtr.Zero;
            try
            {
                bool retVal = LogonUser(user, domain, password, 2, 0, ref userToken);

                if (!retVal)
                {
                    //throw new Exception("The user name and password does not exist in " + domain);
                    return false;
                }
                return IsUserTokenAdmin(userToken);
            }
            finally
            {
                CloseHandle(userToken);
            }
        }

        private static bool IsUserTokenAdmin(IntPtr userToken)
        {
            using (WindowsIdentity user = new WindowsIdentity(userToken))
            {
                WindowsPrincipal principal = new WindowsPrincipal(user);

                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
        #endregion        
    }

    /// <summary>
    /// This class encodes and decodes JSON strings.
    /// Spec. details, see http://www.json.org/
    /// 
    /// JSON uses Arrays and Objects. These correspond here to the datatypes ArrayList and Hashtable.
    /// All numbers are parsed to doubles.
    /// </summary>
    internal class JSON
    {
        public const int TOKEN_NONE = 0;
        public const int TOKEN_CURLY_OPEN = 1;
        public const int TOKEN_CURLY_CLOSE = 2;
        public const int TOKEN_SQUARED_OPEN = 3;
        public const int TOKEN_SQUARED_CLOSE = 4;
        public const int TOKEN_COLON = 5;
        public const int TOKEN_COMMA = 6;
        public const int TOKEN_STRING = 7;
        public const int TOKEN_NUMBER = 8;
        public const int TOKEN_TRUE = 9;
        public const int TOKEN_FALSE = 10;
        public const int TOKEN_NULL = 11;

        private const int BUILDER_CAPACITY = 2000;

        /// <summary>
        /// Parses the string json into a value
        /// </summary>
        /// <param name="json">A JSON string.</param>
        /// <returns>An ArrayList, a Hashtable, a double, a string, null, true, or false</returns>
        public static object JsonDecode(string json)
        {
            bool success = true;

            return JsonDecode(json, ref success);
        }

        /// <summary>
        /// Parses the string json into a value; and fills 'success' with the successfullness of the parse.
        /// </summary>
        /// <param name="json">A JSON string.</param>
        /// <param name="success">Successful parse?</param>
        /// <returns>An ArrayList, a Hashtable, a double, a string, null, true, or false</returns>
        public static object JsonDecode(string json, ref bool success)
        {
            success = true;
            if (json != null)
            {
                char[] charArray = json.ToCharArray();
                int index = 0;
                object value = ParseValue(charArray, ref index, ref success);
                return value;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Converts a Hashtable / ArrayList object into a JSON string
        /// </summary>
        /// <param name="json">A Hashtable / ArrayList</param>
        /// <returns>A JSON encoded string, or null if object 'json' is not serializable</returns>
        public static string JsonEncode(object json)
        {
            StringBuilder builder = new StringBuilder(BUILDER_CAPACITY);
            bool success = SerializeValue(json, builder);
            return (success ? builder.ToString() : null);
        }

        protected static Hashtable ParseObject(char[] json, ref int index, ref bool success)
        {
            Hashtable table = new Hashtable();
            int token;

            // {
            NextToken(json, ref index);

            bool done = false;
            while (!done)
            {
                token = LookAhead(json, index);
                if (token == JSON.TOKEN_NONE)
                {
                    success = false;
                    return null;
                }
                else if (token == JSON.TOKEN_COMMA)
                {
                    NextToken(json, ref index);
                }
                else if (token == JSON.TOKEN_CURLY_CLOSE)
                {
                    NextToken(json, ref index);
                    return table;
                }
                else
                {

                    // name
                    string name = ParseString(json, ref index, ref success);
                    if (!success)
                    {
                        success = false;
                        return null;
                    }

                    // :
                    token = NextToken(json, ref index);
                    if (token != JSON.TOKEN_COLON)
                    {
                        success = false;
                        return null;
                    }

                    // value
                    object value = ParseValue(json, ref index, ref success);
                    if (!success)
                    {
                        success = false;
                        return null;
                    }

                    table[name] = value;
                }
            }

            return table;
        }

        protected static ArrayList ParseArray(char[] json, ref int index, ref bool success)
        {
            ArrayList array = new ArrayList();

            // [
            NextToken(json, ref index);

            bool done = false;
            while (!done)
            {
                int token = LookAhead(json, index);
                if (token == JSON.TOKEN_NONE)
                {
                    success = false;
                    return null;
                }
                else if (token == JSON.TOKEN_COMMA)
                {
                    NextToken(json, ref index);
                }
                else if (token == JSON.TOKEN_SQUARED_CLOSE)
                {
                    NextToken(json, ref index);
                    break;
                }
                else
                {
                    object value = ParseValue(json, ref index, ref success);
                    if (!success)
                    {
                        return null;
                    }

                    array.Add(value);
                }
            }

            return array;
        }

        protected static object ParseValue(char[] json, ref int index, ref bool success)
        {
            switch (LookAhead(json, index))
            {
                case JSON.TOKEN_STRING:
                    return ParseString(json, ref index, ref success);
                case JSON.TOKEN_NUMBER:
                    return ParseNumber(json, ref index, ref success);
                case JSON.TOKEN_CURLY_OPEN:
                    return ParseObject(json, ref index, ref success);
                case JSON.TOKEN_SQUARED_OPEN:
                    return ParseArray(json, ref index, ref success);
                case JSON.TOKEN_TRUE:
                    NextToken(json, ref index);
                    return true;
                case JSON.TOKEN_FALSE:
                    NextToken(json, ref index);
                    return false;
                case JSON.TOKEN_NULL:
                    NextToken(json, ref index);
                    return null;
                case JSON.TOKEN_NONE:
                    break;
            }

            success = false;
            return null;
        }

        protected static string ParseString(char[] json, ref int index, ref bool success)
        {
            StringBuilder s = new StringBuilder(BUILDER_CAPACITY);
            char c;

            EatWhitespace(json, ref index);

            // "
            c = json[index++];

            bool complete = false;
            while (!complete)
            {

                if (index == json.Length)
                {
                    break;
                }

                c = json[index++];
                if (c == '"')
                {
                    complete = true;
                    break;
                }
                else if (c == '\\')
                {

                    if (index == json.Length)
                    {
                        break;
                    }
                    c = json[index++];
                    if (c == '"')
                    {
                        s.Append('"');
                    }
                    else if (c == '\\')
                    {
                        s.Append('\\');
                    }
                    else if (c == '/')
                    {
                        s.Append('/');
                    }
                    else if (c == 'b')
                    {
                        s.Append('\b');
                    }
                    else if (c == 'f')
                    {
                        s.Append('\f');
                    }
                    else if (c == 'n')
                    {
                        s.Append('\n');
                    }
                    else if (c == 'r')
                    {
                        s.Append('\r');
                    }
                    else if (c == 't')
                    {
                        s.Append('\t');
                    }
                    else if (c == 'u')
                    {
                        int remainingLength = json.Length - index;
                        if (remainingLength >= 4)
                        {
                            // parse the 32 bit hex into an integer codepoint
                            uint codePoint;
                            if (!(success = UInt32.TryParse(new string(json, index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out codePoint)))
                            {
                                return "";
                            }
                            // convert the integer codepoint to a unicode char and add to string
                            s.Append(Char.ConvertFromUtf32((int)codePoint));
                            // skip 4 chars
                            index += 4;
                        }
                        else
                        {
                            break;
                        }
                    }

                }
                else
                {
                    s.Append(c);
                }

            }

            if (!complete)
            {
                success = false;
                return null;
            }

            return s.ToString();
        }

        protected static double ParseNumber(char[] json, ref int index, ref bool success)
        {
            EatWhitespace(json, ref index);

            int lastIndex = GetLastIndexOfNumber(json, index);
            int charLength = (lastIndex - index) + 1;

            double number;
            success = Double.TryParse(new string(json, index, charLength), NumberStyles.Any, CultureInfo.InvariantCulture, out number);

            index = lastIndex + 1;
            return number;
        }

        protected static int GetLastIndexOfNumber(char[] json, int index)
        {
            int lastIndex;

            for (lastIndex = index; lastIndex < json.Length; lastIndex++)
            {
                if ("0123456789+-.eE".IndexOf(json[lastIndex]) == -1)
                {
                    break;
                }
            }
            return lastIndex - 1;
        }

        protected static void EatWhitespace(char[] json, ref int index)
        {
            for (; index < json.Length; index++)
            {
                if (" \t\n\r".IndexOf(json[index]) == -1)
                {
                    break;
                }
            }
        }

        protected static int LookAhead(char[] json, int index)
        {
            int saveIndex = index;
            return NextToken(json, ref saveIndex);
        }

        protected static int NextToken(char[] json, ref int index)
        {
            EatWhitespace(json, ref index);

            if (index == json.Length)
            {
                return JSON.TOKEN_NONE;
            }

            char c = json[index];
            index++;
            switch (c)
            {
                case '{':
                    return JSON.TOKEN_CURLY_OPEN;
                case '}':
                    return JSON.TOKEN_CURLY_CLOSE;
                case '[':
                    return JSON.TOKEN_SQUARED_OPEN;
                case ']':
                    return JSON.TOKEN_SQUARED_CLOSE;
                case ',':
                    return JSON.TOKEN_COMMA;
                case '"':
                    return JSON.TOKEN_STRING;
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                case '-':
                    return JSON.TOKEN_NUMBER;
                case ':':
                    return JSON.TOKEN_COLON;
            }
            index--;

            int remainingLength = json.Length - index;

            // false
            if (remainingLength >= 5)
            {
                if (json[index] == 'f' &&
                    json[index + 1] == 'a' &&
                    json[index + 2] == 'l' &&
                    json[index + 3] == 's' &&
                    json[index + 4] == 'e')
                {
                    index += 5;
                    return JSON.TOKEN_FALSE;
                }
            }

            // true
            if (remainingLength >= 4)
            {
                if (json[index] == 't' &&
                    json[index + 1] == 'r' &&
                    json[index + 2] == 'u' &&
                    json[index + 3] == 'e')
                {
                    index += 4;
                    return JSON.TOKEN_TRUE;
                }
            }

            // null
            if (remainingLength >= 4)
            {
                if (json[index] == 'n' &&
                    json[index + 1] == 'u' &&
                    json[index + 2] == 'l' &&
                    json[index + 3] == 'l')
                {
                    index += 4;
                    return JSON.TOKEN_NULL;
                }
            }

            return JSON.TOKEN_NONE;
        }

        protected static bool SerializeValue(object value, StringBuilder builder)
        {
            bool success = true;

            if (value is string)
            {
                success = SerializeString((string)value, builder);
            }
            else if (value is Hashtable)
            {
                success = SerializeObject((Hashtable)value, builder);
            }
            else if (value is ArrayList)
            {
                success = SerializeArray((ArrayList)value, builder);
            }
            else if (IsNumeric(value))
            {
                success = SerializeNumber(Convert.ToDouble(value), builder);
            }
            else if ((value is Boolean) && ((Boolean)value == true))
            {
                builder.Append("true");
            }
            else if ((value is Boolean) && ((Boolean)value == false))
            {
                builder.Append("false");
            }
            else if (value == null)
            {
                builder.Append("null");
            }
            else
            {
                success = false;
            }
            return success;
        }

        protected static bool SerializeObject(Hashtable anObject, StringBuilder builder)
        {
            builder.Append("{");

            IDictionaryEnumerator e = anObject.GetEnumerator();
            bool first = true;
            while (e.MoveNext())
            {
                string key = e.Key.ToString();
                object value = e.Value;

                if (!first)
                {
                    builder.Append(", ");
                }

                SerializeString(key, builder);
                builder.Append(":");
                if (!SerializeValue(value, builder))
                {
                    return false;
                }

                first = false;
            }

            builder.Append("}");
            return true;
        }

        protected static bool SerializeArray(ArrayList anArray, StringBuilder builder)
        {
            builder.Append("[");

            bool first = true;
            for (int i = 0; i < anArray.Count; i++)
            {
                object value = anArray[i];

                if (!first)
                {
                    builder.Append(", ");
                }

                if (!SerializeValue(value, builder))
                {
                    return false;
                }

                first = false;
            }

            builder.Append("]");
            return true;
        }

        protected static bool SerializeString(string aString, StringBuilder builder)
        {
            builder.Append("\"");

            char[] charArray = aString.ToCharArray();
            for (int i = 0; i < charArray.Length; i++)
            {
                char c = charArray[i];
                if (c == '"')
                {
                    builder.Append("\\\"");
                }
                else if (c == '\\')
                {
                    builder.Append("\\\\");
                }
                else if (c == '\b')
                {
                    builder.Append("\\b");
                }
                else if (c == '\f')
                {
                    builder.Append("\\f");
                }
                else if (c == '\n')
                {
                    builder.Append("\\n");
                }
                else if (c == '\r')
                {
                    builder.Append("\\r");
                }
                else if (c == '\t')
                {
                    builder.Append("\\t");
                }
                else
                {
                    int codepoint = Convert.ToInt32(c);
                    if ((codepoint >= 32) && (codepoint <= 126))
                    {
                        builder.Append(c);
                    }
                    else
                    {
                        builder.Append("\\u" + Convert.ToString(codepoint, 16).PadLeft(4, '0'));
                    }
                }
            }

            builder.Append("\"");
            return true;
        }

        protected static bool SerializeNumber(double number, StringBuilder builder)
        {
            builder.Append(Convert.ToString(number, CultureInfo.InvariantCulture));
            return true;
        }

        /// <summary>
        /// Determines if a given object is numeric in any way
        /// (can be integer, double, null, etc). 
        /// 
        /// Thanks to mtighe for pointing out Double.TryParse to me.
        /// </summary>
        protected static bool IsNumeric(object o)
        {
            double result;

            return (o == null) ? false : Double.TryParse(o.ToString(), out result);
        }
    }

    public class MemCache : INotifyPropertyChanged
    {
        //dlls using: 
        //Memcached.ClientLibrary.dll(client):Commons.dll,ICSharpCode.SharpZipLib.dll,log4net.dll
        //memcached.exe(server):msvcr71.dll(when in system32 folder, take no effects)
        //help:"memcached -h"
        // why not velocity: 
        // http://stackoverflow.com/questions/397824/ms-velocity-vs-memcached-for-windows
        // Velocity versus Memcached:http://blog.moxen.us/2010/05/26/velocity-versus-memcached/
        // .NET中使用Memcached的相关资源整理： http://www.cnblogs.com/dudu/archive/2009/07/19/1526407.html
        // memcached+net缓存：http://www.cnblogs.com/wyxy2005/archive/2010/08/23/1806785.html      
        private bool _isActived;//MemCache switch to indicate if this memory cache ability is currently enabled.
        public bool IsActived
        {
            get { return _isActived; }
            set
            {
                _isActived = value;
                NotifyPropertyChanged("IsActived");
                if (MemCache.IsActivedChanged != null)
                    MemCache.IsActivedChanged(this, new IsActivedChangedEventArgs(_isActived));
            }
        }
        public MemcachedClient MC { get; set; }
        public delegate void IsActivedChangedEventHandler(object sender, IsActivedChangedEventArgs e);
        public static event IsActivedChangedEventHandler IsActivedChanged;//raised when IsActived property changed, used for app to change UI. In order to add event listener before ServiceManager.Memcache is initialized(so app UI could be changed when enable memory cache through REST admin API), this must be static.
        public class IsActivedChangedEventArgs : EventArgs
        {
            public bool NewValue { get; set; }
            public IsActivedChangedEventArgs(bool b)
            {
                NewValue = b;
            }
        }

        private SockIOPool _pool;
        private Process _cmdMemcachedProcess;//used for holding the Memcached process, if this process has been closed, the process in taskmanager will be lost.
        private string _memcachedInWinFolder;

        //max memory to use for memcached items in megabytes, default is 64 MB
        public MemCache(int memorySize)
        {
            //the goal is copy memcached.exe to %windir% folder, then from there, install memcached windows service so that the executable path of the service is %windir%\memcached.exe. Thus, PBS folder could be moved to anywhere else.
            string strCmdResult;
            try
            {
                //check if memcached.exe,msvcr71.dll exists in PBS folder
                if (!File.Exists("memcached.exe"))
                    throw new Exception("memcached.exe doesn't exists!");
                if (!File.Exists("msvcr71.dll"))
                    throw new Exception("msvcr71.dll doesn't exists!");
                _memcachedInWinFolder = Environment.ExpandEnvironmentVariables("%SystemRoot%") + "\\memcached.exe";
                //check if memcached.exe,pthreadGC2.dll exists in windows path
                if (!File.Exists(_memcachedInWinFolder))
                    File.Copy("memcached.exe", _memcachedInWinFolder, true);
                if (!File.Exists(Environment.ExpandEnvironmentVariables("%SystemRoot%") + "\\msvcr71.dll"))
                    File.Copy("msvcr71.dll", Environment.ExpandEnvironmentVariables("%SystemRoot%") + "\\msvcr71.dll", true);
                //if windows service exists, check if the exe path of the service is in windows directory
                if (Utility.IsWindowsServiceExisted("memcached Server"))
                {
                    Utility.Cmd("sc qc \"memcached Server\"", false, out strCmdResult);
                    if (!strCmdResult.Contains("\\Windows\\"))
                        Utility.Cmd("sc delete \"memcached Server\"", false, out strCmdResult);//try to uninstall windows service
                }
                //check if windows service exists
                if (!Utility.IsWindowsServiceExisted("memcached Server"))
                {
                    Utility.Cmd(_memcachedInWinFolder + " -d install", false, out strCmdResult);//install memcached windows service
                    //google:使用cmd命令手动、自动启动和禁用服务
                    Utility.Cmd("sc config \"memcached Server\" start= demand", false, out strCmdResult);//set to 手动启动
                }
                Utility.Cmd(_memcachedInWinFolder + " -d stop", false, out strCmdResult);
                _cmdMemcachedProcess = Utility.Cmd(_memcachedInWinFolder + " -m " + memorySize + " -d start", true, out strCmdResult);
                string[] serverlist = { "127.0.0.1:11211" };

                // initialize the pool for memcache servers
                _pool = SockIOPool.GetInstance();
                _pool.SetServers(serverlist);

                _pool.InitConnections = 3;
                _pool.MinConnections = 3;
                _pool.MaxConnections = 5;

                _pool.SocketConnectTimeout = 1000;
                _pool.SocketTimeout = 3000;

                _pool.MaintenanceSleep = 30;
                _pool.Failover = true;

                _pool.Nagle = false;
                _pool.Initialize();
                if (_pool.GetConnection(serverlist[0]) == null)
                {
                    _pool.Shutdown();
                    Utility.Cmd("sc delete \"memcached Server\"", false, out strCmdResult);//try to uninstall windows service
                    throw new Exception("Can not managed to run 'memcached -d start' on your machine.");
                }

                MC = new MemcachedClient()
                {
                    EnableCompression = false
                };
                //try to cache one object in memory to ensure memcached ability can be used
                if (!MC.Set("test", DateTime.Now.ToString()))
                {
                    throw new Exception("Can't managed to set key-value in memched!");
                }
                IsActived = true;
            }
            catch (Exception e)
            {
                IsActived = false;
                Shutdown();//important
                //copy error:"Access to the path 'C:\Windows\system32\memcached.exe' is denied."
                if (e.Message.Contains("Access") && e.Message.Contains("denied"))
                    throw new Exception("Copy memcached.exe to '%windir%' failed.\r\nPlease reopen PortableBasemapServer by right clicking and select 'Run as Administrator'.");
                throw new Exception(e.Message);
            }
        }

        //make memory cache of a specified service unavailable(not really delete them)
        public string InvalidateServiceMemcache(int port,string servicename)
        {
            /// memcached批量删除方案探讨:http://it.dianping.com/memcached_item_batch_del.htm Key flag 方案
            if (!ServiceManager.PortEntities.ContainsKey(port))
                return "The specified port does not exist in PBS.";
            else if (!ServiceManager.PortEntities[port].ServiceProvider.Services.ContainsKey(servicename))
                return "The specified service does not exist on port " + port + ".";
            else
            {
                StringBuilder sb=new StringBuilder();
                sb.Append(DateTime.Now.Year);
                sb.Append(DateTime.Now.Month);
                sb.Append(DateTime.Now.Day);
                sb.Append(DateTime.Now.Hour);
                sb.Append(DateTime.Now.Minute);
                sb.Append(DateTime.Now.Second);
                ServiceManager.PortEntities[port].ServiceProvider.Services[servicename].MemcachedValidKey = sb.ToString();
                return string.Empty;
            }
        }

        public void FlushAll()
        {
            MC.FlushAll();//just make all the items in memcached unavailable by expiring them
        }

        public void Shutdown()
        {
            if (_pool != null)
                _pool.Shutdown();//very important. otherwise, the app will not be termitated after closing the window
            _pool = null;
            //after leave this function, _cmdMemcachedProcess will be automatic exited by app lifecycle
            if (_cmdMemcachedProcess != null)
            {
                _cmdMemcachedProcess.StandardInput.WriteLine(_memcachedInWinFolder+" -d stop");
                System.Threading.Thread.Sleep(250);
                _cmdMemcachedProcess.StandardInput.WriteLine("exit");
                System.Threading.Thread.Sleep(250);
                _cmdMemcachedProcess.Close();
            }
            _cmdMemcachedProcess = null;
            IsActived = false;
            if (MC != null)
            { MC.FlushAll(); MC = null; }   
        }

        ~MemCache()
        {
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion
    }

    [ValueConversion(typeof(Enum), typeof(String))]
    public class EnumConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Enum visualStyle = (Enum)value;
            return visualStyle.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    /// <summary>
    /// avoid ObservableCollection to throw "This type of CollectionView does not support changes to its SourceCollection from a thread different from the Dispatcher thread."
    /// ref:Where do I get a thread-safe CollectionView?
    /// http://stackoverflow.com/questions/2137769/where-do-i-get-a-thread-safe-collectionview
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MTObservableCollection<T> : ObservableCollection<T>
    {
        public override event NotifyCollectionChangedEventHandler CollectionChanged;
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            var eh = CollectionChanged;
            if (eh != null)
            {
                Dispatcher dispatcher = (from NotifyCollectionChangedEventHandler nh in eh.GetInvocationList()
                                         let dpo = nh.Target as DispatcherObject
                                         where dpo != null
                                         select dpo.Dispatcher).FirstOrDefault();

                if (dispatcher != null && dispatcher.CheckAccess() == false)
                {
                    dispatcher.Invoke(DispatcherPriority.DataBind, (Action)(() => OnCollectionChanged(e)));
                }
                else
                {
                    foreach (NotifyCollectionChangedEventHandler nh in eh.GetInvocationList())
                        nh.Invoke(this, e);
                }
            }
        }
    }

    #region geometry
    [Serializable]
    public abstract class Geometry
    {
        public abstract Envelope Extent { get; }
    }
    [Serializable]
    public class Envelope : Geometry
    {
        private Envelope extent;
        public double XMin { get; set; }
        public double YMin { get; set; }
        public double XMax { get; set; }
        public double YMax { get; set; }
        public Point UpperLeft
        {
            get
            {
                return new Point(XMin, YMax);
            }
        }
        public Point LowerLeft
        {
            get
            {
                return new Point(XMin, YMin);
            }
        }
        public Point UpperRight
        {
            get
            {
                return new Point(XMax  , YMax);
            }
        }
        public Point LowerRight
        {
            get
            {
                return new Point(XMax   , YMin);
            }
        }
        public Envelope(double xmin, double ymin, double xmax, double ymax)
        {
            XMin = xmin;
            YMin = ymin;
            XMax = xmax;
            YMax = ymax;
        }
        public Envelope()
        {

        }

        public Envelope Union(Envelope newExtent)
        {
            return new Envelope(Math.Min(this.XMin, newExtent.XMin),
                Math.Min(this.YMin, newExtent.YMin),
                Math.Max(this.XMax, newExtent.XMax),
                Math.Max(this.YMax, newExtent.YMax));
        }
        public bool ContainsPoint(Point p)
        {
            return p.X > XMin && p.X < XMax && p.Y > YMin && p.Y < YMax;
        }
        public override Envelope Extent
        {
            get
            {
                if (extent == null)
                    extent = this;
                return extent;
            }
        }

        public Polygon ToPolygon()
        {
            PointCollection pc = new PointCollection();
            pc.Add(this.LowerLeft);
            pc.Add(this.LowerRight);
            pc.Add(this.UpperRight);
            pc.Add(this.UpperLeft);
            pc.Add(this.LowerLeft);
            Polygon p = new Polygon();
            p.Rings.Add(pc);
            return p;
        }
    }
    [Serializable]
    public class Point : Geometry
    {
        private Envelope extent;
        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }
        public double X { get; set; }
        public double Y { get; set; }
        public override Envelope Extent{
            get
            {
                if(extent==null)
                    extent=new Envelope(this.X, this.Y, this.X, this.Y);
                return extent;
            }
    }
    }
    [Serializable]
    public class PointCollection : ObservableCollection<Point>
    {
        public Envelope Extent { get {
            Envelope extent = null;
            foreach (Point point in this)
            {
                if (point != null)
                {
                    if (extent == null)
                    {
                        extent = point.Extent;
                    }
                    else
                    {
                        extent = extent.Union(point.Extent);
                    }
                }
            }
            return extent;
        } }
    }
    [Serializable]
    public class Polygon : Geometry
    {
        private Envelope extent;
        public ObservableCollection<PointCollection> Rings { get; set; }
        public Polygon()
        {
            Rings = new ObservableCollection<PointCollection>();        
        }
        public override Envelope Extent
        {
            get
            {
                if (this.extent == null)
                {
                    foreach (PointCollection points in this.Rings)
                    {
                        if (this.extent == null)
                        {
                            this.extent = points.Extent;
                        }
                        else
                        {
                            this.extent = this.Extent.Union(points.Extent);
                        }
                    }
                }
                return this.extent;
            }
        }
        /// <summary>
        /// Determining if a point lies on the interior of this polygon.
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public bool ContainsPoint(Point p)
        {
            Polygon polygon = this;
            bool result = false;
            int counter = 0;
            int i;
            double xinters;
            Point p1, p2;
            foreach (PointCollection pc in polygon.Rings)
            {
                int N = pc.Count;

                p1 = pc[0];
                for (i = 1; i <= N; i++)
                {
                    p2 = pc[i % N];
                    if (p.Y > Math.Min(p1.Y, p2.Y))
                    {
                        if (p.Y <= Math.Max(p1.Y, p2.Y))
                        {
                            if (p.X <= Math.Max(p1.X, p2.X))
                            {
                                if (p1.Y != p2.Y)
                                {
                                    xinters = (p.Y - p1.Y) * (p2.X - p1.X) / (p2.Y - p1.Y) + p1.X;
                                    if (p1.X == p2.X || p.X <= xinters)
                                        counter++;
                                }
                            }
                        }
                    }
                    p1 = p2;
                }

                if (counter % 2 == 0)
                    result = false;
                else
                {
                    result = true;
                    break;
                }
            }
            return result;
        }

        /// <summary>
        /// /// Determines if the two polygons supplied intersect each other, by checking if either polygon has points which are contained in the other.(It doesn't detect body-only intersections, but is sufficient in most cases.)
        /// http://wikicode.wikidot.com/check-for-polygon-polygon-intersection
        /// </summary>
        /// <param name="poly"></param>
        /// <returns></returns>
        public bool IsIntersectsWithPolygon(Polygon poly)
        {
            foreach (PointCollection ring in this.Rings)
            {
                for (int i = 0; i < ring.Count; i++)
                {
                    if (poly.ContainsPoint(ring[i]))
                        return true;
                }
            }
            foreach (PointCollection ring in poly.Rings)
            {
                for (int i = 0; i < ring.Count; i++)
                {
                    if (this.ContainsPoint(ring[i]))
                        return true;
                }
            }            
            return false;
        }
    }

    
    #endregion
}
