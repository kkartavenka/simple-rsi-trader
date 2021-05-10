using CommonLib.Models;
using CommonLib.Models.Export;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CommonLib.Classes
{
    public class SqliteExportClass
    {
        private const string _instrumentDataTable = @"instrument_data";
        private const string _predictionTable = @"prediction_data";

        private readonly string _filename;
        private readonly string _connectionString;
        public SqliteExportClass(string filename) {
            _filename = $"{filename}.db";
            _connectionString = $"Data Source={_filename}";
            EnsureCreated();
        }

        private void EnsureCreated() {
            if (File.Exists(_filename))
                File.Delete(_filename);

            string sqlQuery = 
                $"CREATE TABLE {_instrumentDataTable} (id INTEGER PRIMARY KEY, date_time TEXT NOT NULL, open REAL NOT NULL, high REAL NOT NULL, low REAL NOT NULL, close REAL NOT NULL, typical_price REAL NOT NULL, volume REAL NOT NULL);" +
                $"CREATE TABLE {_predictionTable} (id INTEGER PRIMARY KEY, operation_type TEXT, instrument_data_id INTEGER NOT NULL, limit_order REAL NOT NULL, stop_loss REAL NOT NULL, take_profit REAL NOT NULL);";

            ExecuteNonQuery(sqlQuery);
        }

        public void ExecuteNonQuery(string sqlQuery) {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = sqlQuery;

            command.ExecuteNonQuery();
            command.Dispose();

            connection.Close();
            connection.Dispose();
        }

        public void PushInstrumentData(DataModel[] data) {
            string insertedValues = string.Join(',', data.Select(m => $"({m.ToSqliteRow()})"));
            string sqlQuery = $"INSERT INTO {_instrumentDataTable} (id, date_time, open, high, low, close, typical_price, volume) VALUES {insertedValues}";

            ExecuteNonQuery(sqlQuery);
        }

        public void PushPredictions(List<PredictionModel> predictions) {
            string insertedValues = string.Join(',', predictions.Select(m => $"({m.ToSqliteRow()})"));
            string sqlQuery = $"INSERT INTO {_predictionTable} (operation_type, instrument_data_id, limit_order, stop_loss, take_profit) VALUES {insertedValues};";

            ExecuteNonQuery(sqlQuery);
        }
    }
}
