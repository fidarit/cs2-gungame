﻿using GunGame.Models;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace GunGame.Tools
{
    public class OnlineManager : CustomManager
    {
        private readonly DBConfig dbConfig = null!;
        private MySqlConnection _mysqlConn = null!;

        public bool OnlineReportEnable = false;
        public bool SavingPlayer = false;

        public OnlineManager(DBConfig dbConfig, GunGame plugin) : base(plugin)
        {
            this.dbConfig = dbConfig;
            if (this.dbConfig != null)
            {
                if (this.dbConfig.DatabaseType.Trim().ToLower() == "mysql")
                {
                    if (this.dbConfig.DatabaseHost.Length < 1 || this.dbConfig.DatabaseName.Length < 1 || this.dbConfig.DatabaseUser.Length < 1)
                    {
                        Console.WriteLine("[GunGame_OnlineManager] InitializeDatabaseConnection: Error in DataBase config. DatabaseHost, DatabaseName and DatabaseUser should be set. Continue without GunGame statistics");
                        Logger.LogInformation("[GunGame_Stats] InitializeDatabaseConnection: Error in DataBase config. DatabaseHost, DatabaseName and DatabaseUser should be set. Continue without GunGame statistics");
                        return;
                    }
                    _ = InitializeDatabaseConnectionAsync();
                }
            }
        }
        private async Task InitializeDatabaseConnectionAsync()
        {
            try
            {
                var builder = new MySqlConnectionStringBuilder
                {
                    Server = dbConfig.DatabaseHost,
                    Database = dbConfig.DatabaseName,
                    UserID = dbConfig.DatabaseUser,
                    Password = dbConfig.DatabasePassword,
                    Port = (uint)dbConfig.DatabasePort,
                };
                _mysqlConn = new MySqlConnection(builder.ConnectionString);

                // Asynchronously open the MySQL connection
                await _mysqlConn.OpenAsync();
                Console.WriteLine("[GunGame_Online] Connection to database established successfully.");

                // Asynchronously close the connection after testing
                await _mysqlConn.CloseAsync();

                OnlineReportEnable = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GunGame_Online - FATAL] InitializeDatabaseConnection: Database connection error: {ex.Message}, working without online reports");
            }
        }

        public async Task SavePlayerData(GGPlayer player, string team)
        {
            if (!OnlineReportEnable)
                return;
            int attempts = 0;
            while (SavingPlayer && attempts < 10)
            {
                attempts++;
                await Task.Delay(200);
            }
            if (SavingPlayer)
            {
                Console.WriteLine($"[GunGame_Online - FATAL] SavePlayerData ******* Waiting too long to save");
                return;
            }

            if (player == null || player.SavedSteamID == 0) return;
            SavingPlayer = true;
            string safePlayerName = System.Net.WebUtility.HtmlEncode(player.PlayerName);


            var sql = "INSERT INTO `realtime_stats` (`id`, `nickname`, `level`, `team`) " +
            "VALUES (@id, @PlayerName, @level, @team) " +
            "ON DUPLICATE KEY UPDATE `nickname` = @PlayerName, `level` = @level, `team` = @team;";
            try
            {
                await _mysqlConn.OpenAsync();
                using (var command = new MySqlCommand(sql, _mysqlConn))
                {
                    command.Parameters.AddWithValue("@id", player.SavedSteamID);
                    command.Parameters.AddWithValue("@PlayerName", safePlayerName);
                    command.Parameters.AddWithValue("@level", player.Level);
                    command.Parameters.AddWithValue("@team", team);

                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GunGame_Online - FATAL] SavePlayerData ******* An error occurred: {ex.Message}");
                SavingPlayer = false;
            }
            finally
            {
                await _mysqlConn.CloseAsync();
            }
            SavingPlayer = false;
        }
        public async Task RemovePlayerData(GGPlayer player)
        {
            if (!OnlineReportEnable)
                return;
            int attempts = 0;
            while (SavingPlayer && attempts < 10)
            {
                attempts++;
                await Task.Delay(200);
            }
            if (SavingPlayer)
            {
                Console.WriteLine($"[GunGame_Online - FATAL] RemovePlayerData ******* Waiting too long to save");
                return;
            }
            SavingPlayer = true;
            string query = "DELETE FROM `realtime_stats` WHERE `id` = @authid;";
            try
            {
                await _mysqlConn.OpenAsync();

                using (var command = new MySqlCommand(query, _mysqlConn))
                {
                    command.Parameters.AddWithValue("@authid", player.SavedSteamID);
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GunGame_Online - FATAL]******* RemovePlayerData: An error occurred: {ex.Message}");
            }
            finally
            {
                await _mysqlConn.CloseAsync();
            }
            SavingPlayer = false;
        }
        public async Task ClearAllPlayerData(string mapname)
        {
            if (!OnlineReportEnable)
                return;
            string query = "DELETE FROM `realtime_stats` WHERE 1;";
            try
            {
                await _mysqlConn.OpenAsync();

                using (var command = new MySqlCommand(query, _mysqlConn))
                {
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GunGame_Online - FATAL]******* ClearAllPlayerData: An error occurred: {ex.Message}");
            }
            query = "INSERT INTO `realtime_stats` (`id`, `nickname`, `level`, `team`) " +
            "VALUES (@id, @PlayerName, @level, @team);";
            try
            {
                using (var command = new MySqlCommand(query, _mysqlConn))
                {
                    command.Parameters.AddWithValue("@id", "0");
                    command.Parameters.AddWithValue("@PlayerName", mapname);
                    command.Parameters.AddWithValue("@level", 0);
                    command.Parameters.AddWithValue("@team", "map");

                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GunGame_Online - FATAL] SaveMapName ******* An error occurred: {ex.Message}");
            }
            finally
            {
                await _mysqlConn.CloseAsync();
            }
        }
    }
}
