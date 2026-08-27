using Microsoft.EntityFrameworkCore;
using MinMessenger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace minMessenger
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    /// Data Source=DESKTOP-JDAG33F\SQLEXPRESS;
    /// Initial Catalog=MinMessenger;Integrated Security=True;
    /// Connect Timeout=30;Encrypt=True;Trust Server Certificate=True;
    /// Application Intent=ReadWrite;Multi Subnet Failover=False;Command Timeout=30
    public partial class MainWindow : Window
    {
        private readonly AppDbContext _db = new AppDbContext();
        private int _currentUserId;
        private int? _selectedChatId = null;

        public MainWindow(int userId)
        {
            InitializeComponent();
            _currentUserId = userId;
            LoadChats();
        }

        // ====================== ЗАГРУЗКА СПИСКА ЧАТОВ ======================
        private void LoadChats()
        {
            ChatsListBox.Items.Clear();

            var myChatIds = _db.ChatMembers
                .Where(cm => cm.UserId == _currentUserId)
                .Select(cm => cm.ChatId)
                .ToList();

            var existingChats = _db.Chats
                .Where(c => myChatIds.Contains(c.Id))
                .Include(c => c.Members)
                    .ThenInclude(m => m.User)
                .ToList();

            foreach (var chat in existingChats)
            {
                string title;
                if (chat.Type == "group")
                {
                    title = "👥 " + (chat.Title ?? "Группа");
                }
                else
                {
                    var other = chat.Members.FirstOrDefault(m => m.UserId != _currentUserId);
                    title = other != null && other.User != null ? other.User.DisplayName : "Неизвестный";
                }

                var lastMsg = _db.Messages
                    .Where(m => m.ChatId == chat.Id && !m.IsDeleted)
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefault();

                string lastText = lastMsg != null ? lastMsg.Content : "Нет сообщений";
                DateTime? lastTime = lastMsg != null ? lastMsg.SentAt : (DateTime?)null;

                AddChatToList(chat.Id, title, lastText, lastTime);
            }

            // Пользователи, с которыми ещё нет личного чата
            var usersWithPrivateChat = existingChats
                .Where(c => c.Type == "private")
                .SelectMany(c => c.Members)
                .Where(m => m.UserId != _currentUserId)
                .Select(m => m.UserId)
                .Distinct()
                .ToList();

            var otherUsers = _db.Users
                .Where(u => u.Id != _currentUserId && !usersWithPrivateChat.Contains(u.Id))
                .ToList();

            foreach (var user in otherUsers)
            {
                AddChatToList(-user.Id, user.DisplayName, "Начать переписку", null);
            }
        }

        private void AddChatToList(int id, string title, string lastMessage, DateTime? time)
        {
            var item = new ListBoxItem();
            item.Tag = id;
            item.Content = CreateChatItem(title, lastMessage, time);
            ChatsListBox.Items.Add(item);
        }

        private UIElement CreateChatItem(string title, string lastMessage, DateTime? time)
        {
            var grid = new Grid();
            grid.Margin = new Thickness(0, 2, 0, 2);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var avatar = new System.Windows.Shapes.Ellipse
            {
                Width = 46,
                Height = 46,
                Fill = new SolidColorBrush(Color.FromRgb(74, 144, 217))
            };
            Grid.SetColumn(avatar, 0);

            var stack = new StackPanel { Margin = new Thickness(12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold
            });

            string shortMsg = lastMessage.Length > 35 ? lastMessage.Substring(0, 35) + "..." : lastMessage;

            stack.Children.Add(new TextBlock
            {
                Text = shortMsg,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 154, 171)),
                FontSize = 13,
                Margin = new Thickness(0, 3, 0, 0)
            });

            Grid.SetColumn(stack, 1);

            var timeText = new TextBlock
            {
                Text = time.HasValue ? time.Value.ToString("HH:mm") : "",
                Foreground = new SolidColorBrush(Color.FromRgb(109, 127, 143)),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0)
            };
            Grid.SetColumn(timeText, 2);

            grid.Children.Add(avatar);
            grid.Children.Add(stack);
            grid.Children.Add(timeText);

            return grid;
        }

        // ====================== ВЫБОР ЧАТА ======================
        private void ChatsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = ChatsListBox.SelectedItem as ListBoxItem;
            if (item == null) return;

            int tag = (int)item.Tag;

            if (tag > 0)
            {
                _selectedChatId = tag;
                LoadMessages(tag);
            }
            else
            {
                int otherUserId = -tag;
                StartPrivateChat(otherUserId);
            }
        }

        // ====================== СОЗДАНИЕ ЛИЧНОГО ЧАТА ======================
        private void StartPrivateChat(int otherUserId)
        {
            var existingChat = _db.Chats
                .Include(c => c.Members)
                .FirstOrDefault(c => c.Type == "private" &&
                                     c.Members.Any(m => m.UserId == _currentUserId) &&
                                     c.Members.Any(m => m.UserId == otherUserId));

            if (existingChat != null)
            {
                _selectedChatId = existingChat.Id;
                LoadMessages(existingChat.Id);
                return;
            }

            var newChat = new Chat
            {
                Type = "private",
                Title = null,
                CreatedByUserId = _currentUserId,
                CreatedAt = DateTime.Now
            };

            _db.Chats.Add(newChat);
            _db.SaveChanges();

            _db.ChatMembers.Add(new ChatMember { ChatId = newChat.Id, UserId = _currentUserId, Role = "member", JoinedAt = DateTime.Now });
            _db.ChatMembers.Add(new ChatMember { ChatId = newChat.Id, UserId = otherUserId, Role = "member", JoinedAt = DateTime.Now });
            _db.SaveChanges();

            _selectedChatId = newChat.Id;
            LoadChats();
            LoadMessages(newChat.Id);
        }

        // ====================== ЗАГРУЗКА СООБЩЕНИЙ ======================
        private void LoadMessages(int chatId)
        {
            UpdateChatHeader(chatId);

            MessagesPanel.Children.Clear();

            var messages = _db.Messages
                .Where(m => m.ChatId == chatId && !m.IsDeleted)
                .OrderBy(m => m.SentAt)
                .Include(m => m.Sender)
                .ToList();

            foreach (var msg in messages)
            {
                bool isMine = msg.SenderId == _currentUserId;

                var border = new Border
                {
                    Background = isMine
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2B5278"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#182533")),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(14, 10, 14, 10),
                    Margin = isMine ? new Thickness(100, 5, 0, 5) : new Thickness(0, 5, 100, 5),
                    HorizontalAlignment = isMine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                    MaxWidth = 480
                };

                var stack = new StackPanel();

                // Имя отправителя (особенно полезно в группах)
                if (!isMine)
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = msg.Sender != null ? msg.Sender.DisplayName : "Неизвестный",
                        Foreground = new SolidColorBrush(Color.FromRgb(110, 180, 255)),
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 0, 0, 3)
                    });
                }

                // Текст сообщения
                stack.Children.Add(new TextBlock
                {
                    Text = msg.Content,
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14.5
                });

                // Время
                stack.Children.Add(new TextBlock
                {
                    Text = msg.SentAt.ToString("HH:mm"),
                    Foreground = new SolidColorBrush(Color.FromRgb(160, 175, 190)),
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 4, 0, 0)
                });

                border.Child = stack;
                MessagesPanel.Children.Add(border);
            }
        }
        // Показывать кнопку "Добавить участника" только для групп
        private void UpdateChatHeader(int chatId)
        {
            var chat = _db.Chats.FirstOrDefault(c => c.Id == chatId);
            if (chat == null) return;

            if (chat.Type == "group")
            {
                ChatTitleText.Text = "👥 " + (chat.Title ?? "Группа");
                AddMemberButton.Visibility = Visibility.Visible;
            }
            else
            {
                var other = _db.ChatMembers
                    .Include(m => m.User)
                    .FirstOrDefault(m => m.ChatId == chatId && m.UserId != _currentUserId);

                ChatTitleText.Text = other?.User?.DisplayName ?? "Чат";
                AddMemberButton.Visibility = Visibility.Collapsed;
            }
        }

        // ====================== ОТПРАВКА СООБЩЕНИЯ ======================
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedChatId == null) return;
            if (string.IsNullOrWhiteSpace(MessageTextBox.Text)) return;

            var newMessage = new Message
            {
                ChatId = _selectedChatId.Value,
                SenderId = _currentUserId,
                Content = MessageTextBox.Text.Trim(),
                SentAt = DateTime.Now,
                IsDeleted = false
            };

            _db.Messages.Add(newMessage);
            _db.SaveChanges();

            MessageTextBox.Clear();
            LoadMessages(_selectedChatId.Value);
            LoadChats();
        }

        // ====================== СОЗДАНИЕ ГРУППЫ ======================
        private void CreateGroup_Click(object sender, RoutedEventArgs e)
        {
            string groupName = Microsoft.VisualBasic.Interaction.InputBox(
                "Введите название группы:",
                "Новая группа",
                "Моя группа");

            if (string.IsNullOrWhiteSpace(groupName)) return;

            var group = new Chat
            {
                Type = "group",
                Title = groupName.Trim(),
                CreatedByUserId = _currentUserId,
                CreatedAt = DateTime.Now
            };

            _db.Chats.Add(group);
            _db.SaveChanges();

            _db.ChatMembers.Add(new ChatMember
            {
                ChatId = group.Id,
                UserId = _currentUserId,
                Role = "admin",
                JoinedAt = DateTime.Now
            });
            _db.SaveChanges();

            MessageBox.Show("Группа успешно создана!");
            LoadChats();
        }
        private void AddMember_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedChatId == null) return;

            // Получаем пользователей, которых ещё нет в этой группе
            var existingMemberIds = _db.ChatMembers
                .Where(m => m.ChatId == _selectedChatId.Value)
                .Select(m => m.UserId)
                .ToList();

            var availableUsers = _db.Users
                .Where(u => !existingMemberIds.Contains(u.Id))
                .ToList();

            if (availableUsers.Count == 0)
            {
                MessageBox.Show("Все пользователи уже в группе");
                return;
            }

            // Простой выбор через InputBox (для диплома сойдёт)
            string userList = string.Join("\n", availableUsers.Select(u => u.Id + " - " + u.DisplayName));

            string input = Microsoft.VisualBasic.Interaction.InputBox(
                "Введите ID пользователя, которого хотите добавить:\n\n" + userList,
                "Добавить участника",
                "");

            if (string.IsNullOrWhiteSpace(input)) return;

            int userIdToAdd;
            if (!int.TryParse(input.Trim(), out userIdToAdd))
            {
                MessageBox.Show("Некорректный ID");
                return;
            }

            if (!availableUsers.Any(u => u.Id == userIdToAdd))
            {
                MessageBox.Show("Такого пользователя нет в списке");
                return;
            }

            // Добавляем
            _db.ChatMembers.Add(new ChatMember
            {
                ChatId = _selectedChatId.Value,
                UserId = userIdToAdd,
                Role = "member",
                JoinedAt = DateTime.Now
            });
            _db.SaveChanges();

            MessageBox.Show("Пользователь добавлен в группу!");
        }
    }
}


/*
 [26.08.2026 21:17] Liren: using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;

namespace MinMessenger
{
    public partial class MainWindow : Window
    {
        private readonly AppDbContext _db = new AppDbContext();
        private int _currentUserId;
        private int? _selectedChatId = null;

        public MainWindow(int userId)
        {
            InitializeComponent();
            _currentUserId = userId;
            LoadChats();
        }

        // ====================== ЗАГРУЗКА СПИСКА ЧАТОВ ======================
        private void LoadChats()
        {
            ChatsListBox.Items.Clear();

            var myChatIds = _db.ChatMembers
                .Where(cm => cm.UserId == _currentUserId)
                .Select(cm => cm.ChatId)
                .ToList();

            var existingChats = _db.Chats
                .Where(c => myChatIds.Contains(c.Id))
                .Include(c => c.Members)
                    .ThenInclude(m => m.User)
                .ToList();

            foreach (var chat in existingChats)
            {
                string title;
                if (chat.Type == "group")
                {
                    title = "👥 " + (chat.Title ?? "Группа");
                }
                else
                {
                    var other = chat.Members.FirstOrDefault(m => m.UserId != _currentUserId);
                    title = other != null && other.User != null ? other.User.DisplayName : "Неизвестный";
                }

                var lastMsg = _db.Messages
                    .Where(m => m.ChatId == chat.Id && !m.IsDeleted)
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefault();

                string lastText = lastMsg != null ? lastMsg.Content : "Нет сообщений";
                DateTime? lastTime = lastMsg != null ? lastMsg.SentAt : (DateTime?)null;

                AddChatToList(chat.Id, title, lastText, lastTime);
            }

            // Пользователи, с которыми ещё нет личного чата
            var usersWithPrivateChat = existingChats
                .Where(c => c.Type == "private")
                .SelectMany(c => c.Members)
                .Where(m => m.UserId != _currentUserId)
                .Select(m => m.UserId)
                .Distinct()
                .ToList();

            var otherUsers = _db.Users
                .Where(u => u.Id != _currentUserId && !usersWithPrivateChat.Contains(u.Id))
                .ToList();

            foreach (var user in otherUsers)
            {
                AddChatToList(-user.Id, user.DisplayName, "Начать переписку", null);
            }
        }

        private void AddChatToList(int id, string title, string lastMessage, DateTime? time)
        {
            var item = new ListBoxItem();
            item.Tag = id;
            item.Content = CreateChatItem(title, lastMessage, time);
            ChatsListBox.Items.Add(item);
        }

        private UIElement CreateChatItem(string title, string lastMessage, DateTime? time)
        {
            var grid = new Grid();
            grid.Margin = new Thickness(0, 2, 0, 2);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var avatar = new System.Windows.Shapes.Ellipse
            {
                Width = 46,
                Height = 46,
                Fill = new SolidColorBrush(Color.FromRgb(74, 144, 217))
            };
            Grid.SetColumn(avatar, 0);

            var stack = new StackPanel { Margin = new Thickness(12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
[26.08.2026 21:17] Liren: stack.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold
            });

            string shortMsg = lastMessage.Length > 35 ? lastMessage.Substring(0, 35) + "..." : lastMessage;

            stack.Children.Add(new TextBlock
            {
                Text = shortMsg,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 154, 171)),
                FontSize = 13,
                Margin = new Thickness(0, 3, 0, 0)
            });

            Grid.SetColumn(stack, 1);

            var timeText = new TextBlock
            {
                Text = time.HasValue ? time.Value.ToString("HH:mm") : "",
                Foreground = new SolidColorBrush(Color.FromRgb(109, 127, 143)),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0)
            };
            Grid.SetColumn(timeText, 2);

            grid.Children.Add(avatar);
            grid.Children.Add(stack);
            grid.Children.Add(timeText);

            return grid;
        }

        // ====================== ВЫБОР ЧАТА ======================
        private void ChatsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = ChatsListBox.SelectedItem as ListBoxItem;
            if (item == null) return;

            int tag = (int)item.Tag;

            if (tag > 0)
            {
                _selectedChatId = tag;
                LoadMessages(tag);
            }
            else
            {
                int otherUserId = -tag;
                StartPrivateChat(otherUserId);
            }
        }

        // ====================== СОЗДАНИЕ ЛИЧНОГО ЧАТА ======================
        private void StartPrivateChat(int otherUserId)
        {
            var existingChat = _db.Chats
                .Include(c => c.Members)
                .FirstOrDefault(c => c.Type == "private" &&
                                     c.Members.Any(m => m.UserId == _currentUserId) &&
                                     c.Members.Any(m => m.UserId == otherUserId));

            if (existingChat != null)
            {
                _selectedChatId = existingChat.Id;
                LoadMessages(existingChat.Id);
                return;
            }

            var newChat = new Chat
            {
                Type = "private",
                Title = null,
                CreatedByUserId = _currentUserId,
                CreatedAt = DateTime.Now
            };

            _db.Chats.Add(newChat);
            _db.SaveChanges();

            _db.ChatMembers.Add(new ChatMember { ChatId = newChat.Id, UserId = _currentUserId, Role = "member", JoinedAt = DateTime.Now });
            _db.ChatMembers.Add(new ChatMember { ChatId = newChat.Id, UserId = otherUserId, Role = "member", JoinedAt = DateTime.Now });
            _db.SaveChanges();

            _selectedChatId = newChat.Id;
            LoadChats();
            LoadMessages(newChat.Id);
        }

        // ====================== ЗАГРУЗКА СООБЩЕНИЙ ======================
        private void LoadMessages(int chatId)
        {
            MessagesPanel.Children.Clear();

            var messages = _db.Messages
                .Where(m => m.ChatId == chatId && !m.IsDeleted)
                .OrderBy(m => m.SentAt)
                .Include(m => m.Sender)
                .ToList();

            foreach (var msg in messages)
            {
                bool isMine = msg.SenderId == _currentUserId;

                var border = new Border
                {
                    Background = isMine
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2B5278"))
[26.08.2026 21:17] Liren: : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#182533")),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(14, 10, 14, 10),
                    Margin = isMine ? new Thickness(100, 5, 0, 5) : new Thickness(0, 5, 100, 5),
                    HorizontalAlignment = isMine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                    MaxWidth = 480
                };

                var textBlock = new TextBlock
                {
                    Text = msg.Content,
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14.5
                };

                border.Child = textBlock;
                MessagesPanel.Children.Add(border);
            }
        }

        // ====================== ОТПРАВКА СООБЩЕНИЯ ======================
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedChatId == null) return;
            if (string.IsNullOrWhiteSpace(MessageTextBox.Text)) return;

            var newMessage = new Message
            {
                ChatId = _selectedChatId.Value,
                SenderId = _currentUserId,
                Content = MessageTextBox.Text.Trim(),
                SentAt = DateTime.Now,
                IsDeleted = false
            };

            _db.Messages.Add(newMessage);
            _db.SaveChanges();

            MessageTextBox.Clear();
            LoadMessages(_selectedChatId.Value);
            LoadChats();
        }

        // ====================== СОЗДАНИЕ ГРУППЫ ======================
        private void CreateGroup_Click(object sender, RoutedEventArgs e)
        {
            string groupName = Microsoft.VisualBasic.Interaction.InputBox(
                "Введите название группы:",
                "Новая группа",
                "Моя группа");

            if (string.IsNullOrWhiteSpace(groupName)) return;

            var group = new Chat
            {
                Type = "group",
                Title = groupName.Trim(),
                CreatedByUserId = _currentUserId,
                CreatedAt = DateTime.Now
            };

            _db.Chats.Add(group);
            _db.SaveChanges();

            _db.ChatMembers.Add(new ChatMember
            {
                ChatId = group.Id,
                UserId = _currentUserId,
                Role = "admin",
                JoinedAt = DateTime.Now
            });
            _db.SaveChanges();

            MessageBox.Show("Группа успешно создана!");
            LoadChats();
        }
    }
}
 */