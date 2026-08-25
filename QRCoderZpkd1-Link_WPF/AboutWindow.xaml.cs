using System.Windows;
using QRCoderZpkd1_Link.Core; // Подключаем пространство имен с нашим менеджером языков
using System.Diagnostics;
using System.Windows.Navigation;

namespace QRCoderZpkd1_Link // Убедитесь, что это совпадает с x:Class в XAML
{
  public partial class AboutWindow : Window
  {
    // Конструктор по умолчанию (нужен для WPF)
    public AboutWindow()
    {
      InitializeComponent();
    }

    // Конструктор с передачей версии
    public AboutWindow(string version) : this() // Вызывает конструктор по умолчанию
    {
      // Получаем переведенное слово (например, "Версия:") и склеиваем с номером версии
      string versionLabel = LanguageManager.GetString("Version");
      VersionTextBlock.Text = $"{versionLabel} {version}";
    }

    // Обработчик нажатия на ссылки профилей GitHub
    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
      try
      {
        Process.Start(new ProcessStartInfo
        {
          FileName = e.Uri.AbsoluteUri,
          UseShellExecute = true
        });
      }
      catch
      {
        // Обработка возможного отсутствия браузера по умолчанию
      }
      e.Handled = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
      this.Close();
    }
  }
}