﻿//--------------------------------------------------------------------------------------------------//
//                                                                                                  //
//        DPO(Data Persistent Object)                                                               //
//                                                                                                  //
//          Copyright(c) Datum Connect Inc.                                                         //
//                                                                                                  //
// This source code is subject to terms and conditions of the Datum Connect Software License. A     //
// copy of the license can be found in the License.html file at the root of this distribution. If   //
// you cannot locate the  Datum Connect Software License, please send an email to                   //
// datconn@gmail.com. By using this source code in any fashion, you are agreeing to be bound        //
// by the terms of the Datum Connect Software License.                                              //
//                                                                                                  //
// You must not remove this notice, or any other, from this software.                               //
//                                                                                                  //
//                                                                                                  //
//--------------------------------------------------------------------------------------------------//
using System;
using System.Collections.Generic;
using System.Text;
using System.Data.Common;
using System.Data;
using System.Diagnostics.Contracts;

using DataProviderHandle = System.Int32;

namespace Sys.Data
{
    public abstract class DbCmd
    {
        protected string script;
        protected DbProvider dbProvider;
        protected ConnectionProvider provider;

        public DbCmd(ConnectionProvider provider, string script)
        {
            this.script = script
                          .Replace("$DB_SYSTEM", Const.DB_SYSTEM)
                          .Replace("$DB_APPLICATION", Const.DB_APPLICATION);

            this.provider = provider;
            this.dbProvider = DbProvider.Factory(script, provider);
        }

        protected DbCommand command
        {
            get
            {
                return this.dbProvider.DbCommand;
            }
        }

        protected DbConnection connection
        {
            get
            {
                return dbProvider.DbConnection;
            }
        }

      
        public virtual void ChangeConnection(ConnectionProvider provider)
        {
            if (this.connection.State != ConnectionState.Closed)
                this.connection.Close();

            this.dbProvider = DbProvider.Factory(this.script, provider);
            this.command.Connection = provider.NewDbConnection; 
        }

        protected DbProviderType DbType
        {
            get
            {
                return this.dbProvider.DbType;
            }
        }

      

        public void ChangeDatabase(string database)
        {
            this.connection.ChangeDatabase(database);
        }


        public event EventHandler<SqlExceptionEventArgs> Error;

        public void OnError(SqlExceptionEventArgs e)
        {
            if (Error != null)
                Error(this, e);
            else
                throw e.Exception;
        }

       
        public object ExecuteScalar()
        {

            try
            {
                connection.Open();
                return command.ExecuteScalar();
            }
            catch (Exception ex)
            {
                OnError(new SqlExceptionEventArgs(command, ex));
            }
            finally
            {
                connection.Close();
            }

            return null;
        }

        public int ExecuteNonQuery()
        {
            int n = 0;
            try
            {
                //
                //Transaction on INSERT/UPDATE/DELETE
                //these commands use ExecuteNonQuery()
                //
                if (command.Transaction == null)
                    connection.Open();

                n = command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                OnError(new SqlExceptionEventArgs(command, ex));
            }
            finally
            {
                if (command.Transaction == null)
                    connection.Close();
            }

            return n;
        }


        /// <summary>
        /// Get stored procedure's return value
        /// </summary>
        /// <param name="parameterName"></param>
        /// <returns></returns>
        public object GetReturnValue(string parameterName)
        {
            return command.Parameters[parameterName].Value;
        }



        public void Execute(Action<DbDataReader> action)
        {
            using (connection)
            {
                connection.Open();
                DbDataReader reader = command.ExecuteReader();
                action(reader);
                
                reader.Close();
            }
        }

        public abstract DataSet FillDataSet(DataSet dataSet);
        public abstract DataTable FillDataTable(DataSet dataSet, string tableName);
        public abstract DataTable FillDataTable(DataTable table);

        public DataSet FillDataSet()
        {

            DataSet ds = new DataSet();

            if (FillDataSet(ds) == null)
                return null;

            return ds;
        }

        public DataTable FillDataTable()
        {
            DataSet ds = FillDataSet();
            if (ds == null)
                return null;

            if (ds.Tables.Count >= 1)
                return ds.Tables[0];

            return null;
        }


        public IEnumerable<T> FillDataColumn<T>(int column)
        {
            Contract.Requires(column >= 0);

            List<T> list = new List<T>();

            DataTable table = FillDataTable();
            if (table == null)
                return list;

            foreach (DataRow row in table.Rows)
            {
                object obj = row[column];
                list.Add(ToObject<T>(obj));
            }

            return list;
        }

        public IEnumerable<T> FillDataColumn<T>(string columnName)
        {
            Contract.Requires(!string.IsNullOrEmpty(columnName));

            List<T> list = new List<T>();

            DataTable table = FillDataTable();
            if (table == null)
                return list;

            foreach (DataRow row in table.Rows)
            {
                object obj = row[columnName];
                list.Add(ToObject<T>(obj));
            }

            return list;
        }

        public DataRow FillDataRow()
        {
            return FillDataRow(0);
        }

        public DataRow FillDataRow(int row)
        {
            Contract.Requires(row >= 0);

            DataTable table = FillDataTable();
            if (table != null && row < table.Rows.Count)
                return table.Rows[row];
            else
                return null;
        }

        public object FillObject()
        {
            DataRow row = FillDataRow();
            if (row != null && row.Table.Columns.Count > 0)
                return row[0];
            else
                return null;
        }

        public T FillObject<T>()
        {
            var obj = FillObject();

            return ToObject<T>(obj);
        }

        private static T ToObject<T>(object obj)
        {
            if (obj != null && obj != DBNull.Value)
                return (T)obj;
            else
                return default(T);
        }

        public DataTable ReadDataTable()
        {
            try
            {
                connection.Open();
                DbDataReader reader = this.command.ExecuteReader();

                DataTable table = new DataTable();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    DataColumn column = new DataColumn(reader.GetName(i), reader.GetFieldType(i));
                    table.Columns.Add(column);
                }

                try
                {
                    DataRow row;
                    while (reader.Read())
                    {
                        row = table.NewRow();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[i] = reader.GetValue(i);
                        }

                        table.Rows.Add(row);
                    }
                }
                finally
                {
                    reader.Close();
                }

                table.AcceptChanges();
                return table;
            }
            catch (Exception ex)
            {
                OnError(new SqlExceptionEventArgs(command, ex));
            }
            finally
            {
                connection.Close();
            }

            return null;
        }

        public override string ToString()
        {
            return this.script;
        }



    }
}
