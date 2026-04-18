using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace NexTierAI.Application.Services;

public class ChatMessage
{
    public string Sender { get; set; }
    public string Text { get; set; }
}

public class ChatSession
{
    public int Id { get; set; }
    public string Title { get; set; }
    public DateTime CreatedAt { get; set; }

    // SİHİRLİ DOKUNUŞ: Saati Türkiye (veya bulunduğun yerin) saatine çevirir ve şık yazar
    public string FormattedDate => CreatedAt.ToLocalTime().ToString("dd MMM yyyy - HH:mm");
}

public class ChatHistoryService
{
    private readonly string _dbPath;

    public ChatHistoryService()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _dbPath = Path.Combine(folder, "NexTierCore_v2.db");
        InitDatabase();
    }

    private void InitDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Sessions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            );
            CREATE TABLE IF NOT EXISTS Messages (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionId INTEGER NOT NULL,
                Sender TEXT NOT NULL,
                Content TEXT NOT NULL,
                FOREIGN KEY(SessionId) REFERENCES Sessions(Id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS UploadedFiles (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FileName TEXT NOT NULL
            );";
        command.ExecuteNonQuery();
    }

    public int CreateSession(string title)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Sessions (Title) VALUES ($title); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$title", title);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    // YENİ METOT: İstenilen sohbeti tamamen siler (Mesajlarıyla birlikte)
    public void DeleteSession(int sessionId)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Sessions WHERE Id = $id";
        command.Parameters.AddWithValue("$id", sessionId);
        command.ExecuteNonQuery();
    }

    public List<ChatSession> GetSessions(string searchQuery = "")
    {
        var list = new List<ChatSession>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        var command = connection.CreateCommand();

        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            command.CommandText = "SELECT Id, Title, CreatedAt FROM Sessions ORDER BY CreatedAt DESC";
        }
        else
        {
            command.CommandText = "SELECT Id, Title, CreatedAt FROM Sessions WHERE Title LIKE $query ORDER BY CreatedAt DESC";
            command.Parameters.AddWithValue("$query", $"%{searchQuery}%");
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new ChatSession
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                CreatedAt = reader.GetDateTime(2)
            });
        }
        return list;
    }

    public void SaveMessage(int sessionId, string sender, string content)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Messages (SessionId, Sender, Content) VALUES ($sessionId, $sender, $content)";
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$sender", sender);
        command.Parameters.AddWithValue("$content", content);
        command.ExecuteNonQuery();
    }

    public List<ChatMessage> GetHistory(int sessionId)
    {
        var list = new List<ChatMessage>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Sender, Content FROM Messages WHERE SessionId = $sessionId ORDER BY Id ASC";
        command.Parameters.AddWithValue("$sessionId", sessionId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new ChatMessage { Sender = reader.GetString(0), Text = reader.GetString(1) });
        }
        return list;
    }

    public void SaveFileRecord(string fileName)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO UploadedFiles (FileName) VALUES ($fileName)";
        command.Parameters.AddWithValue("$fileName", fileName);
        command.ExecuteNonQuery();
    }

    public List<string> GetUploadedFiles()
    {
        var list = new List<string>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT FileName FROM UploadedFiles ORDER BY Id DESC";
        using var reader = command.ExecuteReader();
        while (reader.Read()) { list.Add(reader.GetString(0)); }
        return list;
    }
}