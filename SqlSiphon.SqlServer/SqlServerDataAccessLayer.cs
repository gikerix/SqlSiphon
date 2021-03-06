/*
https://www.github.com/capnmidnight/SqlSiphon
Copyright (c) 2009, 2010, 2011, 2012, 2013 Sean T. McBeth
All rights reserved.

Redistribution and use in source and binary forms, with or without modification, 
are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this 
  list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice, this 
  list of conditions and the following disclaimer in the documentation and/or 
  other materials provided with the distribution.

* Neither the name of McBeth Software Systems nor the names of its contributors
  may be used to endorse or promote products derived from this software without 
  specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND 
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED 
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. 
IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, 
INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, 
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF 
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE 
OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED 
OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using SqlSiphon.Mapping;

namespace SqlSiphon.SqlServer
{
    /// <summary>
    /// A base class for building Data Access Layers that connect to MS SQL Server 2005/2008
    /// databases and execute store procedures stored within.
    /// </summary>
    public abstract class SqlServerDataAccessLayer : DataAccessLayer<SqlConnection, SqlCommand, SqlParameter, SqlDataAdapter, SqlDataReader>
    {
        /// <summary>
        /// creates a new connection to a MS SQL Server 2005/2008 database and automatically
        /// opens the connection. 
        /// </summary>
        /// <param name="connectionString">a standard MS SQL Server connection string</param>
        public SqlServerDataAccessLayer(string connectionString)
            : base(connectionString)
        {
        }

        protected override void PreCreateProcedures()
        {
            base.PreCreateProcedures();
            this.SynchronizeUserDefinedTableTypes();
        }

        public SqlServerDataAccessLayer(SqlConnection connection)
            : base(connection)
        {
        }

        public SqlServerDataAccessLayer(SqlServerDataAccessLayer dal)
            : base(dal)
        {
        }

        protected override string IdentifierPartBegin { get { return "["; } }
        protected override string IdentifierPartEnd { get { return "]"; } }
        protected override string DefaultSchemaName { get { return "dbo"; } }

        private static Dictionary<string, Type> typeMapping;
        private static Dictionary<Type, string> reverseTypeMapping;
        static SqlServerDataAccessLayer()
        {
            typeMapping = new Dictionary<string, Type>();
            typeMapping.Add("bigint", typeof(long));
            typeMapping.Add("int", typeof(int));
            typeMapping.Add("smallint", typeof(short));
            typeMapping.Add("tinyint", typeof(byte));
            typeMapping.Add("decimal", typeof(decimal));
            typeMapping.Add("numeric", typeof(decimal));
            typeMapping.Add("money", typeof(decimal));
            typeMapping.Add("smallmoney", typeof(decimal));
            typeMapping.Add("bit", typeof(bool));
            typeMapping.Add("float", typeof(float));
            typeMapping.Add("real", typeof(double));
            typeMapping.Add("datetime2", typeof(DateTime));
            typeMapping.Add("datetime", typeof(DateTime));
            typeMapping.Add("smalldatetime", typeof(DateTime));
            typeMapping.Add("date", typeof(DateTime));
            typeMapping.Add("datetimeoffset", typeof(DateTime));
            typeMapping.Add("time", typeof(DateTime));
            typeMapping.Add("timestamp", typeof(DateTime));
            typeMapping.Add("nvarchar", typeof(string));
            typeMapping.Add("char", typeof(string));
            typeMapping.Add("varchar", typeof(string));
            typeMapping.Add("text", typeof(string));
            typeMapping.Add("nchar", typeof(string));
            typeMapping.Add("ntext", typeof(string));
            typeMapping.Add("varbinary", typeof(byte[]));
            typeMapping.Add("binary", typeof(byte[]));
            typeMapping.Add("image", typeof(byte[]));
            typeMapping.Add("uniqueidentifier", typeof(Guid));

            reverseTypeMapping = typeMapping
                .GroupBy(kv => kv.Value, kv => kv.Key)
                .ToDictionary(g => g.Key, g => g.First());

            reverseTypeMapping.Add(typeof(char[]), "nchar");

            reverseTypeMapping.Add(typeof(int?), "int");
            reverseTypeMapping.Add(typeof(uint), "int");
            reverseTypeMapping.Add(typeof(uint?), "int");

            reverseTypeMapping.Add(typeof(long?), "bigint");
            reverseTypeMapping.Add(typeof(ulong), "bigint");
            reverseTypeMapping.Add(typeof(ulong?), "bigint");

            reverseTypeMapping.Add(typeof(short?), "smallint");
            reverseTypeMapping.Add(typeof(ushort), "smallint");
            reverseTypeMapping.Add(typeof(ushort?), "smallint");

            reverseTypeMapping.Add(typeof(byte?), "tinyint");
            reverseTypeMapping.Add(typeof(sbyte), "tinyint");
            reverseTypeMapping.Add(typeof(sbyte?), "tinyint");
            reverseTypeMapping.Add(typeof(char), "tinyint");
            reverseTypeMapping.Add(typeof(char?), "tinyint");

            reverseTypeMapping.Add(typeof(decimal?), "decimal");
            reverseTypeMapping.Add(typeof(bool?), "bit");
            reverseTypeMapping.Add(typeof(float?), "float");
            reverseTypeMapping.Add(typeof(double?), "real");
            reverseTypeMapping.Add(typeof(DateTime?), "datetime2");
            reverseTypeMapping.Add(typeof(Guid?), "uniqueidentifier");
        }

        protected override void ModifyQuery(MappedMethodAttribute info)
        {
            if (info.EnableTransaction)
            {
                string transactionName = string.Format("TRANS{0}", Guid.NewGuid().ToString().Replace("-", "")).Substring(0, 32);
                string transactionBegin = string.Format("begin try\r\nbegin transaction {0};", transactionName);
                string transactionEnd = string.Format(
@"commit transaction {0};
end try
begin catch
    declare @msg nvarchar(4000), @lvl int, @stt int;
    select @msg = error_message(), @lvl = error_severity(), @stt = error_state();
    rollback transaction {0};
    raiserror(@msg, @lvl, @stt);
end catch;", transactionName);
                info.Query = string.Format("{0}\r\n{1}\r\n{2}", transactionBegin, info.Query, transactionEnd);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization | MethodImplOptions.PreserveSig)]
        [MappedMethod(CommandType = CommandType.Text,
            Query =
@"select routine_name 
from information_schema.routines 
where routine_schema = @schemaName 
    and routine_name = @routineName")]
        public bool ProcedureExistsQuery(string schemaName, string routineName)
        {
            return this.GetList<string>("routine_name", schemaName, routineName).Count >= 1;
        }

        protected override bool ProcedureExists(MappedMethodAttribute info)
        {
            return ProcedureExistsQuery(info.Schema, info.Name);
        }

        protected override string MakeDropProcedureScript(MappedMethodAttribute info)
        {
            return string.Format("drop procedure {0}", this.MakeIdentifier(info.Schema, info.Name));
        }

        protected override string MakeCreateProcedureScript(MappedMethodAttribute info)
        {
            var identifier = this.MakeIdentifier(info.Schema ?? DefaultSchemaName, info.Name);
            var parameterSection = this.MakeParameterSection(info);
            return string.Format(
@"create procedure {0}
    {1}
as begin
    set nocount on;
    {2}
end",
                identifier,
                parameterSection,
                info.Query);
        }

        protected override string MakeCreateTableScript(MappedClassAttribute info)
        {
            var schema = info.Schema ?? DefaultSchemaName;
            var identifier = this.MakeIdentifier(schema, info.Name);
            var columnSection = this.MakeColumnSection(info);
            var pk = info.Properties.Where(p => p.IncludeInPrimaryKey).ToArray();
            var pkString = "";
            if (pk.Length > 0)
            {
                pkString = string.Format(",{4}    constraint PK_{1}_{2} primary key({3}){4}",
                    identifier,
                    schema,
                    info.Name,
                    string.Join(",", pk.Select(c => c.Name)),
                    Environment.NewLine);
            }
            return string.Format(
@"if not exists(select * from information_schema.tables where table_schema = '{0}' and table_name = '{1}')
create table {2}(
    {3}{4}
)",
                schema,
                info.Name,
                identifier,
                columnSection,
                pkString);
        }

        protected override string MakeParameterString(MappedParameterAttribute p)
        {
            var typeStr = MakeSqlTypeString(p);
            return string.Join(" ",
                "@" + p.Name,
                typeStr,
                p.DefaultValue ?? "",
                IsUDTT(p.SystemType) ? "readonly" : "").Trim();
        }

        protected override string MakeColumnString(MappedPropertyAttribute p)
        {
            var typeStr = MakeSqlTypeString(p);
            var defaultString = "";
            if (p.DefaultValue != null)
                defaultString = string.Format("DEFAULT ({0})", p.DefaultValue);
            else if (p.IsIdentity)
                defaultString = "IDENTITY(1, 1)";

            return string.Format("{0} {1} {2} {3}",
                p.Name,
                typeStr,
                p.IsOptional ? "" : "NOT NULL",
                defaultString);
        }

        protected override string MakeAddColumnScript(string tableID, MappedPropertyAttribute prop)
        {
            return string.Format("alter table {0} add {1} {2};", tableID, prop.Name, prop.SqlType);
        }

        protected override string MakeDropColumnScript(ColumnInfo c)
        {
            return string.Format("alter table {0} drop column {1};",
                MakeIdentifier(c.table_schema ?? DefaultSchemaName, c.table_name),
                MakeIdentifier(c.column_name));
        }

        protected override string MakeAlterColumnScript(ColumnInfo c, MappedPropertyAttribute prop)
        {
            var temp = prop.DefaultValue;
            prop.DefaultValue = null;
            var col = string.Format("alter table {0} alter column {1};",
                MakeIdentifier(c.table_schema ?? DefaultSchemaName, c.table_name),
                MakeColumnString(prop));
            prop.DefaultValue = temp;
            return col;
        }

        protected override string MakeDefaultConstraintScript(ColumnInfo c, MappedPropertyAttribute prop)
        {
            return string.Format("alter table {0} add constraint DEF_{1} default {2} for {1}",
                MakeIdentifier(c.table_schema ?? DefaultSchemaName, c.table_name),
                c.column_name,
                prop.DefaultValue);
        }


        protected override string MakeSqlTypeString(string sqlType, Type systemType, bool isCollection, bool isSizeSet, int size, bool isPrecisionSet, int precision)
        {
            if (sqlType == null)
            {
                if (reverseTypeMapping.ContainsKey(systemType))
                    sqlType = reverseTypeMapping[systemType];
                else if (isCollection || IsUDTT(systemType))
                    sqlType = MakeUDTTName(systemType);
            }

            if (sqlType != null)
            {
                if (sqlType[sqlType.Length - 1] == ')') // someone already setup the type name, so skip it
                    return sqlType;
                else
                {
                    var typeStr = new StringBuilder(sqlType);
                    if (isSizeSet)
                    {
                        typeStr.AppendFormat("({0}", size);
                        if (isPrecisionSet)
                        {
                            typeStr.AppendFormat(", {0}", precision);
                        }
                        typeStr.Append(")");
                    }

                    if (sqlType.Contains("var")
                        && typeStr[typeStr.Length - 1] != ')')
                    {
                        typeStr.Append("(MAX)");
                    }
                    return typeStr.ToString();
                }
            }
            else
            {
                return null;
            }
        }

        static bool IsUDTT(Type t)
        {
            var isUDTT = false;
            if (t.IsArray)
            {
                t = t.GetElementType();
                isUDTT = t != typeof(byte) && IsTypePrimitive(t);
            }
            var attr = MappedObjectAttribute.GetAttribute<SqlServerMappedClassAttribute>(t);
            return (attr != null && attr.IsUploadable) || isUDTT;
        }

        public override void SynchronizeUserDefinedTableTypes()
        {
            var type = this.GetType();
            var methods = type.GetMethods();
            var complexToSync = new List<Type>();
            var simpleToSync = new List<Type>();
            foreach (var method in methods)
            {
                foreach (var parameter in method.GetParameters())
                {
                    if (IsUDTT(parameter.ParameterType))
                    {
                        var t = parameter.ParameterType;
                        if (t.IsArray)
                            t = t.GetElementType();
                        if (IsUDTT(t))
                            complexToSync.Add(t);
                        else
                            simpleToSync.Add(t);
                    }
                }
            }

            int total = simpleToSync.Count + complexToSync.Count;
            int current = 0;
            foreach (var t in simpleToSync.Distinct())
            {
                SynchronizeSimpleUDTT(t);
                this.MakeProgress(current, total, string.Format("Syncing simple array type {0}", t.Name));
                current++;
            }

            foreach (var c in complexToSync.Distinct())
            {
                MaybeSynchronizeUDTT(c);
                this.MakeProgress(current, total, string.Format("Syncing complex array type {0}", c.Name));
                current++;
            }
        }

        private void MaybeSynchronizeUDTT(Type t)
        {
            var attr = MappedObjectAttribute.GetAttribute<SqlServerMappedClassAttribute>(t);
            if (attr != null && attr.IsUploadable)
                SynchronizeComplexUDTT(t, attr);
        }

        public static string MakeUDTTName(Type t)
        {
            if (t.IsArray)
                t = t.GetElementType();

            var attr = MappedObjectAttribute.GetAttribute<SqlServerMappedClassAttribute>(t);
            if (attr == null)
            {
                attr = new SqlServerMappedClassAttribute();
                attr.Name = t.Name;
            }

            attr.InferProperties(t);

            return attr.Name + "UDTT";
        }

        private void SynchronizeSimpleUDTT(Type t)
        {
            var name = MakeUDTTName(t);
            var fullName = MakeIdentifier(DefaultSchemaName, name);
            if (this.UDTTExists(DefaultSchemaName, name))
            {
                this.DropUDTT(fullName);
            }
            this.CreateSimpleUDTT(fullName, t);
        }

        private void SynchronizeComplexUDTT(Type c, SqlServerMappedClassAttribute attr)
        {
            var schema = DefaultSchemaName;
            if (attr != null && attr.Schema != null)
                schema = attr.Schema;
            var name = MakeUDTTName(c);
            var fullName = MakeIdentifier(schema, name);
            if (this.UDTTExists(schema, name))
            {
                this.DropUDTT(fullName);
            }
            this.CreateComplexUDTT(fullName, c);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization | MethodImplOptions.PreserveSig)]
        [MappedMethod(CommandType = CommandType.Text,
            Query =
@"SELECT types.name
FROM sys.types
	inner join sys.schemas on types.schema_id = schemas.schema_id
where is_user_defined = 1
	and is_table_type = 1
	and schemas.name = @schemaName
	and types.name = @UDTTName;")]
        protected bool UDTTExists(string schemaName, string UDTTName)
        {
            return this.GetList<string>("name", schemaName, UDTTName).Count >= 1;
        }

        private void DropUDTT(string fullName)
        {
            try
            {
                this.ExecuteQuery(string.Format("DROP TYPE {0}", fullName));
            }
            catch (Exception exp)
            {
                throw new Exception(string.Format("Could not create UDTT: {0}. Reason: {1}", fullName, exp.Message), exp);
            }
        }

        private void CreateComplexUDTT(string fullName, Type mappedClass)
        {
            string script = CreateComplexUDTTScript(fullName, mappedClass);

            if (script != null)
            {
                try
                {
                    this.ExecuteQuery(script);
                }
                catch (Exception exp)
                {
                    throw new Exception(string.Format("Could not create UDTT: {0}. Reason: {1}", fullName, exp.Message), exp);
                }
            }
        }

        public string CreateComplexUDTTScript(string fullName, Type mappedClass)
        {
            var sb = new StringBuilder();
            var columns = GetProperties(mappedClass);
            // don't upload auto-incrementing identity columns
            // or columns that have a default value defined
            var colStrings = columns
                .Where(c => c.Include && !c.IsIdentity && (c.IsIncludeSet || c.DefaultValue == null))
                .Select(c => this.MaybeMakeColumnTypeString(c, true))
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
            if (colStrings.Length == 0)
            {
                return null;
            }
            else
            {
                var columnDefinition = string.Join("," + Environment.NewLine + "    ", colStrings);
                return string.Format(
    @"CREATE TYPE {0} AS TABLE(
    {1}
)",
                    fullName,
                    columnDefinition);
            }
        }

        private void CreateSimpleUDTT(string fullName, Type mappedClass)
        {
            var sb = new StringBuilder();
            var columnDefinition = reverseTypeMapping[mappedClass];
            var script = string.Format(@"CREATE TYPE {0} AS TABLE(Value {1})", fullName, columnDefinition);

            try
            {
                this.ExecuteQuery(script);
            }
            catch (Exception exp)
            {
                throw new Exception(string.Format("Could not create UDTT: {0}. Reason: {1}", fullName, exp.Message), exp);
            }
        }

        private string MaybeMakeColumnTypeString(MappedPropertyAttribute attr, bool skipDefault = false)
        {
            if (reverseTypeMapping.ContainsKey(attr.SystemType))
            {
                var typeStr = MakeSqlTypeString(attr);
                return string.Format("{0} {1} {2}NULL {3}",
                    attr.Name,
                    typeStr,
                    attr.IsOptional ? "" : "NOT ",
                    !skipDefault ? attr.DefaultValue ?? "" : "").Trim();
            }
            return null;
        }

        protected override object PrepareParameter(object parameterValue)
        {
            if (parameterValue != null)
            {
                var t = parameterValue.GetType();
                if (IsUDTT(t))
                {
                    System.Collections.IEnumerable array = null;
                    if (t.IsArray)
                    {
                        t = t.GetElementType();
                        array = (System.Collections.IEnumerable)parameterValue;
                    }
                    else
                    {
                        array = new object[] { parameterValue };
                    }

                    var table = new DataTable(MakeUDTTName(t));
                    if (reverseTypeMapping.ContainsKey(t))
                    {
                        table.Columns.Add("Value", t);
                        foreach (object obj in array)
                        {
                            table.Rows.Add(new object[] { obj });
                        }
                    }
                    else
                    {
                        // don't upload auto-incrementing identity columns
                        // or columns that have a default value defined
                        var props = GetProperties(t);
                        var columns = props
                            .Where(p => p.Include && !p.IsIdentity && (p.IsIncludeSet || p.DefaultValue == null))
                            .ToList();
                        foreach (var column in columns)
                        {
                            table.Columns.Add(column.Name, column.SystemType);
                        }
                        foreach (object obj in array)
                        {
                            List<object> row = new List<object>();
                            foreach (var column in columns)
                            {
                                var element = column.GetValue<object>(obj);
                                row.Add(element);
                            }
                            table.Rows.Add(row.ToArray());
                        }
                    }
                    return table;
                }
            }
            return parameterValue;
        }

        protected string FKToUsers<T>()
        {
            return FKToUsers<T>("UserID");
        }

        protected string FKToUsers<T>(string tableColumn)
        {
            return FK<T>(tableColumn, "dbo", "aspnet_Users", "UserID");
        }

        protected string FKToRoles<T>()
        {
            return FKToRoles<T>("RoleID");
        }

        protected string FKToRoles<T>(string tableColumn)
        {
            return FK<T>(tableColumn, "dbo", "aspnet_Roles", "RoleID");
        }

        protected override string MakeFKScript(string tableSchema, string tableName, string tableColumns, string foreignSchema, string foreignName, string foreignColumns)
        {
            var constraintName = string.Join("_",
                "FK",
                tableSchema ?? DefaultSchemaName,
                tableName,
                tableColumns.Replace(',', '_'),
                "to",
                foreignSchema ?? DefaultSchemaName,
                foreignName);

            var tableFullName = MakeIdentifier(
                tableSchema ?? DefaultSchemaName,
                tableName);

            var foreignFullName = MakeIdentifier(
                foreignSchema ?? DefaultSchemaName,
                foreignName);

            return string.Format(
@"if not exists(SELECT *  FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_NAME ='{0}')
    alter table {1} add constraint {2}
    foreign key({3})
    references {4}({5});",
                    constraintName,
                    tableFullName,
                    MakeIdentifier(constraintName),
                    tableColumns,
                    foreignFullName,
                    foreignColumns);
        }

        protected override string MakeIndexScript(string indexName, string tableSchema, string tableName, string[] tableColumns)
        {
            var columnSection = string.Join(",", tableColumns.Select(c => c + " ASC"));
            var identifier = MakeIdentifier(tableSchema ?? DefaultSchemaName, tableName);
            return string.Format(
@"if not exists(select * from sys.indexes where name = '{0}')
CREATE NONCLUSTERED INDEX {0} ON {1}({2})",
                indexName,
                identifier,
                columnSection);
        }
    }
}