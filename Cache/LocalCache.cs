using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using CYQ.Data.Tool;
using CYQ.Data.Table;
using CYQ.Data.SQL;
using System.IO;


namespace CYQ.Data.Cache
{
    /// <summary>
    /// ����������
    /// Ϊ����.NET Core ȥ��Web.Caching����д
    /// </summary>
    internal class LocalCache : CacheManage
    {

        private List<string> theKey = new List<string>();
        private MDictionary<string, object> theCache = new MDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private MDictionary<string, DateTime> theKeyTime = new MDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private SortedDictionary<int, List<string>> theTime = new SortedDictionary<int, List<string>>();
        private MDictionary<string, FileSystemWatcher> theFile = new MDictionary<string, FileSystemWatcher>();
        private MDictionary<string, List<string>> theFileKeys = new MDictionary<string, List<string>>();//һ��·����Ӧ���Key

        private static object lockObj = new object();
        private DateTime workTime, startTime;
        private MDataTable _CacheSchemaTable;
        private MDataTable CacheSchemaTable
        {
            get
            {
                if (_CacheSchemaTable == null || _CacheSchemaTable.Columns.Count == 0)
                {
                    _CacheSchemaTable = new MDataTable(CacheType.ToString());
                    _CacheSchemaTable.Columns.Add("Key", System.Data.SqlDbType.NVarChar);
                    _CacheSchemaTable.Columns.Add("Value", System.Data.SqlDbType.Float);
                }
                return _CacheSchemaTable;
            }
        }

        private DateTime getCacheTableTime = DateTime.Now;//��ȡ���������ݵ�ʱ�䡣
        /// <summary>
        /// ��ȡ������Ϣ�����б�
        /// </summary>
        public override MDataTable CacheInfo
        {
            get
            {
                if (CacheSchemaTable.Rows.Count == 0 || getCacheTableTime.AddSeconds(20) < DateTime.Now)
                {
                    getCacheTableTime = DateTime.Now;
                    CacheSchemaTable.Rows.Clear();
                    CacheSchemaTable.NewRow(true).Set(0, "KeyCount").Set(1, theKey.Count);
                    CacheSchemaTable.NewRow(true).Set(0, "KeyTimeCount").Set(1, theKeyTime.Count);
                    CacheSchemaTable.NewRow(true).Set(0, "CacheCount").Set(1, theCache.Count);
                    CacheSchemaTable.NewRow(true).Set(0, "FileCount").Set(1, theFile.Count);
                    CacheSchemaTable.NewRow(true).Set(0, "TimeCount").Set(1, theTime.Count);
                    CacheSchemaTable.NewRow(true).Set(0, "TaskCount").Set(1, taskCount);
                    CacheSchemaTable.NewRow(true).Set(0, "ErrorCount").Set(1, errorCount);
                }
                return _CacheSchemaTable;
            }
        }
        internal LocalCache()
        {
            try
            {
                ThreadBreak.AddGlobalThread(new ParameterizedThreadStart(ClearState));
                if (AppConfig.Cache.IsAutoCache)
                {
                    ThreadBreak.AddGlobalThread(new ParameterizedThreadStart(AutoCache.ClearCache));
                }
            }
            catch (Exception err)
            {
                errorCount++;
                Log.WriteLogToTxt(err);
            }
        }
        int taskCount = 0, taskInterval = 5;//5������һ�λ��档
        private static DateTime errTime = DateTime.MinValue;
        private void ClearState(object threadID)
        {
            startTime = DateTime.Now;
            while (true)
            {
                try
                {
                    workTime = startTime.AddMinutes((taskCount + 1) * taskInterval);
                    TimeSpan ts = workTime - DateTime.Now;
                    if (ts.TotalSeconds > 0)
                    {
                        Thread.Sleep(ts);//taskInterval * 60 * 1000);//10��������ʱ��
                    }
                    #region �µĻ���
                    if (theTime.ContainsKey(taskCount))
                    {
                        RemoveList(theTime[taskCount]);
                        theTime.Remove(taskCount);
                    }
                    #endregion


                }
                catch (ThreadAbortException e)
                {
                }
                catch (OutOfMemoryException)
                { errorCount++; }
                catch (Exception err)
                {
                    errorCount++;
                    if (errTime == DateTime.MinValue || errTime.AddMinutes(10) < DateTime.Now) // 10���Ӽ�¼һ��
                    {
                        errTime = DateTime.Now;
                        Log.WriteLogToTxt("LocalCache.ClearState:" + Log.GetExceptionMessage(err));
                    }
                }
                finally
                {
                    taskCount++;
                    if (taskCount % 10 == 9)
                    {
                        try
                        {
                            if (theKey.Count > 500000)
                            {
                                NoSqlAction.ResetStaticVar();
                                GC.Collect();
                            }
                        }
                        catch
                        {
                            errorCount++;
                        }
                    }
                }

            }
        }
        private int errorCount = 0;//���沶�쳣����
        /// <summary>
        /// �ڴ湤����Ϣ
        /// </summary>
        public override string WorkInfo
        {
            get
            {
                JsonHelper js = new JsonHelper(false, false);
                js.Add("TaskCount", taskCount.ToString(), true);
                js.Add("ErrorCount", errorCount.ToString(), true);
                js.Add("NextTaskTime", workTime.ToString());
                js.AddBr();
                return js.ToString();
                // return string.Format("try catch error count:{0}--clear count:{1}--next clear work time at:{2}", errorCount, taskCount, workTime);
            }
        }
        /// <summary>
        /// ��ͻ�������
        /// </summary>
        public override int Count
        {
            get
            {
                return theKey.Count;
            }
        }

        /// <summary>
        /// ���һ��Cache����
        /// </summary>
        /// <param name="key">��ʶ</param>
        public override object Get(string key)
        {
            if (Contains(key))
            {
                return theCache[key];// && theCache.ContainsKey(key) �ڲ������жϺ�Lock
            }
            return null;
        }
        /// <summary>
        /// �Ƿ���ڻ���
        /// </summary>
        /// <param name="key">��ʶ</param>
        /// <returns></returns>
        public override bool Contains(string key)
        {
            return theKey.Contains(key) && theKeyTime.ContainsKey(key) && theKeyTime[key] > DateTime.Now;
        }
        /// <summary>
        /// ���һ��Cache����
        /// </summary>
        /// <param name="key">��ʶ</param>
        /// <param name="value">����ֵ</param>
        public override void Set(string key, object value)
        {
            Set(key, value, AppConfig.Cache.DefaultCacheTime);
        }
        /// <param name="cacheMinutes">����ʱ��(��λ����)</param>
        public override void Set(string key, object value, double cacheMinutes)
        {
            Set(key, value, cacheMinutes, null);
        }
        /// <param name="fileName">�ļ�����·��</param>
        public override void Set(string key, object value, double cacheMinutes, string fileName)
        {
            try
            {
                lock (lockObj)
                {
                    if (!theKey.Contains(key))
                    {
                        theKey.Add(key);//1������ key
                    }

                    if (theCache.ContainsKey(key))
                    {
                        theCache[key] = value;
                    }
                    else
                    {
                        theCache.Add(key, value);//2������value
                    }
                    double cacheTime = cacheMinutes;
                    if (cacheMinutes <= 0)
                    {
                        cacheTime = AppConfig.Cache.DefaultCacheTime;
                    }
                    DateTime cTime = DateTime.Now.AddMinutes(cacheTime);
                    int workCount = GetWorkCount(cTime);
                    if (theKeyTime.ContainsKey(key))
                    {
                        int wc = GetWorkCount(theKeyTime[key]);
                        if (wc != workCount && theTime.ContainsKey(wc))
                        {
                            if (theTime[wc].Contains(key))
                            {
                                theTime[wc].Remove(key); //�Ƴ���ֵ
                            }
                        }
                        theKeyTime[key] = cTime;
                    }
                    else
                    {
                        theKeyTime.Add(key, cTime);
                    }


                    if (theTime.ContainsKey(workCount))//3������time
                    {
                        if (!theTime[workCount].Contains(key))
                        {
                            theTime[workCount].Add(key);
                        }
                    }
                    else
                    {
                        List<string> list = new List<string>();
                        list.Add(key);
                        theTime.Add(workCount, list);
                    }

                    if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))//3������file
                    {
                        if (!theFile.ContainsKey(key))
                        {
                            if (fileName.IndexOf("\\\\") > -1)
                            {
                                fileName = fileName.Replace("\\\\", "\\");
                            }
                            theFile.Add(key, CreateFileSystemWatcher(fileName));
                            if (theFileKeys.ContainsKey(fileName))
                            {
                                theFileKeys[fileName].Add(key);
                            }
                            else
                            {
                                List<string> list = new List<string>();
                                list.Add(key);
                                theFileKeys.Add(fileName, list);
                            }
                        }
                    }
                }
            }
            catch
            {
                errorCount++;
            }
        }

        private int GetWorkCount(DateTime cTime)
        {
            TimeSpan ts = cTime - startTime;
            return (int)ts.TotalMinutes / taskInterval;//������뿪ʼ�ж��ٸ����ʱ�䡣
        }

        /// <summary>
        /// ɾ��һ��Cache����
        /// </summary>
        /// <param name="key">��ʶ</param>
        public override void Remove(string key)
        {
            if (Contains(key))//������ʱ�л��Զ����cacheState
            {
                try
                {
                    theKey.Remove(key);//ֻ���Key��value��Time���ڶ�ʱ���б����
                }
                catch
                {
                    errorCount++;
                }
            }
        }
        /// <summary>
        /// �Ƴ�Key��Value
        /// </summary>
        /// <param name="removeKeys"></param>
        private void RemoveList(List<string> removeKeys)
        {
            if (removeKeys != null && removeKeys.Count > 0)
            {
                lock (lockObj)
                {
                    foreach (string key in removeKeys)
                    {
                        try
                        {
                            Remove(key);//key
                            if (theCache.ContainsKey(key))
                            {
                                theCache.Remove(key);//value
                            }
                            if (theKeyTime.ContainsKey(key))
                            {
                                theKeyTime.Remove(key);
                            }
                            if (theFile.ContainsKey(key))
                            {
                                string file = theFile[key].Path;
                                theFile[key].Changed -= new FileSystemEventHandler(fsy_Changed);//ȡ���¼�
                                List<string> keys = theFileKeys[file];
                                keys.Remove(key);
                                if (keys.Count == 0)
                                {
                                    theFileKeys.Remove(file);//filekeys
                                }
                                theFile.Remove(key);//file

                            }
                            //file
                        }
                        catch
                        {
                            errorCount++;
                            break;
                        }
                    }
                }
            }
        }
        /// <summary>
        /// ������л���
        /// </summary>
        public override void Clear()
        {
            try
            {
                lock (lockObj)
                {
                    TableSchema.tableCache.Clear();
                    TableSchema.columnCache.Clear();
                    theKey.Clear();
                    theCache.Clear();
                    theTime.Clear();
                    theKeyTime.Clear();
                    for (int i = 0; i < theFile.Count; i++)
                    {
                        theFile[i].Changed -= new FileSystemEventHandler(fsy_Changed);
                        theFile[i] = null;
                    }
                    theFile.Clear();
                    theFileKeys.Clear();
                }
            }
            catch
            {
                errorCount++;
            }
        }


        public override CacheType CacheType
        {
            get { return CacheType.LocalCache; }
        }

        #region �����ļ�����
        private FileSystemWatcher CreateFileSystemWatcher(string fileName)
        {
            FileSystemWatcher fsy = new FileSystemWatcher(Path.GetDirectoryName(fileName), "*" + Path.GetFileName(fileName));
            fsy.EnableRaisingEvents = true;
            fsy.IncludeSubdirectories = false;
            fsy.Changed += new FileSystemEventHandler(fsy_Changed);
            return fsy;
        }
        private static readonly object obj2 = new object();
        void fsy_Changed(object sender, FileSystemEventArgs e)
        {
            lock (obj2)
            {
                string fileName = e.FullPath;
                if (theFileKeys.ContainsKey(fileName))
                {
                    List<string> keys = theFileKeys[fileName];
                    int count = keys.Count;
                    for (int i = 0; i < count; i++)
                    {
                        Remove(keys[i]);
                    }
                    keys.Clear();
                }
            }
        }
        #endregion
    }
}
