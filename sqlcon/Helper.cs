﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using System.Data.Common;
using System.IO;
using Sys.Data;

namespace sqlcon
{
    static class Helper
    {

        public static StreamWriter NewStreamWriter(this string fileName)
        {
            try
            {
                string folder = Path.GetDirectoryName(fileName);
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
            }
            catch (ArgumentException)
            { 
            }

            return new StreamWriter(fileName);
        }


      
        public static bool parse(this string arg, out string t1, out string t2)
        {
            if (string.IsNullOrEmpty(arg) || arg.StartsWith("/"))
            {
                t1 = null;
                t2 = null;
                return false;
            }

            string[] x = arg.Split(':');
            if (x.Length == 1)
            {
                t1 = x[0];
                t2 = x[0];
            }
            else
            {
                t1 = x[0];
                t2 = x[1];
            }

            return true;
        }



        public static bool IsGoodConnectionString(this SqlConnectionStringBuilder cs)
        {
            SqlConnection conn = new SqlConnection(cs.ConnectionString);
            try
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("EXEC sp_databases", conn);
                cmd.ExecuteScalar();
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                conn.Close();
            }

            return true;
        }


    }
}
