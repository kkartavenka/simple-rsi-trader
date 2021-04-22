using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonLib.Classes
{
    public class SqliteExportClass
    {
        private readonly string _filename;
        public SqliteExportClass(string filename) {
            _filename = $"{filename}.db";
            EnsureCreated();
        }

        private void EnsureCreated() {
            if (File.Exists(_filename))
                File.Delete(_filename);

            using var connection = new SqliteConnection($"Data Source={_filename}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"CREATE TABLE instrument_data (id INTEGER PRIMARY KEY, date_time TEXT, open REAL, high REAL, low REAL, close REAL, typical_price REAL, volume REAL);
                                    CREATE TABLE prediction_data (id INTEGER PRIMARY KEY, instrument_data_id INTEGER, limit_order REAL, stop_loss REAL, take_profit REAL);";

            command.ExecuteNonQuery();
            command.Dispose();

            connection.Close();
            connection.Dispose();
        }
    }
}
