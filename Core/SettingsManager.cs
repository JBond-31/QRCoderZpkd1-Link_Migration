using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace QRCoderZpkd1_Link.Core
{
  /// <summary>
  /// Модель данных для хранения пользовательских настроек
  /// </summary>
  public class UserSettings
  {
    // Делаем координаты nullable (int?), чтобы при первом запуске (когда их еще нет) 
    // окно могло отцентрироваться по умолчанию
    public int? WindowLeft { get; set; }
    public int? WindowTop { get; set; }

    // По умолчанию приложение запускается с темной темой
    public string Theme { get; set; } = "DarkTheme";

    // Если язык пустой, LanguageManager сам подхватит язык системы
    public string Language { get; set; } = string.Empty;
  }

  /// <summary>
  /// Логический класс для управления сохранением и загрузкой настроек пользователя
  /// </summary>
  public static class SettingsManager
  {
    // Текущие активные настройки
    public static UserSettings Current { get; private set; } = new UserSettings();

    /// <summary>
    /// Динамически вычисляет путь к файлу конфигурации в зависимости от операционной системы.
    /// </summary>
    private static string GetSettingsFilePath()
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      {
        // На Windows приложение портативное: сохраняем строго рядом с исполняемым файлом .exe
        return Path.Combine(AppContext.BaseDirectory, "UserSetting.json");
      }
      else
      {
        // На Linux/macOS приложение инсталлируется: папка программы доступна только для чтения.
        // Сохраняем в папку пользователя ~/.config/QRCoderZpkd1_Link/ (или эквивалент ApplicationData)
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appFolder = Path.Combine(appDataPath, "QRCoderZpkd1_Link");

        if (!Directory.Exists(appFolder))
        {
          Directory.CreateDirectory(appFolder);
        }

        return Path.Combine(appFolder, "UserSetting.json");
      }
    }

    /// <summary>
    /// Загрузка настроек из файла UserSetting.json
    /// </summary>
    public static void Load()
    {
      try
      {
        string filePath = GetSettingsFilePath();
        if (File.Exists(filePath))
        {
          string json = File.ReadAllText(filePath);
          Current = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
        }
      }
      catch
      {
        // В случае повреждения файла или ошибки прав доступа создаем чистый дефолтный конфиг
        Current = new UserSettings();
      }
    }

    /// <summary>
    /// Сохранение текущих настроек в файл UserSetting.json
    /// </summary>
    public static void Save()
    {
      try
      {
        string filePath = GetSettingsFilePath();
        // Форматируем JSON с отступами, чтобы пользователю было удобно его читать при необходимости
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(Current, options);
        File.WriteAllText(filePath, json);
      }
      catch
      {
        // Игнорируем возможные ошибки записи (например, если нет прав)
      }
    }
  }
}