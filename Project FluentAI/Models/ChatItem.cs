using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Project_FluentAI.Models
{
    public class Message : INotifyPropertyChanged
    {
        private string _content;
        public string Content 
        { 
            get => _content; 
            set { _content = value; OnPropertyChanged(); }
        }
        public DateTime Timestamp { get; set; }
        public bool IsUser { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public Message()
        {
            Timestamp = DateTime.Now;
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class ChatItem : INotifyPropertyChanged
    {
        private string _title;
        private bool _isPinned;

        public string Id { get; set; }
        
        public string Title 
        { 
            get => _title; 
            set { _title = value; OnPropertyChanged(); }
        }

        /// <summary>Whether this chat is pinned to the top of the list.</summary>
        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (_isPinned != value)
                {
                    _isPinned = value;
                    OnPropertyChanged();
                    // Also notify PinLabel so the context-menu text updates
                    OnPropertyChanged(nameof(PinLabel));
                }
            }
        }

        /// <summary>Computed label used by the Pin/Unpin context-menu item.</summary>
        public string PinLabel => _isPinned ? "Unpin" : "Pin";

        public ObservableCollection<Message> Messages { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public ChatItem(string title)
        {
            Id = Guid.NewGuid().ToString();
            Title = title;
            Messages = new ObservableCollection<Message>();
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
