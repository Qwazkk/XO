using System;
using System.Windows;

namespace XO
{
    public partial class LoginWindow : Window
    {
        public LoginWindow() { InitializeComponent(); }

        private void OnLoginClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtNickname.Text))
            {
                MessageBox.Show("Введите ник!");
                return;
            }
            var main = new MainWindow(TxtNickname.Text);
            main.Show();
            this.Close();
        }
    }
}