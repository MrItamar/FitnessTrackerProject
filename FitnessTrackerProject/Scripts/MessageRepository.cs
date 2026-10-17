using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;

namespace FitnessTrackerProject.Scripts
{
    public class MessageItem
    {
        public int MessageID { get; set; }
        public int SenderID { get; set; }
        public string SenderName { get; set; }
        public string MessageText { get; set; }
        public DateTime SentDate { get; set; }
        public string DisplayDate { get { return SentDate.ToString("MMM d, HH:mm"); } }
        public bool IsMine { get; set; } // Helps the UI align messages left or right
    }

    public static class MessageRepository
    {
        public static void SendMessage(int senderId, int receiverId, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            DatabaseHelper.ExecuteNonQuery(
                "INSERT INTO Messages (SenderID, ReceiverID, MessageText, SentDate) VALUES (?, ?, ?, ?)",
                p =>
                {
                    p.AddWithValue("?", senderId);
                    p.AddWithValue("?", receiverId);
                    p.AddWithValue("?", text.Trim());
                    p.Add("?", OleDbType.Date).Value = DateTime.Now;
                });
        }

        public static List<MessageItem> GetConversation(int currentUserId, int otherUserId)
        {
            var list = new List<MessageItem>();

            // Gets all messages where these two users are the sender and receiver, sorted oldest to newest
            string sql = @"
                SELECT [Messages].[MessageID], [Messages].[SenderID], [Messages].[MessageText], [Messages].[SentDate], [Users].[Username] 
                FROM [Messages]
                INNER JOIN [Users] ON [Messages].[SenderID] = [Users].[ID]
                WHERE ([Messages].[SenderID] = ? AND [Messages].[ReceiverID] = ?) 
                   OR ([Messages].[SenderID] = ? AND [Messages].[ReceiverID] = ?)
                ORDER BY [Messages].[SentDate] ASC";

            DataTable dt = DatabaseHelper.ExecuteQueryTable(sql, p =>
            {
                p.AddWithValue("?", currentUserId);
                p.AddWithValue("?", otherUserId);
                p.AddWithValue("?", otherUserId);
                p.AddWithValue("?", currentUserId);
            });

            foreach (DataRow row in dt.Rows)
            {
                int sender = Convert.ToInt32(row["SenderID"]);
                list.Add(new MessageItem
                {
                    MessageID = Convert.ToInt32(row["MessageID"]),
                    SenderID = sender,
                    SenderName = row["Username"].ToString(),
                    MessageText = row["MessageText"].ToString(),
                    SentDate = Convert.ToDateTime(row["SentDate"]),
                    IsMine = (sender == currentUserId)
                });
            }

            return list;
        }
    }
}