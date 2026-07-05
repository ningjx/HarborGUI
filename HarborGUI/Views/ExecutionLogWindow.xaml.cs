using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Threading;

namespace HarborGUI.Views;

public partial class ExecutionLogWindow : Window
{
    private readonly ObservableCollection<string> _messages;

    public ExecutionLogWindow(ObservableCollection<string> messages)
    {
        InitializeComponent();
        _messages = messages;

        // 初始加载已有内容
        RefreshContent();

        // 实时监听新消息
        _messages.CollectionChanged += OnMessagesChanged;
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            Dispatcher.Invoke(() =>
            {
                foreach (var item in e.NewItems)
                    LogContent.AppendText(item + Environment.NewLine);
                LogScroller.ScrollToEnd();
            });
        }
    }

    private void RefreshContent()
    {
        LogContent.Text = _messages.Count > 0
            ? string.Join(Environment.NewLine, _messages) + Environment.NewLine
            : "(暂无日志)";
        LogScroller.ScrollToEnd();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
