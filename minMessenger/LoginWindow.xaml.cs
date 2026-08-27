using MinMessenger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace minMessenger
{
    /// <summary>
    /// Логика взаимодействия для LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        private bool _isRegisterMode = false;
        private readonly AppDbContext _db = new AppDbContext();

        public LoginWindow()
        {
            InitializeComponent();
        }

        private void SwitchMode_Click(object sender, RoutedEventArgs e)
        {
            _isRegisterMode = !_isRegisterMode;

            if (_isRegisterMode)
            {
                ActionButton.Content = "Зарегистрироваться";
                NameLabel.Visibility = Visibility.Visible;
                NameBox.Visibility = Visibility.Visible;
                SwitchLink.Inlines.Clear();
                SwitchLink.Inlines.Add(new Run("Уже есть аккаунт? Войти"));
                Title = "MinMessenger — Регистрация";
            }
            else
            {
                ActionButton.Content = "Войти";
                NameLabel.Visibility = Visibility.Collapsed;
                NameBox.Visibility = Visibility.Collapsed;
                SwitchLink.Inlines.Clear();
                SwitchLink.Inlines.Add(new Run("Нет аккаунта? Зарегистрироваться"));
                Title = "MinMessenger — Вход";
            }

            ErrorText.Text = "";
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginBox.Text.Trim();
            string password = PasswordBox.Password;
            string displayName = NameBox.Text.Trim();

            ErrorText.Text = "";

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                ErrorText.Text = "Заполни логин и пароль";
                return;
            }

            if (_isRegisterMode)
            {
                // ===== РЕГИСТРАЦИЯ =====
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    ErrorText.Text = "Укажи отображаемое имя";
                    return;
                }

                if (_db.Users.Any(u => u.Login == login))
                {
                    ErrorText.Text = "Такой логин уже занят";
                    return;
                }

                var newUser = new User
                {
                    Login = login,
                    PasswordHash = password, // для диплома можно так (в реале хешировать!)
                    DisplayName = displayName,
                    CreatedAt = DateTime.Now
                };

                _db.Users.Add(newUser);
                _db.SaveChanges();

                OpenMainWindow(newUser.Id);
            }
            else
            {
                // ===== ВХОД =====
                var user = _db.Users.FirstOrDefault(u => u.Login == login && u.PasswordHash == password);

                if (user == null)
                {
                    ErrorText.Text = "Неверный логин или пароль";
                    return;
                }

                OpenMainWindow(user.Id);
            }
        }

        private void OpenMainWindow(int userId)
        {
            var main = new MainWindow(userId); // передаём id пользователя
            main.Show();
            this.Close();
        }
    }
}
