﻿using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using DatabaseSchemaReader.DataSchema;
using DatabaseSchemaReader.ProviderSchemaReaders.Converters.KeyMaps;
using DatabaseSchemaReader.ProviderSchemaReaders.Converters.RowConverters;

namespace DatabaseSchemaReader.ProviderSchemaReaders.Databases.SqlServer
{
    internal class Columns : SqlExecuter<DatabaseColumn>
    {
        private readonly string _tableName;
        private readonly ColumnRowConverter _converter;

        public Columns(string owner, string tableName)
        {
            _tableName = tableName;
            Owner = owner;
            Sql = @"select c.TABLE_SCHEMA, 
c.TABLE_NAME, 
COLUMN_NAME, 
ORDINAL_POSITION, 
COLUMN_DEFAULT, 
IS_NULLABLE, 
DATA_TYPE, 
CHARACTER_MAXIMUM_LENGTH, 
NUMERIC_PRECISION, 
NUMERIC_SCALE, 
DATETIME_PRECISION 
from INFORMATION_SCHEMA.COLUMNS c
JOIN INFORMATION_SCHEMA.TABLES t 
 ON c.TABLE_SCHEMA = t.TABLE_SCHEMA AND 
    c.TABLE_NAME = t.TABLE_NAME
where 
    (c.TABLE_SCHEMA = @Owner or (@Owner is null)) and 
    (c.TABLE_NAME = @TableName or (@TableName is null)) AND
    TABLE_TYPE = 'BASE TABLE'
 order by 
    c.TABLE_SCHEMA, c.TABLE_NAME, ORDINAL_POSITION";

            var keyMap = new ColumnsKeyMap();
            _converter = new ColumnRowConverter(keyMap);
        }

        public IList<DatabaseColumn> Execute(DbConnection connection)
        {
            ExecuteDbReader(connection);
            return Result;
        }

        protected override void AddParameters(DbCommand command)
        {
            AddDbParameter(command, "Owner", Owner);
            AddDbParameter(command, "TableName", _tableName);
        }

        protected override void Mapper(IDataRecord record)
        {
            var col = _converter.Convert(record);
            Result.Add(col);
        }
    }
}
