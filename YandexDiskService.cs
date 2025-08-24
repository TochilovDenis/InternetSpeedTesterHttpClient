using System.Diagnostics;
using System.Net;

namespace InternetSpeedTesterHttpClient
{
    /// <summary>
    /// Сервис для работы с API Яндекс.Диска
    /// Обеспечивает взаимодействие с облачным хранилищем
    /// </summary>
    public class YandexDiskService
    {
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Конструктор сервиса работы с диском
        /// </summary>
        public YandexDiskService()
        {
            _httpClient = new HttpClient(); // HTTP клиент для API запросов
        }

        /// <summary>
        /// Получение URL для загрузки файла на Яндекс.Диск
        /// </summary>
        public async Task<string> GetUploadUrlAsync(string fileName, string accessToken, CancellationToken cancellationToken)
        {
            try
            {
                string path = fileName; // Загрузка в корень диска
                string apiUrl = $"https://cloud-api.yandex.net/v1/disk/resources/upload?path={Uri.EscapeDataString(path)}&overwrite=true";

                using (var request = new HttpRequestMessage(HttpMethod.Get, apiUrl))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", accessToken);

                    using (var response = await _httpClient.SendAsync(request, cancellationToken))
                    {
                        // Обработка ошибки 404 (папка приложения не существует)
                        if (response.StatusCode == HttpStatusCode.NotFound && path.StartsWith("app:/"))
                        {
                            await EnsureAppFolderExists(accessToken, cancellationToken); // Создание папки

                            // Повторный запрос после создания папки
                            using (var retryRequest = new HttpRequestMessage(HttpMethod.Get, apiUrl))
                            {
                                retryRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", accessToken);
                                using (var retryResponse = await _httpClient.SendAsync(retryRequest, cancellationToken))
                                {
                                    retryResponse.EnsureSuccessStatusCode();
                                    var _json = await retryResponse.Content.ReadAsStringAsync();
                                    var _jsonObj = Newtonsoft.Json.Linq.JObject.Parse(_json);
                                    return _jsonObj["href"]?.ToString(); // URL для загрузки
                                }
                            }
                        }

                        response.EnsureSuccessStatusCode();
                        var json = await response.Content.ReadAsStringAsync();
                        var jsonObj = Newtonsoft.Json.Linq.JObject.Parse(json);

                        if (jsonObj["href"] == null)
                            throw new Exception("Не получен URL для загрузки");

                        return jsonObj["href"].ToString();
                    }
                }
            }
            catch (Exception ex) { throw new Exception($"Ошибка получения URL для загрузки: {ex.Message}", ex); }
        }

        /// <summary>
        /// Создание папки приложения если она не существует
        /// </summary>
        public async Task EnsureAppFolderExists(string accessToken, CancellationToken cancellationToken)
        {
            try
            {
                string apiUrl = "https://cloud-api.yandex.net/v1/disk/resources?path=app:";
                using (var request = new HttpRequestMessage(HttpMethod.Put, apiUrl))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", accessToken);
                    request.Headers.Add("Accept", "application/json"); // Для корректного JSON
                    var response = await _httpClient.SendAsync(request, cancellationToken);
                    // 201 Created или 409 Conflict - оба означают успех
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Ошибка создания папки приложения: {ex.Message}"); }
        }

        /// <summary>
        /// Получение прямой ссылки для скачивания
        /// </summary>
        public async Task<string> GetDirectDownloadUrlAsync(string publicUrl, string accessToken, CancellationToken cancellationToken)
        {
            try
            {
                // Обработка локальных file:// URL
                if (publicUrl.StartsWith("file:///")) return publicUrl;

                // Если это уже прямая ссылка - возвращаем как есть
                if (publicUrl.StartsWith("https://getfile.dokpub.com/") || publicUrl.Contains("/download?") ||
                    publicUrl.EndsWith(".dat") || publicUrl.EndsWith(".txt") ||
                    publicUrl.EndsWith(".zip") || publicUrl.EndsWith(".rar")) return publicUrl;

                // Обработка публичных ссылок Яндекс.Диска
                if (publicUrl.Contains("disk.yandex.ru/d/"))
                {
                    // Попытка через официальное API с токеном
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        try
                        {
                            string apiUrl = $"https://cloud-api.yandex.net/v1/disk/public/resources/download?public_key={Uri.EscapeDataString(publicUrl)}";
                            using (var request = new HttpRequestMessage(HttpMethod.Get, apiUrl))
                            {
                                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", accessToken);
                                using (var response = await _httpClient.SendAsync(request, cancellationToken))
                                {
                                    if (response.IsSuccessStatusCode)
                                    {
                                        var json = await response.Content.ReadAsStringAsync();
                                        var jsonObj = Newtonsoft.Json.Linq.JObject.Parse(json);
                                        string directUrl = jsonObj["href"]?.ToString();
                                        if (!string.IsNullOrEmpty(directUrl)) return directUrl;
                                    }
                                }
                            }
                        }
                        catch { return publicUrl; } // Fallback на оригинальный URL
                    }

                    // Резервный метод через сервис dokpub (без токена)
                    try
                    {
                        string fileId = publicUrl.Split(new string[] { "disk.yandex.ru/d/" }, StringSplitOptions.None)[1];
                        // Очистка ID от лишних параметров
                        if (fileId.Contains('?')) fileId = fileId.Split('?')[0];
                        if (fileId.Contains('&')) fileId = fileId.Split('&')[0];
                        if (fileId.Contains('/')) fileId = fileId.Split('/')[0];

                        string dokpubUrl = $"https://getfile.dokpub.com/yandex/get/{fileId}";

                        // Проверка доступности резервной ссылки
                        using (var testClient = new HttpClient())
                        {
                            testClient.Timeout = TimeSpan.FromSeconds(5);
                            var testResponse = await testClient.GetAsync(dokpubUrl, cancellationToken);
                            if (testResponse.IsSuccessStatusCode) return dokpubUrl;
                        }
                    }
                    catch { return publicUrl; }
                }
            }
            catch { return publicUrl; } // Всегда возвращаем URL, даже при ошибках
            return publicUrl;
        }

        /// <summary>
        /// Преобразование публичной ссылки в URL для скачивания
        /// </summary>
        public string GetYandexDiskDownloadUrl(string publicUrl, string accessToken)
        {
            if (publicUrl.Contains("disk.yandex.ru/d/"))
            {
                string fileId = publicUrl.Split(new string[] { "disk.yandex.ru/d/" }, StringSplitOptions.None)[1];

                // Приоритет: официальное API с токеном
                if (!string.IsNullOrEmpty(accessToken))
                {
                    return $"https://cloud-api.yandex.net/v1/disk/public/resources/download?public_key={Uri.EscapeDataString(publicUrl)}";
                }
                else
                {
                    // Fallback: сервис dokpub без токена
                    return $"https://getfile.dokpub.com/yandex/get/{fileId}";
                }
            }
            return publicUrl.Replace("disk.yandex.ru", "getfile.dokpub.com/yandex");
        }

        /// <summary>
        /// Проверка существования файла по URL
        /// </summary>
        public async Task<bool> CheckFileExistsAsync(string url, string accessToken, CancellationToken cancellationToken)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Head, url))
                {
                    // Добавление токена для Яндекс API
                    if (!string.IsNullOrEmpty(accessToken) && url.Contains("yandex"))
                    {
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", accessToken);
                    }

                    var response = await _httpClient.SendAsync(request, cancellationToken);

                    // Файл считается существующим при различных успешных статусах
                    return response.StatusCode == HttpStatusCode.OK ||
                           response.StatusCode == HttpStatusCode.PartialContent ||
                           response.StatusCode == HttpStatusCode.Found ||
                           response.StatusCode == HttpStatusCode.Forbidden;
                }
            }
            catch { return false; } // При ошибках считаем что файла нет
        }

        // <summary>
        /// Проверка доступности файла через браузерный URL
        /// </summary>
        public async Task<bool> CheckYandexDiskFileAvailable(string url, CancellationToken cancellationToken)
        {
            try
            {
                if (url.Contains("disk.yandex.ru"))
                {
                    // Простой HTTP запрос для проверки доступности
                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(10);
                        var response = await client.GetAsync(url, cancellationToken);
                        return response.IsSuccessStatusCode;
                    }
                }
                return true; // Для не-Яндекс URL считаем доступным
            }
            catch { return false; }
        }

        /// <summary>
        /// Получение размера файла через HEAD запрос
        /// </summary>
        public async Task<long?> GetFileSizeAsync(string url, string accessToken, CancellationToken cancellationToken)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Head, url))
                {
                    if (!string.IsNullOrEmpty(accessToken) && url.Contains("yandex"))
                    {
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", accessToken);
                    }

                    using (var response = await _httpClient.SendAsync(request, cancellationToken))
                    {
                        if (response.IsSuccessStatusCode)
                            return response.Content.Headers.ContentLength; // Размер из заголовков
                        return null;
                    }
                }
            }
            catch { return null; } // При ошибках возвращаем null
        }
    }
}