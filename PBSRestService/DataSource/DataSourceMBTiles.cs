//****************************************
//Copyright@diligentpig, https://geopbs.codeplex.com
//Please using source code under LGPL license.
//****************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using PBS.Util;

namespace PBS.DataSource
{
    public class DataSourceMBTiles:DataSourceBase
    {
        private SQLiteConnection _sqlConn;

        public DataSourceMBTiles(string path)
        {
            Initialize(path);
        }

        protected override void Initialize(string path)
        {
            this.Type = DataSourceTypePredefined.MBTiles.ToString();
            _sqlConn = new SQLiteConnection("Data Source=" + path);
            _sqlConn.Open();
            base.Initialize(path);
        }

        ~DataSourceMBTiles()
        {
            if (_sqlConn != null)
                _sqlConn.Close();
            _sqlConn = null;
        }

        protected override void ReadTilingScheme(out TilingScheme tilingScheme)
        {
            //validate MBTile tile field
            using (SQLiteCommand sqlCmd = new SQLiteCommand("SELECT tile_data FROM tiles",_sqlConn))
            {
                try
                {
                    object o = sqlCmd.ExecuteScalar();
                }
                catch (Exception e)
                {
                    throw new Exception("Selected file is not a valid MBTile file\r\n" + e.Message);
                }
            }
            ReadSqliteTilingScheme(out tilingScheme, _sqlConn);
            this.TilingScheme = TilingSchemePostProcess(tilingScheme); ;
        }

        public override byte[] GetTileBytes(int level, int row, int col)
        {
            int tmsCol, tmsRow;
            Utility.ConvertGoogleTileToTMSTile(level, row, col, out tmsRow, out tmsCol);
            string commandText = string.Format("SELECT {0} FROM tiles WHERE tile_column={1} AND tile_row={2} AND zoom_level={3}", "tile_data", tmsCol, tmsRow, level);
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
