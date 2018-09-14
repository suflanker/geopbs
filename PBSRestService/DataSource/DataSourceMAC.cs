//****************************************
//Copyright@diligentpig, https://geopbs.codeplex.com
//Please using source code under LGPL license.
//****************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;

namespace PBS.DataSource
{
    public class DataSourceMAC:DataSourceBase
    {
        private SQLiteConnection _sqlConn;

        public DataSourceMAC(string path)
        {
            Initialize(path);
        }

        protected override void Initialize(string path)
        {
            this.Type = DataSourceTypePredefined.MobileAtlasCreator.ToString();
            _sqlConn = new SQLiteConnection("Data Source=" + path);
            _sqlConn.Open();
            base.Initialize(path);
        }

        ~DataSourceMAC()
        {
            if (_sqlConn != null)
                _sqlConn.Close();
            _sqlConn = null;
        }

        protected override void ReadTilingScheme(out TilingScheme tilingScheme)
        {
            //validate MAC tile field
            using (SQLiteCommand sqlCmd = new SQLiteCommand("SELECT image FROM tiles", _sqlConn))
            {
                try
                {
                    object o = sqlCmd.ExecuteScalar();
                }
                catch (Exception e)
                {
                    throw new Exception("Selected file is not a valid MobileAtlasCreator file\r\n" + e.Message);
                }
            }
            ReadSqliteTilingScheme(out tilingScheme,_sqlConn);
            this.TilingScheme = TilingSchemePostProcess(tilingScheme);
        }

        public override byte[] GetTileBytes(int level, int row, int col)
        {
            string commandText = string.Format("SELECT {0} FROM tiles WHERE x={1} AND y={2} AND z={3} AND s={4}", "image", col, row, 17 - level, 0);
            using (SQLiteCommand sqlCmd = new SQLiteCommand(commandText, _sqlConn))
            {
                object o = sqlCmd.ExecuteScalar();//null can not directly convert to byte[], if so, will return "buffer can not be null" exception
                if (o != null)
                {
                    return (byte[])o;
                }
                return null;
            }
        }
    }
}
