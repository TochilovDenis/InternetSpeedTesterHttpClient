using System.Diagnostics;
using System.Net;

namespace InternetSpeedTesterHttpClient
{
    public partial class ISTHC : Form
    {
        public ISTHC()
        {
            InitializeComponent();
            CreateLocalTestFile(); // Создание локального тестового файла
            // Загрузка сохраненных токенов из настроек приложения
            _accessToken = Properties.Settings.Default.AccessToken;
            _tokenExpiration = Properties.Settings.Default.TokenExpiration;

            // Инициализация HttpClient с настройками
            _httpClient = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = true,        // Разрешение автоматических перенаправлений
                MaxAutomaticRedirections = 3,    // Максимальное количество перенаправлений
                UseProxy = true,                 // Использование системных настроек прокси
                Proxy = HttpClient.DefaultProxy // Использование системного прокси
            });

            // Добавление User-Agent для обхода ограничений
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(60); // Таймаут запросов
            _random = new Random(); // Инициализация генератора случайных чисел
        }

        // OAuth настройки
        private readonly string _clientId = "e86eba31e1fa43f882c6cc52f0f7ea8d";
        private readonly string _clientSecret = "ee4d7463dfbc4045b10c4df92a236e2d";
        private string _accessToken;       // Токен доступа
        private DateTime _tokenExpiration; // Время истечения токена

        // URL для тестирования скачивания
        private string[] downloadUrls = {
            "https://disk.yandex.ru/d/vt-pDfQ1qOFpWQ",
            "https://disk.yandex.ru/d/vAawlND_0pDyCg"
        };

        private HttpClient _httpClient;  // HTTP клиент для всех запросов
        private readonly Random _random;  // Генератор случайных чисел
        private CancellationTokenSource _cancellationTokenSource; // Источник токена отмены


        // ==================== ОБРАБОТЧИКИ СОБЫТИЙ ====================

        /// <summary>
        /// Обработчик нажатия кнопки начала теста скорости скачивания
        /// </summary>
        private async void StartTestButton_Click(object sender, EventArgs e)
        {
            // Блокировка кнопки во время выполнения теста
            StartTestButton.Enabled = false;
            CancelTestButton.Enabled = true;
            ProgressBar.Value = 0;
            StatusLabel.Text = "Подготовка...";

            // Создание токена отмены для возможности прерывания теста
            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;

            try
            {
                // Прохождение OAuth аутентификации
                if (!await AuthenticateWithCodeAsync(cancellationToken))
                {
                    StatusLabel.Text = "Авторизация отменена";
                    return;
                }

                // Измерение сетевой задержки (ping)
                StatusLabel.Text = "Измерение задержки (Ping)...";
                long pingMs = await MeasurePingAsync("https://yandex.ru", cancellationToken);
                PingResultLabel.Text = $"Ping: {pingMs} мс";

                // Измерение скорости скачивания
                StatusLabel.Text = "Тест скорости скачивания...";
                double downloadSpeed = await MeasureDownloadSpeedAsync(await GetRandomDownloadUrlAsync(cancellationToken), cancellationToken);
                DownloadResultLabel.Text = $"Скачивание: {downloadSpeed:F2} Мбит/с";

                StatusLabel.Text = "Тест завершен!";
            }
            catch (TaskCanceledException)
            {
                // Обработка отмены теста пользователем
                StatusLabel.Text = "Тест отменен.";
                ProgressBar.Value = 0;
                ProgressLabel.Text = "Прогресс: 0%";
            }
            catch (Exception ex)
            {
                // Обработка ошибок во время теста
                MessageBox.Show($"Ошибка во время теста: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                StatusLabel.Text = "Ошибка";
            }
            finally
            {
                // Восстановление состояния UI после завершения теста
                StartTestButton.Enabled = true;
                CancelTestButton.Enabled = false;
            }
        }

        /// <summary>
        /// Обработчик нажатия кнопки теста скорости загрузки на Яндекс.Диск
        /// </summary>
        private async void UploadTestButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Блокировка кнопок во время выполнения теста
                StartTestButton.Enabled = false;
                UploadTestButton.Enabled = false;
                StatusLabel.Text = "Подготовка к тесту загрузки...";

                // Проверка валидности токена доступа
                if (!await ValidateYandexTokenAsync(CancellationToken.None))
                {
                    MessageBox.Show("Токен невалиден. Требуется повторная авторизация.", "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Проверка прав доступа токена
                await CheckTokenScopes(CancellationToken.None);

                // Создание временного файла для теста загрузки (1MB)
                string testFilePath = CreateTestFile(1 * 1024 * 1024);

                try
                {
                    StatusLabel.Text = "Тест скорости загрузки...";

                    // Измерение скорости загрузки на Яндекс.Диск
                    double uploadSpeed = await MeasureUploadSpeedAsync(testFilePath, CancellationToken.None);
                    UploadResultLabel.Text = $"Загрузка: {uploadSpeed:F2} Мбит/с";

                    StatusLabel.Text = "Тест загрузки завершен!";
                }
                finally
                {
                    // Удаление временного файла после завершения теста
                    if (File.Exists(testFilePath))
                        File.Delete(testFilePath);

                    // Восстановление состояния UI
                    StartTestButton.Enabled = true;
                    UploadTestButton.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                // Обработка ошибок теста загрузки
                MessageBox.Show($"Ошибка теста загрузки: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                StatusLabel.Text = "Ошибка загрузки";
                StartTestButton.Enabled = true;
                UploadTestButton.Enabled = true;
            }
        }

        /// <summary>
        /// Обработчик нажатия кнопки отмены теста
        /// </summary>
        private void CancelTestButton_Click(object sender, EventArgs e)
        {
            _cancellationTokenSource?.Cancel(); // Отмена текущего теста
        }

        /// <summary>
        /// Обработчик нажатия кнопки повторной авторизации
        /// </summary>
        private async void ReAuthButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Сброс текущих учетных данных
                _accessToken = null;
                _tokenExpiration = DateTime.MinValue;
                Properties.Settings.Default.AccessToken = null;
                Properties.Settings.Default.TokenExpiration = DateTime.MinValue;
                Properties.Settings.Default.Save();

                // Запрос новой авторизации
                if (await AuthenticateWithCodeAsync(CancellationToken.None))
                {
                    MessageBox.Show("Повторная авторизация успешна!", "Успех",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                // Обработка ошибок повторной авторизации
                MessageBox.Show($"Ошибка повторной авторизации: {ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Обработчик закрытия формы - освобождение ресурсов
        /// </summary>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _httpClient?.Dispose(); // Освобождение ресурсов HttpClient
        }


        // ==================== МЕТОДЫ АУТЕНТИФИКАЦИИ И АВТОРИЗАЦИИ ====================

        /// <summary>
        /// Аутентификация с использованием OAuth кода через Яндекс OAuth
        /// </summary>
        private async Task<bool> AuthenticateWithCodeAsync(CancellationToken cancellationToken)
        {
            // Проверка существующего токена на валидность
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.Now < _tokenExpiration)
            {
                // Дополнительная проверка валидности токена через API
                if (await ValidateYandexTokenAsync(cancellationToken))
                {
                    return true;
                }
                else if (await ValidateTokenAsync(cancellationToken))
                {
                    return true;
                }
                else
                {
                    // Токен невалиден, очистка учетных данных
                    _accessToken = null;
                    _tokenExpiration = DateTime.MinValue;
                }
            }

            try
            {
                StatusLabel.Text = "Авторизация...";

                // Шаг 1: Формирование URL для запроса кода авторизации
                string authUrl = $"https://oauth.yandex.ru/authorize?response_type=code&client_id={_clientId}";

                // Открытие браузера для авторизации пользователя
                Process.Start(new ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true
                });

                // Запрос кода авторизации у пользователя
                string authCode = Microsoft.VisualBasic.Interaction.InputBox(
                    "Пожалуйста, авторизуйтесь в браузере и введите полученный код:",
                    "Авторизация Яндекс",
                    "");

                if (string.IsNullOrEmpty(authCode))
                    return false; // Пользователь отменил ввод

                // Шаг 2: Обмен кода авторизации на токен доступа
                StatusLabel.Text = "Получение токена...";

                // Обмен кода на токен
                using (var client = new HttpClient())
                {
                    // Формирование данных для запроса токена
                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("grant_type", "authorization_code"),
                        new KeyValuePair<string, string>("code", authCode),
                        new KeyValuePair<string, string>("client_id", _clientId),
                        new KeyValuePair<string, string>("client_secret", _clientSecret)
                    });

                    // Отправка POST-запроса для получения токена
                    var response = await client.PostAsync("https://oauth.yandex.ru/token", content, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    // Парсинг JSON ответа с данными токена
                    var json = await response.Content.ReadAsStringAsync();
                    var tokenData = Newtonsoft.Json.Linq.JObject.Parse(json);

                    // Извлечение данных токена из ответа
                    _accessToken = tokenData["access_token"]?.ToString();
                    int expiresIn = tokenData["expires_in"]?.ToObject<int>() ?? 3600;
                    _tokenExpiration = DateTime.Now.AddSeconds(expiresIn);

                    // Сохранение токена в настройках приложения
                    Properties.Settings.Default.AccessToken = _accessToken;
                    Properties.Settings.Default.TokenExpiration = _tokenExpiration;
                    Properties.Settings.Default.Save();

                    return true;
                }
            }
            catch (Exception ex)
            {
                // Обработка ошибок авторизации
                MessageBox.Show($"Ошибка авторизации: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        // ==================== МЕТОДЫ ВАЛИДАЦИЯ ТОКЕНОВ ====================
        /// <summary>
        /// Проверка валидности токена через API Яндекс.Паспорта
        /// </summary>
        private async Task<bool> ValidateTokenAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_accessToken))
                return false;

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, "https://login.yandex.ru/info"))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", _accessToken);
                    using (var response = await _httpClient.SendAsync(request, cancellationToken))
                    {
                        return response.IsSuccessStatusCode;
                    }
                }
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// Проверка валидности токена через API Яндекс.Диска
        /// </summary>
        private async Task<bool> ValidateYandexTokenAsync(CancellationToken cancellationToken)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, "https://cloud-api.yandex.net/v1/disk"))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", _accessToken);

                    using (var response = await _httpClient.SendAsync(request, cancellationToken))
                    {
                        return response.IsSuccessStatusCode;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Проверка прав доступа (scopes) токена
        /// </summary>
        private async Task CheckTokenScopes(CancellationToken cancellationToken)
        {
            try
            {
                // Запрос к API диска для проверки прав доступа
                using (var request = new HttpRequestMessage(HttpMethod.Get, "https://cloud-api.yandex.net/v1/disk"))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", _accessToken);

                    using (var response = await _httpClient.SendAsync(request, cancellationToken))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            Debug.WriteLine("Токен имеет доступ к Яндекс Диску");

                            // Дополнительная проверка прав на запись
                            await TestWritePermissions(cancellationToken);
                        }
                        else
                        {
                            string errorContent = await response.Content.ReadAsStringAsync();
                            Debug.WriteLine($"Ошибка доступа к диску: {response.StatusCode} - {errorContent}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка проверки прав: {ex.Message}");
            }
        }

        /// <summary>
        /// Тестирование прав на запись путем создания тестовой папки
        /// </summary>
        private async Task TestWritePermissions(CancellationToken cancellationToken)
        {
            try
            {
                // Создание уникального имени тестовой папки
                string testFolderPath = "test_write_permission_" + DateTime.Now.Ticks;
                string apiUrl = $"https://cloud-api.yandex.net/v1/disk/resources?path={Uri.EscapeDataString(testFolderPath)}";

                using (var request = new HttpRequestMessage(HttpMethod.Put, apiUrl))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", _accessToken);

                    using (var response = await _httpClient.SendAsync(request, cancellationToken))
                    {
                        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict)
                        {
                            Debug.WriteLine("Токен имеет права на запись");

                            // Удаление тестовой папки после проверки
                            await DeleteTestFolder(testFolderPath, cancellationToken);
                        }
                        else
                        {
                            Debug.WriteLine("Токен НЕ имеет прав на запись");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка проверки прав записи: {ex.Message}");
            }
        }

        /// <summary>
        /// Удаление тестовой папки с Яндекс.Диска
        /// </summary>
        private async Task DeleteTestFolder(string folderPath, CancellationToken cancellationToken)
        {
            try
            {
                string apiUrl = $"https://cloud-api.yandex.net/v1/disk/resources?path={Uri.EscapeDataString(folderPath)}&permanently=true";

                using (var request = new HttpRequestMessage(HttpMethod.Delete, apiUrl))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", _accessToken);
                    await _httpClient.SendAsync(request, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка удаления тестовой папки: {ex.Message}");
            }
        }

        // ==================== МЕТОДЫ РАБОТЫ С ЯНДЕКС ДИСКОМ ====================

        /// <summary>
        /// Получение URL для загрузки файла на Яндекс Диск
        /// </summary>
        private async Task<string> GetUploadUrlAsync(string fileName, CancellationToken cancellationToken)
        {
            try
            {
                // Использование корня диска для загрузки
                string path = fileName; // Для корня диска
                                        // string path = $"app:/{fileName}"; // Для папки приложения

                string apiUrl = $"https://cloud-api.yandex.net/v1/disk/resources/upload?path={Uri.EscapeDataString(path)}&overwrite=true";
                Debug.WriteLine($"Запрос URL для загрузки: {apiUrl}");

                using (var request = new HttpRequestMessage(HttpMethod.Get, apiUrl))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", _accessToken);

                    using (var response = await _httpClient.SendAsync(request, cancellationToken))
                    {
                        Debug.WriteLine($"Ответ: {response.StatusCode}");

                        if (!response.IsSuccessStatusCode)
                        {
                            string errorContent = await response.Content.ReadAsStringAsync();
                            Debug.WriteLine($"Ошибка: {errorContent}");

                            // Обработка ошибки 404 (не найдено)
                            if (response.StatusCode == HttpStatusCode.NotFound && path.StartsWith("app:/"))
                            {
                                await EnsureAppFolderExists(cancellationToken);

                                // Повторная попытка запроса после создания папки
                                using (var retryRequest = new HttpRequestMessage(HttpMethod.Get, apiUrl))
                                {
                                    retryRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", _accessToken);
                                    using (var retryResponse = await _httpClient.SendAsync(retryRequest, cancellationToken))
                                    {
                                        retryResponse.EnsureSuccessStatusCode();
                                        var _json = await retryResponse.Content.ReadAsStringAsync();
                                        var _jsonObj = Newtonsoft.Json.Linq.JObject.Parse(_json);
                                        return _jsonObj["href"]?.ToString();
                                    }
                                }
                            }

                            response.EnsureSuccessStatusCode(); // Выбросит исключение для других ошибок
                        }

                        var json = await response.Content.ReadAsStringAsync();
                        Debug.WriteLine($"Ответ JSON: {json}");

                        var jsonObj = Newtonsoft.Json.Linq.JObject.Parse(json);

                        if (jsonObj["href"] == null)
                            throw new Exception("Не получен URL для загрузки");

                        return jsonObj["href"].ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Исключение: {ex}");
                throw new Exception($"Ошибка получения URL для загрузки: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Создание папки приложения на Яндекс.Диске если она не существует
        /// </summary>
        private async Task EnsureAppFolderExists(CancellationToken cancellationToken)
        {
            try
            {
                string apiUrl = "https://cloud-api.yandex.net/v1/disk/resources?path=app:";

                using (var request = new HttpRequestMessage(HttpMethod.Put, apiUrl))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", _accessToken);

                    // Добавление заголовка для корректной обработки JSON
                    request.Headers.Add("Accept", "application/json");

                    var response = await _httpClient.SendAsync(request, cancellationToken);

                    // Обработка статусов (409 - конфликт, папка уже существует)
                    if (response.StatusCode != HttpStatusCode.Created &&
                        response.StatusCode != HttpStatusCode.Conflict)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        Debug.WriteLine($"Ошибка создания папки: {response.StatusCode} - {errorContent}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка создания папки приложения: {ex.Message}");
            }
        }

        // <summary>
        /// Получение прямой ссылки для скачивания файла с Яндекс.Диска
        /// </summary>
        private async Task<string> GetDirectDownloadUrlAsync(string publicUrl, CancellationToken cancellationToken)
        {
            try
            {
                Debug.WriteLine($"Получение прямой ссылки для: {publicUrl}");

                // Обработка локальных file:// URL
                if (publicUrl.StartsWith("file:///"))
                {
                    string filePath = publicUrl.Replace("file:///", "").Replace("/", "\\");
                    return publicUrl; // или обработайте локальный файл отдельно
                }

                // Если это уже прямая ссылка, возвращаем как есть
                if (publicUrl.StartsWith("https://getfile.dokpub.com/") ||
                    publicUrl.Contains("/download?") ||
                    publicUrl.EndsWith(".dat") || publicUrl.EndsWith(".txt") ||
                    publicUrl.EndsWith(".zip") || publicUrl.EndsWith(".rar"))
                {
                    return publicUrl;
                }

                // Обработка публичных ссылок Яндекс.Диска:
                if (publicUrl.Contains("disk.yandex.ru/d/"))
                {
                    // Попытка получения прямой ссылки через официальное API
                    if (!string.IsNullOrEmpty(_accessToken))
                    {
                        try
                        {
                            string apiUrl = $"https://cloud-api.yandex.net/v1/disk/public/resources/download?public_key={Uri.EscapeDataString(publicUrl)}";
                            Debug.WriteLine($"Запрос к API: {apiUrl}");

                            using (var request = new HttpRequestMessage(HttpMethod.Get, apiUrl))
                            {
                                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", _accessToken);

                                using (var response = await _httpClient.SendAsync(request, cancellationToken))
                                {
                                    if (response.IsSuccessStatusCode)
                                    {
                                        var json = await response.Content.ReadAsStringAsync();
                                        Debug.WriteLine($"Ответ API: {json}");

                                        var jsonObj = Newtonsoft.Json.Linq.JObject.Parse(json);
                                        string directUrl = jsonObj["href"]?.ToString();

                                        if (!string.IsNullOrEmpty(directUrl))
                                        {
                                            Debug.WriteLine($"Получена прямая ссылка: {directUrl}");
                                            return directUrl;
                                        }
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"Ошибка API: {response.StatusCode}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Ошибка получения прямой ссылки через API: {ex.Message}");
                            return publicUrl;
                        }
                    }

                    // Резервный метод для публичных файлов через сервис dokpub
                    try
                    {
                        string fileId = publicUrl.Split(new string[] { "disk.yandex.ru/d/" }, StringSplitOptions.None)[1];
                        // Очистка ID файла от параметров
                        if (fileId.Contains('?')) fileId = fileId.Split('?')[0];
                        if (fileId.Contains('&')) fileId = fileId.Split('&')[0];
                        if (fileId.Contains('/')) fileId = fileId.Split('/')[0];

                        string dokpubUrl = $"https://getfile.dokpub.com/yandex/get/{fileId}";
                        Debug.WriteLine($"Используем резервную ссылку: {dokpubUrl}");

                        // Проверка доступности резервной ссылки
                        using (var testClient = new HttpClient())
                        {
                            testClient.Timeout = TimeSpan.FromSeconds(5);
                            var testResponse = await testClient.GetAsync(dokpubUrl, cancellationToken);
                            if (testResponse.IsSuccessStatusCode)
                            {
                                return dokpubUrl;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Ошибка формирования резервной ссылки: {ex.Message}");
                    }

                    // Возврат оригинального URL если другие методы не сработали
                    return publicUrl;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Общая ошибка GetDirectDownloadUrlAsync: {ex.Message}");
                return publicUrl;
            }

            return publicUrl;
        }

        /// <summary>
        /// Выбор случайного URL из списка для тестирования скачивания
        /// </summary>
        private async Task<string> GetRandomDownloadUrlAsync(CancellationToken cancellationToken)
        {
            string publicUrl = downloadUrls[_random.Next(downloadUrls.Length)];

            // Проверка доступности файлов на Яндекс.Диске
            if (publicUrl.Contains("disk.yandex.ru"))
            {
                bool isAvailable = await CheckYandexDiskFileAvailable(publicUrl, cancellationToken);
                if (!isAvailable)
                {
                    Debug.WriteLine("Файл на Яндекс Диске недоступен, используем локальный файл");
                    return GetLocalTestFileUrl();
                }
            }

            return GetYandexDiskDownloadUrl(publicUrl);
        }

        /// <summary>
        /// Получение URL локального тестового файла
        /// </summary>
        private string GetLocalTestFileUrl()
        {
            string testFilePath = Path.Combine(Path.GetTempPath(), "test100mb.bin");
            return $"file:///{testFilePath.Replace("\\", "/")}";
        }

        /// <summary>
        /// Преобразование публичной ссылки Яндекс.Диска в URL для скачивания
        /// </summary>
        private string GetYandexDiskDownloadUrl(string publicUrl)
        {
            if (publicUrl.Contains("disk.yandex.ru/d/"))
            {
                string fileId = publicUrl.Split(new string[] { "disk.yandex.ru/d/" }, StringSplitOptions.None)[1];

                if (!string.IsNullOrEmpty(_accessToken))
                {
                    // Использование официального API с токеном
                    return $"https://cloud-api.yandex.net/v1/disk/public/resources/download?public_key={Uri.EscapeDataString(publicUrl)}";
                }
                else
                {
                    // Использование резервного сервиса без токена
                    return $"https://getfile.dokpub.com/yandex/get/{fileId}";
                }
            }
            return publicUrl.Replace("disk.yandex.ru", "getfile.dokpub.com/yandex");
        }

        /// <summary>
        /// Проверка существования файла по URL
        /// </summary>
        private async Task<bool> CheckFileExistsAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Head, url))
                {
                    // Добавление токена авторизации для запросов к Яндекс API
                    if (!string.IsNullOrEmpty(_accessToken) && url.Contains("yandex"))
                    {
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", _accessToken);
                    }

                    var response = await _httpClient.SendAsync(request, cancellationToken);

                    // Файл существует если: 200 OK, 206 Partial Content, 302 Redirect, или 403 Forbidden
                    return response.StatusCode == HttpStatusCode.OK ||
                           response.StatusCode == HttpStatusCode.PartialContent ||
                           response.StatusCode == HttpStatusCode.Found ||
                           response.StatusCode == HttpStatusCode.Forbidden;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка проверки файла: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Проверка доступности файла на Яндекс.Диске через браузерный URL
        /// </summary>
        private async Task<bool> CheckYandexDiskFileAvailable(string url, CancellationToken cancellationToken)
        {
            try
            {
                if (url.Contains("disk.yandex.ru"))
                {
                    // Проверка доступности через прямой HTTP запрос
                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(10);
                        var response = await client.GetAsync(url, cancellationToken);
                        return response.IsSuccessStatusCode;
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Получение размера файла по URL через HEAD запрос
        /// </summary>
        private async Task<long?> GetFileSizeAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Head, url))
                {
                    if (!string.IsNullOrEmpty(_accessToken) && url.Contains("yandex"))
                    {
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", _accessToken);
                    }

                    using (var response = await _httpClient.SendAsync(request, cancellationToken))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            return response.Content.Headers.ContentLength;
                        }
                        return null;
                    }
                }
            }
            catch
            {
                return null;
            }
        }


        // ==================== МЕТОДЫ ТЕСТИРОВАНИЯ СКОРОСТИ ====================

        /// <summary>
        /// Измерение ping (сетевой задержки) до указанного URL
        /// </summary>
        private async Task<long> MeasurePingAsync(string url, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Использование HEAD запроса для измерения задержки
                using (var request = new HttpRequestMessage(HttpMethod.Head, url))
                using (var response = await _httpClient.SendAsync(request, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    stopwatch.Stop();
                    return stopwatch.ElapsedMilliseconds;
                }
            }
            catch (HttpRequestException)
            {
                // Резервный метод: использование GET если HEAD не сработал
                stopwatch.Stop();
                stopwatch = Stopwatch.StartNew();
                using (var response = await _httpClient.GetAsync(url, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    stopwatch.Stop();
                    return stopwatch.ElapsedMilliseconds;
                }
            }
        }

        /// <summary>
        /// Измерение скорости скачивания файла
        /// </summary>
        private async Task<double> MeasureDownloadSpeedAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                Debug.WriteLine($"Начало измерения скорости для: {url}");

                // Обработка локальных файлов
                if (url.StartsWith("file:///"))
                {
                    return await MeasureLocalFileSpeed(url, cancellationToken);
                }

                // Получение прямой ссылки для скачивания
                string downloadUrl = await GetDirectDownloadUrlAsync(url, cancellationToken);
                Debug.WriteLine($"Прямая ссылка: {downloadUrl}");

                // Проверка существования файла
                bool fileExists = await CheckFileExistsAsync(downloadUrl, cancellationToken);
                Debug.WriteLine($"Файл существует: {fileExists}");

                if (!fileExists)
                {
                    // Попробуем без проверки, иногда HEAD запрос не работает
                    Debug.WriteLine("Пробуем скачать без проверки...");
                }

                // Определение размера файла
                long? fileSize = await GetFileSizeAsync(downloadUrl, cancellationToken);
                Debug.WriteLine($"Размер файла: {fileSize} bytes");

                if (!fileSize.HasValue || fileSize.Value == 0)
                {
                    // Установка размера по умолчанию если не удалось определить
                    fileSize = 100 * 1024 * 1024; // 100MB
                    Debug.WriteLine($"Используем размер по умолчанию: {fileSize} bytes");
                }

                // Добавление параметра cache для избежания кэширования
                string uniqueUrl = $"{downloadUrl}?cache={_random.Next()}";
                Debug.WriteLine($"Уникальный URL: {uniqueUrl}");

                Stopwatch stopwatch = new Stopwatch();
                long totalBytesRead = 0L;

                using (var response = await _httpClient.GetAsync(uniqueUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    Debug.WriteLine($"Статус ответа: {response.StatusCode}");

                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        throw new Exception("Файл не найден (404)");
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"Ошибка HTTP: {response.StatusCode}");
                    }

                    // Получение реального размера из заголовков ответа
                    long? contentLength = response.Content.Headers.ContentLength;
                    if (contentLength.HasValue && contentLength.Value > 0)
                    {
                        fileSize = contentLength.Value;
                        Debug.WriteLine($"Реальный размер из заголовков: {fileSize} bytes");
                    }

                    // Измерение скорости скачивания
                    stopwatch.Start();
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        int updateCounter = 0;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                        {
                            totalBytesRead += bytesRead;
                            cancellationToken.ThrowIfCancellationRequested();

                            // Обновление прогресса каждые 100КБ или при завершении
                            updateCounter++;
                            if (updateCounter % 10 == 0 || totalBytesRead == fileSize)
                            {
                                int progressPercentage = fileSize > 0 ?
                                    (int)((double)totalBytesRead / fileSize.Value * 100) : 0;

                                UpdateProgress(progressPercentage, totalBytesRead, stopwatch.Elapsed);
                            }
                        }
                    }
                    stopwatch.Stop();
                }
                // Расчет скорости в Мбит/с
                if (stopwatch.Elapsed.TotalSeconds <= 0)
                    return 0;

                double speedMbps = (totalBytesRead * 8) / (stopwatch.Elapsed.TotalSeconds * 1024 * 1024);
                Debug.WriteLine($"Скорость скачивания: {speedMbps:F2} Мбит/с");

                return speedMbps;
            }
            catch (HttpRequestException ex)
            {
                // Обработка HTTP ошибок с конкретными сообщениями
                Debug.WriteLine($"HTTP ошибка: {ex.Message}");
                switch (ex.StatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                        throw new Exception("Ошибка авторизации. Токен невалиден.");
                    case HttpStatusCode.Forbidden:
                        throw new Exception("Доступ запрещен.");
                    case HttpStatusCode.NotFound:
                        throw new Exception("Файл не найден.");
                    case HttpStatusCode.TooManyRequests:
                        throw new Exception("Слишком много запросов. Попробуйте позже.");
                    default:
                        throw new Exception($"HTTP ошибка: {ex.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Общая ошибка скачивания: {ex.Message}");
                throw new Exception($"Ошибка скачивания: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Измерение скорости чтения локального файла (для тестирования)
        /// </summary>
        private async Task<double> MeasureLocalFileSpeed(string fileUrl, CancellationToken cancellationToken)
        {
            try
            {
                string filePath = fileUrl.Replace("file:///", "").Replace("/", "\\");

                if (!File.Exists(filePath))
                {
                    throw new Exception("Локальный файл не найден");
                }

                FileInfo fileInfo = new FileInfo(filePath);
                long fileSize = fileInfo.Length;
                Debug.WriteLine($"Размер локального файла: {fileSize} bytes");

                Stopwatch stopwatch = new Stopwatch();
                long totalBytesRead = 0L;

                using (var fileStream = File.OpenRead(filePath))
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    int updateCounter = 0;

                    stopwatch.Start();
                    while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        totalBytesRead += bytesRead;
                        cancellationToken.ThrowIfCancellationRequested();

                        // Обновление прогресса чтения
                        updateCounter++;
                        if (updateCounter % 10 == 0 || totalBytesRead == fileSize)
                        {
                            int progressPercentage = fileSize > 0 ?
                                (int)((double)totalBytesRead / fileSize * 100) : 0;

                            UpdateProgress(progressPercentage, totalBytesRead, stopwatch.Elapsed);
                        }

                        // Имитация сетевой задержки для реалистичности теста
                        await Task.Delay(1, cancellationToken);
                    }
                    stopwatch.Stop();
                }
                // Расчет скорости чтения в Мбит/с
                if (stopwatch.Elapsed.TotalSeconds <= 0)
                    return 0;

                double speedMbps = (totalBytesRead * 8) / (stopwatch.Elapsed.TotalSeconds * 1024 * 1024);
                Debug.WriteLine($"Скорость чтения локального файла: {speedMbps:F2} Мбит/с");

                return speedMbps;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка измерения скорости локального файла: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Измерение скорости загрузки файла на Яндекс.Диск
        /// </summary>
        private async Task<double> MeasureUploadSpeedAsync(string filePath, CancellationToken cancellationToken)
        {
            try
            {
                // Генерация уникального имени файла
                string fileName = $"speedtest_{DateTime.Now:yyyyMMdd_HHmmss}.dat";

                // Получение URL для загрузки
                string uploadUrl = await GetUploadUrlAsync(fileName, cancellationToken);

                if (string.IsNullOrEmpty(uploadUrl))
                    throw new Exception("Не удалось получить URL для загрузки");

                Stopwatch stopwatch = new Stopwatch();
                long fileSize = new FileInfo(filePath).Length;

                // Загрузка файла с отслеживанием прогресса
                using (var fileStream = File.OpenRead(filePath))
                using (var content = new ProgressStreamContent(fileStream, fileSize,(bytesRead, totalRead, elapsed)
                    => UpdateUploadProgress(bytesRead, totalRead, elapsed, fileSize)))
                {
                    stopwatch.Start();

                    using (var response = await _httpClient.PutAsync(uploadUrl, content, cancellationToken))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            string errorContent = await response.Content.ReadAsStringAsync();
                            throw new Exception($"Ошибка загрузки: {response.StatusCode} - {errorContent}");
                        }

                        stopwatch.Stop();
                    }
                }
                // Расчет скорости загрузки в Мбит/с
                if (stopwatch.Elapsed.TotalSeconds <= 0)
                    return 0;

                return (fileSize * 8) / (stopwatch.Elapsed.TotalSeconds * 1024 * 1024);
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка измерения скорости загрузки: {ex.Message}", ex);
            }
        }


        // ==================== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ====================

        /// <summary>
        /// Создание временного тестового файла указанного размера
        /// </summary>
        private string CreateTestFile(long sizeInBytes)
        {
            string tempFile = Path.GetTempFileName();

            using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
            {
                byte[] buffer = new byte[8192];
                Random random = new Random();
                long bytesWritten = 0;

                // Заполнение файла случайными данными
                while (bytesWritten < sizeInBytes)
                {
                    random.NextBytes(buffer); 
                    int bytesToWrite = (int)Math.Min(buffer.Length, sizeInBytes - bytesWritten);
                    fileStream.Write(buffer, 0, bytesToWrite);
                    bytesWritten += bytesToWrite;
                }
            }

            return tempFile;
        }

        /// <summary>
        /// Создание локального тестового файла размером 100MB
        /// </summary>
        private void CreateLocalTestFile()
        {
            try
            {
                string testFilePath = Path.Combine(Path.GetTempPath(), "test100mb.bin");
                if (!File.Exists(testFilePath))
                {
                    using (var fileStream = new FileStream(testFilePath, FileMode.Create))
                    {
                        byte[] buffer = new byte[8192];
                        Random random = new Random();
                        long bytesToWrite = 100 * 1024 * 1024; // 100MB
                        long bytesWritten = 0;

                        // Создание файла с случайным содержимым
                        while (bytesWritten < bytesToWrite)
                        {
                            random.NextBytes(buffer);
                            int chunkSize = (int)Math.Min(buffer.Length, bytesToWrite - bytesWritten);
                            fileStream.Write(buffer, 0, chunkSize);
                            bytesWritten += chunkSize;
                        }
                    }
                }
                // НЕ перезаписываем downloadUrls, просто создаем файл для теста
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка создания тестового файла: {ex.Message}");
            }
        }


        /// <summary>
        /// Обновление прогресса скачивания в пользовательском интерфейсе
        /// </summary>
        private void UpdateProgress(int progressPercentage, long bytesRead, TimeSpan timeElapsed)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<int, long, TimeSpan>(UpdateProgress), progressPercentage, bytesRead, timeElapsed);
                return;
            }

            // Обновление прогресс - бара
            if (progressPercentage >= 0)
            {
                ProgressBar.Value = progressPercentage;
                ProgressLabel.Text = $"Прогресс: {progressPercentage}%";
            }

            // Обновление отображения текущей скорости
            if (timeElapsed.TotalSeconds > 0)
            {
                double currentSpeedMbps = (bytesRead * 8) / (timeElapsed.TotalSeconds * 1024 * 1024);
                SpeedLabel.Text = currentSpeedMbps < 0.01 ?
                    "Текущая скорость: < 0.01 Мбит/с" :
                    $"Текущая скорость: {currentSpeedMbps:F2} Мбит/с";
            }
            else
            {
                SpeedLabel.Text = "Текущая скорость: измеряется...";
            }
        }

        /// <summary>
        /// Обновление прогресса загрузки в пользовательском интерфейсе
        /// </summary>
        private void UpdateUploadProgress(long bytesRead, long totalBytesRead, TimeSpan timeElapsed, long totalFileSize)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<long, long, TimeSpan, long>(UpdateUploadProgress),
                       bytesRead, totalBytesRead, timeElapsed, totalFileSize);
                return;
            }

            // Расчет и отображение прогресса в процентах
            int progressPercentage = totalFileSize > 0 ?
                (int)((double)totalBytesRead / totalFileSize * 100) : 0;

            ProgressBar.Value = progressPercentage;
            ProgressLabel.Text = $"Прогресс: {progressPercentage}%";

            // Обновляем скорость в реальном времени
            if (timeElapsed.TotalSeconds > 0)
            {
                double currentSpeedMbps = (totalBytesRead * 8) / (timeElapsed.TotalSeconds * 1024 * 1024);
                SpeedLabel.Text = currentSpeedMbps < 0.01 ?
                    "Текущая скорость: < 0.01 Мбит/с" :
                    $"Текущая скорость: {currentSpeedMbps:F2} Мбит/с";
            }
        }

        /// <summary>
        /// Кастомный класс для отслеживания прогресса загрузки с callback'ом
        /// </summary>
        private class ProgressStreamContent : StreamContent
        {
            private readonly Stream _stream;
            private readonly long _totalSize;
            private readonly Action<long, long, TimeSpan> _progressCallback;
            private readonly Stopwatch _stopwatch;

            public ProgressStreamContent(Stream stream, long totalSize, Action<long, long, TimeSpan> progressCallback)
                : base(stream)
            {
                _stream = stream;
                _totalSize = totalSize;
                _progressCallback = progressCallback;
                _stopwatch = Stopwatch.StartNew();
            }

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;

                // Чтение и отправка данных с отслеживанием прогресса
                while ((bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await stream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;
                    _progressCallback?.Invoke(bytesRead, totalRead, _stopwatch.Elapsed);
                }
            }
        }
    }
}