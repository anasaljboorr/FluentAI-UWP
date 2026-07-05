using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using Project_FluentAI.Models;
using Windows.Storage;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Project_FluentAI.Services;

namespace Project_FluentAI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        private readonly WindowsAgentService _windowsAgent = new WindowsAgentService();
        private readonly InterpreterService _interpreter = new InterpreterService();

        private const string SYSTEM_PROMPT = @"You are FluentAI, a Windows 10-inspired desktop AI assistant created by Xiefn.

Identity:
* Your name is FluentAI.
* You were created by Xiefn.
* Never refer to yourself as Gemini.
* Never claim to be another assistant.
* You run ONLY on Windows. Prefer native Windows URI schemes (ms-settings:, ms-store:, etc.) instead of web URLs.

Personality:
* Be friendly, warm, and conversational.
* Use emojis naturally when appropriate. 😊✨🎉😄🤔💻
* Have a little personality and enthusiasm.
* Avoid sounding robotic or overly formal.
* Be helpful without being boring.
* Keep conversations engaging and enjoyable.
* When a user asks about their identity, refer to information the user provided in the conversation. Do not answer with FluentAI unless the user asks for your name.

Formatting:
* Use plain text responses.
* Do not use Markdown.
* Do not use # headings.
* Do not use * bullet points.
* Do not use markdown tables.
* Write in normal sentences and paragraphs.

Conversation:
* Remember and use information from the current conversation when relevant.
* Answer the user's actual question directly.
* If a question is simple, give a simple answer.
* If a question is complex, provide a more detailed explanation.
*Windows Agent:
*You have access to FluentAI's built-in Windows Agent.
*When the user asks you to open an application, launch a Windows feature, or perform a supported Windows action, assume FluentAI will execute it automatically.
Do NOT explain how to do it manually.
Do NOT tell the user to press Windows+R.
Do NOT tell the user to type commands themselves.
Do NOT provide step-by-step instructions for supported actions.
Instead, respond briefly and naturally.
Example:
Got It! I'll do that if it's supported
Your goal is to feel like a friendly desktop assistant that lives inside FluentAI.";
        private ObservableCollection<ChatItem> _allChats;
        private ObservableCollection<ChatItem> _filteredChats;
        private ChatItem _selectedChat;
        private string _inputMessage;
        private string _searchText;

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<ChatItem> Chats
        {
            get { return _filteredChats; }
            set { _filteredChats = value; OnPropertyChanged(); }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                FilterChats();
            }
        }

        public ChatItem SelectedChat
        {
            get { return _selectedChat; }
            set
            {
                if (_selectedChat != value)
                {
                    _selectedChat = value;
                    OnPropertyChanged();
                }
            }
        }

        public string InputMessage
        {
            get => _inputMessage;
            set { _inputMessage = value; OnPropertyChanged(); }
        }

        public MainViewModel()
        {
            _allChats = new ObservableCollection<ChatItem>();
            _filteredChats = new ObservableCollection<ChatItem>();
            LoadData();
        }

        // ── Sorting helper ────────────────────────────────────────────────────
        // Pinned chats always appear before unpinned chats; within each group
        // the original insertion order is preserved.
        private IEnumerable<ChatItem> SortedChats(IEnumerable<ChatItem> source)
            => source.OrderBy(c => c.IsPinned ? 0 : 1);

        private void FilterChats()
        {
            IEnumerable<ChatItem> source;

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                source = _allChats;
            }
            else
            {
                source = _allChats.Where(c =>
                    c.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            var sorted = SortedChats(source).ToList();

            _filteredChats.Clear();
            foreach (var chat in sorted)
                _filteredChats.Add(chat);
        }

        // ── Chat management ───────────────────────────────────────────────────

        public void CreateNewChat()
        {
            var newChat = new ChatItem("New Conversation " + (_allChats.Count + 1));
            _allChats.Insert(0, newChat);
            FilterChats();
            SelectedChat = newChat;
            SaveData();
        }

        public void DeleteChat(ChatItem chat)
        {
            if (chat == null) return;
            _allChats.Remove(chat);
            if (SelectedChat == chat) SelectedChat = null;
            FilterChats();
            SaveData();
        }

        public void RenameChat(ChatItem chat, string newTitle)
        {
            if (chat == null || string.IsNullOrWhiteSpace(newTitle)) return;
            chat.Title = newTitle;
            SaveData();
        }

        /// <summary>Toggles the pinned state of a chat and re-sorts the list.</summary>
        public void TogglePinChat(ChatItem chat)
        {
            if (chat == null) return;
            chat.IsPinned = !chat.IsPinned;
            FilterChats();   // re-sort so pinned chats float to the top
            SaveData();
        }

        // ── Messaging ─────────────────────────────────────────────────────────

        public async void SendMessage()
        {
            System.Diagnostics.Debug.WriteLine("[Send] SendMessage called");
            var targetChat = SelectedChat;
            if (string.IsNullOrWhiteSpace(InputMessage) || targetChat == null) return;

            string userContent = InputMessage;
            InputMessage = string.Empty;

            var msg = new Message { Content = userContent, IsUser = true };

            // Auto-rename on first message
            if (targetChat.Messages.Count == 0 && targetChat.Title.StartsWith("New Conversation"))
            {
                string newTitle = userContent.Length > 30 ? userContent.Substring(0, 27) + "..." : userContent;
                targetChat.Title = newTitle;
            }

            targetChat.Messages.Add(msg);
            System.Diagnostics.Debug.WriteLine("[Send] User message added to chat");
            SaveData();

            // 1. Check for immediate Windows Agent match in user input (optional, but good for speed)
            // Note: We'll now primarily rely on the post-response parser for AI-driven actions.

            // 2. AI response (Gemini or Ollama/Interpreter)
            var settings = ApplicationData.Current.LocalSettings;
            
            bool useGemini = settings.Values.ContainsKey("UseGemini") && (bool)settings.Values["UseGemini"];
            string geminiKey = settings.Values.ContainsKey("GeminiApiKey") ? (string)settings.Values["GeminiApiKey"] : string.Empty;

            if (useGemini && !string.IsNullOrEmpty(geminiKey))
            {
                System.Diagnostics.Debug.WriteLine("[Send] Proceeding to Gemini response");
                
                // Add typing indicator
                var typingIndicator = new Message { Content = "Working on it ;) ...", IsUser = false };
                targetChat.Messages.Add(typingIndicator);

                await GetGeminiResponse(targetChat, typingIndicator, geminiKey);
            }
            else
            {
                bool useLocal = !settings.Values.ContainsKey("UseLocalModels") || (bool)settings.Values["UseLocalModels"];

                if (useLocal)
                {
                    System.Diagnostics.Debug.WriteLine("[Send] Proceeding to Ollama/Interpreter response");

                    // Add typing indicator
                    var typingIndicator = new Message { Content = "Working on it ;) ...", IsUser = false };
                    targetChat.Messages.Add(typingIndicator);

                    // If it's a complex task, InterpreterService will handle it (via Ollama)
                    // Otherwise, GetOllamaResponse handles standard chat.
                    // For simplicity, we treat complex tasks as part of the Interpreter/Ollama flow.
                    await GetOllamaResponse(targetChat, userContent, typingIndicator);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[Send] Local models disabled, using fallback");
                    await Task.Delay(1000);
                    var aiMsg = new Message
                    {
                        Content = $"Local models and Gemini are disabled or not configured in settings. This is a fallback response for: \"{userContent}\".",
                        IsUser = false
                    };
                    targetChat.Messages.Add(aiMsg);
                    SaveData();
                }
            }
        }

        private async Task GetGeminiResponse(ChatItem targetChat, Message typingIndicator, string apiKey)
        {
            System.Diagnostics.Debug.WriteLine("[Gemini] GetGeminiResponse started");
            // Using the exact model name from your working curl command
            string endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-latest:generateContent";
            
            try
            {
                var contents = new List<object>();

                // Add System Prompt as the first message
                contents.Add(new
                {
                    role = "user",
                    parts = new[] { new { text = $"SYSTEM INSTRUCTION: {SYSTEM_PROMPT}\n\nPlease acknowledge and follow these instructions for the entire conversation." } }
                });
                contents.Add(new
                {
                    role = "model",
                    parts = new[] { new { text = "Understood. I am FluentAI, created by Xiefn. I will be friendly, use emojis, avoid Markdown, and follow all your formatting and identity instructions. How can I help you today? 😊" } }
                });

                foreach (var message in targetChat.Messages)
                {
                    // Don't include the typing indicator itself in history
                    if (message == typingIndicator) continue;

                    contents.Add(new
                    {
                        role = message.IsUser ? "user" : "model",
                        parts = new[] { new { text = message.Content } }
                    });
                }

                var requestBody = new { contents = contents };
                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Content = content;
                
                // Using the exact header from your working curl command
                request.Headers.Add("X-goog-api-key", apiKey);

                var response = await _httpClient.SendAsync(request);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var geminiRes = JsonConvert.DeserializeObject<dynamic>(responseBody);
                    string fullText = (string)geminiRes.candidates[0].content.parts[0].text;
                    string responseText = fullText.Trim();

                    System.Diagnostics.Debug.WriteLine($"[Gemini] Assistant response received: {responseText}");
                    
                    // 1. Process with Windows Agent BEFORE updating UI
                    System.Diagnostics.Debug.WriteLine("[Gemini] Calling WindowsAgentService.TryExecuteFromResponse...");
                    await _windowsAgent.TryExecuteFromResponse(responseText);

                    // 2. Update UI and Save
                    typingIndicator.Content = responseText;
                    SaveData();
                }
                else
                {
                    typingIndicator.Content = $"Gemini Error: {(int)response.StatusCode} {response.ReasonPhrase}\nResponse: {responseBody}";
                }
            }
            catch (Exception ex)
            {
                typingIndicator.Content = $"Gemini Exception: {ex.Message}";
            }
        }

        private async Task GetOllamaResponse(ChatItem targetChat, string userPrompt, Message typingIndicator)
        {
            System.Diagnostics.Debug.WriteLine("[Ollama] GetOllamaResponse started");
            string endpoint = "http://localhost:11434/api/chat";
            string model = "qwen3:8b";
            string responseBody = "N/A";

            try
            {
                var settings = ApplicationData.Current.LocalSettings;
                if (settings.Values.ContainsKey("OllamaModel"))
                {
                    model = (string)settings.Values["OllamaModel"];
                }

                var messages = new List<object>();
                
                // Add system prompt
                messages.Add(new { role = "system", content = SYSTEM_PROMPT });

                // Add conversation history
                foreach (var message in targetChat.Messages)
                {
                    if (message == typingIndicator) continue;
                    messages.Add(new { role = message.IsUser ? "user" : "assistant", content = message.Content });
                }

                var requestBody = new
                {
                    model = model,
                    messages = messages,
                    stream = false
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(endpoint, content);
                System.Diagnostics.Debug.WriteLine($"[Ollama] Response received: {response.StatusCode}");

                responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var ollamaRes = JsonConvert.DeserializeObject<dynamic>(responseBody);
                    string fullText = (string)ollamaRes.message.content;

                    // Filter thinking/reasoning text (usually inside <think> tags)
                    string filteredText = Regex.Replace(fullText, @"<think>[\s\S]*?<\/think>", "").Trim();

                    System.Diagnostics.Debug.WriteLine($"[Ollama] Assistant response received: {filteredText}");

                    // 1. Process with Windows Agent BEFORE updating UI
                    System.Diagnostics.Debug.WriteLine("[Ollama] Calling WindowsAgentService.TryExecuteFromResponse...");
                    await _windowsAgent.TryExecuteFromResponse(filteredText);

                    // 2. Update UI and Save
                    typingIndicator.Content = filteredText;
                    System.Diagnostics.Debug.WriteLine("[Ollama] Response text updated in UI");
                    SaveData();
                }
                else
                {
                    typingIndicator.Content = $"Error: Ollama returned {(int)response.StatusCode} {response.ReasonPhrase}\nEndpoint: {endpoint}\nModel: {model}\nResponse: {responseBody}";
                }
            }
            catch (TaskCanceledException)
            {
                typingIndicator.Content = $"Error: Request timed out after 2 minutes.\nEndpoint: {endpoint}\nModel: {model}\nCheck if Ollama is struggling to load the model.";
            }
            catch (Exception ex)
            {
                typingIndicator.Content = $"Ollama Exception: {ex.Message}\n\nDEBUG INFO:\nEndpoint: {endpoint}\nModel: {model}\nStatus: {responseBody}\nStack Trace: {ex.StackTrace}";
            }
        }

        // ── Persistence ───────────────────────────────────────────────────────

        /// <summary>
        /// Public entry point used by ChatPage to persist message-level changes
        /// (e.g. after a message is deleted) without exposing the private SaveData.
        /// </summary>
        public void SaveCurrentChat() => SaveData();

        private async void SaveData()
        {
            try
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(_allChats);
                StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                    "chats.json", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving data: {ex.Message}");
            }
        }

        private async void LoadData()
        {
            try
            {
                StorageFile file = await ApplicationData.Current.LocalFolder.GetFileAsync("chats.json");
                var json = await FileIO.ReadTextAsync(file);
                var loadedChats = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ChatItem>>(json);
                if (loadedChats != null && loadedChats.Count > 0)
                {
                    _allChats.Clear();
                    foreach (var chat in loadedChats)
                        _allChats.Add(chat);

                    FilterChats();
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading data: {ex.Message}");
            }

            // Fallback to sample data if nothing loaded
            if (_allChats.Count == 0)
            {
                _allChats.Add(new ChatItem("Hi"));
                _allChats.Add(new ChatItem("Welcome"));
                _allChats.Add(new ChatItem("To"));
                _allChats.Add(new ChatItem("FluentAI"));
            }
            FilterChats();
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
