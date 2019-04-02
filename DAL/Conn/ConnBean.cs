using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Data.SqlClient;

namespace CYQ.Data
{
    /// <summary>
    /// ���ݿ�������ʵ��
    /// </summary>
    internal partial class ConnBean
    {
        private ConnBean()
        {

        }
        /// <summary>
        /// ��Ӧ��ConnectionString��Name
        /// ������ԭʼ������������
        /// </summary>
        public string ConnName = string.Empty;
        /// <summary>
        /// ���ӵ�״̬�Ƿ�������
        /// </summary>
        public bool IsOK = true;
        /// <summary>
        /// �Ƿ�ӿ�
        /// </summary>
        public bool IsSlave = false;
        /// <summary>
        /// �Ƿ��ÿ�
        /// </summary>
        public bool IsBackup = false;
        /// <summary>
        /// ���Ӵ���ʱ���쳣��Ϣ��
        /// </summary>
        internal string ErrorMsg = string.Empty;
        /// <summary>
        /// ������ʽ��������ݿ������ַ���
        /// </summary>
        public string ConnString = string.Empty;
        /// <summary>
        /// ���ݿ�����
        /// </summary>
        public DalType ConnDalType;
        /// <summary>
        /// ���ݿ�汾��Ϣ
        /// </summary>
        public string Version;
        public ConnBean Clone()
        {
            ConnBean cb = new ConnBean();
            cb.ConnName = this.ConnName;
            cb.ConnString = this.ConnString;
            cb.ConnDalType = this.ConnDalType;
            cb.IsOK = this.IsOK;
            return cb;
        }
        public bool TryTestConn()
        {
            //err = string.Empty;
            if (!string.IsNullOrEmpty(ConnName))
            {
                DbBase helper = DalCreate.CreateDal(ConnName);
                try
                {

                    helper.Con.Open();
                    Version = helper.Con.ServerVersion;
                    if (string.IsNullOrEmpty(Version)) { Version = helper.DataBaseType.ToString(); }
                    helper.Con.Close();
                    IsOK = true;
                    ErrorMsg = string.Empty;
                }
                catch (Exception er)
                {
                    ErrorMsg = er.Message;
                    IsOK = false;
                }
                finally
                {
                    helper.Dispose();
                }
            }
            else
            {
                IsOK = false;
            }
            return IsOK;
        }
    }
    internal partial class ConnBean
    {
        /// <summary>
        /// ����һ��ʵ����
        /// </summary>
        /// <returns></returns>
        public static ConnBean Create(string connNameOrString)
        {
            string connString = string.Format(AppConfig.GetConn(connNameOrString), AppConfig.WebRootPath);
            if (string.IsNullOrEmpty(connString))
            {
                return null;
            }
            ConnBean cb = new ConnBean();
            cb.ConnName = connNameOrString;
            cb.ConnDalType = GetDalTypeByConnString(connString);
            cb.ConnString = RemoveConnProvider(cb.ConnDalType, connString);

            return cb;
        }
        /// <summary>
        /// ȥ�� �����е� provider=xxxx;
        /// </summary>
        public static string RemoveConnProvider(DalType dal, string connString)
        {
            if (dal != DalType.Access)
            {
                string conn = connString.ToLower();
                int index = conn.IndexOf("provider");
                if (index > -1 && index < connString.Length - 5 && (connString[index + 8] == '=' || connString[index + 9] == '='))
                {
                    int end = conn.IndexOf(';', index);
                    if (end > index)
                    {
                        connString = connString.Remove(index, end - index + 1);
                    }
                }
            }
            return connString;
        }
        public static DalType GetDalTypeByConnString(string connString)
        {
            connString = connString.ToLower().Replace(" ", "");//ȥ���ո�

            #region �ȴ��������жϹ����
            if (connString.Contains("server=") && !connString.Contains("port="))
            {
                //server=.;database=xx;uid=xx;pwd=xx;
                return DalType.MsSql;
            }
            if (connString.Contains("txtpath="))
            {
                // txt path={0}
                return DalType.Txt;
            }
            if (connString.Contains("xmlpath="))
            {
                // xml path={0}
                return DalType.Xml;
            }
            if (connString.Contains(".mdb") || connString.Contains(".accdb"))
            {
                //Provider=Microsoft.Jet.OLEDB.4.0; Data Source={0}App_Data/demo.mdb
                //Provider=Microsoft.ACE.OLEDB.12.0;Data Source={0}App_Data/demo.accdb
                return DalType.Access;
            }
            if (connString.Contains(".db;") || connString.Contains(".db3;"))
            {
                //Data Source={0}App_Data/demo.db;failifmissing=false
                return DalType.SQLite;
            }
            if (connString.Contains("provider=msdaora") || connString.Contains("provider=oraoledb.oracle")
               || connString.Contains("description=") || connString.Contains("fororacle"))
            {
                //Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=127.0.0.1)(PORT = 1521)))(CONNECT_DATA =(SID = orcl)));User ID=sa;password=123456
                return DalType.Oracle;
            }

            #endregion

            #region �����򵥻�˿�ʶ��
            if (connString.Contains("provider=ase") || connString.Contains("port=5000"))
            {
                //data source=127.0.0.1;port=5000;database=cyqdata;uid=sa;pwd=123456
                return DalType.Sybase;
            }
            if (connString.Contains("port=5432"))
            {
                ////server=.;port=5432;database=xx;uid=xx;pwd=xx;
                return DalType.PostgreSQL;
            }
            if (connString.Contains("port=3306"))
            {
                //host=127.0.0.1;Port=3306;Database=mysql;uid=sa;pwd=12346
                return DalType.MySql;
            }
            #endregion

            if (connString.Contains("host=") && File.Exists(AppConfig.AssemblyPath + "MySql.Data.dll"))
            {
                return DalType.MySql;
            }

            if (connString.Contains("datasource") && 
                (File.Exists(AppConfig.AssemblyPath + "Sybase.AdoNet2.AseClient.dll") || File.Exists(AppConfig.AssemblyPath + "Sybase.AdoNet4.AseClient.dll")))
            {
                return DalType.Sybase;
            }
            //postgre��mssql���������һ����Ϊpostgre
            if (File.Exists(AppConfig.AssemblyPath + "Npgsql.dll"))
            {
                return DalType.PostgreSQL;
            }

            return DalType.MsSql;

        }

    }
}
