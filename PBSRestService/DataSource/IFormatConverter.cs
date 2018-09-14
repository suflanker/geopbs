//****************************************
//Copyright@diligentpig, https://geopbs.codeplex.com
//Please using source code under LGPL license.
//****************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Diagnostics;
using PBS.Util;

namespace PBS.DataSource
{
    public delegate void ConvertEventHandler(object sender, ConvertEventArgs e);
    public interface IFormatConverter
    {
        /// <summary>
        /// Convert to MBTiles format.
        /// </summary>
        /// <param name="outputPath">The output path and file name of .mbtiles file.</param>
        /// <param name="name">The plain-english name of the tileset, required by MBTiles.</param>
        /// <param name="description">A description of the tiles as plain text., required by MBTiles.</param>
        /// <param name="attribution">An attribution string, which explains in English (and HTML) the sources of data and/or style for the map., required by MBTiles.</param>
        /// <param name="levels">tiles in which levels to convert to mbtiles.</param>
        /// <param name="geometry">convert/download extent, sr=3857.If this is Envelope, download by rectangle, if this is polygon, download by polygon's shape.</param>
        /// <param name="doCompact">implementing the reducing redundant tile bytes part of MBTiles specification?</param>
        void ConvertToMBTiles(string outputPath, string name, string description, string attribution, int[] levels, Geometry geometry,bool doCompact);

        /// <summary>
        /// Convert to MBTiles format.
        /// </summary>
        /// <param name="outputPath">The output path and file name of .mbtiles file.</param>
        /// <param name="name">The plain-english name of the tileset, required by MBTiles.</param>
        /// <param name="description">A description of the tiles as plain text., required by MBTiles.</param>
        /// <param name="attribution">An attribution string, which explains in English (and HTML) the sources of data and/or style for the map., required by MBTiles.</param>
        /// <param name="doCompact">implementing the reducing redundant tile bytes part of MBTiles specification?</param>
        void ConvertToMBTiles(string outputPath, string name, string description, string attribution, bool doCompact);

        /// <summary>
        /// Cancel any pending converting progress, and fire the ConvertCancelled event  when cancelled successfully.
        /// </summary>
        void CancelConverting();

        /// <summary>
        /// Fire when converting completed.
        /// </summary>
        event ConvertEventHandler ConvertCompleted;
        /// <summary>
        /// Fire when converting cancelled gracefully.
        /// </summary>
        event EventHandler ConvertCancelled;
    }

    /// <summary>
    /// The processing unit of a thread in format converting.
    /// </summary>
    public class Bundle
    {
        /// <summary>
        /// The row number of this bundle in the level.
        /// </summary>
        public int Row { get; set; }
        /// <summary>
        /// the column number of this bundle in the level.
        /// </summary>
        public int Col { get; set; }
        /// <summary>
        /// the level number which this bundle belongs.
        /// </summary>
        public int Level { get; set; }
        public TilingScheme TilingScheme{get;set;}
        /// <summary>
        /// The total count of tiles this bundle contains is PacketSize*PacketSize.
        /// When PacketSize=128, this bundle is a ArcGIS compact bundle.
        /// When PacketSize=16, this bundle is a ArcGIS supertile.
        /// </summary>
        public int PacketSize { get; set; }

        private int _startTileRow;
        /// <summary>
        /// The row number of the upper left tile in this bundle.
        /// </summary>
        public int StartTileRow
        {
            get { return _startTileRow; }
        }
        private int _startTileCol;
        /// <summary>
        /// The column number of the upper left tile in this bundle.
        /// </summary>
        public int StartTileCol
        {
            get { return _startTileCol; }
        }
        private int _endTileRow;
        /// <summary>
        /// The row number of the bottom right tile in this bundle.
        /// </summary>
        public int EndTileRow
        {
            get { return _endTileRow; }
        }
        private int _endTileCol;
        /// <summary>
        /// The column number of the bottom right tile in this bundle.
        /// </summary>
        public int EndTileCol
        {
            get { return _endTileCol; }
        }
        private Envelope extent;
        public Envelope Extent
        {
            get
            {
                if (extent == null)
                {
                    double xmin, ymin, xmax, ymax;
                    Utility.CalculateBBox(TilingScheme.TileOrigin, TilingScheme.LODs[Level].Resolution, TilingScheme.TileRows*PacketSize, TilingScheme.TileCols*PacketSize, Row, Col, out xmin, out ymin, out xmax, out ymax);
                    extent = new Envelope(xmin, ymin, xmax, ymax);
                }
                return extent;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="packetSize">When PacketSize=128, this bundle is a ArcGIS compact bundle, when PacketSize=16, this bundle is a ArcGIS supertile.</param>
        /// <param name="level">the level which this bundle belongs.</param>
        /// <param name="row">row number of this bundle in the level.</param>
        /// <param name="col">column number of this bundle in the level.</param>
        /// /// <param name="ts">using for calculate the bounding box.</param>
        public Bundle(int packetSize, int level,int row, int col,TilingScheme ts)
        {
            PacketSize = packetSize;
            Row = row;
            Col = col;
            Level = level;
            TilingScheme = ts;
            _startTileRow = PacketSize * Row;
            _startTileCol = PacketSize * Col;
            _endTileRow = (Row + 1) * PacketSize - 1;
            _endTileCol = (Col + 1) * PacketSize - 1;
        }
    }

    /// <summary>
    /// several indexes of the progress of cache conversion.
    /// </summary>
    public class ConvertStatus : INotifyPropertyChanged
    {
        private System.Timers.Timer _timer;
        private DateTime _startTime;
        /// <summary>
        /// using to calculate estimated remain time. 
        /// </summary>
        private int _countSeconds;

        private bool _isInProgress;
        /// <summary>
        /// Indicates if the the conversion is in progress, true if in progress, false if finished or not started.
        /// Also control the start and stop of the ElapsedTime.
        /// </summary>
        public bool IsInProgress
        {
            get { return _isInProgress; }
            set
            {
                _isInProgress = value;
                if (_isInProgress)
                {
                    _startTime = DateTime.Now;
                    _timer.Start();
                }
                else
                {
                    _timer.Stop();
                    _timer.Close();
                    //clear the timeremaining
                    NotifyPropertyChanged(p => p.TimeRemaining);
                }
                NotifyPropertyChanged(p => p.IsInProgress);
            }
        }

        private bool _isCommittingTransaction;
        /// <summary>
        /// Indicates if the conversion is committing a transaction/writing to sqlite file.
        /// </summary>
        public bool IsCommittingTransaction
        {
            get { return _isCommittingTransaction; }
            set
            {
                _isCommittingTransaction = value;
                NotifyPropertyChanged(p => p.IsCommittingTransaction);
            }
        }

        private bool _isDoingCompact;
        /// <summary>
        /// Indicates if the compacting progress is undergoing.
        /// </summary>
        public bool IsDoingCompact
        {
            get { return _isDoingCompact; }
            set
            {
                _isDoingCompact = value;
                NotifyPropertyChanged(p => p.IsDoingCompact);
            }
        }

        private double _sizeBeforeCompact;
        /// <summary>
        /// .mbtiles file size before compact, unit:byte.
        /// </summary>
        public double SizeBeforeCompact
        {
            get { return _sizeBeforeCompact; }
            internal set
            {
                _sizeBeforeCompact = value;
            }
        }

        private double _sizeAfterCompact;
        /// <summary>
        /// .mbtiles file size after compact, unit:byte.
        /// </summary>
        public double SizeAfterCompact
        {
            get { return _sizeAfterCompact; }
            internal set
            {
                _sizeAfterCompact = value;
            }
        }

        private bool _isCompletedSuccessfully;
        /// <summary>
        /// tell the final result of the conversion.
        /// </summary>
        public bool IsCompletedSuccessfully
        {
            get { return _isCompletedSuccessfully; }
            set { _isCompletedSuccessfully = value;
            NotifyPropertyChanged(p => p.IsCompletedSuccessfully);
            }
        }

        private long _totalCount;
        /// <summary>
        /// total count of tiles of all levels
        /// </summary>
        public long TotalCount
        {
            get { return _totalCount; }
            set
            {
                _totalCount = value;
                NotifyPropertyChanged(p => p.TotalCount);
                NotifyPropertyChanged(p => p.CompletePercent);
            }
        }

        private long _completeTotalBytes;
        /// <summary>
        /// total bytes of tiles of all levels have been downloaded/converted, using for estimated the final result file size.
        /// </summary>
        public long CompleteTotalBytes
        {
            get { return _completeTotalBytes; }
            set
            {
                _completeTotalBytes = value;            
            }
        }

        /// <summary>
        /// the estimated file size of converted result, based on totalbytes.
        /// unit: MB
        /// </summary>
        public long EstimatedFileSize
        {
            get
            {
                if (CompleteCount == 0)
                    return 0;
                return CompleteTotalBytes / CompleteCount * TotalCount / 1024 / 1024;
            }
        }

        private long _completeCount;
        /// <summary>
        /// count of tiles which already completed format conversion of all levels.
        /// </summary>
        public long CompleteCount
        {
            get { return _completeCount; }
            set
            {
                _completeCount = value;
                NotifyPropertyChanged(p => p.CompleteCount);
                NotifyPropertyChanged(p => p.CompletePercent);
            }
        }

        private long _errorCount;
        /// <summary>
        /// count of tiles which return null during format conversion of all levels.
        /// </summary>
        public long ErrorCount
        {
            get { return _errorCount; }
            set
            {
                _errorCount = value;
                NotifyPropertyChanged(p => p.ErrorCount);
            }
        }

        private int _level;
        /// <summary>
        /// current converting level
        /// </summary>
        public int Level
        {
            get { return _level; }
            set
            {
                _level = value;
                NotifyPropertyChanged(p => p.Level);
            }
        }

        private long _levelTotalCount;
        /// <summary>
        /// total count of tiles of current level.
        /// </summary>
        public long LevelTotalCount
        {
            get { return _levelTotalCount; }
            set
            {
                _levelTotalCount = value;
                NotifyPropertyChanged(p => p.LevelTotalCount);
                NotifyPropertyChanged(p => p.LevelCompletePercent);
            }
        }

        private long _levelCompleteCount;
        /// <summary>
        /// count of tiles which already completed format conversion of current level.
        /// </summary>
        public long LevelCompleteCount
        {
            get { return _levelCompleteCount; }
            set
            {
                _levelCompleteCount = value;
                NotifyPropertyChanged(p => p.LevelCompleteCount);
                NotifyPropertyChanged(p => p.LevelCompletePercent);
            }
        }

        private long _levelErrorCount;
        /// <summary>
        /// count of tiles which return null during format conversion of current level.
        /// </summary>
        public long LevelErrorCount
        {
            get { return _levelErrorCount; }
            set
            {
                _levelErrorCount = value;
                NotifyPropertyChanged(p => p.LevelErrorCount);
            }
        }

        private int _threadCount;
        /// <summary>
        /// count of threads using in the converting progress.
        /// </summary>
        public int ThreadCount
        {
            get { return _threadCount; }
            set { _threadCount = value;
            NotifyPropertyChanged(p => p.ThreadCount);
            }
        }

        private bool _isCancelled;

        public bool IsCancelled
        {
            get { return _isCancelled; }
            set { _isCancelled = value;
            NotifyPropertyChanged(p => p.IsCancelled);
            }
        }



        /// <summary>
        /// complete percent of conversion of all levels.
        /// </summary>
        public double CompletePercent
        {
            get { return (double)_completeCount / _totalCount*100; }
        }

        /// <summary>
        /// complete percent of conversion of current level.
        /// </summary>
        public double LevelCompletePercent
        {
            get { return (double)_levelCompleteCount / _levelTotalCount*100; }
        }

        /// <summary>
        /// used to record the processing time.
        /// start recording the time when IsInProgress=true, and stop when false.
        /// </summary>
        public TimeSpan TimeElapsed
        {
            get { return DateTime.Now - _startTime; }
        }

        /// <summary>
        /// the estimated remaining time, calculated based on the elapsed time and CompleteCount.
        /// </summary>
        public TimeSpan TimeRemaining
        {
            get {
                if (CompleteCount > 0 && IsInProgress)
                    return TimeSpan.FromMilliseconds((TimeElapsed.TotalMilliseconds / CompleteCount) * (TotalCount - CompleteCount));
                else
                    return TimeSpan.FromMilliseconds(0);
            }
        }

        public ConvertStatus()
        {
            _countSeconds = 0;
            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += (s, a) =>
            {
                if (!IsInProgress)
                    return;
                NotifyPropertyChanged(p => p.TimeElapsed);
                //update RemainingTime/EstimatedFileSize every 3 seconds
                _countSeconds++;
                if (_countSeconds == 3)
                {
                    NotifyPropertyChanged(p => p.TimeRemaining);
                    NotifyPropertyChanged(p => p.EstimatedFileSize);
                    _countSeconds = 0;
                }
            };

            _isInProgress = false;
            _isCommittingTransaction = false;
            _isCompletedSuccessfully = false;
            _isCancelled = false;
            _threadCount = 1;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged<TValue>(Expression<Func<ConvertStatus, TValue>> propertySelector)
        {
            if (PropertyChanged == null)
                return;

            var memberExpression = propertySelector.Body as MemberExpression;
            if (memberExpression == null)
                return;

            PropertyChanged(this, new PropertyChangedEventArgs(memberExpression.Member.Name));
        }
    }

    public class ConvertEventArgs : EventArgs
    {
        public bool Successful { get; set; }
        public ConvertEventArgs(bool b)
        {
            Successful = b;
        }
    }
}
