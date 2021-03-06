﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.Common;
using Sys.Data.Comparison;

namespace Sys.Data
{
    class TableScript
    {
        public static readonly string GO = "GO";

        private TableSchema schema;
        private TableName tableName;

        public TableScript(TableSchema schema)
        {
            this.schema = schema;
            this.tableName = schema.TableName;
        }



        #region SELECT/INSERT/UPDATE/DELETE


        public string IF_NOT_EXISTS_INSERT(string[] columns, object[] values)
        {
            string[] keys = schema.PrimaryKeys.Keys;
            var L1 = new List<ColumnPair>();
            foreach (var key in keys)
            {
                for (int i = 0; i < columns.Length; i++)
                    if (key == columns[i])
                    {
                        L1.Add(new ColumnPair(key, values[i]));
                    }
            }

            string where = string.Join<ColumnPair>(" AND ", L1);
            return string.Format(ifNotExistsInsertTemplate, where, INSERT(columns, values));
        }

        public string INSERT(DataRow row)
        {
            var direct = RowCompare.Direct(row).Where(column => !schema.Identity.ColumnNames.Contains(column.ColumnName));
            return INSERT(direct);
        }

        public string INSERT(string[] columnName, object[] values)
        {
            var direct = RowCompare.Direct(columnName, values).Where(column => !schema.Identity.ColumnNames.Contains(column.ColumnName));
            return INSERT(direct);
        }


        public string INSERT(IEnumerable<ColumnPair> pairs)
        {
            var x1 = pairs.Select(p => "[" + p.ColumnName + "]");
            var x2 = pairs.Select(p => p.Value.ToScript());

            return string.Format(insertCommandTemplate,
                string.Join(",", x1),
                string.Join(",", x2)
                );
        }

        public string UPDATE(RowCompare compare)
        {
            return string.Format(updateCommandTemplate, compare.Set, compare.Where);
        }



        public string DELETE(DataRow row, IPrimaryKeys primaryKey)
        {
            var L1 = new List<ColumnPair>();
            foreach (var column in primaryKey.Keys)
            {
                L1.Add(new ColumnPair(column, row[column]));
            }

            return string.Format(deleteCommandTemplate, string.Join<ColumnPair>(" AND ", L1));
        }
        #endregion


        #region SELECT/UPDATE/DELETE/INSERT template

        public string SELECT(IEnumerable<IColumn> columns)
        {
            var L = columns.Select(column => "[" + column.ColumnName + "]");
            return string.Format(selectCommandTemplate, string.Join(",", L), primaryWhere(columns));
        }

        public string INSERT(IEnumerable<IColumn> columns)
        {
            var x1 = columns.Select(column => "[" + column.ColumnName + "]");
            var x2 = columns.Select(column => ColumnValue.ToScript(column));

            return string.Format(insertCommandTemplate,
             string.Join(",", x1),
             string.Join(",", x2)
             );
        }


        public string UPDATE(IEnumerable<IColumn> columns)
        {

            string[] C = columns.Where(c => !c.IsPrimary && !c.IsIdentity).Select(c => c.ColumnName).ToArray();

            var L = new List<string>();
            foreach (var c in C)
            {
                L.Add(string.Format("[{0}]={1}", c, c.SqlParameterName()));
            }

            return string.Format(updateCommandTemplate, string.Join(",", L), primaryWhere(columns));
        }


        public string DELETE(IEnumerable<IColumn> columns)
        {
            return string.Format(deleteCommandTemplate, primaryWhere(columns));
        }

        private string primaryWhere(IEnumerable<IColumn> columns)
        {
            string[] primaryKeys = columns.Where(c => c.IsPrimary).Select(c => c.ColumnName).ToArray();
            var L = new List<string>();
            foreach (var key in primaryKeys)
            {
                L.Add(string.Format("[{0}]={1}", key, key.SqlParameterName()));
            }
            return string.Join(" AND ", L);
        }

        #endregion

        #region CREATE/DROP Table
        public string CREATE_TABLE()
        {
            TableSchema schema1 = new TableSchema(tableName);
            string format = TableSchema.GenerateCREATE_TABLE(schema1);
            string script = string.Format(format, tableName.FormalName);
            return script;
        }

        public string DROP_TABLE()
        {
            string script = string.Format("DROP TABLE {0}", tableName.FormalName);
            return script;
        }

        #endregion


        #region Add/Alter/Drop Column

        public string ADD_COLUMN(IColumn column)
        {
            return string.Format("ALTER TABLE {0} ADD {1}", tableName.FormalName, ColumnSchema.GetSQLField(column));
        }

        public string ALTER_COLUMN(IColumn column)
        {
            return string.Format("ALTER TABLE {0} ALTER COLUMN {1}", tableName.FormalName, ColumnSchema.GetSQLField(column));
        }

        public string DROP_COLUMN(IColumn column)
        {
            return string.Format("ALTER TABLE {0} DROP  COLUMN {1}", tableName.FormalName, column.ColumnName);
        }

        #endregion

        #region Primary Key

        public string ADD_PRIMARY_KEY(IPrimaryKeys primaryKey)
        {
            return string.Format("ALTER TABLE {0} ADD PRIMARY KEY ({1})", tableName.FormalName, string.Join(",", primaryKey.Keys));
        }

        public string DROP_PRIMARY_KEY(IPrimaryKeys primaryKey)
        {
            return string.Format("ALTER TABLE {0} DROP CONSTRAINT ({1})", tableName.FormalName, primaryKey.ConstraintName);
        }

        #endregion

        #region Foreign Key

        public string DROP_FOREIGN_KEY(IForeignKey foreignKey)
        {
            return string.Format("ALTER TABLE {0} DROP CONSTRAINT ({1})", tableName.FormalName, foreignKey.Constraint_Name);
        }

        public string ADD_FOREIGN_KEY(IForeignKey foreignKey)
        {
            string reference;
            if (foreignKey.PK_Schema != TableName.dbo)
                reference = string.Format(" [{0}].[{1}]([{2}])", foreignKey.PK_Schema, foreignKey.PK_Table, foreignKey.PK_Column);
            else
                reference = string.Format(" [{0}]([{1}])", foreignKey.PK_Table, foreignKey.PK_Column);

            return string.Format("ALTER TABLE {0} ADD CONSTRAINT [{1}] FOREIGN KEY ([{2}])\nREFERENCES {3}",
                tableName.FormalName,
                foreignKey.Constraint_Name,
                foreignKey.FK_Column,
                reference
                );
        }

        #endregion

        #region Insert/Update/Delete template

        private string ifNotExistsInsertTemplate
        {
            get
            {
                string ifExists = @"
IF NOT EXISTS(SELECT * FROM @@0 WHERE @@1)
   @@2";
                return ifExists
                    .Replace("@@0", tableName.FormalName)
                    .Replace("@@1", "{0}")
                    .Replace("@@2", "{1}");
            }
        }

        private string selectCommandTemplate
        {
            get { return string.Format("SELECT {0} FROM {1} WHERE {2}", "{0}", tableName.FormalName, "{1}"); }
        }
        private string updateCommandTemplate
        {
            get { return string.Format("UPDATE {0} SET {1} WHERE {2}", tableName.FormalName, "{0}", "{1}"); }
        }

        private string insertCommandTemplate
        {
            get { return string.Format("INSERT INTO {0}({1}) VALUES({2})", tableName.FormalName, "{0}", "{1}"); }
        }

        private string deleteCommandTemplate
        {
            get { return string.Format("DELETE FROM {0} WHERE {1}", tableName.FormalName, "{0}"); }
        }
        
        #endregion

    }
}
