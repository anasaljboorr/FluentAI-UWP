using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Project_FluentAI.Services
{
    public class InterpreterService
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        private const string OllamaEndpoint = "http://localhost:11434/api/chat";

        public async Task<string> ExecuteComplexTask(string input, string modelName)
        {
            try
            {
                var requestBody = new
                {
                    model = modelName,
                    messages = new[]
                    {
                        new { role = "system", content = "You are Open Interpreter running inside FluentAI on Windows. You can execute code and manage files. The user wants to perform a complex task." },
                        new { role = "user", content = input }
                    },
                    stream = false
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(OllamaEndpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<dynamic>(responseString);
                    return (string)result.message.content;
                }
                else
                {
                    return $"Interpreter Error: {response.ReasonPhrase}";
                }
            }
            catch (Exception ex)
            {
                return $"Interpreter Exception: {ex.Message}";
            }
        }
    }
}
