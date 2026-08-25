using System;
using System.Windows;
using System.Windows.Threading;
using System.Collections.Generic;

namespace QRCoderZpkd1_Link
{
  /// Toast-уведомление.
  ///  Окно является исключительно UI-компонентом. Core о существовании этого класса не знает.
  public partial class ToastNotificationWindow : Window
  {
    // Хранит все одновременно открытые Toast для расчёта вертикального расположения.
    private static readonly List<ToastNotificationWindow> OpenToasts =
        new List<ToastNotificationWindow>();

    /// Время автоматического отображения Toast.
    /// 10 секунд находятся внутри заданного диапазона 5-30 секунд.
    private static readonly TimeSpan DisplayDuration =
        TimeSpan.FromSeconds(10);

    /// Таймер автоматического закрытия.
    private readonly DispatcherTimer _closeTimer;

    /// Текст заголовка Toast. Используется XAML через Binding.
    public string ToastTitle { get; }

    /// Основной текст Toast. Используется XAML через Binding.
    public string ToastMessage { get; }

    /// Создаёт Toast с локализованным заголовком и локализованным сообщением.
    public ToastNotificationWindow(
        string title,
        string message)
    {
      // Сохраняем локализованный заголовок.
      ToastTitle = title;

      // Сохраняем локализованное сообщение.
      ToastMessage = message;

      // Создаём визуальную часть окна.
      InitializeComponent();

      // Назначаем DataContext текущего Toast.
      // Благодаря этому XAML может использовать: {Binding ToastTitle} {Binding ToastMessage}
      DataContext = this;

      // Создаём таймер автоматического закрытия.
      _closeTimer = new DispatcherTimer
      {
        Interval = DisplayDuration
      };

      // Подписываемся на событие таймера.
      _closeTimer.Tick += CloseTimer_Tick;

      // После загрузки окна рассчитываем его положение и запускаем таймер.
      Loaded += ToastNotificationWindow_Loaded;
    }

    /// Показывает Toast в правом нижнем углу рабочей области основного монитора. Каждый следующий Toast располагается выше предыдущего с зазором 5 пикселей.
    private void ToastNotificationWindow_Loaded(
    object sender,
    RoutedEventArgs e)
    {
      // Откладываем расчёт до завершения измерения SizeToContent.
      Dispatcher.BeginInvoke(new Action(() =>
      {
        // Получаем рабочую область Windows без панели задач.
        var workArea = SystemParameters.WorkArea;

        // Начинаем расчёт от нижнего края рабочей области с зазором 10 пикселей.
        double currentBottom = workArea.Bottom - 10;

        // Учитываем уже открытые Toast.
        foreach (var toast in OpenToasts)
        {
          currentBottom -= toast.ActualHeight;
          currentBottom -= 5;
        }

        // Добавляем текущий Toast в список открытых.
        OpenToasts.Add(this);

        // Смещение от правого края монитора 10 пикселей.
        Left =
            workArea.Right -
            ActualWidth -
            10;

        // Располагаем Toast над предыдущими уведомлениями.
        Top =
            currentBottom -
            ActualHeight;

        // Запускаем таймер закрытия.
        _closeTimer.Start();

      }), DispatcherPriority.Loaded);
    }

    /// Закрывает Toast после истечения 30 секунд.
    private void CloseTimer_Tick(
        object sender,
        EventArgs e)
    {
      // Останавливаем таймер перед закрытием.
      _closeTimer.Stop();

      // Закрываем окно.
      Close();
    }

    /// Обработчик кнопки ×. Пользователь может закрыть Toast в любой момент самостоятельно.
    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
      // Закрываем Toast немедленно. OnClosed() ниже дополнительно остановит таймер.
      Close();
    }

    /// Гарантированно останавливает таймер при любом способе закрытия окна.
    protected override void OnClosed(
    EventArgs e)
    {
      // Останавливаем таймер.
      _closeTimer.Stop();

      // Удаляем закрытый Toast из списка активных окон.
      OpenToasts.Remove(this);

      // Передаём управление базовому классу.
      base.OnClosed(e);
    }
  }
}