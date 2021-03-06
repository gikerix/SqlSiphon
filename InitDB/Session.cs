﻿using System.Linq;
using System.IO;
using SqlSiphon;

namespace InitDB
{
    class Session : SqlSiphon.BoundObject
    {
        public string Name { get { return Get<string>(); } set { Set(value); } }
        public string Server { get { return Get<string>(); } set { Set(value); } }
        public string DBName { get { return Get<string>(); } set { Set(value); } }
        public string AdminName { get { return Get<string>(); } set { Set(value); } }
        public string AdminPassword { get { return Get<string>(); } set { Set(value); } }
        public string LoginName { get { return Get<string>(); } set { Set(value); } }
        public string LoginPassword { get { return Get<string>(); } set { Set(value); } }
        public string AssemblyFile { get { return Get<string>(); } set { Set(value); } }
        public bool CreateDatabase { get { return Get<bool>(); } set { Set(value); } }
        public bool CreateLogin { get { return Get<bool>(); } set { Set(value); } }
        public bool RegisterASPNETMembership { get { return Get<bool>(); } set { Set(value); } }
        public bool CreateSchemaObjects { get { return Get<bool>(); } set { Set(value); } }
        public bool InitializeData { get { return Get<bool>(); } set { Set(value); } }
        public bool SyncStoredProcedures { get { return Get<bool>(); } set { Set(value); } }
        public bool CreateFKs { get { return Get<bool>(); } set { Set(value); } }
        public bool CreateIndices { get { return Get<bool>(); } set { Set(value); } }

        public static char PAIR_SEPARATOR = ';';
        public static char KEY_VALUE_SEPARATOR = ':';


        public Session() : this(InitDB.Form1.DEFAULT_SESSION_NAME, "localhost\\SQLEXPRESS", "", "", "", "", "", "", false, false, false, false, false, true, false, false)
        {
        }

        public Session(string name, string server, string dbname, string adminName, string adminPassword, 
            string loginName, string loginPassword, string assemblyFile,
            bool createDatabase, bool createLogin, bool registerASPNET, bool createSchemaObj, bool initData, bool syncProcs,
            bool createFKs, bool createIndices)
        {
            this.Name = name;
            this.Server = server;
            this.DBName = dbname;
            this.AdminName = adminName;
            this.AdminPassword = adminPassword;
            this.LoginName = loginName;
            this.LoginPassword = loginPassword;
            this.AssemblyFile = assemblyFile;
            this.CreateDatabase = createDatabase;
            this.CreateLogin = createLogin;
            this.RegisterASPNETMembership = registerASPNET;
            this.CreateSchemaObjects = createSchemaObj;
            this.InitializeData = initData;
            this.SyncStoredProcedures = syncProcs;
            this.CreateFKs = createFKs;
            this.CreateIndices = createIndices;
        }

        public Session(string line)
        {
            this.values = line
                .Split(PAIR_SEPARATOR)
                .Select(pair => pair.Split(KEY_VALUE_SEPARATOR))
                .ToDictionary(pair => pair.FirstOrDefault(), pair =>
                {
                    var v = pair.LastOrDefault() ?? "";
                    var o = v.Equals("True") || v.Equals("False")
                        ? (object)v.Equals("True")
                        : (object)v;
                    return o;
                });
        }

        public override string ToString()
        {
            return
                string.Join(PAIR_SEPARATOR.ToString(),
                    this.values.Select(pair =>
                        string.Join(KEY_VALUE_SEPARATOR.ToString(), pair.Key, pair.Value)));
        }

        public string GetMOTD()
        {
            using (var db = this.MakeDatabaseConnection())
                return db.MOTD;
        }

        public ISqlSiphon MakeDatabaseConnection()
        {
            if (File.Exists(this.AssemblyFile))
            {
                var ss = typeof(ISqlSiphon);
                var constructorParams = new[] { typeof(string) };
                var constructorArgs = new object[] { this.MakeConnectionString() };
                var assembly = System.Reflection.Assembly.LoadFrom(this.AssemblyFile);
                return assembly.GetTypes()
                    .Where(t => t.GetInterfaces().Contains(ss)
                                && !t.IsAbstract
                                && !t.IsInterface)
                    .Select(t => (ISqlSiphon)(t.GetConstructor(constructorParams)
                                .Invoke(constructorArgs)))
                    .FirstOrDefault();
            }
            return null;
        }

        private string MakeConnectionString()
        {
            var conn = new System.Data.SqlClient.SqlConnectionStringBuilder
            {
                DataSource = this.Server,
                InitialCatalog = this.DBName
            };
            var cred = new [] { 
                new[]{this.AdminName, this.AdminPassword}, 
                new[]{this.LoginName, this.LoginPassword}}
                .Where(s => !string.IsNullOrEmpty(s[0]) && !string.IsNullOrEmpty(s[1]))
                .FirstOrDefault();
            conn.IntegratedSecurity = cred == null;
            if (cred != null)
            {
                conn.UserID = cred[0];
                conn.Password = cred[1];
            }
            return conn.ConnectionString;
        }
    }
}
