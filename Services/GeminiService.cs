using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CineVerify.Services
{
    public class GeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _baseUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-pro";

        public GeminiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["GeminiApi:ApiKey"];
        }

        public async Task<string> GeneratePersonalizedRecommendationsAsync(
            string userId,
            string[] favoriteGenres,
            string[] favoriteMovies,
            int[] userRatings)
        {
            try
            {
                var genresText = string.Join(", ", favoriteGenres);
                var moviesText = string.Join(", ", favoriteMovies);
                var ratingsText = string.Join(", ", userRatings);

                string prompt = $@"
Sei un esperto di cinema. Genera consigli personalizzati per l'utente.
I generi preferiti dell'utente sono: {genresText}.
I film che l'utente ha apprezzato includono: {moviesText}.
Le valutazioni recenti dell'utente sono: {ratingsText}.

Fornisci consigli personalizzati in base a questi dati. Sii gentile, informativo e mostra entusiasmo. 
Inizia con un saluto personalizzato, quindi fornisci 3-5 suggerimenti di film basati sui gusti dell'utente,
spiegando brevemente perché consigliamo ogni film. Concludi con una frase di incoraggiamento.
Limita la risposta a massimo 4 paragrafi.
";

                var requestPayload = new
                {
                    contents = new[]
                    {
                        new { role = "user", parts = new[] { new { text = prompt } } }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        maxOutputTokens = 800,
                        topP = 0.8,
                        topK = 40
                    }
                };

                var requestContent = new StringContent(
                    JsonSerializer.Serialize(requestPayload),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}:generateContent?key={_apiKey}",
                    requestContent);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var responseData = JsonSerializer.Deserialize<JsonElement>(content);

                    try
                    {
                        var generatedText = responseData
                            .GetProperty("candidates")[0]
                            .GetProperty("content")
                            .GetProperty("parts")[0]
                            .GetProperty("text")
                            .GetString();

                        return generatedText ?? "Non sono disponibili consigli personalizzati al momento.";
                    }
                    catch
                    {
                        return "Non sono disponibili consigli personalizzati al momento.";
                    }
                }
                else
                {
                    return "Non sono disponibili consigli personalizzati al momento.";
                }
            }
            catch (Exception)
            {
                return "Non sono disponibili consigli personalizzati al momento. Riprova più tardi.";
            }
        }

        public async Task<string> AnalyzeSentimentAsync(string text)
        {
            try
            {
                string prompt = $@"
Analizza il sentimento del seguente testo in italiano. Rispondi solo con una delle seguenti parole: 'Positivo', 'Negativo', o 'Neutro'.

Testo:
{text}

Classificazione:
";

                var requestPayload = new
                {
                    contents = new[]
                    {
                        new { role = "user", parts = new[] { new { text = prompt } } }
                    },
                    generationConfig = new
                    {
                        temperature = 0.2,
                        maxOutputTokens = 10,
                        topP = 0.95,
                        topK = 40
                    }
                };

                var requestContent = new StringContent(
                    JsonSerializer.Serialize(requestPayload),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}:generateContent?key={_apiKey}",
                    requestContent);

                if (response.IsSuccessStatusCode)
                {
                    var responseData = await JsonSerializer.DeserializeAsync<JsonElement>(
                        await response.Content.ReadAsStreamAsync());

                    var generatedText = responseData
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString() ?? "";

                    // Pulisci e normalizza il risultato
                    generatedText = generatedText.Trim().ToLower();

                    if (generatedText.Contains("positivo"))
                        return "Positivo";
                    else if (generatedText.Contains("negativo"))
                        return "Negativo";
                    else
                        return "Neutro";
                }
                return "Neutro";
            }
            catch
            {
                return "Neutro";
            }
        }

        public async Task<string> GenerateMovieAnalysisAsync(string title, string plot, string[] genres, string releaseYear)
        {
            try
            {
                string genresText = string.Join(", ", genres);

                string prompt = $@"
Genera un'analisi del film in italiano. Fornisci informazioni interessanti e approfondimenti sul film.

Titolo: {title}
Anno: {releaseYear}
Generi: {genresText}
Trama: {plot}

Crea un'analisi in 3-4 paragrafi che includa:
1. Un'introduzione sul film e il suo background/contesto.
2. Un'analisi tematica e una discussione su cosa rende questo film interessante.
3. L'impatto culturale o sociale del film, se pertinente.
4. Una conclusione con un giudizio equilibrato.

L'analisi deve essere scritta in modo professionale ma accessibile, adatta a lettori che potrebbero non aver visto il film. Non raccontare l'intera trama.
";

                var requestPayload = new
                {
                    contents = new[]
                    {
                        new { role = "user", parts = new[] { new { text = prompt } } }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        maxOutputTokens = 1000,
                        topP = 0.8,
                        topK = 40
                    }
                };

                var requestContent = new StringContent(
                    JsonSerializer.Serialize(requestPayload),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}:generateContent?key={_apiKey}",
                    requestContent);

                if (response.IsSuccessStatusCode)
                {
                    var responseData = await JsonSerializer.DeserializeAsync<JsonElement>(
                        await response.Content.ReadAsStreamAsync());

                    var generatedText = responseData
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();

                    return generatedText ?? "Non è stato possibile generare un'analisi.";
                }

                return "Non è stato possibile generare un'analisi.";
            }
            catch (Exception ex)
            {
                return $"Si è verificato un errore durante la generazione dell'analisi: {ex.Message}";
            }
        }
    }
}