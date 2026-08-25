
using System; //Для диагностики. почему не видно окно приложения
using System.Windows;
using QRCoderZpkd1_Link.Core; // Подключаем пространство имен нашего менеджера
using System.IO; // Обязательно для работы с файлами
using System.Reflection; // При получении релиза для передоса *.dll
using System.Linq;
using System.Windows.Threading; // Нужен для работы с Dispatcher WPF, чтобы показывать Toast безопасно из UI-потока.

namespace QRCoderZpkd1_Link
{
  /// Логика взаимодействия для App.xaml
  public partial class App : Application
  {
    protected override void OnStartup(StartupEventArgs e)
    {

      //1. WPF-версия приложения исторически хранит настройки рядом с исполняемым файлом.
      SettingsManager.SetSettingsFilePath(
        Path.Combine(
          AppContext.BaseDirectory,
          "UserSetting.json"));

      // 2. Загружаем пользовательские настройки перед отрисовкой интерфейса.
      SettingsManager.Load();

      // 3. Инициализируем локализацию
      if (!string.IsNullOrEmpty(SettingsManager.Current.Language))
      {
        // Если язык был ранее сохранен пользователем, применяем строго его
        LanguageManager.SwitchLanguage(SettingsManager.Current.Language);
      }
      else
      {
        // Если запуск первый (или язык не сохранен), подхватываем язык Windows
        LanguageManager.Initialize();
      }

      // Переносим все загруженные слова в глобальные ресурсы WPF
      UpdateLanguageResources();

      // Один раз при запуске проверяем все доступные JSON-файлы локализации. LanguageManager передаёт обнаруженные проблемы через WarningOccurred.
      LanguageManager.ValidateLanguageFiles();

      // 4. Применяем сохраненную тему (Светлую или Темную)
      ApplyGlobalTheme(SettingsManager.Current.Theme);

      try
      {
        base.OnStartup(e);
        // Если ты используешь StartupUri в XAML, WPF сам создаст MainWindow.
        // Если окно не появится, значит, падение происходит внутри конструктора MainWindow или при загрузке стилей в App.xaml.
      }
      catch (Exception ex)
      {
        // Это заставит программу показать окно с ошибкой перед закрытием
        MessageBox.Show($"Критическая ошибка при запуске:\n{ex.Message}\n\n{ex.StackTrace}",
                        "Ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Error);

        // Также выведем в консоль для терминала
        Console.WriteLine(ex.ToString());

        Shutdown();
      }
    }
    
    public static void ApplyGlobalTheme(string themeName)
    {
      try
      {
        var mergedDictionaries = Current.Resources.MergedDictionaries;
        // Ищем словарь, который отвечает за темы (заканчивается на Theme.xaml)
        var themeDict = mergedDictionaries.FirstOrDefault(d =>
            d.Source != null && d.Source.ToString().EndsWith("Theme.xaml"));

        if (themeDict != null)
        {
          string uriPath = $"Styles/Themes/{themeName}.xaml";
          themeDict.Source = new Uri(uriPath, UriKind.Relative);
        }
      }
      catch (Exception ex)
      {
        MessageBox.Show($"Не удалось сменить тему:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    /// Публичный метод, который можно вызвать из любого окна для смены языка.
    public static void ChangeLanguage(string languageName)
    {
      // Переключаем словарь в Core
      LanguageManager.SwitchLanguage(languageName);

      // Обновляем ресурсы в WPF
      UpdateLanguageResources();

      // Сохраняем выбранный язык в файле настроек
      SettingsManager.Current.Language = languageName;
      SettingsManager.Save();

      // Срочно обновляем выпадающий список в главном окне, чтобы плейсхолдер перевелся
      if (Current.MainWindow is MainWindow mainWin)
      {
        mainWin.LoadResolutions();
      }
    }

    /// Переносит все текущие переводы из Core в глобальные ресурсы WPF.
    private static void UpdateLanguageResources()
    {
      foreach (var kvp in LanguageManager.Translations)
      {
        string resourceKey = $"Lang_{kvp.Key}";
        string textValue = kvp.Value;

        // Точечно применяем защиту от висячих слов для текста описания
        if (kvp.Key == "AboutDescription" && !string.IsNullOrWhiteSpace(textValue))
        {
          textValue = textValue.TrimEnd(); // Убираем случайные пробелы в самом конце
          int lastSpaceIndex = textValue.LastIndexOf(' ');

          if (lastSpaceIndex > 0)
          {
            // Заменяем ровно ОДИН последний пробел на неразрывный (\u00A0)
            textValue = textValue.Remove(lastSpaceIndex, 1).Insert(lastSpaceIndex, "\u00A0");
          }
        }

        Current.Resources[resourceKey] = textValue;
      }
    }

    /// Настройки для создания релизной сборки приложения.
    public App()
    {
      // Подписываемся на событие поиска библиотек
      AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

      // Подписываемся на предупреждения ConfigurationManager.
      // Core сам не создаёт WPF Toast. Он только сообщает о проблеме, а App показывает соответствующее UI-уведомление.
      ConfigManager.WarningOccurred += ConfigManager_WarningOccurred;

      // Подписываемся на предупреждения LanguageManager.
      // Core только сообщает о проблеме, а WPF переводит её и показывает существующий Toast.
      LanguageManager.WarningOccurred += LanguageManager_WarningOccurred;
    }

    /// Получает предупреждение от Core, переводит его на текущий язык приложения и показывает Toast.
    private void ConfigManager_WarningOccurred(
    ConfigurationWarning warning)
    {
      // Событие потенциально может прийти не из UI-потока. Поэтому гарантируем выполнение WPF-кода в Dispatcher-потоке приложения.
      Dispatcher.BeginInvoke(
        new Action(() =>
        {
          // Создаём Toast с текстом предупреждения.
          string title =
                LanguageManager.GetString(
                    "ConfigurationWarningTitle");

          // Определяем основной текст по типу предупреждения.
          string message =
              GetLocalizedConfigurationWarning(
                  warning);

          // Создаём Toast.
          var toast =
              new ToastNotificationWindow(
                  title,
                  message);

          // Показываем его без блокировки главного окна.
          toast.Show();
        }));
    }

    /// Получает предупреждение LanguageManager, переводит его на текущий язык
    /// и показывает существующий WPF Toast.
    private void LanguageManager_WarningOccurred(
    LanguageWarning warning)
    {
      // Переносим создание WPF Toast в UI-поток.
      Dispatcher.BeginInvoke(
        new Action(() =>
        {
          // Получаем локализованный заголовок предупреждения.
          string title =
              LanguageManager.GetString(
                  "LanguageWarningTitle");

          // Формируем локализованный текст проблемы.
          string message =
              GetLocalizedLanguageWarning(
                  warning);

          // Создаём Toast с предупреждением.
          var toast =
              new ToastNotificationWindow(
                  title,
                  message);

          // Показываем Toast без блокировки приложения.
          toast.Show();
        }));
    }

    /// Формирует локализованный текст предупреждения ConfigurationManager.
    /// Все постоянные тексты берутся из LanguageManager. Поэтому Toast автоматически соответствует выбранному языку.
    private static string GetLocalizedConfigurationWarning(
        ConfigurationWarning warning)
    {
      // Выбираем перевод по типу предупреждения.
      switch (warning.Type)
      {
        case ConfigurationWarningType.DataDirectoryMissing:

          // Сообщение об отсутствии обязательной папки Data.
          return LanguageManager.GetString(
              "ConfigurationDataDirectoryMissing");

        case ConfigurationWarningType.ConfigurationFileMissing:

          // Сообщение об отсутствии configurations.json.
          return LanguageManager.GetString(
              "ConfigurationFileMissing");

        case ConfigurationWarningType.InvalidJson:

          // Сообщение о повреждённом JSON.
          return LanguageManager.GetString(
              "ConfigurationInvalidJson");

        case ConfigurationWarningType.InvalidModel:

          // Получаем переведённый заголовок списка неправильных моделей.
          string invalidModelsHeader =
              LanguageManager.GetString(
                  "ConfigurationInvalidModels");

          // Получаем переведённый текст о том, что такие модели были проигнорированы.
          string ignoredModels =
              LanguageManager.GetString(
                  "ConfigurationIgnoredModels");

          // Объединяем постоянный перевод с конкретными данными из Core.
          // Details содержит только данные, поэтому переводить сами названия моделей не требуется.
          string details =
              string.Join(
                  Environment.NewLine,
                  warning.Details);

          return
              invalidModelsHeader +
              Environment.NewLine +
              Environment.NewLine +
              ignoredModels +
              Environment.NewLine +
              details;

        default:

          // Защита от появления нового типа предупреждения, для которого ещё не добавлен перевод.
          return LanguageManager.GetString(
              "ConfigurationUnknownWarning");
      }
    }

    /// Формирует локализованный текст предупреждения LanguageManager.
    private static string GetLocalizedLanguageWarning(
        LanguageWarning warning)
    {
      // Получаем локализованный текст основной проблемы.
      string message;

      switch (warning.Type)
      {
        case LanguageWarningType.LanguageFileMissing:

          // Указанный файл языка отсутствует.
          message = LanguageManager.GetString(
              "LanguageFileMissing");
          break;

        case LanguageWarningType.InvalidJson:

          // JSON-файл языка повреждён.
          message = LanguageManager.GetString(
              "LanguageInvalidJson");
          break;

        case LanguageWarningType.MissingLanguageCode:

          // В JSON отсутствует обязательный LangCode.
          message = LanguageManager.GetString(
              "LanguageMissingCode");
          break;

        case LanguageWarningType.InvalidLanguageCode:

          // LangCode имеет неправильный формат.
          message = LanguageManager.GetString(
              "LanguageInvalidCode");
          break;

        case LanguageWarningType.LanguageFileReadError:

          // Файл существует, но не может быть прочитан.
          message = LanguageManager.GetString(
              "LanguageFileReadError");
          break;

        case LanguageWarningType.EmptyTranslationValue:

          // В файле перевода значение ключа пустое.
          message = LanguageManager.GetString(
              "EmptyTranslationValue");
          break;

        case LanguageWarningType.MissingTranslationKey:

          // В файле перевода отсутствует ключ.
          message = LanguageManager.GetString(
              "MissingTranslationKey");
          break;

        default:

          // Защита от появления нового типа предупреждения.
          message = LanguageManager.GetString(
              "LanguageUnknownWarning");
          break;
      }

      // Добавляем сведения о конкретном проблемном файле.
      if (warning.Details.Count > 0)
      {
        message +=
            Environment.NewLine +
            Environment.NewLine +
            LanguageManager.GetString(
                "LanguageProblemFile") +
            ": " +
            warning.Details[0];
      }

      // Для некорректного кода дополнительно показываем полученное значение.
      if (warning.Type ==
              LanguageWarningType.InvalidLanguageCode &&
          warning.Details.Count > 1)
      {
        message +=
            Environment.NewLine +
            LanguageManager.GetString(
                "LanguageProblemCode") +
            ": " +
            warning.Details[1];
      }

      return message;
    }

    private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
    {
      // Получаем имя сборки
      string assemblyName = new AssemblyName(args.Name).Name + ".dll";

      // Путь к папке Libs
      string libsFolder = Path.Combine(AppContext.BaseDirectory, "Libs");

      // Переменная называется assemblyPath
      string assemblyPath = Path.Combine(libsFolder, assemblyName);

      // Проверяем именно assemblyPath
      if (File.Exists(assemblyPath))
      {
        return Assembly.LoadFrom(assemblyPath);
      }
      return null;
    }
  }
}
