using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sma5hMusic.GUI.Helpers
{
    public class ControlWriter : TextWriter
    {
        private const int MaxDisplayedLines = 1000;
        private readonly TextBox _textbox;
        private readonly ScrollViewer _scrollViewer;
        private readonly ConcurrentQueue<string> _queuedMessages;
        private readonly Queue<string> _displayedLines;
        private readonly StringBuilder _currentDisplayedLine;
        private readonly CancellationTokenSource _cts;

        public ControlWriter(TextBox textbox, ScrollViewer scrollViewer)
        {
            _textbox = textbox;
            _scrollViewer = scrollViewer;
            _queuedMessages = new ConcurrentQueue<string>();
            _displayedLines = new Queue<string>();
            _currentDisplayedLine = new StringBuilder();
            _cts = new CancellationTokenSource();
            _ = Task.Run(async() => await RunQueue(_cts.Token));
        }

        public override void Write(char value)
        {
            _queuedMessages.Enqueue(value.ToString());
        }

        public override void Write(string value)
        {
            if(!string.IsNullOrWhiteSpace(value))
                _queuedMessages.Enqueue($"{DateTime.Now:yyyy-MM-ddTHH\\:mm\\:ss}: {value}\r\n");
        }

        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }

        private async Task RunQueue(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var messagesToPrint = new StringBuilder();
                int i = 0;
                while (i < 100 && _queuedMessages.TryDequeue(out string newMessage))
                {
                    messagesToPrint.Append(newMessage);
                    i++;
                }
                if (messagesToPrint.Length > 0)
                {
                    var textToPrint = messagesToPrint.ToString();
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        foreach (var character in textToPrint)
                        {
                            //ignore Windows line ending, might screw up the line
                            if (character == '\r')
                                continue;

                            if (character == '\n')
                            {
                                _displayedLines.Enqueue(_currentDisplayedLine + "\r\n");
                                _currentDisplayedLine.Clear();
                                continue;
                            }

                            _currentDisplayedLine.Append(character);
                        }

                        //FIFO queue for lines
                        while (_displayedLines.Count > MaxDisplayedLines)
                            _displayedLines.Dequeue();

                        var displayedText = new StringBuilder();
                        foreach (var line in _displayedLines)
                            displayedText.Append(line);
                        displayedText.Append(_currentDisplayedLine);

                        _textbox.Text = displayedText.ToString();
                        _scrollViewer.ScrollToEnd();
                    }, DispatcherPriority.Background);
                }
                await Task.Delay(50);
            }
        }

        public override ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _cts?.Dispose();
            return base.DisposeAsync();
        }
    }
}
