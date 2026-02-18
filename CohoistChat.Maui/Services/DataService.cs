using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CohoistChat.Maui.Services
{
    public class DataService
    {
        HttpClient _client;
        IConfiguration _config;

        public DataService(IConfiguration config)
        {
            _config = config;
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("x-functions-key", config.GetValue<string>("FunctionsKey"));
        }
        public async Task<string> GetSecretAsync(string key)
        {
            var url = $"{_config.GetValue<string>("FunctionsUri")}?key={key}";
            var response = await _client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
    }
}
