using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace QRCoderZpkd1_Link.Core
{
  /// Информация о модели часов. Этот класс содержит только данные и не зависит от WPF, Windows, Avalonia или любой другой UI-платформы.
  public class WatchModelInfo
  {
    /// Ключ модели из configurations.json.
    public string Key { get; set; } = string.Empty;

    /// Отображаемое название модели.
    public string Name { get; set; } = string.Empty;

    /// Ширина экрана из background.w.
    public int Width { get; set; }

    /// Высота экрана из background.h.
    public int Height { get; set; }

    /// Тип экрана.
    public string ScreenType { get; set; } = string.Empty;

    /// Возвращает название модели без префикса "Amazfit ". Это свойство используется существующим UI при формировании предпросмотра.
    public string CleanName =>
        Name.StartsWith(
            "Amazfit ",
            StringComparison.OrdinalIgnoreCase)
            ? Name.Substring(8).Trim()
            : Name;
  }

  /// Причина, по которой запись конфигурации была отклонена. Перечисление находится в Core и поэтому также не зависит от конкретной UI-платформы.
  public enum ConfigurationWarningType
  {
    /// Папка Data отсутствует.
    DataDirectoryMissing,

    /// Файл configurations.json отсутствует.
    ConfigurationFileMissing,

    /// JSON-файл повреждён или имеет неправильный формат.
    InvalidJson,

    /// Отдельная запись модели содержит некорректные данные.
    InvalidModel
  }

  /// Данные предупреждения, передаваемые из Core в UI. Core не знает, как именно UI будет показывать это сообщение.
  /// WPF может использовать Toast, Avalonia — собственный Toast, а другой UI может использовать другой механизм.
  public sealed class ConfigurationWarning
  {
    /// Тип предупреждения.
    public ConfigurationWarningType Type { get; }

    /// Дополнительные сведения о проблеме (имена некорректных моделей и причины их отклонения).
    public IReadOnlyList<string> Details { get; }

    public ConfigurationWarning(
        ConfigurationWarningType type,
        IEnumerable<string> details = null)
    {
      // Создаём предупреждение. Сохраняем тип проблемы с дополнительными сведениями.
      Type = type;

      // Создаём независимую копию списка.
      // Это защищает объект предупреждения от изменения исходного списка после его создания.
      Details =
          details != null
              ? new List<string>(details)
              : new List<string>();
    }
  }

  /// Загружает configurations.json и формирует данные, необходимые приложению для выбора разрешения и модели часов.
  /// Единственный источник конфигурации:
  /// Data/configurations.json рядом с исполняемым файлом приложения (Windows).
  public static class ConfigManager
  {
    /// Имя обязательной папки приложения. Используется именно это имя с заглавной буквы. Никаких альтернативных названий не поддерживаем.
    private const string DataDirectoryName = "Data";

    /// Имя обязательного файла конфигурации.
    private const string ConfigurationFileName = "configurations.json";

    /// Разрешённые типы экранов. Неизвестный тип означает некорректную запись, потому что приложение не умеет построить для него существующий список разрешений.
    private static readonly HashSet<string> SupportedScreenTypes =
        new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
                "round",
                "square",
                "bar"
        };

    /// Событие предупреждения конфигурации. Core только сообщает о проблеме. Сам Toast создаётся UI-проектом.
    public static event Action<ConfigurationWarning> WarningOccurred;

    /// Последний успешно обработанный путь и время изменения файла. Это позволяет не показывать одно и то же предупреждение/ при каждом повторном вызове GetResolutions().
    private static string _lastLoadedPath;

    private static DateTime _lastLoadedWriteTimeUtc =
        DateTime.MinValue;

    /// Кэш загруженных моделей. Null означает, что конфигурация ещё не загружалась или предыдущая попытка была неуспешной.
    private static Dictionary<string, WatchModelInfo> _cachedModels;

    /// Возвращает путь к обязательной папке Data. Путь строится относительно каталога приложения. AppContext.BaseDirectory является кроссплатформенным API .NET.
    public static string GetDataDirectoryPath()
    {
      return Path.Combine(
          AppContext.BaseDirectory,
          DataDirectoryName);
    }

    /// Возвращает путь к обязательному configurations.json.
    public static string GetConfigFilePath()
    {
      return Path.Combine(
          GetDataDirectoryPath(),
          ConfigurationFileName);
    }

    /// Загружает все модели из configurations.json. Каждая модель проверяется отдельно. Если одна запись неправильная, она пропускается, но остальные корректные записи продолжают загружаться.
    public static Dictionary<string, WatchModelInfo> LoadModels()
    {
      string configPath = GetConfigFilePath();

      // 1. Проверяем обязательную папку Data.
      string dataDirectory = GetDataDirectoryPath();

      if (!Directory.Exists(dataDirectory))
      {
        //1. Сообщаем UI только тип проблемы. Сам текст будет получен UI из LanguageManager на текущем языке приложения.
        NotifyWarning(
            new ConfigurationWarning(
                ConfigurationWarningType.DataDirectoryMissing));

        return new Dictionary<string, WatchModelInfo>(
            StringComparer.OrdinalIgnoreCase);
      }

      // 2. Проверяем обязательный configurations.json.
      if (!File.Exists(configPath))
      {
        NotifyWarning(
    new ConfigurationWarning(
        ConfigurationWarningType.ConfigurationFileMissing));

        return new Dictionary<string, WatchModelInfo>(
            StringComparer.OrdinalIgnoreCase);
      }

      // 3. Проверяем, не изменился ли файл. Пользователь может заменить configurations.json новой версией без перекомпиляции приложения.
      DateTime writeTimeUtc;

      try
      {
        writeTimeUtc =
            File.GetLastWriteTimeUtc(configPath);
      }
      catch
      {
        writeTimeUtc = DateTime.MinValue;
      }

      if (_cachedModels != null &&
          string.Equals(
              _lastLoadedPath,
              configPath,
              StringComparison.Ordinal) &&
          _lastLoadedWriteTimeUtc == writeTimeUtc)
      {
        // Файл не изменился. Возвращаем уже проверенные данные.
        return new Dictionary<string, WatchModelInfo>(
            _cachedModels,
            StringComparer.OrdinalIgnoreCase);
      }

      // 4. Читаем JSON.
      string json;

      try
      {
        json = File.ReadAllText(configPath);
      }
      catch
      {
        NotifyWarning(
            new ConfigurationWarning(
                ConfigurationWarningType.InvalidJson));

        return new Dictionary<string, WatchModelInfo>(
            StringComparer.OrdinalIgnoreCase);
      }

      // 5. Разбираем JSON.
      var models =
          new Dictionary<string, WatchModelInfo>(
              StringComparer.OrdinalIgnoreCase);

      try
      {
        using (JsonDocument document =
               JsonDocument.Parse(json))
        {
          // Корневой элемент должен быть JSON-объектом.
          if (document.RootElement.ValueKind !=
              JsonValueKind.Object)
          {
            NotifyWarning(
                new ConfigurationWarning(
                    ConfigurationWarningType.InvalidJson));

            return models;
          }

          // Здесь собираем названия всех неправильных моделей, чтобы показать пользователю ОДИН Toast, а не десятки отдельных уведомлений.
          var invalidModels =
              new List<string>();

          foreach (JsonProperty property
                   in document.RootElement.EnumerateObject())
          {
            // Проверяем отдельную запись.
            if (TryCreateModelInfo(
                    property,
                    out WatchModelInfo model,
                    out string reason))
            {
              // Корректная запись добавляется.
              models[property.Name] = model;
            }
            else
            {
              // Некорректная запись полностью игнорируется.
              invalidModels.Add(
                  $"{property.Name}: {reason}");
            }
          }

          // Если есть хотя бы одна неправильная модель, показываем одно сводное предупреждение.
          if (invalidModels.Count > 0)
          {
            NotifyWarning(
                new ConfigurationWarning(
                    ConfigurationWarningType.InvalidModel,
                    invalidModels));
          }
        }
      }
      catch
      {
        NotifyWarning(
            new ConfigurationWarning(
                ConfigurationWarningType.InvalidJson));

        return new Dictionary<string, WatchModelInfo>(
            StringComparer.OrdinalIgnoreCase);
      }

      // 6. Обновляем кэш. Если пользователь заменил configurations.json, следующий вызов автоматически получит новую дату изменения и загрузит новый файл.
      _cachedModels = models;

      _lastLoadedPath = configPath;

      _lastLoadedWriteTimeUtc =
          writeTimeUtc;

      return new Dictionary<string, WatchModelInfo>(
          models,
          StringComparer.OrdinalIgnoreCase);
    }

    /// Проверяет отдельную запись configurations.json и создаёт WatchModelInfo только если запись полностью корректна.
    private static bool TryCreateModelInfo(
        JsonProperty property,
        out WatchModelInfo model,
        out string reason)
    {
      model = null;
      reason = string.Empty;

      // Ключ записи не может быть пустым.
      if (string.IsNullOrWhiteSpace(property.Name))
      {
        reason = "пустое имя записи";
        return false;
      }

      JsonElement value = property.Value;

      // Каждая модель должна быть JSON-объектом.
      if (value.ValueKind != JsonValueKind.Object)
      {
        reason = "запись модели не является объектом JSON";
        return false;
      }

      // name
      if (!value.TryGetProperty(
              "name",
              out JsonElement nameProperty) ||
          nameProperty.ValueKind != JsonValueKind.String)
      {
        reason = "отсутствует поле name";
        return false;
      }

      string name =
          nameProperty.GetString();

      if (string.IsNullOrWhiteSpace(name))
      {
        reason = "поле name пустое";
        return false;
      }

      // screenType
      if (!value.TryGetProperty(
              "screenType",
              out JsonElement screenTypeProperty) ||
          screenTypeProperty.ValueKind !=
              JsonValueKind.String)
      {
        reason = "отсутствует поле screenType";
        return false;
      }

      string screenType =
          screenTypeProperty.GetString()?.Trim();

      if (string.IsNullOrWhiteSpace(screenType))
      {
        reason = "поле screenType пустое";
        return false;
      }

      // Проверяем, умеет ли приложение работать с указанным типом экрана.
      if (!SupportedScreenTypes.Contains(screenType))
      {
        reason =
            $"неподдерживаемый screenType '{screenType}'";

        return false;
      }

      // background
      if (!value.TryGetProperty(
              "background",
              out JsonElement backgroundProperty) ||
          backgroundProperty.ValueKind !=
              JsonValueKind.Object)
      {
        reason = "отсутствует объект background";
        return false;
      }

      // background.w
      if (!backgroundProperty.TryGetProperty(
              "w",
              out JsonElement widthProperty) ||
          widthProperty.ValueKind != JsonValueKind.Number ||
          !widthProperty.TryGetInt32(out int width) ||
          width <= 0)
      {
        reason =
            "background.w отсутствует или имеет " +
            "некорректное значение";

        return false;
      }

      // background.h
      if (!backgroundProperty.TryGetProperty(
              "h",
              out JsonElement heightProperty) ||
          heightProperty.ValueKind != JsonValueKind.Number ||
          !heightProperty.TryGetInt32(out int height) ||
          height <= 0)
      {
        reason =
            "background.h отсутствует или имеет " +
            "некорректное значение";

        return false;
      }

      // Создаём модель только после прохождения ВСЕХ проверок.
      model = new WatchModelInfo
      {
        Key = property.Name,
        Name = name.Trim(),
        Width = width,
        Height = height,
        ScreenType = screenType.ToLowerInvariant()
      };

      return true;
    }

    /// Формирует список уникальных разрешений.
    /// Существующий порядок сохраняется:
    /// Round → Square → Bar.
    /// Внутри типа: от большего разрешения к меньшему.
    public static List<string> GetResolutions()
    {
      var models =
          LoadModels().Values;

      var uniqueItems =
          models
              .Select(m => new
              {
                H = m.Height,
                W = m.Width,
                Type = m.ScreenType.ToLowerInvariant()
              })
              .Distinct()
              .ToList();

      // Определяем приоритет типов экранов.
      int GetTypePriority(string type)
      {
        if (type == "round")
        {
          return 0;
        }

        if (type == "square")
        {
          return 1;
        }

        return 2;
      }

      return uniqueItems
          .OrderBy(x => GetTypePriority(x.Type))
          .ThenByDescending(x => x.H)
          .ThenByDescending(x => x.W)
          .Select(x =>
              $"{x.H}x{x.W} " +
              char.ToUpperInvariant(x.Type[0]) +
              x.Type.Substring(1))
          .ToList();
    }

    /// Возвращает список моделей для выбранного разрешения.
    /// Формат строки: 480x480 Round
    public static List<WatchModelInfo>
        GetModelsForResolution(
            string resolutionString)
    {
      if (string.IsNullOrWhiteSpace(
              resolutionString))
      {
        return new List<WatchModelInfo>();
      }

      // Разделяем: 480x480 Round
      // на: 480x480
      // Round
      var parts =
          resolutionString.Split(
              new[] { ' ' },
              StringSplitOptions.RemoveEmptyEntries);

      if (parts.Length < 2)
      {
        return new List<WatchModelInfo>();
      }

      var dimensionParts =
          parts[0].Split('x');

      if (dimensionParts.Length != 2)
      {
        return new List<WatchModelInfo>();
      }

      // Получаем высоту.
      if (!int.TryParse(
              dimensionParts[0],
              out int targetHeight))
      {
        return new List<WatchModelInfo>();
      }

      // Получаем ширину.
      if (!int.TryParse(
              dimensionParts[1],
              out int targetWidth))
      {
        return new List<WatchModelInfo>();
      }

      string targetType =
          parts[1].Trim();

      var allModels =
          LoadModels().Values;

      return allModels
          .Where(m =>
              m.Height == targetHeight &&
              m.Width == targetWidth &&
              m.ScreenType.Equals(
                  targetType,
                  StringComparison.OrdinalIgnoreCase))
          .OrderBy(m => m.Name)
          .ToList();
    }

    /// Передаёт предупреждение подписчикам UI. Core не знает, кто именно подписан.
    private static void NotifyWarning(
        ConfigurationWarning warning)
    {
      try
      {
        WarningOccurred?.Invoke(warning);
      }
      catch
      {
        // Ошибка UI-обработчика не должна ломать Core.
      }
    }
  }
}