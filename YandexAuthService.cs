using System.Diagnostics;
using System.Net;

namespace InternetSpeedTesterHttpClient
{
    /// <summary>
    /// Сервис для OAuth аутентификации с Яндекс
    /// Управляет процессом получения и валидации токенов доступа
    /// </summary>
    public class YandexAuthService
    {
        // OAuth credentials приложения (зарегистрировать в Яндекс.OAuth)
        private readonly string _clientId = " ";
        private readonly string _clientSecret = " ";
        private readonly HttpClient _httpClient; // HTTP клиент для запросов

        // Свойства для хранения токена и времени его истечения
        public string AccessToken { get; private set; }
        public DateTime TokenExpiration { get; private set; }

        /// <summary>
        /// Конструктор сервиса аутентификации
        /// </summary>
        public YandexAuthService()
        {
            _httpClient = new HttpClient();
            LoadCredentials(); // Загрузка сохраненных учетных данных
        }

        /// <summary>
        /// Загрузка токенов из настроек приложения
        /// </summary>
        private void LoadCredentials()
        {
            AccessToken = Properties.Settings.Default.AccessToken;
            TokenExpiration = Properties.Settings.Default.TokenExpiration;
        }

        /// <summary>
        /// Сохранение токенов в настройки приложения
        /// </summary>
        public void SaveCredentials()
        {
            Properties.Settings.Default.AccessToken = AccessToken;
            Properties.Settings.Default.TokenExpiration = TokenExpiration;
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Сброс учетных данных
        /// </summary>
        public void ResetCredentials()
        {
            AccessToken = null;
            TokenExpiration = DateTime.MinValue;
            SaveCredentials();
        }

        /// <summary>
        /// Основной метод аутентификации через OAuth code flow
        /// </summary>
        public async Task<bool> AuthenticateWithCodeAsync(CancellationToken cancellationToken)
        {
            // Проверка существующего токена на валидность
            if (!string.IsNullOrEmpty(AccessToken) && DateTime.Now < TokenExpiration)
            {
                // Двойная проверка через разные API
                if (await ValidateYandexTokenAsync(cancellationToken) || await ValidateTokenAsync(cancellationToken))
                {
                    return true; // Токен еще валиден
                }
                else
                {
                    ResetCredentials(); // Токен невалиден, сброс
                }
            }

            try
            {
                // Шаг 1: Формирование URL для запроса кода авторизации
                string authUrl = $"https://oauth.yandex.ru/authorize?response_type=code&client_id={_clientId}";

                // Открытие браузера для пользовательской авторизации
                Process.Start(new ProcessStartInfo { FileName = authUrl, UseShellExecute = true });

                // Запрос кода авторизации у пользователя
                string authCode = Microsoft.VisualBasic.Interaction.InputBox(
                    "Пожалуйста, авторизуйтесь в браузере и введите полученный код:",
                    "Авторизация Яндекс", "");

                if (string.IsNullOrEmpty(authCode))
                    return false; // Пользователь отменил ввод

                // Шаг 2: Обмен кода авторизации на токен доступа
                using (var client = new HttpClient())
                {
                    // Формирование данных для POST запроса
                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("grant_type", "authorization_code"),
                        new KeyValuePair<string, string>("code", authCode),
                        new KeyValuePair<string, string>("client_id", _clientId),
                        new KeyValuePair<string, string>("client_secret", _clientSecret)
                    });

                    // Отправка запроса к токен endpoint
                    var response = await client.PostAsync("https://oauth.yandex.ru/token", content, cancellationToken);
                    response.EnsureSuccessStatusCode(); // Проверка успешности

                    // Парсинг JSON ответа
                    var json = await response.Content.ReadAsStringAsync();
                    var tokenData = Newtonsoft.Json.Linq.JObject.Parse(json);

                    // Извлечение данных токена
                    AccessToken = tokenData["access_token"]?.ToString();
                    int expiresIn = tokenData["expires_in"]?.ToObject<int>() ?? 3600; // 1 час по умолчанию
                    TokenExpiration = DateTime.Now.AddSeconds(expiresIn);

                    SaveCredentials(); // Сохранение нового токена
                    return true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка авторизации: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// Проверка токена через API Яндекс.Паспорта
        /// </summary>
        public async Task<bool> ValidateTokenAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(AccessToken)) return false;

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, "https://login.yandex.ru/info"))
                {
                    // Добавление токена в заголовок Authorization
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", AccessToken);
                    using (var response = await _httpClient.SendAsync(request, cancellationToken))
                    {
                        return response.IsSuccessStatusCode; // 200 OK = валидный токен
                    }
                }
            }
            catch { return false; } // Любая ошибка = невалидный токен
        }

        /// <summary>
        /// Проверка валидности токена через API Яндекс.Диска
        /// </summary>
        public async Task<bool> ValidateYandexTokenAsync(CancellationToken cancellationToken)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, "https://cloud-api.yandex.net/v1/disk"))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", AccessToken);
                    using (var response = await _httpClient.SendAsync(request, cancellationToken))
                    {
                        return response.IsSuccessStatusCode;
                    }
                }
            }
            catch { return false; }
        }

        /// <summary>
        /// Проверка прав доступа (scopes) токена
        /// </summary>
        public async Task CheckTokenScopes(CancellationToken cancellationToken)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, "https://cloud-api.yandex.net/v1/disk"))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", AccessToken);

                    using (var response = await _httpClient.SendAsync(request, cancellationToken))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            Debug.WriteLine("Токен имеет доступ к Яндекс Диску");
                            await TestWritePermissions(cancellationToken); // Дополнительная проверка прав записи
                        }
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Ошибка проверки прав: {ex.Message}"); }
        }

        /// <summary>
        /// Тестирование прав на запись путем создания тестовой папки
        /// </summary>
        private async Task TestWritePermissions(CancellationToken cancellationToken)
        {
            try
            {
                // Создание уникального имени папки
                string testFolderPath = "test_write_permission_" + DateTime.Now.Ticks;
                string apiUrl = $"https://cloud-api.yandex.net/v1/disk/resources?path={Uri.EscapeDataString(testFolderPath)}";

                using (var request = new HttpRequestMessage(HttpMethod.Put, apiUrl))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", AccessToken);

                    using (var response = await _httpClient.SendAsync(request, cancellationToken))
                    {
                        // 201 Created или 409 Conflict (уже существует) = права есть
                        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict)
                        {
                            Debug.WriteLine("Токен имеет права на запись");
                            await DeleteTestFolder(testFolderPath, cancellationToken); // Уборка после теста
                        }
                        else { Debug.WriteLine("Токен НЕ имеет прав на запись"); }
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Ошибка проверки прав записи: {ex.Message}"); }
        }

        /// <summary>
        /// Удаление тестовой папки
        /// </summary
        private async Task DeleteTestFolder(string folderPath, CancellationToken cancellationToken)
        {
            try
            {
                string apiUrl = $"https://cloud-api.yandex.net/v1/disk/resources?path={Uri.EscapeDataString(folderPath)}&permanently=true";
                using (var request = new HttpRequestMessage(HttpMethod.Delete, apiUrl))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", AccessToken);
                    await _httpClient.SendAsync(request, cancellationToken);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Ошибка удаления тестовой папки: {ex.Message}"); }
        }
    }
}