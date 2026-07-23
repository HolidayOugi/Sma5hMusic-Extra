using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Sma5hMusic.GUI.Controls;
using System;

namespace Sma5hMusic.GUI.Views
{
    public class MSBTFieldView : UserControl
    {
        private MsbtRichTextEditorController _richTextController;

        public MSBTFieldView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            CreateRichTextController();
        }

        private void CreateRichTextController()
        {
            if (_richTextController != null)
                return;

            _richTextController = new MsbtRichTextEditorController(
                this.FindControl<TextBox>("RichTextEditor"),
                this.FindControl<StackPanel>("RichTextPreview"),
                this.FindControl<ComboBox>("TextColorComboBox"),
                this.FindControl<Button>("SmallTextMarkerButton"));
            _richTextController.SetDataContext(DataContext);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            _richTextController?.SetDataContext(DataContext);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            CreateRichTextController();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _richTextController?.Dispose();
            _richTextController = null;
            base.OnDetachedFromVisualTree(e);
        }
    }
}
