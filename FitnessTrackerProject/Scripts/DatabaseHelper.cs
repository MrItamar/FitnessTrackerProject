using System;
using System.Data;
using System.Data.OleDb;
namespace FitnessTrackerProject.Scripts
{
    public static class DatabaseHelper
    {
        // Saved in ONE single spot (using your exact database path and name)
        public static readonly string ConnectionString = @"Provider=Microsoft.ACE.OLEDB.12.0;Data Source=../../DataBase\FitnessTrackerDB1.accdb;";

        // Open and return a new connection easily
        public static OleDbConnection GetConnection()
        {
            return new OleDbConnection(ConnectionString);
        }

        // Helper for queries that return a single value (like checking if a user exists, or getting an ID / COUNT)
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
        // Helper for commands that change data (INSERT, UPDATE, DELETE)
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
    }
}