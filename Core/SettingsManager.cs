using System;
using System.IO;
using System.Text.Json;

namespace QRCoderZpkd1_Link.Core
{

  /// Модель данных для хранения пользовательских настроек.
  public class UserSettings
  {
    // Nullable, чтобы при первом запуске окно могло отцентрироваться по умолчанию.
    public int? WindowLeft { get; set; }
    public int? WindowTop { get; set; }

    // По умолчанию приложение запускается с темной темой.
    public string Theme { get; set; } = "DarkTheme";

    // Если язык пустой, LanguageManager сам определит язык системы.
    public string Language { get; set; } = string.Empty;
  }

  /// Управляет загрузкой и сохранением пользовательских настроек.
  /// Класс не содержит платформенной логики. Путь к файлу может быть задан приложением-хостом.
  public static class SettingsManager
  {
    private const string SettingsFileName = "UserSetting.json";
    private const string ApplicationFolderName = "QRCoderZpkd1_Link";

    private static string _settingsFilePath;

    /// Текущие активные настройки.
    public static UserSettings Current { get; private set; } = new UserSettings();

    /// Устанавливает путь к файлу пользовательских настроек.
    /// Этот метод позволяет UI-хосту определить собственную политику хранения файлов, не помещая платформенную логику в Core.
    public static void SetSettingsFilePath(string filePath)
    {
      if (string.IsNullOrWhiteSpace(filePath))
        throw new ArgumentException(
          "Путь к файлу настроек не может быть пустым.",
          nameof(filePath));

      _settingsFilePath = filePath;
    }

    /// Возвращает путь к файлу настроек.
    /// Если хост приложения явно не задал путь, используется стандартная пользовательская папка ApplicationData.
    private static string GetSettingsFilePath()
    {
      if (!string.IsNullOrWhiteSpace(_settingsFilePath))
        return _settingsFilePath;

      string appDataPath =
        Environment.GetFolderPath(
          Environment.SpecialFolder.ApplicationData);

      string appFolder =
        Path.Combine(appDataPath, ApplicationFolderName);

      return Path.Combine(appFolder, SettingsFileName);
    }

    /// Загружает настройки из UserSetting.json.
    public static void Load()
    {
      try
      {
        string filePath = GetSettingsFilePath();

        if (!File.Exists(filePath))
        {
          Current = new UserSettings();
          return;
        }

        string json = File.ReadAllText(filePath);

        Current =
          JsonSerializer.Deserialize<UserSettings>(json)
          ?? new UserSettings();
      }
      catch
      {
        // При поврежденном файле или ошибке доступа продолжаем работу с настройками по умолчанию.
        Current = new UserSettings();
      }
    }

    /// Сохраняет текущие настройки в UserSetting.json.
    public static void Save()
    {
      try
      {
        string filePath = GetSettingsFilePath();

        string directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
          Directory.CreateDirectory(directory);
        }

        var options = new JsonSerializerOptions
        {
          WriteIndented = true
        };

        string json =
          JsonSerializer.Serialize(Current, options);

        File.WriteAllText(filePath, json);
      }
      catch
      {
        // Ошибки записи не должны приводить к падению приложения.
      }
    }
  }
}
