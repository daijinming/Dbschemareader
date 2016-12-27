﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using DatabaseSchemaReader.Conversion.KeyMaps;
using DatabaseSchemaReader.DataSchema;

namespace DatabaseSchemaReader.Conversion
{
    /// <summary>
    /// Converts the DataTables returned by DbConnection.GetSchema into objects.
    /// Resolves differences between SqlServer and Oracle names
    /// </summary>
    /// <remarks>
    /// SqlServer: http://msdn.microsoft.com/en-us/library/ms254969.aspx
    /// Oracle: http://msdn.microsoft.com/en-us/library/cc716723.aspx
    /// Also supports OleDb, MySql, SQLite, DataDirect and Devart providers
    /// ODP: http://download.oracle.com/docs/html/E15167_01/appSpecificSchema.htm
    /// Devart: http://www.devart.com/dotconnect/oracle/docs/MetaData.html
    /// </remarks>
    internal static class SchemaConverter
    {

        /// <summary>
        /// Converts the "Tables" DataTable into <see cref="DatabaseTable"/> objects.
        /// </summary>
        /// <remarks>
        /// Note the SqlServer DataTable includes views, which we explicitly remove. 
        /// </remarks>
        public static List<DatabaseTable> Tables(DataTable dt)
        {
            List<DatabaseTable> list = new List<DatabaseTable>();

            TableKeyMap keyMap = new TableKeyMap(dt);

            foreach (DataRow row in dt.Rows)
            {
                string type = row[keyMap.TypeKey].ToString();
                //Sql server has base tables and views. Oracle has system and user
                if (IsNotTable(type)) continue;
                DatabaseTable t = new DatabaseTable();
                t.Name = row[keyMap.TableName].ToString();
                //exclude Oracle bin tables
                if (t.Name.StartsWith("BIN$", StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(keyMap.OwnerKey))
                {
                    t.SchemaOwner = row[keyMap.OwnerKey].ToString();
                    //Db2 system tables creeping in
                    if (keyMap.IsDb2 && t.SchemaOwner.Equals("SYSTOOLS", StringComparison.OrdinalIgnoreCase)) continue;
                }
                list.Add(t);
            }
            return list;
        }

        private static bool IsNotTable(string type)
        {
            //may be a VIEW or a system table
            return !type.Equals("TABLE", StringComparison.OrdinalIgnoreCase) &&
                   !type.Equals("BASE", StringComparison.OrdinalIgnoreCase) && //sybase
                   !type.Equals("BASE TABLE", StringComparison.OrdinalIgnoreCase) &&
                   !type.Equals("User", StringComparison.OrdinalIgnoreCase) &&
                //MySQL types are something different
                   !type.Equals("InnoDB", StringComparison.OrdinalIgnoreCase) &&
                   !type.Equals("MyISAM", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Finds the first schema (usually COLUMNS table)
        /// </summary>
        public static string FindSchema(DataTable dt)
        {
            //sql server
            string ownerKey = "TABLE_SCHEMA";
            //oracle
            if (!dt.Columns.Contains(ownerKey)) ownerKey = "OWNER";
            //Devart.Data.Oracle - TABLE_NAME is NAME
            if (!dt.Columns.Contains(ownerKey)) ownerKey = "SCHEMA";
            //DB2 iSeries
            if (!dt.Columns.Contains(ownerKey)) ownerKey = "SchemaName";
            //don't know
            if (!dt.Columns.Contains(ownerKey)) return null;

            //find the first table and return it
            foreach (DataRow row in dt.Rows)
            {
                return row[ownerKey].ToString();
            }
            return null;
        }

        /// <summary>
        /// Converts the "Views" DataTable into <see cref="DatabaseView"/> objects.
        /// </summary>
        public static List<DatabaseView> Views(DataTable dt)
        {
            List<DatabaseView> list = new List<DatabaseView>();

            ViewKeyMap viewKeyMap = new ViewKeyMap(dt);

            foreach (DataRow row in dt.Rows)
            {
                if (viewKeyMap.TypeKey != null)
                {
                    var type = row[viewKeyMap.TypeKey].ToString();
                    if (type != "VIEW") continue;
                }
                DatabaseView t = new DatabaseView();
                t.Name = row[viewKeyMap.Key].ToString();
                t.SchemaOwner = row[viewKeyMap.OwnerKey].ToString();
                //ignore db2 system tables
                if (viewKeyMap.TypeKey != null && t.SchemaOwner.StartsWith("SYS", StringComparison.OrdinalIgnoreCase)) continue;
                if (viewKeyMap.HasSql) t.Sql = row[viewKeyMap.Definition].ToString();
                list.Add(t);
            }
            return list;
        }

        public static List<DataType> DataTypes(DataTable dataTable)
        {
            List<DataType> list = new List<DataType>();
            if (dataTable == null || dataTable.Rows.Count == 0) return list;

            var typename = "TypeName";
            var datatype = "DataType";
            var providerdbtype = "ProviderDbType";
            var literalprefix = "LiteralPrefix";
            var literalsuffix = "LiteralSuffix";
            var createformat = "CreateFormat";
            //DB2
            if (!dataTable.Columns.Contains(typename)) typename = "provider_type_name";
            if (!dataTable.Columns.Contains(datatype)) datatype = "framework_type";
            if (!dataTable.Columns.Contains(providerdbtype)) providerdbtype = "provider_type";
            if (!dataTable.Columns.Contains(literalprefix)) literalprefix = "literal_prefix";
            if (!dataTable.Columns.Contains(literalsuffix)) literalsuffix = "literal_suffix";
            if (!dataTable.Columns.Contains(createformat)) createformat = null;

            foreach (DataRow row in dataTable.Rows)
            {
                string typeName = row[typename].ToString();
                string netDataType = row[datatype].ToString();
                DataType d = new DataType(typeName, netDataType);
                var pdt = GetNullableInt(row[providerdbtype]);
                d.ProviderDbType = pdt.HasValue ? pdt.Value : -1;
                d.LiteralPrefix = row[literalprefix].ToString();
                d.LiteralSuffix = row[literalsuffix].ToString();
                if (createformat != null)
                    d.CreateFormat = row[createformat].ToString();
                list.Add(d);
            }
            return list;
        }

        private static int? GetNullableInt(object o)
        {
            try
            {
                return (o != DBNull.Value) ? Convert.ToInt32(o, CultureInfo.CurrentCulture) : (int?)null;
            }
            catch (OverflowException)
            {
                //this occurs for blobs and clobs using the OleDb provider
                return -1;
            }
        }
    }
}
