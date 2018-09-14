//****************************************
//Copyright@diligentpig, https://geopbs.codeplex.com
//Please using source code under LGPL license.
//****************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SQLite;
using PBS.Service;
using System.Data;
using PBS.DataSource;
using System.Collections.ObjectModel;
using PBS.Util;
using System.Windows;

namespace PBS
{
    public class ConfigManager
    {
        #region PBS app settings
        /// <summary>
        /// used for bingmaps imagery service
        /// </summary>
        public static string App_BingMapsAppKey { get; set; }
        /// <summary>
        /// indicate if onlinemaps datasource should be cached on local disk.
        /// if true, when onlinemaps tile downloaded, it will be written to local .cache file, and GetTile() method will try to retrieve tile from local cache first.
        /// </summary>
        public static bool App_AllowFileCacheOfOnlineMaps { get; set; }
        public static bool App_AllowFileCacheOfRasterImage { get; set; }
        /// <summary>
        /// the path where local cache file(.cache) of onlinemaps and rasterimage datasource should be saved.
        /// </summary>
        public static string App_FileCachePath { get; set; }
        #endregion

        public string CONST_strConfigFileName = AppDomain.CurrentDomain.BaseDirectory+"Config.db";
        public const string CONST_strLastConfigName = "___AUTOSAVED___";
        public const string CONST_strTableNameConfigurations = "configurations";
        public const string CONST_strTableNameServices = "services";
        public const string CONST_strTableNameAgsDynamicMapServiceParams = "agsDynamicMapServiceParams";
        public const string CONST_strTableNameDownloadProfile="downloadProfiles";
        private ObservableCollection<Configuration> _configurations = new ObservableCollection<Configuration>();
        /// <summary>
        /// used for ui binding
        /// </summary>
        public ObservableCollection<Configuration> Configurations
        {
            get
            {
                _configurations.Clear();
                using (SQLiteCommand cmd = new SQLiteCommand(@"SELECT * FROM " + CONST_strTableNameConfigurations, _conn))
                {
                    SQLiteDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        _configurations.Add(new Configuration((string)reader["name"], int.Parse(reader["servicecount"].ToString()), (DateTime)reader["created"]));
                    }
                    reader.Close();
                }                
                //hide auto saved configuration
                if (_instance.IsConfigurationExists(ConfigManager.CONST_strLastConfigName))
                    _configurations.Remove(_configurations.Where(config => config.Name == ConfigManager.CONST_strLastConfigName).ToList()[0]);
                return _configurations;
            }
        }
        private SQLiteConnection _conn;
        //avoid to duplicate more than one SQLiteConnection
        private static ConfigManager _instance;
        public static ConfigManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ConfigManager();
                return ConfigManager._instance;
            }
        }

        private ConfigManager()
        {
            ValidateConfigFile();
        }

        ~ConfigManager()
        {            
            if (_conn != null)
            {
                _conn.Close();
                _conn.Dispose();
                _conn = null;
            }
        }

        /// <summary>
        /// check if the config.db file is valid. if not, create new one and backup old one.
        /// </summary>
        /// <returns>if not string.empty, the return message need to show to user</returns>
        public string ValidateConfigFile()
        {
            if (!File.Exists(CONST_strConfigFileName))
            {
                CreateConfigFile();
                return CONST_strConfigFileName+" didn't exist. New "+CONST_strConfigFileName+" created.";
            }
            try
            {
                if (_conn == null)
                {
                    _conn = new SQLiteConnection("Data source = " + CONST_strConfigFileName);
                    _conn.Open();
                }
                //validate config.db file
                if (!IsTableExists(CONST_strTableNameConfigurations) ||
                    !IsTableExists(CONST_strTableNameServices) ||
                    !IsTableExists(CONST_strTableNameAgsDynamicMapServiceParams) ||
                !IsTableExists(CONST_strTableNameDownloadProfile)) 
                {
                    //release the file first
                    _conn.Close();
                    _conn = null;
                    if (File.Exists(CONST_strConfigFileName + ".bak"))
                        File.Delete(CONST_strConfigFileName + ".bak");
                    //rename
                    File.Move(CONST_strConfigFileName, CONST_strConfigFileName+".bak");
                    CreateConfigFile();
                    return CONST_strConfigFileName + " is not valid. New "+CONST_strConfigFileName+" has been created.";
                }
                return null;
            }
            catch (Exception e)
            {
                throw new Exception("Validating " + CONST_strConfigFileName + " error.\r\n" + e.Message);
            }
        }
        
        public string LoadConfigurationAndStartServices(string name)
        {
            ServiceManager.DeleteAllServiceHost();
            string result = string.Empty;
            using (SQLiteCommand cmd = new SQLiteCommand(@"SELECT * FROM " + CONST_strTableNameServices + " WHERE configuration='" + name + "'", _conn))
            {
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if ((bool)reader["using3857"])
                    {
                        string s = ServiceManager.CreateService((string)reader["name"], int.Parse(reader["port"].ToString()), reader["type"].ToString(), (string)reader["datasourcepath"], (bool)reader["allowmemcache"], (bool)reader["disableclientcache"], (bool)reader["displaynodatatile"], (VisualStyle)Enum.Parse(typeof(VisualStyle), (string)reader["visualstyle"], true));
                        if (s != string.Empty)
                            result += s + "\r\n";
                    }
                    else
                    {
                        string s = ServiceManager.CreateService((string)reader["name"], int.Parse(reader["port"].ToString()), reader["type"].ToString(), (string)reader["datasourcepath"], (bool)reader["allowmemcache"], (bool)reader["disableclientcache"], (bool)reader["displaynodatatile"], (VisualStyle)Enum.Parse(typeof(VisualStyle), (string)reader["visualstyle"], true), (string)reader["tilingschemepath"]);     
                        if (s != string.Empty)
                            result += s + "\r\n";
                    }
                    
                    //if is ArcGISDynamicMapService, read params additionally.
                    if (reader["type"].ToString() == DataSource.DataSourceTypePredefined.ArcGISDynamicMapService.ToString()&&(ServiceManager.GetService(int.Parse(reader["port"].ToString()), (string)reader["name"])!=null))
                    {
                        using (SQLiteCommand cmd1 = new SQLiteCommand(@"SELECT * FROM " + CONST_strTableNameAgsDynamicMapServiceParams + " WHERE configuration='" + name + "' AND name='"+(string)reader["name"]+"'", _conn))
                        {
                            SQLiteDataReader reader1 = cmd1.ExecuteReader();
                            if (reader1.Read())
                            {
                                (ServiceManager.GetService(int.Parse(reader["port"].ToString()), (string)reader["name"]).DataSource as DataSourceArcGISDynamicMapService).exportParam_layers = reader1["layers"].ToString();
                                (ServiceManager.GetService(int.Parse(reader["port"].ToString()), (string)reader["name"]).DataSource as DataSourceArcGISDynamicMapService).exportParam_layerDefs = reader1["layerDefs"].ToString();
                                (ServiceManager.GetService(int.Parse(reader["port"].ToString()), (string)reader["name"]).DataSource as DataSourceArcGISDynamicMapService).exportParam_time = reader1["time"].ToString();
                                (ServiceManager.GetService(int.Parse(reader["port"].ToString()), (string)reader["name"]).DataSource as DataSourceArcGISDynamicMapService).exportParam_layerTimeOptions = reader1["layerTimeOptions"].ToString();
                            }
                        }
                    }
                }
                reader.Close();
            }
            return result;
        }

        public string SaveConfigurationWithOverwrite(string name)
        {
            try
            {
                using (SQLiteTransaction transaction = _conn.BeginTransaction())
                {
                    using (SQLiteCommand cmd = new SQLiteCommand(_conn))
                    {
                        //if configuration name already exists, delete configuration and associated services first
                        //delete services
                        cmd.CommandText = "DELETE FROM " + CONST_strTableNameServices + " WHERE configuration='" + name + "'";
                        cmd.ExecuteNonQuery();
                        //delete configuration
                        cmd.CommandText = "DELETE FROM "+CONST_strTableNameConfigurations+" WHERE name='" + name + "'";
                        cmd.ExecuteNonQuery();
                        //delete ArcGISDynamicMapService params
                        cmd.CommandText = "DELETE FROM " + CONST_strTableNameAgsDynamicMapServiceParams + " WHERE configuration='" + name + "'";
                        cmd.ExecuteNonQuery();
                        //save configuration
                        cmd.CommandText = "INSERT INTO " + CONST_strTableNameConfigurations + " VALUES (@name,@servicecount,@created)";
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.Parameters.AddWithValue("@servicecount", ServiceManager.Services.Count);
                        cmd.Parameters.AddWithValue("@created", DateTime.Now);
                        cmd.ExecuteNonQuery();
                        foreach (PBSService service in ServiceManager.Services)
                        {
                            //save services
                            cmd.CommandText = "INSERT INTO " + CONST_strTableNameServices + " VALUES (@configuration,@name,@port,@type,@tilingschemepath,@using3857,@datasourcepath,@allowmemcache,@disableclientcache,@displaynodatatile,@visualstyle)";
                            cmd.Parameters.AddWithValue("@configuration", name);
                            cmd.Parameters.AddWithValue("@name", service.ServiceName);
                            cmd.Parameters.AddWithValue("@port", service.Port);
                            cmd.Parameters.AddWithValue("@type", service.DataSource.Type.ToString());
                            cmd.Parameters.AddWithValue("@tilingschemepath", service.DataSource.TilingScheme.Path);
                            cmd.Parameters.AddWithValue("@using3857", service.DataSource.TilingScheme.WKID == 102100||service.DataSource.TilingScheme.WKID==3857 ? true : false);
                            cmd.Parameters.AddWithValue("@datasourcepath", service.DataSource.Path);
                            cmd.Parameters.AddWithValue("@allowmemcache", service.AllowMemCache);
                            cmd.Parameters.AddWithValue("@disableclientcache", service.DisableClientCache);
                            cmd.Parameters.AddWithValue("@displaynodatatile", service.DisplayNoDataTile);
                            cmd.Parameters.AddWithValue("@visualstyle", service.Style.ToString());
                            cmd.ExecuteNonQuery();
                            //if is ArcGISDynamicMapService, save additional params.
                            if (service.DataSource.Type == DataSource.DataSourceTypePredefined.ArcGISDynamicMapService.ToString())
                            {
                                cmd.CommandText = "INSERT INTO " + CONST_strTableNameAgsDynamicMapServiceParams + " VALUES (@configuration,@name,@layers,@layerDefs,@time,@layerTimeOptions)";
                                cmd.Parameters.Clear();
                                cmd.Parameters.AddWithValue("@configuration", name);
                                cmd.Parameters.AddWithValue("@name", service.ServiceName);
                                cmd.Parameters.AddWithValue("@layers",(service.DataSource as DataSourceArcGISDynamicMapService).exportParam_layers);
                                cmd.Parameters.AddWithValue("@layerDefs", (service.DataSource as DataSourceArcGISDynamicMapService).exportParam_layerDefs);
                                cmd.Parameters.AddWithValue("@time", (service.DataSource as DataSourceArcGISDynamicMapService).exportParam_time);
                                cmd.Parameters.AddWithValue("@layerTimeOptions", (service.DataSource as DataSourceArcGISDynamicMapService).exportParam_layerTimeOptions);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                    transaction.Commit();
                }
                _configurations.Add(new Configuration(name, ServiceManager.Services.Count, DateTime.Now));
                return string.Empty;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        public string DeleteConfiguration(string name)
        {
            try
            {
                using (SQLiteTransaction transaction = _conn.BeginTransaction())
                {
                    using (SQLiteCommand cmd = new SQLiteCommand(_conn))
                    {
                        //delete services
                        cmd.CommandText = "DELETE FROM " + CONST_strTableNameServices + " WHERE configuration='" + name + "'";
                        cmd.ExecuteNonQuery();
                        //delete configuration
                        cmd.CommandText = "DELETE FROM " + CONST_strTableNameConfigurations + " WHERE name='" + name + "'";
                        cmd.ExecuteNonQuery();
                        //delete ArcGISDynamicMapService params
                        cmd.CommandText = "DELETE FROM " + CONST_strTableNameAgsDynamicMapServiceParams + " WHERE name='" + name + "'";
                        cmd.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }
                _configurations.Remove(_configurations.Where(config => config.Name == name).ToList()[0]);
                return string.Empty;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        #region download profile
        /// <summary>
        /// save download extent and levels as a profile
        /// </summary>
        /// <returns>any error message</returns>
        public string SaveDownloadProfileWithOverwrite(DownloadProfile profile)
        {
            try
            {
                using (SQLiteCommand cmd = new SQLiteCommand(_conn))
                {
                    cmd.CommandText = "INSERT OR REPLACE INTO " + CONST_strTableNameDownloadProfile + " (name,levels,extent,polygon) VALUES (@name,@levels,@extent,@polygon)";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@name", profile.Name);
                    string levels = string.Empty;
                    foreach (int i in profile.Levels)
                    {
                        levels += i.ToString() + ",";
                    }
                    levels = levels.Remove(levels.Length - 1, 1);
                    cmd.Parameters.AddWithValue("@levels", levels);
                    cmd.Parameters.AddWithValue("@extent", string.Format("{0},{1},{2},{3}", profile.Envelope.XMin, profile.Envelope.YMin, profile.Envelope.XMax, profile.Envelope.YMax));
                    cmd.Parameters.AddWithValue("@polygon", Util.Utility.SerializeObject(profile.Polygon));
                    cmd.ExecuteNonQuery();
                }
                return string.Empty;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <returns>null if fail</returns>
        public DownloadProfile LoadDownloadProfile(string name)
        {
            try
            {
                using (SQLiteCommand cmd = new SQLiteCommand(_conn))
            {
                cmd.CommandText = "SELECT * FROM " + CONST_strTableNameDownloadProfile + " WHERE name=" + "'" + name + "'";
                SQLiteDataReader dr = cmd.ExecuteReader();
                if (!dr.HasRows)
                    return null;
                List<int> levels = new List<int>();
                foreach (string level in dr[1].ToString().Split(new char[] { ',' }))
                {
                    levels.Add(int.Parse(level));
                }
                Envelope env=new Envelope(double.Parse(dr[2].ToString().Split(new char[] { ',' })[0]), double.Parse(dr[2].ToString().Split(new char[] { ',' })[1]), double.Parse(dr[2].ToString().Split(new char[] { ',' })[2]), double.Parse(dr[2].ToString().Split(new char[] { ',' })[3]));
                Polygon polygon = dr[3] is System.DBNull ? null : Utility.DeserializeObject((byte[])dr[3]) as Polygon;
                dr.Close();
                return new DownloadProfile(name, levels.ToArray(), env, polygon);
            }
            }
            catch (Exception e)
            {
                throw new Exception("Load download profile failed!\r\n" + e.Message);
            }
        }

        public string DeleteDownloadProfile(string name)
        {
            try
            {
                using (SQLiteCommand cmd = new SQLiteCommand(_conn))
                {
                    cmd.CommandText = "DELETE FROM " + CONST_strTableNameDownloadProfile + " WHERE name='" + name + "'";
                    cmd.ExecuteNonQuery();
                }
                return string.Empty;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        public string[] GetAllDownloadProfileNames()
        {
            using (SQLiteCommand cmd = new SQLiteCommand(_conn))
            {
                cmd.CommandText = "SELECT name FROM " + CONST_strTableNameDownloadProfile;
                SQLiteDataReader dr = cmd.ExecuteReader();
                List<string> names = new List<string>();
                while (dr.Read())
                {
                    names.Add(dr[0].ToString());
                }
                dr.Close();
                return names.ToArray();
            }
        }
        #endregion

        private void CreateConfigFile()
        {
            if (_conn != null)
            {
                _conn.Close();
                _conn = null;
            }
            //create new sqlite database
            SQLiteConnection.CreateFile(CONST_strConfigFileName);
            _conn = new SQLiteConnection("Data source = " + CONST_strConfigFileName);
            _conn.Open();
            //create tables
            using (SQLiteTransaction transaction = _conn.BeginTransaction())
            {
                using (SQLiteCommand cmd = _conn.CreateCommand())
                {
                    //config names table
                    cmd.CommandText = @"CREATE TABLE """ + CONST_strTableNameConfigurations + @""" (""name"" TEXT PRIMARY KEY  NOT NULL  UNIQUE , ""servicecount"" INTEGER NOT NULL , ""created"" DATETIME)";
                    cmd.ExecuteNonQuery();
                    //services table
                    cmd.CommandText = @"CREATE TABLE """ + CONST_strTableNameServices + @""" (""configuration"" TEXT NOT NULL , ""name"" TEXT NOT NULL , ""port"" INTEGER NOT NULL , ""type"" TEXT NOT NULL , ""tilingschemepath"" TEXT, ""using3857"" BOOL, ""datasourcepath"" TEXT, ""allowmemcache"" BOOL,""disableclientcache"" BOOL, ""displaynodatatile"" BOOL, ""visualstyle"" TEXT)";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = @"CREATE INDEX services_index on "+CONST_strTableNameServices+@" (configuration,name)";
                    cmd.ExecuteNonQuery();
                    //arcgisdynamicmapservice params table
                    cmd.CommandText = @"CREATE TABLE """ + CONST_strTableNameAgsDynamicMapServiceParams + @""" (""configuration"" TEXT NOT NULL , ""name"" TEXT NOT NULL , ""layers"" TEXT , ""layerDefs"" TEXT , ""time"" TEXT, ""layerTimeOptions"" TEXT)";
                    cmd.ExecuteNonQuery();
                    //onlinemaps download extent and levels table
                    cmd.CommandText = @"CREATE TABLE """+CONST_strTableNameDownloadProfile+@""" (""name"" TEXT PRIMARY KEY  NOT NULL  UNIQUE , ""levels"" TEXT NOT NULL , ""extent"" TEXT NOT NULL ,""polygon"" BLOB)";
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }
        }

        public bool IsConfigurationExists(string name)
        {
            long i = 0;
            using (SQLiteCommand cmd = new SQLiteCommand("SELECT COUNT(*) FROM " + CONST_strTableNameConfigurations + " WHERE name='" + name + "'", _conn))
            {
                i = (long)cmd.ExecuteScalar();
            }
            return i != 0 ? true : false;
        }

        private bool IsTableExists(string name)
        {
            using (SQLiteCommand cmd = new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='" + name + "'", _conn))
            {
                long i = (long)cmd.ExecuteScalar();
                if (i == 0)
                    return false;
                else
                    return true;
            }
        }
    }

    /// <summary>
    /// services are saved in sqlite config file as a Configuration.
    /// </summary>
    public class Configuration
    {
        public string Name { get; set; }
        public int ServiceCount { get; set; }
        public DateTime CreatedTime { get; set; }
        public Configuration(string name, int servicecount, DateTime created)
        {
            Name = name;
            ServiceCount = servicecount;
            CreatedTime = created;
        }
    }
    /// <summary>
    /// If polygon==null, means download extent is rectangle and drawed by using mouse by user; if polygon!=null, means download extent is a polygon by importing a shapefile.
    /// </summary>
    public class DownloadProfile{
        public string Name { get; set; }
        public int[] Levels { get; set; }
        public PBS.Util.Envelope Envelope { get; set; }
        public PBS.Util.Polygon Polygon { get; set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="levels"></param>
        /// <param name="env">wkid of envelope must be 4326</param>
        /// <param name="polygon">If null, means download extent is rectangle and drawed by using mouse by user; if not null, means download extent is a polygon by importing a shapefile.</param>
        public DownloadProfile(string name,int[] levels,PBS.Util.Envelope env,PBS.Util.Polygon polygon)
        {
            Name=name;
            Levels=levels;
            Envelope=env;
            Polygon = polygon;
        }
    }    
}
