using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace QRCoderZpkd1_Link.Core
{

  /// Модель для представления языка в меню.
  public class LanguageItem
  {
    public string Code { get; set; } // Имя JSON-файла без расширения, например "Russian".
    public string DisplayName { get; set; } // Обычный переводимый ключ "LanguageName", например "Русский".
    public string LanguageCode { get; set; } // Служебный ISO 639-1 код из ключа "LangCode", например "ru".
  }

  /// Причина, по которой загрузка или определение языка завершились с предупреждением. Core сообщает только тип проблемы, а UI самостоятельно формирует локализованный текст.
  public enum LanguageWarningType
  {
    /// Файл языка отсутствует.
    LanguageFileMissing,

    /// JSON-файл языка повреждён или имеет неправильный формат.
    InvalidJson,

    /// В JSON отсутствует обязательное служебное поле LangCode.
    MissingLanguageCode,

    /// Значение LangCode не соответствует ISO 639-1.
    InvalidLanguageCode,

    /// Файл языка не удалось прочитать.
    LanguageFileReadError,

    /// В переводе отсутствует обязательный ключ.
    MissingTranslationKey,

    /// Перевод существует, но значение пустое.
    EmptyTranslationValue
  }

  /// Предупреждение от LanguageManager, передаваемое из Core в UI.
  public sealed class LanguageWarning
  {
    /// Тип предупреждения.
    public LanguageWarningType Type { get; }

    /// Дополнительные сведения: имя файла, код языка или причина ошибки.
    public IReadOnlyList<string> Details { get; }

    public LanguageWarning(
        LanguageWarningType type,
        IEnumerable<string> details = null)
    {
      // Сохраняем тип предупреждения.
      Type = type;

      // Создаём независимую копию дополнительных сведений.
      Details =
          details != null
              ? new List<string>(details)
              : new List<string>();
    }
  }

  public static class LanguageManager
  {
    private static Dictionary<string, string> _translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static Dictionary<string, string> Translations => _translations;
    public static string CurrentLanguage { get; private set; } = "English";

    // Хранит пути файлов, которые уже были проверены в рамках текущего запуска.
    private static readonly HashSet<string> _validatedLanguageFiles =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// Событие предупреждения языка. Core не знает, какой UI будет показывать это предупреждение.
    public static event Action<LanguageWarning> WarningOccurred;

    public static void Initialize()
    {
      string detectedLanguage = GetSystemLanguage();
      SwitchLanguage(detectedLanguage);
    }

    /// Определяет язык операционной системы по ISO 639-1 коду и сопоставляет его с доступным языковым файлом.
    private static string GetSystemLanguage()
    {
      try
      {
        // Получаем двухбуквенный ISO 639-1 код текущего языка интерфейса ОС.
        string langCode = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        // Получаем языки из JSON-файлов каталога приложения.
        var available = GetAvailableLanguages();

        // Сопоставляем код ОС только со служебным LangCode из JSON.
        var match = available.FirstOrDefault(l =>
            string.Equals(l.LanguageCode, langCode, StringComparison.OrdinalIgnoreCase));

        if (match != null)
        {
          return match.Code;
        }
      }
      catch
      {
        // Ошибка определения языка: используем English; Core не зависит от WPF и не создаёт Toast напрямую.
      }

      // English остаётся безопасным языком по умолчанию.
      return "English";
    }

    /// Кроссплатформенный поиск папки с языками с защитой от регистра в Linux/macOS
    private static string GetLanguagesDirectory()
    {
      // Единственное допустимое расположение языковых файлов. WPF-проект копирует Assets/Language/*.json в каталог приложения как Language/*.json.
      return Path.Combine(AppContext.BaseDirectory, "Language");
    }

    /// Сканирует папку и возвращает список доступных языков с их родными названиями
    /// Сортировка: English всегда первый, остальные — строго по алфавиту имен файлов (Code) одинаково на всех .NET платформах.
    public static List<LanguageItem> GetAvailableLanguages()
    {
      var languages = new List<LanguageItem>();
      try
      {
        string dir = GetLanguagesDirectory();
        if (Directory.Exists(dir))
        {
          var files = Directory.GetFiles(dir, "*.json");
          foreach (var file in files)
          {
            string code = Path.GetFileNameWithoutExtension(file);
            if (!string.IsNullOrEmpty(code))
            {
              // Читаем обычное отображаемое имя языка из ключа LanguageName.
              string displayName = GetNativeNameFromFile(file) ?? code;

              // Читаем служебный ISO 639-1 код из ключа LangCode.
              string languageCode = GetLanguageCodeFromFile(file);

              // Отсутствующий LangCode делает файл непригодным для автоматического определения языка.
              if (string.IsNullOrWhiteSpace(languageCode))
              {
                NotifyWarning(
                    new LanguageWarning(
                        LanguageWarningType.MissingLanguageCode,
                        new[] { Path.GetFileName(file) }));

                languageCode = null;
              }

              // Добавляем язык с отдельными именем файла, отображаемым именем и ISO-кодом.
              languages.Add(new LanguageItem
              {
                Code = code,
                DisplayName = displayName,
                LanguageCode = languageCode
              });
            }
          }
        }
      }
      catch
      {
        // Игнорируем ошибки доступа к файловой системе
      }

      if (languages.Count == 0)
      {
        // Минимальный fallback содержит корректный ISO 639-1 код английского языка.
        languages.Add(new LanguageItem
        {
          Code = "English",
          DisplayName = "English",
          LanguageCode = "en"
        });
      }

      // Группируем по коду с учетом регистронезависимости, ставим English первым, а остальные сортируем по имени файла (Code) с использованием OrdinalIgnoreCase для идентичной работы в .NET Framework и .NET 10.
      return languages.GroupBy(l => l.Code, StringComparer.OrdinalIgnoreCase)
                      .Select(g => g.First())
                      .OrderBy(l => string.Equals(l.Code, "English", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                      .ThenBy(l => l.Code, StringComparer.OrdinalIgnoreCase)
                      .ToList();
    }

    /// Быстрое чтение только одного ключа "LanguageName" без полной десериализации словаря.
    private static string GetNativeNameFromFile(string path)
    {
      try
      {
        if (File.Exists(path))
        {
          string json = File.ReadAllText(path);
          using (JsonDocument doc = JsonDocument.Parse(json))
          {
            if (doc.RootElement.TryGetProperty("LanguageName", out JsonElement val))
            {
              return val.GetString();
            }
          }
        }
      }
      catch
      {
        // Файл поврежден или не является корректным JSON
      }
      return null;
    }

    /// Читает служебный ISO 639-1 код языка из ключа LangCode.
    private static string GetLanguageCodeFromFile(string path)
    {
      try
      {
        if (File.Exists(path))
        {
          // Читаем JSON и извлекаем только служебный код языка.
          string json = File.ReadAllText(path);
          using (JsonDocument doc = JsonDocument.Parse(json))
          {
            if (doc.RootElement.TryGetProperty("LangCode", out JsonElement val))
            {
              return val.GetString();
            }
          }
        }
      }
      catch
      {
        // Ошибка чтения LangCode: файл не используется для автоматического определения языка.
      }

      return null;
    }

    /// Один раз за запуск проверяет все JSON-файлы локализации.
    /// Метод не загружает переводы и не изменяет текущий язык.
    public static void ValidateLanguageFiles()
    {
      string dir = GetLanguagesDirectory();

      // Проверяем наличие единственного допустимого каталога Language.
      if (!Directory.Exists(dir))
      {
        NotifyWarning(
            new LanguageWarning(
                LanguageWarningType.LanguageFileMissing,
                new[] { "Language" }));

        return;
      }

      string[] files;

      try
      {
        // Получаем все JSON-файлы локализации из рабочего каталога.
        files = Directory.GetFiles(dir, "*.json");
      }
      catch
      {
        // Каталог существует, но его содержимое невозможно прочитать.
        NotifyWarning(
            new LanguageWarning(
                LanguageWarningType.LanguageFileReadError,
                new[] { "Language" }));

        return;
      }

      foreach (string file in files)
      {
        // Нормализуем путь, чтобы один файл не проверялся повторно.
        string fullPath = Path.GetFullPath(file);

        if (_validatedLanguageFiles.Contains(fullPath))
        {
          continue;
        }

        // Запоминаем файл как проверенный именно в текущем запуске.
        _validatedLanguageFiles.Add(fullPath);

        ValidateLanguageFile(fullPath);
      }
    }

    /// Проверяет один JSON-файл локализации.
    private static void ValidateLanguageFile(string path)
    {
      string fileName = Path.GetFileName(path);
      string json;

      try
      {
        // Читаем весь файл для проверки структуры JSON.
        json = File.ReadAllText(path);
      }
      catch
      {
        // Файл существует, но его невозможно прочитать.
        NotifyWarning(
            new LanguageWarning(
                LanguageWarningType.LanguageFileReadError,
                new[] { fileName }));

        return;
      }

      try
      {
        // Проверяем, что файл является корректным JSON-объектом.
        using (JsonDocument document = JsonDocument.Parse(json))
        {
          if (document.RootElement.ValueKind != JsonValueKind.Object)
          {
            NotifyWarning(
                new LanguageWarning(
                    LanguageWarningType.InvalidJson,
                    new[] { fileName }));

            return;
          }

          // Проверяем обязательный служебный код языка.
          if (!document.RootElement.TryGetProperty(
                  "LangCode",
                  out JsonElement langCodeProperty) ||
              langCodeProperty.ValueKind != JsonValueKind.String ||
              string.IsNullOrWhiteSpace(langCodeProperty.GetString()))
          {
            NotifyWarning(
                new LanguageWarning(
                    LanguageWarningType.MissingLanguageCode,
                    new[] { fileName }));

            return;
          }

          string langCode =
              langCodeProperty.GetString().Trim();

          // ISO 639-1 использует двухбуквенные коды.
          if (langCode.Length != 2 ||
              !langCode.All(char.IsLetter))
          {
            NotifyWarning(
                new LanguageWarning(
                    LanguageWarningType.InvalidLanguageCode,
                    new[]
                    {
                      fileName,
                      langCode
                    }));

            return;
          }
        }

        // Для English проверяем только структуру и LangCode.
        // Сравнивать English с самим собой не требуется.
        if (string.Equals(
                fileName,
                "English.json",
                StringComparison.OrdinalIgnoreCase))
        {
          return;
        }

        // Загружаем эталонный английский файл.
        string englishPath =
            Path.Combine(
                GetLanguagesDirectory(),
                "English.json");

        if (!File.Exists(englishPath))
        {
          return;
        }

        string englishJson =
            File.ReadAllText(englishPath);

        // Получаем все ключи эталонного английского перевода.
        Dictionary<string, string> englishTranslations =
            JsonSerializer.Deserialize<
                Dictionary<string, string>>(englishJson);

        if (englishTranslations == null)
        {
          return;
        }

        // Получаем переводы пользовательского языка.
        Dictionary<string, string> userTranslations =
            JsonSerializer.Deserialize<
                Dictionary<string, string>>(json);

        if (userTranslations == null)
        {
          return;
        }

        // Проверяем каждый ключ, который существует в английском переводе.
        foreach (var englishEntry in englishTranslations)
        {
          // Отсутствующий ключ требует использования английского fallback.
          if (!userTranslations.ContainsKey(englishEntry.Key))
          {
            NotifyWarning(
                new LanguageWarning(
                    LanguageWarningType.MissingTranslationKey,
                    new[]
                    {
                      fileName,
                      englishEntry.Key
                    }));

            continue;
          }

          // Пустое значение требует использования английского fallback.
          if (string.IsNullOrWhiteSpace(
                  userTranslations[englishEntry.Key]))
          {
            NotifyWarning(
                new LanguageWarning(
                    LanguageWarningType.EmptyTranslationValue,
                    new[]
                    {
                      fileName,
                      englishEntry.Key
                    }));
          }
        }
      }
      catch
      {
        // JSON невозможно разобрать или проверить.
        NotifyWarning(
            new LanguageWarning(
                LanguageWarningType.InvalidJson,
                new[] { fileName }));
      }
    }

    /// Переключает язык переводов в меню.
    public static void SwitchLanguage(string languageName)
    {
      // Очищаем предыдущие переводы перед загрузкой нового языка.
      _translations.Clear();

      // English используется как резервный перевод.
      LoadLanguageFile("English.json");

      // Если выбран не English, один раз проверяем выбранный файл и затем загружаем его.
      if (!string.Equals(
              languageName,
              "English",
              StringComparison.OrdinalIgnoreCase))
      {
        // Формируем путь к выбранному языковому файлу.
        string dir = GetLanguagesDirectory();
        string fileName = $"{languageName}.json";
        string path = Path.Combine(dir, fileName);

        // Ищем файл без учёта регистра имени.
        if (!File.Exists(path) && Directory.Exists(dir))
        {
          var matchedFile =
              Directory.GetFiles(dir, "*.json")
                  .FirstOrDefault(
                      f => Path.GetFileName(f).Equals(
                          fileName,
                          StringComparison.OrdinalIgnoreCase));

          if (matchedFile != null)
          {
            path = matchedFile;
          }
        }

        // Проверяем выбранный файл один раз, но результат проверки не блокирует загрузку.
        if (File.Exists(path))
        {
          ValidateLanguageFile(path);
        }
      }

      // Загружаем выбранный язык независимо от результата проверки.
      if (!string.Equals(
              languageName,
              "English",
              StringComparison.OrdinalIgnoreCase))
      {
        LoadLanguageFile($"{languageName}.json");
      }

      // Сохраняем выбранный язык как текущий.
      CurrentLanguage = languageName;
    }

    /// Кроссплатформенная загрузка файла перевода с защитой от регистра символов.
    /// Метод только загружает перевод и не выполняет повторную валидацию.
    private static void LoadLanguageFile(string fileName)
    {
      try
      {
        // Получаем единственный допустимый каталог языковых файлов.
        string dir = GetLanguagesDirectory();

        // Формируем путь к требуемому файлу.
        string path = Path.Combine(dir, fileName);

        // Ищем файл без учёта регистра имени.
        if (!File.Exists(path) && Directory.Exists(dir))
        {
          var matchedFile =
              Directory.GetFiles(dir, "*.json")
                  .FirstOrDefault(
                      f => Path.GetFileName(f).Equals(
                          fileName,
                          StringComparison.OrdinalIgnoreCase));

          if (matchedFile != null)
          {
            path = matchedFile;
          }
        }

        // Если файл отсутствует, сообщаем об этом и прекращаем загрузку.
        if (!File.Exists(path))
        {
          NotifyWarning(
              new LanguageWarning(
                  LanguageWarningType.LanguageFileMissing,
                  new[] { fileName }));

          return;
        }

        string json;

        try
        {
          // Читаем содержимое файла.
          json = File.ReadAllText(path);
        }
        catch
        {
          // Файл существует, но его невозможно прочитать.
          NotifyWarning(
              new LanguageWarning(
                  LanguageWarningType.LanguageFileReadError,
                  new[] { fileName }));

          return;
        }

        Dictionary<string, string> data;

        try
        {
          // Загружаем JSON в словарь переводов без дополнительной валидации LangCode.
          data =
          JsonSerializer.Deserialize<
              Dictionary<string, string>>(json);
        }
        catch
        {
          // Некорректный JSON невозможно загрузить.
          NotifyWarning(
              new LanguageWarning(
                  LanguageWarningType.InvalidJson,
                  new[] { fileName }));

          return;
        }

        // Защищаемся от неожиданного null после десериализации.
        if (data == null)
        {
          NotifyWarning(
              new LanguageWarning(
                  LanguageWarningType.InvalidJson,
                  new[] { fileName }));

          return;
        }

        // Проверяем пользовательский перевод относительно уже загруженного English fallback.
        // Пустые значения и отсутствующие ключи не заменяют английский перевод.
        if (!string.Equals(
            fileName,
            "English.json",
            StringComparison.OrdinalIgnoreCase))
        {
          // Проверяем каждый обязательный ключ английского перевода.
          foreach (var englishEntry in _translations.ToList())
          {
            // Отсутствующий ключ оставляем без изменения:
            // в _translations уже находится английское значение.
            if (!data.ContainsKey(englishEntry.Key))
            {
              continue;
            }

            // Пустое значение также оставляем без изменения:
            // используется английский fallback.
            if (string.IsNullOrWhiteSpace(
                    data[englishEntry.Key]))
            {
              continue;
            }

            // Корректный перевод заменяет английский fallback.
            _translations[englishEntry.Key] =
                data[englishEntry.Key];
          }

          // Сохраняем дополнительные ключи пользовательского перевода.
          foreach (var entry in data)
          {
            // Пустые значения не должны заменять существующий перевод.
            if (string.IsNullOrWhiteSpace(entry.Value))
            {
              continue;
            }

            // Добавляем дополнительные корректные ключи.
            if (!_translations.ContainsKey(entry.Key))
            {
              _translations[entry.Key] = entry.Value;
            }
          }

          return;
        }

        // Для English загружаем все его корректные значения.
        foreach (var entry in data)
        {
          // Пустые значения English не должны становиться переводом.
          if (string.IsNullOrWhiteSpace(entry.Value))
          {
            continue;
          }

          // Загружаем корректное английское значение.
          _translations[entry.Key] = entry.Value;
        }
      }
      catch
      {
        // Непредвиденная ошибка не должна ломать работу Core.
        NotifyWarning(
            new LanguageWarning(
                LanguageWarningType.LanguageFileReadError,
                new[] { fileName }));
      }
    }

    public static string GetString(string key)
    {
      if (string.IsNullOrEmpty(key)) return string.Empty;

      if (_translations.TryGetValue(key, out string translation))
      {
        return translation;
      }

      return $"[{key}]";
    }
    /// Передаёт предупреждение подписчикам UI. Ошибка UI-обработчика не должна ломать работу Core.
    private static void NotifyWarning(LanguageWarning warning)
    {
      try
      {
        // Core только передаёт предупреждение подписчикам.
        WarningOccurred?.Invoke(warning);
      }
      catch
      {
        // Ошибка UI-обработчика не должна прерывать работу LanguageManager.
      }
    }
  }
}