using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FitnessTrackerProject.Models;
using FitnessTrackerProject.Scripts;

namespace FitnessTrackerProject
{
    public partial class AIAnalysisWindow : Window
    {
        private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        private static readonly Random random = new Random();

        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";
        private const int MaxAttempts = 3;
        bool isTrainer;

        private static readonly string[] Models = { "gemini-3.8-flash", "gemini-3.5-flash", "gemini-3.1-flash-lite" };
        public AIAnalysisWindow()
        {
            InitializeComponent();
        }
        public AIAnalysisWindow(bool IsTrainer, string prefilledData = "")
        {
            InitializeComponent();
            this.isTrainer = IsTrainer;
            // Allow the caller to pre-fill the CSV-style data when opening the window
            if (!string.IsNullOrWhiteSpace(prefilledData))
            {
                DataInput.Text = prefilledData;
            }
            else
            {
                CoachTraineeItem traineeItem = CoachRepository.GetTraineeById(MainWindow.currentUserId);

                DataInput.Text = AIAnalysisWindow.BuildEfficientAIPrompt(traineeItem.TraineeId, traineeItem.Name, traineeItem.Goal, traineeItem.Details);
            }
        }

        // Helper method you can call from anywhere to build the highly-efficient CSV string
        public static string BuildEfficientAIPrompt(int traineeId, string traineeName, string traineeGoal, string traineeDetails)
        {
            var workouts = CoachRepository.GetTraineeWorkoutHistory(traineeId);
            var recentWorkouts = workouts.Take(25).ToList();

            if (recentWorkouts.Count == 0) return string.Empty;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Trainee Name: {traineeName}");
            sb.AppendLine($"Primary Goal: {traineeGoal}");
            sb.AppendLine($"Physical Details: {traineeDetails}"); // AI easily reads "Age 24 · 178cm · 72kg"

            sb.AppendLine("\nRecent Workout Data (Date | Exercise | Result | Quality):");
            sb.AppendLine("--------------------------------------------------");

            foreach (var w in recentWorkouts)
            {
                sb.AppendLine($"{w.DateText} | {w.ExerciseName} | {w.ResultDisplay} | Quality: {w.TrackingQuality}%");
            }

            return sb.ToString();
        }

        private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            string apiKey = ""; // Replace with your actual Gemini API Key A99Q.9999Ab8R9N6IncV999UHmxBcIq-vradf4dQS99999iEXSVj000000wF-FcpGCDX000Lg0007f000e5w
            string data = DataInput.Text;

            if (string.IsNullOrWhiteSpace(data))
            {
                MessageBox.Show("Please enter or load workout data first.", "Missing Data");
                return;
            }

            Button clickedButton = sender as Button;
            if (clickedButton != null) clickedButton.IsEnabled = false;

            ResultOutput.Text = "Analyzing data...";

            try
            {
                string prompt =
                    "You are an energetic, highly motivating AI fitness coach talking directly to a trainee right after their workout.\n\n" +
                    "Here is the trainee's recent history:\n" + data + "\n\n" +
                    "Provide a fun, encouraging debrief using these exactly three sections:\n" +
                    "1. 🎉 Milestone & Streak: Analyze the dates in the data. Detect and celebrate any consistency streaks (e.g., consecutive days logged, or consistent weekly frequency). Hype them up!\n" +
                    "2. 📈 Numbers Translated: Analyze their sets, reps, and weights. Calculate their total volume lifted or highlight a personal best. Translate these raw numbers into plain, exciting English (e.g., 'You lifted a total of 2,000kg today—that is the weight of a rhino!').\n" +
                    "3. 💡 Pro Tip: One gentle, highly actionable tip for their next session based on their tracking quality or exercise choice.\n\n" +
                    "Speak directly to them using 'You'. Keep it highly motivating, keep the sections concise, and base it strictly on the data provided.";
                if (isTrainer)
                {
                    prompt =
                        "You are an AI assistant helping a fitness coach quickly review a client's workout data.\n\n" +
                        "Here is the trainee's recent history:\n" + data + "\n\n" +
                        "Provide three concise sections formatted specifically for the coach:\n" +
                        "1. 🔍 Coach's Summary: A quick, bulleted analysis of their consistency and progression. You must explicitly flag Plateau Detection (e.g., stuck on the same weight/reps across multiple sessions) and Compliance & Effort (e.g., skipped days, rushing, or sandbagging sets based on low 'Tracking Quality' and timestamps).\n" +
                        "2. 🎯 Action Item: 1-2 specific program adjustments. You must suggest the exact mathematical progressive overload for next week (e.g., 'Suggest increasing Squat volume by 5%' or 'Add 2.5kg to Bench Press') to break any plateaus.\n" +
                        "3. 💬 Draft Message: A short, friendly, personalized chat message (written from the coach to the trainee) praising their specific wins and gently addressing a specific area for improvement. The coach will copy/paste this directly.\n\n" +
                        "Base conclusions strictly on the supplied data.";
                }

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[] { new { text = prompt } }
                        }
                    }
                };

                string json = JsonConvert.SerializeObject(requestBody);
                ResultOutput.Text = await AskGeminiAsync(apiKey, json);
            }
            catch (Exception ex)
            {
                ResultOutput.Text = "Error: " + ex.Message;
            }
            finally
            {
                if (clickedButton != null) clickedButton.IsEnabled = true;
            }
        }

        private async Task<string> AskGeminiAsync(string apiKey, string json)
        {
            string lastError = "No answer.";

            foreach (string model in Models)
            {
                for (int attempt = 1; attempt <= MaxAttempts; attempt++)
                {
                    try
                    {
                        using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + model + ":generateContent"))
                        {
                            request.Headers.Add("x-goog-api-key", apiKey);
                            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                            using (HttpResponseMessage response = await http.SendAsync(request))
                            {
                                string body = await response.Content.ReadAsStringAsync();

                                if (response.IsSuccessStatusCode)
                                {
                                    string text = (string)JObject.Parse(body).SelectToken("candidates[0].content.parts[0].text");
                                    if (string.IsNullOrWhiteSpace(text)) return "Gemini returned no answer.";
                                    return text;
                                }
                            }
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        lastError = "The request timed out.";
                    }
                    catch (HttpRequestException ex)
                    {
                        lastError = "Network problem: " + ex.Message;
                    }

                    // FIXED: attempt - 1 (Typo resolved)
                    if (attempt < MaxAttempts)
                    {
                        int waitMs = 1000 * (int)Math.Pow(2, attempt - 1) + random.Next(0, 500);
                        ResultOutput.Text = $"Gemini is busy, trying again... ({attempt}/{MaxAttempts})";
                        await Task.Delay(waitMs);
                    }
                }
            }

            return lastError + "\n\nGemini is probably overloaded right now. Please try again in a minute.";
        }
    }
}