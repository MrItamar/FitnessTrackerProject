using System;
using System.Data;
using System.Data.OleDb;
namespace FitnessTrackerProject.Scripts
{
    public static class DatabaseHelper
    {
        public static readonly string ConnectionString = @"Provider=Microsoft.ACE.OLEDB.12.0;Data Source=../../DataBase\FitnessTrackerDB1.accdb;";

        // Open and return a new connection easily
        public static OleDbConnection GetConnection()
        {
            return new OleDbConnection(ConnectionString);
        }

        // Executes a select query for a single value
        public static object ExecuteScalar(string query, Action<OleDbParameterCollection> parameterAction = null)
        {
            using (OleDbConnection connection = GetConnection())
            {
                connection.Open();
                using (OleDbCommand command = new OleDbCommand(query, connection))
                {
                    if (parameterAction != null)
                    {
                        parameterAction(command.Parameters);
                    }
                    return command.ExecuteScalar();
                }
            }
        }
        //Executes a select query and returns the results in a DataTable.
        public static DataTable ExecuteQueryTable(string query, Action<OleDbParameterCollection> parameterAction = null)
        {
            using (OleDbConnection connection = GetConnection())
            {
                connection.Open();
                using (OleDbCommand command = new OleDbCommand(query, connection))
                {
                    if (parameterAction != null)
                    {
                        parameterAction(command.Parameters);
                    }
                    using (OleDbDataAdapter adapter = new OleDbDataAdapter(command))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
        }
        // Helper for commands that change data like INSERT, UPDATE, DELETE while creating a new connection
        public static int ExecuteNonQuery(string query, Action<OleDbParameterCollection> parameterAction = null)
        {
            using (OleDbConnection connection = GetConnection())
            {
                connection.Open();
                using (OleDbCommand command = new OleDbCommand(query, connection))
                {
                    if (parameterAction != null)
                    {
                        parameterAction(command.Parameters);
                    }
                    return command.ExecuteNonQuery();
                }
            }
        }
        // Helper for commands that change data like INSERT, UPDATE, DELETE while using the current connection
        public static int ExecuteNonQueryWithCurrentConnection(OleDbConnection connection, OleDbTransaction transaction, string query, params object[] values)
        {
            using (OleDbCommand command = new OleDbCommand(query, connection, transaction))
            {
                foreach (object value in values)
                {
                    command.Parameters.AddWithValue("?", value);
                }

                return command.ExecuteNonQuery();
            }
        }
    }
}