using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using QRCoderZpkd1_Link.Core;

namespace QRCoderZpkd1_Link
{
  public partial class MainWindow : Window
  {
    // Объявляем коллекцию и фиксированное поле для плейсхолдера
    private ObservableCollection<string> _resolutionItems;
    private string _currentPlaceholderText;
    public MainWindow()
    {
      InitializeComponent();
      // Устанавливаем версию при старте
      VersionText.Text = GetAppVersion();
      // Загружаем список разрешений из конфигурации в ComboBox
      LoadResolutions();
      // При старте приложения на предпросмотре не должно быть ни одной буквы
      ClearPreview();
    }

    /// Загружает доступные разрешения в ComboBox из ConfigManager
    public void LoadResolutions(bool resetToPlaceholder = false)
    {
      if (ResolutionComboBox == null) return;

      string savedResolution = null;
      bool wasPlaceholderSelected = true;

      if (!resetToPlaceholder)
      {
        // 1. Запоминаем текущее выбранное разрешение (если не требуется сброс)
        savedResolution = ResolutionComboBox.SelectedItem as string;
        wasPlaceholderSelected = string.IsNullOrEmpty(savedResolution) || savedResolution == _currentPlaceholderText;
      }

      // 2. Запоминаем список выбранных моделей (если разрешение было выбрано)
      List<string> savedSelectedModels = new List<string>();
      if (!wasPlaceholderSelected && ModelsItemsControl != null)
      {
        foreach (var item in ModelsItemsControl.Items)
        {
          var container = ModelsItemsControl.ItemContainerGenerator.ContainerFromItem(item) as DependencyObject;
          if (container != null)
          {
            var checkBox = FindVisualChild<CheckBox>(container);
            if (checkBox != null && checkBox.IsChecked == true)
            {
              var modelInfo = item as QRCoderZpkd1_Link.Core.WatchModelInfo;
              if (modelInfo != null)
              {
                savedSelectedModels.Add(modelInfo.Name);
              }
            }
          }
        }
      }

      // --- Оригинальный код перевода плейсхолдера ---
      string placeholderText = QRCoderZpkd1_Link.Core.LanguageManager.GetString("ResolutionPlaceholder");

      if (string.IsNullOrEmpty(placeholderText) || placeholderText.StartsWith("["))
      {
        string currentLang = QRCoderZpkd1_Link.Core.LanguageManager.CurrentLanguage;
        bool isRussian = currentLang.Contains("Russian", StringComparison.OrdinalIgnoreCase) ||
                         currentLang.Contains("ru", StringComparison.OrdinalIgnoreCase);

        placeholderText = isRussian ? "Выберите разрешение..." : "Select resolution...";
      }

      _currentPlaceholderText = placeholderText;

      var resolutions = QRCoderZpkd1_Link.Core.ConfigManager.GetResolutions();

      _resolutionItems = new ObservableCollection<string>
      {
        _currentPlaceholderText
      };

      foreach (var res in resolutions)
      {
        _resolutionItems.Add(res);
      }

      ResolutionComboBox.SelectionChanged -= ResolutionComboBox_SelectionChanged;

      ResolutionComboBox.ItemsSource = _resolutionItems;

      // 4. Восстанавливаем выбор или сбрасываем на плейсхолдер
      if (!wasPlaceholderSelected && _resolutionItems.Contains(savedResolution))
      {
        ResolutionComboBox.SelectedItem = savedResolution;
      }
      else
      {
        ResolutionComboBox.SelectedIndex = 0;
      }

      // ПОДКЛЮЧАЕМ обработчик обратно
      ResolutionComboBox.SelectionChanged += ResolutionComboBox_SelectionChanged;

      // 5. Восстанавливаем выбранные модели, если разрешение было успешно восстановлено
      if (!wasPlaceholderSelected && _resolutionItems.Contains(savedResolution))
      {
        // Принудительно загружаем модели для выбранного разрешения
        var matchingModels = QRCoderZpkd1_Link.Core.ConfigManager.GetModelsForResolution(savedResolution);
        ModelsItemsControl.ItemsSource = matchingModels;

        // ВАЖНО: Заставляем UI немедленно создать визуальные элементы (чекбоксы), чтобы мы могли их отметить
        ModelsItemsControl.UpdateLayout();

        // Блокируем лишние вызовы событий, чтобы интерфейс не "мигал"
        _isBulkUpdating = true;

        // Проставляем галочки для ранее выбранных моделей
        foreach (var item in ModelsItemsControl.Items)
        {
          var modelInfo = item as QRCoderZpkd1_Link.Core.WatchModelInfo;
          if (modelInfo != null && savedSelectedModels.Contains(modelInfo.Name))
          {
            var container = ModelsItemsControl.ItemContainerGenerator.ContainerFromItem(item) as DependencyObject;
            if (container != null)
            {
              var checkBox = FindVisualChild<CheckBox>(container);
              if (checkBox != null)
              {
                checkBox.IsChecked = true;
              }
            }
          }
        }

        _isBulkUpdating = false;

        // Вручную обновляем интерфейс после пакетного восстановления (текст и кнопку "Отметить все")
        UpdateSelectedModelsDisplay();
        UpdateSelectAllGroupBoxState();
      }
    }

    /// <summary>
    /// Обработчик смены разрешения в ComboBox. 
    /// Сбрасывает (очищает) выбранные модели при смене разрешения для предотвращения конфликтов.
    /// </summary>
    private void ResolutionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (ResolutionComboBox == null || ModelsItemsControl == null) return;
      if (_resolutionItems == null || _resolutionItems.Count == 0) return;

      string selectedResolution = ResolutionComboBox.SelectedItem as string;

      // Если выбран плейсхолдер или ничего не выбрано
      if (string.IsNullOrEmpty(selectedResolution) || selectedResolution == _currentPlaceholderText)
      {
        ModelsItemsControl.ItemsSource = null;
        UpdateSelectedModelsDisplay();

        // Блокируем кнопку "Отметить все" и делаем её полупрозрачной
        if (SelectAllGroupBox != null)
        {
          SelectAllGroupBox.IsEnabled = false;
          SelectAllGroupBox.Opacity = 0.4;
        }
        return;
      }

      // Удаляем из коллекции только оригинальный плейсхолдер, если он там еще остался
      if (_resolutionItems.Contains(_currentPlaceholderText))
      {
        _resolutionItems.Remove(_currentPlaceholderText);
      }

      // Получаем модели для выбранного разрешения
      var matchingModels = QRCoderZpkd1_Link.Core.ConfigManager.GetModelsForResolution(selectedResolution);
      ModelsItemsControl.ItemsSource = matchingModels;

      UpdateSelectedModelsDisplay();
      // Сбрасываем состояние массового выбора при смене разрешения или очистке
      _isAllModelsSelected = false;
      UpdateGroupBoxHeaderText();

      // Разблокируем кнопку "Отметить все" и возвращаем ей нормальный вид
      if (SelectAllGroupBox != null)
      {
        SelectAllGroupBox.IsEnabled = true;
        SelectAllGroupBox.Opacity = 0.8;
      }
    }

    /// <summary>
    /// Очищает все текстовые элементы на панели предпросмотра
    /// </summary>
    private void ClearPreview()
    {
      if (PreviewType != null) PreviewType.Text = string.Empty;
      if (PreviewName != null) PreviewName.Text = string.Empty;
      if (PreviewVersion != null) PreviewVersion.Text = string.Empty;
    }
    // Метод получения версии приложения и форматирования отображения версии
    private string GetAppVersion()
    {
      // Получаем атрибут версии из сборки
      var versionAttr = Assembly.GetExecutingAssembly()
                              .GetCustomAttribute<AssemblyInformationalVersionAttribute>();

      if (versionAttr == null) return "v unknown";

      // Убираем возможный префикс "v.", чтобы получить чистые цифры для парсинга
      string raw = versionAttr.InformationalVersion;
      // Очищаем строку от старых префиксов ("v." или "v ") для правильного парсинга
      string cleanVersion = raw;
      if (raw.StartsWith("v.", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("v ", StringComparison.OrdinalIgnoreCase))
      {
        cleanVersion = raw.Substring(2);
      }

      if (Version.TryParse(cleanVersion, out Version v))
      {
        // Меняем интерполяцию строк: теперь используем "v " вместо "v."
        if (v.Build <= 0)
        {
          return $"v {v.Major}.{v.Minor}";
        }
        else if (v.Build <= 9)
        {
          return $"v {v.Major}.{v.Minor}.{v.Build}";
        }

        return $"v {v.Major}.{v.Minor}.{v.Build}";
      }

      // Если парсинг не удался, просто заменяем "v." на "v " в исходной строке
      return raw.Replace("v.", "v ").Replace("V.", "v ");
    }
    //Методы переключения темы приложения через выпадающее меню
    private void MenuItem_LightTheme_Click(object sender, RoutedEventArgs e)
    {
      ApplyTheme("LightTheme");
    }

    private void MenuItem_DarkTheme_Click(object sender, RoutedEventArgs e)
    {
      ApplyTheme("DarkTheme");
    }
    private void ApplyTheme(string themeName)
    {
      // Обращаемся к глобальному методу в App
      App.ApplyGlobalTheme(themeName);

      // Сохраняем новую тему в настройки пользователя
      SettingsManager.Current.Theme = themeName;
      SettingsManager.Save();
    }
    // Метод для открытия меню по клику на кнопку (если вы его еще не добавили)
    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
      Button btn = sender as Button;
      if (btn != null && btn.ContextMenu != null)
      {
        btn.ContextMenu.PlacementTarget = btn;
        btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        btn.ContextMenu.IsOpen = true;
      }
    }
    // Метод открытия окна при клике на меню "About"
    private void MenuItem_About_Click(object sender, RoutedEventArgs e)
    {
      // Получаем текущую версию тем же методом, что и в главном окне
      string currentVersion = GetAppVersion();
      // Создаем экземпляр окна
      AboutWindow aboutWin = new AboutWindow(currentVersion);

      // Привязываем владельца (чтобы окно открылось строго по центру MainWindow)
      aboutWin.Owner = this;

      // Открываем
      aboutWin.ShowDialog();
    }
    // Динамическое построение меню языков на их родных языках
    private void LanguageButton_Click(object sender, RoutedEventArgs e)
    {
      Button btn = sender as Button;
      if (btn == null) return;

      if (btn.ContextMenu == null)
      {
        btn.ContextMenu = new ContextMenu();
      }

      ContextMenu ctxMenu = btn.ContextMenu;
      ctxMenu.Items.Clear(); // Очищаем старые пункты

      // Запрашиваем список языков (теперь возвращает объекты с Code и DisplayName)
      var availableLanguages = QRCoderZpkd1_Link.Core.LanguageManager.GetAvailableLanguages();

      foreach (var lang in availableLanguages)
      {
        MenuItem item = new MenuItem();

        // Отображаем РОДНОЕ название (например: "English", "Deutsch", "Русский", "Українська")
        item.Header = lang.DisplayName;

        // В Tag прячем системное имя файла (например: "German" или "Russian")
        item.Tag = lang.Code;

        // Сверяем активный язык по системному коду
        if (string.Equals(lang.Code, QRCoderZpkd1_Link.Core.LanguageManager.CurrentLanguage, StringComparison.OrdinalIgnoreCase))
        {
          item.IsChecked = true;
        }

        // Назначаем обработчик клика
        item.Click += (s, args) =>
        {
          MenuItem clickedItem = s as MenuItem;
          if (clickedItem != null && clickedItem.Tag != null)
          {
            App.ChangeLanguage(clickedItem.Tag.ToString());
          }
        };

        ctxMenu.Items.Add(item);
      }

      ctxMenu.PlacementTarget = btn;
      ctxMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
      ctxMenu.IsOpen = true;
    }
    // Флаг для предотвращения зацикливания при автоматическом изменении текста ссылки
    private bool _isUpdatingText = false;

    /// <summary>
    /// Конвертирует System.Drawing.Bitmap (из Ядра) в BitmapImage для отображения в WPF интерфейсе.
    /// </summary>
    private System.Windows.Media.Imaging.BitmapImage BitmapToImageSource(System.Drawing.Bitmap bitmap)
    {
      using (System.IO.MemoryStream memory = new System.IO.MemoryStream())
      {
        // Сохраняем битмап в поток памяти в формате PNG
        bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
        memory.Position = 0;

        System.Windows.Media.Imaging.BitmapImage bitmapimage = new System.Windows.Media.Imaging.BitmapImage();
        bitmapimage.BeginInit();
        bitmapimage.StreamSource = memory;
        bitmapimage.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bitmapimage.EndInit();

        return bitmapimage;
      }
    }
    // ==========================================
    // ЛОГИКА АВТОЗАПОЛНЕНИЯ (AUTO-FILL) ПО ССЫЛКЕ
    // ==========================================

    /// <summary>
    /// Обработчик изменения текста в поле URL Link. Выполняет парсинг, автокоррекцию, 
    /// генерацию QR-кода и проверку доступности файла по сети.
    /// </summary>
    private async void UrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
      if (UrlTextBox == null || _isUpdatingText) return;

      // По умолчанию блокируем кнопку сохранения и ставим иконку крестика
      if (SaveButton != null) SaveButton.IsEnabled = false;

      if (StatusIcon != null)
      {
        StatusIcon.Source = new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/Assets/Icons/close.png", UriKind.Absolute));
      }

      string input = UrlTextBox.Text.Trim();

      // Декодируем и проверяем на наличие GitHub-ссылок
      input = LinkParser.DecodeUrl(input);
      if (input.Contains("github.com"))
      {
        input = LinkParser.ConvertGitHubBlobToPagesUrl(input);
      }

      // Если поле пустое, возвращаем дефолтный логотип и очищаем превью
      if (string.IsNullOrEmpty(input))
      {
        if (QrPreviewImage != null)
        {
          QrPreviewImage.Source = new System.Windows.Media.Imaging.BitmapImage(
              new Uri("pack://application:,,,/Assets/Icons/logo_qr.png", UriKind.Absolute));
        }
        return;
      }

      // Корректируем протокол на zpkd1://
      string corrected = LinkParser.CorrectUrl(input);

      // Если ссылка была исправлена программой, обновляем текстовое поле в UI
      if (corrected != input)
      {
        _isUpdatingText = true;
        UrlTextBox.Text = corrected;
        UrlTextBox.SelectionStart = corrected.Length; // Переносим курсор в конец строки
        _isUpdatingText = false;
      }

      // 1. Автозаполнение Name и Version с помощью парсера
      var parsed = LinkParser.Parse(corrected);
      if (NameTextBox != null) NameTextBox.Text = parsed.Name;
      if (VersionTextBox != null) VersionTextBox.Text = parsed.Version;
      UpdatePreview();

      // 2. Генерируем QR код через ядро и выводим в предпросмотр
      try
      {
        // Загружаем логотип из ресурсов приложения как Stream для передачи в ядро
        var logoUri = new Uri("pack://application:,,,/Assets/Icons/logo_qr.png", UriKind.Absolute);
        var logoStreamInfo = System.Windows.Application.GetResourceStream(logoUri);

        using (var logoStream = logoStreamInfo?.Stream)
        {
          // Генерируем финальное изображение 490x630
          using (System.Drawing.Bitmap finalQrImage = QrGenerator.GenerateFinalImage(corrected, logoStream))
          {
            // Конвертируем и устанавливаем в UI. WPF автоматически сожмет его до размеров 
            // карточки (в 1.4 раза) благодаря атрибуту Stretch="Uniform" в XAML.
            if (QrPreviewImage != null)
            {
              QrPreviewImage.Source = BitmapToImageSource(finalQrImage);
            }
          }
        }
      }
      catch
      {
        // В случае сбоя генерации оставляем логотип
      }

      // 3. Асинхронно проверяем доступность файла по сети
      bool exists = await LinkParser.UrlFileExistsAsync(corrected);

      if (exists)
      {
        if (StatusIcon != null)
        {
          StatusIcon.Source = new System.Windows.Media.Imaging.BitmapImage(
              new Uri("pack://application:,,,/Assets/Icons/check.png", UriKind.Absolute));
        }

        // Если файл успешно найден, разблокируем кнопку сохранения
        if (SaveButton != null)
        {
          SaveButton.IsEnabled = true;
        }
      }

      // Обновляем превью в реальном времени
      UpdatePreview();
    }

    /// Обработчик ручного изменения полей Name или Version
    private void InputField_Changed(object sender, TextChangedEventArgs e)
    {
      UpdatePreview();
    }

    /// Обработчик переключения радиокнопок типа (Watch face / App / None)
    private void RadioType_Checked(object sender, RoutedEventArgs e)
    {
      UpdatePreview();
    }

    /// Централизованный метод обновления панели предпросмотра
    private void UpdatePreview()
    {
      // Префикс типа жестко закреплен по состоянию радиокнопок
      if (PreviewType != null)
      {
        if (RadioWatchFace != null && RadioWatchFace.IsChecked == true)
        {
          PreviewType.Text = "Watch face: ";
        }
        else if (RadioApp != null && RadioApp.IsChecked == true)
        {
          PreviewType.Text = "App: ";
        }
        else
        {
          PreviewType.Text = string.Empty; // Если выбран "Не выбран" (None)
        }
      }

      // Вывод имени из поля NameTextBox с поддержкой переноса длинных строк
      if (PreviewName != null && NameTextBox != null)
      {
        string nameValue = NameTextBox.Text.Trim();
        PreviewName.Text = string.IsNullOrEmpty(nameValue) ? string.Empty : $"{nameValue} ";
      }

      // Вывод версии в формате "v. [значение]" из VersionTextBox
      if (PreviewVersion != null && VersionTextBox != null)
      {
        string versionValue = VersionTextBox.Text.Trim();
        if (string.IsNullOrEmpty(versionValue))
        {
          PreviewVersion.Text = string.Empty;
        }
        else
        {
          PreviewVersion.Text = $"v.\u00A0{versionValue}";
        }
      }
    }

    /// <summary>
    /// Динамически формирует список выбранных моделей через запятую для отображения под превью.
    /// Выводит имена без слова "Amazfit" и переносит длинный текст на новую строку.
    /// </summary>
    private void UpdateSelectedModelsDisplay()
    {
      if (ModelsListText == null || ModelsItemsControl == null) return;

      var selectedCleanNames = new List<string>();

      // Проходим по всем элементам ItemsControl и ищем отмеченные чекбоксы
      foreach (var item in ModelsItemsControl.Items)
      {
        var container = ModelsItemsControl.ItemContainerGenerator.ContainerFromItem(item) as DependencyObject;
        if (container != null)
        {
          var checkBox = FindVisualChild<CheckBox>(container);
          if (checkBox != null && checkBox.IsChecked == true)
          {
            var modelInfo = item as QRCoderZpkd1_Link.Core.WatchModelInfo;
            if (modelInfo != null)
            {
              selectedCleanNames.Add(modelInfo.CleanName);
            }
          }
        }
      }

      if (selectedCleanNames.Count == 0)
      {
        ModelsListText.Text = string.Empty;
      }
      else
      {
        // Выводим через запятую с пробелом
        ModelsListText.Text = string.Join(", ", selectedCleanNames);
      }
    }

    /// <summary>
    /// Вспомогательный метод для поиска дочерних элементов в визуальном дереве WPF
    /// </summary>
    private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
      for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
      {
        var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
        if (child is T typedChild)
        {
          return typedChild;
        }
        var descendant = FindVisualChild<T>(child);
        if (descendant != null)
        {
          return descendant;
        }
      }
      return null;
    }

    /// <summary> 
    /// Массово отмечает или снимает отметки со всех модельков в списке.
    /// </summary>
    // Флаг для отслеживания текущего состояния выбора всех моделей
    private bool _isAllModelsSelected = false;

    // Флаг-блокиратор для предотвращения лавины событий (event storm) при массовом выборе чекбоксов
    private bool _isBulkUpdating = false;

    /// <summary>
    /// Обработчик изменения состояния чекбоксов моделей (вызов при Checked / Unchecked)
    /// </summary>
    private void ModelCheckBox_Changed(object sender, RoutedEventArgs e)
    {
      // Если идет массовое обновление через GroupBox, игнорируем индивидуальные события
      if (_isBulkUpdating) return;

      UpdateSelectedModelsDisplay();

      // Синхронизируем состояние GroupBox, если пользователь кликает чекбоксы вручную по одному
      UpdateSelectAllGroupBoxState();
    }

    /// <summary>
    /// Обработчик клика по интерактивному GroupBox. 
    /// Массово отмечает или снимает отметки со всех моделей в списке.
    /// </summary>
    private void SelectAllGroupBox_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
      // 1. Проверяем, существует ли список и есть ли в нем модели часов на экране.
      // Если моделей нет (например, пользователь еще не выбрал разрешение), то ничего не делаем.
      if (ModelsItemsControl == null || ModelsItemsControl.Items.Count == 0) return;

      // 2. Меняем состояние нашего флага на противоположное.
      // Если было false (отметок нет), станет true (выбрать все). И наоборот.
      _isAllModelsSelected = !_isAllModelsSelected;

      // 3. Включаем блокировку событий, чтобы индивидуальные чекбоксы не вызывали лишние пересчеты во время цикла
      _isBulkUpdating = true;

      try
      {
        // 4. Запускаем цикл: перебираем по очереди все модели в нашем списке ModelsItemsControl.
        foreach (var item in ModelsItemsControl.Items)
        {
          // Получаем визуальный контейнер WPF для текущей модели (он невидимо оборачивает наш CheckBox).
          var container = ModelsItemsControl.ItemContainerGenerator.ContainerFromItem(item) as DependencyObject;
          if (container != null)
          {
            // С помощью существующего метода FindVisualChild находим сам CheckBox внутри контейнера.
            var checkBox = FindVisualChild<CheckBox>(container);
            if (checkBox != null)
            {
              // Ставим или убираем галочку в чекбоксе (согласно нашему флагу _isAllModelsSelected).
              checkBox.IsChecked = _isAllModelsSelected;
            }
          }
        }
      }
      finally
      {
        // 5. Обязательно снимаем блокировку в блоке finally, даже если произойдет непредвиденный сбой
        _isBulkUpdating = false;
      }

      // 6. Обновляем текст заголовка GroupBox и отображение выбранных моделей под превью ровно один раз после цикла
      UpdateGroupBoxHeaderText();
      UpdateSelectedModelsDisplay();
    }

    /// <summary>
    /// Проверяет, выбраны ли все модели в списке вручную, и обновляет состояние GroupBox и его текста.
    /// </summary>
    private void UpdateSelectAllGroupBoxState()
    {
      if (_isBulkUpdating) return;

      if (ModelsItemsControl == null || ModelsItemsControl.Items.Count == 0)
      {
        _isAllModelsSelected = false;
        UpdateGroupBoxHeaderText();
        return;
      }

      bool allChecked = true;
      bool anyChecked = false;

      foreach (var item in ModelsItemsControl.Items)
      {
        var container = ModelsItemsControl.ItemContainerGenerator.ContainerFromItem(item) as DependencyObject;
        if (container != null)
        {
          var checkBox = FindVisualChild<CheckBox>(container);
          if (checkBox != null)
          {
            if (checkBox.IsChecked == true)
            {
              anyChecked = true;
            }
            else
            {
              allChecked = false; // Хотя бы один чекбокс не отмечен
            }
          }
        }
        else
        {
          allChecked = false;
        }
      }

      // Устанавливаем флаг в true только если все элементы отмечены
      _isAllModelsSelected = allChecked && anyChecked;
      UpdateGroupBoxHeaderText();
    }

    /// <summary>
    /// Динамически обновляет привязку текста кнопки ("Отметить все" / "Снять все отметки") к ресурсам словаря и управляет видимостью галочки.
    /// </summary>
    private void UpdateGroupBoxHeaderText()
    {
      if (SelectAllTextblock != null)
      {
        // Выбираем правильный ключ из наших словарей ресурсов (как в DesignerResources.xaml)
        string resourceKey = _isAllModelsSelected ? "Lang_UncheckAll" : "Lang_CheckAll";

        // Восстанавливаем / обновляем динамическую привязку, чтобы WPF сам переводил текст
        SelectAllTextblock.SetResourceReference(TextBlock.TextProperty, resourceKey);
      }

      // Управляем видимостью галочки внутри иконки
      if (SelectAllCheckMark != null)
      {
        SelectAllCheckMark.Visibility = _isAllModelsSelected ? Visibility.Visible : Visibility.Collapsed;
      }
    }

    /// Обработчик нажатия на кнопку "Сохранить QR-код". 
    /// Предлагает пользователю выбрать папку и сохранить полную карточку (490x630) в формате PNG.
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
      if (UrlTextBox == null) return;

      string correctedUrl = LinkParser.CorrectUrl(UrlTextBox.Text.Trim());
      if (string.IsNullOrEmpty(correctedUrl)) return;

      // Предлагаем имя файла по умолчанию на основе распарсенного названия и версии
      string defaultFileName = "PreviewCard.png";
      if (NameTextBox != null && !string.IsNullOrEmpty(NameTextBox.Text))
      {
        string ver = VersionTextBox != null ? VersionTextBox.Text.Trim() : string.Empty;
        defaultFileName = string.IsNullOrEmpty(ver)
            ? $"{NameTextBox.Text.Trim()}.png"
            : $"{NameTextBox.Text.Trim()}_v_{ver}.png";
      }

      // Открываем диалоговое окно выбора папки и имени файла (SaveFileDialog)
      Microsoft.Win32.SaveFileDialog saveDlg = new Microsoft.Win32.SaveFileDialog
      {
        Filter = "PNG Image (*.png)|*.png",
        FileName = defaultFileName,
        DefaultExt = ".png"
      };

      bool? result = saveDlg.ShowDialog();
      if (result == true)
      {
        try
        {
          // 1. Считываем текущие текстовые значения с панели предпросмотра
          string typeText = PreviewType != null ? PreviewType.Text : "";
          string nameText = PreviewName != null ? PreviewName.Text : "";
          string versionText = PreviewVersion != null ? PreviewVersion.Text : "";
          string modelsText = ModelsListText != null ? ModelsListText.Text : "";

          // Загружаем ресурс логотипа для передачи в ядро
          var logoUri = new Uri("pack://application:,,,/Assets/Icons/logo_qr.png", UriKind.Absolute);
          var logoStreamInfo = System.Windows.Application.GetResourceStream(logoUri);

          using (var logoStream = logoStreamInfo?.Stream)
          {
            // 2. ВЫЗОВ НОВОГО МЕТОДА ИЗ ЯДРА
            // Делегируем сборку 490x630 изображения вынесенной бизнес-логике
            using (System.Drawing.Bitmap finalCardImage = Core.PreviewCardExporter.ExportCard(
                correctedUrl, logoStream, typeText, nameText, versionText, modelsText))
            {
              // 3. Сохраняем готовую скомпонованную карточку в выбранный пользователем путь
              finalCardImage.Save(saveDlg.FileName, System.Drawing.Imaging.ImageFormat.Png);
            }
          }

          // Динамический перевод диалогового окна успеха
          string successMessage = LanguageManager.GetString("SaveSuccessMessage");
          string successTitle = LanguageManager.GetString("SaveSuccessTitle");

          MessageBox.Show(successMessage, successTitle, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
          // Динамический перевод диалогового окна ошибки
          string errorMessage = LanguageManager.GetString("SaveErrorMessage");
          string errorTitle = LanguageManager.GetString("SaveErrorTitle");

          MessageBox.Show($"{errorMessage} {ex.Message}", errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
      }
    }

    // Метод, который срабатывает при нажатии на кнопку "Clear"
    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
      // 1. Очищаем текстовое поле ввода URL-ссылки с GitHub
      if (UrlTextBox != null)
      {
        UrlTextBox.Text = string.Empty;
      }

      // 2. Очищаем текстовое поле ввода названия (Name)
      if (NameTextBox != null)
      {
        NameTextBox.Text = string.Empty;
      }

      // 3. Очищаем текстовое поле ввода версии (Version)
      if (VersionTextBox != null)
      {
        VersionTextBox.Text = string.Empty;
      }

      // 4. Сбрасываем выбор типа на значение по умолчанию ("Не выбран" / None)
      if (RadioNone != null)
      {
        RadioNone.IsChecked = true;
      }

      // 5. Полностью перезагружаем выпадающий список разрешений, возвращая плейсхолдер на первое место
      LoadResolutions(true);

      // 6. Очищаем список моделей часов на панели выбора (скрываем чекбоксы)
      if (ModelsItemsControl != null)
      {
        ModelsItemsControl.ItemsSource = null;
      }

      // 7. Очищаем текст выбранных моделей под карточкой предпросмотра
      if (ModelsListText != null)
      {
        ModelsListText.Text = string.Empty;
      }

      // 8. Очищаем все текстовые надписи на самой карточке предпросмотра (тип, имя, версия)
      ClearPreview();
      // Сбрасываем состояние массового выбора при смене разрешения или очистке
      _isAllModelsSelected = false;
      UpdateGroupBoxHeaderText();

      // 9. Возвращаем центральное изображение предпросмотра к исходному логотипу-заглушке
      if (QrPreviewImage != null)
      {
        QrPreviewImage.Source = new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/Assets/Icons/logo_qr.png", UriKind.Absolute));
      }
    }

    // ==========================================
    // Win32 API для работы с мониторами и панелью задач
    // ==========================================
    private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct RECT
    {
      public int Left;
      public int Top;
      public int Right;
      public int Bottom;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MONITORINFO
    {
      public uint cbSize;
      public RECT rcMonitor;
      public RECT rcWork;
      public uint dwFlags;
    }

    // ==========================================
    // Логика инициализации и позиционирования
    // ==========================================

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
      System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(this);
      System.Windows.Interop.HwndSource source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
      source.AddHook(HwndHook);

      ApplyMonitorScalingAndBounds(true);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
      const int WM_DPICHANGED = 0x02E0;
      const int WM_EXITSIZEMOVE = 0x0232; // Срабатывает в момент, когда пользователь отпускает мышь (завершил перетаскивать окно)

      if (msg == WM_DPICHANGED)
      {
        ApplyMonitorScalingAndBounds(true);
      }
      else if (msg == WM_EXITSIZEMOVE)
      {
        // Перемещение завершено — идеально ровно ставим окно без мерцания
        ApplyMonitorScalingAndBounds(false);

        // ПОЛУЧАЕМ И СОХРАНЯЕМ ФИНАЛЬНЫЕ КООРДИНАТЫ ОКНА НА ЭКРАНЕ
        System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(this);
        GetWindowRect(helper.Handle, out RECT rect);

        SettingsManager.Current.WindowLeft = rect.Left;
        SettingsManager.Current.WindowTop = rect.Top;
        SettingsManager.Save(); // Записываем в UserSetting.json
      }

      return IntPtr.Zero;
    }

    /// <summary>
    /// Точно позиционирует и масштабирует окно с фиксированными рамками без изменения ширины на границах мониторов.
    /// </summary>
    private void ApplyMonitorScalingAndBounds(bool isInitial)
    {
      // Исходные базовые размеры окна и его пропорция (4:3)
      double baseWidth = 800.0;
      double baseHeight = 600.0;
      double baseClientWidth = 784.0;
      double baseClientHeight = 561.0;
      double aspectRatio = baseWidth / baseHeight;

      System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(this);
      if (helper.Handle == IntPtr.Zero) return;

      // 1. Точное определение DPI конкретно для этого окна через Win32 API
      double dpiScaleX = 1.0;
      double dpiScaleY = 1.0;

      try
      {
        uint dpi = GetDpiForWindow(helper.Handle);
        if (dpi > 0)
        {
          dpiScaleX = dpi / 96.0;
          dpiScaleY = dpi / 96.0;
        }
      }
      catch
      {
        var hwndSource = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
        if (hwndSource?.CompositionTarget != null)
        {
          dpiScaleX = hwndSource.CompositionTarget.TransformToDevice.M11;
          dpiScaleY = hwndSource.CompositionTarget.TransformToDevice.M22;
        }
      }

      if (dpiScaleX <= 0) dpiScaleX = 1.0;
      if (dpiScaleY <= 0) dpiScaleY = 1.0;

      IntPtr hMonitor = MonitorFromWindow(helper.Handle, MONITOR_DEFAULTTONEAREST);
      MONITORINFO mi = new MONITORINFO();
      mi.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(MONITORINFO));

      if (GetMonitorInfo(hMonitor, ref mi))
      {
        int workLeft = mi.rcWork.Left;
        int workTop = mi.rcWork.Top;
        int workWidth = mi.rcWork.Right - mi.rcWork.Left;
        int workHeight = mi.rcWork.Bottom - mi.rcWork.Top;

        // 2. Статическая рамка Windows (7px при 100% DPI).
        // Отказываемся от DWM, чтобы ширeна не прыгала на границах мониторов.
        int frameBorderX = (int)Math.Round(7 * dpiScaleX);
        int frameBorderY = (int)Math.Round(7 * dpiScaleY);

        // Рассчитываем предельный масштаб
        double maxScaleY = (double)workHeight / (baseHeight * dpiScaleY);
        double maxScaleX = (double)workWidth / (baseWidth * dpiScaleX);

        double windowUniformScale = Math.Min(1.0, Math.Min(maxScaleX, maxScaleY));

        int targetVisualHeight = (int)Math.Round(baseHeight * dpiScaleY * windowUniformScale);
        if (targetVisualHeight > workHeight) targetVisualHeight = workHeight;

        // Фиксируем ширину СТРОГО из высоты по соотношению 4:3
        int targetVisualWidth = (int)Math.Round(targetVisualHeight * aspectRatio);

        RECT windowRect;
        GetWindowRect(helper.Handle, out windowRect);

        int targetVisualY = windowRect.Top;
        if (targetVisualHeight >= workHeight)
        {
          targetVisualY = workTop;
        }
        else
        {
          if (targetVisualY + targetVisualHeight > mi.rcWork.Bottom)
            targetVisualY = mi.rcWork.Bottom - targetVisualHeight;
          if (targetVisualY < workTop)
            targetVisualY = workTop;
        }

        int targetVisualX = windowRect.Left + frameBorderX;

        // Восстановление позиции из настроек или центрирование при первом запуске
        if (isInitial)
        {
          var settings = SettingsManager.Current;

          // Проверяем, есть ли сохраненные координаты в настройках
          if (settings.WindowLeft.HasValue && settings.WindowTop.HasValue)
          {
            targetVisualX = settings.WindowLeft.Value;
            targetVisualY = settings.WindowTop.Value;

            // ЗАЩИТА: Если у пользователя отключился второй монитор и окно оказалось "за краем" экрана, 
            // принудительно возвращаем его в центр текущего рабочего монитора.
            if (targetVisualX < mi.rcMonitor.Left || targetVisualX > mi.rcMonitor.Right - 50 ||
                targetVisualY < mi.rcMonitor.Top || targetVisualY > mi.rcMonitor.Bottom - 50)
            {
              targetVisualX = workLeft + (workWidth - targetVisualWidth) / 2;
              targetVisualY = workTop + (workHeight - targetVisualHeight) / 2;
            }
          }
          else
          {
            // Настроек нет (первый запуск) — просто центрируем окно на экране
            targetVisualX = workLeft + (workWidth - targetVisualWidth) / 2;
            targetVisualY = workTop + (workHeight - targetVisualHeight) / 2;
            if (targetVisualHeight >= workHeight) targetVisualY = workTop;
          }
        }

        // Вычисляем финальные размеры без плавающих рамок DWM
        int finalX = targetVisualX - frameBorderX;
        int finalY = targetVisualY;
        int finalWidth = targetVisualWidth + (frameBorderX * 2);
        int finalHeight = targetVisualHeight + frameBorderY;

        MoveWindow(helper.Handle, finalX, finalY, finalWidth, finalHeight, true);

        // 3. Масштабирование внутреннего содержимого WPF
        GetClientRect(helper.Handle, out RECT clientRect);
        double clientWpfWidth = (clientRect.Right - clientRect.Left) / dpiScaleX;
        double clientWpfHeight = (clientRect.Bottom - clientRect.Top) / dpiScaleY;

        if (clientWpfWidth > 100 && clientWpfHeight > 100)
        {
          double contentScaleX = clientWpfWidth / baseClientWidth;
          double contentScaleY = clientWpfHeight / baseClientHeight;

          double contentUniformScale = Math.Min(contentScaleX, contentScaleY);

          AppScaleTransform.ScaleX = contentUniformScale;
          AppScaleTransform.ScaleY = contentUniformScale;

          if (RootLayoutGrid != null)
          {
            RootLayoutGrid.Width = baseClientWidth;
            RootLayoutGrid.Height = baseClientHeight;
            RootLayoutGrid.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            RootLayoutGrid.VerticalAlignment = System.Windows.VerticalAlignment.Center;
          }
        }
      }
    }
  }
}